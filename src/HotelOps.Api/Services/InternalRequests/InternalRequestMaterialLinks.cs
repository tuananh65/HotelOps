using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestMaterialLinkRow(long MaterialRequestId, string Code, string Purpose,
    string State, bool Mandatory, bool? CompletionSatisfied, string? CompletionUnavailableReason);
public sealed record InternalRequestMaterialLinksView(long RequestId, long BranchId, string Version,
    InternalRequestMaterialLinkRow[] Items, int Total, int Page, bool Candidates, bool CanLink);
public sealed record InternalRequestMaterialLinkInput(long MaterialRequestId, [property: JsonRequired] bool Mandatory);

// A filtered/empty list never proves that all mandatory material is complete.
// UC120 rechecks every link independently in its closure transaction.
public sealed class InternalRequestMaterialLinks(HotelOpsDbContext db, ISqlSessionService sessions,
    ICurrentUserAccessor current, InternalRequestProcessAuthority authority)
{
    private static readonly string[] OpenStates = ["DRAFT", "PENDING_APPROVAL", "NEEDS_INFORMATION", "APPROVED", "PARTIALLY_ISSUED"];
    private sealed class Link { public long Id { get; set; } public bool Mandatory { get; set; } }
    private sealed class Row
    {
        public long Id { get; set; }
        public string Code { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string State { get; set; } = "";
        public bool Mandatory { get; set; }
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    private async Task<InternalRequest> RequestAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        await SessionAsync(ticket, sid, ct);
        var now = DateTime.UtcNow;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
    }

    private IQueryable<Link> Links(long id) => db.Database.SqlQuery<Link>($"SELECT material_request_id AS Id,is_mandatory AS Mandatory FROM dbo.internal_request_material_links WHERE internal_request_id={id}");

    private async Task<IQueryable<Row>> VisibleAsync(long user, long branch, long id, bool candidates, CancellationToken ct)
    {
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, branch, DateTime.UtcNow, ct);
        var materials = scope.Apply(db.MaterialRequests.AsNoTracking());
        var links = Links(id);
        if (candidates)
        {
            var linked = links.Select(l => l.Id);
            return materials.Where(m => OpenStates.Contains(m.Status) && !linked.Contains(m.RequestId))
                .Select(m => new Row { Id = m.RequestId, Code = m.RequestNo, Purpose = m.Purpose, State = m.Status, Mandatory = false });
        }
        // Select only baseline columns: missing lifecycle evidence is represented explicitly,
        // without preventing independently authorized historical records from being read.
        return from link in links join material in materials on link.Id equals material.RequestId
            select new Row { Id = material.RequestId, Code = material.RequestNo, Purpose = material.Purpose,
                State = material.Status, Mandatory = link.Mandatory };
    }

    private async Task<InternalRequest> LockRequestAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InternalRequestCompletionService.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_MATERIAL_LINK_SCHEMA_REQUIRED");
        await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        return await RequestAsync(ticket, sid, id, ct);
    }

    public async Task<InternalRequestMaterialLinksView> ReadAsync(SessionReference ticket, long sid, long id, bool candidates, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw new ValidationException("Invalid linked material page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await LockRequestAsync(ticket, sid, id, ct);
        var canLink = request.Status is "ACCEPTED" or "IN_PROGRESS" && await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct);
        if (candidates && !canLink) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var visible = await VisibleAsync(ticket.UserId, request.BranchId, id, candidates, ct);
        var total = await visible.CountAsync(ct);
        var rows = await visible.OrderBy(r => r.Id).Skip((page - 1) * 25).Take(25).ToArrayAsync(ct);
        var items = new List<InternalRequestMaterialLinkRow>();
        foreach (var row in rows)
        {
            MaterialCompletionEvidence? evidence = row.Mandatory
                ? await MaterialCompletionEvidence.ReadAsync(db, row.Id, request.BranchId, ct) : null;
            items.Add(new(row.Id, row.Code, row.Purpose, row.State, row.Mandatory,
                evidence is null ? null : evidence.FullyIssuedProven || evidence.RemainderClosureProven, evidence?.Failure));
        }
        await RequestAsync(ticket, sid, id, ct);
        var final = await VisibleAsync(ticket.UserId, request.BranchId, id, candidates, ct);
        var ids = rows.Select(r => r.Id).ToArray();
        if (await final.CountAsync(ct) != total || await final.CountAsync(r => ids.Contains(r.Id), ct) != ids.Length)
            throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
        canLink = request.Status is "ACCEPTED" or "IN_PROGRESS" && await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct);
        if (candidates && !canLink) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await tx.CommitAsync(ct);
        return new(id, request.BranchId, Convert.ToBase64String(version), items.ToArray(), total, page, candidates, canLink);
    }

    public async Task<string> LinkAsync(SessionReference ticket, long sid, long id, byte[] version, InternalRequestMaterialLinkInput input, CancellationToken ct)
    {
        if (id <= 0 || input.MaterialRequestId <= 0 || version.Length != 8) throw new ValidationException("Request, material and loaded version required.");
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction != null) throw new InvalidOperationException("Clean link context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var request = await LockRequestAsync(ticket, sid, id, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(version)) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            if (request.Status is not ("ACCEPTED" or "IN_PROGRESS")) throw new InternalRequestReadException(409, "IR_LINK_STATE_INVALID");
            var scope = await MaterialRequestReadAccess.ReadAsync(db, ticket.UserId, request.BranchId, DateTime.UtcNow, ct);
            var state = await scope.Apply(db.MaterialRequests.AsNoTracking()).Where(m => m.RequestId == input.MaterialRequestId).Select(m => m.Status).SingleOrDefaultAsync(ct)
                ?? throw new InternalRequestReadException(404, "IR_TARGET_NOT_FOUND");
            if (!OpenStates.Contains(state)) throw new InternalRequestReadException(409, "IR_TARGET_NOT_OPEN");
            if (await Links(id).AnyAsync(l => l.Id == input.MaterialRequestId, ct)) throw new InternalRequestReadException(409, "IR_MATERIAL_ALREADY_LINKED");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var audit = new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                NewValues = JsonSerializer.Serialize(new { operation = "LINK_MATERIAL", input.MaterialRequestId, input.Mandatory }), ChangedFields = "[\"linked_work\"]" };
            db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_material_links(internal_request_id,material_request_id,is_mandatory,audit_id) VALUES({id},{input.MaterialRequestId},{input.Mandatory},{audit.AuditId})", ct);
            if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={id} AND row_version={version}", ct) != 1)
                throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            await RequestAsync(ticket, sid, id, ct);
            if (!await authority.AllowsCurrentRequestAsync(ticket.UserId, id, ct)) throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            scope = await MaterialRequestReadAccess.ReadAsync(db, ticket.UserId, request.BranchId, DateTime.UtcNow, ct);
            if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(m => m.RequestId == input.MaterialRequestId && OpenStates.Contains(m.Status), ct))
                throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
            var next = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct);
            return Convert.ToBase64String(next);
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
