using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private sealed record ResolutionUsageProof(long UsageId, long WorkLogId, long IssueLineId,
        long ItemId, long UomId, long BaseUomId, decimal UsedQty, decimal Factor, decimal UsedBaseQty);
    private sealed record ResolutionIssueProof(long IssueLineId, long DocumentId, long RequestId,
        long FulfillmentId, decimal IssuedBaseQty, decimal ConsumedBaseQty, decimal ReturnedBaseQty,
        IReadOnlyList<ResolutionUsageProof> Usages);

    // Submit takes the inventory catalog before user/time and owning-source locks,
    // matching material recording/returns. Never acquire it after locking the work.
    private async Task LockResolutionMaterialsAsync(long workId, CancellationToken ct)
    {
        await _issueCapacity.AcquireCatalog(ct);
        var lines = await _dbContext.MaintenanceMaterialUsages.AsNoTracking()
            .Where(u => u.MaintenanceWorkId == workId).Select(u => u.StockDocumentLineId)
            .Distinct().OrderBy(id => id).ToArrayAsync(ct);
        await _issueCapacity.LockSourceDocuments(lines, ct);
    }

    private async Task<IReadOnlyList<ResolutionIssueProof>> ValidateResolutionMaterialsAsync(MaintenanceWork work, Incident incident, CancellationToken ct)
    {
        // A legacy header-only issue reference cannot prove item/UOM/quantity usage.
        // Do not silently treat such a record as zero consumption.
        if (await _dbContext.MaintenanceWorkLogs.AsNoTracking().AnyAsync(l =>
                l.MaintenanceWorkId == work.MaintenanceWorkId && l.MaterialIssueId != null, ct))
            throw new ValidationException("Legacy material usage requires source-line reconciliation before submission.");

        var usages = await _dbContext.MaintenanceMaterialUsages.AsNoTracking()
            .Include(u => u.MaintenanceWorkLog)
            .Where(u => u.MaintenanceWorkId == work.MaintenanceWorkId)
            .OrderBy(u => u.StockDocumentLineId).ThenBy(u => u.UsageId).ToArrayAsync(ct);
        var proofs = new List<ResolutionIssueProof>();
        foreach (var group in usages.GroupBy(u => u.StockDocumentLineId))
        {
            var source = await ProveUsageSource(group.Key, incident, ct);
            foreach (var usage in group)
            {
                var factor = usage.UomId == source.BaseUomId ? 1m
                    : usage.UomId == source.Line.UomId ? source.Line.ConversionFactor
                    : throw new ValidationException("Recorded material UOM does not match its ISSUE source.");
                if (usage.MaintenanceWorkLog.MaintenanceWorkId != work.MaintenanceWorkId
                    || usage.ItemId != source.Line.ItemId || usage.BaseUomId != source.BaseUomId
                    || usage.ConversionFactor != factor
                    || usage.UsedBaseQty != _issueCapacity.HistoricalBase(usage.UsedQty, factor))
                    throw new ValidationException("Recorded material usage does not match its work log and historical ISSUE quantity.");
            }
            // Capacity aggregates all consumers and posted returns, not just this work.
            // Existing consumption is checked without adding it a second time.
            var capacity = await _issueCapacity.ValidateCapacity(group.Key, 0, 0, null, ct);
            if (capacity.IssuedBaseQty != source.Line.BaseQty)
                throw new ValidationException("Recorded material ISSUE movement cannot be reconciled.");
            proofs.Add(new(group.Key, source.Line.DocumentId, source.FulfillmentLine.Fulfillment.RequestId,
                source.FulfillmentLine.FulfillmentId, capacity.IssuedBaseQty, capacity.ConsumedBaseQty, capacity.ReturnedBaseQty,
                group.Select(u => new ResolutionUsageProof(u.UsageId, u.MaintenanceWorkLogId, u.StockDocumentLineId,
                    u.ItemId, u.UomId, u.BaseUomId, u.UsedQty, u.ConversionFactor, u.UsedBaseQty)).ToArray()));
        }
        return proofs;
    }
}
