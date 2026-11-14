using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestHistoryFilter(
    long? RequestTypeId = null, long? PriorityId = null, long? ReceivingDepartmentId = null,
    string? Status = null, string? Search = null, bool? Overdue = null, int Page = 1, int PageSize = 25);
public sealed record InternalRequestHistoryRow(long Id, string Number, long BranchId, string Subject,
    string Status, long TypeId, long? PriorityId, long? ReceivingDepartmentId, DateTime? Deadline, bool Overdue);
public sealed record InternalRequestHistoryPage(IReadOnlyList<InternalRequestHistoryRow> Items, int TotalCount,
    int Page, int PageSize, DateTime SnapshotUtc);
public sealed record InternalRequestTimelineEvent(long Id, string Type, string? OldStatus, string? NewStatus,
    string? Note, long ActorId, DateTime OccurredAt);
public sealed record InternalRequestTimeline(long RequestId, IReadOnlyList<InternalRequestTimelineEvent> Events);

/// <summary>
/// Query component only. Caller must supply an authorized, branch-scoped EF query and a stable UTC snapshot
/// inside its authorization/read transaction. No controller or DI registration grants access through this type.
/// Linked work is deliberately absent: its separate resource authorization has not been established.
/// </summary>
public static class InternalRequestHistoryQueries
{
    public static async Task<InternalRequestHistoryPage> ListAsync(IQueryable<InternalRequest> authorizedRequests,
        InternalRequestHistoryFilter filter, DateTime snapshotUtc, CancellationToken ct)
    {
        if (snapshotUtc.Kind != DateTimeKind.Utc || filter.Page < 1 || filter.PageSize is < 1 or > 100
            || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue
            || filter.RequestTypeId is <= 0 || filter.PriorityId is <= 0 || filter.ReceivingDepartmentId is <= 0
            || filter.Status?.Length > 40 || filter.Search?.Length > 200)
            throw new ValidationException("Invalid history filters or UTC snapshot.");
        var query = authorizedRequests.AsNoTracking();
        if (filter.RequestTypeId is {} type) query = query.Where(r => r.RequestTypeId == type);
        if (filter.PriorityId is {} priority) query = query.Where(r => r.PriorityId == priority);
        if (filter.ReceivingDepartmentId is {} department) query = query.Where(r => r.ReceivingDepartmentId == department);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(r => r.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(r => r.RequestNo.Contains(term) || r.Subject.Contains(term));
        }
        // GBR-156: strictly after deadline; Closed/Rejected/Cancelled are excluded. No deadline means not overdue.
        var rows = query.Select(r => new { Id=r.InternalRequestId, Number=r.RequestNo, r.BranchId,
            r.Subject, r.Status, TypeId=r.RequestTypeId, r.PriorityId, r.ReceivingDepartmentId, Deadline=r.DueAt,
            Overdue=r.DueAt != null && r.DueAt < snapshotUtc && r.Status != "CLOSED" && r.Status != "REJECTED" && r.Status != "CANCELLED" });
        if (filter.Overdue is {} overdue) rows = rows.Where(r => r.Overdue == overdue);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(r => r.Deadline == null).ThenBy(r => r.Deadline).ThenBy(r => r.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(r => new InternalRequestHistoryRow(r.Id,r.Number,r.BranchId,r.Subject,r.Status,r.TypeId,r.PriorityId,r.ReceivingDepartmentId,r.Deadline,r.Overdue)).ToArrayAsync(ct);
        return new(items, count, filter.Page, filter.PageSize, snapshotUtc);
    }

    public static async Task<InternalRequestTimeline?> TimelineAsync(IQueryable<InternalRequest> authorizedRequests,
        long requestId, CancellationToken ct)
    {
        if (requestId <= 0) throw new ValidationException("Invalid request identifier.");
        var request = authorizedRequests.AsNoTracking().Where(r => r.InternalRequestId == requestId);
        if (!await request.AnyAsync(ct)) return null;
        var events = await request.SelectMany(r => r.InternalRequestEvents)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.EventId)
            .Select(e => new InternalRequestTimelineEvent(e.EventId, e.EventType, e.OldStatus, e.NewStatus,
                e.EventNote, e.ActorId, e.OccurredAt)).ToArrayAsync(ct);
        return new(requestId, events);
    }
}
