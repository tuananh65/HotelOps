using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

// Runs only inside the exact-identity RecoveryAuthProbe synthetic branch.
internal static class RecoveryReplenishmentPost
{
    internal static async Task<string> RunAsync(HotelOpsDbContext db, HttpClient http, long root, long line,
        string version, long warehouse, long item, long unit, long actor, long role, string csrf,
        List<object> checks, CancellationToken ct)
    {
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.stock_receipt_line_details','U') IS NOT NULL
            AND NOT EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=0 AND parent_id IN
              (OBJECT_ID('dbo.stock_documents'),OBJECT_ID('dbo.stock_document_lines'),
               OBJECT_ID('dbo.stock_movements'),OBJECT_ID('dbo.stock_receipt_line_details')))
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new InvalidOperationException("Receipt schema/cleanup preflight required.");
        var document = new StockDocument { DocumentNo = "D3POST" + Guid.NewGuid().ToString("N"),
            DocumentType = "RECEIPT", Status = "DRAFT", DestinationWarehouseId = warehouse,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedBy = actor, CreatedAt = DateTime.UtcNow };
        var receiptLine = new StockDocumentLine { Document = document, LineNo = 1, ItemId = item, UomId = unit,
            ConversionFactor = 1, RequestedQty = 2, ActualQty = 0, BaseQty = 0 };
        db.StockDocumentLines.Add(receiptLine); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        async Task<JsonElement> Post(decimal quantity, int expected)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/replenishments/{root}/receipts/post");
            request.Headers.Add("X-CSRF-TOKEN", csrf);
            request.Content = JsonContent.Create(new { actorUserId = actor, expectedRowVersion = version,
                receiptDocumentId = document.DocumentId, receiptExpectedRowVersion = Convert.ToBase64String(document.RowVersion),
                receiptLines = new[] { new { lineId = receiptLine.LineId, acceptedQty = quantity, rejectedQty = 0,
                    damagedQty = 0, discrepancyReason = quantity == 2 ? (string?)null : "Synthetic excess delivery" } },
                lines = new[] { new { stockDocumentLineId = receiptLine.LineId, replenishmentLineId = line } } });
            using var response = await http.SendAsync(request, ct);
            checks.Add(new { receiptPost = quantity, expected, actual = (int)response.StatusCode });
            if ((int)response.StatusCode != expected) throw new InvalidOperationException($"Receipt Post returned {response.StatusCode}; expected {expected}.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return json.RootElement.Clone();
        }
        await Post(2, 403);
        var codes = new[] { "REPLENISHMENT_RECEIVE", "STOCK_RECEIPT_POST" };
        var permissions = await db.Permissions.AsNoTracking().Where(p => codes.Contains(p.PermissionCode)).ToArrayAsync(ct);
        if (permissions.Length != codes.Length) throw new InvalidOperationException("Receipt permission catalog missing.");
        foreach (var p in permissions) db.RolePermissions.Add(new() { RoleId = role, PermissionId = p.PermissionId,
            DataScope = "BRANCH", GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        using var drafts = await http.GetAsync($"/api/replenishments/{root}/draft-receipts", ct);
        if (drafts.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Draft receipt discovery failed.");
        using var draftJson = JsonDocument.Parse(await drafts.Content.ReadAsStringAsync(ct));
        if (!draftJson.RootElement.GetProperty("items").EnumerateArray().Any(d => d.GetProperty("documentId").GetInt64() == document.DocumentId))
            throw new InvalidOperationException("Synthetic Draft receipt missing from authorized discovery.");
        await Post(6, 400);
        if (!await db.StockDocuments.AsNoTracking().AnyAsync(d => d.DocumentId == document.DocumentId && d.Status == "DRAFT" && d.PostedAt == null, ct)
            || await db.StockMovements.AnyAsync(m => m.DocumentLineId == receiptLine.LineId, ct)
            || await db.StockReceiptLineDetails.AnyAsync(d => d.StockDocumentLineId == receiptLine.LineId, ct)
            || await db.MaterialReplenishmentReceiptLines.AnyAsync(l => l.StockDocumentLineId == receiptLine.LineId, ct))
            throw new InvalidOperationException("Excess receipt Post did not roll back atomically.");
        var posted = await Post(2, 200);
        if (posted.GetProperty("rawStatus").GetString() != "PARTIALLY_RECEIVED"
            || posted.GetProperty("lines")[0].GetProperty("netReceivedBaseQty").GetDecimal() != 2
            || posted.GetProperty("lines")[0].GetProperty("remainingBaseQty").GetDecimal() != 3
            || await db.StockMovements.CountAsync(m => m.DocumentLineId == receiptLine.LineId && m.SignedBaseQty == 2, ct) != 1
            || await db.MaterialReplenishmentReceiptLines.CountAsync(l => l.StockDocumentLineId == receiptLine.LineId && l.CreditedBaseQty == 2, ct) != 1)
            throw new InvalidOperationException("Posted receipt ledger/credit/status mismatch.");
        checks.Add(new { receiptPostAtomicRollback = true, receiptPostPartialCredit = true, draftDiscovery = true });
        return posted.GetProperty("rowVersion").GetString()!;
    }
}
