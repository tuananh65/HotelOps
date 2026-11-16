using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    internal static void ValidateWorkloadQuery(TaskWorkloadQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue
            || query.DepartmentId <= 0 || query.EmployeeId <= 0 || query.PriorityId <= 0
            || query.Status != null && !TaskWorkflowRead.States.Contains(query.Status)
            || query.Search?.Length > 150 || query.Search?.Any(char.IsControl) == true
            || query.DeadlineTo < query.DeadlineFrom || query.Calendar is not ("UTC" or "Asia/Ho_Chi_Minh"))
            throw new ValidationException("Invalid workload filters.");
    }

    private async Task<PermissionEvaluation> WorkloadAccess(long actor, long branch, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // SRS UC113 actors; grants stay on an eligible actor's own assignment.
        // Department Head is branch-wide, not limited to the user's department.
        var allowed = await db.UserRoles.AsNoTracking().Where(r => r.UserId == actor
            && (r.Role.RoleCode == "DEPT_MANAGER" || r.Role.RoleCode == "OPERATIONS_MANAGER")
            && r.IsActive && r.Role.IsActive && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == branch || r.BranchId == null)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null
            && db.UserBranchAccesses.Any(a => a.UserId == actor && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now)
            && db.RolePermissions.Any(p => p.RoleId == r.RoleId && p.Permission.PermissionCode == "TASK_VIEW"
                && (p.DataScope == "ALL" || p.DataScope == "BRANCH" && r.BranchId == branch))).AnyAsync(ct);
        return new(allowed ? PermissionDecision.Granted : PermissionDecision.Denied);
    }

    public Task<RoomTypeBatchResult> ReadTaskWorkloadAsync(SessionReference ticket, long sid, long branch,
        TaskWorkloadQuery query, long? employee, CancellationToken ct)
    {
        ValidateWorkloadQuery(query);
        if (employee <= 0 || employee.HasValue && query.EmployeeId.HasValue && query.EmployeeId != employee)
            throw new ValidationException("Invalid employee identity.");
        var includesShiftData = false;
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await TaskPlanningStore.InstalledAsync(db, token) || !await TaskVerificationStore.InstalledAsync(db, token))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var period = WorkTimePeriod.Create(query.From, query.To, now, query.Calendar);
            var deadline = query.DeadlineFrom != null || query.DeadlineTo != null
                ? WorkTimePeriod.Create(query.DeadlineFrom ?? query.DeadlineTo, query.DeadlineTo ?? query.DeadlineFrom, now, query.Calendar)
                : (WorkTimePeriod?)null;
            if (query.DepartmentId is { } department && !await db.Departments.AsNoTracking()
                .AnyAsync(d => d.DepartmentId == department && d.BranchId == branch, token)) return new(RoomTypeBatchStatus.NotFound);
            if (query.PriorityId is { } priority && !await db.ReferenceValues.AsNoTracking()
                .AnyAsync(r => r.ReferenceId == priority && r.GroupCode == "PRIORITY", token)) return new(RoomTypeBatchStatus.InvalidReference);

            var tasks = db.OperationalTasks.AsNoTracking().Where(t => t.BranchId == branch && t.Department.BranchId == branch
                && (t.RoomId == null || t.Room!.BranchId == branch) && (t.AreaId == null || t.Area!.BranchId == branch));
            if (query.DepartmentId.HasValue) tasks = tasks.Where(t => t.DepartmentId == query.DepartmentId);
            if (query.PriorityId.HasValue) tasks = tasks.Where(t => t.PriorityId == query.PriorityId);
            if (deadline is { } due) tasks = tasks.Where(t => t.DueAt >= due.Start && t.DueAt < due.End);
            var search = query.Search?.Trim();
            if (!string.IsNullOrEmpty(search)) tasks = tasks.Where(t => t.TaskCode.Contains(search) || t.Title.Contains(search)
                || t.OperationalTaskAssignments.Any(a => a.Assignee.FullName.Contains(search) || a.Assignee.EmployeeCode.Contains(search)));
            var states = TaskWorkflowRead.Query(db, now);
            if (await tasks.AnyAsync(t => !states.Any(s => s.TaskId == t.TaskId), token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var facts = from t in tasks join s in states on t.TaskId equals s.TaskId
                select new TaskWorkloadTask { TaskId = t.TaskId, Code = t.TaskCode, Title = t.Title, DepartmentId = t.DepartmentId, Department = t.Department.DepartmentName,
                    State = s.State, PriorityId = t.PriorityId, Priority = t.Priority == null ? null : t.Priority.DisplayName, Deadline = t.DueAt,
                    CurrentAssigneeId = t.OperationalTaskAssignments.Where(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now)
                        .Select(a => (long?)a.AssigneeId).FirstOrDefault(),
                    CompletedBy = t.OperationalTaskAssignments.Where(a => a.AssignmentStatus == "COMPLETED" && a.EndedAt == t.CompletedAt)
                        .OrderByDescending(a => a.AssignmentId).Select(a => (long?)a.AssigneeId).FirstOrDefault(),
                    CompletedAt = t.CompletedAt, Overdue = s.State != "CLOSED" && s.State != "CANCELLED" && t.DueAt < now };
            if (query.Status != null) facts = facts.Where(t => t.State == query.Status);
            if (query.OverdueOnly) facts = facts.Where(t => t.Overdue);
            var ids = facts.Select(t => t.TaskId);
            var intervals = db.WorkIntervals.AsNoTracking().Where(i => i.BranchId == branch && i.WorkItemKind == "TASK" && ids.Contains(i.WorkItemId)
                && db.Workdays.Any(d => d.WorkdayId == i.WorkdayId && d.BranchId == branch && d.UserId == i.UserId
                    && tasks.Any(t => t.TaskId == i.WorkItemId && t.DepartmentId == d.DepartmentId)))
                .Where(i => i.EffectiveStartedAt < period.End && (i.EffectiveEndedAt == null || i.EffectiveEndedAt > period.Start));
            if (await intervals.AnyAsync(i => i.EffectiveStartedAt > now || i.EffectiveEndedAt < i.EffectiveStartedAt
                || i.ClosedAt == null && i.EffectiveEndedAt != null || i.ClosedAt != null && i.EffectiveEndedAt == null, token))
                return new(RoomTypeBatchStatus.Unavailable);
            var decisions = TaskVerificationStore.Query(db).Where(v => v.BranchId == branch && v.Decision == "ACCEPT"
                && v.DecidedAt >= period.Start && v.DecidedAt < period.End && ids.Contains(v.TaskId));
            var users = db.Users.AsNoTracking().Where(u => facts.Any(t => t.CurrentAssigneeId == u.UserId)
                || intervals.Any(i => i.UserId == u.UserId) || decisions.Any(v => v.SubmittedBy == u.UserId));
            var selected = employee ?? query.EmployeeId;
            if (selected.HasValue) users = users.Where(u => u.UserId == selected);
            if (employee.HasValue)
            {
                var person = await users.SingleOrDefaultAsync(token);
                if (person == null) return new(RoomTypeBatchStatus.NotFound);
                var mine = facts.Where(t => t.CurrentAssigneeId == employee || intervals.Any(i => i.UserId == employee && i.WorkItemId == t.TaskId)
                    || decisions.Any(v => v.SubmittedBy == employee && v.TaskId == t.TaskId));
                var logs = intervals.Where(i => i.UserId == employee);
                var taskRows = await mine.OrderBy(t => t.Code).ThenBy(t => t.TaskId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(token);
                var rows = await logs.OrderByDescending(i => i.EffectiveStartedAt).ThenByDescending(i => i.IntervalId)
                    .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(token);
                var logRows = rows.Select(i => new TaskWorkloadInterval(i.IntervalId, i.WorkItemId, WorkloadUtc(i.EffectiveStartedAt),
                    i.EffectiveEndedAt is { } end ? WorkloadUtc(end) : null,
                    i.ClosedAt != null ? period.Seconds(i.EffectiveStartedAt, i.EffectiveEndedAt, now) : 0,
                    i.ClosedAt == null ? period.Seconds(i.EffectiveStartedAt, null, now) : 0)).ToArray();
                TaskWorkloadShift[]? shifts = null; int? shiftCount = null;
                if ((await permissions.EvaluateBranchAsync("SHIFT_VIEW", branch, token)).IsGranted)
                {
                    includesShiftData = true;
                    var days = db.Workdays.AsNoTracking().Where(d => d.UserId == employee && d.BranchId == branch
                        && db.Departments.Any(x => x.DepartmentId == d.DepartmentId && x.BranchId == branch)
                        && (!query.DepartmentId.HasValue || d.DepartmentId == query.DepartmentId)
                        && d.StartedAt < period.End && (d.EndedAt == null || d.EndedAt > period.Start));
                    shiftCount = await days.CountAsync(token);
                    var dayRows = await days.OrderByDescending(d => d.StartedAt).ThenByDescending(d => d.WorkdayId)
                        .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(token);
                    shifts = dayRows.Select(d => new TaskWorkloadShift(d.WorkdayId, d.WorkDate, d.DepartmentId, WorkloadUtc(d.StartedAt),
                        d.EndedAt is { } end ? WorkloadUtc(end) : null, d.EndedAt != null ? period.Seconds(d.StartedAt, d.EndedAt, now) : 0,
                        d.EndedAt == null ? period.Seconds(d.StartedAt, null, now) : 0)).ToArray();
                }
                return new(RoomTypeBatchStatus.Success, new TaskWorkloadDetail(branch, person.UserId, person.FullName, period.From,
                    period.To, WorkloadUtc(now), taskRows.Select(t => t with { Deadline = UtcTaskDate(t.Deadline), CompletedAt = UtcTaskDate(t.CompletedAt) }).ToArray(),
                    await mine.CountAsync(token), logRows, await logs.CountAsync(token), shifts, shiftCount, query.Page, query.PageSize, query.Calendar));
            }
            var count = await users.CountAsync(token);
            var people = await users.OrderBy(u => u.FullName).ThenBy(u => u.UserId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                .Select(u => new { u.UserId, u.FullName, u.EmployeeCode }).ToArrayAsync(token);
            var result = new List<TaskWorkloadEmployee>();
            foreach (var person in people)
            {
                var mine = intervals.Where(i => i.UserId == person.UserId);
                var closed = await mine.Where(i => i.ClosedAt != null).SumAsync(i => (long?)EF.Functions.DateDiffSecond(
                    i.EffectiveStartedAt < period.Start ? period.Start : i.EffectiveStartedAt,
                    i.EffectiveEndedAt > period.End ? period.End : i.EffectiveEndedAt!.Value), token) ?? 0;
                var openEnd = now < period.End ? now : period.End;
                var open = await mine.Where(i => i.ClosedAt == null && i.EffectiveStartedAt < openEnd && openEnd > period.Start)
                    .SumAsync(i => (long?)EF.Functions.DateDiffSecond(i.EffectiveStartedAt < period.Start ? period.Start : i.EffectiveStartedAt, openEnd), token) ?? 0;
                result.Add(new(person.UserId, person.FullName, person.EmployeeCode,
                    await facts.CountAsync(t => t.CurrentAssigneeId == person.UserId && t.State != "CLOSED" && t.State != "CANCELLED", token),
                    await facts.CountAsync(t => t.CurrentAssigneeId == person.UserId && t.Overdue, token),
                    await decisions.Where(v => v.SubmittedBy == person.UserId).Select(v => v.TaskId).Distinct().CountAsync(token), closed, open));
            }
            return new(RoomTypeBatchStatus.Success, new TaskWorkloadPage(branch, period.From, period.To, WorkloadUtc(now), result,
                count, query.Page, query.PageSize, count == 0 ? "MSG10" : null, query.Calendar));
        }, ct, requireRoomTypeSchema: false, featurePermission: async token =>
        {
            var access = await WorkloadAccess(ticket.UserId, branch, token);
            if (!access.IsGranted || !includesShiftData) return access;
            return await permissions.EvaluateBranchAsync("SHIFT_VIEW", branch, token);
        });
    }

    private static DateTimeOffset WorkloadUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
