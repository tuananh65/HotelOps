using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

internal static class BusinessContractSqlSuite
{
    internal static async Task<IReadOnlyList<string>> RunAsync(string connection, Action<bool, string> report, CancellationToken ct)
    {
        var cases = new List<string>(); void Check(bool ok, string label) { report(ok, label); cases.Add(label); }
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var assembly = typeof(UserAccountDeactivationService).Assembly;
        async Task<(IReadOnlyList<string> Blockers, IReadOnlyList<string> Unresolved, IReadOnlyList<string> Unsupported)> Review(long id)
        {
            var type = assembly.GetType("HotelOps.Api.Services.Users.IntegratedDeactivationResponsibilityQueries", true)!;
            var task = (Task)type.GetMethod("ReadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Activator.CreateInstance(type, db), [id, ct])!;
            await task; var value = task.GetType().GetProperty("Result")!.GetValue(task)!;
            IReadOnlyList<string> List(string name) => (IReadOnlyList<string>)value.GetType().GetProperty(name)!.GetValue(value)!;
            return (List("Blockers"), List("Unresolved"), List("Unsupported"));
        }
        var releaseType = assembly.GetType("HotelOps.Api.Services.Users.UserAccountDeactivationReleaseAcceptance", true)!;
        var release = (IReadOnlyList<string>)releaseType.GetProperty("PendingGates", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Check(release.Order().SequenceEqual(new[] { "DEV1_WRITER_COORDINATION", "DEV2_INTEGRATED_SQL_ACCEPTANCE", "GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE" }.Order()),
            "release acceptance retains all three original release gates independently of target data");
        var immutable = false;
        try { ((IList<string>)release).RemoveAt(0); } catch (NotSupportedException) { immutable = true; }
        Check(immutable && release.Count == 3, "caller cannot mutate the pending release gate collection");
        var denied = false;
        try { releaseType.GetMethod("EnsureAccepted", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null); }
        catch (TargetInvocationException e) when (e.InnerException?.GetType().Name == "DeactivationReleaseNotAcceptedException") { denied = true; }
        Check(denied, "mandatory release fuse rejects independently; no approval flag or injected clear evaluator exists");
        Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID('dbo.shift_attendance') AND name='shift_assignment_id' AND is_nullable=0").SingleAsync(ct) == 1,
            "actual SQL confirms attendance cannot represent an unscheduled workday without a shift assignment");
        Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name LIKE '%workday%' OR name LIKE '%work_day%'").SingleAsync(ct) == 0,
            "canonical trial has no independent Workday table; absence is not a safe responsibility result");
        var eventColumns = await db.Database.SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_request_events')").ToArrayAsync(ct);
        Check(eventColumns.Contains("actor_id") && eventColumns.Contains("event_note")
            && !eventColumns.Any(x => x.Contains("receiver") || x.Contains("acknowledg")),
            "actual SQL event actor and free text do not supply designated receiver acknowledgment");
        var department = await db.Departments.AsNoTracking().Where(x => x.IsActive && x.Branch.IsActive).FirstAsync(ct);
        var now = DateTime.UtcNow; var prefix = "T55B_" + Guid.NewGuid().ToString("N")[..10];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        User Person(string suffix) => new() { Username = prefix + suffix, EmployeeCode = prefix + suffix,
            Email = prefix + suffix + "@example.invalid", FullName = "Owned contract fixture", CreatedAt = now,
            AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE" };
        var target = Person("t"); var employee = Person("e");
        db.AddRange(target, employee); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await new UserEligibilityLock(db).LockAsync([target.UserId, employee.UserId], ct);
        var empty = await Review(target.UserId);
        Check(empty.Blockers.Count == 0 && empty.Unresolved.Count == 5 && !empty.Unresolved.Intersect(release).Any(),
            "empty modeled target has five business contracts, not three release predicates mixed into responsibility");
        Check(empty.Unsupported.Contains("UNSCHEDULED_WORKDAY_MODEL") && empty.Unsupported.Contains("MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL"),
            "missing models remain Unsupported even though no controller or workflow writer is implemented");
        await db.Users.Where(x => x.UserId == employee.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ManagerUserId, target.UserId), ct);
        Check((await Review(target.UserId)).Blockers.Contains("LINE_MANAGER_DESIGNATION"), "nondeleted active employee's explicit manager designation is positive V1 obligation evidence");
        await db.Users.Where(x => x.UserId == employee.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
        Check((await Review(target.UserId)).Blockers.Contains("LINE_MANAGER_DESIGNATION"), "inactive login does not end an active employee's management designation");
        await db.Users.Where(x => x.UserId == employee.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EmploymentStatus, "active"), ct);
        var unmappedEmployee = await Review(target.UserId);
        Check(!unmappedEmployee.Blockers.Contains("LINE_MANAGER_DESIGNATION") && unmappedEmployee.Unresolved.Contains("LINE_MANAGER_EMPLOYMENT_MAPPING"),
            "unmapped employment vocabulary is unresolved despite CI SQL matching; no false current-or-historical release mapping");
        await db.Users.Where(x => x.UserId == employee.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EmploymentStatus, "ACTIVE"), ct);
        await db.Users.Where(x => x.UserId == employee.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, now), ct);
        var history = await Review(target.UserId);
        Check(!history.Blockers.Contains("LINE_MANAGER_DESIGNATION") && history.Unresolved.Contains("LINE_MANAGER_CONTRACT"),
            "deleted subordinate reference is not current positive evidence and never clears the unresolved manager release contract");
        await db.Users.Where(x => x.UserId == employee.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ManagerUserId, (long?)null), ct);
        Check(!(await Review(target.UserId)).Blockers.Contains("LINE_MANAGER_DESIGNATION"), "no manager designation is not invented from role or department membership");

        var template = new ShiftTemplate { BranchId = department.BranchId, ShiftCode = prefix, ShiftName = prefix,
            StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), IsActive = true };
        db.Add(template); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var shift = new WorkShift { BranchId = department.BranchId, DepartmentId = department.DepartmentId,
            ShiftTemplateId = template.ShiftTemplateId, ShiftDate = DateOnly.FromDateTime(now), ScheduledStartAt = now.AddHours(-4),
            ScheduledEndAt = now.AddHours(-2), CreatedBy = employee.UserId, CreatedAt = now, Status = "PLANNED" };
        db.Add(shift); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var assignment = new ShiftAssignment { ShiftId = shift.ShiftId, UserId = target.UserId, AssignedBy = employee.UserId,
            AssignedAt = now.AddHours(-5), AssignmentStatus = "ASSIGNED" };
        db.Add(assignment); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var scheduled = await Review(target.UserId);
        Check(!scheduled.Blockers.Contains("SHIFT_OPEN_ATTENDANCE") && scheduled.Unresolved.Contains("SHIFT_CONTRACT"),
            "roster reference and elapsed scheduled end alone neither prove current attendance nor resolve release semantics");
        var attendance = new ShiftAttendance { ShiftAssignmentId = assignment.ShiftAssignmentId, AttendanceStatus = "NOT_STARTED" };
        db.Add(attendance); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Check(!(await Review(target.UserId)).Blockers.Contains("SHIFT_OPEN_ATTENDANCE"), "NOT_STARTED row without actual timestamps is not fabricated open attendance");
        await db.ShiftAttendances.Where(x => x.AttendanceId == attendance.AttendanceId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ActualCheckInAt, now.AddHours(-1)), ct);
        var open = await Review(target.UserId);
        Check(open.Blockers.Contains("SHIFT_OPEN_ATTENDANCE") && open.Unresolved.Contains("SHIFT_CONTRACT"),
            "explicit ASSIGNED open actual attendance blocks even after scheduled end; unknown lifecycle mapping still fails closed");
        await db.ShiftAttendances.Where(x => x.AttendanceId == attendance.AttendanceId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ActualCheckOutAt, now.AddMinutes(-1)), ct);
        var ended = await Review(target.UserId);
        Check(!ended.Blockers.Contains("SHIFT_OPEN_ATTENDANCE") && ended.Unresolved.Contains("SHIFT_CONTRACT") && ended.Unsupported.Count == 2,
            "closed timestamp history does not imply handover acknowledgment or safe Workday release");
        foreach (var fault in new[] { "out-before-in", "out-without-in", "future-in" })
        {
            DateTime? checkIn = fault == "out-without-in" ? null : fault == "future-in" ? now.AddHours(1) : now.AddMinutes(-1);
            DateTime? checkOut = fault == "future-in" ? null : now.AddHours(-1);
            await db.ShiftAttendances.Where(x => x.AttendanceId == attendance.AttendanceId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ActualCheckInAt, checkIn).SetProperty(x => x.ActualCheckOutAt, checkOut), ct);
            var invalid = await Review(target.UserId);
            Check(invalid.Unresolved.Contains("SHIFT_ATTENDANCE_DATA_INCONSISTENCY") && !invalid.Blockers.Contains("SHIFT_OPEN_ATTENDANCE"),
                "invalid attendance evidence is unresolved, not a false history/clear result: " + fault);
        }
        await db.ShiftAttendances.Where(x => x.AttendanceId == attendance.AttendanceId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.ActualCheckInAt, now.AddHours(-1)).SetProperty(x => x.ActualCheckOutAt, (DateTime?)null), ct);
        await db.ShiftAssignments.Where(x => x.ShiftAssignmentId == assignment.ShiftAssignmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignmentStatus, "assigned"), ct); // Invalid ordinal casing, not a new business status.
        Check((await Review(target.UserId)).Unresolved.Contains("SHIFT_OPEN_ATTENDANCE_MAPPING"), "unmapped assignment vocabulary cannot silently clear actual open attendance");
        await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
        Check(!await db.Users.AnyAsync(x => x.UserId == target.UserId || x.UserId == employee.UserId, ct)
            && !await db.WorkShifts.AnyAsync(x => x.ShiftId == shift.ShiftId, ct), "contract fixtures including manager links and attendance roll back without schema or catalog writes");
        return cases;
    }
}
