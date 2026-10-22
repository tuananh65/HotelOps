using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

sealed partial class MaterialSuite
{
    async Task ValidationAndNumbering()
    {
        await Bad("POST", "material-requests", Write(1, true) with { NeededAt = null });
        var r = await Create(Write(5, true));
        foreach (var value in new[] { -1m, 6m, .0001m, 1000000000000000m }) await Bad("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(approver, r.RowVersion, [new(r.Lines[0].RequestLineId, value)]));
        await Bad("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(actor, r.RowVersion, [new(r.Lines[0].RequestLineId, 5)]));
        await Bad("POST", $"material-requests/{r.RequestId}/approve", new MaterialApproval(approver, r.RowVersion, [new(r.Lines[0].RequestLineId, 0)]));
        await Bad("POST", $"material-requests/{r.RequestId}/request-information", new MaterialCommand(approver, r.RowVersion, "short"));
        await Bad("POST", $"material-requests/{r.RequestId}/reject", new MaterialCommand(approver, r.RowVersion, "short"));
        var rejected = await Cmd(r, "reject", approver, prefix); Check(rejected.RawStatus == "REJECTED", "Explicit rejection");
        r = await Approved(5);
        await Bad("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(approver, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)));
        foreach (var entries in new MaterialIssueLine[][] { [], [new(r.Lines[0].RequestLineId, 0)], [new(r.Lines[0].RequestLineId, 6)], [new(-1, 1)], [new(r.Lines[0].RequestLineId, 1), new(r.Lines[0].RequestLineId, 1)] })
            await Bad("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(issuer, r.RowVersion, "Partial", DateOnly.FromDateTime(DateTime.UtcNow), entries));
        await Bad("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(issuer, "bad", "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)));
        await Bad("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(issuer, r.RowVersion, "FullRemaining", default));
        await Bad("POST", "material-requests", Write() with { ActorUserId = -1 });
        await Bad("POST", "material-requests", Write() with { DepartmentId = -1 });
        await Bad("POST", "material-requests", Write() with { Lines = [new(null, 1, item.ItemId, unit.UomId, 1, null), new(null, 1, item.ItemId, unit.UomId, 1, null)] });
        await using (var db = Db())
        {
            var inactive = new User { EmployeeCode = prefix, Username = prefix, Email = prefix + "@example.invalid", FullName = prefix,
                PrimaryBranchId = branch, EmploymentStatus = "ACTIVE", AccountStatus = "INACTIVE", CreatedAt = DateTime.UtcNow };
            db.Users.Add(inactive); await db.SaveChangesAsync();
            try
            {
                await Bad("POST", "material-requests", Write(1, true) with { AssignedApproverUserId = inactive.UserId });
                inactive.AccountStatus = "ACTIVE"; inactive.EmploymentStatus = "UNKNOWN"; await db.SaveChangesAsync();
                await Bad("POST", "material-requests", Write() with { ActorUserId = inactive.UserId });
                inactive.EmploymentStatus = "ACTIVE"; inactive.DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync();
                await Bad("POST", "material-requests", Write(1, true) with { AssignedApproverUserId = inactive.UserId });
            }
            finally { db.Users.Remove(inactive); await db.SaveChangesAsync(); }
            var foreignUser = await db.Users.Where(x => x.PrimaryBranchId == otherBranch && !x.UserBranchAccessUsers.Any(a => a.BranchId == branch && a.RevokedAt == null)).Select(x => x.UserId).FirstAsync();
            await Bad("POST", "material-requests", Write(2, true) with { AssignedApproverUserId = foreignUser });
            var foreignWarehouse = await db.Warehouses.Where(x => x.BranchId == otherBranch && x.IsActive).Select(x => x.WarehouseId).FirstAsync();
            await Bad("POST", "material-requests", Write() with { SourceWarehouseId = foreignWarehouse });
            var foreignArea = await db.Areas.Where(x => x.BranchId == otherBranch && x.IsActive).Select(x => x.AreaId).FirstAsync();
            await Bad("POST", "material-requests", Write() with { ReceivingAreaId = foreignArea });
            var template = await db.Rooms.AsNoTracking().FirstAsync(x => x.BranchId == branch && x.IsActive);
            var inactiveRoom = new Room { BranchId = branch, FloorId = template.FloorId, RoomTypeId = template.RoomTypeId, RoomNumber = Guid.NewGuid().ToString("N")[..8], OperationalStatus = template.OperationalStatus, IsActive = false };
            db.Rooms.Add(inactiveRoom); await db.SaveChangesAsync();
            try { await Bad("POST", "material-requests", Write() with { ReceivingRoomId = inactiveRoom.RoomId }); }
            finally { db.Rooms.Remove(inactiveRoom); await db.SaveChangesAsync(); }
        }
        // Mutate only owned fixture masters, restoring them in finally.
        await using (var db = Db())
        {
            await db.InventoryItems.Where(x => x.ItemId == item.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            try { await Bad("POST", "material-requests", Write()); await Bad("POST", $"material-requests/{r.RequestId}/fulfillments", new MaterialIssue(issuer, r.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow))); }
            finally { await db.InventoryItems.Where(x => x.ItemId == item.ItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true)); }
            await db.Warehouses.Where(x => x.WarehouseId == warehouse.WarehouseId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            try { await Bad("POST", "material-requests", Write()); }
            finally { await db.Warehouses.Where(x => x.WarehouseId == warehouse.WarehouseId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true)); }
            await db.UnitsOfMeasures.Where(x => x.UomId == box.UomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            try { await Bad("POST", "material-requests", Write() with { Lines = [new(null, 1, item.ItemId, box.UomId, 1, null)] }); }
            finally { await db.UnitsOfMeasures.Where(x => x.UomId == box.UomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true)); }
        }
        var chainUnit = await Send<InventoryUnitDto>("POST", "inventory/units", new InventoryUnitWrite(actor, prefix + "C", prefix), 201); unitIds.Add(chainUnit.UomId);
        await using (var db = Db())
        {
            var chain = new UomConversion { ItemId = item.ItemId, FromUomId = chainUnit.UomId, ToUomId = box.UomId, ConversionFactor = 2, IsActive = true };
            db.UomConversions.Add(chain); await db.SaveChangesAsync();
            await Bad("POST", "material-requests", Write() with { Lines = [new(null, 1, item.ItemId, chainUnit.UomId, 1, null)] });
        }
        var converted = await Approved(2);
        var convDraft = await Create(Write(2, true) with { Lines = [new(null, 1, item.ItemId, box.UomId, 2, null)] }); convDraft = await Approve(convDraft);
        await using (var db = Db())
        {
            var conversion = await db.UomConversions.SingleAsync(x => x.ItemId == item.ItemId && x.FromUomId == box.UomId);
            var original = conversion.ConversionFactor;
            conversion.IsActive = false; await db.SaveChangesAsync();
            var global = new UomConversion { ItemId = null, FromUomId = box.UomId, ToUomId = unit.UomId, ConversionFactor = 2, IsActive = true }; db.UomConversions.Add(global); await db.SaveChangesAsync();
            try
            {
                await Bad("POST", "material-requests", Write() with { Lines = [new(null, 1, item.ItemId, box.UomId, 1, null)] });
                await Bad("POST", $"material-requests/{convDraft.RequestId}/fulfillments", new MaterialIssue(issuer, convDraft.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow)));
            }
            finally { db.UomConversions.Remove(global); conversion.IsActive = true; await db.SaveChangesAsync(); }
            conversion.ConversionFactor = .000001m; await db.SaveChangesAsync();
            try { await Bad("POST", $"material-requests/{convDraft.RequestId}/fulfillments", new MaterialIssue(issuer, convDraft.RowVersion, "FullRemaining", DateOnly.FromDateTime(DateTime.UtcNow))); }
            finally { conversion.ConversionFactor = original; await db.SaveChangesAsync(); }
            // Conversion snapshot is taken by Inventory at issue, not approval.
            conversion.ConversionFactor = 3; await db.SaveChangesAsync();
            try { var batch = await Issue(convDraft); var baseQty = await db.StockDocumentLines.Where(x => x.DocumentId == batch.StockDocumentId).Select(x => x.BaseQty).SingleAsync(); Check(baseQty == 6, "Current conversion at ISSUE time"); }
            finally { conversion.ConversionFactor = original; await db.SaveChangesAsync(); }
        }
        // Number tests use owned rows; no legacy row is rewritten.
        var seed = await Create();
        var date = DateTime.UtcNow.ToString("yyyyMMdd"); var mr = "MR-" + date + "-";
        await using (var db = Db())
        {
            var values = await db.MaterialRequests.Where(x => x.RequestNo.StartsWith(mr)).Select(x => x.RequestNo).ToArrayAsync();
            var max = values.Select(x => long.TryParse(x[mr.Length..], out var v) ? v : 0).Max(); var n = Math.Max(999, max + 10);
            await db.MaterialRequests.Where(x => x.RequestId == seed.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequestNo, mr + n));
            var next = await Create(); Check(next.RequestNo == mr + (n + 1).ToString("D3"), "MR suffix beyond999/UTC");
            await db.MaterialRequests.Where(x => x.RequestId == seed.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequestNo, mr + "bad"));
            var afterMalformed = await Create(); Check(afterMalformed.RequestNo == mr + (n + 2).ToString("D3"), "MR ignores malformed suffix");
            await using (var failed = Db(new SaveFailure(1)))
            {
                var threw = false; try { await Service(failed).Save(null, Write(), default); } catch (InjectedFailure) { threw = true; }
                Check(threw, "MR number rollback injection");
            }
            var afterRollback = await Create(); Check(afterRollback.RequestNo == mr + (n + 3).ToString("D3"), "Rolled-back MR number not consumed");
        }
        var seedApproved = await Approved(1); var f = await Issue(seedApproved); var ful = "FUL-" + date + "-";
        await using (var db = Db())
        {
            var values = await db.MaterialFulfillments.Where(x => x.FulfillmentNo.StartsWith(ful)).Select(x => x.FulfillmentNo).ToArrayAsync(); var max = values.Select(x => long.TryParse(x[ful.Length..], out var v) ? v : 0).Max(); var n = Math.Max(999, max + 10);
            await db.MaterialFulfillments.Where(x => x.FulfillmentId == f.FulfillmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FulfillmentNo, ful + n));
            var next = await Issue(await Approved(1)); Check(next.FulfillmentNo == ful + (n + 1).ToString("D3"), "FUL beyond999");
            await db.MaterialFulfillments.Where(x => x.FulfillmentId == f.FulfillmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FulfillmentNo, ful + "bad"));
            next = await Issue(await Approved(1)); Check(next.FulfillmentNo == ful + (n + 2).ToString("D3"), "FUL ignores malformed suffix");
        }
        var linked = await Create(Write(2, true) with { IncidentId = incidents[0] }); linked = await Approve(linked);
        linked = await Send<MaterialRequestDto>("PUT", $"material-requests/{linked.RequestId}", Edit(linked) with { IncidentId = incidents[1] });
        Check(linked.RawStatus == "PENDING_APPROVAL", "Approved link change requires reapproval");
        linked = await Approve(linked);
        await using (var db = Db()) await db.Incidents.Where(x => x.IncidentId == incidents[1]).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "CLOSED"));
        await Issue(linked); // Closing source after linking must not block ISSUE.
        var batchReturn = await Issue(converted);
        var ret = await Send<InventoryDocumentDto>("POST", "inventory/returns", new InventoryDraft(issuer, null, warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), [new(item.ItemId, unit.UomId, 1, batchReturn.Lines[0].StockDocumentLineId)], Note: prefix), 201);
        await Send<InventoryDocumentDto>("POST", $"inventory/returns/{ret.DocumentId}/post", new InventoryPost(issuer, ret.RowVersion, [new(ret.Lines[0].LineId, 1)]));
        var returned = await Get(converted.RequestId); Check(returned.Lines[0].FulfilledQty == 2 && returned.Lines[0].OpenFulfillableQty == 0 && returned.Returns.Count == 1, "Return linkage without reopening demand");
    }
}
