using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.WorkTime;

public sealed class WorkTimeContractException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// Trusted backend participant, never an HTTP authorization boundary. The caller owns the
/// same scoped context/transaction and source authorization/locks. No method commits.
/// Workday HTTP permissions, unscheduled notifications and Pause All are not implemented here.
/// </summary>
public sealed class SqlWorkTimeTransactionParticipant(HotelOpsDbContext db) : IWorkTimeTransactionParticipant
{
    private Guid? lockedTransaction;
    private long[] lockedIds = [];
    private static readonly TimeZoneInfo HotelTimezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public Task LockUsersAsync(IReadOnlyCollection<long> actorAndOwnerIds, CancellationToken cancellationToken) => RunAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(actorAndOwnerIds);
        var tx = Transaction();
        var ids = actorAndOwnerIds.Distinct().Order().ToArray();
        if (ids.Length == 0 || ids.Any(x => x <= 0)) throw Failure("TIME_LOCK_SET_REQUIRED");
        if (lockedTransaction is not null)
        {
            if (lockedTransaction != tx || !lockedIds.SequenceEqual(ids)) throw Failure("TIME_LOCK_SET_REQUIRED");
            return true;
        }
        await new UserEligibilityLock(db).LockAsync(ids, cancellationToken);
        foreach (var id in ids)
        {
            var resource = "HotelOps:WorkTime:User:" + id;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
                    @LockOwner='Transaction', @LockTimeout=15000;
                IF @result<0 THROW 51776,'Time serialization lock unavailable.',1;
                """, cancellationToken);
            await Working(id).ToArrayAsync(cancellationToken);
            await Open(id).ToArrayAsync(cancellationToken);
        }
        lockedIds = ids;
        lockedTransaction = tx;
        return true;
    });

    // Scheduled foundation primitive. No public endpoint or implicit attendance->Workday mapping.
    public Task<Workday> StartScheduledWorkdayAsync(long actorUserId, long branchId, long shiftAssignmentId,
        CancellationToken ct) => RunAsync(async () =>
    {
        RequireLocks(actorUserId);
        if (branchId <= 0 || shiftAssignmentId <= 0) throw Failure("TIME_STATE_CONFLICT");
        var now = await NowAsync(ct);
        await EligibleAsync(actorUserId, branchId, now, ct);
        if (await Working(actorUserId).AnyAsync(ct)) throw Failure("TIME_ALREADY_WORKING");
        if (await Open(actorUserId).AnyAsync(ct)) throw Failure("TIME_STATE_CONFLICT");
        var assignment = await db.ShiftAssignments.FromSqlInterpolated(
            $"SELECT * FROM dbo.shift_assignments WITH(UPDLOCK,HOLDLOCK) WHERE shift_assignment_id={shiftAssignmentId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        // ASSIGNED is the actual legacy schema default, not an invented SRS status.
        if (assignment is null || assignment.UserId != actorUserId || assignment.AssignmentStatus != "ASSIGNED"
            || assignment.AssignedAt > now) throw Failure("TIME_SCOPE_DENIED");
        var shift = await db.WorkShifts.FromSqlInterpolated(
            $"SELECT * FROM dbo.work_shifts WITH(UPDLOCK,HOLDLOCK) WHERE shift_id={assignment.ShiftId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, HotelTimezone));
        if (shift is null || shift.BranchId != branchId || shift.Status != "PLANNED" || shift.ShiftDate != today
            || shift.ScheduledEndAt < shift.ScheduledStartAt) throw Failure("TIME_STATE_CONFLICT");
        if (!await db.ShiftTemplates.FromSqlInterpolated(
                $"SELECT * FROM dbo.shift_templates WITH(HOLDLOCK) WHERE shift_template_id={shift.ShiftTemplateId}")
                .AsNoTracking().AnyAsync(x => x.ShiftTemplateId == shift.ShiftTemplateId
                && x.BranchId == branchId && x.IsActive, ct)
            || !await db.Departments.FromSqlInterpolated(
                $"SELECT * FROM dbo.departments WITH(HOLDLOCK) WHERE department_id={shift.DepartmentId}")
                .AsNoTracking().AnyAsync(x => x.DepartmentId == shift.DepartmentId
                && x.BranchId == branchId && x.IsActive, ct)) throw Failure("TIME_SCOPE_DENIED");
        now = await NowAsync(ct);
        var day = new Workday { UserId = actorUserId, BranchId = branchId, DepartmentId = shift.DepartmentId,
            WorkDate = shift.ShiftDate, ShiftAssignmentId = shiftAssignmentId, IsUnscheduled = false,
            Status = "WORKING", StartedAt = now };
        db.Workdays.Add(day);
        await db.SaveChangesAsync(ct);
        Audit(branchId, actorUserId, "workdays", day.WorkdayId, "CREATE", null,
            new { day.UserId, day.Status, day.ShiftAssignmentId, day.StartedAt }, now);
        await db.SaveChangesAsync(ct);
        return day;
    });

    public Task<Workday> EndWorkdayAsync(long actorUserId, long branchId, long workdayId,
        byte[] expectedRowVersion, CancellationToken ct) => RunAsync(async () =>
    {
        RequireLocks(actorUserId);
        RequireVersion(expectedRowVersion);
        var now = await NowAsync(ct);
        await EligibleAsync(actorUserId, branchId, now, ct);
        var day = await Working(actorUserId).SingleOrDefaultAsync(ct);
        if (day is null || day.WorkdayId != workdayId || day.BranchId != branchId) throw Failure("TIME_STATE_CONFLICT");
        if (!day.RowVersion.AsSpan().SequenceEqual(expectedRowVersion)) throw Failure("TIME_STALE");
        if (await Open(actorUserId).AnyAsync(ct)) throw Failure("TIME_INTERVAL_ALREADY_OPEN");
        now = await NowAsync(ct);
        if (now < day.StartedAt || await db.WorkIntervals.AsNoTracking().AnyAsync(x => x.WorkdayId == workdayId
                && (x.ClosedAt > now || x.EffectiveEndedAt > now), ct)) throw Failure("TIME_STATE_CONFLICT");
        // ExecuteUpdate avoids a stale tracked entity surviving a User-lock wait.
        var affected = await db.Workdays.Where(x => x.WorkdayId == workdayId && x.Status == "WORKING"
                && x.RowVersion == expectedRowVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ENDED").SetProperty(x => x.EndedAt, now), ct);
        if (affected != 1) throw Failure("TIME_STALE");
        Audit(branchId, actorUserId, "workdays", day.WorkdayId, "UPDATE", new { day.Status, day.EndedAt },
            new { Status = "ENDED", EndedAt = now }, now);
        await db.SaveChangesAsync(ct);
        return await db.Workdays.AsNoTracking().SingleAsync(x => x.WorkdayId == workdayId, ct);
    });

    public Task<WorkTimeIntervalResult> OpenIntervalAsync(WorkTimeOperation operation, CancellationToken cancellationToken)
        => RunAsync(async () =>
    {
        RequireOperation(operation);
        if (operation.ActorUserId != operation.OwnerUserId) throw Failure("TIME_SCOPE_DENIED");
        var now = await NowAsync(cancellationToken);
        await EligibleAsync(operation.OwnerUserId, operation.BranchId, now, cancellationToken);
        var day = await Working(operation.OwnerUserId).SingleOrDefaultAsync(cancellationToken);
        if (day is null || day.BranchId != operation.BranchId) throw Failure("TIME_WORKDAY_REQUIRED");
        if (await Open(operation.OwnerUserId).AnyAsync(cancellationToken)) throw Failure("TIME_INTERVAL_ALREADY_OPEN");
        if (now < day.StartedAt || await db.WorkIntervals.AsNoTracking().AnyAsync(x => x.UserId == operation.OwnerUserId
            && x.EffectiveEndedAt > now, cancellationToken)) throw Failure("TIME_STATE_CONFLICT");
        await SourceAsync(operation, now, cancellationToken);
        now = await NowAsync(cancellationToken);
        if (now < day.StartedAt || await db.WorkIntervals.AsNoTracking().AnyAsync(x => x.UserId == operation.OwnerUserId
            && x.EffectiveEndedAt > now, cancellationToken)) throw Failure("TIME_STATE_CONFLICT");
        var interval = new WorkInterval { WorkdayId = day.WorkdayId, UserId = operation.OwnerUserId,
            BranchId = operation.BranchId, WorkItemKind = operation.WorkItemKind, WorkItemId = operation.WorkItemId,
            OpenedBy = operation.ActorUserId, OpenedAt = now, EffectiveStartedAt = now };
        db.WorkIntervals.Add(interval);
        await db.SaveChangesAsync(cancellationToken);
        Audit(operation.BranchId, operation.ActorUserId, "work_intervals", interval.IntervalId, "CREATE", null,
            new { interval.WorkdayId, interval.UserId, interval.WorkItemKind, interval.WorkItemId, interval.OpenedAt }, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result(interval);
    });

    public Task<WorkTimeIntervalResult> CloseIntervalAsync(WorkTimeOperation operation, long intervalId,
        byte[] expectedRowVersion, CancellationToken cancellationToken) => RunAsync(async () =>
    {
        RequireOperation(operation);
        RequireVersion(expectedRowVersion);
        var now = await NowAsync(cancellationToken);
        await EligibleAsync(operation.ActorUserId, operation.BranchId, now, cancellationToken);
        var day = await Working(operation.OwnerUserId).SingleOrDefaultAsync(cancellationToken);
        var interval = await Open(operation.OwnerUserId).SingleOrDefaultAsync(cancellationToken);
        if (day is null || interval is null || interval.IntervalId != intervalId || interval.WorkdayId != day.WorkdayId
            || interval.BranchId != operation.BranchId || day.BranchId != operation.BranchId
            || interval.WorkItemKind != operation.WorkItemKind || interval.WorkItemId != operation.WorkItemId
            || now < interval.OpenedAt) throw Failure("TIME_STATE_CONFLICT");
        if (!interval.RowVersion.AsSpan().SequenceEqual(expectedRowVersion)) throw Failure("TIME_STALE");
        await SourceAsync(operation, now, cancellationToken);
        now = await NowAsync(cancellationToken);
        if (now < interval.OpenedAt) throw Failure("TIME_STATE_CONFLICT");
        var affected = await db.WorkIntervals.Where(x => x.IntervalId == intervalId && x.ClosedAt == null
                && x.RowVersion == expectedRowVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClosedAt, now).SetProperty(x => x.ClosedBy, operation.ActorUserId)
                .SetProperty(x => x.EffectiveEndedAt, now), cancellationToken);
        if (affected != 1) throw Failure("TIME_STALE");
        Audit(operation.BranchId, operation.ActorUserId, "work_intervals", intervalId, "UPDATE",
            new { interval.ClosedAt, interval.EffectiveEndedAt }, new { ClosedAt = now, ClosedBy = operation.ActorUserId }, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result(await db.WorkIntervals.AsNoTracking().SingleAsync(x => x.IntervalId == intervalId, cancellationToken));
    });

    private IQueryable<Workday> Working(long userId) => db.Workdays.FromSqlInterpolated(
        $"SELECT * FROM dbo.workdays WITH(UPDLOCK,HOLDLOCK) WHERE user_id={userId} AND status='WORKING'").AsNoTracking();
    private IQueryable<WorkInterval> Open(long userId) => db.WorkIntervals.FromSqlInterpolated(
        $"SELECT * FROM dbo.work_intervals WITH(UPDLOCK,HOLDLOCK) WHERE user_id={userId} AND closed_at IS NULL").AsNoTracking();
    private Guid Transaction()
    {
        var transaction = db.Database.CurrentTransaction ?? throw Failure("TIME_TRANSACTION_REQUIRED");
        if (transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted
            && transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw Failure("TIME_TRANSACTION_REQUIRED");
        return transaction.TransactionId;
    }
    private void RequireLocks(params long[] ids)
    {
        if (lockedTransaction != Transaction() || ids.Any(x => !lockedIds.Contains(x))) throw Failure("TIME_LOCK_SET_REQUIRED");
    }
    private void RequireOperation(WorkTimeOperation op)
    {
        ArgumentNullException.ThrowIfNull(op);
        RequireLocks(op.ActorUserId, op.OwnerUserId);
        if (op.BranchId <= 0 || op.WorkItemId <= 0) throw Failure("TIME_STATE_CONFLICT");
        // DEV2 must confirm PM current occurrence identity/assignee before a PM adapter is enabled.
        if (op.WorkItemKind is not ("CLEANING" or "REWORK" or "TASK")) throw Failure("TIME_SOURCE_UNRESOLVED");
    }
    private async Task EligibleAsync(long userId, long branchId, DateTime now, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.DeletedAt, x.AccountStatus, x.EmploymentStatus }).SingleOrDefaultAsync(ct);
        if (user is null || user.DeletedAt is not null || user.AccountStatus != "ACTIVE" || user.EmploymentStatus != "ACTIVE")
            throw Failure("TIME_ACTOR_INVALID");
        if (!await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(HOLDLOCK) WHERE branch_id={branchId}")
                .AsNoTracking().AnyAsync(x => x.IsActive, ct)
            || !await db.UserBranchAccesses.FromSqlInterpolated(
                $"SELECT * FROM dbo.user_branch_access WITH(HOLDLOCK) WHERE user_id={userId} AND branch_id={branchId}")
                .AsNoTracking().AnyAsync(x => x.UserId == userId && x.BranchId == branchId
                && x.GrantedAt <= now && x.RevokedAt == null, ct)) throw Failure("TIME_SCOPE_DENIED");
    }
    private async Task SourceAsync(WorkTimeOperation op, DateTime now, CancellationToken ct)
    {
        // Additional authoritative consistency checks. Caller still owns source locks/permission/transition.
        bool valid;
        if (op.WorkItemKind == "CLEANING")
            valid = await db.CleaningTasks.AsNoTracking().AnyAsync(x => x.CleaningTaskId == op.WorkItemId
                && x.Status == "IN_PROGRESS" && x.Room.BranchId == op.BranchId && x.Room.IsActive
                && x.CleaningAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now) == 1
                && x.CleaningAssignments.Any(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null
                    && a.AssignedAt <= now && a.AssigneeUserId == op.OwnerUserId), ct);
        else if (op.WorkItemKind == "REWORK")
            valid = await db.CleaningReworks.AsNoTracking().AnyAsync(x => x.ReworkId == op.WorkItemId
                && x.Status == "IN_PROGRESS" && x.CompletedAt == null && x.AssigneeUserId == op.OwnerUserId
                && x.CleaningTask.Room.BranchId == op.BranchId && x.CleaningTask.Room.IsActive
                && x.Inspection.RoomId == x.CleaningTask.RoomId && x.Inspection.CleaningTaskId == x.CleaningTaskId, ct);
        else
            valid = await db.OperationalTasks.AsNoTracking().AnyAsync(x => x.TaskId == op.WorkItemId && x.BranchId == op.BranchId
                && x.Status == "IN_PROGRESS" && x.Department.BranchId == op.BranchId && x.Department.IsActive
                && x.OperationalTaskAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now) == 1
                && x.OperationalTaskAssignments.Any(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null
                    && a.AssignedAt <= now && a.AssigneeId == op.OwnerUserId), ct);
        if (!valid) throw Failure("TIME_SCOPE_DENIED");
    }
    private async Task<DateTime> NowAsync(CancellationToken ct) => DateTime.SpecifyKind(
        await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct), DateTimeKind.Utc);
    private void Audit(long branch, long actor, string entity, long id, string action, object? before, object after, DateTime now)
        => db.AuditLogs.Add(new AuditLog { BranchId = branch, ActorUserId = actor, EntityType = entity,
            EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = action, OccurredAt = now,
            OldValues = before is null ? null : JsonSerializer.Serialize(before), NewValues = JsonSerializer.Serialize(after) });
    private static WorkTimeIntervalResult Result(WorkInterval row)
        => new(row.IntervalId, row.WorkdayId, DateTime.SpecifyKind(row.OpenedAt, DateTimeKind.Utc),
            row.ClosedAt is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null, row.RowVersion.ToArray());
    private static void RequireVersion(byte[] version)
    {
        if (version is not { Length: 8 }) throw Failure("TIME_STALE");
    }
    private static WorkTimeContractException Failure(string code) => new(code);
    private static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception e) when (MapSql(e) is not null) { throw MapSql(e)!; }
    }
    private static WorkTimeContractException? MapSql(Exception error)
    {
        var sql = error as SqlException ?? (error as DbUpdateException)?.InnerException as SqlException;
        if (sql is null) return null;
        if (sql.Number is 207 or 208) return Failure("TIME_SOURCE_UNRESOLVED");
        if (sql.Number is 1205 or 1222 or -2 or 51776) return Failure("TIME_BUSY");
        if (sql.Number is not (2601 or 2627)) return null;
        if (sql.Message.Contains("UX_workdays_one_working", StringComparison.Ordinal)) return Failure("TIME_ALREADY_WORKING");
        return sql.Message.Contains("UX_work_intervals_one_open", StringComparison.Ordinal) ? Failure("TIME_INTERVAL_ALREADY_OPEN") : null;
    }
}
