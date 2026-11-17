using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class ReportTransferLosses
{
    internal sealed record Row(long ReceiptLineId, long ReceiptId, long DocumentId, long ItemId, long UomId,
        long SourceWarehouseId, long DestinationWarehouseId, long SourceBranchId, long DestinationBranchId,
        DateTime ReceivedAt, decimal ReceivedQty, decimal DamagedQty, decimal MissingQty);
    internal sealed record Result(Row[] Rows, bool MissingEvidence);

    internal static async Task<Result> ReadAsync(HotelOpsDbContext db, ReportQuery query, InsightFilters filters,
        long[] allowed, CancellationToken ct)
    {
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        // Current warehouse scope only selects candidates. Event-time scope is
        // independently proven below; current ownership is never historical proof.
        var candidates = await db.StockTransferReceiptLines.AsNoTracking()
            .Where(l => l.TransferReceipt.ReceivedAt >= start && l.TransferReceipt.ReceivedAt < end
                && l.TransferLine.DocumentId == l.TransferReceipt.TransferDocumentId
                && l.TransferLine.Document.DocumentType == "TRANSFER"
                && l.TransferLine.Document.SourceWarehouseId != null && l.TransferLine.Document.DestinationWarehouseId != null
                && (filters.BranchIds.Contains(l.TransferLine.Document.SourceWarehouse!.BranchId)
                    || filters.BranchIds.Contains(l.TransferLine.Document.DestinationWarehouse!.BranchId))
                && (query.ItemId == null || l.TransferLine.ItemId == query.ItemId)
                && (query.WarehouseId == null || l.TransferLine.Document.SourceWarehouseId == query.WarehouseId
                    || l.TransferLine.Document.DestinationWarehouseId == query.WarehouseId)
                && (filters.DepartmentId == null || l.TransferLine.Document.SourceWarehouse!.DepartmentId == filters.DepartmentId
                    || l.TransferLine.Document.DestinationWarehouse!.DepartmentId == filters.DepartmentId))
            .OrderBy(l => l.TransferReceiptLineId).Select(l => new
            {
                l.TransferReceiptLineId, l.TransferReceiptId, l.TransferLineId, l.ReceivedQty, l.DamagedQty, l.MissingQty,
                l.TransferLine.ItemId, l.TransferLine.UomId, l.TransferLine.DocumentId,
                l.TransferLine.Document.SourceWarehouseId, l.TransferLine.Document.DestinationWarehouseId,
                l.TransferReceipt.ReceivedAt, l.TransferReceipt.ReceivedBy
            }).Take(50001).ToArrayAsync(ct);
        if (candidates.Length > 50000) throw Limit();
        var documentIds = candidates.Select(l => l.DocumentId.ToString(CultureInfo.InvariantCulture)).Distinct().ToArray();
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "stock_documents"
            && documentIds.Contains(a.EntityId) && a.BranchId != null && allowed.Contains(a.BranchId.Value)
            && a.OccurredAt >= start && a.OccurredAt < end && a.Action == "UPDATE")
            .OrderBy(a => a.AuditId).Take(100001).ToArrayAsync(ct);
        if (audits.Length > 100000) throw Limit();
        var rows = new List<Row>();
        var missing = false;
        foreach (var l in candidates)
        {
            var matches = new HashSet<(long Source, long Destination)>();
            foreach (var audit in audits.Where(a => a.EntityId == l.DocumentId.ToString(CultureInfo.InvariantCulture)
                && a.OccurredAt == l.ReceivedAt && a.ActorUserId == l.ReceivedBy))
            {
                try
                {
                    using var json = JsonDocument.Parse(audit.NewValues ?? "null");
                    var p = json.RootElement;
                    if (p.ValueKind != JsonValueKind.Object || Text(p, "businessEvent") != "BATCH_RECEIVED") continue;
                    var source = Number(p, "sourceBranchId");
                    var destination = Number(p, "destinationBranchId");
                    if (source == null && destination == null)
                    {
                        // Legacy writer emitted the same event payload twice with
                        // explicit SOURCE/DESTINATION scope roles. Those immutable
                        // audit branches are evidence; current warehouse branches are not.
                        var legacy = LegacyBranches(p, audit, audits);
                        source = legacy.Source;
                        destination = legacy.Destination;
                    }
                    if (source is not > 0 || destination is not > 0 || !allowed.Contains(source.Value) || !allowed.Contains(destination.Value)
                        || !filters.BranchIds.Contains(source.Value) && !filters.BranchIds.Contains(destination.Value)
                        || audit.BranchId != source && audit.BranchId != destination) continue;
                    if (!p.TryGetProperty("document", out var doc) || doc.ValueKind != JsonValueKind.Object
                        || Number(doc, "document_id") != l.DocumentId || Text(doc, "document_type") != "TRANSFER"
                        || Number(doc, "source_warehouse_id") != l.SourceWarehouseId
                        || Number(doc, "destination_warehouse_id") != l.DestinationWarehouseId) continue;
                    if (!Array(p, "lines").Any(line => Number(line, "line_id") == l.TransferLineId
                        && Number(line, "document_id") == l.DocumentId && Number(line, "item_id") == l.ItemId
                        && Number(line, "uom_id") == l.UomId)) continue;
                    var valid = Array(p, "receivingBatches").Count(batch =>
                    {
                        if (!batch.TryGetProperty("header", out var header) || header.ValueKind != JsonValueKind.Object
                            || Number(header, "transfer_receipt_id") != l.TransferReceiptId
                            || Number(header, "transfer_document_id") != l.DocumentId
                            || Number(header, "received_by") != l.ReceivedBy || Timestamp(header, "received_at") != l.ReceivedAt) return false;
                        return Array(batch, "lines").Count(line => Number(line, "transfer_receipt_line_id") == l.TransferReceiptLineId
                            && Number(line, "transfer_receipt_id") == l.TransferReceiptId && Number(line, "transfer_line_id") == l.TransferLineId
                            && Decimal(line, "received_qty") == l.ReceivedQty && Decimal(line, "damaged_qty") == l.DamagedQty
                            && Decimal(line, "missing_qty") == l.MissingQty) == 1;
                    }) == 1;
                    if (valid) matches.Add((source.Value, destination.Value));
                }
                catch (JsonException) { }
            }
            if (matches.Count != 1 || l.ReceivedQty < 0 || l.DamagedQty < 0 || l.MissingQty < 0)
            { missing = true; continue; }
            var scope = matches.Single();
            rows.Add(new(l.TransferReceiptLineId, l.TransferReceiptId, l.DocumentId, l.ItemId, l.UomId,
                l.SourceWarehouseId!.Value, l.DestinationWarehouseId!.Value, scope.Source, scope.Destination,
                l.ReceivedAt, l.ReceivedQty, l.DamagedQty, l.MissingQty));
        }
        return new(rows.ToArray(), missing);
    }
    private static (long? Source, long? Destination) LegacyBranches(JsonElement payload, AuditLog audit, AuditLog[] audits)
    {
        var role = Text(payload, "scopeRole");
        if (role == "WAREHOUSE") return (audit.BranchId, audit.BranchId);
        if (role is not ("SOURCE" or "DESTINATION")) return (null, null);
        var matches = new List<(string? Role, long? Branch)>();
        foreach (var peer in audits.Where(a => a.EntityId == audit.EntityId && a.OccurredAt == audit.OccurredAt
            && a.ActorUserId == audit.ActorUserId))
        {
            try
            {
                using var json = JsonDocument.Parse(peer.NewValues ?? "null");
                var p = json.RootElement;
                if (p.ValueKind != JsonValueKind.Object || Text(p, "businessEvent") != "BATCH_RECEIVED") continue;
                if (new[] { "document", "lines", "receivingBatches" }.All(key =>
                    payload.TryGetProperty(key, out var left) && p.TryGetProperty(key, out var right)
                    && left.GetRawText() == right.GetRawText()))
                    matches.Add((Text(p, "scopeRole"), peer.BranchId));
            }
            catch (JsonException) { }
        }
        return matches.Count(m => m.Role == "SOURCE") == 1 && matches.Count(m => m.Role == "DESTINATION") == 1
            ? (matches.Single(m => m.Role == "SOURCE").Branch, matches.Single(m => m.Role == "DESTINATION").Branch)
            : (null, null);
    }
    private static IEnumerable<JsonElement> Array(JsonElement p, string key) => p.ValueKind == JsonValueKind.Object
        && p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array
        ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object) : [];
    private static long? Number(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
    private static decimal? Decimal(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n) ? n : null;
    private static string? Text(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static DateTime? Timestamp(JsonElement p, string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var d) ? d : null;
    private static InsightException Limit() => new(422, "REPORT_ROW_LIMIT", "Transfer evidence exceeds the report limit; narrow the period.");
}
