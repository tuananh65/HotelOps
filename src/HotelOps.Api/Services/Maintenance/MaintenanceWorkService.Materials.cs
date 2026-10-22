using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private sealed record UsageSource(MaterialFulfillmentLine FulfillmentLine, StockDocumentLine Line, long BaseUomId);
    private sealed record PreparedUsage(MaintenanceMaterialUsage Row, UsageSource Source, InventoryIssueCapacity Before);

    private async Task PrepareMaterialLocks(IReadOnlyList<MaintenanceMaterialUsageWrite> input, CancellationToken ct)
    {
        if (input.Any(x => x is null || x.StockDocumentLineId <= 0 || x.UomId <= 0) || input.Select(x => x.StockDocumentLineId).Distinct().Count() != input.Count)
            throw new ValidationException("Distinct positive ISSUE-line/UOM identities are required.");
        await _issueCapacity.AcquireCatalog(ct);
        await _issueCapacity.LockSourceDocuments(input.Select(x => x.StockDocumentLineId).ToArray(), ct);
    }
    private async Task<UsageSource> ProveUsageSource(long lineId, Incident incident, CancellationToken ct)
    {
        var matches = await _dbContext.MaterialFulfillmentLines.AsNoTracking()
            .Include(x => x.Fulfillment).ThenInclude(x => x.Request)
            .Include(x => x.Fulfillment).ThenInclude(x => x.Warehouse)
            .Include(x => x.RequestLine).Include(x => x.Item)
            .Include(x => x.StockDocumentLine).ThenInclude(x => x!.Document)
            .Where(x => x.StockDocumentLineId == lineId).ToListAsync(ct);
        if (matches.Count != 1) throw new ValidationException("ISSUE requires exactly one durable FE-09 fulfillment-line correlation.");
        var f = matches[0]; var h = f.Fulfillment; var r = h.Request; var l = f.RequestLine;
        var s = f.StockDocumentLine!; var d = s.Document;
        if (d.DocumentType != "ISSUE" || d.Status != "POSTED" || d.PostedAt == null || d.ReversalOfDocumentId != null ||
            d.ReferenceType != "MATERIAL_FULFILLMENT" || d.ReferenceId != h.FulfillmentId ||
            h.StockDocumentId != d.DocumentId || h.Status != "ISSUED" || l.RequestId != r.RequestId ||
            r.IncidentId != incident.IncidentId || r.BranchId != incident.BranchId || h.Warehouse.BranchId != incident.BranchId ||
            h.WarehouseId != r.SourceWarehouseId || d.SourceWarehouseId != h.WarehouseId ||
            r.Status is not ("PARTIALLY_ISSUED" or "FULLY_ISSUED" or "REMAINDER_CLOSED") || !l.IsActive ||
            f.ItemId != l.ItemId || s.ItemId != f.ItemId || f.UomId != l.UomId || s.UomId != f.UomId ||
            f.IssuedQty <= 0 || s.ActualQty != f.IssuedQty || s.RequestedQty != s.ActualQty ||
            s.ConversionFactor <= 0 || s.BaseQty <= 0 || _issueCapacity.HistoricalBase(s.ActualQty, s.ConversionFactor) != s.BaseQty ||
            s.UomId == f.Item.BaseUomId && s.ConversionFactor != 1)
            throw new ValidationException("ISSUE/Incident/request/fulfillment identity or historical quantity is inconsistent.");
        if (await _dbContext.MaterialFulfillments.CountAsync(x => x.StockDocumentId == d.DocumentId, ct) != 1 ||
            await _dbContext.StockDocuments.AnyAsync(x => x.ReversalOfDocumentId == d.DocumentId, ct))
            throw new ValidationException("ISSUE ownership or reversal lineage is ambiguous.");
        var cycle = await _dbContext.MaterialRequestApprovalCycles.AsNoTracking().Where(x => x.RequestId == r.RequestId).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
        if (cycle is null || cycle.Status != "APPROVED" ||
            await _dbContext.MaterialRequestApprovals.CountAsync(x => x.RequestId == r.RequestId && x.ApprovalCycleId == cycle.ApprovalCycleId && x.Decision == "APPROVED" && x.ApproverId == cycle.AssignedApproverId, ct) != 1)
            throw new ValidationException("Safe approved FE-09 workflow evidence is required.");
        var snapshots = await _dbContext.MaterialRequestApprovalLineSnapshots.AsNoTracking().Where(x => x.ApprovalCycleId == cycle.ApprovalCycleId && x.RequestLineId == l.LineId).ToListAsync(ct);
        var issued = await _dbContext.MaterialFulfillmentLines.Where(x => x.RequestLineId == l.LineId).SumAsync(x => (decimal?)x.IssuedQty, ct) ?? 0;
        if (snapshots.Count != 1 || snapshots[0].ItemId != l.ItemId || snapshots[0].UomId != l.UomId ||
            snapshots[0].ApprovedQty != l.ApprovedQty || l.ApprovedQty is null or <= 0 || issued != l.FulfilledQty || issued > l.ApprovedQty)
            throw new ValidationException("Approved source-line quantities cannot be reconciled.");
        var movements = await _dbContext.StockMovements.AsNoTracking().Where(x => x.DocumentLineId == s.LineId).ToListAsync(ct);
        if (movements.Count != 1 || movements[0].SignedBaseQty != -s.BaseQty || movements[0].ItemId != s.ItemId || movements[0].WarehouseId != h.WarehouseId)
            throw new ValidationException("Original ISSUE negative movement does not prove issued base quantity.");
        return new(f, s, f.Item.BaseUomId);
    }
    private async Task<List<PreparedUsage>> PrepareUsages(MaintenanceWork work, Incident incident, IReadOnlyList<MaintenanceMaterialUsageWrite> input, CancellationToken ct)
    {
        if (await _dbContext.PreventiveMaintenanceRuns.AnyAsync(x => x.MaintenanceWorkId == work.MaintenanceWorkId, ct)) throw new ValidationException("Material Usage V1 requires an unambiguous corrective Work.");
        var result = new List<PreparedUsage>();
        foreach (var entry in input.OrderBy(x => x.StockDocumentLineId))
        {
            var source = await ProveUsageSource(entry.StockDocumentLineId, incident, ct);
            var factor = entry.UomId == source.BaseUomId ? 1 : entry.UomId == source.Line.UomId ? source.Line.ConversionFactor : throw new ValidationException("Use only source base UOM or original ISSUE UOM.");
            var quantity = _issueCapacity.HistoricalBase(entry.UsedQty, factor);
            var before = await _issueCapacity.ValidateCapacity(entry.StockDocumentLineId, quantity, 0, null, ct);
            if (before.IssuedBaseQty != source.Line.BaseQty) throw new ValidationException("ISSUE quantity/movement mismatch.");
            result.Add(new(new() { MaintenanceWorkId = work.MaintenanceWorkId, StockDocumentLineId = entry.StockDocumentLineId, ItemId = source.Line.ItemId,
                BaseUomId = source.BaseUomId, UomId = entry.UomId, UsedQty = entry.UsedQty, ConversionFactor = factor, UsedBaseQty = quantity }, source, before));
        }
        return result;
    }
    private async Task SaveUsages(MaintenanceWorkLog log, List<PreparedUsage> prepared, Incident incident, long actor, DateTime now, CancellationToken ct)
    {
        foreach (var item in prepared) { item.Row.MaintenanceWorkLogId = log.LogId; _dbContext.MaintenanceMaterialUsages.Add(item.Row); }
        await _dbContext.SaveChangesAsync(ct);
        foreach (var item in prepared)
        {
            var u = item.Row; var f = item.Source.FulfillmentLine; var s = item.Source.Line;
            AddAudit("maintenance_material_usages", u.UsageId, incident.BranchId, actor, now, null, new Dictionary<string, object?>
            {
                ["business_event"] = "MAINTENANCE_MATERIAL_USAGE_RECORDED", ["usage_id"] = u.UsageId,
                ["maintenance_work_id"] = u.MaintenanceWorkId, ["maintenance_work_log_id"] = log.LogId,
                ["stock_document_line_id"] = u.StockDocumentLineId, ["document_id"] = s.DocumentId,
                ["incident_id"] = incident.IncidentId, ["request_id"] = f.Fulfillment.RequestId,
                ["request_line_id"] = f.RequestLineId, ["fulfillment_id"] = f.FulfillmentId,
                ["fulfillment_line_id"] = f.FulfillmentLineId, ["warehouse_id"] = f.Fulfillment.WarehouseId,
                ["item_id"] = u.ItemId, ["base_uom_id"] = u.BaseUomId, ["uom_id"] = u.UomId,
                ["used_qty"] = u.UsedQty, ["conversion_factor"] = u.ConversionFactor, ["used_base_qty"] = u.UsedBaseQty,
                ["actor_user_id"] = actor, ["occurred_at"] = now, ["branch_id"] = incident.BranchId,
                ["capacity_before"] = item.Before,
                ["capacity_after"] = item.Before with { ConsumedBaseQty = item.Before.ConsumedBaseQty + u.UsedBaseQty }
            });
        }
    }
    private Task<List<MaintenanceMaterialUsageDto>> UsageDetails(long work, CancellationToken ct) => _dbContext.MaintenanceMaterialUsages.AsNoTracking()
        .Where(x => x.MaintenanceWorkId == work).OrderBy(x => x.UsageId)
        .Select(x => new MaintenanceMaterialUsageDto(x.UsageId, x.MaintenanceWorkId, x.MaintenanceWorkLogId, x.StockDocumentLineId,
            x.StockDocumentLine.DocumentId, x.StockDocumentLine.Document.DocumentNo, x.MaintenanceWork.IncidentId!.Value,
            x.StockDocumentLine.MaterialFulfillmentLine!.Fulfillment.RequestId, x.StockDocumentLine.MaterialFulfillmentLine.RequestLineId,
            x.StockDocumentLine.MaterialFulfillmentLine.FulfillmentId, x.StockDocumentLine.MaterialFulfillmentLine.FulfillmentLineId,
            x.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.WarehouseId, x.ItemId, x.BaseUomId, x.UomId, x.UsedQty, x.ConversionFactor, x.UsedBaseQty)).ToListAsync(ct);

    public async Task<MaintenanceMaterialAvailability?> MaterialAvailabilityAsync(long id, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new ValidationException("Invalid paging.");
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _issueCapacity.AcquireCatalog(cancellationToken);
        var discovered = await _dbContext.MaintenanceWorks.AsNoTracking().Where(x => x.MaintenanceWorkId == id).Select(x => new { x.IncidentId }).SingleOrDefaultAsync(cancellationToken);
        if (discovered is null) return null;
        if (discovered.IncidentId is not long incidentId) throw new ValidationException("Corrective Work required.");
        var incident = await LockIncidentAsync(incidentId, cancellationToken);
        var work = await _dbContext.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM maintenance_works WITH (UPDLOCK, HOLDLOCK) WHERE maintenance_work_id={id}").SingleAsync(cancellationToken);
        if (incident is null || work.IncidentId != incidentId) throw new ValidationException("Corrective linkage changed.");
        RequireSynchronizedPair(work, incident); RequireState(work, "IN_PROGRESS", "ON_HOLD");
        if (await _dbContext.PreventiveMaintenanceRuns.AnyAsync(x => x.MaintenanceWorkId == id, cancellationToken)) throw new ValidationException("Corrective Work required.");
        var ids = await _dbContext.MaterialFulfillmentLines.AsNoTracking().Where(x => x.Fulfillment.Request.IncidentId == incidentId && x.StockDocumentLineId != null)
            .OrderBy(x => x.StockDocumentLine!.Document.PostedAt).ThenBy(x => x.StockDocumentLine!.DocumentId).ThenBy(x => x.StockDocumentLine!.LineNo).ThenBy(x => x.StockDocumentLineId)
            .Select(x => x.StockDocumentLineId!.Value).Distinct().ToListAsync(cancellationToken);
        var result = new List<MaintenanceMaterialAvailabilityLine>();
        foreach (var line in ids)
        {
            try
            {
                var source = await ProveUsageSource(line, incident, cancellationToken); var cap = await _issueCapacity.Capacity(line, null, cancellationToken);
                if (cap.IssuedBaseQty != source.Line.BaseQty || cap.RemainingBaseQty <= 0) continue;
                var f = source.FulfillmentLine; var s = source.Line;
                var options = new List<MaintenanceUsageUom> { new(source.BaseUomId, 1) }; if (s.UomId != source.BaseUomId) options.Add(new(s.UomId, s.ConversionFactor));
                result.Add(new(line, s.LineNo, s.DocumentId, s.Document.DocumentNo, DateTime.SpecifyKind(s.Document.PostedAt!.Value, DateTimeKind.Utc), incidentId,
                    f.Fulfillment.RequestId, f.RequestLineId, f.FulfillmentId, f.FulfillmentLineId, f.Fulfillment.WarehouseId, s.ItemId, source.BaseUomId, options,
                    cap.IssuedBaseQty, cap.ConsumedBaseQty, cap.ReturnedBaseQty, cap.RemainingBaseQty));
            }
            catch (ValidationException) { /* Unsafe candidates are excluded, never repaired. Infrastructure/cancellation propagate. */ }
        }
        var ordered = result.OrderBy(x => x.PostedAt).ThenBy(x => x.DocumentId).ThenBy(x => x.LineNo).ThenBy(x => x.StockDocumentLineId).ToArray();
        await tx.CommitAsync(cancellationToken);
        return new(page, pageSize, ordered.Length, ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
    }
}
