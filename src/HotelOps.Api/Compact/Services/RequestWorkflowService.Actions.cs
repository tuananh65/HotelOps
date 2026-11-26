using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class RequestWorkflowService
{
    public Task<RequestWorkflowDetail> CreateRefill(CreateRefillWorkflowRequest input,CancellationToken ct)=>atomic.Run(async()=>{
        await Lock(ct);Quantities(input.Items);var reason=Output.Reason(input.Reason);
        var allocation=await db.StaffSupplyAllocations.SingleOrDefaultAsync(a=>a.AllocationId==input.AllocationId,ct)??throw new ApiException(404,"SUPPLY_ALLOCATION_NOT_FOUND");
        await access.Require("IR_CREATE",allocation.BranchId,true,ct);
        Output.Require(await access.HasRole("HK_STAFF",allocation.BranchId,ct)&&!await access.HasRole("OPERATION_MANAGER",allocation.BranchId,ct)&&allocation.UserId==actor.Id,"REFILL_REQUESTER_REQUIRED",403);
        var hash=Hash(input);if(await Retry(input.OperationKey,"CREATE_REFILL",hash,null,ct) is long previous)return await Detail(previous,ct);
        await ActiveAllocation(allocation,ct);
        Output.Require(!await db.RequestWorkflows.AnyAsync(r=>r.AllocationId==allocation.AllocationId&&r.RequestType==RefillType&&!Finished.Contains(r.Status),ct),"REFILL_REQUEST_ALREADY_OPEN");
        var snapshot=await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
        Output.Require(input.Items.All(i=>snapshot.Any(b=>b.ItemId==i.ItemId)),"SUPPLY_ITEM_NOT_IN_SHIFT_SNAPSHOT");
        var consumables=await db.ItemLifecycleProfiles.AsNoTracking().Where(p=>p.BranchId==allocation.BranchId&&p.Classification=="CONSUMABLE").Select(p=>p.ItemId).ToArrayAsync(ct);
        Output.Require(input.Items.All(i=>consumables.Contains(i.ItemId)),"CONSUMABLE_ITEMS_REQUIRED",400);
        var work=await NewWork(allocation.BranchId,"Yêu cầu bổ sung tiêu hao",reason,null,ct);
        var flow=new RequestWorkflow{WorkId=work.WorkId,BranchId=work.BranchId,RequestType=RefillType,Status="PENDING_OPERATIONS_MANAGER",AllocationId=allocation.AllocationId,CorrelationId=Guid.NewGuid(),UpdatedAt=Now};
        await Route(flow,"OPERATION_MANAGER",ct);db.RequestWorkflows.Add(flow);await db.SaveChangesAsync(ct);
        foreach(var requested in input.Items){var b=snapshot.Single(x=>x.ItemId==requested.ItemId);db.RequestWorkflowLines.Add(new RequestWorkflowLine{WorkId=work.WorkId,ItemId=b.ItemId,UnitId=b.UnitId,RequestedQuantity=requested.Quantity,StockAtRequest=SupplyMath.Available(b),MinimumAtRequest=0});}
        await Record(flow,work,"CREATE_REFILL",await Role("HK_STAFF",ct),input.OperationKey,hash,null,null,null,null,reason,ct);
        return await Detail(work.WorkId,ct);
    },ct);

    public Task<RequestWorkflowDetail> CreateReplenishment(CreateReplenishmentWorkflowRequest input,CancellationToken ct)=>atomic.Run(async()=>{
        await Lock(ct);await access.RequireRolePermission("STOREKEEPER","REPLENISHMENT_CREATE",input.BranchId,ct);
        Quantities(input.Items);var reason=Output.Reason(input.Reason);var hash=Hash(input);
        if(await Retry(input.OperationKey,"CREATE_REPLENISHMENT",hash,null,ct) is long previous)return await Detail(previous,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,input.BranchId,"STOREKEEPER",ct);
        Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==input.WarehouseId&&w.BranchId==input.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        if(input.PriorityId!=null)Output.Require(await db.ReferenceValues.AnyAsync(v=>v.ReferenceId==input.PriorityId&&v.Kind=="PRIORITY"&&v.IsActive,ct),"INVALID_PRIORITY",400);
        var ids=input.Items.Select(i=>i.ItemId).ToArray();
        var catalog=await db.InventoryItems.AsNoTracking().Where(i=>ids.Contains(i.ItemId)&&i.IsActive).ToArrayAsync(ct);
        Output.Require(catalog.Length==ids.Length,"ITEM_UNAVAILABLE");
        var work=await NewWork(input.BranchId,"Yêu cầu bổ sung hàng cho kho",reason,input.PriorityId,ct);
        var flow=new RequestWorkflow{WorkId=work.WorkId,BranchId=work.BranchId,RequestType=ReplenishmentType,Status="PENDING_OPERATIONS_MANAGER",WarehouseId=input.WarehouseId,CorrelationId=Guid.NewGuid(),UpdatedAt=Now};
        await Route(flow,"OPERATION_MANAGER",ct);db.RequestWorkflows.Add(flow);await db.SaveChangesAsync(ct);
        foreach(var requested in input.Items){var item=catalog.Single(i=>i.ItemId==requested.ItemId);db.RequestWorkflowLines.Add(new RequestWorkflowLine{WorkId=work.WorkId,ItemId=item.ItemId,UnitId=item.BaseUnitId,RequestedQuantity=requested.Quantity,StockAtRequest=await Stock(input.WarehouseId,item.ItemId,ct),MinimumAtRequest=await db.InventoryPolicies.Where(p=>p.WarehouseId==input.WarehouseId&&p.ItemId==item.ItemId).Select(p=>(decimal?)p.MinimumQty).SingleOrDefaultAsync(ct)??0});}
        await db.SaveChangesAsync(ct);
        flow.DocumentId=await documents.CreateWorkflowReplenishment(flow,work,await db.RequestWorkflowLines.Where(l=>l.WorkId==work.WorkId).ToArrayAsync(ct),input.OperationKey,ct);
        await Record(flow,work,"CREATE_REPLENISHMENT",await Role("STOREKEEPER",ct),input.OperationKey,hash,null,null,null,null,reason,ct);
        return await Detail(work.WorkId,ct);
    },ct);

    private async Task<WorkItem> NewWork(long branch,string title,string reason,long? priority,CancellationToken ct)
    {
        var work=new WorkItem{BranchId=branch,WorkNo=Guid.NewGuid().ToString("N"),Kind="INTERNAL_REQUEST",Status="SUBMITTED",Title=title,Description=reason,CreatedBy=actor.Id,CreatedAt=Now,PriorityId=priority,CategoryKind="WORK_CATEGORY",PriorityKind="PRIORITY",SeverityKind="SEVERITY"};
        work.RequesterDepartmentId=await db.Memberships.Where(m=>m.UserId==actor.Id&&m.BranchId==branch&&m.Kind=="EMPLOYMENT"&&m.ValidFrom<=Now&&(m.ValidTo==null||m.ValidTo>Now)).OrderBy(m=>m.MembershipId).Select(m=>m.DepartmentId).FirstOrDefaultAsync(ct);
        db.WorkItems.Add(work);await db.SaveChangesAsync(ct);return work;
    }

    private async Task<(RequestWorkflow Flow,WorkItem Work)> Load(long id,CancellationToken ct)
    {
        var flow=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.WorkId==id,ct)??throw new ApiException(404,"REQUEST_WORKFLOW_NOT_FOUND");
        await access.Require("IR_VIEW",flow.BranchId,true,ct);
        Output.Require(await (await Visible(flow.BranchId,ct)).AnyAsync(w=>w.WorkId==id,ct),"REQUEST_VISIBILITY_DENIED",403);
        return (flow,await db.WorkItems.SingleAsync(w=>w.WorkId==id,ct));
    }

    private static string HandlerRole(RequestWorkflow flow)=>flow.Status switch{
        "PENDING_STOREKEEPER" when flow.RequestType==RefillType=>"STOREKEEPER",
        "PENDING_OPERATIONS_MANAGER" when flow.RequestType is RefillType or ReplenishmentType=>"OPERATION_MANAGER",
        "PENDING_RECEIPT" when flow.RequestType==ReplenishmentType=>"STOREKEEPER",
        _=>throw new ApiException(409,"REQUEST_NOT_ACTIONABLE")};

    private async Task<long> HandlerAuthority(RequestWorkflow flow,CancellationToken ct,bool allowStaleClaim=false)
    {
        Output.Require(flow.RequestType is RefillType or ReplenishmentType||!await access.HasRole("OPERATION_MANAGER",flow.BranchId,ct),"OM_REQUEST_READ_ONLY",403);
        var role=flow.RequestType is RefillType or ReplenishmentType?HandlerRole(flow):await DomainHandlerRole(flow,ct);
        var permission=role=="OPERATION_MANAGER"?"REPLENISHMENT_APPROVE":role=="STOREKEEPER"?(flow.RequestType==ReplenishmentType?"REPLENISHMENT_RECEIVE":"MR_FULFILL"):role=="MAINTENANCE_SUPERVISOR"?"MAINTENANCE_EXECUTE":"IR_PROCESS";
        await access.RequireRolePermission(role,permission,flow.BranchId,ct);
        if(role!="OPERATION_MANAGER")await WorkdayAuthority.Require(db,access,actor,clock,flow.BranchId,role,ct);
        var roleId=await Role(role,ct);
        Output.Require(flow.QueueRoleId==roleId&&(flow.HandlerUserId==null||flow.HandlerUserId==actor.Id||allowStaleClaim&&!await HasValidHandler(flow,ct)),"REQUEST_ALREADY_CLAIMED",403);
        return roleId;
    }

    private async Task<IReadOnlyList<string>> Actions(RequestWorkflow flow,WorkItem work,CancellationToken ct)
    {
        var result=new List<string>();
        if(Finished.Contains(flow.Status))return result;
        if(flow.RequestType==RefillType&&work.CreatedBy==actor.Id&&!await access.HasRole("OPERATION_MANAGER",flow.BranchId,ct))result.Add("cancel");
        try{await HandlerAuthority(flow,ct,allowStaleClaim:true);}catch(ApiException e) when(e.Status is 403 or 409){return result;}
        if(flow.HandlerUserId!=null&&flow.HandlerUserId!=actor.Id){result.Add("claim");return result;}
        result.Add(flow.HandlerUserId==null?"claim":"release");
        if(flow.RequestType is not (RefillType or ReplenishmentType))return result;
        if(flow.Status=="PENDING_RECEIPT")result.Add("receive");
        else{result.Add("reject");result.Add(flow.Status=="PENDING_STOREKEEPER"?"issue":"approve");}
        return result;
    }

    public Task<RequestWorkflowDetail> Act(long id,string action,RequestWorkflowAction input,CancellationToken ct)=>atomic.Run(async()=>{
        await Lock(ct);var (flow,work)=await Load(id,ct);var hash=Hash(new{action,input});
        if(await Retry(input.OperationKey,action,hash,id,ct)!=null)return await Detail(id,ct);
        Output.Version(flow.RowVersion,input.RowVersion);
        var previous=flow.Status;var queue=flow.QueueRoleId;var handler=flow.HandlerUserId;var version=flow.RowVersion.ToArray();
        long actorRole;
        if(action=="cancel"){
            Output.Require(flow.RequestType==RefillType&&work.CreatedBy==actor.Id&&!Finished.Contains(flow.Status)&&await access.HasRole("HK_STAFF",flow.BranchId,ct)&&!await access.HasRole("OPERATION_MANAGER",flow.BranchId,ct),"REQUEST_CANCEL_DENIED",403);
            await access.Require("IR_CREATE",flow.BranchId,true,ct);actorRole=await Role("HK_STAFF",ct);
            flow.Status="CANCELLED";work.Status="CANCELLED";work.ClosedAt=Now;await Route(flow,null,ct);
        }else{
            actorRole=await HandlerAuthority(flow,ct,allowStaleClaim:action=="claim");
            Output.Require(flow.RequestType is RefillType or ReplenishmentType||action is "claim" or "release","DOMAIN_WORKFLOW_ACTION_REQUIRED",403);
            switch(action){
                case "claim":
                    // Authority was checked under the transaction lock; another active handler still blocks claiming.
                    Output.Require(flow.HandlerUserId!=actor.Id,"REQUEST_ALREADY_CLAIMED");flow.HandlerUserId=actor.Id;break;
                case "release":Output.Require(flow.HandlerUserId==actor.Id,"REQUEST_HANDLER_REQUIRED",403);flow.HandlerUserId=null;break;
                case "reject":
                    Output.Require(flow.Status!="PENDING_RECEIPT","APPROVED_REPLENISHMENT_CANNOT_REJECT");Output.Reason(input.Reason);
                    flow.Status="REJECTED";work.Status="REJECTED";work.ClosedAt=Now;work.CompletionNote=input.Reason!.Trim();
                    if(flow.DocumentId is long rejected){var d=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==rejected,ct);d.Status="CLOSED";d.ClosedAt=Now;}
                    await Route(flow,null,ct);break;
                case "approve":
                    Output.Require(flow.Status is "PENDING_SUPERVISOR" or "PENDING_OPERATIONS_MANAGER","REQUEST_NOT_APPROVABLE");
                    Output.Require(work.CreatedBy!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);
                    if(flow.AllocationId is long allocation)await ActiveAllocation(await db.StaffSupplyAllocations.SingleAsync(a=>a.AllocationId==allocation,ct),ct);
                    var lines=await db.RequestWorkflowLines.Where(l=>l.WorkId==id).ToArrayAsync(ct);
                    var approved=input.ApprovedItems??lines.Select(l=>new SupplyIssueQuantity(l.ItemId,l.RequestedQuantity)).ToArray();Quantities(approved);
                    Output.Require(approved.Count==lines.Length&&approved.All(a=>lines.Any(l=>l.ItemId==a.ItemId&&a.Quantity<=l.RequestedQuantity)),"APPROVED_LINES_MISMATCH");
                    foreach(var line in lines)line.ApprovedQuantity=approved.Single(a=>a.ItemId==line.ItemId).Quantity;
                    if(flow.RequestType==RefillType)flow.Status="PENDING_STOREKEEPER";
                    else{await documents.ApproveWorkflowReplenishment(flow,lines,input.OperationKey,ct);flow.Status="PENDING_RECEIPT";work.Status="IN_PROGRESS";work.ActualStartAt=Now;}
                    await Route(flow,"STOREKEEPER",ct);break;
                case "issue":
                    Output.Require(flow.RequestType==RefillType&&flow.Status=="PENDING_STOREKEEPER"&&flow.AllocationId!=null,"REQUEST_NOT_ISSUABLE");
                    var amounts=await db.RequestWorkflowLines.Where(l=>l.WorkId==id).ToArrayAsync(ct);
                    Output.Require(amounts.Length>0&&amounts.All(l=>l.ApprovedQuantity>0),"APPROVED_LINES_REQUIRED");
                    await supplies.Refill(flow.AllocationId!.Value,id,amounts.Select(l=>new SupplyIssueQuantity(l.ItemId,l.ApprovedQuantity!.Value)).ToArray(),input.OperationKey,ct);
                    flow.Status="FULFILLED";work.Status="CLOSED";work.CompletedAt=Now;work.ClosedAt=Now;await Route(flow,null,ct);break;
                default:throw new ApiException(400,"UNKNOWN_REQUEST_ACTION");
            }
        }
        await Record(flow,work,action,actorRole,input.OperationKey,hash,previous,queue,handler,version,action=="claim"&&handler!=null?"Tiếp nhận từ người xử lý không còn ca làm việc hợp lệ.":input.Reason,ct);
        return await Detail(id,ct);
    },ct);

    public Task<RequestWorkflowDetail> Receive(long id,ReceiveReplenishmentWorkflowRequest input,CancellationToken ct)=>atomic.Run(async()=>{
        await Lock(ct);var (flow,work)=await Load(id,ct);var hash=Hash(input);
        if(await Retry(input.OperationKey,"receive",hash,id,ct)!=null)return await Detail(id,ct);
        Output.Version(flow.RowVersion,input.RowVersion);var role=await HandlerAuthority(flow,ct);
        Output.Require(flow.RequestType==ReplenishmentType&&flow.Status=="PENDING_RECEIPT","APPROVED_REPLENISHMENT_REQUIRED");Quantities(input.Items);
        var previous=flow.Status;var queue=flow.QueueRoleId;var handler=flow.HandlerUserId;var version=flow.RowVersion.ToArray();
        var receipt=await documents.ReceiveWorkflowReplenishment(flow,input,ct);
        var lines=await db.RequestWorkflowLines.Where(l=>l.WorkId==id).ToArrayAsync(ct);var complete=true;
        foreach(var line in lines)complete&=line.DocumentLineId!=null&&await Received(line.DocumentLineId.Value,ct)>=line.ApprovedQuantity;
        if(complete){flow.Status="FULFILLED";work.Status="CLOSED";work.CompletedAt=Now;work.ClosedAt=Now;await Route(flow,null,ct);}
        else flow.HandlerUserId=actor.Id;
        await Record(flow,work,"receive",role,input.OperationKey,hash,previous,queue,handler,version,input.Note,ct,receipt);
        return await Detail(id,ct);
    },ct);

    // Workday must check this before closing an allocation. Resolution is explicit, never silently cancelled.
    internal Task<bool> HasOpenRefill(long allocationId,CancellationToken ct)=>db.RequestWorkflows.AnyAsync(r=>r.AllocationId==allocationId&&r.RequestType==RefillType&&!Finished.Contains(r.Status),ct);
}
