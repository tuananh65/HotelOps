using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

internal static class ReceiptPostingProof
{
    // Idempotent replay is a read of a proven completed posting, never another stock effect.
    internal static async Task RequireAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Receipt proof requires the caller transaction.");
        var d = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == id, ct);
        if (d == null || d.DocumentType != "RECEIPT" || d.Status != "POSTED" || d.SourceWarehouseId != null
            || d.DestinationWarehouseId == null || d.PostedBy == null || d.PostedAt == null || d.CancelledAt != null || d.ReversalOfDocumentId != null)
            throw Invalid();
        var lines = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == id).ToArrayAsync(ct);
        if (lines.Length == 0) throw Invalid();
        var ids = lines.Select(l => l.LineId).ToArray();
        var details = await db.StockReceiptLineDetails.AsNoTracking().Where(r => ids.Contains(r.StockDocumentLineId)).ToArrayAsync(ct);
        var movements = await db.StockMovements.AsNoTracking().Where(m => ids.Contains(m.DocumentLineId)).ToArrayAsync(ct);
        foreach (var l in lines)
        {
            var receipt = details.SingleOrDefault(r => r.StockDocumentLineId == l.LineId);
            if (receipt == null || l.RequestedQty is not > 0 || l.ActualQty < 0 || l.ConversionFactor <= 0
                || receipt.RejectedQty < 0 || receipt.DamagedQty < 0) throw Invalid();
            decimal quantity;
            try { quantity = checked(l.ActualQty * l.ConversionFactor); } catch (OverflowException) { throw Invalid(); }
            if (quantity != l.BaseQty || decimal.Round(quantity, 3) != quantity) throw Invalid();
            var effects = movements.Where(m => m.DocumentLineId == l.LineId).ToArray();
            if (quantity == 0 ? effects.Length != 0 : effects.Length != 1
                || effects[0].WarehouseId != d.DestinationWarehouseId || effects[0].ItemId != l.ItemId
                || effects[0].SignedBaseQty != quantity || effects[0].CreatedBy != d.PostedBy || effects[0].OccurredAt != d.PostedAt) throw Invalid();
        }
        var branch = await db.Warehouses.Where(w => w.WarehouseId == d.DestinationWarehouseId).Select(w => w.BranchId).SingleAsync(ct);
        var key = id.ToString(CultureInfo.InvariantCulture);
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "stock_documents" && a.EntityId == key
            && a.Action == "UPDATE" && a.BranchId == branch && a.ActorUserId == d.PostedBy && a.OccurredAt == d.PostedAt)
            .Select(a => a.NewValues).ToArrayAsync(ct);
        var proven = 0;
        foreach (var audit in audits)
        {
            try
            {
                using var json = JsonDocument.Parse(audit ?? "null");
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("businessEvent", out var type) && type.GetString() == "POSTED"
                    && json.RootElement.TryGetProperty("document", out var row) && row.TryGetProperty("document_id", out var identity)
                    && identity.TryGetInt64(out var number) && number == id) proven++;
            }
            catch (JsonException) { }
        }
        if (proven != 1) throw Invalid();
    }

    private static ValidationException Invalid() => new("MSG03: Receipt posting evidence is incomplete or inconsistent; no additional stock movement was created.");
}
