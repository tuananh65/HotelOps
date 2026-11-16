using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record WorkdayResult(int Status, object? Value = null, string? Code = null);

/// <summary>B1.1 scheduled own-workday boundary; caller identity is never model-bound.</summary>
public sealed class WorkdayService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IHttpContextAccessor http, ISqlSessionService sessions, IPermissionService permissions,
    SqlWorkTimeTransactionParticipant participant, OwnWorkdaySnapshotReader reader,
    RoomTypeCreationService? workflows = null)
{
    private static readonly TimeZoneInfo HotelTimezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public Task<WorkdayResult> StartAsync(long branchId, StartWorkdayRequest request, CancellationToken ct)
        => Execute(branchId, "ATTENDANCE_RECORD", async actor =>
        {
            if (request.ShiftAssignmentId is null)
            {
                if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
                    return new(400, Code: "UNSCHEDULED_REASON_REQUIRED");
                return new(503, Code: "UNSCHEDULED_NOTIFICATION_UNAVAILABLE");
            }
            if (request.ShiftAssignmentId <= 0 || !string.IsNullOrWhiteSpace(request.Reason))
                return new(400, Code: "INVALID_SCHEDULED_START");
            var assignmentId = request.ShiftAssignmentId.Value;
            var allowed = await Authorize("ATTENDANCE_RECORD", new("shift_assignment", assignmentId), ct);
            if (allowed is not null) return allowed;
            if (!await db.ShiftAssignments.AsNoTracking().AnyAsync(a => a.ShiftAssignmentId == assignmentId
                    && a.UserId == actor.UserId && a.Shift.BranchId == branchId, ct))
                return new(403, Code: "TIME_SCOPE_DENIED");
            var day = await participant.StartScheduledWorkdayAsync(actor.UserId, branchId, assignmentId, ct);
            return new(201, Response(day));
        }, ct);

    public Task<WorkdayResult> CurrentAsync(long branchId, CancellationToken ct)
        => Execute(branchId, "ATTENDANCE_RECORD", async _ =>
        {
            var snapshot = await reader.ReadAsync(branchId, ct);
            return snapshot is null ? new(204) : new(200, snapshot);
        }, ct);

    public Task<WorkdayResult> OwnTimeLogAsync(long branchId,DateOnly date,int page,int pageSize,CancellationToken ct)
    {
        if(date.Year<1900||date==DateOnly.MaxValue||page<1||pageSize is <1 or >100||(long)(page-1)*pageSize>int.MaxValue)
            return Task.FromResult(new WorkdayResult(400,Code:"INVALID_TIME_LOG_QUERY"));
        return Execute(branchId,"ATTENDANCE_RECORD",async actor=>
        {
            var result=await OwnTimeLogReader.ReadAsync(db,actor.UserId,branchId,date,page,pageSize,ct);
            return new(200,result);
        },ct);
    }

    public Task<WorkdayResult> EndAsync(long branchId, long workdayId, byte[] expectedVersion,
        bool pauseAll, CancellationToken ct)
        => Execute(branchId, "ATTENDANCE_RECORD", async actor =>
        {
            if (workdayId <= 0 || expectedVersion is not { Length: 8 }) return new(400, Code: "INVALID_IF_MATCH");
            var allowed = await Authorize("ATTENDANCE_RECORD", new("workday", workdayId), ct);
            if (allowed is not null) return allowed;
            if (!await db.Workdays.AsNoTracking().AnyAsync(d => d.WorkdayId == workdayId
                    && d.UserId == actor.UserId && d.BranchId == branchId, ct))
                return new(403, Code: "TIME_SCOPE_DENIED");
            if (pauseAll)
            {
                var dayBefore = await db.Workdays.AsNoTracking().SingleAsync(d => d.WorkdayId == workdayId, ct);
                if (dayBefore.Status != "WORKING") return new(409, Code: "TIME_STATE_CONFLICT");
                if (!dayBefore.RowVersion.AsSpan().SequenceEqual(expectedVersion)) return new(412, Code: "TIME_STALE");
                var open = await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == actor.UserId && i.ClosedAt == null)
                    .Take(2).ToArrayAsync(ct);
                if (open.Length > 1) return new(409, Code: "TIME_STATE_CONFLICT");
                foreach (var interval in open)
                {
                    if (interval.WorkdayId != workdayId || interval.BranchId != branchId)
                        return new(409, Code: "TIME_STATE_CONFLICT");
                    // Unsupported owning workflows remain running and this entire transaction rolls back.
                    if (interval.WorkItemKind != "TASK" || workflows is null)
                        return new(503, Code: "PAUSE_ALL_SOURCE_UNAVAILABLE");
                    var paused = await workflows.PauseTaskForWorkdayEndAsync(actor.UserId, branchId,
                        interval.WorkItemId, participant, ct);
                    if (paused.Status != RoomTypeBatchStatus.Success)
                        return new(paused.Status == RoomTypeBatchStatus.Forbidden ? 403
                            : paused.Status == RoomTypeBatchStatus.Stale ? 412
                            : paused.Status == RoomTypeBatchStatus.Inactive ? 409 : 503,
                            Code: "PAUSE_ALL_" + paused.Status.ToString().ToUpperInvariant());
                }
            }
            var day = await participant.EndWorkdayAsync(actor.UserId, branchId, workdayId, expectedVersion, ct);
            return new(200, Response(day));
        }, ct);

    public Task<WorkdayResult> OwnShiftsAsync(long branchId, CancellationToken ct)
        => Execute(branchId, "SHIFT_VIEW", async actor =>
        {
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct), DateTimeKind.Utc);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, HotelTimezone));
            var shifts = await ScheduledWorkdayClaims.Unclaimed(db.ShiftAssignments.AsNoTracking(), db.Workdays.AsNoTracking()).Where(a => a.UserId == actor.UserId
                    && a.AssignmentStatus == "ASSIGNED" && a.AssignedAt <= now && a.Shift.BranchId == branchId
                    && a.Shift.Status == "PLANNED" && a.Shift.ShiftDate == today
                    && a.Shift.ScheduledEndAt >= a.Shift.ScheduledStartAt
                    && a.Shift.ShiftTemplate.BranchId == branchId && a.Shift.ShiftTemplate.IsActive
                    && a.Shift.Department.BranchId == branchId && a.Shift.Department.IsActive)
                .OrderBy(a => a.Shift.ScheduledStartAt).ThenBy(a => a.ShiftAssignmentId)
                .Select(a => new
                {
                    a.ShiftAssignmentId,
                    a.ShiftId,
                    a.Shift.BranchId,
                    a.Shift.DepartmentId,
                    a.Shift.ShiftDate,
                    a.Shift.ScheduledStartAt,
                    a.Shift.ScheduledEndAt
                }).ToArrayAsync(ct);
            return new(200, shifts.Select(s => new OwnEligibleShift(s.ShiftAssignmentId, s.ShiftId, s.BranchId,
                s.DepartmentId, s.ShiftDate, Utc(s.ScheduledStartAt), Utc(s.ScheduledEndAt))).ToArray());
        }, ct);

    private async Task<WorkdayResult> Execute(long branchId, string permission,
        Func<CurrentUserInfo, Task<WorkdayResult>> action, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0)
            return new(401, Code: "SESSION_INVALID");
        if (actor.IsRestricted) return new(403, Code: "PASSWORD_CHANGE_REQUIRED");
        if (branchId <= 0) return new(403, Code: "TIME_SCOPE_DENIED");
        var context = http.HttpContext;
        if (context is null || !HotelOpsCookieTicket.TryRead(context.User, out var ticket, out var sessionId)
            || ticket is null || sessionId != actor.SessionId || ticket.UserId != actor.UserId)
            return new(401, Code: "SESSION_INVALID");
        try
        {
            // Serializable retains session/grant/source reads until commit. User/time locks first;
            // stale middleware identity cannot survive a revocation committed during a lock wait.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await participant.LockUsersAsync([actor.UserId], ct);
            var validated = await sessions.ValidateSessionAsync(ticket, ct);
            if (!validated.IsValid || validated.Session?.SessionId != actor.SessionId
                || validated.Session.UserId != actor.UserId || validated.Session.IsRestricted)
                return new(401, Code: "SESSION_INVALID");
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actor.UserId
                    && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null, ct))
                return new(403, Code: "TIME_ACTOR_INVALID");
            var denied = await Authorize(permission, new("workday_self_branch", branchId), ct);
            if (denied is not null) return denied;
            var result = await action(actor);
            if (result.Status is >= 200 and < 300)
            {
                // Locks preserve rows, not wall-clock validity during a long operation.
                var finalSession = await sessions.ValidateSessionAsync(ticket, ct);
                if (!finalSession.IsValid || finalSession.Session?.SessionId != actor.SessionId
                    || finalSession.Session.UserId != actor.UserId || finalSession.Session.IsRestricted)
                    return new(401, Code: "SESSION_INVALID");
                await tx.CommitAsync(ct);
            }
            return result;
        }
        catch (WorkTimeContractException e)
        {
            return e.Code switch
            {
                "TIME_STALE" => new(412, Code: e.Code),
                "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID" => new(403, Code: e.Code),
                "TIME_SESSION_INVALID" => new(401, Code: "SESSION_INVALID"),
                "TIME_RESTRICTED" => new(403, Code: "PASSWORD_CHANGE_REQUIRED"),
                "TIME_SOURCE_UNRESOLVED" or "TIME_BUSY" => new(503, Code: e.Code),
                "TIME_INTERVAL_ALREADY_OPEN" => new(409, Code: "MSG31_PAUSE_ALL_REQUIRED"),
                _ => new(409, Code: e.Code)
            };
        }
        catch (SessionStoreUnavailableException) { return new(503, Code: "SESSION_STORE_UNAVAILABLE"); }
        catch (SqlException) when (!ct.IsCancellationRequested) { return new(503, Code: "WORKDAY_STORE_UNAVAILABLE"); }
        catch (DbUpdateException) when (!ct.IsCancellationRequested) { return new(503, Code: "WORKDAY_STORE_UNAVAILABLE"); }
    }

    private async Task<WorkdayResult?> Authorize(string permission, PermissionResource resource, CancellationToken ct)
    {
        var decision = await permissions.EvaluateResourceAsync(permission, resource, ct);
        return decision.Decision switch
        {
            PermissionDecision.Granted => null,
            PermissionDecision.Unauthenticated => new(401, Code: "SESSION_INVALID"),
            PermissionDecision.Unavailable => new(503, Code: "AUTHORIZATION_UNAVAILABLE"),
            _ => new(403, Code: "WORKDAY_FORBIDDEN")
        };
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static WorkdayResponse Response(Workday day) => new(day.WorkdayId, day.BranchId, day.DepartmentId,
        day.WorkDate, day.ShiftAssignmentId, day.IsUnscheduled, day.Status, Utc(day.StartedAt),
        day.EndedAt is { } end ? Utc(end) : null, Convert.ToBase64String(day.RowVersion));
}
