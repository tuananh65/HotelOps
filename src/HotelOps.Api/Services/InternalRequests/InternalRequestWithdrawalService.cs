using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestWithdrawalInput(string Reason);
public sealed record InternalRequestWithdrawalView(long RequestId, string Status, string Version, bool CanWithdraw);

// GBR-154 requester authority is separate from department processing authority.
public sealed class InternalRequestWithdrawalService(HotelOpsDbContext db, InternalRequestProcessAuthority authority,
    ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static string Validate(InternalRequestWithdrawalInput input)
    {
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 4000 || reason.Any(char.IsControl))
            throw new ValidationException("Withdrawal requires a reason of at most 4000 characters without controls.");
        return reason;
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private async Task<InternalRequest> ReadCurrentAsync(long user, long id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(user, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
    }

    public async Task<InternalRequestWithdrawalView> ReadAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Request identifier required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_WITHDRAWAL_SCHEMA_REQUIRED");
        var request = await ReadCurrentAsync(ticket.UserId, id, ct);
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await SessionAsync(ticket, sid, ct);
        await ReadCurrentAsync(ticket.UserId, id, ct);
        await tx.CommitAsync(ct);
        return new(id, request.Status, Convert.ToBase64String(version), request.RequesterId == ticket.UserId && request.Status == "PENDING_ACCEPTANCE");
    }

    private Task<long[]> Candidates(long id, CancellationToken ct) => db.Database.SqlQuery<long>($"""
        SELECT DISTINCT m.user_id AS Value FROM dbo.department_management_access m
        JOIN dbo.internal_requests r ON r.branch_id=m.branch_id AND r.receiving_department_id=m.department_id
        WHERE r.internal_request_id={id} AND m.revoked_at IS NULL AND m.granted_at<=SYSUTCDATETIME()
        """).OrderBy(v => v).ToArrayAsync(ct);

    public async Task<InternalRequestDecisionResult> WithdrawAsync(SessionReference ticket, long sid, long id,
        byte[] version, InternalRequestWithdrawalInput input, CancellationToken ct)
    {
        var reason = Validate(input);
        if (id <= 0 || version.Length != 8) throw new ValidationException("Loaded request version required.");
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Clean withdrawal context required.");
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct) || !await authority.InstalledAsync(ct))
            throw new InternalRequestReadException(503, "IR_WITHDRAWAL_SCHEMA_REQUIRED");
        var candidates = await Candidates(id, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync(candidates.Append(ticket.UserId), ct);
            await SessionAsync(ticket, sid, ct);
            // Serialize with Accept/Route/Assign before checking owner, status and version.
            await db.Database.SqlQuery<long>($"SELECT internal_request_id AS Value FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}").ToArrayAsync(ct);
            var request = await ReadCurrentAsync(ticket.UserId, id, ct);
            if (request.RequesterId != ticket.UserId) throw new InternalRequestReadException(403, "IR_REQUESTER_REQUIRED");
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(version))
                throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            if (request.Status != "PENDING_ACCEPTANCE") throw new InternalRequestReadException(409, "IR_NOT_PENDING_ACCEPTANCE");
            if (request.ReceivingDepartmentId is not { } department) throw new InternalRequestReadException(409, "IR_RECEIVING_DEPARTMENT_REQUIRED");
            if (await db.InternalRequestAssignments.AnyAsync(a => a.InternalRequestId == id && a.Status == "ACTIVE", ct))
                throw new InternalRequestReadException(409, "IR_ASSIGNMENT_INCONSISTENT");
            var lockedCandidates = await Candidates(id, ct);
            if (!candidates.SequenceEqual(lockedCandidates)) throw new InternalRequestReadException(409, "IR_RECIPIENT_SCOPE_CHANGED");
            var recipients = new List<long>();
            foreach (var candidate in candidates)
                if (await authority.AllowsDepartmentAsync(candidate, request.BranchId, department, ct)) recipients.Add(candidate);
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), DateTimeKind.Utc);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.internal_requests SET status='CANCELLED',cancellation_reason={reason},updated_at={now}
                WHERE internal_request_id={id} AND requester_id={ticket.UserId} AND status='PENDING_ACCEPTANCE' AND row_version={version}
                """, ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var entry = new InternalRequestEvent { InternalRequestId = id, EventType = "WITHDRAW", OldStatus = request.Status,
                NewStatus = "CANCELLED", EventNote = reason, ActorId = ticket.UserId, OccurredAt = now };
            db.InternalRequestEvents.Add(entry);
            db.AuditLogs.Add(new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { status = request.Status, cancellationReason = request.CancellationReason }),
                NewValues = JsonSerializer.Serialize(new { operation = "WITHDRAW", status = "CANCELLED", cancellationReason = reason }),
                ChangedFields = "[\"status\",\"cancellation_reason\"]" });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            foreach (var recipient in recipients)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_withdrawal_outbox(event_id,recipient_id,branch_id,department_id,created_at) VALUES({entry.EventId},{recipient},{request.BranchId},{department},{now})", ct);
            await SessionAsync(ticket, sid, ct);
            if ((await ReadCurrentAsync(ticket.UserId, id, ct)).RequesterId != ticket.UserId)
                throw new InternalRequestReadException(403, "IR_REQUESTER_REQUIRED");
            var finalVersion = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct);
            return new(id, "CANCELLED", Convert.ToBase64String(finalVersion), entry.EventId, "MSG08");
        }
        finally { db.ChangeTracker.Clear(); }
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestWithdrawal.V1' AND CONVERT(nvarchar(20),value)='1')
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_withdrawal_outbox')
                AND name IN('FK_ir_withdrawal_event','FK_ir_withdrawal_recipient','FK_ir_withdrawal_department','FK_ir_withdrawal_notification')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=4
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_withdrawal_outbox')
                AND name='PK_internal_request_withdrawal_outbox' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
}
