using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkdayService
{
    private sealed record DepartmentRole(long DepartmentId, long RoleId);
    private IQueryable<DepartmentRole> DepartmentRoles(long branch)
    {
        var now = Now;
        return from m in db.Memberships.AsNoTracking()
               join u in db.Users on m.UserId equals u.UserId
               join ur in db.UserRoles on u.UserId equals ur.UserId
               join r in db.Roles on ur.RoleId equals r.RoleId
               where m.BranchId == branch && m.DepartmentId != null && m.Kind == "EMPLOYMENT"
                   && m.ValidFrom <= now && (m.ValidTo == null || m.ValidTo > now)
                   && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
                   && ur.BranchId == branch && ur.AssignedAt <= now && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now)
                   && r.IsActive && db.RolePermissions.Any(g => g.RoleId == r.RoleId
                       && (g.DataScope == "ALL" || g.DataScope == "BRANCH" || g.DataScope == "OWN")
                       && db.Permissions.Any(p => p.PermissionId == g.PermissionId && p.IsActive && p.Code == "ATTENDANCE_RECORD"))
               select new DepartmentRole(m.DepartmentId.GetValueOrDefault(), r.RoleId);
    }
    public static DateOnly[] SchedulingDates(DateOnly[]? dates, DateOnly today)
    {
        Output.Require(dates is { Length: > 0 and <= 31 }, "SHIFT_DATES_REQUIRED", 400);
        var result = dates!.Distinct().Order().ToArray();
        Output.Require(result.Length == dates!.Length, "SHIFT_DUPLICATE_DATES", 400);
        Output.Require(result[0] >= today && result[^1] <= today.AddDays(366), "SHIFT_DATE_OUT_OF_RANGE", 400);
        Output.Require(result.All(d => d.Year == result[0].Year && d.Month == result[0].Month)
            || result.All(d => WeekStart(d) == WeekStart(result[0])), "SHIFT_ONE_MONTH_OR_WEEK_REQUIRED", 400);
        return result;
    }

    public async Task<WorkdayCandidatesDto> BatchCandidates(long shiftId, DateOnly[] dates, CancellationToken ct)
    {
        var shift = await db.WorkdayShifts.AsNoTracking().SingleOrDefaultAsync(s => s.ShiftId == shiftId, ct)
            ?? throw new ApiException(404, "SHIFT_NOT_FOUND");
        await access.Require("SHIFT_MANAGE", shift.BranchId, ct: ct);
        var workDates = SchedulingDates(dates, await Today(shift.BranchId, ct));
        Output.Require(shift.IsActive, "SHIFT_INACTIVE");
        await RequireShiftNotEnded(shift, workDates, ct);
        WorkdayCandidateDto[]? users = null;
        var slots = new List<WorkdaySlotDto>();
        foreach (var date in workDates)
        {
            var busy = from a in db.WorkdayAssignments.AsNoTracking()
                       join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                       where a.WorkDate == date && a.Status != "CANCELLED"
                           && s.StartMinute < shift.EndMinute && s.EndMinute > shift.StartMinute
                       select a.UserId;
            var eligible = await (await EligibleQuery(shift, date, ct)).Where(u => !busy.Contains(u.UserId))
                .OrderBy(u => u.FullName).Select(u => new WorkdayCandidateDto(u.UserId, u.EmployeeCode, u.FullName)).ToArrayAsync(ct);
            users = users == null ? eligible : users.Where(u => eligible.Any(e => e.UserId == u.UserId)).ToArray();
            var count = await db.WorkdayAssignments.AsNoTracking().CountAsync(a => a.ShiftId == shiftId
                && a.WorkDate == date && a.Status != "CANCELLED", ct);
            slots.Add(new(date, Math.Max(0, shift.Capacity - count)));
        }
        var allowed = new List<WorkdayCandidateDto>();
        foreach (var user in users ?? [])
        {
            try { await access.EligibleFor(user.UserId, shift.BranchId, "ATTENDANCE_RECORD", ct); allowed.Add(user); }
            catch (ApiException e) when (e.Code is "ASSIGNEE_EXECUTION_PERMISSION_REQUIRED" or "ASSIGNEE_NOT_ACTIVE_IN_BRANCH") { }
        }
        var reviews = await HoursReviews(shift, workDates, allowed.Select(u => u.UserId).ToArray(), ct);
        return new(allowed.Select(u => u with { HourWarnings = reviews[u.UserId].Warnings, HoursToken = reviews[u.UserId].Token }).ToArray(), slots.ToArray());
    }

    public Task<object> AssignBatch(AssignShiftsRequest request, CancellationToken ct) => atomic.Run(async () =>
    {
        await Lock(ct);
        Output.Key(request.OperationKey);
        var shift = await db.WorkdayShifts.SingleOrDefaultAsync(s => s.ShiftId == request.ShiftId, ct)
            ?? throw new ApiException(404, "SHIFT_NOT_FOUND");
        await access.Require("SHIFT_MANAGE", shift.BranchId, ct: ct);
        Output.Version(shift.RowVersion, request.RowVersion);
        Output.Require(shift.IsActive, "SHIFT_INACTIVE");
        var dates = SchedulingDates(request.WorkDates, await Today(shift.BranchId, ct));
        Output.Require(request.UserIds is { Length: > 0 and <= 100 } && request.UserIds.All(id => id > 0)
            && request.UserIds.Distinct().Count() == request.UserIds.Length, "SHIFT_EMPLOYEES_REQUIRED", 400);
        Output.Require(!await db.WorkdayEvents.AnyAsync(e => e.OperationKey == request.OperationKey, ct), "DUPLICATE_RECORD");
        Output.Require(request.HolidaysAcknowledged || !dates.Any(d => WorkdayCalendar.Markers(d.Year).Any(m => m.Date == d)),
            "SHIFT_HOLIDAY_ACKNOWLEDGEMENT_REQUIRED", 400);
        var candidates = await BatchCandidates(shift.ShiftId, dates, ct);
        Output.Require(candidates.Slots.All(s => s.Remaining >= request.UserIds.Length), "SHIFT_CAPACITY_REACHED");
        Output.Require(request.UserIds.All(id => candidates.Users.Any(u => u.UserId == id)), "SHIFT_EMPLOYEE_NOT_ELIGIBLE");
        foreach (var id in request.UserIds)
        {
            var candidate = candidates.Users.Single(u => u.UserId == id);
            ConfirmHours(new(candidate.HourWarnings ?? [], candidate.HoursToken!), request.HoursAcknowledgements?.GetValueOrDefault(id));
        }
        // Validate the complete selection before adding anything; Atomic + the shared lock also cover single assignment/actions.
        var rows = dates.SelectMany(date => request.UserIds.Order().Select(id => new WorkdayAssignment
        {
            ShiftId = shift.ShiftId, UserId = id, WorkDate = date, CreatedBy = actor.Id, CreatedAt = Now
        })).ToArray();
        db.WorkdayAssignments.AddRange(rows);
        await db.SaveChangesAsync(ct);
        for (var i = 0; i < rows.Length; i++)
        {
            // Existing history has a UNIQUE operation_key per event, so a batch needs deterministic child keys.
            var key = i == 0 ? request.OperationKey : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{request.OperationKey:N}:{rows[i].UserId}:{rows[i].WorkDate:yyyy-MM-dd}"))[..16]);
            var candidate = candidates.Users.Single(u => u.UserId == rows[i].UserId);
            var hours = new WorkdayHoursReview(candidate.HourWarnings ?? [], candidate.HoursToken!);
            Event(rows[i], "ASSIGN", key, HoursReason(hours, rows[i].WorkDate));
            audit.Add("Workday", rows[i].AssignmentId, "ASSIGN", shift.BranchId, null,
                hours.Warnings.Length == 0 ? (object)Output.Record(rows[i]) : new { assignment = Output.Record(rows[i]), hoursOverride = hours.Warnings }, key);
        }
        return (object)new { count = rows.Length, assignments = rows.Select(Output.Record).ToArray() };
    }, ct);

    public async Task<object> Configuration(long branch, CancellationToken ct)
    {
        await access.Require("SHIFT_MANAGE", branch, ct: ct);
        var departments = await db.Departments.AsNoTracking().Where(d => d.BranchId == branch && d.IsActive)
            .OrderBy(d => d.Name).Select(d => new { d.DepartmentId, d.Name }).ToArrayAsync(ct);
        var roles = await db.Roles.AsNoTracking().Where(r => r.IsActive && db.RolePermissions.Any(g => g.RoleId == r.RoleId
            && (g.DataScope == "ALL" || g.DataScope == "BRANCH" || g.DataScope == "OWN")
            && db.Permissions.Any(p => p.PermissionId == g.PermissionId && p.Code == "ATTENDANCE_RECORD" && p.IsActive)))
            .OrderBy(r => r.Name).Select(r => new { r.RoleId, r.Name }).ToArrayAsync(ct);
        var departmentRoles = await DepartmentRoles(branch).Distinct().ToArrayAsync(ct);
        var shifts = await (from s in db.WorkdayShifts.AsNoTracking()
                            join d in db.Departments on s.DepartmentId equals d.DepartmentId
                            join r in db.Roles on s.RoleId equals r.RoleId
                            where s.BranchId == branch orderby d.Name, s.Name
                            select new { s.ShiftId, s.DepartmentId, s.RoleId, s.Name, s.StartMinute, s.EndMinute,
                                s.Capacity, s.IsActive, s.RowVersion, department = d.Name, role = r.Name,
                                hasHistory = db.WorkdayAssignments.Any(a => a.ShiftId == s.ShiftId) }).ToArrayAsync(ct);
        return new { departments, roles, departmentRoles, shifts };
    }

    public Task<object> SaveShift(long? id, SaveWorkdayShiftRequest request, CancellationToken ct) => atomic.Run(async () =>
    {
        await Lock(ct);
        await access.Require("SHIFT_MANAGE", request.BranchId, ct: ct);
        Output.Key(request.OperationKey);
        Output.Require(!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length <= 150, "SHIFT_NAME_REQUIRED", 400);
        Output.Require(request.StartMinute >= 0 && request.EndMinute <= 1440 && request.StartMinute < request.EndMinute
            && request.Capacity is >= 1 and <= 100, "INVALID_SHIFT_DEFINITION", 400);
        var row = id == null ? new WorkdayShift { BranchId = request.BranchId }
            : await db.WorkdayShifts.SingleOrDefaultAsync(s => s.ShiftId == id && s.BranchId == request.BranchId, ct)
                ?? throw new ApiException(404, "SHIFT_NOT_FOUND");
        if (id != null) Output.Version(row.RowVersion, request.RowVersion ?? "");
        var before = id == null ? null : Output.Record(row);
        var hasHistory = id != null && await db.WorkdayAssignments.AnyAsync(a => a.ShiftId == id, ct);
        Output.Require(!hasHistory || (row.DepartmentId == request.DepartmentId && row.RoleId == request.RoleId
            && row.StartMinute == request.StartMinute && row.EndMinute == request.EndMinute), "SHIFT_HISTORY_DEFINITION_LOCKED");
        // Allow an existing definition to be deactivated even if its old role/department is already inactive.
        if (id == null || request.IsActive)
        {
            Output.Require(await db.Departments.AnyAsync(d => d.DepartmentId == request.DepartmentId
                && d.BranchId == request.BranchId && d.IsActive, ct), "SHIFT_DEPARTMENT_INVALID", 400);
            Output.Require(await db.Roles.AnyAsync(r => r.RoleId == request.RoleId && r.IsActive && db.RolePermissions.Any(g =>
                g.RoleId == r.RoleId && (g.DataScope == "ALL" || g.DataScope == "BRANCH" || g.DataScope == "OWN")
                && db.Permissions.Any(p => p.PermissionId == g.PermissionId && p.IsActive && p.Code == "ATTENDANCE_RECORD")), ct),
                "SHIFT_ROLE_INVALID", 400);
            var departmentRoles = await DepartmentRoles(request.BranchId).Distinct().ToArrayAsync(ct);
            Output.Require(departmentRoles.Any(x => x.DepartmentId == request.DepartmentId && x.RoleId == request.RoleId),
                "SHIFT_ROLE_DEPARTMENT_MISMATCH", 400);
        }
        Output.Require(request.IsActive || !await db.WorkdayAssignments.AnyAsync(a => a.ShiftId == id
            && (a.Status == "PLANNED" || a.Status == "WORKING"), ct), "SHIFT_HAS_OPEN_ASSIGNMENTS");
        Output.Require(!await db.WorkdayAssignments.Where(a => a.ShiftId == id && a.Status != "CANCELLED")
            .GroupBy(a => a.WorkDate).AnyAsync(g => g.Count() > request.Capacity, ct), "SHIFT_CAPACITY_BELOW_ASSIGNED");
        Output.Require(!await db.WorkdayShifts.AnyAsync(s => s.ShiftId != id && s.BranchId == request.BranchId
            && s.DepartmentId == request.DepartmentId && s.RoleId == request.RoleId
            && s.StartMinute == request.StartMinute && s.EndMinute == request.EndMinute, ct), "SHIFT_DEFINITION_EXISTS");
        row.DepartmentId = request.DepartmentId; row.RoleId = request.RoleId; row.Name = request.Name.Trim();
        row.StartMinute = request.StartMinute; row.EndMinute = request.EndMinute;
        row.Capacity = request.Capacity; row.IsActive = request.IsActive;
        if (id == null) db.WorkdayShifts.Add(row);
        await db.SaveChangesAsync(ct);
        audit.Add("WorkdayShift", row.ShiftId, id == null ? "CREATE" : "UPDATE", row.BranchId, before, Output.Record(row), request.OperationKey);
        return (object)Output.Record(row);
    }, ct);
}
