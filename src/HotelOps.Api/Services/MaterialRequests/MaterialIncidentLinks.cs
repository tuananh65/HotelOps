using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed class MaterialIncidentLinks(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    private static InternalRequestReadException Error(int status, string code) => new(status, code);
    private async Task<MaterialRequest> RequestAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw Error(401, "SESSION_INVALID");
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        var session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw Error(401, "SESSION_INVALID");
        var row = await db.MaterialRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.material_requests WITH(UPDLOCK,HOLDLOCK) WHERE request_id={id}").AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw Error(404, "MR_NOT_FOUND");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, ticket.UserId, row.BranchId, now, ct);
        if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == id, ct)
            || !await MaterialReadProvenance.ValidAsync(db, id, row.BranchId, ct)) throw Error(404, "MR_NOT_FOUND");
        return row;
    }
    private async Task<bool> CanLink(long user, MaterialRequest request, CancellationToken ct)
    {
        if (request.RequesterId != user || request.Status is "CANCELLED" or "REJECTED") return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return (await MaterialCreationAccess.BranchesAsync(db, user, now, ct)).Any(b => b.BranchId == request.BranchId);
    }
    private async Task<IQueryable<Incident>> Population(long user, MaterialRequest request, bool candidates, CancellationToken ct)
    {
        var scope = await IncidentReadAccess.ReadAsync(db, user, request.BranchId, ct);
        var rows = scope.Apply(db.Incidents.AsNoTracking());
        return candidates ? rows.Where(i => request.IncidentId == null && (i.Status == "OPEN" || i.Status == "IN_PROGRESS" || i.Status == "RESOLVED"))
            : rows.Where(i => i.IncidentId == request.IncidentId);
    }
    public async Task<MaterialTaskLinksView> ReadAsync(SessionReference ticket, long sid, long id, bool candidates, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw Error(400, "INVALID_PAGE");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await RequestAsync(ticket, sid, id, ct);
        var canLink = await CanLink(ticket.UserId, request, ct);
        if (candidates && !canLink) throw Error(403, "MR_LINK_FORBIDDEN");
        var tasks = await Population(ticket.UserId, request, candidates, ct);
        var total = await tasks.CountAsync(ct);
        var rows = await tasks.OrderByDescending(t => t.ReportedAt).ThenByDescending(t => t.IncidentId)
            .Skip((page - 1) * 25).Take(25).Select(t => new { TaskId = t.IncidentId, TaskCode = t.IncidentNo, Title = t.IncidentTitle, State = t.Status }).ToArrayAsync(ct);
        var ids = rows.Select(t => t.TaskId).ToArray();
        request = await RequestAsync(ticket, sid, id, ct);
        var finalCanLink = await CanLink(ticket.UserId, request, ct);
        if (canLink != finalCanLink) throw Error(403, "MR_LINK_AUTHORITY_CHANGED");
        var final = await Population(ticket.UserId, request, candidates, ct);
        if (await final.CountAsync(ct) != total || await final.CountAsync(t => ids.Contains(t.IncidentId), ct) != ids.Length)
            throw Error(403, "MR_TARGET_SCOPE_CHANGED");
        await tx.CommitAsync(ct);
        return new(id, request.BranchId, Convert.ToBase64String(request.RowVersion), canLink, candidates, page, total,
            rows.Select(t => new MaterialTaskLinkRow(t.TaskId, t.TaskCode, t.Title, t.State)).ToArray());
    }
    public async Task<string> LinkAsync(SessionReference ticket, long sid, long id, MaterialTaskLinkInput input, CancellationToken ct)
    {
        byte[] version;
        try { version = Convert.FromBase64String(input.ExpectedRowVersion ?? ""); }
        catch (FormatException) { throw Error(400, "MR_VERSION_INVALID"); }
        if (id <= 0 || input.TaskId <= 0 || version.Length != 8 || Convert.ToBase64String(version) != input.ExpectedRowVersion)
            throw Error(400, "MR_LINK_INPUT_INVALID");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean link context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var request = await RequestAsync(ticket, sid, id, ct);
            if (!await CanLink(ticket.UserId, request, ct)) throw Error(403, "MR_LINK_FORBIDDEN");
            if (!request.RowVersion.SequenceEqual(version)) throw Error(412, "MR_VERSION_CHANGED");
            if (request.IncidentId != null) throw Error(409, "MR_INCIDENT_ALREADY_LINKED");
            if (!await (await Population(ticket.UserId, request, true, ct)).AnyAsync(t => t.IncidentId == input.TaskId, ct))
                throw Error(404, "MR_OPEN_INCIDENT_NOT_FOUND");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.material_requests SET incident_id={input.TaskId},updated_at={now} WHERE request_id={id} AND row_version={version}", ct) != 1)
                throw Error(412, "MR_VERSION_CHANGED");
            var audit = new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId,
                EntityType = "material_requests", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "UPDATE", OccurredAt = now, ChangedFields = "[\"incidentId\"]",
                OldValues = JsonSerializer.Serialize(new { incidentId = request.IncidentId }),
                NewValues = JsonSerializer.Serialize(new { operation = "LINK_INCIDENT", incidentId = input.TaskId }) };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            request = await RequestAsync(ticket, sid, id, ct);
            if (!await CanLink(ticket.UserId, request, ct)) throw Error(403, "MR_LINK_AUTHORITY_CHANGED");
            if (!await (await Population(ticket.UserId, request, false, ct)).AnyAsync(t => t.IncidentId == input.TaskId, ct)) throw Error(403, "MR_TARGET_SCOPE_CHANGED");
            if (!await (await Population(ticket.UserId, request, false, ct)).AnyAsync(t => t.IncidentId == input.TaskId
                && (t.Status == "OPEN" || t.Status == "IN_PROGRESS" || t.Status == "RESOLVED"), ct)) throw Error(409, "MR_TARGET_NOT_OPEN");
            await tx.CommitAsync(ct);
            return Convert.ToBase64String(request.RowVersion);
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
