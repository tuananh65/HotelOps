using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;

sealed partial class ReplenishmentSuite
{
    async Task<string> LegacyFingerprint()
    {
        // Fixed table/key whitelist. The entire pre-existing rows (including rowversions)
        // must survive fixture creation, tests and cleanup unchanged.
        await using var d = Db(); var values = new List<string>();
        foreach (var sql in new[] {
            "SELECT (SELECT * FROM dbo.stock_documents ORDER BY document_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.stock_document_lines ORDER BY line_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.stock_movements ORDER BY movement_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_requests ORDER BY request_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_request_lines ORDER BY line_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.incidents ORDER BY incident_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.maintenance_works ORDER BY maintenance_work_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.assets ORDER BY asset_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value" })
            values.Add(await d.Database.SqlQueryRaw<string>(sql).SingleAsync());
        return JsonSerializer.Serialize(values);
    }
    async Task ContractEvidence()
    {
        await ReleaseAll(); await SetStock(100);
        var low = await Send<ReplenishmentPaged<LowStockShortage>>("GET", $"replenishment-shortages/low-stock?warehouseId={warehouse.WarehouseId}&itemId={item.ItemId}");
        Check(low.Total == 0, "Threshold at minimum excluded from read");
        var full = await ApprovedMR(2);
        await Send<MaterialFulfillmentDto>("POST", $"material-requests/{full.RequestId}/fulfillments", new MaterialIssue(issuer, full.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), 201);
        var closed = await ApprovedMR(2);
        await Send<MaterialFulfillmentDto>("POST", $"material-requests/{closed.RequestId}/fulfillments", new MaterialIssue(issuer, closed.RowVersion, "Partial", DateOnly.FromDateTime(DateTime.UtcNow), [new(closed.Lines[0].RequestLineId, 1)]), 201);
        closed = await Send<MaterialRequestDto>("GET", $"material-requests/{closed.RequestId}");
        await Send<MaterialRequestDto>("POST", $"material-requests/{closed.RequestId}/close-remainder", new MaterialCommand(approver, closed.RowVersion, prefix));
        await SetStock(0);
        var sources = await Send<ReplenishmentPaged<MaterialRequestShortage>>("GET", $"replenishment-shortages/material-requests?warehouseId={warehouse.WarehouseId}");
        Check(sources.Items.All(x => x.RequestId != full.RequestId && x.RequestId != closed.RequestId), "Terminal MR sources excluded despite physical shortage");
        foreach (var source in new[] { full, closed }) await Bad("POST", "replenishments", Input(1, source.Lines[0].RequestLineId));
        var r = await Send<ReplenishmentDto>("POST", "replenishments", Input() with { SupplierName = new('S', 200) }, 201);
        Check(r.SupplierName?.Length == 200, "Maximum supplier length accepted"); await Close(r);
        var receipt = await Draft(1);
        await using (var d = Db())
        {
            var primitive = new InventoryService(d); var refused = false;
            try { await primitive.PostReceipt(receipt.DocumentId, new(actor, receipt.RowVersion, [new(receipt.Lines[0].LineId, 1)]), default); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "Receipt primitive refuses missing caller transaction");
            await using var tx = await d.Database.BeginTransactionAsync();
            var result = await primitive.PostReceipt(receipt.DocumentId, new(actor, receipt.RowVersion, [new(receipt.Lines[0].LineId, 1)]), default);
            Check(result.Lines.Single().BaseQty == 1 && d.Database.CurrentTransaction == tx, "Receipt primitive retains caller transaction and authoritative base output");
            await tx.RollbackAsync();
        }
        Check((await Send<InventoryDocumentDto>("GET", $"inventory/documents/{receipt.DocumentId}")).Status == "DRAFT", "Caller rollback undoes receipt primitive");
        r = await Create();
        var before = await Footprint();
        await using (var d = Db()) await d.MaterialReplenishmentLines.Where(x => x.ReplenishmentId == r.ReplenishmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReceivedBaseQty, 1));
        try
        {
            await Send<object>("GET", $"replenishments/{r.ReplenishmentId}", status: 400);
            await Bad("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, r.RowVersion, prefix));
        }
        finally
        {
            await using var d = Db(); await d.MaterialReplenishmentLines.Where(x => x.ReplenishmentId == r.ReplenishmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReceivedBaseQty, 0));
        }
        Check(await Footprint() == before, "Cache mismatch fails closed with no success audit/state changes");
        await Close(r);
        // Failed number allocation must not consume a business number (SQL IDENTITY gaps are permitted).
        var head = "RPL-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-";
        string[] numbers;
        await using (var d = Db()) numbers = await d.MaterialReplenishments.Where(x => x.ReplenishmentNo.StartsWith(head)).Select(x => x.ReplenishmentNo).ToArrayAsync();
        var maximum = numbers.Select(x => long.TryParse(x[head.Length..], out var n) ? n : 0).DefaultIfEmpty().Max();
        await using (var d = Db(new SaveFailure(1))) await Rollback("number allocation", async () => { await Service(d).Create(Input(), default); });
        r = await Create(); Check(r.ReplenishmentNo == head + (maximum + 1).ToString("D3"), "Rolled-back RPL number reused"); await Close(r);
    }
    async Task VerifyAudit()
    {
        await using var d = Db();
        var ids = await d.MaterialReplenishments.Where(x => warehouses.Contains(x.WarehouseId)).Select(x => x.ReplenishmentId.ToString()).ToArrayAsync();
        var audits = await d.AuditLogs.AsNoTracking().Where(x => x.EntityType == "material_replenishments" && ids.Contains(x.EntityId)).ToArrayAsync();
        var events = new HashSet<string>();
        foreach (var a in audits)
        {
            Check(a.Action is "CREATE" or "UPDATE" && a.ActorUserId == actor && a.BranchId == branch, "FE10 audit action/actor/derived branch");
            using var json = JsonDocument.Parse(a.NewValues!); var root = json.RootElement; var state = root.GetProperty("State");
            events.Add(root.GetProperty("BusinessEvent").GetString()!);
            Check(root.GetProperty("ActorUserId").GetInt64() == actor && root.GetProperty("OccurredAt").GetDateTime().Kind == DateTimeKind.Utc, "Audit JSON actor/UTC");
            Check(state.GetProperty("BranchId").GetInt64() == branch && state.GetProperty("WarehouseId").GetInt64() > 0 && state.GetProperty("ReplenishmentNo").GetString()!.StartsWith("RPL-"), "Audit root identity");
            foreach (var l in state.GetProperty("Lines").EnumerateArray())
                Check(l.GetProperty("ItemId").GetInt64() > 0 && l.GetProperty("BaseUomId").GetInt64() > 0 && l.GetProperty("SourceType").GetString() != null
                    && l.GetProperty("RequestedBaseQty").GetDecimal() == l.GetProperty("NetReceivedBaseQty").GetDecimal() + l.GetProperty("RemainingBaseQty").GetDecimal()
                    && l.GetProperty("GrossCreditedBaseQty").GetDecimal() >= l.GetProperty("NetReceivedBaseQty").GetDecimal(), "Audit quantity/source snapshots");
            Check(a.Action == "CREATE" ? a.OldValues == null : a.OldValues != null, "Audit before/after");
        }
        foreach (var evt in new[] { "REPLENISHMENT_CREATED", "RECEIPT_LINKED", "REPLENISHMENT_PARTIALLY_RECEIVED", "REPLENISHMENT_FULLY_RECEIVED", "REMAINDER_CLOSED", "RECEIPT_REVERSED_REPLENISHMENT_RECALCULATED" }) Check(events.Contains(evt), "Audit event " + evt);
        Check(audits.Length > 0, "FE10 audit content verified");
    }
}
