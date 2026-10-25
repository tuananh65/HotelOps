using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.Replenishments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

await new ReplenishmentSuite().Run();

sealed partial class ReplenishmentSuite
{
    readonly string connection = Environment.GetEnvironmentVariable("ConnectionStrings__HotelOpsDb") ?? throw new Exception("Supply connection in process environment.");
    readonly string prefix = "RPLTEST_" + Guid.NewGuid().ToString("N")[..10];
    readonly Dev2HttpClient http = new() { BaseAddress = new("https://localhost:5193"), Timeout = TimeSpan.FromSeconds(60) };
    readonly System.Collections.Concurrent.ConcurrentQueue<string> errors = new();
    readonly List<long> items = [], units = [], warehouses = [];
    long actor, approver, issuer, branch, department, category;
    InventoryUnitDto unit = null!, box = null!;
    InventoryItemDto item = null!;
    InventoryWarehouseDto warehouse = null!;
    InventoryThresholdDto threshold = null!;
    Process? api;
    int requests, assertions, races, faults;
    bool verified;
    string? legacyBefore;
    HotelOpsDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options);
    ReplenishmentService Service(HotelOpsDbContext d, IInventoryReceiptPosting? post = null) { var inv = new InventoryService(d); return new(d, post ?? inv, inv); }
    void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    async Task<T> Send<T>(string method, string path, object? body = null, int status = 200)
    {
        using var m = new HttpRequestMessage(new(method), "/api/" + path); if (body != null) m.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(m); requests++; var text = await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode == status, $"{method} {path}: {(int)response.StatusCode} expected {status}: {text}\n" + string.Join('\n', errors.TakeLast(55)));
        if (status >= 400) { if (status == 400) Check(JsonDocument.Parse(text).RootElement.GetProperty("status").GetInt32() == 400, "ProblemDetails"); return default!; }
        return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    Task Bad(string method, string path, object body) => Send<object>(method, path, body, 400);
    ReplenishmentCreate Input(decimal qty = 10, long? mrLine = null) => new(actor, warehouse.WarehouseId, prefix, null,
        [new(1, item.ItemId, mrLine == null ? "LOW_STOCK" : "MATERIAL_REQUEST_SHORTAGE", mrLine == null ? threshold.ThresholdId : null, mrLine, qty)]);
    Task<ReplenishmentDto> Create(decimal qty = 10, long? source = null) => Send<ReplenishmentDto>("POST", "replenishments", Input(qty, source), 201);
    Task<ReplenishmentDto> Get(long id) => Send<ReplenishmentDto>("GET", $"replenishments/{id}");
    Task<ReplenishmentDto> Close(ReplenishmentDto r) => Send<ReplenishmentDto>("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, r.RowVersion, prefix));
    async Task<InventoryDocumentDto> Draft(decimal quantity, long? warehouseId = null, long? itemId = null, long? uom = null)
        => await Send<InventoryDocumentDto>("POST", "inventory/receipts", new InventoryDraft(actor, null, warehouseId ?? warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), [new(itemId ?? item.ItemId, uom ?? unit.UomId, quantity)], Note: prefix), 201);
    Task<InventoryDocumentDto> Post(InventoryDocumentDto d, decimal? accepted = null) => Send<InventoryDocumentDto>("POST", $"inventory/receipts/{d.DocumentId}/post", new InventoryPost(actor, d.RowVersion, d.Lines.Select(l => new InventoryAcceptedLine(l.LineId, accepted ?? l.RequestedQty!.Value, DiscrepancyReason: prefix)).ToArray()));
    ReplenishmentPost Posting(ReplenishmentDto r, InventoryDocumentDto d, decimal accepted, decimal rejected = 0, decimal damaged = 0) => new(actor, r.RowVersion, d.DocumentId, d.RowVersion,
        [new(d.Lines[0].LineId, accepted, rejected, damaged, prefix)], [new(d.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)]);
    Task<ReplenishmentDto> Receive(ReplenishmentDto r, InventoryDocumentDto d, decimal accepted, decimal rejected = 0, decimal damaged = 0) => Send<ReplenishmentDto>("POST", $"replenishments/{r.ReplenishmentId}/receipts/post", Posting(r, d, accepted, rejected, damaged));
    Task<ReplenishmentDto> Link(ReplenishmentDto r, InventoryDocumentDto d) => Send<ReplenishmentDto>("POST", $"replenishments/{r.ReplenishmentId}/receipts/link", new ReplenishmentLink(actor, r.RowVersion, [new(d.Lines[0].LineId, r.Lines[0].ReplenishmentLineId)]));
    async Task Reverse(long doc, int status = 200)
    {
        var d = await Send<InventoryDocumentDto>("GET", $"inventory/documents/{doc}");
        await Send<InventoryDocumentDto>("POST", $"inventory/documents/{doc}/reverse", new InventoryCommand(actor, d.RowVersion, prefix), status);
    }
    async Task<MaterialRequestDto> ApprovedMR(decimal qty = 100, bool duplicate = false)
    {
        var r = await Send<MaterialRequestDto>("POST", "material-requests", new MaterialRequestWrite(actor, branch, department, warehouse.WarehouseId, prefix, DateTime.UtcNow.AddDays(-1), null, null, null, null,
            duplicate ? [new(null, 1, item.ItemId, unit.UomId, qty, null), new(null, 2, item.ItemId, unit.UomId, qty, null)] : [new(null, 1, item.ItemId, unit.UomId, qty, null)], true, approver), 201);
        return await Send<MaterialRequestDto>("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(approver, r.RowVersion, r.Lines.Select(x => new MaterialApprovalLine(x.RequestLineId, x.RequestedQty)).ToArray()));
    }
    async Task Stock(decimal delta)
    {
        await Send<InventoryDocumentDto>("POST", "inventory/adjustments", new InventoryAdjustment(actor, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, delta)]), 201);
    }
    async Task SetStock(decimal desired)
    {
        var now = await Send<InventoryStockDto>("GET", $"inventory/warehouses/{warehouse.WarehouseId}/items/{item.ItemId}/stock");
        if (now.OnHand != desired) await Stock(desired - now.OnHand);
    }
    async Task ReleaseAll()
    {
        var page = await Send<ReplenishmentPaged<ReplenishmentSummary>>("GET", $"replenishments?warehouseId={warehouse.WarehouseId}&pageSize=100");
        foreach (var x in page.Items.Where(x => x.Status is "REQUESTED" or "PARTIALLY_RECEIVED")) await Close(await Get(x.ReplenishmentId));
    }
    public async Task Run()
    {
        try
        {
            await using (var db = Db())
            {
                Check(await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'/'+DB_NAME() Value").SingleAsync() == "KIENPC/HotelOps_Canonical_DEV2_Regression", "Wrong target"); verified = true;
                legacyBefore = await LegacyFingerprint();
                var users = await db.Users.Where(x => x.PrimaryBranchId != null && x.AccountStatus == "ACTIVE" && x.EmploymentStatus == "ACTIVE" && x.DeletedAt == null).OrderBy(x => x.UserId).ToListAsync();
                branch = users.GroupBy(x => x.PrimaryBranchId!.Value).First(g => g.Count() >= 3).Key; var selected = users.Where(x => x.PrimaryBranchId == branch).Take(3).ToArray(); actor = selected[0].UserId; approver = selected[1].UserId; issuer = selected[2].UserId;
                department = await db.Departments.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.DepartmentId).FirstAsync(); category = await db.ItemCategories.Where(x => x.IsActive).Select(x => x.ItemCategoryId).FirstAsync();
            }
            var start = new ProcessStartInfo("dotnet", "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll --urls https://localhost:5193") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["ConnectionStrings__HotelOpsDb"] = connection; start.Environment["Logging__LogLevel__Default"] = "Warning"; start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            api = Process.Start(start)!; api.OutputDataReceived += (_, e) => { if (e.Data != null) errors.Enqueue(e.Data); }; api.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.Enqueue(e.Data); }; api.BeginOutputReadLine(); api.BeginErrorReadLine();
            var ready = false; for (var i = 0; i < 100; i++) { try { using var response = await http.GetAsync("/api/auth/csrf"); ready = response.IsSuccessStatusCode; if (!ready) { await Task.Delay(100); continue; } break; } catch (HttpRequestException) { await Task.Delay(100); } } Check(ready, "Startup");
            await http.LoginAsync();
            unit = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(actor, prefix + "U", prefix), 201); units.Add(unit.UomId);
            box = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(actor, prefix + "B", prefix), 201); units.Add(box.UomId);
            item = await Send<InventoryItemDto>("POST", "inventory/items", new InventoryItemWrite(actor, prefix, prefix, category, unit.UomId, true), 201); items.Add(item.ItemId);
            warehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(actor, branch, department, prefix, prefix), 201); warehouses.Add(warehouse.WarehouseId);
            threshold = await Send<InventoryThresholdDto>("PUT", $"inventory/warehouses/{warehouse.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(actor, 100));
            await Send<InventoryConversionDto>("POST", $"inventory/items/{item.ItemId}/conversions", new InventoryConversionWrite(actor, box.UomId, 2), 201);
            await Functional(); await ContractEvidence(); await Concurrency(); await Faults(); await VerifyCatalog(); await VerifyAudit();
            foreach (var path in new[] { "assets", "incidents", "maintenance-works" }) await Send<JsonElement>("GET", path);
            Console.WriteLine($"PASS requests={requests} assertions={assertions} SQL_overlap_races={races} rollback_cases={faults}");
        }
        finally
        {
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); } api?.Dispose();
            if (verified) { await Cleanup(); if (legacyBefore != null) { Check(legacyBefore == await LegacyFingerprint(), "Legacy business rows changed"); Console.WriteLine("Legacy business rows: byte-equivalent JSON before/after fixtures."); } }
        }
    }
    async Task Functional()
    {
        var low = await Send<ReplenishmentPaged<LowStockShortage>>("GET", $"replenishment-shortages/low-stock?warehouseId={warehouse.WarehouseId}"); Check(low.Items.Single().DeficitToMinimum == 100, "Low stock context");
        var r = await Create(); Check(r.RawStatus == "REQUESTED" && r.Lines[0].IsOpenCoverage && r.Lines[0].RemainingBaseQty == 10, "Create low");
        await Bad("POST", "replenishments", Input());
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, "bad", prefix));
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, r.RowVersion, "short"));
        var stale = r; r = await Close(r); Check(r.RawStatus == "CLOSED" && r.Lines[0].RemainingBaseQty == 10 && !r.Lines[0].IsOpenCoverage, "Close requested");
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, stale.RowVersion, prefix));
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, r.RowVersion, prefix));
        foreach (var q in new[] { 0m, -1m, .0001m, 1000000000000000m }) await Bad("POST", "replenishments", Input(q));
        r = await Create(999999999999999.999m); await Close(r);
        await Bad("POST", "replenishments", Input() with { SupplierName = new('X', 201) });
        r = await Send<ReplenishmentDto>("POST", "replenishments", Input() with { SupplierName = null, ExpectedOn = new DateOnly(2000, 1, 1) }, 201); await Close(r);
        r = await Create();
        var draft = await Draft(10); r = await Receive(r, draft, 3, 4, 3); Check(r.Lines[0].NetReceivedBaseQty == 3 && r.RawStatus == "PARTIALLY_RECEIVED", "Accepted only");
        draft = await Draft(3); r = await Receive(r, draft, 3); draft = await Draft(4); r = await Receive(r, draft, 4);
        Check(r.RawStatus == "FULLY_RECEIVED" && r.Lines[0].GrossCreditedBaseQty == 10 && !r.Lines[0].IsOpenCoverage, "Three receipts full");
        await Bad("POST", $"replenishments/{r.ReplenishmentId}/close", new ReplenishmentClose(actor, r.RowVersion, prefix));
        await Reverse(r.Receipts.Last().StockDocumentId); r = await Get(r.ReplenishmentId); Check(r.RawStatus == "PARTIALLY_RECEIVED" && r.Lines[0].NetReceivedBaseQty == 6 && r.Lines[0].GrossCreditedBaseQty == 10, "Full to partial correction");
        await Close(r);
        r = await Create(5); draft = await Draft(5); r = await Receive(r, draft, 5); await Reverse(draft.DocumentId); r = await Get(r.ReplenishmentId); Check(r.RawStatus == "REQUESTED" && r.Lines[0].NetReceivedBaseQty == 0, "Full to requested correction"); await Close(r);
        r = await Create(5); draft = await Draft(2); r = await Receive(r, draft, 2); await Reverse(draft.DocumentId); r = await Get(r.ReplenishmentId); Check(r.RawStatus == "REQUESTED", "Partial to requested"); await Close(r);
        r = await Create(5); draft = await Draft(2); r = await Receive(r, draft, 2); r = await Close(r); await Reverse(draft.DocumentId); r = await Get(r.ReplenishmentId); Check(r.RawStatus == "CLOSED" && r.Lines[0].RemainingBaseQty == 5 && !r.Lines[0].IsOpenCoverage, "Closed stays closed correction");
        r = await Create(5); draft = await Draft(5); r = await Receive(r, draft, 5); var competing = await Create(2); await Reverse(draft.DocumentId, 400); Check((await Get(r.ReplenishmentId)).RawStatus == "FULLY_RECEIVED", "Coverage collision atomically blocks reversal"); await Close(competing); await Reverse(draft.DocumentId); await Close(await Get(r.ReplenishmentId));
        r = await Create(2); draft = await Post(await Draft(2)); r = await Link(r, draft); Check(r.RawStatus == "FULLY_RECEIVED", "Posted adoption");
        competing = await Create(2); await Bad("POST", $"replenishments/{competing.ReplenishmentId}/receipts/link", new ReplenishmentLink(actor, competing.RowVersion, [new(draft.Lines[0].LineId, competing.Lines[0].ReplenishmentLineId)])); await Close(competing);
        r = await Create(2); draft = await Draft(3); await Bad("POST", $"replenishments/{r.ReplenishmentId}/receipts/post", Posting(r, draft, 3)); Check((await Send<InventoryDocumentDto>("GET", $"inventory/documents/{draft.DocumentId}")).Status == "DRAFT", "Overcredit rolls back Inventory post"); await Close(r);
        r = await Create(2); draft = await Draft(2); r = await Receive(r, draft, 0, 2); Check(r.Lines[0].NetReceivedBaseQty == 0 && r.Receipts.Count == 0, "Zero accepted gives no credit"); await Close(r);
        await SetStock(0); var mr = await ApprovedMR(6, true);
        var ms = await Send<ReplenishmentPaged<MaterialRequestShortage>>("GET", $"replenishment-shortages/material-requests?warehouseId={warehouse.WarehouseId}"); Check(ms.Items.Count(x => x.RequestId == mr.RequestId) == 2, "Duplicate MR lines independent");
        r = await Create(3, mr.Lines[0].RequestLineId);
        await Bad("POST", "replenishments", Input()); await Bad("POST", "replenishments", Input(3, mr.Lines[1].RequestLineId)); await Close(r);
        r = await Create(); await Bad("POST", "replenishments", Input(3, mr.Lines[0].RequestLineId)); await Close(r);
        await Bad("POST", "replenishments", Input(7, mr.Lines[0].RequestLineId));
        await using (var db = Db()) { var legacy = await db.MaterialRequestLines.Where(x => x.Request.Status == "APPROVED" && !x.Request.MaterialRequestApprovalCycles.Any()).Select(x => x.LineId).FirstAsync(); await Bad("POST", "replenishments", Input(1, legacy)); }
        await SetStock(1000); await Bad("POST", "replenishments", Input()); await Bad("POST", "replenishments", Input(1, mr.Lines[0].RequestLineId)); await SetStock(0);
        var read = await Send<ReplenishmentPaged<ReplenishmentSummary>>("GET", $"replenishments?warehouseId={warehouse.WarehouseId}&pageSize=100"); Check(read.Items.Where(x => x.ExpectedOn != null).Count() == 1 && read.Items[0].ExpectedOn != null, "Null-last ordering");
        await Send<ReplenishmentHistory>("GET", $"replenishments/{r.ReplenishmentId}/history"); await Send<ReplenishmentReceiptDto[]>("GET", $"replenishments/{r.ReplenishmentId}/receipts");
        await Send<object>("GET", "replenishments/9223372036854775807", status: 404);
        await Send<object>("GET", "replenishments?pageSize=101", status: 400);
        foreach (var q in new[] { "status=CLOSED", "sourceType=LOW_STOCK", $"itemId={item.ItemId}", $"branchId={branch}", "supplier=RPLTEST", "expectedFrom=1999-01-01&expectedTo=2001-01-01" }) await Send<ReplenishmentPaged<ReplenishmentSummary>>("GET", "replenishments?" + q);
        await Additional();
    }
}
