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

public sealed record InternalRequestRouteInput(long DepartmentId, string Reason);
public sealed record InternalRequestRouteResult(long RequestId, long DepartmentId, long EventId, string Version);
public sealed record InternalRequestRouteDepartment(long DepartmentId, string Name);
public sealed record InternalRequestRouteView(long RequestId, string Status, string Version, InternalRequestRouteDepartment[] Departments);

// UC115 transfers current receiving-department responsibility while preserving Pending Acceptance.
public sealed class InternalRequestRouteService(HotelOpsDbContext db, InternalRequestProcessAuthority authority,
    ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static string Validate(InternalRequestRouteInput input)
    {
        var reason = input.Reason?.Trim();
        if (input.DepartmentId <= 0 || reason is null || reason.Length is < 10 or > 4000 || reason.Any(char.IsControl))
            throw new ValidationException("Routing requires a target department and a reason of 10–4000 characters without controls.");
        return reason;
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var user) || user.UserId != ticket.UserId || user.SessionId != sid || user.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private Task<long[]> Candidates(long department, CancellationToken ct) => db.Database.SqlQuery<long>($"""
        SELECT DISTINCT user_id AS Value FROM dbo.department_management_access
        WHERE department_id={department} AND revoked_at IS NULL AND granted_at<=SYSUTCDATETIME()
        """).OrderBy(id => id).ToArrayAsync(ct);

    public async Task<InternalRequestRouteView> ReadAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        if(id<=0)throw new ValidationException("Request identifier required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);
        if(!await InternalRequestDecisionStore.InstalledAsync(db,ct)||!await InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_ROUTE_SCHEMA_REQUIRED");
        if(!await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_PROCESS_REQUIRED");
        var now=DateTime.UtcNow;var scope=await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId,now,ct);
        var request=await scope.Apply(db.InternalRequests.AsNoTracking(),now).SingleOrDefaultAsync(r=>r.InternalRequestId==id,ct);
        if(request==null)throw new InternalRequestReadException(404,"IR_NOT_FOUND");
        var departments=await db.Departments.AsNoTracking().Where(d=>d.BranchId==request.BranchId&&d.IsActive&&d.DepartmentId!=request.ReceivingDepartmentId)
            .OrderBy(d=>d.DepartmentName).ThenBy(d=>d.DepartmentId).Select(d=>new InternalRequestRouteDepartment(d.DepartmentId,d.DepartmentName)).ToArrayAsync(ct);
        var version=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);await SessionAsync(ticket,sid,ct);
        if(!await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
        now=DateTime.UtcNow;scope=await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId,now,ct);
        if(!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==id,ct))throw new InternalRequestReadException(403,"IR_SCOPE_CHANGED");
        await tx.CommitAsync(ct);return new(id,request.Status,Convert.ToBase64String(version),departments);
    }

    public async Task<InternalRequestRouteResult> RouteAsync(SessionReference ticket, long sid, long id, byte[] version,
        InternalRequestRouteInput input, CancellationToken ct)
    {
        var reason = Validate(input);
        if (id <= 0 || version.Length != 8) throw new ValidationException("Loaded request version required.");
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction != null) throw new InvalidOperationException("Clean independent route context required.");
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct) || !await authority.InstalledAsync(ct))
            throw new InternalRequestReadException(503, "IR_ROUTE_SCHEMA_REQUIRED");
        // Hints only. The exact set is re-read under Serializable after all hinted user locks are acquired.
        var candidates = await Candidates(input.DepartmentId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync(candidates.Append(ticket.UserId), ct);
            await SessionAsync(ticket, sid, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
            var request = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == id, ct);
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(version)) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            if (request.Status != "PENDING_ACCEPTANCE") throw new InternalRequestReadException(409, "IR_ROUTE_REQUIRES_PENDING");
            if (request.ReceivingDepartmentId is not {} previous || previous == input.DepartmentId)
                throw new InternalRequestReadException(400, "IR_DIFFERENT_DEPARTMENT_REQUIRED");
            if (!await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == input.DepartmentId && d.BranchId == request.BranchId && d.IsActive && d.Branch.IsActive, ct))
                throw new InternalRequestReadException(400, "IR_TARGET_DEPARTMENT_INVALID");
            if (await db.InternalRequestAssignments.AnyAsync(a => a.InternalRequestId == id && a.Status == "ACTIVE", ct))
                throw new InternalRequestReadException(409, "IR_ASSIGNMENT_INCONSISTENT");
            var lockedCandidates = await Candidates(input.DepartmentId, ct);
            if (!candidates.SequenceEqual(lockedCandidates)) throw new InternalRequestReadException(409, "IR_RECIPIENT_SCOPE_CHANGED");
            var recipients = new HashSet<long> { request.RequesterId };
            foreach (var candidate in candidates)
                if (await authority.AllowsDepartmentAsync(candidate, request.BranchId, input.DepartmentId, ct)) recipients.Add(candidate);
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), DateTimeKind.Utc);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET receiving_department_id={input.DepartmentId},updated_at={now} WHERE internal_request_id={id} AND row_version={version} AND status='PENDING_ACCEPTANCE'", ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var entry = new InternalRequestEvent { InternalRequestId = id, EventType = "ROUTE", OldStatus = request.Status, NewStatus = request.Status,
                EventNote = reason, ActorId = ticket.UserId, OccurredAt = now };
            db.InternalRequestEvents.Add(entry);
            db.AuditLogs.Add(new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { receivingDepartmentId = previous, status = request.Status }),
                NewValues = JsonSerializer.Serialize(new { operation = "ROUTE", receivingDepartmentId = input.DepartmentId, status = request.Status, reason }),
                ChangedFields = "[\"receiving_department_id\"]" });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            foreach (var recipient in recipients.Order())
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_route_outbox(event_id,recipient_id,branch_id,previous_department_id,target_department_id,is_requester,created_at) VALUES({entry.EventId},{recipient},{request.BranchId},{previous},{input.DepartmentId},{recipient == request.RequesterId},{now})", ct);
            await SessionAsync(ticket, sid, ct);
            // Revalidate the actor's authority for the operation's origin, never grant ownership of the destination.
            if (!await authority.AllowsDepartmentAsync(ticket.UserId, request.BranchId, previous, ct)) throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            var revision = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct);
            return new(id, input.DepartmentId, entry.EventId, Convert.ToBase64String(revision));
        }
        finally { db.ChangeTracker.Clear(); }
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestRoute.V1' AND CONVERT(nvarchar(20),value)='1')
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_route_outbox')
                AND name IN('FK_ir_route_event','FK_ir_route_recipient','FK_ir_route_previous','FK_ir_route_target','FK_ir_route_notification')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=5
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_route_outbox')
                AND name='PK_internal_request_route_outbox' AND is_primary_key=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
}
