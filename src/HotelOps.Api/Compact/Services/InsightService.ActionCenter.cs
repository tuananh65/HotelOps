using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    private async Task<OperationsActionCenter?> ActionCenter(long branchId,DateTime now,CancellationToken ct)
    {
        if(!await access.HasRole("OPERATION_MANAGER",branchId,ct))return null;
        var groups=new List<ActionCenterGroup>();
        var assets=0;var retired=0;var lowStock=0;var inTransit=0;
        if(await access.Allows("TASK_VIEW",branchId,ct:ct)){
            var canManage=await access.Allows("TASK_MANAGE",branchId,ct:ct);
            var tasks=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&w.Kind=="OPERATIONAL"&&!WorkRules.Terminal.Contains(w.Status));
            foreach(var category in new[]{"UNASSIGNED_TASKS","TASKS_TO_VERIFY","OVERDUE_TASKS"}){
                var query=category=="UNASSIGNED_TASKS"?tasks.Where(w=>w.Status=="PENDING_ASSIGNMENT")
                    :category=="TASKS_TO_VERIFY"?tasks.Where(w=>w.Status=="PENDING_VERIFICATION"):tasks.Where(w=>w.DueAt<now);
                var rows=await query.OrderBy(w=>w.DueAt).ThenBy(w=>w.WorkId).Take(10)
                    .Select(w=>new ActionCenterItem("work",w.WorkId,w.Kind,w.Title,w.Status,w.CreatedAt,w.DueAt,canManage)).ToArrayAsync(ct);
                groups.Add(new(category,await query.CountAsync(ct),rows));
            }
        }
        if(await access.Allows("ASSET_VIEW",branchId,ct:ct)){
            var all=db.Assets.AsNoTracking().Where(a=>a.BranchId==branchId);
            assets=await all.CountAsync(a=>a.IsActive,ct);retired=await all.CountAsync(a=>!a.IsActive,ct);
            // A pending offer stays in immutable history after acceptance. Only the latest
            // handover event represents the current handover, not every old PENDING event.
            var pending=from e in db.AssetEvents.AsNoTracking() join a in all on e.AssetId equals a.AssetId
                where a.IsActive&&e.BranchId==branchId&&e.HandoverStatus=="PENDING"
                    &&!db.AssetEvents.Any(n=>n.AssetId==e.AssetId&&n.HandoverStatus!=null&&n.AssetEventId>e.AssetEventId)
                select new{e,a};
            groups.Add(new("ASSET_HANDOVERS",await pending.CountAsync(ct),await pending.OrderBy(x=>x.e.OccurredAt).Take(10)
                .Select(x=>new ActionCenterItem("asset",x.a.AssetId,"ASSET_HANDOVER",x.a.Name,"PENDING",x.e.OccurredAt,null,x.e.ToUserId==actor.Id)).ToArrayAsync(ct)));
        }
        if(await access.Allows("SHIFT_MANAGE",branchId,ct:ct)){
            var kinds=new List<string>();
            foreach(var kind in new[]{"CLEANING","REWORK","OPERATIONAL","INTERNAL_REQUEST"})
                if(await access.Allows(WorkRules.Permission(kind,"read"),branchId,ct:ct))kinds.Add(kind);
            var visible=InternalRequestVisibility.Apply(db.WorkItems.AsNoTracking(),db,actor.Id,now);
            var pending=from h in db.VBacklogHandovers.AsNoTracking() join w in visible on h.WorkId equals w.WorkId
                where h.BranchId==branchId&&w.BranchId==branchId&&h.Decision==null&&kinds.Contains(w.Kind)&&!WorkRules.Terminal.Contains(w.Status)
                select new{h,w};
            groups.Add(new("WORK_HANDOVERS",await pending.CountAsync(ct),await pending.OrderBy(x=>x.h.OccurredAt).Take(10)
                .Select(x=>new ActionCenterItem("work",x.w.WorkId,x.w.Kind,x.w.Title,x.w.Status,x.h.OccurredAt,x.w.DueAt,false)).ToArrayAsync(ct)));
        }
        if(await access.Allows("INVENTORY_VIEW",branchId,ct:ct)){
            lowStock=(await StockRows(branchId,ct)).Count(s=>s.MinimumQty!=null&&s.AvailableQty<s.MinimumQty);
            var discrepancies=StocktakeDifferenceLines(branchId).Select(x=>x.DocumentId).Distinct();
            var stocktakes=db.InventoryDocuments.AsNoTracking().Where(d=>discrepancies.Contains(d.DocumentId));
            groups.Add(new("STOCKTAKE_DIFFERENCES",await stocktakes.CountAsync(ct),await stocktakes.OrderBy(d=>d.CreatedAt).Take(10)
                .Select(d=>new ActionCenterItem("document",d.DocumentId,d.Kind,d.DocumentNo,d.Status,d.CreatedAt,null,false)).ToArrayAsync(ct)));
            var transfers=db.InventoryDocuments.AsNoTracking().Where(d=>d.Kind=="TRANSFER_REQUEST"&&d.BranchId==branchId&&d.Status=="PENDING_APPROVAL");
            var canApprove=await access.Allows("TRANSFER_REQUEST_APPROVE",branchId,ct:ct);
            groups.Add(new("TRANSFER_APPROVALS",await transfers.CountAsync(ct),await transfers.OrderBy(d=>d.CreatedAt).Take(10)
                .Select(d=>new ActionCenterItem("document",d.DocumentId,d.Kind,d.DocumentNo,d.Status,d.CreatedAt,null,canApprove&&d.CreatedBy!=actor.Id)).ToArrayAsync(ct)));
            var posted=await db.InventoryDocuments.AsNoTracking().Where(d=>d.Kind=="TRANSFER"&&(d.BranchId==branchId||d.DestinationBranchId==branchId)&&d.PostedAt!=null
                &&!db.InventoryDocuments.Any(r=>r.Kind=="REVERSAL"&&r.SourceDocumentId==d.DocumentId&&r.PostedAt!=null)).Select(d=>d.DocumentId).ToArrayAsync(ct);
            inTransit=(await TransferStatusProjection.Read(db,posted,ct)).Values.Count(s=>s is "IN_TRANSIT" or "PARTIALLY_RECEIVED");
        }
        // P0 supersedes the earlier material-approval tile: OM only approves stock replenishment.
        if(await access.Allows("IR_VIEW",branchId,ct:ct)){
            var canApprove=await access.Allows("REPLENISHMENT_APPROVE",branchId,ct:ct);
            if(canApprove){
                try{await WorkdayAuthority.Require(db,access,actor,clock,branchId,"OPERATION_MANAGER",ct);}
                catch(ApiException error) when(error.Status is 403 or 409){canApprove=false;}
            }
            var replenishments=from r in db.RequestWorkflows.AsNoTracking() join w in db.WorkItems on r.WorkId equals w.WorkId
                where r.BranchId==branchId&&r.RequestType==RequestWorkflowService.ReplenishmentType&&r.Status=="PENDING_OPERATIONS_MANAGER"
                select new{r,w};
            groups.Add(new("REPLENISHMENT_APPROVALS",await replenishments.CountAsync(ct),await replenishments.OrderBy(x=>x.w.CreatedAt).Take(10)
                .Select(x=>new ActionCenterItem("request",x.w.WorkId,x.r.RequestType,x.w.Title,x.r.Status,x.w.CreatedAt,x.w.DueAt,
                    canApprove&&x.w.CreatedBy!=actor.Id&&(x.r.HandlerUserId==null||x.r.HandlerUserId==actor.Id))).ToArrayAsync(ct)));
        }
        return new(now,groups,assets,retired,lowStock,inTransit);
    }
}
