using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

sealed partial class InventorySuite
{
    private async Task<(string Status,int Movements,decimal Delta)> Effect(long document)
    {
        await using var db=Db();var status=await db.StockDocuments.Where(d=>d.DocumentId==document).Select(d=>d.Status).SingleAsync();
        var movements=db.StockMovements.Where(m=>m.DocumentLine.DocumentId==document);
        return(status,await movements.CountAsync(),await movements.SumAsync(m=>(decimal?)m.SignedBaseQty)??0);
    }
    private async Task<int> Concurrent(string method, string path, object payload)
    {
        using var message = new HttpRequestMessage(new HttpMethod(method), "/api/" + path) { Content = JsonContent.Create(payload) };
        using var response = await ClientFor(method, path, payload).SendAsync(message); var status = (int)response.StatusCode;
        if (response.IsSuccessStatusCode)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("documentId", out var doc)) lock (documents) documents.Add(doc.GetInt64());
            if (json.RootElement.TryGetProperty("stocktakeId", out var take)) lock (stocktakes) stocktakes.Add(take.GetInt64());
        }
        lock (statuses) { requests++; statuses[status] = statuses.GetValueOrDefault(status) + 1; }
        return status;
    }
    private async Task<InventoryDocumentDto> Transfer(long from, long to, decimal quantity = 1)
    {
        return await Send<InventoryDocumentDto>("POST", "inventory/transfers", Draft(to, quantity) with { SourceWarehouseId = from }, 201);
    }
    private async Task<InventoryDocumentDto> Dispatch(InventoryDocumentDto d) =>
        await Send<InventoryDocumentDto>("POST", $"inventory/transfers/{d.DocumentId}/dispatch", Command(d.RowVersion));
    private async Task<InventoryStocktakeDto> Take(long warehouse) =>
        await Send<InventoryStocktakeDto>("POST", "inventory/stocktakes", new InventoryStocktakeCreate(warehouse, prefix), 201);
    private async Task<InventoryStocktakeDto> Count(InventoryStocktakeDto s, decimal qty, string? reason = null) =>
        await Send<InventoryStocktakeDto>("PUT", $"inventory/stocktakes/{s.StocktakeId}/counts", new InventoryCounts(s.RowVersion, s.Lines.Select(l => new InventoryCount(l.StocktakeLineId, qty, reason)).ToList()));
    private async Task<InventoryStocktakeDto> Action(InventoryStocktakeDto s, string action, long? who = null) =>
        await Send<InventoryStocktakeDto>("POST", $"inventory/stocktakes/{s.StocktakeId}/{action}", Command(s.RowVersion, prefix, who));
    private async Task<StockDocumentLine> IssueFixture(decimal qty)
    {
        await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var issue = new StockDocument
        {
            DocumentNo = prefix + Guid.NewGuid().ToString("N")[..8],
            DocumentType = "ISSUE",
            Status = "POSTED",
            SourceWarehouseId = source.WarehouseId,
            CreatedAt = now,
            PostedAt = now,
            CreatedBy = actor,
            PostedBy = actor,
            DocumentDate = DateOnly.FromDateTime(now)
        };
        var l = new StockDocumentLine { ItemId = item.ItemId, UomId = unit.UomId, LineNo = 1, ActualQty = qty, BaseQty = qty, ConversionFactor = 1 }; issue.StockDocumentLines.Add(l); db.StockDocuments.Add(issue);
        db.StockMovements.Add(new() { DocumentLine = l, WarehouseId = source.WarehouseId, ItemId = item.ItemId, SignedBaseQty = -qty, CreatedBy = actor, OccurredAt = now });
        await db.SaveChangesAsync(); await tx.CommitAsync(); documents.Add(issue.DocumentId); return l;
    }
    private async Task DeepTests()
    {
        // Null/blank, invalid references, precision, stale versions and inactive catalog.
        await Bad("POST", "inventory/units", new InventoryUnitWrite(" ", ""));
        await Bad("POST", "inventory/units", new InventoryUnitWrite(prefix + "OVER", new('x', 101)));
        await Bad("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, -1, prefix + "BAD", "Bad department"));
        await Bad("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "BAD", "Bad manager", ManagerUserId: -1));
        await Bad("POST", "inventory/receipts", new InventoryDraft(null, source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), []));
        await Bad("POST", "inventory/receipts", Draft(source.WarehouseId) with { DocumentDate = default });
        await Bad("POST", "inventory/receipts", new { actorUserId = actor, destinationWarehouseId = source.WarehouseId, documentDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new object?[] { null } });
        await Send<object>("PUT", "inventory/warehouses/-1", new InventoryWarehouseWrite(branch, department, "X", "Missing", ExpectedRowVersion: source.RowVersion), 404);
        await Bad("PUT", $"inventory/items/{item.ItemId}", new InventoryItemWrite(item.ItemCode, item.ItemName, category, unit.UomId, true, ExpectedRowVersion: "invalid-base64"));
        var inactive = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(prefix + "OFF", "Inactive", false), 201);
        await Bad("POST", "inventory/items", new InventoryItemWrite(prefix + "OFFITEM", "Inactive reference", category, inactive.UomId, true));
        var threshold = await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(0, (await Send<List<InventoryThresholdDto>>("GET", $"inventory/warehouses/{source.WarehouseId}/thresholds"))[0].RowVersion));
        await Bad("PUT", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(-1, threshold.RowVersion));
        var balance = (await Stock(source.WarehouseId)).OnHand;
        threshold = await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(balance, threshold.RowVersion));
        Check(!(await Stock(source.WarehouseId)).BelowMinimum, "Equality must not be below minimum.");
        threshold = await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(balance + 1, threshold.RowVersion));
        Check((await Stock(source.WarehouseId)).BelowMinimum, "Below-minimum mathematics.");
        await using (var db = Db())
        {
            var t = await db.StockThresholds.SingleAsync(t => t.ThresholdId == threshold.ThresholdId); t.AlertEnabled = false;
            await db.SaveChangesAsync();
        }
        Check((await Stock(source.WarehouseId)).BelowMinimum, "AlertEnabled changed low-stock definition.");
        // Conversion snapshot, global rejection, no chaining and exact representability.
        var conversions = await Send<List<InventoryConversionDto>>("GET", $"inventory/items/{item.ItemId}/conversions");
        var conv = conversions.Single(x => x.ItemId == item.ItemId && x.FromUomId == box.UomId);
        var posted = await Send<InventoryDocumentDto>("POST", "inventory/receipts", Draft(source.WarehouseId, 1, uom: box.UomId), 201); posted = await PostReceipt(posted, 1);
        conv = await Send<InventoryConversionDto>("PUT", $"inventory/items/{item.ItemId}/conversions/{conv.ConversionId}", new InventoryConversionWrite(box.UomId, 0.333333m, ExpectedRowVersion: conv.RowVersion));
        Check((await Get(posted.DocumentId)).Lines[0].ConversionFactor == 2, "Historical factor snapshot changed.");
        var fractional = await Send<InventoryDocumentDto>("POST", "inventory/receipts", Draft(source.WarehouseId, 1, uom: box.UomId), 201);
        await Bad("POST", $"inventory/receipts/{fractional.DocumentId}/post", new InventoryPost(fractional.RowVersion, [new(fractional.Lines[0].LineId, 1)]));
        await Send<InventoryConversionDto>("PUT", $"inventory/items/{item.ItemId}/conversions/{conv.ConversionId}", new InventoryConversionWrite(box.UomId, 2, ExpectedRowVersion: conv.RowVersion));
        await using (var db = Db())
        {
            var global = new UomConversion { FromUomId = inactive.UomId, ToUomId = unit.UomId, ConversionFactor = 5, IsActive = true }; db.UomConversions.Add(global); await db.SaveChangesAsync();
            // Temporarily activate fixture UOM; global conversion remains readable but unusable.
            var u = await db.UnitsOfMeasures.SingleAsync(u => u.UomId == inactive.UomId); u.IsActive = true; await db.SaveChangesAsync();
            await Bad("POST", "inventory/receipts", Draft(source.WarehouseId, 1, uom: inactive.UomId));
            db.UomConversions.Remove(global); await db.SaveChangesAsync();
            var chain = new UomConversion { ItemId = item.ItemId, FromUomId = inactive.UomId, ToUomId = box.UomId, ConversionFactor = 2, IsActive = true };
            db.UomConversions.Add(chain); await db.SaveChangesAsync();
            await Bad("POST", "inventory/receipts", Draft(source.WarehouseId, 1, uom: inactive.UomId));
            db.UomConversions.Remove(chain); await db.SaveChangesAsync();
        }
        // Draft editing (including duplicate ItemId), stale edit, zero stock before posting.
        var draft = await Receipt(2);
        var edit = Draft(source.WarehouseId, 2) with { ExpectedRowVersion = draft.RowVersion, Lines = [new(item.ItemId, unit.UomId, 1), new(item.ItemId, unit.UomId, 1)] };
        var edited = await Send<InventoryDocumentDto>("PUT", $"inventory/receipts/{draft.DocumentId}", edit);
        Check(edited.Lines.Select(l => l.LineNo).SequenceEqual(new[] { 1, 2 }), "Line numbering or duplicate-item handling.");
        await Bad("PUT", $"inventory/receipts/{draft.DocumentId}", edit);
        await Bad("POST", $"inventory/returns/{draft.DocumentId}/post", new InventoryPost(edited.RowVersion, edited.Lines.Select(l => new InventoryAcceptedLine(l.LineId, 1)).ToList()));
        // Zero-variance, zero-count, return/recount and cancel stocktake branches.
        var s = await Take(destination.WarehouseId); s = await Count(s, (await Stock(destination.WarehouseId)).OnHand); s = await Action(s, "submit"); s = await Action(s, "reconcile");
        Check(s.Status == "RECONCILED" && s.AdjustmentDocumentId is null, "Zero variance created adjustment.");
        s = await Take(destination.WarehouseId); var originalVersion = s.RowVersion; s = await Count(s, 0, "Zero is a valid count");
        await Bad("PUT", $"inventory/stocktakes/{s.StocktakeId}/counts", new InventoryCounts(originalVersion, []));
        s = await Action(s, "submit"); s = await Action(s, "reconcile"); s = await Action(s, "return"); Check(s.Status == "COUNTED", "Return stocktake state.");
        s = await Count(s, (await Stock(destination.WarehouseId)).OnHand); s = await Action(s, "reconcile");
        s = await Take(destination.WarehouseId); s = await Action(s, "cancel"); Check(s.Status == "CANCELLED", "Stocktake cancel state.");
        // Warehouse deactivation and cross-branch audit.
        source = await Send<InventoryWarehouseDto>("GET", $"inventory/warehouses/{source.WarehouseId}");
        await Bad("PUT", $"inventory/warehouses/{source.WarehouseId}", new InventoryWarehouseWrite(branch, department, source.WarehouseCode, source.WarehouseName, false, ExpectedRowVersion: source.RowVersion));
        await using (var db = Db())
        {
            var cross = await db.Departments.Where(d => d.IsActive && d.Branch.IsActive && d.BranchId != branch).OrderBy(d => d.DepartmentId).FirstAsync();
            var w = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(cross.BranchId, cross.DepartmentId, prefix + "X", "Cross branch"), 201);
            await Bad("PUT", $"inventory/warehouses/{source.WarehouseId}", new InventoryWarehouseWrite(cross.BranchId, cross.DepartmentId, source.WarehouseCode, source.WarehouseName, ExpectedRowVersion: source.RowVersion));
            var d = await Transfer(source.WarehouseId, w.WarehouseId); d = await Dispatch(d);
            var key = d.DocumentId.ToString(); var audit = await db.AuditLogs.Where(a => a.EntityType == "stock_documents" && a.EntityId == key).ToListAsync();
            Check(audit.Count == 4 && audit.Select(a => a.BranchId).Distinct().Count() == 2, "Cross-branch audit scopes.");
            Check(audit.All(a => a.NewValues!.Contains("SOURCE") || a.NewValues!.Contains("DESTINATION")), "Missing audit scopeRole.");
            d = await Send<InventoryDocumentDto>("POST", $"inventory/transfers/{d.DocumentId}/receipts", new InventoryReceive(d.RowVersion, [new(d.Lines[0].LineId, 0, 0, 1, "Missing entire transfer")]));
            Check(d.Status == "RECEIVED", "All missing transfer must fully reconcile.");
        }
        // Historical period with no movements retains opening balance.
        var ledger = await Send<InventoryLedger>("GET", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/ledger?from=2090-01-01T00:00:00Z&to=2091-01-01T00:00:00Z");
        Check(ledger.Movements.Count == 0 && ledger.OpeningBalance == ledger.ClosingBalance && ledger.ClosingBalance == (await Stock(source.WarehouseId)).OnHand, "Empty historical period opening balance.");
        await InternalIssue(); await Numbering(); await Cancellation();
        await ExtraBranches();
        passed.Add("extended catalog/UOM/threshold/warehouse, draft edits, exact decimal, stocktake branches, cross-branch audit, historical opening, internal issue, numbering and cancellation");
    }
    private async Task ExtraBranches()
    {
        var w = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "EMPTY", "Empty fixture warehouse"), 201);
        var take = await Take(w.WarehouseId);
        InventoryWarehouseWrite Deactivate() => new(branch, w.DepartmentId, w.WarehouseCode, w.WarehouseName, false, ExpectedRowVersion: w.RowVersion);
        await Bad("PUT", $"inventory/warehouses/{w.WarehouseId}", Deactivate());
        await Action(take, "cancel");
        await using (var db = Db()) { var legacy = await db.Stocktakes.SingleAsync(x => x.StocktakeId == take.StocktakeId); legacy.Status = "COMPLETED"; await db.SaveChangesAsync(); }
        Check((await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{take.StocktakeId}")).Status == "COMPLETED", "Legacy status was remapped.");
        var d = await Receipt(1, w.WarehouseId);
        await Bad("PUT", $"inventory/warehouses/{w.WarehouseId}", Deactivate());
        await Send<InventoryDocumentDto>("POST", $"inventory/documents/{d.DocumentId}/cancel", Command(d.RowVersion, prefix));
        await using (var db = Db()) { var fixture = await db.Warehouses.SingleAsync(x => x.WarehouseId == w.WarehouseId); fixture.DepartmentId = null; await db.SaveChangesAsync(); }
        w = await Send<InventoryWarehouseDto>("GET", $"inventory/warehouses/{w.WarehouseId}"); Check(w.DepartmentId is null, "Legacy NULL department not readable.");
        w = await Send<InventoryWarehouseDto>("PUT", $"inventory/warehouses/{w.WarehouseId}", Deactivate()); Check(!w.IsActive, "Zero-stock warehouse could not deactivate.");
        await Bad("POST", "inventory/receipts", Draft(w.WarehouseId, 1));
        var positive = await Receipt(1); positive = await PostReceipt(positive, 1);
        await ReversePosted(positive);
        var rwarehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "REV", "Reversal fixture"), 201);
        positive = await Receipt(2, rwarehouse.WarehouseId); positive = await PostReceipt(positive, 2); await Adjust(-2, rwarehouse.WarehouseId);
        var insufficientReversal = await ProposeReversal(positive);
        await ReversalPostFails(insufficientReversal);
        // Wrong-type origin and mismatching item must never be matched by ItemId alone.
        await Bad("POST", "inventory/returns", Draft(source.WarehouseId, 1, positive.Lines[0].LineId));
        await using (var db = Db())
        {
            var origin = await db.StockDocumentLines.FirstAsync(l => l.ItemId == item.ItemId && l.Document.DocumentType == "ISSUE");
            var alternate = await Send<InventoryItemDto>("POST", "inventory/items", new InventoryItemWrite(prefix + "OTHER", "Other fixture", category, unit.UomId, true), 201);
            await Bad("POST", "inventory/returns", Draft(source.WarehouseId, 1, origin.LineId) with { Lines = [new(alternate.ItemId, unit.UomId, 1, origin.LineId)] });
            var returned = await db.StockDocuments.Where(x => x.DocumentType == "RETURN" && x.Status == "POSTED" && x.StockDocumentLines.Any(l => l.ItemId == item.ItemId)).Select(x => x.DocumentId).FirstAsync();
            var originalReturn = await Get(returned); var before = (await Stock(source.WarehouseId)).OnHand;
            await ReversePosted(originalReturn);
            Check((await Stock(source.WarehouseId)).OnHand == before - originalReturn.Lines.Sum(l => l.BaseQty), "Return reversal offset incorrect.");
        }
        var discrepancy = await Take(destination.WarehouseId); discrepancy = await Count(discrepancy, (await Stock(destination.WarehouseId)).OnHand + 1); discrepancy = await Action(discrepancy, "submit");
        await Bad("POST", $"inventory/stocktakes/{discrepancy.StocktakeId}/reconcile", Command(discrepancy.RowVersion));
        discrepancy = await Count(discrepancy, (await Stock(destination.WarehouseId)).OnHand + 1, "Missing unit"); discrepancy = await Action(discrepancy, "reconcile"); discrepancy = await Action(discrepancy, "return");
        discrepancy = await Count(discrepancy, (await Stock(destination.WarehouseId)).OnHand); await Action(discrepancy, "reconcile");
    }
    private async Task InternalIssue()
    {
        await Adjust(5); await using var db = Db(); var service = new InventoryService(db);
        var missingTx = false; try { await service.PostIssue(new(actor, 0, source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), []), default); } catch (InvalidOperationException) { missingTx = true; }
        Check(missingTx, "Issue must require caller transaction.");
        var before = await db.StockDocuments.CountAsync(); await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var now = DateTime.UtcNow;
            var request = new MaterialRequest { RequestNo = prefix + "MR", BranchId = branch, DepartmentId = department, RequesterId = actor, Purpose = prefix, CreatedAt = now };
            var line = new MaterialRequestLine { ItemId = item.ItemId, UomId = unit.UomId, RequestedQty = 1 }; request.MaterialRequestLines.Add(line); db.MaterialRequests.Add(request);
            var fulfillment = new MaterialFulfillment { FulfillmentNo = prefix + "MF", Request = request, WarehouseId = source.WarehouseId, FulfilledBy = actor, CreatedAt = now }; db.MaterialFulfillments.Add(fulfillment); await db.SaveChangesAsync();
            var result = await service.PostIssue(new(actor, fulfillment.FulfillmentId, source.WarehouseId, DateOnly.FromDateTime(now), [new(line.LineId, item.ItemId, unit.UomId, 1)]), default);
            Check(db.Database.CurrentTransaction is not null && result.Lines.Single().RequestLineId == line.LineId, "Issue correlation/transaction participation.");
            Check(await db.StockMovements.AnyAsync(x => x.DocumentLineId == result.Lines[0].StockDocumentLineId && x.SignedBaseQty == -1), "Issue primitive movement missing.");
            await tx.RollbackAsync();
        }
        await using var fresh = Db(); Check(await fresh.StockDocuments.CountAsync() == before, "Issue independently committed.");
    }
    private async Task Numbering()
    {
        await using var db = Db(); var date = DateTime.UtcNow.ToString("yyyyMMdd"); var head = "GRN-" + date + "-";
        var numbers = await db.StockDocuments.Where(x => x.DocumentNo.StartsWith(head)).Select(x => x.DocumentNo).ToListAsync();
        var max = numbers.Select(x => long.TryParse(x[head.Length..], out var n) ? n : 0).DefaultIfEmpty().Max(); var sentinel = Math.Max(max + 1000, 1000);
        foreach (var suffix in new[] { sentinel.ToString(), "invalid" })
        {
            var d = new StockDocument { DocumentNo = head + suffix, DocumentType = "RECEIPT", Status = "DRAFT", DestinationWarehouseId = source.WarehouseId, CreatedBy = actor, CreatedAt = DateTime.UtcNow, DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow), Note = prefix };
            db.StockDocuments.Add(d); await db.SaveChangesAsync(); documents.Add(d.DocumentId);
        }
        var generated = await Receipt(1); Check(generated.DocumentNo == head + (sentinel + 1), "Number allocator mishandled >999 or malformed legacy suffix.");
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Concurrent("POST", "inventory/receipts", Draft(source.WarehouseId, 1))));
        Check(results.All(x => x == 201), "Concurrent number allocation.");
        var ids = documents.ToArray(); Check(await db.StockDocuments.Where(x => ids.Contains(x.DocumentId)).Select(x => x.DocumentNo).Distinct().CountAsync() == ids.Length, "Document number uniqueness.");
    }
    private async Task Cancellation()
    {
        await using var locker = Db(); await using var tx = await locker.Database.BeginTransactionAsync();
        await locker.Database.ExecuteSqlRawAsync("EXEC sys.sp_getapplock @Resource='HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction';");
        await using var waiting = Db(); using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)); var cancelled = false;
        try { await new InventoryService(waiting).CreateAdjustment(actor, new(source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1)]), cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "CancellationToken did not cancel SQL lock wait."); await tx.RollbackAsync();
    }
    private async Task DeepRaces()
    {
        await Adjust(20);
        // Edit versus post, one legal winner.
        var d = await Receipt(1); var post = new InventoryPost(d.RowVersion, [new(d.Lines[0].LineId, 1)]);
        var results = await Task.WhenAll(Concurrent("POST", $"inventory/receipts/{d.DocumentId}/post", post), Concurrent("PUT", $"inventory/receipts/{d.DocumentId}", Draft(source.WarehouseId, 2) with { ExpectedRowVersion = d.RowVersion }));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }), "Edit-versus-post race.");
        var effect=await Effect(d.DocumentId);Check(effect.Status is "DRAFT" or "POSTED" && effect.Movements==(effect.Status=="POSTED"?1:0),"Edit-versus-post persisted illegal effects.");
        // Opposite transfers and double dispatch.
        await Adjust(5, destination.WarehouseId);
        var a = await Transfer(source.WarehouseId, destination.WarehouseId); var b = await Transfer(destination.WarehouseId, source.WarehouseId);
        results = await Task.WhenAll(Concurrent("POST", $"inventory/transfers/{a.DocumentId}/dispatch", Command(a.RowVersion)), Concurrent("POST", $"inventory/transfers/{b.DocumentId}/dispatch", Command(b.RowVersion)));
        Check(results.All(x => x == 200), "Opposite transfers deadlocked/failed.");
        var aEffect=await Effect(a.DocumentId);var bEffect=await Effect(b.DocumentId);
        Check(aEffect==("IN_TRANSIT",1,-1m) && bEffect==("IN_TRANSIT",1,-1m),"Opposite transfer DB effects.");
        a = await Get(a.DocumentId); b = await Get(b.DocumentId);
        await Bad("POST", $"inventory/transfers/{a.DocumentId}/dispatch", Command(a.RowVersion));
        // Same stale token receiving race: one batch only, retry succeeds on fresh token.
        var payload = new InventoryReceive(a.RowVersion, [new(a.Lines[0].LineId, 1, 0, 0)]);
        results = await Task.WhenAll(Concurrent("POST", $"inventory/transfers/{a.DocumentId}/receipts", payload), Concurrent("POST", $"inventory/transfers/{a.DocumentId}/receipts", payload));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }) && (await Get(a.DocumentId)).ReceivingBatches.Count == 1, "Concurrent receiving duplicated batch.");
        effect=await Effect(a.DocumentId);Check(effect==("RECEIVED",2,0m),"Concurrent receive destination movement invariant.");
        await Send<InventoryDocumentDto>("POST", $"inventory/transfers/{b.DocumentId}/receipts", new InventoryReceive(b.RowVersion, [new(b.Lines[0].LineId, 1, 0, 0)]));
        // Concurrent returns against a common original issue line.
        var origin = await IssueFixture(3);
        var r1 = await Send<InventoryDocumentDto>("POST", "inventory/returns", Draft(source.WarehouseId, 2, origin.LineId), 201);
        var r2 = await Send<InventoryDocumentDto>("POST", "inventory/returns", Draft(source.WarehouseId, 2, origin.LineId), 201);
        results = await Task.WhenAll(Concurrent("POST", $"inventory/returns/{r1.DocumentId}/post", new InventoryPost(r1.RowVersion, [new(r1.Lines[0].LineId, 2)])), Concurrent("POST", $"inventory/returns/{r2.DocumentId}/post", new InventoryPost(r2.RowVersion, [new(r2.Lines[0].LineId, 2)])));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }), "Concurrent returns exceeded original issue.");
        await using(var db=Db())
        {
            var physical=await db.StockReturnLineDetails.Where(l=>l.OriginalIssueLineId==origin.LineId && l.StockDocumentLine.Document.Status=="POSTED")
                .SumAsync(l=>(decimal?)((l.StockDocumentLine.ActualQty+l.DamagedQty)*l.StockDocumentLine.ConversionFactor))??0;
            Check(physical==2 && physical<=origin.BaseQty,"Concurrent return cumulative DB cap.");
        }
        var doubleDispatch = await Transfer(source.WarehouseId, destination.WarehouseId);
        results = await Task.WhenAll(Concurrent("POST", $"inventory/transfers/{doubleDispatch.DocumentId}/dispatch", Command(doubleDispatch.RowVersion)), Concurrent("POST", $"inventory/transfers/{doubleDispatch.DocumentId}/dispatch", Command(doubleDispatch.RowVersion)));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }), "Concurrent dispatch duplicated source debit.");
        effect=await Effect(doubleDispatch.DocumentId);Check(effect==("IN_TRANSIT",1,-1m),"Double-dispatch DB movement invariant.");
        // Two reversal requests and open-stocktake creation.
        d = await Adjust(1); results = await Task.WhenAll(Concurrent("POST", $"inventory/documents/{d.DocumentId}/reverse", Command(d.RowVersion, prefix)), Concurrent("POST", $"inventory/documents/{d.DocumentId}/reverse", Command(d.RowVersion, prefix)));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }), "Concurrent reversal proposals must have one winner.");
        await using (var proposalDb = Db())
        {
            var reversalId = await proposalDb.StockDocuments.Where(x => x.ReversalOfDocumentId == d.DocumentId).Select(x => x.DocumentId).SingleAsync();
            Check((await Get(d.DocumentId)).Status == "POSTED" && (await Effect(reversalId)).Movements == 0, "Concurrent proposal changed original/effects.");
            await PostReversalFixture(await Get(reversalId));
        }
        await using(var db=Db())
        {
            Check(await db.StockDocuments.CountAsync(x=>x.ReversalOfDocumentId==d.DocumentId)==1 &&
                await db.StockMovements.Where(m=>m.DocumentLine.DocumentId==d.DocumentId || m.DocumentLine.Document.ReversalOfDocumentId==d.DocumentId).SumAsync(m=>m.SignedBaseQty)==0,"Concurrent reversal DB uniqueness/offset invariant.");
        }
        results = await Task.WhenAll(Concurrent("POST", "inventory/stocktakes", new InventoryStocktakeCreate(destination.WarehouseId, prefix)), Concurrent("POST", "inventory/stocktakes", new InventoryStocktakeCreate(destination.WarehouseId, prefix)));
        Check(results.Order().SequenceEqual(new[] { 201, 400 }), "Concurrent stocktake creation.");
        await using (var db = Db())
        {
            var open = await db.Stocktakes.AsNoTracking().SingleAsync(x => x.WarehouseId == destination.WarehouseId && x.Status == "IN_PROGRESS");
            var s = await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{open.StocktakeId}"); await Action(s, "cancel");
        }
        // Adjustment versus ordinary write preserves both deltas.
        var start = (await Stock(source.WarehouseId)).OnHand; var take = await Take(source.WarehouseId); take = await Count(take, start - 1, prefix); take = await Action(take, "submit"); take = await Action(take, "reconcile");
        var ordinary = await ProposeAdjustment(2, source.WarehouseId);
        results = await Task.WhenAll(Concurrent("POST", $"inventory/stocktakes/{take.StocktakeId}/approve-adjustment", Command(take.RowVersion, prefix, other)),
            Concurrent("POST", $"inventory/adjustments/{ordinary.DocumentId}/post", Command(ordinary.RowVersion, who: other)));
        take = await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{take.StocktakeId}"); documents.Add(take.AdjustmentDocumentId!.Value);
        Check(results.All(x => x == 200) && (await Stock(source.WarehouseId)).OnHand == start + 1, "Stocktake-vs-operation lost delta.");
        passed.Add("all required SQL race families: edit/post, opposite transfers, dispatch, receive, return cap, reversal, open stocktake, stocktake adjustment/ordinary stock");
    }
    private async Task<int[]> Footprint()
    {
        await using var db = Db(); return [await db.StockDocuments.CountAsync(), await db.StockDocumentLines.CountAsync(), await db.StockMovements.CountAsync(), await db.AuditLogs.CountAsync(), await db.StockTransferReceipts.CountAsync(), await db.StockTransferReceiptLines.CountAsync()];
    }
    private async Task Fails(Func<InventoryService, Task> operation, IInterceptor interceptor)
    {
        var before = await Footprint(); await using (var db = Db(interceptor))
        {
            var injected = false; try { await operation(new(db)); } catch (Exception ex) when (ex is InjectedFailure || ex.InnerException is InjectedFailure) { injected = true; }
            Check(injected, "Expected injected failure not reached.");
        }
        var after = await Footprint(); Check(before.SequenceEqual(after), "Rollback left partial rows/audit.");
    }
    private async Task DeepRollback()
    {
        var receipt = await Receipt(1);
        await Fails(async svc => { await svc.Post("RECEIPT", receipt.DocumentId, actor, new(receipt.RowVersion, [new(receipt.Lines[0].LineId, 1)]), default); }, new FailAfterWrite("stock_movements"));
        Check((await Get(receipt.DocumentId)).Status == "DRAFT", "Post rollback changed status.");
        var transfer = await Dispatch(await Transfer(source.WarehouseId, destination.WarehouseId));
        await Fails(async svc => { await svc.Receive(transfer.DocumentId, actor, new(transfer.RowVersion, [new(transfer.Lines[0].LineId, 1, 0, 0)]), default); }, new FailAfterWrite("stock_transfer_receipts"));
        Check((await Get(transfer.DocumentId)).ReceivingBatches.Count == 0, "Receipt batch rollback orphan.");
        var take = await Take(source.WarehouseId); take = await Count(take, (await Stock(source.WarehouseId)).OnHand - 1, prefix); take = await Action(take, "submit"); take = await Action(take, "reconcile");
        await Fails(async svc => { await svc.StocktakeAction(take.StocktakeId, "approve-adjustment", other, Command(take.RowVersion, prefix, other), default); }, new FailAfterWrite("stock_documents"));
        Check((await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{take.StocktakeId}")).AdjustmentDocumentId is null, "Stocktake adjustment rollback retained linkage.");
        await Fails(async svc => { await svc.CreateAdjustment(actor, new(source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1)]), default); }, new FailBeforeCommit());
        passed.Add("rollback receipt status/movement, transfer batch, stocktake adjustment/link, before commit; no orphan business or audit rows");
    }
}

sealed class FailBeforeCommit : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        => throw new InjectedFailure();
}
