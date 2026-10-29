using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

if (args.Length == 1 && args[0] == "--responsibility-coordination") { await ResponsibilityCoordinationChecks.Run(); return; }

var suite = new InventorySuite();
await suite.Run(adjustmentReversalOnly: args.Length == 1 && args[0] == "--adjustment-reversal");

sealed partial class InventorySuite
{
    private readonly string connection = Environment.GetEnvironmentVariable("ConnectionStrings__HotelOpsDb")
        ?? throw new InvalidOperationException("Supply ConnectionStrings__HotelOpsDb through the process environment.");
    private readonly InventoryAuthenticatedClient http = new("HOTEL_OPS_INV_STOREKEEPER") { BaseAddress = new("https://localhost:5188"), Timeout = TimeSpan.FromSeconds(60) };
    private readonly string prefix = "INVTEST_" + Guid.NewGuid().ToString("N")[..10];
    private readonly HashSet<long> documents = [], stocktakes = [], items = [], units = [], warehouses = [];
    private readonly List<string> passed = [];
    private readonly Dictionary<int, int> statuses = [];
    private long actor, other, category, branch, department;
    private InventoryUnitDto unit = null!, box = null!;
    private InventoryItemDto item = null!;
    private InventoryWarehouseDto source = null!, destination = null!;
    private Process? api;
    private int assertions, requests;
    private bool verifiedTarget;
    private HotelOpsDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
        .UseSqlServer(connection).AddInterceptors(interceptors).Options);
    private void Check(bool condition, string message)
    {
        assertions++; if (!condition) throw new InvalidOperationException(message);
    }
    private async Task<T> Send<T>(string method, string path, object? body = null, int expected = 200)
    {
        using var message = new HttpRequestMessage(new HttpMethod(method), "/api/" + path);
        if (body is not null) message.Content = JsonContent.Create(body);
        using var response = await ClientFor(method, path, body).SendAsync(message); requests++;
        var status = (int)response.StatusCode; statuses[status] = statuses.GetValueOrDefault(status) + 1;
        var text = await response.Content.ReadAsStringAsync();
        Check(status == expected, $"{method} {path}: expected {expected}, got {status}: {text}");
        if (expected >= 400)
        {
            if (expected == 400) { using var problem = JsonDocument.Parse(text); Check(problem.RootElement.GetProperty("status").GetInt32() == 400, "ProblemDetails missing."); }
            return default!;
        }
        var result = JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (result is InventoryDocumentDto d) documents.Add(d.DocumentId);
        if (result is InventoryStocktakeDto s) stocktakes.Add(s.StocktakeId);
        if (method == "POST")
        {
            if (result is InventoryItemDto i) items.Add(i.ItemId);
            if (result is InventoryUnitDto u) units.Add(u.UomId);
            if (result is InventoryWarehouseDto w) warehouses.Add(w.WarehouseId);
        }
        return result;
    }
    private Task Bad(string method, string path, object body) => Send<object>(method, path, body, 400);
    private Task<InventoryDocumentDto> Get(long id) => Send<InventoryDocumentDto>("GET", $"inventory/documents/{id}");
    private Task<InventoryStockDto> Stock(long warehouse) => Send<InventoryStockDto>("GET", $"inventory/warehouses/{warehouse}/items/{item.ItemId}/stock");
    private InventoryDraft Draft(long warehouse, decimal quantity = 10, long? origin = null, long? uom = null) =>
        new(null, warehouse, DateOnly.FromDateTime(DateTime.UtcNow), [new(item.ItemId, uom ?? unit.UomId, quantity, origin)], Note: prefix);
    private Task<InventoryDocumentDto> Receipt(decimal qty = 10, long? warehouse = null) => Send<InventoryDocumentDto>("POST", "inventory/receipts", Draft(warehouse ?? source.WarehouseId, qty), 201);
    private Task<InventoryDocumentDto> PostReceipt(InventoryDocumentDto d, decimal qty, decimal rejected = 0, decimal damaged = 0, string? reason = null) => Send<InventoryDocumentDto>("POST", $"inventory/receipts/{d.DocumentId}/post",
        new InventoryPost(d.RowVersion, [new(d.Lines[0].LineId, qty, rejected, damaged, reason)]));
    private async Task<InventoryDocumentDto> Adjust(decimal delta, long? warehouse = null) =>
        await PostAdjustmentFixture(await ProposeAdjustment(delta, warehouse ?? source.WarehouseId));
    private InventoryCommand Command(string version, string? reason = null, long? who = null) => SelectActor(new InventoryCommand(version, reason), who);
    public async Task Run(bool adjustmentReversalOnly = false)
    {
        Exception? failure = null;
        try
        {
            if (adjustmentReversalOnly)
            {
                Console.WriteLine("RUN_MODE=adjustment-reversal");
                if (reversalPosterHttp is null)
                    throw new InvalidOperationException("--adjustment-reversal requires externally provisioned HOTEL_OPS_INV_REVERSE_POSTER_USERNAME/_PASSWORD for an independent HTTP reversal poster; service fallback is not allowed in this mode.");
            }
            await using (var db = Db())
            {
                await db.Database.OpenConnectionAsync(); using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'|'+DB_NAME()";
                Check((string?)await cmd.ExecuteScalarAsync() == "KIENPC|HotelOps_Canonical_DEV2_Regression", "Wrong SQL target; abort all writes.");
                verifiedTarget = true;

                category = await db.ItemCategories.Where(x => x.IsActive).OrderBy(x => x.ItemCategoryId).Select(x => x.ItemCategoryId).FirstAsync();

                foreach (var type in new[] { typeof(InventoryItem), typeof(UnitsOfMeasure), typeof(UomConversion), typeof(Warehouse), typeof(StockThreshold), typeof(StockDocument), typeof(Stocktake) })
                {
                    var prop = db.Model.FindEntityType(type)!.FindProperty("RowVersion")!;
                    Check(prop.IsConcurrencyToken && prop.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, "Rowversion metadata invalid.");
                }
                await SchemaMetadata(db);
            }
            StartApi();
            await WaitForApi();
            await LoginIdentities();
            await using (var db = Db())
            {
                branch = await db.Users.Where(x => x.UserId == actor).Select(x => x.PrimaryBranchId).SingleAsync() ?? throw new Exception("STOREKEEPER fixture needs a primary branch.");
                department = await db.Departments.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.DepartmentId).FirstAsync();
            }
            if (adjustmentReversalOnly)
            {
                await Masters(); await Receipts(); await Transfers(); await AdjustmentsAndReversals();
            }
            else
            {
                await Masters(); await AuthorizationCases(); await Receipts(); await Transfers(); await AdjustmentsAndReversals(); await Stocktakes(); await Returns(); await Races(); await Rollbacks(); await DeepTests(); await DeepRaces(); await DeepRollback(); await ReadsAndRegression();
            }
        }
        catch (Exception ex) { failure = ex; Console.Error.WriteLine(ex); }
        finally
        {
            if (api is { HasExited: false })
            {
                foreach (var client in Clients()) try { await client.LogoutAsync(); } catch { Console.Error.WriteLine("Fixture logout failed; auth details redacted."); }
                api.Kill(true); await api.WaitForExitAsync();
            }
            foreach (var client in Clients()) client.Dispose();
            if (verifiedTarget) try { await Cleanup(); } catch (Exception ex) { failure ??= ex; Console.Error.WriteLine("Cleanup failed: " + ex); }
            Console.WriteLine(JsonSerializer.Serialize(new { prefix, assertions, requests, statuses, passed, success = failure is null }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure is not null) throw new InvalidOperationException("Inventory integration suite failed.", failure);
    }
    private void StartApi()
    {
        var info = new ProcessStartInfo("dotnet", "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll --urls https://localhost:5188")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["ConnectionStrings__HotelOpsDb"] = connection;
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["Logging__LogLevel__Default"] = "Warning";
        api = Process.Start(info)!; api.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine(e.Data); };
        api.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); }; api.BeginOutputReadLine(); api.BeginErrorReadLine();
    }
    private async Task WaitForApi()
    {
        for (var n = 0; n < 50; n++)
        {
            if (api!.HasExited) throw new InvalidOperationException("API process exited.");
            try { using var response = await http.GetAsync("/api/auth/csrf"); if (response.IsSuccessStatusCode) return; } catch (HttpRequestException) { }
            await Task.Delay(200);
        }
        throw new TimeoutException("API did not start.");
    }
    private async Task Masters()
    {
        unit = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(prefix + "U", "Test unit"), 201);
        box = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(prefix + "B", "Test box"), 201);
        var r = new InventoryItemWrite(prefix + "I", "Test item", category, unit.UomId, true);
        item = await Send<InventoryItemDto>("POST", "inventory/items", r, 201);
        await Bad("POST", "inventory/items", r);
        await Bad("POST", "inventory/items", r with { ItemCode = " " });
        await Bad("POST", "inventory/items", r with { ItemCode = new('X', 51) });
        await Bad("POST", "inventory/items", r with { ItemCode = prefix + "BAD", CategoryId = -1 });
        var changed = await Send<InventoryItemDto>("PUT", $"inventory/items/{item.ItemId}", r with { ItemName = "Changed", ExpectedRowVersion = item.RowVersion });
        await Bad("PUT", $"inventory/items/{item.ItemId}", r with { ExpectedRowVersion = item.RowVersion }); item = changed;
        var noOp = await Send<InventoryItemDto>("PUT", $"inventory/items/{item.ItemId}", r with { ItemName = "Changed", ExpectedRowVersion = item.RowVersion });
        Check(noOp.RowVersion == item.RowVersion, "No-op changed rowversion.");
        source = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "S", "Test source"), 201);
        destination = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "D", "Test destination"), 201);
        await Bad("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, null, prefix + "N", "Missing department"));
        await Bad("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "S", "Duplicate"));
        var conversion = await Send<InventoryConversionDto>("POST", $"inventory/items/{item.ItemId}/conversions", new InventoryConversionWrite(box.UomId, 2), 201);
        await Bad("PUT", $"inventory/items/{item.ItemId}/conversions/{conversion.ConversionId}", new InventoryConversionWrite(box.UomId, 0, ExpectedRowVersion: conversion.RowVersion));
        await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(10));
        Check((await Stock(source.WarehouseId)).OnHand == 0, "Empty pair must be zero.");
        passed.Add("masters, duplicates, lengths, references, rowversion, no-op, empty pair");
    }
    private async Task Receipts()
    {
        var d = await Receipt(); Check((await Stock(source.WarehouseId)).OnHand == 0, "Draft moved stock.");
        await Bad("POST", $"inventory/receipts/{d.DocumentId}/post", new InventoryPost(d.RowVersion, [new(d.Lines[0].LineId, 8, 1, 1)]));
        var posted = await PostReceipt(d, 8, 1, 1, "Shipment damage");
        Check((await Stock(source.WarehouseId)).OnHand == 8, "Receipt credited non-usable quantity.");
        await Bad("POST", $"inventory/receipts/{d.DocumentId}/post", new InventoryPost(posted.RowVersion, [new(d.Lines[0].LineId, 10)]));
        item = await Send<InventoryItemDto>("GET", $"inventory/items/{item.ItemId}");
        await Bad("PUT", $"inventory/items/{item.ItemId}", new InventoryItemWrite(item.ItemCode, item.ItemName, category, box.UomId, true, ExpectedRowVersion: item.RowVersion));
        var cancel = await Receipt(); cancel = await Send<InventoryDocumentDto>("POST", $"inventory/documents/{cancel.DocumentId}/cancel", Command(cancel.RowVersion, "Test cancellation"));
        Check(cancel.Status == "CANCELLED", "Cancel state.");
        await Bad("POST", $"inventory/documents/{cancel.DocumentId}/cancel", Command(cancel.RowVersion, "Again"));
        var conversionReceipt = await Send<InventoryDocumentDto>("POST", "inventory/receipts", Draft(source.WarehouseId, 2, uom: box.UomId), 201);
        await PostReceipt(conversionReceipt, 2); Check((await Stock(source.WarehouseId)).OnHand == 12, "Conversion credit incorrect.");
        await Bad("POST", "inventory/receipts", Draft(source.WarehouseId, 0));
        await Bad("POST", "inventory/receipts", Draft(source.WarehouseId, 0.0001m));
        passed.Add("receipt accepted-only stock, discrepancy, double post, cancellation, conversion snapshot, base UOM immutability");
    }
    private async Task Transfers()
    {
        var r = Draft(destination.WarehouseId, 5) with { SourceWarehouseId = source.WarehouseId };
        await Bad("POST", "inventory/transfers", r with { DestinationWarehouseId = source.WarehouseId });
        var d = await Send<InventoryDocumentDto>("POST", "inventory/transfers", r, 201);
        d = await Send<InventoryDocumentDto>("POST", $"inventory/transfers/{d.DocumentId}/dispatch", Command(d.RowVersion));
        Check(d.Status == "IN_TRANSIT" && d.Lines[0].InTransitQty == 5, "Dispatch/in transit.");
        Check((await Stock(source.WarehouseId)).OnHand == 7 && (await Stock(destination.WarehouseId)).OnHand == 0, "Dispatch destination credit.");
        d = await Send<InventoryDocumentDto>("POST", $"inventory/transfers/{d.DocumentId}/receipts", new InventoryReceive(d.RowVersion, [new(d.Lines[0].LineId, 2, 1, 0, "Damage")]));
        Check(d.Status == "PARTIALLY_RECEIVED" && d.Lines[0].InTransitQty == 2, "Partial receive.");
        await Bad("POST", $"inventory/transfers/{d.DocumentId}/receipts", new InventoryReceive(d.RowVersion, [new(d.Lines[0].LineId, 3, 0, 0)]));
        d = await Send<InventoryDocumentDto>("POST", $"inventory/transfers/{d.DocumentId}/receipts", new InventoryReceive(d.RowVersion, [new(d.Lines[0].LineId, 1, 0, 1, "Missing")]));
        Check(d.Status == "RECEIVED" && d.ReceivingBatches.Count == 2 && d.Lines[0].InTransitQty == 0, "Final receiving state.");
        Check((await Stock(destination.WarehouseId)).OnHand == 3, "Damaged/missing credited stock.");
        await Bad("POST", $"inventory/documents/{d.DocumentId}/reverse", Command(d.RowVersion, "Not eligible"));
        passed.Add("transfer source-only dispatch, partial/multiple receiving, loss persistence, cumulative cap, transfer reversal prohibited");
    }
    private Task AdjustmentsAndReversals() => AdjustmentReversalWorkflow();
    private async Task Stocktakes()
    {
        var s = await Send<InventoryStocktakeDto>("POST", "inventory/stocktakes", new InventoryStocktakeCreate(source.WarehouseId, prefix), 201);
        await Bad("POST", "inventory/stocktakes", new InventoryStocktakeCreate(source.WarehouseId, prefix));
        await Bad("POST", $"inventory/stocktakes/{s.StocktakeId}/submit", Command(s.RowVersion));
        Check(s.Lines.Count == 1 && s.Lines[0].SystemQty == 7 && s.Lines[0].CountedQty is null, "Cutoff snapshot/universe.");
        s = await Send<InventoryStocktakeDto>("PUT", $"inventory/stocktakes/{s.StocktakeId}/counts", new InventoryCounts(s.RowVersion, [new(s.Lines[0].StocktakeLineId, 5, "Count correction")]));
        s = await Send<InventoryStocktakeDto>("POST", $"inventory/stocktakes/{s.StocktakeId}/submit", Command(s.RowVersion));
        s = await Send<InventoryStocktakeDto>("POST", $"inventory/stocktakes/{s.StocktakeId}/reconcile", Command(s.RowVersion));
        Check(s.Status == "PENDING_ADJUSTMENT" && s.Lines[0].VarianceQty == -2, "Variance/reconcile.");
        await Adjust(3);
        await Send<object>("POST", $"inventory/stocktakes/{s.StocktakeId}/approve-adjustment", Command(s.RowVersion, "Correction", actor), 403);
        s = await Send<InventoryStocktakeDto>("POST", $"inventory/stocktakes/{s.StocktakeId}/approve-adjustment", Command(s.RowVersion, "Correction", other));
        documents.Add(s.AdjustmentDocumentId!.Value); Check(s.Status == "RECONCILED" && (await Stock(source.WarehouseId)).OnHand == 8, "Delta correction lost post-cutoff movement.");
        await Bad("POST", $"inventory/stocktakes/{s.StocktakeId}/approve-adjustment", Command(s.RowVersion, "Again", other));
        passed.Add("stocktake singleton, null count, cutoff, variance, segregation, delta preserves post-cutoff movements, one adjustment");
    }
    private async Task Returns()
    {
        // Isolated caller-owned transaction proves issue does not commit or implement MR workflow.
        await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync();
        var issue = new StockDocument
        {
            DocumentNo = prefix + "ISSUE",
            DocumentType = "ISSUE",
            Status = "POSTED",
            SourceWarehouseId = source.WarehouseId,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedBy = actor,
            PostedBy = actor,
            CreatedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow
        };
        var line = new StockDocumentLine { LineNo = 1, ItemId = item.ItemId, UomId = unit.UomId, ConversionFactor = 1, ActualQty = 3, BaseQty = 3 }; issue.StockDocumentLines.Add(line); db.StockDocuments.Add(issue);
        db.StockMovements.Add(new() { DocumentLine = line, WarehouseId = source.WarehouseId, ItemId = item.ItemId, SignedBaseQty = -3, CreatedBy = actor, OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); await tx.CommitAsync(); documents.Add(issue.DocumentId);
        var d = await Send<InventoryDocumentDto>("POST", "inventory/returns", Draft(source.WarehouseId, 3, line.LineId), 201);
        d = await Send<InventoryDocumentDto>("POST", $"inventory/returns/{d.DocumentId}/post", new InventoryPost(d.RowVersion, [new(d.Lines[0].LineId, 2, 0, 1, "Damaged return")]));
        Check(d.Lines[0].OriginalIssueLineId == line.LineId && (await Stock(source.WarehouseId)).OnHand == 7, "Return lineage/usable-only credit.");
        await Bad("POST", "inventory/returns", Draft(source.WarehouseId, 1, line.LineId));
        await Bad("POST", $"inventory/documents/{issue.DocumentId}/reverse", Command(Convert.ToBase64String(issue.RowVersion), "Deferred issue reversal"));
        passed.Add("return explicit ISSUE lineage, usable/damaged, cumulative physical cap, deferred issue reversal");
    }
    private async Task Races()
    {
        var d = await Receipt(1); var body = new InventoryPost(d.RowVersion, [new(d.Lines[0].LineId, 1)]);
        async Task<int> Race(string path, object payload)
        {
            using var response = await ClientFor("POST", path, payload).PostAsJsonAsync("/api/" + path, payload); var status = (int)response.StatusCode;
            lock (statuses) { requests++; statuses[status] = statuses.GetValueOrDefault(status) + 1; }
            return status;
        }
        var results = await Task.WhenAll(Race($"inventory/receipts/{d.DocumentId}/post", body), Race($"inventory/receipts/{d.DocumentId}/post", body));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }), "Double-post race must have one winner.");
        var effect=await Effect(d.DocumentId);Check(effect.Status=="POSTED" && effect.Movements==1 && effect.Delta==1,"Double-post DB movement invariant.");
        var current = (await Stock(source.WarehouseId)).OnHand; await Adjust(10 - current);
        var consume = new InventoryAdjustment(source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, -8)]);
        var first = await Send<InventoryDocumentDto>("POST", "inventory/adjustments", consume, 201);
        var second = await Send<InventoryDocumentDto>("POST", "inventory/adjustments", consume, 201);
        results = await Task.WhenAll(Race($"inventory/adjustments/{first.DocumentId}/post", Command(first.RowVersion, who: other)),
            Race($"inventory/adjustments/{second.DocumentId}/post", Command(second.RowVersion, who: other)));
        Check(results.Order().SequenceEqual(new[] { 200, 400 }) && (await Stock(source.WarehouseId)).OnHand == 2, "Concurrent 8+8 consumption invariant.");
        await using (var db = Db()) foreach (var id in await db.StockDocuments.Where(x => x.Reason == prefix).Select(x => x.DocumentId).ToListAsync()) documents.Add(id);
        d = await Receipt(1); body = new(d.RowVersion, [new(d.Lines[0].LineId, 1)]);
        results = await Task.WhenAll(Race($"inventory/receipts/{d.DocumentId}/post", body), Race($"inventory/documents/{d.DocumentId}/cancel", Command(d.RowVersion, "Race cancellation")));
        Check(results.Count(x => x == 200) == 1 && results.Count(x => x == 400) == 1, "Cancel-vs-post race.");
        effect=await Effect(d.DocumentId);Check(effect.Status is "POSTED" or "CANCELLED" && effect.Movements==(effect.Status=="POSTED"?1:0),"Cancel-versus-post DB terminal path.");
        passed.Add("real SQL races: double post, 8+8 from stock 10, cancel versus post");
    }
    private async Task Rollbacks()
    {
        foreach (var target in new[] { "stock_documents", "stock_document_lines", "audit_logs" })
        {
            await using var beforeDb = Db(); var before = await beforeDb.StockMovements.Where(x => x.WarehouseId == source.WarehouseId).SumAsync(x => (decimal?)x.SignedBaseQty) ?? 0;
            var count = await beforeDb.StockDocuments.CountAsync(x => x.DestinationWarehouseId == source.WarehouseId);
            await using (var db = Db(new FailAfterWrite(target)))
            {
                var service = new InventoryService(db); var failed = false;
                try { await service.CreateAdjustment(actor, new(source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1)]), default); }
                catch (Exception ex) when (ex is InjectedFailure || ex.InnerException is InjectedFailure) { failed = true; }
                Check(failed, "Fault injection was not reached: " + target);
            }
            await using var afterDb = Db(); Check((await afterDb.StockMovements.Where(x => x.WarehouseId == source.WarehouseId).SumAsync(x => (decimal?)x.SignedBaseQty) ?? 0) == before, "Rollback left a stock effect.");
            Check(await afterDb.StockDocuments.CountAsync(x => x.DestinationWarehouseId == source.WarehouseId) == count, "Rollback left a document.");
        }
        passed.Add("draft fault injection after document/line/audit SQL; posting movement faults covered separately");
    }
    private async Task ReadsAndRegression()
    {
        foreach (var path in new[] { "inventory/stock", "inventory/low-stock", "inventory/movements", "inventory/documents", "inventory/stocktakes", "inventory/units", "inventory/warehouses" }) await Send<JsonElement>("GET", path);
        var ledger = await Send<InventoryLedger>("GET", $"inventory/warehouses/{source.WarehouseId}/items/{item.ItemId}/ledger?from=2000-01-01T00:00:00Z&to=2100-01-01T00:00:00Z");
        Check(ledger.ClosingBalance == (await Stock(source.WarehouseId)).OnHand, "Ledger closing balance.");
        Check(ledger.Movements.Any(x => x.Status == "REVERSED") && ledger.Movements.Any(x => x.DocumentType == "REVERSAL"), "Ledger dropped reversed originals.");
        await using (var db = Db())
        {
            var ids = documents.Select(x => x.ToString()).ToList(); var audit = await db.AuditLogs.Where(x => x.EntityType == "stock_documents" && ids.Contains(x.EntityId)).ToListAsync();
            Check(audit.Count > 0 && audit.All(x => x.Action is "CREATE" or "UPDATE" or "CANCEL"), "Audit taxonomy.");
            foreach (var row in audit) { using var json = JsonDocument.Parse(row.NewValues!); using var fields = JsonDocument.Parse(row.ChangedFields!); Check(row.ActorUserId.HasValue && row.BranchId.HasValue, "Audit actor/scope."); }
            var takeKeys=stocktakes.Select(x=>x.ToString()).ToArray();
            var takeAudit=await db.AuditLogs.Where(x=>x.EntityType=="stocktakes" && takeKeys.Contains(x.EntityId)).ToListAsync();
            Check(takeAudit.Count>0 && takeAudit.All(x=>JsonDocument.Parse(x.NewValues!).RootElement.GetProperty("lines").ValueKind==JsonValueKind.Array),"Stocktake audit lacks structured line snapshots.");
            Check(takeAudit.Where(x=>x.Action=="CANCEL").All(x=>!x.ChangedFields!.Contains("lines")),"Cancellation fabricated line changes.");
            var itemKey=item.ItemId.ToString();Check(await db.AuditLogs.CountAsync(x=>x.EntityType=="inventory_items" && x.EntityId==itemKey)==2,"No-op or failed edit created an audit.");
        }
        foreach (var path in new[] { "assets", "incidents", "maintenance-works" }) await Send<JsonElement>("GET", path);
        passed.Add("ledger reversal history, balances, raw read models, audit JSON/taxonomy, DEV2 API regression");
    }
    private async Task Cleanup()
    {
        await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync();
        // Exact fixture ownership sets only; never delete unrelated business data.
        var docIds = documents.ToArray(); var takeIds = stocktakes.ToArray(); var itemIds = items.ToArray(); var warehouseIds = warehouses.ToArray(); var unitIds = units.ToArray();
        var keys = docIds.Select(x => x.ToString()).ToArray(); var takeKeys = takeIds.Select(x => x.ToString()).ToArray();
        await db.AuditLogs.Where(x => (x.EntityType == "stock_documents" && keys.Contains(x.EntityId)) || (x.EntityType == "stocktakes" && takeKeys.Contains(x.EntityId)) ||
            (x.EntityType == "inventory_items" && itemIds.Select(i => i.ToString()).Contains(x.EntityId)) || (x.EntityType == "warehouses" && warehouseIds.Select(i => i.ToString()).Contains(x.EntityId)) ||
            (x.EntityType == "units_of_measure" && unitIds.Select(i => i.ToString()).Contains(x.EntityId))).ExecuteDeleteAsync();
        var conversions = await db.UomConversions.Where(x => x.ItemId.HasValue && itemIds.Contains(x.ItemId.Value)).Select(x => x.ConversionId.ToString()).ToListAsync();
        var thresholds = await db.StockThresholds.Where(x => warehouseIds.Contains(x.WarehouseId)).Select(x => x.ThresholdId.ToString()).ToListAsync();
        await db.AuditLogs.Where(x => (x.EntityType == "uom_conversions" && conversions.Contains(x.EntityId)) || (x.EntityType == "stock_thresholds" && thresholds.Contains(x.EntityId))).ExecuteDeleteAsync();
        await db.StocktakeLines.Where(x => takeIds.Contains(x.StocktakeId)).ExecuteDeleteAsync();
        await db.Stocktakes.Where(x => takeIds.Contains(x.StocktakeId)).ExecuteDeleteAsync();
        await db.StockTransferReceiptLines.Where(x => docIds.Contains(x.TransferReceipt.TransferDocumentId)).ExecuteDeleteAsync();
        await db.StockTransferReceipts.Where(x => docIds.Contains(x.TransferDocumentId)).ExecuteDeleteAsync();
        await db.StockReceiptLineDetails.Where(x => docIds.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockReturnLineDetails.Where(x => docIds.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockMovements.Where(x => docIds.Contains(x.DocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockDocumentLines.Where(x => docIds.Contains(x.DocumentId)).ExecuteDeleteAsync();
        await db.StockDocuments.Where(x => docIds.Contains(x.DocumentId) && x.ReversalOfDocumentId.HasValue).ExecuteDeleteAsync();
        await db.StockDocuments.Where(x => docIds.Contains(x.DocumentId)).ExecuteDeleteAsync();
        await db.StockThresholds.Where(x => warehouseIds.Contains(x.WarehouseId)).ExecuteDeleteAsync();
        await db.UomConversions.Where(x => (x.ItemId.HasValue && itemIds.Contains(x.ItemId.Value)) || unitIds.Contains(x.FromUomId) || unitIds.Contains(x.ToUomId)).ExecuteDeleteAsync();
        await db.InventoryItems.Where(x => itemIds.Contains(x.ItemId)).ExecuteDeleteAsync();
        await db.Warehouses.Where(x => warehouseIds.Contains(x.WarehouseId)).ExecuteDeleteAsync();
        await db.UnitsOfMeasures.Where(x => unitIds.Contains(x.UomId)).ExecuteDeleteAsync();
        await tx.CommitAsync();
        Console.WriteLine("Fixture cleanup committed: " + prefix);
    }
}

sealed class InjectedFailure : Exception;
sealed class FailAfterWrite(string table) : DbCommandInterceptor
{
    private bool fired;
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (!fired && command.CommandText.Contains("INSERT INTO [" + table + "]", StringComparison.Ordinal))
        {
            fired = true; await result.DisposeAsync(); throw new InjectedFailure();
        }
        return result;
    }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (!fired && command.CommandText.Contains("INSERT INTO [" + table + "]", StringComparison.Ordinal)) { fired = true; throw new InjectedFailure(); }
        return ValueTask.FromResult(result);
    }
}
