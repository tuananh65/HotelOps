using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;

sealed partial class IntegritySuite
{
    readonly List<long> materialItems = [], materialUnits = [], materialWarehouses = [];
    InventoryUnitDto usageUnit = null!, usageBox = null!;
    InventoryItemDto usageItem = null!, usageItem2 = null!;
    InventoryWarehouseDto usageWarehouse = null!;
    long usageDepartment, materialCategory;
    sealed record IssuedSource(long Incident, long Work, MaterialRequestDto Request, MaterialFulfillmentDto Fulfillment, InventoryDocumentDto Document);
    MaintenanceWorkService Maintenance(HotelOpsDbContext d) => new(d, new InventoryService(d));
    CreateMaintenanceWorkLogRequest UsageInput(long line, decimal qty, long? uom = null) => new() { ActorUserId = actor, WorkNote = prefix, SpentMinutes = 1, MaterialUsages = [new(line, uom ?? usageUnit.UomId, qty)] };
    Task<MaintenanceWorkLogDto> Use(IssuedSource s, decimal qty, long? uom = null) => Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{s.Work}/logs", UsageInput(s.Document.Lines[0].LineId, qty, uom), 201);
    Task<MaintenanceMaterialAvailability> Available(IssuedSource s) => Send<MaintenanceMaterialAvailability>("GET", $"maintenance-works/{s.Work}/material-availability");
    async Task<IssuedSource> IssueSource(decimal quantity = 10, bool boxed = false, bool multi = false, IssuedSource? sameWork = null)
    {
        (long Incident, long Work) p = sameWork is null ? await Prepare("IN_PROGRESS") : (sameWork.Incident, sameWork.Work);
        var r = await Send<MaterialRequestDto>("POST", "material-requests", new MaterialRequestWrite(actor, branch, usageDepartment, usageWarehouse.WarehouseId, prefix, DateTime.UtcNow, null, null, null, p.Incident,
            multi ? [new(null, 1, usageItem.ItemId, boxed ? usageBox.UomId : usageUnit.UomId, quantity, null), new(null, 2, usageItem2.ItemId, usageUnit.UomId, quantity, null)] : [new(null, 1, usageItem.ItemId, boxed ? usageBox.UomId : usageUnit.UomId, quantity, null)], true, verifier), 201);
        r = await Send<MaterialRequestDto>("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(verifier, r.RowVersion, r.Lines.Select(x => new MaterialApprovalLine(x.RequestLineId, x.RequestedQty)).ToArray()));
        var f = await Send<MaterialFulfillmentDto>("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(actor, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)), 201);
        var document = await Send<InventoryDocumentDto>("GET", $"inventory/documents/{f.StockDocumentId}");
        return new(p.Incident, p.Work, r, f, document);
    }
    async Task<InventoryDocumentDto> ReturnDraft(IssuedSource s, decimal quantity)
        => await Send<InventoryDocumentDto>("POST", "inventory/returns", new InventoryDraft(actor, null, usageWarehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), [new(s.Document.Lines[0].ItemId, s.Document.Lines[0].UomId, quantity, s.Document.Lines[0].LineId)], Note: prefix), 201);
    Task<InventoryDocumentDto> ReturnPost(InventoryDocumentDto d, decimal usable, decimal damaged = 0) => Send<InventoryDocumentDto>("POST", $"inventory/returns/{d.DocumentId}/post", new InventoryPost(actor, d.RowVersion, [new(d.Lines[0].LineId, usable, DamagedQty: damaged, DiscrepancyReason: prefix)]));
    async Task RejectUsage(IssuedSource s, CreateMaintenanceWorkLogRequest input)
    {
        var before = await Footprint(); await Bad("POST", $"maintenance-works/{s.Work}/logs", input); Check(before == await Footprint(), "Usage validation has no business/stock/history effects");
    }
    async Task MaterialUsage()
    {
        await using (var d = Db()) usageDepartment = await d.Departments.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.DepartmentId).FirstAsync();
        usageUnit = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(actor, prefix + "U", prefix), 201); materialUnits.Add(usageUnit.UomId);
        usageBox = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(actor, prefix + "B", prefix), 201); materialUnits.Add(usageBox.UomId);
        long category; await using (var d = Db()) { var c = new HotelOps.Api.Entities.ItemCategory { CategoryCode = prefix, CategoryName = prefix, IsActive = true }; d.ItemCategories.Add(c); await d.SaveChangesAsync(); category = materialCategory = c.ItemCategoryId; }
        usageItem = await Send<InventoryItemDto>("POST", "inventory/items", new InventoryItemWrite(actor, prefix + "I", prefix, category, usageUnit.UomId, true), 201); materialItems.Add(usageItem.ItemId);
        usageItem2 = await Send<InventoryItemDto>("POST", "inventory/items", new InventoryItemWrite(actor, prefix + "I2", prefix, category, usageUnit.UomId, true), 201); materialItems.Add(usageItem2.ItemId);
        usageWarehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(actor, branch, usageDepartment, prefix + "W", prefix), 201); materialWarehouses.Add(usageWarehouse.WarehouseId);
        await Send<InventoryConversionDto>("POST", $"inventory/items/{usageItem.ItemId}/conversions", new InventoryConversionWrite(actor, usageBox.UomId, 2), 201);
        await Send<InventoryDocumentDto>("POST", "inventory/adjustments", new InventoryAdjustment(actor, usageWarehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(usageItem.ItemId, 10000), new(usageItem2.ItemId, 10000)]), 201);
        var source = await IssueSource(10, true); var capacity = await Available(source); Check(capacity.Items.Single().IssuedBaseQty == 20 && capacity.Items[0].AllowedUoms.Count == 2, "Historical source availability");
        int movements, documents; decimal onHand;
        await using (var d = Db()) { movements = await d.StockMovements.CountAsync(); documents = await d.StockDocuments.CountAsync(); onHand = await d.StockMovements.Where(x => x.ItemId == usageItem.ItemId).SumAsync(x => x.SignedBaseQty); }
        var history = await HistoryCounts(source.Incident);
        var log = await Use(source, 3); Check(log.MaterialIssueId == null && log.MaterialUsages.Single().UsedBaseQty == 3, "Base UOM usage and null compatibility header");
        Check(history == await HistoryCounts(source.Incident), "Material log creates no unchanged Work/Incident status history");
        await using (var d = Db()) await d.UomConversions.Where(x => x.ItemId == usageItem.ItemId && x.FromUomId == usageBox.UomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConversionFactor, 9));
        log = await Use(source, 2, usageBox.UomId); Check(log.MaterialUsages.Single().ConversionFactor == 2 && log.MaterialUsages[0].UsedBaseQty == 4, "ISSUE snapshot survives conversion-master change");
        await using (var d = Db()) { Check(await d.StockMovements.CountAsync() == movements && await d.StockDocuments.CountAsync() == documents && await d.StockMovements.Where(x => x.ItemId == usageItem.ItemId).SumAsync(x => x.SignedBaseQty) == onHand, "Usage creates no document/movement/stock effect"); await d.UomConversions.Where(x => x.ItemId == usageItem.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConversionFactor, 2)); }
        var detail = await Send<MaintenanceWorkDetailDto>("GET", $"maintenance-works/{source.Work}"); Check(detail.MaintenanceWorkLogs.SelectMany(x => x.MaterialUsages).Sum(x => x.UsedBaseQty) == 7, "Usage history projection");
        Check((await Available(source)).Items.Single().RemainingBaseQty == 13, "Cumulative remaining");
        foreach (var q in new[] { 0m, -1m, .0001m, 1000000000000000m }) await RejectUsage(source, UsageInput(source.Document.Lines[0].LineId, q));
        await RejectUsage(source, UsageInput(source.Document.Lines[0].LineId, 1, long.MaxValue));
        await RejectUsage(source, new() { ActorUserId = actor, WorkNote = prefix, MaterialUsages = [new(source.Document.Lines[0].LineId, usageUnit.UomId, 1), new(source.Document.Lines[0].LineId, usageBox.UomId, 1)] });
        // Same HTTP payload is a fresh incremental POST, not an invented idempotency contract.
        await Use(source, 1); await Use(source, 1); Check((await Available(source)).Items.Single().ConsumedBaseQty == 9, "Non-idempotent retry within cap");
        var unrelated = await IssueSource(); await RejectUsage(source, UsageInput(unrelated.Document.Lines[0].LineId, 1));
        var ret = await ReturnDraft(source, 1); await ReturnPost(ret, 1); // 2 base physical return.
        Check((await Available(source)).Items.Single().ReturnedBaseQty == 2, "Posted RETURN capacity");
        await Use(source, 9); Check((await Available(source)).Total == 0, "Consumed plus returned exactly issued"); await RejectUsage(source, UsageInput(source.Document.Lines[0].LineId, 1));
        var split = await IssueSource(5); var earlyDraft = await ReturnDraft(split, 5); await Use(split, 3);
        var before = await Footprint(); await Bad("POST", $"inventory/returns/{earlyDraft.DocumentId}/post", new InventoryPost(actor, earlyDraft.RowVersion, [new(earlyDraft.Lines[0].LineId, 5)])); Check(before == await Footprint(), "RETURN posting rechecks consumed, failed post atomic");
        await Bad("POST", "inventory/returns", new InventoryDraft(actor, null, usageWarehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), [new(usageItem.ItemId, usageUnit.UomId, 3, split.Document.Lines[0].LineId)]));
        ret = await ReturnDraft(split, 2); await ReturnPost(ret, 1, 1); Check((await Available(split)).Total == 0, "Usable plus damaged physical return counts");
        var closed = await IssueSource(); await Use(closed, 4); await Command(closed.Work, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix }); await Command(closed.Work, "verify", new { ActorUserId = verifier, Note = prefix });
        await using (var d = Db()) { await using var tx = await d.Database.BeginTransactionAsync(); var inv = new InventoryService(d); await inv.AcquireCatalog(default); Check((await inv.Capacity(closed.Document.Lines[0].LineId, null, default)).ConsumedBaseQty == 4, "Closed work still consumed"); await tx.RollbackAsync(); }
        var cancelled = await IssueSource(); await Use(cancelled, 4); await Command(cancelled.Work, "cancel", new { ActorUserId = verifier, Reason = prefix });
        await using (var d = Db()) { await using var tx = await d.Database.BeginTransactionAsync(); var inv = new InventoryService(d); await inv.AcquireCatalog(default); Check((await inv.Capacity(cancelled.Document.Lines[0].LineId, null, default)).ConsumedBaseQty == 4, "Cancelled work still consumed"); await tx.RollbackAsync(); }
        var multi = await IssueSource(5, multi: true); log = await Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{multi.Work}/logs", new CreateMaintenanceWorkLogRequest { ActorUserId = actor, WorkNote = prefix, MaterialUsages = multi.Document.Lines.Select(x => new MaintenanceMaterialUsageWrite(x.LineId, usageUnit.UomId, 2)).Reverse().ToArray() }, 201); Check(log.MaterialUsages.Count == 2, "Multiple source lines atomic log");
        foreach (var body in new object[] { new { ActorUserId = actor, WorkNote = prefix }, new CreateMaintenanceWorkLogRequest { ActorUserId = actor, WorkNote = prefix, MaterialUsages = null }, new CreateMaintenanceWorkLogRequest { ActorUserId = actor, WorkNote = prefix, MaterialUsages = [] } })
        { log = await Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{multi.Work}/logs", body, 201); Check(log.MaterialIssueId == null && log.MaterialUsages.Count == 0, "Omitted/null/empty plain log compatibility"); }
        await Send<object>("GET", "maintenance-works/9223372036854775807/material-availability", status: 404);
        await Send<object>("GET", $"maintenance-works/{multi.Work}/material-availability?pageSize=101", status: 400);
        await MaterialLineageCases(); await MaterialAudit();
    }
    async Task MaterialLineageCases()
    {
        var source = await IssueSource(); var id = source.Document.Lines[0].LineId;
        // Each mutation is limited to owned data and reverted, even when an assertion fails.
        await using var d = Db();
        var otherBranch = await d.Branches.Where(x => x.BranchId != branch).Select(x => x.BranchId).FirstAsync();
        await d.MaterialRequests.Where(x => x.RequestId == source.Request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.BranchId, otherBranch));
        try { await RejectUsage(source, UsageInput(id, 1)); Check((await Available(source)).Total == 0, "Unrelated branch excluded"); }
        finally { await d.MaterialRequests.Where(x => x.RequestId == source.Request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.BranchId, branch)); }
        var wrong = await IssueSource();
        var ambiguousBlocked = false;
        try { await d.MaterialFulfillments.Where(x => x.FulfillmentId == wrong.Fulfillment.FulfillmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StockDocumentId, source.Document.DocumentId)); }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number is 2601 or 2627) { ambiguousBlocked = true; }
        Check(ambiguousBlocked, "SQL unique ISSUE owner prevents ambiguous fulfillment headers");
        ambiguousBlocked = false;
        try { await d.MaterialFulfillmentLines.Where(x => x.FulfillmentId == wrong.Fulfillment.FulfillmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StockDocumentLineId, id)); }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number is 2601 or 2627) { ambiguousBlocked = true; }
        Check(ambiguousBlocked, "SQL unique ISSUE line prevents duplicate durable correlations");
        var fulfillmentLine = source.Fulfillment.Lines.Single().FulfillmentLineId;
        await d.MaterialFulfillmentLines.Where(x => x.FulfillmentLineId == fulfillmentLine).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequestLineId, wrong.Request.Lines.Single().RequestLineId));
        try { await RejectUsage(source, UsageInput(id, 1)); }
        finally { await d.MaterialFulfillmentLines.Where(x => x.FulfillmentLineId == fulfillmentLine).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequestLineId, source.Request.Lines.Single().RequestLineId)); }
        await d.StockDocuments.Where(x => x.DocumentId == source.Document.DocumentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "REVERSED"));
        try { await RejectUsage(source, UsageInput(id, 1)); }
        finally { await d.StockDocuments.Where(x => x.DocumentId == source.Document.DocumentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "POSTED")); }
        var evidence = await d.MaterialRequestApprovals.Where(x => x.RequestId == source.Request.RequestId).Select(x => x.ApprovalId).SingleAsync();
        await d.MaterialRequestApprovals.Where(x => x.ApprovalId == evidence).ExecuteUpdateAsync(s => s.SetProperty(x => x.Decision, "REJECTED"));
        try { await RejectUsage(source, UsageInput(id, 1)); Check((await Available(source)).Total == 0, "Missing approval candidate excluded"); }
        finally { await d.MaterialRequestApprovals.Where(x => x.ApprovalId == evidence).ExecuteUpdateAsync(s => s.SetProperty(x => x.Decision, "APPROVED")); }
        await d.StockDocumentLines.Where(x => x.LineId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.BaseQty, 9));
        try { await RejectUsage(source, UsageInput(id, 1)); }
        finally { await d.StockDocumentLines.Where(x => x.LineId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.BaseQty, 10)); }
        await d.MaterialFulfillments.Where(x => x.FulfillmentId == source.Fulfillment.FulfillmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StockDocumentId, (long?)null));
        try { await RejectUsage(source, UsageInput(id, 1)); }
        finally { await d.MaterialFulfillments.Where(x => x.FulfillmentId == source.Fulfillment.FulfillmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StockDocumentId, source.Document.DocumentId)); }
        var legacy = await IssueSource(); await d.StockDocuments.Where(x => x.DocumentId == legacy.Document.DocumentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReferenceType, "LEGACY_TEST"));
        await RejectUsage(legacy, UsageInput(legacy.Document.Lines[0].LineId, 1)); var ret = await ReturnDraft(legacy, 1); await ReturnPost(ret, 1); Check(true, "Legacy-valid RETURN does not require strict FE09 provenance");
        var nonIssue = await Send<InventoryDocumentDto>("POST", "inventory/receipts", new InventoryDraft(actor, null, usageWarehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), [new(usageItem.ItemId, usageUnit.UomId, 1)], Note: prefix), 201);
        await RejectUsage(source, UsageInput(nonIssue.Lines[0].LineId, 1));
        await d.InventoryItems.Where(x => x.ItemId == usageItem.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await d.UnitsOfMeasures.Where(x => x.UomId == usageUnit.UomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await d.Warehouses.Where(x => x.WarehouseId == usageWarehouse.WarehouseId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await d.ItemCategories.Where(x => x.ItemCategoryId == materialCategory).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        try { var log = await Use(source, 1); Check(log.MaterialUsages.Single().UsedBaseQty == 1, "Inactive historical masters do not invalidate issuance"); }
        finally
        {
            await d.InventoryItems.Where(x => x.ItemId == usageItem.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
            await d.UnitsOfMeasures.Where(x => x.UomId == usageUnit.UomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
            await d.Warehouses.Where(x => x.WarehouseId == usageWarehouse.WarehouseId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
            await d.ItemCategories.Where(x => x.ItemCategoryId == materialCategory).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
        }
        // SQL UQ itself rejects duplicate persisted log/source allocation.
        var row = await d.MaintenanceMaterialUsages.AsNoTracking().FirstAsync(x => x.MaintenanceWorkId == source.Work); var duplicate = false;
        try { d.MaintenanceMaterialUsages.Add(new() { MaintenanceWorkId = row.MaintenanceWorkId, MaintenanceWorkLogId = row.MaintenanceWorkLogId, StockDocumentLineId = row.StockDocumentLineId, ItemId = row.ItemId, BaseUomId = row.BaseUomId, UomId = row.UomId, UsedQty = 1, ConversionFactor = 1, UsedBaseQty = 1 }); await d.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627) { duplicate = true; d.ChangeTracker.Clear(); }
        Check(duplicate, "SQL duplicate allocation blocked");
        var plain = await Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{source.Work}/logs", new { ActorUserId = actor, WorkNote = prefix }, 201);
        foreach (var violation in new[] { "exact-product", "base-factor", "composite-owner" })
        {
            await using var invalid = Db();
            invalid.MaintenanceMaterialUsages.Add(new()
            {
                MaintenanceWorkId = violation == "composite-owner" ? wrong.Work : source.Work,
                MaintenanceWorkLogId = plain.LogId, StockDocumentLineId = id, ItemId = usageItem.ItemId, BaseUomId = usageUnit.UomId,
                UomId = violation == "exact-product" ? usageBox.UomId : usageUnit.UomId,
                UsedQty = violation == "exact-product" ? .001m : 1, ConversionFactor = violation == "exact-product" ? .000001m : violation == "base-factor" ? 2 : 1,
                UsedBaseQty = violation == "base-factor" ? 2 : violation == "exact-product" ? .001m : 1
            });
            var blocked = false;
            try { await invalid.SaveChangesAsync(); }
            catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number == 547) { blocked = true; }
            Check(blocked, "SQL constraint rejects " + violation);
        }
        await d.UomConversions.Where(x => x.ItemId == usageItem.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConversionFactor, .000001m));
        try
        {
            var precise = await IssueSource(1000, true); var line = precise.Document.Lines[0].LineId;
            Check((await Available(precise)).Items.Single().IssuedBaseQty == .001m, "Valid exact historical tiny factor");
            await RejectUsage(precise, UsageInput(line, .001m, usageBox.UomId));
            Check((await Use(precise, .001m)).MaterialUsages.Single().UsedBaseQty == .001m, "Exact base usage survives original-UOM rounding rejection");
        }
        finally { await d.UomConversions.Where(x => x.ItemId == usageItem.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConversionFactor, 2)); }
        var restored = await IssueSource(); var returned = await ReturnPost(await ReturnDraft(restored, 2), 2);
        await Send<InventoryDocumentDto>("POST", $"inventory/documents/{returned.DocumentId}/reverse", new InventoryCommand(actor, returned.RowVersion, prefix));
        Check((await Available(restored)).Items.Single().ReturnedBaseQty == 0, "Reversed RETURN no longer consumes capacity");
        await Use(restored, 10);
    }
    async Task MaterialAudit()
    {
        await using var d = Db(); var ids = await d.MaintenanceMaterialUsages.Where(x => owned.Contains(x.MaintenanceWork.IncidentId!.Value)).Select(x => x.UsageId.ToString()).ToArrayAsync();
        var audits = await d.AuditLogs.Where(x => x.EntityType == "maintenance_material_usages" && ids.Contains(x.EntityId)).ToArrayAsync(); Check(audits.Length == ids.Length && audits.Length > 0, "One CREATE audit per allocation");
        foreach (var a in audits)
        {
            Check(a.Action == "CREATE" && a.OldValues == null && a.ActorUserId == actor && a.BranchId == branch, "Usage audit action/actor/branch");
            using var json = JsonDocument.Parse(a.NewValues!); var value = json.RootElement;
            Check(value.GetProperty("stock_document_line_id").GetInt64() > 0 && value.GetProperty("request_id").GetInt64() > 0 && value.GetProperty("capacity_after").GetProperty("RemainingBaseQty").GetDecimal() >= 0, "Audit lineage/snapshots/capacity");
        }
    }
    async Task CleanupMaterialFixtures(HotelOpsDbContext d, long[] incidents)
    {
        var requests = await d.MaterialRequests.Where(x => x.Purpose.StartsWith(prefix) && x.IncidentId != null && incidents.Contains(x.IncidentId.Value)).Select(x => x.RequestId).ToArrayAsync();
        var fulfillments = await d.MaterialFulfillments.Where(x => requests.Contains(x.RequestId)).Select(x => x.FulfillmentId).ToArrayAsync();
        var documents = await d.StockDocuments.Where(x => x.StockDocumentLines.Any(l => materialItems.Contains(l.ItemId))).Select(x => x.DocumentId).ToArrayAsync();
        var replenishments = await d.MaterialReplenishments.Where(x => materialWarehouses.Contains(x.WarehouseId)).Select(x => x.ReplenishmentId).ToArrayAsync(); var rp = replenishments.Select(x => x.ToString()).ToArray();
        await d.AuditLogs.Where(x => x.EntityType == "material_replenishments" && rp.Contains(x.EntityId)).ExecuteDeleteAsync();
        await d.MaterialReplenishmentReceiptLines.Where(x => replenishments.Contains(x.ReplenishmentLine.ReplenishmentId)).ExecuteDeleteAsync(); await d.MaterialReplenishmentLines.Where(x => replenishments.Contains(x.ReplenishmentId)).ExecuteDeleteAsync(); await d.MaterialReplenishments.Where(x => replenishments.Contains(x.ReplenishmentId)).ExecuteDeleteAsync();
        var rr = requests.Select(x => x.ToString()).ToArray(); var ff = fulfillments.Select(x => x.ToString()).ToArray(); var dd = documents.Select(x => x.ToString()).ToArray();
        var conversions = await d.UomConversions.Where(x => x.ItemId != null && materialItems.Contains(x.ItemId.Value)).Select(x => x.ConversionId.ToString()).ToArrayAsync();
        var thresholds = await d.StockThresholds.Where(x => materialWarehouses.Contains(x.WarehouseId)).Select(x => x.ThresholdId.ToString()).ToArrayAsync();
        await d.AuditLogs.Where(x => x.EntityType == "uom_conversions" && conversions.Contains(x.EntityId) || x.EntityType == "stock_thresholds" && thresholds.Contains(x.EntityId)).ExecuteDeleteAsync();
        await d.AuditLogs.Where(x => x.EntityType == "material_requests" && rr.Contains(x.EntityId) || x.EntityType == "material_fulfillments" && ff.Contains(x.EntityId) || x.EntityType == "stock_documents" && dd.Contains(x.EntityId) || x.NewValues != null && x.NewValues.Contains(prefix)).ExecuteDeleteAsync();
        await d.MaterialRequestApprovals.Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync(); await d.MaterialRequestApprovalLineSnapshots.Where(x => requests.Contains(x.ApprovalCycle.RequestId)).ExecuteDeleteAsync(); await d.MaterialRequestApprovalCycles.Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync();
        await d.MaterialFulfillmentLines.Where(x => fulfillments.Contains(x.FulfillmentId)).ExecuteDeleteAsync(); await d.MaterialFulfillments.Where(x => fulfillments.Contains(x.FulfillmentId)).ExecuteDeleteAsync(); await d.MaterialRequestLines.Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync(); await d.MaterialRequests.Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync();
        await d.StockMovements.Where(x => documents.Contains(x.DocumentLine.DocumentId)).ExecuteDeleteAsync(); await d.StockReceiptLineDetails.Where(x => documents.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync(); await d.StockReturnLineDetails.Where(x => documents.Contains(x.StockDocumentLine.DocumentId)).ExecuteDeleteAsync(); await d.StockDocumentLines.Where(x => documents.Contains(x.DocumentId)).ExecuteDeleteAsync(); await d.StockDocuments.Where(x => documents.Contains(x.DocumentId)).ExecuteDeleteAsync();
        await d.StockThresholds.Where(x => materialWarehouses.Contains(x.WarehouseId)).ExecuteDeleteAsync(); await d.UomConversions.Where(x => x.ItemId != null && materialItems.Contains(x.ItemId.Value)).ExecuteDeleteAsync(); await d.InventoryItems.Where(x => materialItems.Contains(x.ItemId)).ExecuteDeleteAsync(); await d.Warehouses.Where(x => materialWarehouses.Contains(x.WarehouseId)).ExecuteDeleteAsync(); await d.UnitsOfMeasures.Where(x => materialUnits.Contains(x.UomId)).ExecuteDeleteAsync();
        await d.ItemCategories.Where(x => x.ItemCategoryId == materialCategory && x.CategoryCode == prefix).ExecuteDeleteAsync();
    }
}
