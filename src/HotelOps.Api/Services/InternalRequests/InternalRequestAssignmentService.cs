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

public sealed record InternalRequestAssignmentInput(long HandlerId, string? Note);
public sealed record InternalRequestAssignmentResult(long RequestId, long AssignmentId, long EventId, string Status, string Version);
public sealed record InternalRequestHandler(long UserId, string Name, int OpenRequests);
public sealed record InternalRequestAssignmentView(long RequestId, string Status, string Version, long? HandlerId, InternalRequestHandler[] Handlers);

// UC115 assignment and reassignment; routing remains a separate operation.
public sealed class InternalRequestAssignmentService(HotelOpsDbContext db, InternalRequestProcessAuthority authority,
    ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static string? Validate(InternalRequestAssignmentInput input)
    {
        var note = input.Note?.Trim();
        if (input.HandlerId <= 0 || note?.Length > 4000 || note?.Any(char.IsControl) == true)
            throw new ValidationException("Select a handler; note must be at most 4000 characters without controls.");
        return string.IsNullOrEmpty(note) ? null : note;
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private async Task<bool> HandlerAsync(long handler, long branch, long department, DateTime now, CancellationToken ct) =>
        await InternalRequestEmployeeMembership.InstalledAsync(db, ct)
        && await InternalRequestEmployeeMembership.Members(db, branch, department, now).AnyAsync(u => u.UserId == handler, ct);

    public async Task<InternalRequestAssignmentView> ReadAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Request identifier required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestEmployeeMembership.InstalledAsync(db, ct) || !await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_ASSIGNMENT_SCHEMA_REQUIRED");
        if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        var request = await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct);
        if (request == null) throw new InternalRequestReadException(404, "IR_NOT_FOUND");
        var handlers = await InternalRequestEmployeeMembership.Members(db, request.BranchId, request.ReceivingDepartmentId, now)
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => new InternalRequestHandler(u.UserId, u.FullName,
                u.InternalRequestAssignmentAssignees.Count(a => a.Status == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now
                    && a.InternalRequest.BranchId == request.BranchId && a.InternalRequest.ReceivingDepartmentId == request.ReceivingDepartmentId
                    && a.InternalRequest.Status == "IN_PROGRESS"))).ToArrayAsync(ct);
        var currentHandler = await db.InternalRequestAssignments.AsNoTracking().Where(a => a.InternalRequestId == id && a.Status == "ACTIVE" && a.EndedAt == null)
            .Select(a => (long?)a.AssigneeId).SingleOrDefaultAsync(ct);
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await SessionAsync(ticket, sid, ct);
        if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
        var finalNow = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var finalScope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, finalNow, ct);
        if (!await finalScope.Apply(db.InternalRequests.AsNoTracking(), finalNow).AnyAsync(r => r.InternalRequestId == id, ct))
            throw new InternalRequestReadException(403, "IR_SCOPE_CHANGED");
        await tx.CommitAsync(ct);
        return new(id, request.Status, Convert.ToBase64String(version), currentHandler, handlers);
    }

    public async Task<InternalRequestAssignmentResult> AssignAsync(SessionReference ticket, long sid, long id,
        byte[] expectedVersion, InternalRequestAssignmentInput input, CancellationToken ct)
    {
        var note = Validate(input);
        if (id <= 0 || expectedVersion.Length != 8) throw new ValidationException("Loaded request version required.");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Assignment requires a clean context.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Same stable order as account lifecycle operations; never take an assignee lock after a request lock.
            await new UserEligibilityLock(db).LockAsync([ticket.UserId, input.HandlerId], ct);
            await SessionAsync(ticket, sid, ct);
            if (!await InternalRequestEmployeeMembership.InstalledAsync(db, ct) || !await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct))
                throw new InternalRequestReadException(503, "IR_ASSIGNMENT_SCHEMA_REQUIRED");
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct))
                throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
            var request = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == id, ct);
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(expectedVersion))
                throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            if (request.Status is not ("ACCEPTED" or "IN_PROGRESS"))
                throw new InternalRequestReadException(409, "IR_ACCEPT_FIRST");
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), DateTimeKind.Utc);
            if (request.ReceivingDepartmentId is not {} department || !await HandlerAsync(input.HandlerId, request.BranchId, department, now, ct))
                throw new InternalRequestReadException(400, "IR_HANDLER_INELIGIBLE");
            var active = await db.InternalRequestAssignments.AsNoTracking().Where(a => a.InternalRequestId == id && a.Status == "ACTIVE").ToArrayAsync(ct);
            if (active.Length > 1 || active.Any(a => a.EndedAt != null || a.AssignedAt > now)
                || request.Status == "ACCEPTED" && active.Length != 0)
                throw new InternalRequestReadException(409, "IR_ASSIGNMENT_INCONSISTENT");
            if (active.Any(a => a.AssigneeId == input.HandlerId)) throw new InternalRequestReadException(409, "IR_HANDLER_UNCHANGED");
            // End the old assignment before inserting the new one: the existing filtered unique index stays authoritative.
            await InternalRequestHandoverStore.EnsureNoPendingAsync(db,id,ct);
            await db.InternalRequestAssignments.Where(a => a.InternalRequestId == id && a.Status == "ACTIVE")
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "REASSIGNED").SetProperty(a => a.EndedAt, now), ct);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET status='IN_PROGRESS',updated_at={now} WHERE internal_request_id={id} AND row_version={expectedVersion}", ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var assignment = new InternalRequestAssignment { InternalRequestId = id, AssigneeId = input.HandlerId,
                AssignedBy = ticket.UserId, AssignedAt = now, Status = "ACTIVE", AssignmentNote = note };
            var entry = new InternalRequestEvent { InternalRequestId = id, EventType = active.Length == 0 ? "ASSIGN" : "REASSIGN",
                OldStatus = request.Status, NewStatus = "IN_PROGRESS", EventNote = note, ActorId = ticket.UserId, OccurredAt = now };
            db.InternalRequestAssignments.Add(assignment); db.InternalRequestEvents.Add(entry);
            db.AuditLogs.Add(new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { status = request.Status, handler = active.SingleOrDefault()?.AssigneeId }),
                NewValues = JsonSerializer.Serialize(new { operation = entry.EventType, status = "IN_PROGRESS", handler = input.HandlerId, department, note }),
                ChangedFields = "[\"status\",\"handler\"]" });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            // Separate from the accept/reject outbox: two recipients, deduplicated per committed event.
            foreach (var recipient in new[] { request.RequesterId, input.HandlerId }.Distinct().Order())
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_assignment_outbox(event_id,recipient_id,assignment_id,created_at) VALUES({entry.EventId},{recipient},{assignment.AssignmentId},{now})", ct);
            await SessionAsync(ticket, sid, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)
                || !await HandlerAsync(input.HandlerId, request.BranchId, department, await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), ct))
                throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct);
            return new(id, assignment.AssignmentId, entry.EventId, "IN_PROGRESS", Convert.ToBase64String(version));
        }
        finally { db.ChangeTracker.Clear(); }
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestAssignment.V1' AND CONVERT(nvarchar(20),value)='1')
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_assignment_outbox')
                AND name IN('FK_ir_assignment_event','FK_ir_assignment_recipient','FK_ir_assignment_assignment','FK_ir_assignment_notification')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=4
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_assignment_outbox')
                AND name='PK_internal_request_assignment_outbox' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
}
