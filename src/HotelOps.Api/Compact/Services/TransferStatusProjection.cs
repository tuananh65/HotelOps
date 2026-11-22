using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

/// <summary>Current receipt progress without modifying immutable posted transfer headers.</summary>
public static class TransferStatusProjection
{
    public static async Task<IReadOnlyDictionary<long, string>> Read(
        HotelOpsCompactDbContext db, long[] documentIds, CancellationToken ct)
    {
        if (documentIds.Length == 0) return new Dictionary<long, string>();

        var lines = await db.InventoryDocumentLines.AsNoTracking()
            .Where(line => documentIds.Contains(line.DocumentId)
                && db.InventoryDocuments.Any(document => document.DocumentId == line.DocumentId
                    && document.Kind == "TRANSFER" && document.PostedAt != null))
            .Select(line => new
            {
                line.DocumentId,
                Dispatched = line.ActualQty * line.ConversionFactor,
                Received = (from receiptLine in db.InventoryDocumentLines
                            join receipt in db.InventoryDocuments on receiptLine.DocumentId equals receipt.DocumentId
                            join movement in db.StockMovements on receiptLine.LineId equals movement.LineId
                            where receiptLine.SourceLineId == line.LineId
                                && receipt.SourceDocumentId == line.DocumentId
                                && receipt.Kind == "TRANSFER_RECEIPT" && receipt.PostedAt != null
                                && !db.StockMovements.Any(reversal => reversal.ReversalOfMovementId == movement.MovementId)
                            select (decimal?)movement.SignedBaseQty).Sum() ?? 0
            }).ToArrayAsync(ct);

        // Evaluate every dispatched line: receiving one item cannot complete another item.
        return lines.GroupBy(line => line.DocumentId).ToDictionary(group => group.Key,
            group => group.All(line => line.Received >= line.Dispatched) ? "RECEIVED"
                : group.Any(line => line.Received > 0) ? "PARTIALLY_RECEIVED" : "IN_TRANSIT");
    }

    public static ResourceDto Record<T>(T row, long documentId, IReadOnlyDictionary<long, string> statuses)
        where T : class
    {
        var record = Output.Record(row);
        if (!statuses.TryGetValue(documentId, out var status)) return record;

        var fields = new Dictionary<string, object?>(record.Fields)
        {
            ["recordedStatus"] = record.Fields["status"],
            ["status"] = status
        };
        return new ResourceDto(record.Type, fields);
    }
}
