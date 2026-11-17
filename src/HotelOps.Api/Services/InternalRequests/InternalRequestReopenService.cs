using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.InternalRequests;

public sealed class InternalRequestReopenOptions
{
    // SYS-PAR-18 SRS default; server configuration, never client input.
    public int WindowDays { get; set; } = 7;
}
public sealed record InternalRequestReopenInput(string Reason);
public sealed record InternalRequestReopenView(long RequestId, string Version, string PreviousResult,
    DateTimeOffset ClosedAt, DateTimeOffset EvaluatedAt, int WindowDays, long LastHandlerId, bool CanReopen, string? Blocker);
public sealed record InternalRequestReopenResult(long RequestId, long EventId, string Status, string Version, string MessageCode);

public sealed class InternalRequestReopenService(HotelOpsDbContext db, ISqlSessionService sessions,
    ICurrentUserAccessor current, IOptions<InternalRequestReopenOptions> options)
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestReopen.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.internal_request_reopens','window_days')=4
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_reopens')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=5
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.internal_request_reopens')
                AND name='TR_ir_reopen_immutable' AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.UserId != ticket.UserId || valid.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }
    private Task<DateTime> Now(CancellationToken ct) => db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
    private static DateTimeOffset Utc(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));

    private async Task<InternalRequest> Readable(long user, long id, DateTime now, CancellationToken ct)
    {
        var scope = await new InternalRequestReadAccess(db).ReadAsync(user, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
    }
    private async Task<bool> EligibleHandler(long handler, InternalRequest request, DateTime now, CancellationToken ct)
    {
        if (request.ReceivingDepartmentId == null || !await InternalRequestEmployeeMembership.InstalledAsync(db, ct)) return false;
        if (!await InternalRequestEmployeeMembership.Members(db, request.BranchId, request.ReceivingDepartmentId, now).AnyAsync(u => u.UserId == handler, ct)) return false;
        var read = await new InternalRequestReadAccess(db).ReadAsync(handler, now, ct);
        // OWN becomes effective with the new active assignment; verify that prospective
        // read path before writing, then verify the actual source after writing.
        return read.Global || read.Branches.Contains(request.BranchId) || read.RelatedBranches.Contains(request.BranchId);
    }

    public async Task<InternalRequestReopenView> ReadAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Request identifier required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct); await SessionAsync(ticket, sid, ct);
        var now = await Now(ct); var request = await Readable(ticket.UserId, id, now, ct);
        var closure = await InternalRequestReopenSource.PreviewAsync(db, id, options.Value.WindowDays, ct);
        var allowed = await InternalRequestReopenAccess.AllowsAsync(db, ticket.UserId, request, now, ct);
        string? blocker = !allowed ? "IR_REOPEN_REQUIRED"
            : !InternalRequestReopenSource.WithinWindow(closure.ClosedAt, now, options.Value.WindowDays) ? "IR_REOPEN_WINDOW_EXPIRED"
            : !await InstalledAsync(db, ct) ? "IR_REOPEN_SCHEMA_REQUIRED"
            : !await EligibleHandler(closure.HandlerId, request, now, ct) ? "IR_LAST_HANDLER_INELIGIBLE" : null;
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await SessionAsync(ticket, sid, ct); now = await Now(ct); await Readable(ticket.UserId, id, now, ct);
        if (blocker == null && (!await InternalRequestReopenAccess.AllowsAsync(db, ticket.UserId, request, now, ct)
            || !await EligibleHandler(closure.HandlerId, request, now, ct))) blocker = "IR_AUTHORITY_CHANGED";
        if (blocker == null && !InternalRequestReopenSource.WithinWindow(closure.ClosedAt, now, options.Value.WindowDays)) blocker = "IR_REOPEN_WINDOW_EXPIRED";
        await tx.CommitAsync(ct);
        return new(id, Convert.ToBase64String(version), closure.Result, Utc(closure.ClosedAt), Utc(now), options.Value.WindowDays, closure.HandlerId, blocker == null, blocker);
    }

    public async Task<InternalRequestReopenResult> ReopenAsync(SessionReference ticket, long sid, long id, byte[] expected,
        string reason, CancellationToken ct)
    {
        reason = reason?.Trim() ?? "";
        if (id <= 0 || expected.Length != 8 || reason.Length is < 10 or > 4000 || reason.Any(char.IsControl))
            throw new ValidationException("Loaded version and reopen reason of 10 to 4000 characters required.");
        await SessionAsync(ticket, sid, ct);
        var admissionNow = await Now(ct);
        var admission = await Readable(ticket.UserId, id, admissionNow, ct);
        if (!await InternalRequestReopenAccess.AllowsAsync(db, ticket.UserId, admission, admissionNow, ct))
            throw new InternalRequestReadException(403, "IR_REOPEN_REQUIRED");
        if (!await InstalledAsync(db, ct)) throw new InternalRequestReadException(503, "IR_REOPEN_SCHEMA_REQUIRED");
        // Hint only for ordered user locks; every identity is rebound to the proven closure under lock.
        var hints = await db.Database.SqlQuery<long>($"""
            SELECT a.assignee_id AS Value FROM dbo.internal_request_closures c
            JOIN dbo.internal_requests r ON r.internal_request_id=c.internal_request_id AND r.closed_at=c.closed_at
            JOIN dbo.internal_request_assignments a ON a.assignment_id=c.assignment_id AND a.internal_request_id=r.internal_request_id
            WHERE r.internal_request_id={id} AND r.status='CLOSED'
            """).ToArrayAsync(ct);
        if (hints.Length != 1) throw new InternalRequestReadException(409, "IR_CLOSURE_INCONSISTENT");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([ticket.UserId, hints[0]], ct); await SessionAsync(ticket, sid, ct);
            var now = await Now(ct); var request = await Readable(ticket.UserId, id, now, ct);
            if (!await InternalRequestReopenAccess.AllowsAsync(db, ticket.UserId, request, now, ct)) throw new InternalRequestReadException(403, "IR_REOPEN_REQUIRED");
            var closure = await InternalRequestReopenSource.ReadAsync(db, id, options.Value.WindowDays, ct);
            if (closure.HandlerId != hints[0]) throw new InternalRequestReadException(409, "IR_CLOSURE_CHANGED");
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(expected)) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            now = await Now(ct);
            if (!await EligibleHandler(closure.HandlerId, request, now, ct)) throw new InternalRequestReadException(409, "IR_LAST_HANDLER_INELIGIBLE");
            await InternalRequestHandoverStore.EnsureNoPendingAsync(db, id, ct);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.internal_requests SET status='IN_PROGRESS',closed_at=NULL,completed_at=NULL,updated_at={now}
                WHERE internal_request_id={id} AND status='CLOSED' AND row_version={expected}
                """, ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var assignment = new InternalRequestAssignment { InternalRequestId = id, AssigneeId = closure.HandlerId,
                AssignedBy = ticket.UserId, AssignedAt = now, Status = "ACTIVE", AssignmentNote = reason };
            var entry = new InternalRequestEvent { InternalRequestId = id, ActorId = ticket.UserId, OccurredAt = now,
                EventType = "REOPEN", OldStatus = "CLOSED", NewStatus = "IN_PROGRESS", EventNote = reason };
            db.InternalRequestAssignments.Add(assignment); db.InternalRequestEvents.Add(entry); await db.SaveChangesAsync(ct);
            var audit = new AuditLog { BranchId = request.BranchId, ActorUserId = ticket.UserId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { status = "CLOSED", request.CompletedAt, request.ClosedAt, closureEventId = closure.EventId, assignmentId = closure.AssignmentId }),
                NewValues = JsonSerializer.Serialize(new { operation = "REOPEN", status = "IN_PROGRESS", assignmentId = assignment.AssignmentId,
                    handlerId = closure.HandlerId, receivingDepartmentId = request.ReceivingDepartmentId, reason, windowDays = options.Value.WindowDays }),
                ChangedFields = "[\"status\",\"closedAt\",\"completedAt\",\"handler\"]" };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.internal_request_reopens(event_id,closure_event_id,assignment_id,audit_id,receiving_department_id,window_days)
                VALUES({entry.EventId},{closure.EventId},{assignment.AssignmentId},{audit.AuditId},{request.ReceivingDepartmentId},{options.Value.WindowDays})
                """, ct);
            await SessionAsync(ticket, sid, ct); var finalNow = await Now(ct);
            if (!await InternalRequestReopenAccess.AllowsAsync(db, ticket.UserId, request, finalNow, ct)
                || !await EligibleHandler(closure.HandlerId, request, finalNow, ct)) throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            if (!InternalRequestReopenSource.WithinWindow(closure.ClosedAt, finalNow, options.Value.WindowDays))
                throw new InternalRequestReadException(409, "IR_REOPEN_WINDOW_EXPIRED");
            await Readable(closure.HandlerId, id, finalNow, ct);
            var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct); await tx.CommitAsync(ct);
            return new(id, entry.EventId, "IN_PROGRESS", Convert.ToBase64String(version), "MSG08");
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
