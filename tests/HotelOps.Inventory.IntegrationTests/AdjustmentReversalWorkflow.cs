using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;

sealed partial class InventorySuite
{
    private Task<InventoryDocumentDto> ProposeAdjustment(decimal delta, long warehouse) =>
        Send<InventoryDocumentDto>("POST", "inventory/adjustments",
            new InventoryAdjustment(warehouse, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, delta)]), 201);
    private Task<InventoryDocumentDto> PostAdjustmentFixture(InventoryDocumentDto draft) =>
        Send<InventoryDocumentDto>("POST", $"inventory/adjustments/{draft.DocumentId}/post", Command(draft.RowVersion, who: other));
    private Task<InventoryDocumentDto> ProposeReversal(InventoryDocumentDto original) =>
        Send<InventoryDocumentDto>("POST", $"inventory/documents/{original.DocumentId}/reverse", Command(original.RowVersion, prefix));
    private async Task<InventoryDocumentDto> PostReversalFixture(InventoryDocumentDto draft)
    {
        if (reversalPosterHttp != null)
            return await Send<InventoryDocumentDto>("POST", $"inventory/reversals/{draft.DocumentId}/post", Command(draft.RowVersion));
        // Explicit trusted service coverage ONLY. APPROVER is independent of OM
        // but is not granted STOCK_REVERSE by this test; no HTTP claim is made.
        await using var db = Db();
        return (await new InventoryService(db).PostReversal(draft.DocumentId, other, new(draft.RowVersion), default))!;
    }
    private async Task<InventoryDocumentDto> ReversePosted(InventoryDocumentDto original) =>
        await PostReversalFixture(await ProposeReversal(original));
    private async Task ReversalPostFails(InventoryDocumentDto draft, string? version = null)
    {
        if (reversalPosterHttp != null)
        {
            await Bad("POST", $"inventory/reversals/{draft.DocumentId}/post", Command(version ?? draft.RowVersion));
            return;
        }
        await using var db = Db();
        var failed = false;
        try { await new InventoryService(db).PostReversal(draft.DocumentId, other, new(version ?? draft.RowVersion), default); }
        catch (ValidationException) { failed = true; }
        Check(failed, "Expected service-level reversal validation failure.");
    }
    private async Task AssertProposal(InventoryDocumentDto d, long proposer, long warehouse, decimal balance)
    {
        await using var db = Db();
        var header = await db.StockDocuments.AsNoTracking().SingleAsync(x => x.DocumentId == d.DocumentId);
        Check(header.Status == "DRAFT" && d.Status == "DRAFT" && header.CreatedBy == proposer
            && header.PostedBy == null && header.PostedAt == null, "Proposal must persist an authenticated proposer and no poster.");
        Check((await Effect(d.DocumentId)).Movements == 0 && (await Stock(warehouse)).OnHand == balance,
            "Proposal changed movements or stock balance.");
    }
    private async Task AdjustmentReversalWorkflow()
    {
        await Bad("POST", "inventory/adjustments", new InventoryAdjustment(source.WarehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 0)]));
        var before = (await Stock(source.WarehouseId)).OnHand;
        var spoof = JsonSerializer.SerializeToNode(new InventoryAdjustment(source.WarehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, -2)]))!.AsObject();
        spoof["actorUserId"] = other;
        var draft = await Send<InventoryDocumentDto>("POST", "inventory/adjustments", spoof, 201);
        await AssertProposal(draft, omHttp.UserId, source.WarehouseId, before);
        Check(draft.Lines.Single().RequestedQty == -2 && draft.Lines.Single().ActualQty == 2
            && draft.Lines.Single().BaseQty == 2 && draft.Lines.Single().ConversionFactor == 1
            && draft.Lines.Single().UomId == unit.UomId, "Signed adjustment intent / positive magnitude persistence.");
        Check(await As(omHttp, "POST", $"adjustments/{draft.DocumentId}/post", new InventoryCommand(draft.RowVersion)) == 400,
            "Adjustment proposer must not self-post.");
        Check(await As(deniedHttp, "POST", $"adjustments/{draft.DocumentId}/post", new InventoryCommand(draft.RowVersion)) == 403,
            "Missing STOCK_ADJUST_POST must deny posting.");
        Check(await As(foreignHttp, "POST", $"adjustments/{draft.DocumentId}/post",
            new { expectedRowVersion = draft.RowVersion, warehouseId = destination.WarehouseId }) == 403,
            "Client warehouse fields cannot authorize a foreign adjustment.");
        await Bad("POST", $"inventory/adjustments/{draft.DocumentId}/post", Command(Convert.ToBase64String(new byte[8]), who: other));
        var postSpoof = JsonSerializer.SerializeToNode(new InventoryCommand(draft.RowVersion))!.AsObject();
        postSpoof["actorUserId"] = omHttp.UserId;
        postSpoof["lines"] = JsonSerializer.SerializeToNode(new[] { new { deltaBaseQty = 999 } });
        var posted = await Send<InventoryDocumentDto>("POST", $"inventory/adjustments/{draft.DocumentId}/post", postSpoof);
        Check((await Effect(posted.DocumentId)) == ("POSTED", 1, -2m) && (await Stock(source.WarehouseId)).OnHand == before - 2,
            "Posting must use the persisted signed proposal, ignoring supplied deltas.");
        await using (var db = Db())
        {
            Check(await db.StockDocuments.Where(x => x.DocumentId == posted.DocumentId)
                .AllAsync(x => x.CreatedBy == omHttp.UserId && x.PostedBy == other && x.PostedAt != null), "Independent adjustment poster attribution.");
            Check(await db.StockMovements.Where(x => x.DocumentLine.DocumentId == posted.DocumentId)
                .AllAsync(x => x.CreatedBy == other && x.SignedBaseQty == -2), "Adjustment movement actor/sign.");
        }
        await Bad("POST", $"inventory/adjustments/{posted.DocumentId}/post", Command(draft.RowVersion, who: other));
        await Bad("POST", $"inventory/adjustments/{posted.DocumentId}/post", Command(posted.RowVersion, who: other));
        var reversal = await ProposeReversal(posted);
        await AssertProposal(reversal, omHttp.UserId, source.WarehouseId, before - 2);
        Check((await Get(posted.DocumentId)).Status == "POSTED" && reversal.ReversalOfDocumentId == posted.DocumentId,
            "Reversal proposal must leave original POSTED.");
        Check(await As(omHttp, "POST", $"reversals/{reversal.DocumentId}/post", new InventoryCommand(reversal.RowVersion)) == 400,
            "Reversal proposer must not self-post even with STOCK_REVERSE.");
        Check(await As(deniedHttp, "POST", $"reversals/{reversal.DocumentId}/post", new InventoryCommand(reversal.RowVersion)) == 403,
            "Missing STOCK_REVERSE must deny posting.");
        Check(await As(foreignHttp, "POST", $"documents/{posted.DocumentId}/reverse", new InventoryCommand(posted.RowVersion, prefix)) == 403,
            "Foreign reversal proposal must be denied.");
        Check(await As(foreignHttp, "POST", $"reversals/{reversal.DocumentId}/post",
            new { expectedRowVersion = reversal.RowVersion, warehouseId = destination.WarehouseId }) == 403,
            "Reversal posting scope must come from original effect warehouses.");
        // OM has the relevant permissions in the normal branch. Give it an
        // existing foreign resource, plus spoofed normal warehouse input, to
        // prove authoritative scope denial independently of missing permissions.
        long foreignBranch, foreignDepartment;
        await using (var db = Db())
        {
            foreignBranch = await db.Users.Where(x => x.UserId == foreignHttp.UserId).Select(x => x.PrimaryBranchId).SingleAsync()
                ?? throw new InvalidOperationException("FOREIGN fixture needs a primary branch.");
            foreignDepartment = await db.Departments.Where(x => x.BranchId == foreignBranch && x.IsActive).Select(x => x.DepartmentId).FirstAsync();
        }
        var foreignWarehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses",
            new InventoryWarehouseWrite(foreignBranch, foreignDepartment, prefix + "SODF", "Foreign SoD fixture"), 201);
        InventoryDocumentDto foreignAdjustment;
        await using (var db = Db())
            foreignAdjustment = await new InventoryService(db).CreateAdjustment(foreignHttp.UserId,
                new(foreignWarehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1)]), default);
        documents.Add(foreignAdjustment.DocumentId);
        Check(await As(omHttp, "POST", $"adjustments/{foreignAdjustment.DocumentId}/post",
            new { expectedRowVersion = foreignAdjustment.RowVersion, warehouseId = source.WarehouseId }) == 403,
            "Normal-branch STOCK_ADJUST_POST cannot post an authoritative foreign warehouse.");
        var foreignReceipt = await Receipt(1, foreignWarehouse.WarehouseId);
        foreignReceipt = await PostReceipt(foreignReceipt, 1);
        Check(await As(omHttp, "POST", $"documents/{foreignReceipt.DocumentId}/reverse",
            new { expectedRowVersion = foreignReceipt.RowVersion, reason = prefix, warehouseId = source.WarehouseId }) == 403,
            "Normal-branch STOCK_REVERSE cannot propose an authoritative foreign reversal.");
        InventoryDocumentDto foreignReversal;
        await using (var db = Db())
            foreignReversal = (await new InventoryService(db).Reverse(foreignReceipt.DocumentId, foreignHttp.UserId,
                new(foreignReceipt.RowVersion, prefix), default))!;
        documents.Add(foreignReversal.DocumentId);
        Check(await As(omHttp, "POST", $"reversals/{foreignReversal.DocumentId}/post",
            new { expectedRowVersion = foreignReversal.RowVersion, warehouseId = source.WarehouseId }) == 403,
            "Reversal poster scope must include foreign original movements despite spoofed normal warehouse input.");
        await ReversalPostFails(reversal, Convert.ToBase64String(new byte[8]));
        var effective = await PostReversalFixture(reversal);
        Check(effective.Status == "POSTED" && (await Get(posted.DocumentId)).Status == "REVERSED"
            && (await Stock(source.WarehouseId)).OnHand == before, "Reversal/original statuses and balance must change atomically.");
        await using (var db = Db())
        {
            var poster = reversalPosterHttp?.UserId ?? other;
            Check(await db.StockDocuments.Where(x => x.DocumentId == reversal.DocumentId)
                .AllAsync(x => x.CreatedBy == omHttp.UserId && x.PostedBy == poster && x.PostedAt != null), "Reversal poster attribution.");
            var effects = await db.StockMovements.AsNoTracking().Where(x => x.DocumentLine.DocumentId == posted.DocumentId
                || x.DocumentLine.DocumentId == reversal.DocumentId).ToListAsync();
            Check(effects.Count == 2 && effects.GroupBy(x => (x.WarehouseId, x.ItemId)).All(g => g.Sum(x => x.SignedBaseQty) == 0),
                "Reversal must exactly offset original movements.");
        }
        await ReversalPostFails(effective);
        await Bad("POST", $"inventory/documents/{posted.DocumentId}/reverse", Command((await Get(posted.DocumentId)).RowVersion, prefix));
        await Bad("POST", $"inventory/documents/{effective.DocumentId}/reverse", Command(effective.RowVersion, prefix));

        // Isolate post-time shortages/faults so existing stocktake baselines stay intact.
        var warehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses",
            new InventoryWarehouseWrite(branch, department, prefix + "SOD", "Adjustment/reversal workflow fixture"), 201);
        var negative = await ProposeAdjustment(-1, warehouse.WarehouseId);
        await AssertProposal(negative, omHttp.UserId, warehouse.WarehouseId, 0);
        await Bad("POST", $"inventory/adjustments/{negative.DocumentId}/post", Command(negative.RowVersion, who: other));
        await AssertProposal(await Get(negative.DocumentId), omHttp.UserId, warehouse.WarehouseId, 0);
        await Adjust(2, warehouse.WarehouseId);
        var delayed = await ProposeAdjustment(-2, warehouse.WarehouseId);
        var consume = await Adjust(-1, warehouse.WarehouseId);
        await Bad("POST", $"inventory/adjustments/{delayed.DocumentId}/post", Command(delayed.RowVersion, who: other));
        await AssertProposal(await Get(delayed.DocumentId), omHttp.UserId, warehouse.WarehouseId, 1);
        var aggregate = await Send<InventoryDocumentDto>("POST", "inventory/adjustments",
            new InventoryAdjustment(warehouse.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix,
                [new(item.ItemId, -0.75m), new(item.ItemId, -0.75m)]), 201);
        await Bad("POST", $"inventory/adjustments/{aggregate.DocumentId}/post", Command(aggregate.RowVersion, who: other));
        await AssertProposal(await Get(aggregate.DocumentId), omHttp.UserId, warehouse.WarehouseId, 1);

        var fault = await ProposeAdjustment(1, warehouse.WarehouseId);
        foreach (var interceptor in new Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[]
            { new FailAfterWrite("stock_movements"), new FailBeforeCommit() })
        {
            await Fails(async service => { await service.PostAdjustment(fault.DocumentId, other, new(fault.RowVersion), default); }, interceptor);
            await AssertProposal(await Get(fault.DocumentId), omHttp.UserId, warehouse.WarehouseId, 1);
        }
        var correction = await ProposeReversal(consume);
        foreach (var interceptor in new Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[]
            { new FailAfterWrite("stock_movements"), new FailBeforeCommit() })
        {
            await Fails(async service => { await service.PostReversal(correction.DocumentId, other, new(correction.RowVersion), default); }, interceptor);
            await AssertProposal(await Get(correction.DocumentId), omHttp.UserId, warehouse.WarehouseId, 1);
            Check((await Get(consume.DocumentId)).Status == "POSTED", "Reversal rollback changed the original.");
        }
        await ConcurrentReversalPost(correction);
        Check((await Effect(correction.DocumentId)) == ("POSTED", 1, 1m)
            && (await Get(consume.DocumentId)).Status == "REVERSED", "Concurrent reversal post duplicated stock effects.");

        var receipt = await Receipt(2, warehouse.WarehouseId);
        receipt = await PostReceipt(receipt, 2);
        var receiptReversal = await ProposeReversal(receipt);
        await Adjust(-(await Stock(warehouse.WarehouseId)).OnHand, warehouse.WarehouseId);
        await ReversalPostFails(receiptReversal);
        await AssertProposal(await Get(receiptReversal.DocumentId), omHttp.UserId, warehouse.WarehouseId, 0);
        Check((await Get(receipt.DocumentId)).Status == "POSTED", "Failed post reversed the receipt.");
        Check((await Stock(source.WarehouseId)).OnHand == before, "Workflow tests disturbed the stocktake baseline.");
        passed.Add("Adjustment/reversal DRAFT/post, signed persisted intent, independent actors, authoritative scope, stale/double posts, post-time shortages, movement/commit rollback and concurrent reversal posting; optional HTTP reversal poster prerequisite reported separately");
    }
    private async Task ConcurrentReversalPost(InventoryDocumentDto draft)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        await using var blocker = Db();
        await using var tx = await blocker.Database.BeginTransactionAsync(ct);
        await blocker.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:Inventory:Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Test blocker failed',1;", ct);
        var ready = new[] { new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously) };
        async Task<int> Attempt(int index)
        {
            await using var db = Db();
            try
            {
                await db.Database.OpenConnectionAsync(ct);
                ready[index].TrySetResult(await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct));
                await new InventoryService(db).PostReversal(draft.DocumentId, other, new(draft.RowVersion), ct);
                return 200;
            }
            catch (ValidationException) { return 400; }
            catch (Exception e) { ready[index].TrySetException(e); throw; }
        }
        var workers = new[] { Attempt(0), Attempt(1) };
        var released = false;
        try
        {
            var sessions = await Task.WhenAll(ready.Select(x => x.Task));
            var waiting = 0;
            for (var attempt = 0; attempt < 100 && waiting != 2; attempt++)
            {
                waiting = await blocker.Database.SqlQuery<int>($"""
                    SELECT COUNT(DISTINCT request_session_id) AS Value FROM sys.dm_tran_locks
                    WHERE resource_database_id=DB_ID() AND resource_type='APPLICATION' AND request_status='WAIT'
                        AND resource_description LIKE '%HotelOps:Inventory:Catalog%'
                        AND request_session_id IN ({sessions[0]}, {sessions[1]})
                    """).SingleAsync(ct);
                if (waiting != 2) await Task.Delay(50, ct);
            }
            Check(waiting == 2, "Both reversal posters must reach the SQL Catalog wait barrier.");
            await tx.CommitAsync(ct);
            released = true;
            var outcomes = await Task.WhenAll(workers);
            Check(outcomes.Order().SequenceEqual(new[] { 200, 400 }), "Exactly one concurrent reversal poster must commit.");
        }
        finally
        {
            cancellation.Cancel();
            if (!released) await tx.RollbackAsync(CancellationToken.None);
            try { await Task.WhenAll(workers); } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
    }
}
