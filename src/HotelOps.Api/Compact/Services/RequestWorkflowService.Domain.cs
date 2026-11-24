using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class RequestWorkflowService
{
    private static bool IsRoomDomain(string type)=>type is "ROOM_DURABLE_ITEM_MISSING" or "ROOM_EQUIPMENT_DAMAGE" or "DURABLE_ITEM_REMEDIATION" or "MAINTENANCE_REMEDIATION" or "ROOM_REINSPECTION";

    private async Task<string> DomainHandlerRole(RequestWorkflow flow,CancellationToken ct)
    {
        Output.Require(flow.ParentRequestWorkId==null,"USE_PARENT_REQUEST_WORKFLOW",409);
        Output.Require(IsRoomDomain(flow.RequestType)&&!Finished.Contains(flow.Status)&&flow.QueueRoleId!=null,"REQUEST_NOT_ACTIONABLE");
        var role=await db.Roles.Where(r=>r.RoleId==flow.QueueRoleId&&r.IsActive).Select(r=>r.Code).SingleOrDefaultAsync(ct);
        var allowed=role switch{
            "HOUSEKEEPING_SUPERVISOR"=>flow.Status is "PENDING_SUPERVISOR" or "ACCEPTED" or "PENDING_REINSPECTION" or "REWORK_REQUIRED",
            "STOREKEEPER"=>flow.Status is "PENDING_STOREKEEPER" or "ACCEPTED" or "IN_PROGRESS" or "REWORK_REQUIRED",
            "MAINTENANCE_SUPERVISOR"=>flow.Status is "PENDING_MAINTENANCE_SUPERVISOR" or "ACCEPTED" or "IN_PROGRESS" or "REWORK_REQUIRED",
            _=>false};
        Output.Require(allowed,"REQUEST_NOT_ACTIONABLE");
        return role!;
    }

    // Call before a specialized mutation in its existing Atomic transaction. The domain still
    // validates its WorkItem row version, command/stage, assignment and object ownership.
    internal async Task RequireDomainHandler(long workId,string expectedRole,IReadOnlyCollection<string> allowedStatuses,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"REQUEST_TRANSACTION_REQUIRED");
        await Lock(ct);
        var flow=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.WorkId==workId,ct)??throw new ApiException(409,"REQUEST_WORKFLOW_NOT_FOUND");
        Output.Require(IsRoomDomain(flow.RequestType)&&allowedStatuses.Contains(flow.Status),"ROOM_ISSUE_WRONG_STAGE");
        Output.Require(await DomainHandlerRole(flow,ct)==expectedRole,"REQUEST_QUEUE_ROLE_REQUIRED",403);
        var roleId=await HandlerAuthority(flow,ct,allowStaleClaim:true);
        if(flow.HandlerUserId==actor.Id)return;
        var work=await db.WorkItems.SingleAsync(w=>w.WorkId==workId&&w.BranchId==flow.BranchId,ct);
        var version=flow.RowVersion.ToArray();var previousHandler=flow.HandlerUserId;flow.HandlerUserId=actor.Id;
        var key=Guid.NewGuid();
        await Record(flow,work,"claim",roleId,key,Hash(new{workId,expectedRole}),flow.Status,flow.QueueRoleId,previousHandler,version,previousHandler==null?null:"Tiếp nhận từ người xử lý không còn ca làm việc hợp lệ.",ct);
    }

    // Called only AFTER the specialized domain service validates and performs its transition,
    // in that SAME transaction. Never exposed as a generic route/status mutation endpoint.
    internal async Task RecordDomainTransition(WorkItem root,string type,string status,string? queueRole,string action,string actorRole,Guid key,string? reason,CancellationToken ct,long? parentRequestId=null,long? documentId=null)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"REQUEST_TRANSACTION_REQUIRED");
        Output.Require(type is "ROOM_DURABLE_ITEM_MISSING" or "ROOM_EQUIPMENT_DAMAGE" or "DURABLE_ITEM_REMEDIATION" or "MAINTENANCE_REMEDIATION" or "ROOM_REINSPECTION","SPECIALIZED_REQUEST_TYPE_REQUIRED",400);
        Output.Require(root.Kind is "INCIDENT" or "MAINTENANCE" or "INSPECTION" or "INTERNAL_REQUEST","INVALID_REQUEST_DOMAIN_ROOT");
        Output.Require(status is "PENDING_SUPERVISOR" or "PENDING_STOREKEEPER" or "PENDING_MAINTENANCE_SUPERVISOR" or "ACCEPTED" or "IN_PROGRESS" or "PENDING_REINSPECTION" or "REWORK_REQUIRED" or "FULFILLED" or "COMPLETED" or "REJECTED" or "CANCELLED","INVALID_REQUEST_STATUS",400);
        Output.Require(queueRole is null or "HOUSEKEEPING_SUPERVISOR" or "STOREKEEPER" or "MAINTENANCE_SUPERVISOR","INVALID_REQUEST_QUEUE_ROLE",400);
        Output.Require(Finished.Contains(status)==(queueRole==null),"REQUEST_QUEUE_REQUIRED");
        Output.Require(!await access.HasRole("OPERATION_MANAGER",root.BranchId,ct),"OM_REQUEST_READ_ONLY",403);
        Output.Require(await access.HasRole(actorRole,root.BranchId,ct),"REQUEST_ACTOR_ROLE_REQUIRED",403);
        if(action is "reject" or "rework" or "fail")Output.Reason(reason);
        var hash=Hash(new{root.WorkId,type,status,queueRole,action,actorRole,reason,parentRequestId,documentId});
        if(await Retry(key,action,hash,root.WorkId,ct)!=null)return;
        var flow=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.WorkId==root.WorkId,ct);
        var previous=flow?.Status;var oldQueue=flow?.QueueRoleId;var oldHandler=flow?.HandlerUserId;var version=flow?.RowVersion.ToArray();
        if(flow==null){
            Guid correlation=Guid.NewGuid();
            if(parentRequestId is long parent){var existing=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.WorkId==parent&&r.BranchId==root.BranchId,ct)??throw new ApiException(409,"PARENT_REQUEST_NOT_FOUND");correlation=existing.CorrelationId;}
            flow=new RequestWorkflow{WorkId=root.WorkId,BranchId=root.BranchId,RequestType=type,Status=status,CorrelationId=correlation,ParentRequestWorkId=parentRequestId,UpdatedAt=Now};db.RequestWorkflows.Add(flow);
        }else Output.Require(flow.RequestType==type&&flow.ParentRequestWorkId==parentRequestId,"REQUEST_ORIGIN_IMMUTABLE");
        flow.Status=status;
        // A same-queue progress update must not transfer an explicit claim to another employee.
        if(queueRole==null||oldQueue!=await Role(queueRole,ct))await Route(flow,queueRole,ct);
        await db.SaveChangesAsync(ct);
        // Historical queue claims are preserved in events; each domain handoff gets a new queue.
        await Record(flow,root,action,await Role(actorRole,ct),key,hash,previous,oldQueue,oldHandler,version,reason,ct,documentId);
    }

    // Parent adds this predicate to generic WorkService mutation authorization, preserving generic
    // request reads. Dedicated typed actions never enter generic WorkService.Act.
    internal Task<bool> IsManagedRequest(long workId,CancellationToken ct)=>db.RequestWorkflows.AnyAsync(r=>r.WorkId==workId&&(r.RequestType==RefillType||r.RequestType==ReplenishmentType||r.RequestType=="DURABLE_ITEM_REMEDIATION"||r.RequestType=="ROOM_REINSPECTION"),ct);
}
