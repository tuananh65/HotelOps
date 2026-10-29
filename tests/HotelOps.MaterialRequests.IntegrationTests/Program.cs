using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

if (args.Length == 1 && args[0] == "--deactivation-coordination") { await DeactivationCoordination.Run(); return; }

await new MaterialSuite().Run();

sealed partial class MaterialSuite
{
    readonly string connection = Environment.GetEnvironmentVariable("ConnectionStrings__HotelOpsDb") ?? throw new Exception("Supply ConnectionStrings__HotelOpsDb through environment.");
    readonly string prefix = "MRTEST_" + Guid.NewGuid().ToString("N")[..10];
    readonly HttpClient http = new() { BaseAddress = new("http://127.0.0.1:5191"), Timeout = TimeSpan.FromSeconds(60) };
    readonly List<long> incidents = [];
    readonly List<long> unitIds = [], itemIds = [], warehouseIds = [];
    long actor, approver, issuer, branch, department, category, otherBranch;
    InventoryUnitDto unit = null!, box = null!;
    InventoryItemDto item = null!;
    InventoryWarehouseDto warehouse = null!;
    Process? api;
    readonly System.Collections.Concurrent.ConcurrentQueue<string> apiErrors = new();
    int checks, requests, races, faults;
    bool verified;
    HotelOpsDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options);
    MaterialRequestService Service(HotelOpsDbContext db, IInventoryIssuePosting? posting = null) { var inv = new InventoryService(db); return new(db, posting ?? inv, inv); }
    void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    async Task<T> Send<T>(string method, string path, object? body = null, int expected = 200)
    {
        using var msg = new HttpRequestMessage(new(method), "/api/" + path); if (body != null) msg.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(msg); requests++; var text = await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode == expected, $"{method} {path}: {(int)response.StatusCode}, expected {expected}: {text}\n" + string.Join('\n', apiErrors.TakeLast(35)));
        if (expected >= 400) { if (expected == 400) Check(JsonDocument.Parse(text).RootElement.GetProperty("status").GetInt32() == 400, "ProblemDetails"); return default!; }
        return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    Task Bad(string method, string path, object body) => Send<object>(method, path, body, 400);
    Task<MaterialRequestDto> Get(long id) => Send<MaterialRequestDto>("GET", $"material-requests/{id}");
    MaterialRequestWrite Write(decimal qty = 12, bool submit = false) => new(actor, branch, department, warehouse.WarehouseId, prefix, DateTime.UtcNow.AddDays(-1), null, null, null, null,
        [new(null, 1, item.ItemId, unit.UomId, qty, null)], submit, approver);
    MaterialRequestWrite Edit(MaterialRequestDto r) => new(actor, r.BranchId, r.DepartmentId, r.SourceWarehouseId, r.Purpose, r.NeededAt, r.PriorityId, r.ReceivingRoomId, r.ReceivingAreaId, r.IncidentId,
        r.Lines.Where(x => x.IsActive).Select(x => new MaterialLineWrite(x.RequestLineId, x.LineNo, x.ItemId, x.UomId, x.RequestedQty, x.Note)).ToArray(), false, null, r.RowVersion);
    Task<MaterialRequestDto> Create(MaterialRequestWrite? w = null) => Send<MaterialRequestDto>("POST", "material-requests", w ?? Write(), 201);
    Task<MaterialRequestDto> Cmd(MaterialRequestDto r, string cmd, long? who = null, string? reason = null) => Send<MaterialRequestDto>("POST", $"material-requests/{r.RequestId}/{cmd}", new MaterialCommand(who ?? actor, r.RowVersion, reason, approver));
    Task<MaterialRequestDto> Approve(MaterialRequestDto r, decimal? qty = null) => Send<MaterialRequestDto>("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(approver, r.RowVersion, r.Lines.Where(x => x.IsActive).Select(x => new MaterialApprovalLine(x.RequestLineId, qty ?? x.RequestedQty)).ToArray()));
    Task<MaterialFulfillmentDto> Issue(MaterialRequestDto r, decimal? quantity = null) => Send<MaterialFulfillmentDto>("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(issuer, r.RowVersion, quantity is null ? "FullRemaining" : "Partial", DateOnly.FromDateTime(DateTime.UtcNow), quantity is null ? null : [new(r.Lines.First(x => x.IsActive).RequestLineId, quantity.Value)]), 201);
    async Task<MaterialRequestDto> Approved(decimal qty = 12) => await Approve(await Create(Write(qty, true)));
    public async Task Run()
    {
        try
        {
            await using (var db = Db())
            {
                var target = await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),@@SERVERNAME)+N'/'+DB_NAME() AS Value").SingleAsync();
                Check(target == "KIENPC/HotelOpsDB", "Wrong SQL target"); verified = true;
                var participants = await db.Users.Where(x => x.DeletedAt == null && x.AccountStatus == "ACTIVE" && x.EmploymentStatus == "ACTIVE" && x.PrimaryBranchId != null).OrderBy(x => x.UserId).ToListAsync();
                branch = participants.GroupBy(x => x.PrimaryBranchId!.Value).First(x => x.Count() >= 3).Key;
                var users = participants.Where(x => x.PrimaryBranchId == branch).Take(3).ToArray(); actor = users[0].UserId; approver = users[1].UserId; issuer = users[2].UserId;
                department = await db.Departments.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.DepartmentId).FirstAsync();
                category = await db.ItemCategories.Where(x => x.IsActive).Select(x => x.ItemCategoryId).FirstAsync();
                otherBranch = await db.Branches.Where(x => x.BranchId != branch && x.IsActive).Select(x => x.BranchId).FirstAsync();
            }
            var start = new ProcessStartInfo("dotnet", "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll --urls http://127.0.0.1:5191") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["ConnectionStrings__HotelOpsDb"] = connection;
            start.Environment["Logging__LogLevel__Default"] = "Warning";
            api = Process.Start(start)!; api.OutputDataReceived += (_, e) => { if(e.Data != null) apiErrors.Enqueue(e.Data); }; api.ErrorDataReceived += (_, e) => { if(e.Data != null) apiErrors.Enqueue(e.Data); }; api.BeginOutputReadLine(); api.BeginErrorReadLine();
            var ready = false; for (var i = 0; i < 100; i++) { try { using var response = await http.GetAsync("/api/health"); ready = true; break; } catch (HttpRequestException) { await Task.Delay(100); } }
            Check(ready, "API startup");
            unit = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(prefix + "U", prefix), 201); unitIds.Add(unit.UomId);
            box = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(prefix + "B", prefix), 201); unitIds.Add(box.UomId);
            item = await Send<InventoryItemDto>("POST", "inventory/items", new InventoryItemWrite(prefix, prefix, category, unit.UomId, true), 201); itemIds.Add(item.ItemId);
            warehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix, prefix), 201); warehouseIds.Add(warehouse.WarehouseId);
            await Send<InventoryConversionDto>("POST", $"inventory/items/{item.ItemId}/conversions", new InventoryConversionWrite(box.UomId, 2), 201);
            await Send<InventoryDocumentDto>("POST", "inventory/adjustments", new InventoryAdjustment(warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 10000)]), 201);
            await Core(); await ValidationAndNumbering(); await Races(); await Rollbacks(); await Metadata(); await VerifyCatalog();
            Console.WriteLine($"PASS requests={requests} assertions={checks} overlapping_races={races} rollback_faults={faults}");
        }
        finally
        {
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); } api?.Dispose();
            if (verified) await Cleanup();
        }
    }
    async Task Core()
    {
        var r = await Create(Write() with { NeededAt = null, SourceWarehouseId = null }); Check(r.RawStatus == "DRAFT" && r.Lines[0].ApprovedQty == null, "Draft");
        var no = await Send<MaterialRequestDto>("PUT", $"material-requests/{r.RequestId}", Edit(r)); Check(no.RowVersion == r.RowVersion, "No-op version");
        Check((await Send<MaterialHistoryDto>("GET", $"material-requests/{r.RequestId}/history")).Audit.Count == 1, "No-op must not audit");
        await Bad("POST", $"material-requests/{r.RequestId}/submit", new MaterialCommand(actor, r.RowVersion, AssignedApproverUserId: approver));
        r = await Send<MaterialRequestDto>("PUT", $"material-requests/{r.RequestId}", Edit(r) with { SourceWarehouseId = warehouse.WarehouseId, NeededAt = DateTime.UtcNow.AddYears(-1) });
        r = await Cmd(r, "submit"); Check(r.Cycles.Count == 1, "First cycle");
        var pending = r; r = await Send<MaterialRequestDto>("PUT", $"material-requests/{r.RequestId}", Edit(r) with { Purpose = prefix + " changed" }); Check(r.Cycles.Count == 2 && r.Cycles[0].Status == "SUPERSEDED", "Pending revision");
        await Bad("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(approver, pending.RowVersion, [new(r.Lines[0].RequestLineId, 1)]));
        r = await Cmd(r, "request-information", approver, prefix + " information");
        r = await Send<MaterialRequestDto>("PUT", $"material-requests/{r.RequestId}", Edit(r) with { Purpose = prefix + " clarified" }); Check(r.RawStatus == "NEEDS_INFORMATION", "Information edit not resubmit");
        r = await Cmd(r, "submit"); r = await Approve(r, 8); Check(r.RawStatus == "APPROVED" && r.Cycles.Last().Lines[0].ApprovedQty == 8, "Partial approval snapshot");
        var historical = r.Cycles.Last(); r = await Send<MaterialRequestDto>("PUT", $"material-requests/{r.RequestId}", Edit(r) with { Lines = [new(null, 2, item.ItemId, unit.UomId, 9, null)] });
        Check(r.RawStatus == "PENDING_APPROVAL" && r.Lines.Count(x => !x.IsActive) == 1 && r.Cycles.Single(x => x.ApprovalCycleId == historical.ApprovalCycleId).Lines[0].ApprovedQty == 8, "Retirement preserves history");
        await Bad("PUT", $"material-requests/{r.RequestId}", Edit(r) with { Lines = [new(null, 1, item.ItemId, unit.UomId, 9, null)] });
        r = await Approve(r); r = await Cmd(r, "cancel", reason: prefix); Check(r.RawStatus == "CANCELLED", "Cancel approved zero issued");
        var z = await Create(Write(5, true)); z = await Send<MaterialRequestDto>("POST", $"material-requests/{z.RequestId}/approve", new MaterialApproval(approver, z.RowVersion, [new(z.Lines[0].RequestLineId, 0)], prefix)); Check(z.RawStatus == "REJECTED", "All zero rejection");
        var f = await Approved();
        for (var n = 0; n < 3; n++) { var batch = await Issue(f, 3); Check(batch.StockDocumentId != null && batch.Lines.All(x => x.StockDocumentLineId != null), "Durable line linkage"); f = await Get(f.RequestId); }
        Check(f.RawStatus == "PARTIALLY_ISSUED" && f.Lines[0].FulfilledQty == 9, "Three batches");
        var batches = await Send<MaterialFulfillmentDto[]>("GET", $"material-requests/{f.RequestId}/fulfillments"); Check(batches.Length == 3, "Fulfillment list endpoint");
        await Send<MaterialFulfillmentDto>("GET", $"material-requests/{f.RequestId}/fulfillments/{batches[0].FulfillmentId}");
        await Send<object>("GET", $"material-requests/{f.RequestId}/fulfillments/9223372036854775807", expected: 404);
        await Bad("POST", $"material-requests/{f.RequestId}/cancel", new MaterialCommand(actor, f.RowVersion, prefix));
        await Bad("PUT", $"material-requests/{f.RequestId}", Edit(f) with { IncidentId = -1 });
        await Issue(f); f = await Get(f.RequestId); Check(f.RawStatus == "FULLY_ISSUED" && f.CompletedAt != null && f.Lines[0].FulfilledQty == 12, "Final issue");
        var c = await Approved(); await Issue(c, 1); c = await Get(c.RequestId); c = await Cmd(c, "close-remainder", approver, prefix); Check(c.Lines[0].HistoricalRemainingQty == 11 && c.Lines[0].OpenFulfillableQty == 0, "Closure retains demand history");
        var shortage = await Approved(999999); await Bad("POST", $"material-requests/{shortage.RequestId}/fulfillments", new MaterialIssue(issuer, shortage.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow))); Check((await Get(shortage.RequestId)).Fulfillments.Count == 0, "Shortage rolls back");
        var dup = await Create(Write(2, true) with { Lines = [new(null, 1, item.ItemId, unit.UomId, 2, null), new(null, 2, item.ItemId, box.UomId, 2, null)] }); dup = await Approve(dup);
        var stockBefore = await Send<InventoryStockDto>("GET", $"inventory/warehouses/{warehouse.WarehouseId}/items/{item.ItemId}/stock"); await Issue(dup); var stockAfter = await Send<InventoryStockDto>("GET", $"inventory/warehouses/{warehouse.WarehouseId}/items/{item.ItemId}/stock"); Check(stockBefore.OnHand - stockAfter.OnHand == 6, "Duplicate different UOM aggregation");
        await Send<MaterialAvailabilityDto>("GET", $"material-requests/{dup.RequestId}/availability"); await Send<MaterialHistoryDto>("GET", $"material-requests/{dup.RequestId}/history");
        await Send<MaterialPageDto>("GET", $"material-requests?branchId={branch}&keyword={prefix}&pageSize=2");
        await Send<object>("GET", "material-requests/9223372036854775807", expected: 404);
        await Bad("POST", "material-requests", Write() with { Lines = [] }); await Bad("POST", "material-requests", Write() with { Lines = [new(null, 1, item.ItemId, unit.UomId, .0001m, null)] });
        await Bad("POST", "material-requests", Write(2, true) with { AssignedApproverUserId = actor });
        await Bad("PUT", $"material-requests/{shortage.RequestId}", Edit(shortage) with { ExpectedRowVersion = "bad" });
        await LocationsAndIncidents();
    }
    async Task LocationsAndIncidents()
    {
        await using var db = Db();
        foreach (var state in new[] { "OPEN", "IN_PROGRESS", "RESOLVED", "CLOSED", "CANCELLED" })
        {
            var i = new Incident { BranchId = branch, IncidentNo = prefix + state, IncidentTitle = prefix, Description = prefix, Status = state, ReportedBy = actor, ReportedAt = DateTime.UtcNow, DiscoveredAt = DateTime.UtcNow }; db.Incidents.Add(i); await db.SaveChangesAsync(); incidents.Add(i.IncidentId);
            if (state is "CLOSED" or "CANCELLED") await Bad("POST", "material-requests", Write() with { IncidentId = i.IncidentId });
            else { var r = await Create(Write() with { IncidentId = i.IncidentId }); Check(r.IncidentId == i.IncidentId, "Incident supported state"); }
        }
        var cross = new Incident { BranchId = otherBranch, IncidentNo = prefix + "CROSS", IncidentTitle = prefix, Description = prefix, Status = "OPEN", ReportedBy = actor, ReportedAt = DateTime.UtcNow, DiscoveredAt = DateTime.UtcNow }; db.Incidents.Add(cross); await db.SaveChangesAsync(); incidents.Add(cross.IncidentId); await Bad("POST", "material-requests", Write() with { IncidentId = cross.IncidentId });
        var room = await db.Rooms.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.RoomId).FirstAsync(); var area = await db.Areas.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.AreaId).FirstAsync();
        await Create(Write() with { ReceivingRoomId = room }); await Create(Write() with { ReceivingAreaId = area }); await Bad("POST", "material-requests", Write() with { ReceivingRoomId = room, ReceivingAreaId = area });
        var foreignRoom = await db.Rooms.Where(x => x.BranchId == otherBranch && x.IsActive).Select(x => x.RoomId).FirstAsync(); await Bad("POST", "material-requests", Write() with { ReceivingRoomId = foreignRoom });
    }
    async Task Metadata()
    {
        await using var db = Db(); var prop = db.Model.FindEntityType(typeof(MaterialRequest))!.FindProperty("RowVersion")!;
        Check(prop.IsConcurrencyToken && prop.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, "Rowversion EF metadata");
        var audits = await db.AuditLogs.Where(x => x.NewValues != null && x.NewValues.Contains(prefix)).ToListAsync();
        Check(audits.Count > 0 && audits.All(x => new[] { "CREATE", "UPDATE", "APPROVE", "REJECT", "CANCEL" }.Contains(x.Action)), "Audit actions");
        foreach (var a in audits) { using var parsed = JsonDocument.Parse(a.NewValues!); Check(a.ActorUserId != null, "Audit actor"); }
        var fulfilled = await db.MaterialFulfillments.AsNoTracking().Include(x => x.MaterialFulfillmentLines).Where(x => x.Request.Purpose.StartsWith(prefix)).ToListAsync();
        foreach (var batch in fulfilled)
        {
            var docId = batch.StockDocumentId!.Value.ToString(); var batchId = batch.FulfillmentId.ToString();
            var stockAudit = await db.AuditLogs.Where(x => x.EntityType == "stock_documents" && x.EntityId == docId).ToArrayAsync();
            var fulfillmentAudit = await db.AuditLogs.Where(x => x.EntityType == "material_fulfillments" && x.EntityId == batchId).ToArrayAsync();
            Check(stockAudit.Length == 1 && fulfillmentAudit.Length == 1 && stockAudit[0].ActorUserId == issuer && fulfillmentAudit[0].ActorUserId == issuer && stockAudit[0].BranchId == branch && fulfillmentAudit[0].BranchId == branch, "Atomic Inventory/fulfillment audit identity/scope");
            Check(batch.MaterialFulfillmentLines.All(x => x.StockDocumentLineId != null), "All new batches have durable line mapping");
        }
        var legacy = await db.MaterialRequests.Where(x => !x.Purpose.StartsWith(prefix)).Select(x => x.RequestId).FirstAsync(); var r = await Get(legacy); Check(!r.IsNewWorkflow, "Legacy read"); await Bad("POST", $"material-requests/{legacy}/cancel", new MaterialCommand(actor, r.RowVersion, prefix));
    }
    async Task Cleanup()
    {
        await using var db = Db();
        // All destructive cleanup is constrained to this run's unique prefix/owned IDs.
        var ids = await db.MaterialRequests.Where(x => x.Purpose.StartsWith(prefix)).Select(x => x.RequestId).ToArrayAsync();
        var fs = await db.MaterialFulfillments.Where(x => ids.Contains(x.RequestId)).Select(x => x.FulfillmentId).ToArrayAsync();
        var docs = await db.StockDocuments.Where(x => x.StockDocumentLines.Any(l => itemIds.Contains(l.ItemId))).Select(x => x.DocumentId).ToArrayAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.MaterialRequestApprovals.Where(x => ids.Contains(x.RequestId)).ExecuteDeleteAsync();
        await db.MaterialRequestApprovalLineSnapshots.Where(x => ids.Contains(x.ApprovalCycle.RequestId)).ExecuteDeleteAsync();
        await db.MaterialRequestApprovalCycles.Where(x => ids.Contains(x.RequestId)).ExecuteDeleteAsync();
        await db.MaterialFulfillmentLines.Where(x => fs.Contains(x.FulfillmentId)).ExecuteDeleteAsync(); await db.MaterialFulfillments.Where(x => fs.Contains(x.FulfillmentId)).ExecuteDeleteAsync();
        await db.MaterialRequestLines.Where(x => ids.Contains(x.RequestId)).ExecuteDeleteAsync(); await db.MaterialRequests.Where(x => ids.Contains(x.RequestId)).ExecuteDeleteAsync();
        await db.StockMovements.Where(x => docs.Contains(x.DocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockReturnLineDetails.Where(x => docs.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockReceiptLineDetails.Where(x => docs.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync();
        await db.StockDocumentLines.Where(x => docs.Contains(x.DocumentId)).ExecuteDeleteAsync(); await db.StockDocuments.Where(x => docs.Contains(x.DocumentId)).ExecuteDeleteAsync();
        await db.UomConversions.Where(x => x.ItemId != null && itemIds.Contains(x.ItemId.Value)).ExecuteDeleteAsync();
        await db.InventoryItems.Where(x => itemIds.Contains(x.ItemId)).ExecuteDeleteAsync(); await db.Warehouses.Where(x => warehouseIds.Contains(x.WarehouseId)).ExecuteDeleteAsync(); await db.UnitsOfMeasures.Where(x => unitIds.Contains(x.UomId)).ExecuteDeleteAsync();
        await db.Incidents.Where(x => incidents.Contains(x.IncidentId)).ExecuteDeleteAsync();
        var idStrings = ids.Select(x => x.ToString()).ToArray(); var fStrings = fs.Select(x => x.ToString()).ToArray(); var dStrings = docs.Select(x => x.ToString()).ToArray();
        await db.AuditLogs.Where(x => x.NewValues != null && x.NewValues.Contains(prefix) || x.EntityType == "material_requests" && idStrings.Contains(x.EntityId) || x.EntityType == "material_fulfillments" && fStrings.Contains(x.EntityId) || x.EntityType == "stock_documents" && dStrings.Contains(x.EntityId)).ExecuteDeleteAsync();
        await tx.CommitAsync(); Console.WriteLine("Fixture cleanup completed.");
    }
}
