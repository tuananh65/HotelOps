using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal sealed partial class MaterialApprovalNotificationDelivery
{
    internal const string ReplenishmentReceiptKind = "MATERIAL_REPLENISHMENT_RECEIVED";
    internal const string ReplenishmentReceiptSource = "MATERIAL_REPLENISHMENT_RECEIPT";

    private static async Task<bool> ReplenishmentReceiptsInstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.material_replenishment_receipt_lines','U') IS NOT NULL
              AND COL_LENGTH('dbo.material_replenishment_lines','source_material_request_line_id') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    // Committed credit is durable intent. One canonical credit per receipt/request
    // prevents multiple item lines from generating duplicate notices for that request.
    private static IQueryable<NoticeEvent> ReplenishmentReceiptEvents(HotelOpsDbContext db) =>
        db.MaterialReplenishmentReceiptLines.AsNoTracking().Where(c =>
            c.ReplenishmentLine.SourceMaterialRequestLine != null
            && c.ReplenishmentLine.SourceType == "MATERIAL_REQUEST_SHORTAGE"
            && c.ReplenishmentLine.SourceMaterialRequestLine.ItemId == c.ReplenishmentLine.ItemId
            && c.ReplenishmentLine.WarehouseId == c.ReplenishmentLine.MaterialReplenishment.WarehouseId
            && c.ReplenishmentLine.SourceMaterialRequestLine.Request.SourceWarehouseId == c.ReplenishmentLine.WarehouseId
            && c.ReplenishmentLine.SourceMaterialRequestLine.Request.BranchId == c.ReplenishmentLine.MaterialReplenishment.Warehouse.BranchId
            && c.StockDocumentLine.Document.DocumentType == "RECEIPT" && c.StockDocumentLine.Document.Status == "POSTED"
            && c.StockDocumentLine.Document.SourceWarehouseId == null && c.StockDocumentLine.Document.ReversalOfDocumentId == null
            && c.StockDocumentLine.Document.DestinationWarehouseId == c.ReplenishmentLine.WarehouseId
            && c.StockDocumentLine.Document.PostedAt != null && c.StockDocumentLine.Document.PostedAt <= c.LinkedAt
            && c.StockDocumentLine.Document.PostedBy != null && c.StockDocumentLine.ItemId == c.ReplenishmentLine.ItemId
            && c.CreditedBaseQty > 0 && c.CreditedBaseQty == c.StockDocumentLine.BaseQty
            && c.StockDocumentLine.ConversionFactor > 0 && c.StockDocumentLine.ActualQty > 0
            && c.StockDocumentLine.ActualQty * c.StockDocumentLine.ConversionFactor == c.CreditedBaseQty
            && c.StockDocumentLine.StockMovements.Count() == 1
            && c.StockDocumentLine.StockMovements.Any(m => m.WarehouseId == c.ReplenishmentLine.WarehouseId
                && m.ItemId == c.ReplenishmentLine.ItemId && m.SignedBaseQty == c.CreditedBaseQty)
            && !db.StockDocuments.Any(d => d.ReversalOfDocumentId == c.StockDocumentLine.DocumentId && d.Status == "POSTED")
            && !db.MaterialReplenishmentReceiptLines.Any(prior => prior.ReplenishmentReceiptLineId < c.ReplenishmentReceiptLineId
                && prior.StockDocumentLine.DocumentId == c.StockDocumentLine.DocumentId
                && prior.ReplenishmentLine.SourceMaterialRequestLine != null
                && prior.ReplenishmentLine.SourceMaterialRequestLine.RequestId == c.ReplenishmentLine.SourceMaterialRequestLine.RequestId))
        .Select(c => new NoticeEvent { Id = c.ReplenishmentReceiptLineId,
            RequestId = c.ReplenishmentLine.SourceMaterialRequestLine!.RequestId,
            RequesterId = c.ReplenishmentLine.SourceMaterialRequestLine.Request.RequesterId,
            BranchId = c.ReplenishmentLine.SourceMaterialRequestLine.Request.BranchId,
            ActorId = c.LinkedBy, At = c.LinkedAt });

    internal static async Task<long?> ReplenishmentReceiptTargetAsync(HotelOpsDbContext db, long id, long user,
        long? actor, DateTime at, CancellationToken ct)
    {
        if (!await ReplenishmentReceiptsInstalledAsync(db, ct)) return null;
        var target = await DecisionTargetAsync(db, id, user, ct, NoticeType.ReplenishmentReceipt);
        return target != null && await ReplenishmentReceiptEvents(db).AnyAsync(e => e.Id == id
            && e.ActorId == actor && e.At == at, ct) ? target : null;
    }
}
