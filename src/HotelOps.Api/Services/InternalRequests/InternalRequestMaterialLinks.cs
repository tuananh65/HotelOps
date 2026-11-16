using System.ComponentModel.DataAnnotations;
using System.Data;
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
    InternalRequestMaterialLinkRow[] Items, int Total, int Page);

// Read only. A filtered/empty list never proves that all mandatory material is complete.
// UC120 rechecks every link independently in its closure transaction.
public sealed class InternalRequestMaterialLinks(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
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

    private async Task<IQueryable<Row>> VisibleAsync(long user, long branch, long id, CancellationToken ct)
    {
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, branch, DateTime.UtcNow, ct);
        var materials = scope.Apply(db.MaterialRequests.AsNoTracking());
        var links = db.Database.SqlQuery<Link>($"SELECT material_request_id AS Id,is_mandatory AS Mandatory FROM dbo.internal_request_material_links WHERE internal_request_id={id}");
        // Select only baseline columns: missing lifecycle evidence is represented explicitly,
        // without preventing independently authorized historical records from being read.
        return from link in links join material in materials on link.Id equals material.RequestId
            select new Row { Id = material.RequestId, Code = material.RequestNo, Purpose = material.Purpose,
                State = material.Status, Mandatory = link.Mandatory };
    }

    public async Task<InternalRequestMaterialLinksView> ReadAsync(SessionReference ticket, long sid, long id, int page, CancellationToken ct)
    {
        if (id <= 0 || page < 1 || page > int.MaxValue / 25) throw new ValidationException("Invalid linked material page.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InternalRequestCompletionService.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_MATERIAL_LINK_SCHEMA_REQUIRED");
        await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        var request = await RequestAsync(ticket, sid, id, ct);
        var visible = await VisibleAsync(ticket.UserId, request.BranchId, id, ct);
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
        var final = await VisibleAsync(ticket.UserId, request.BranchId, id, ct);
        var ids = rows.Select(r => r.Id).ToArray();
        if (await final.CountAsync(ct) != total || await final.CountAsync(r => ids.Contains(r.Id), ct) != ids.Length)
            throw new InternalRequestReadException(403, "IR_TARGET_SCOPE_CHANGED");
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await tx.CommitAsync(ct);
        return new(id, request.BranchId, Convert.ToBase64String(version), items.ToArray(), total, page);
    }
}
