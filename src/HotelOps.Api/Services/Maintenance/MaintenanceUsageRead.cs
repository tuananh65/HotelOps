using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

// Caller owns the serializable transaction and maintenance/session authorization.
internal static class MaintenanceUsageRead
{
    internal sealed record Result(bool Available, MaintenanceMaterialUsageDto[] Items);
    internal static async Task<Result> ReadAsync(HotelOpsDbContext db, long user, long branch, long work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Usage read transaction required.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await new EfPermissionDataSource(db).ReadPathsAsync(user, "INVENTORY_VIEW", now, ct);
        if (!paths.Any(p => p.PermissionCode == "INVENTORY_VIEW" && PermissionGrantRules.IsEligible(p, now)
            && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) == "BRANCH"))) return new(false, []);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, branch, now, ct);
        if (!scope.Branch && !scope.Own) return new(false, []);
        var requests = scope.Apply(db.MaterialRequests.AsNoTracking()).Select(r => r.RequestId);
        var usages = db.MaintenanceMaterialUsages.AsNoTracking().Where(u => u.MaintenanceWorkId == work);
        var valid = VisibleSources(usages, requests, branch, work);
        // Never present an incomplete visible subset as the total material usage.
        if (await valid.CountAsync(ct) != await usages.CountAsync(ct)) return new(false, []);
        var requestIds = await valid.Select(u => u.StockDocumentLine.MaterialFulfillmentLine!.Fulfillment.RequestId).Distinct().ToArrayAsync(ct);
        foreach (var request in requestIds)
            if (!await MaterialReadProvenance.ValidAsync(db, request, branch, ct)) return new(false, []);
        var rows = await valid.OrderBy(u => u.UsageId).Select(u => new MaintenanceMaterialUsageDto(
            u.UsageId, u.MaintenanceWorkId, u.MaintenanceWorkLogId, u.StockDocumentLineId,
            u.StockDocumentLine.DocumentId, u.StockDocumentLine.Document.DocumentNo, u.MaintenanceWork.IncidentId!.Value,
            u.StockDocumentLine.MaterialFulfillmentLine!.Fulfillment.RequestId,
            u.StockDocumentLine.MaterialFulfillmentLine.RequestLineId, u.StockDocumentLine.MaterialFulfillmentLine.FulfillmentId,
            u.StockDocumentLine.MaterialFulfillmentLine.FulfillmentLineId, u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.WarehouseId,
            u.ItemId, u.BaseUomId, u.UomId, u.UsedQty, u.ConversionFactor, u.UsedBaseQty)).ToArrayAsync(ct);
        return new(true, rows);
    }
    internal static IQueryable<HotelOps.Api.Entities.MaintenanceMaterialUsage> VisibleSources(
        IQueryable<HotelOps.Api.Entities.MaintenanceMaterialUsage> usages, IQueryable<long> requests, long branch, long work) =>
        usages.Where(u => u.MaintenanceWorkLog.MaintenanceWorkId == work
            && u.MaintenanceWork.IncidentId != null
            && u.StockDocumentLine.MaterialFulfillmentLine != null
            && u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.Request.IncidentId == u.MaintenanceWork.IncidentId
            && requests.Contains(u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.RequestId)
            && u.StockDocumentLine.MaterialFulfillmentLine.RequestLine.RequestId == u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.RequestId
            && u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.Warehouse.BranchId == branch
            && u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.StockDocumentId == u.StockDocumentLine.DocumentId
            && u.StockDocumentLine.Document.SourceWarehouseId == u.StockDocumentLine.MaterialFulfillmentLine.Fulfillment.WarehouseId
            && u.StockDocumentLine.Document.DestinationWarehouseId == null
            && u.StockDocumentLine.Document.DocumentType == "ISSUE" && u.StockDocumentLine.Document.Status == "POSTED"
            && u.StockDocumentLine.Document.PostedAt != null
            && u.StockDocumentLine.Document.ReferenceType == "MATERIAL_FULFILLMENT"
            && u.StockDocumentLine.Document.ReferenceId == u.StockDocumentLine.MaterialFulfillmentLine.FulfillmentId
            && u.ItemId == u.StockDocumentLine.ItemId
            && u.ItemId == u.StockDocumentLine.MaterialFulfillmentLine.ItemId
            && u.ItemId == u.StockDocumentLine.MaterialFulfillmentLine.RequestLine.ItemId
            && u.BaseUomId == u.Item.BaseUomId && u.UsedQty > 0 && u.ConversionFactor > 0 && u.UsedBaseQty > 0);
}
