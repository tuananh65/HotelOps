using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestCreationContextView(long RequestId, long BranchId, string Version,
    string Subject, string Description, long? RoomId, long? AreaId, long? PriorityId);

// Read-only source defaults. Target creation permissions are independently enforced by
// the existing Incident/Material commands; this response never constitutes a permit.
public sealed class InternalRequestCreationContext(HotelOpsDbContext db, ISqlSessionService sessions,
    ICurrentUserAccessor current, InternalRequestProcessAuthority authority)
{
    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    public async Task<InternalRequestCreationContextView> ReadAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (id <= 0) throw new InternalRequestReadException(400, "IR_ID_REQUIRED");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await authority.InstalledAsync(ct))
            throw new InternalRequestReadException(503, "IR_LINK_SCHEMA_REQUIRED");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        var row = await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
        if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct))
            throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        if (row.Status is not ("ACCEPTED" or "IN_PROGRESS")) throw new InternalRequestReadException(409, "IR_LINK_STATE_INVALID");
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await SessionAsync(ticket, sid, ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        if (!await scope.Apply(db.InternalRequests.AsNoTracking(), now).AnyAsync(r => r.InternalRequestId == id, ct)
            || !await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct))
            throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
        await tx.CommitAsync(ct);
        return new(id, row.BranchId, Convert.ToBase64String(version), row.Subject, row.Description, row.RoomId, row.AreaId, row.PriorityId);
    }
}
