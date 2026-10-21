using HotelOps.Api.DTOs.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed partial class MaterialRequestService
{
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    public async Task<MaterialPageDto> List(MaterialPage p, CancellationToken ct)
    {
        if (p.Page < 1 || p.PageSize < 1 || p.PageSize > 100 || (long)(p.Page - 1) * p.PageSize > int.MaxValue || p.From > p.To) throw Invalid("Invalid page/date range.");
        var q = db.MaterialRequests.AsNoTracking();
        if (p.BranchId is long b) q = q.Where(x => x.BranchId == b);
        if (p.DepartmentId is long d) q = q.Where(x => x.DepartmentId == d);
        if (p.RequesterId is long u) q = q.Where(x => x.RequesterId == u);
        if (p.IncidentId is long i) q = q.Where(x => x.IncidentId == i);
        if (p.ItemId is long item) q = q.Where(x => x.MaterialRequestLines.Any(l => l.IsActive && l.ItemId == item));
        if (!string.IsNullOrWhiteSpace(p.Status)) q = q.Where(x => x.Status == p.Status);
        if (!string.IsNullOrWhiteSpace(p.Keyword)) q = q.Where(x => x.RequestNo.Contains(p.Keyword) || x.Purpose.Contains(p.Keyword));
        if (p.From is DateTime from) q = q.Where(x => x.CreatedAt >= from);
        if (p.To is DateTime to) q = q.Where(x => x.CreatedAt <= to);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.RequestId).Skip((p.Page - 1) * p.PageSize).Take(p.PageSize)
            .Select(x => new MaterialSummaryDto(x.RequestId, x.RequestNo, x.Status, x.BranchId, x.DepartmentId, x.RequesterId, x.CreatedAt, x.NeededAt)).ToListAsync(ct);
        return new(p.Page, p.PageSize, total, rows.Select(x => x with { CreatedAt = Utc(x.CreatedAt), NeededAt = Utc(x.NeededAt) }).ToArray());
    }
    public async Task<MaterialRequestDto?> Get(long id, CancellationToken ct)
    {
        var r = await db.MaterialRequests.AsNoTracking().Include(x => x.MaterialRequestLines).SingleOrDefaultAsync(x => x.RequestId == id, ct);
        if (r is null) return null;
        var cycles = await db.MaterialRequestApprovalCycles.AsNoTracking().Include(x => x.MaterialRequestApprovalLineSnapshots).Where(x => x.RequestId == id).OrderBy(x => x.RevisionNo).ToListAsync(ct);
        var decisions = await db.MaterialRequestApprovals.AsNoTracking().Where(x => x.RequestId == id).OrderBy(x => x.DecidedAt).ThenBy(x => x.ApprovalId)
            .Select(x => new MaterialDecisionDto(x.ApprovalId, x.ApprovalCycleId, x.ApproverId, x.Decision, x.DecisionNote, x.DecidedAt)).ToListAsync(ct);
        var batches = await Fulfillments(id, ct) ?? [];
        // Header FK also safely identifies legacy ISSUE returns without inventing
        // a request-line mapping from ItemId or line order.
        var issueDocuments = batches.Where(x => x.StockDocumentId != null).Select(x => x.StockDocumentId!.Value).ToArray();
        var returns = await db.StockReturnLineDetails.AsNoTracking().Where(x => issueDocuments.Contains(x.OriginalIssueLine.DocumentId) && x.OriginalIssueLine.Document.DocumentType == "ISSUE")
            .Select(x => new MaterialReturnDto(x.StockDocumentLine.DocumentId, x.StockDocumentLine.Document.DocumentNo, x.StockDocumentLine.Document.Status, x.OriginalIssueLineId)).Distinct().ToListAsync(ct);
        var isNew = await NewWorkflow(r, ct); var eligible = isNew && r.Status is "DRAFT" or "PENDING_APPROVAL" or "NEEDS_INFORMATION" or "APPROVED" or "PARTIALLY_ISSUED";
        return new(r.RequestId, r.RequestNo, r.Status, isNew, eligible, eligible ? null : isNew ? "Terminal workflow state." : "Legacy workflow: mutation reconciliation unavailable.", Convert.ToBase64String(r.RowVersion),
            r.BranchId, r.DepartmentId, r.RequesterId, r.SourceWarehouseId, r.Purpose, Utc(r.NeededAt), r.PriorityId, r.ReceivingRoomId, r.ReceivingAreaId, r.IncidentId,
            Utc(r.CreatedAt), Utc(r.SubmittedAt), Utc(r.CompletedAt), Utc(r.UpdatedAt), r.CancellationReason, Utc(r.RemainderClosedAt), r.RemainderClosedBy, r.RemainderCloseReason,
            r.MaterialRequestLines.OrderBy(x => x.LineNo).Select(x => new MaterialLineDto(x.LineId, x.LineNo, x.ItemId, x.UomId, x.RequestedQty, x.ApprovedQty, x.FulfilledQty, x.ApprovedQty - x.FulfilledQty,
                x.IsActive && r.Status is "APPROVED" or "PARTIALLY_ISSUED" ? (x.ApprovedQty ?? 0) - x.FulfilledQty : 0, x.IsActive, x.Note)).ToArray(),
            cycles.Select(x => new MaterialCycleDto(x.ApprovalCycleId, x.RevisionNo, x.SubmittedBy, x.AssignedApproverId, x.Status, Utc(x.SubmittedAt), Utc(x.ResolvedAt),
                x.MaterialRequestApprovalLineSnapshots.OrderBy(l => l.LineNo).Select(l => new MaterialCycleLineDto(l.RequestLineId, l.LineNo, l.ItemId, l.UomId, l.RequestedQty, l.ApprovedQty, l.Note)).ToArray())).ToArray(),
            decisions.Select(x => x with { DecidedAt = Utc(x.DecidedAt) }).ToArray(), batches, returns);
    }
    public async Task<IReadOnlyList<MaterialFulfillmentDto>?> Fulfillments(long id, CancellationToken ct)
    {
        if (!await db.MaterialRequests.AnyAsync(x => x.RequestId == id, ct)) return null;
        var rows = await db.MaterialFulfillments.AsNoTracking().Include(x => x.StockDocument).Include(x => x.MaterialFulfillmentLines).Where(x => x.RequestId == id).OrderBy(x => x.FulfillmentId).ToListAsync(ct);
        return rows.Select(x => new MaterialFulfillmentDto(x.FulfillmentId, x.FulfillmentNo, id, x.WarehouseId, x.FulfilledBy, x.ReceivedBy, x.Status, Utc(x.FulfilledAt), Utc(x.ReceivedAt), x.StockDocumentId, x.StockDocument?.DocumentNo,
            x.MaterialFulfillmentLines.OrderBy(l => l.FulfillmentLineId).Select(l => new MaterialFulfillmentLineDto(l.FulfillmentLineId, l.RequestLineId, l.ItemId, l.UomId, l.IssuedQty, l.ReceivedQty, l.StockDocumentLineId)).ToArray())).ToArray();
    }
    public async Task<MaterialFulfillmentDto?> Fulfillment(long id, long fulfillmentId, CancellationToken ct) => (await Fulfillments(id, ct))?.SingleOrDefault(x => x.FulfillmentId == fulfillmentId);
    public async Task<MaterialHistoryDto?> History(long id, CancellationToken ct)
    {
        var r = await Get(id, ct); if (r is null) return null;
        var batches = r.Fulfillments.Select(x => x.FulfillmentId.ToString()).ToArray(); var docs = r.Fulfillments.Where(x => x.StockDocumentId != null).Select(x => x.StockDocumentId!.Value.ToString()).ToArray();
        var audits = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "material_requests" && x.EntityId == id.ToString() || x.EntityType == "material_fulfillments" && batches.Contains(x.EntityId) || x.EntityType == "stock_documents" && docs.Contains(x.EntityId))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.AuditId).Select(x => new MaterialAuditDto(x.AuditId, x.EntityType, x.EntityId, x.Action, x.ActorUserId, x.OccurredAt, x.OldValues, x.NewValues, x.ChangedFields)).ToListAsync(ct);
        return new(r, audits.Select(x => x with { OccurredAt = Utc(x.OccurredAt) }).ToArray());
    }
    public async Task<MaterialAvailabilityDto?> Availability(long id, CancellationToken ct)
    {
        var r = await Get(id, ct); if (r is null) return null;
        if (r.SourceWarehouseId is not long w) throw Invalid("Source warehouse is required for availability.");
        var stocks = new List<object>();
        foreach (var item in r.Lines.Where(x => x.IsActive).Select(x => x.ItemId).Distinct()) { var s = await inventory.GetStock(w, item, ct); if (s != null) stocks.Add(s); }
        return new(id, w, "Preview only. Shared stock is reported once per item in base UOM; line quantities are not independent stock promises. No reservation.", stocks,
            r.Lines.Where(x => x.IsActive).Select(x => new MaterialAvailabilityLineDto(x.RequestLineId, x.ItemId, x.UomId, x.RequestedQty, x.ApprovedQty, x.FulfilledQty, x.HistoricalRemainingQty)).ToArray());
    }
}
