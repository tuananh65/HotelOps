using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class AuditReadService(HotelOpsDbContext db, InsightAccess access)
{
    private async Task<IQueryable<AuditLog>> ScopedAsync(AuditQuery query, CancellationToken ct)
    {
        if (query.DepartmentId is not null)
            throw new InsightException(400, "AUDIT_DEPARTMENT_UNAVAILABLE", "Audit entries do not record a department scope.");
        var global = await access.HasGlobalAsync("AUDIT_VIEW", ct);
        long[] branches = global && query.BranchId is null ? [] : await access.BranchesAsync("AUDIT_VIEW", query.BranchId, ct);
        var q = db.AuditLogs.AsNoTracking();
        if (!global || query.BranchId is not null) q = q.Where(a => a.BranchId.HasValue && branches.Contains(a.BranchId.Value));
        if (query.From is { } from) q = q.Where(a => a.OccurredAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.To is { } to)
        {
            if (to == DateOnly.MaxValue || query.From > to) throw new InsightException(400, "INVALID_PERIOD", "Select a valid UTC calendar period.");
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(a => a.OccurredAt < end);
        }
        if (query.ActorId is { } actor) q = q.Where(a => a.ActorUserId == actor);
        if (!string.IsNullOrWhiteSpace(query.EntityType)) q = q.Where(a => a.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.EntityId)) q = q.Where(a => a.EntityId == query.EntityId);
        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(a => a.Action == query.Action);
        // Search metadata only. Never search raw secret-bearing JSON into snippets.
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(a => a.EntityType.Contains(query.Search) || a.EntityId.Contains(query.Search) || a.Action.Contains(query.Search));
        return q;
    }

    public async Task<AuditPage> ListAsync(AuditQuery query, CancellationToken ct)
    {
        if (query.Page <= 0 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new InsightException(400, "INVALID_PAGE", "Invalid page.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var q = await ScopedAsync(query, ct);
        var count = await q.CountAsync(ct);
        var entries = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.AuditId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(a => new { a.AuditId, a.BranchId, a.ActorUserId, Actor = a.ActorUser == null ? null : a.ActorUser.FullName, a.Action, a.EntityType, a.EntityId, a.OccurredAt }).ToArrayAsync(ct);
        await tx.CommitAsync(ct);
        return new(entries.Select(a => new AuditListRow(a.AuditId, a.BranchId, a.ActorUserId, a.Actor,
            a.Action, a.EntityType, a.EntityId, InsightAccess.Utc(a.OccurredAt))).ToArray(), count, query.Page, query.PageSize);
    }

    public async Task<AuditDetail> DetailAsync(long id, CancellationToken ct)
    {
        var q = await ScopedAsync(new(), ct);
        var row = await q.Where(a => a.AuditId == id).Select(a => new { a.AuditId, a.BranchId, a.ActorUserId,
            Actor = a.ActorUser == null ? null : a.ActorUser.FullName, a.Action, a.EntityType, a.EntityId, a.OccurredAt,
            a.RequestId, a.OldValues, a.NewValues }).SingleOrDefaultAsync(ct);
        if (row is null) throw new InsightException(404, "AUDIT_NOT_FOUND", "Audit entry unavailable.");
        var entry = new AuditListRow(row.AuditId, row.BranchId, row.ActorUserId, row.Actor, row.Action, row.EntityType,
            row.EntityId, InsightAccess.Utc(row.OccurredAt));
        return new(entry, row.RequestId, AuditRedaction.Read(row.OldValues), AuditRedaction.Read(row.NewValues),
            "dbo.audit_logs / audit_id; persisted source event; unrecorded changes are unavailable", true);
    }
}
