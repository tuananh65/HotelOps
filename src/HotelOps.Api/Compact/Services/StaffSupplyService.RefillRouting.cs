using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    // Upgrade only unfinished midshift refill routing. Historical approvals and other
    // internal/replenishment requests retain their recorded business decisions.
    internal async Task RoutePendingRefillsToStore(CancellationToken ct)
    {
        var flows=await db.RequestWorkflows.Where(r=>r.RequestType==RequestWorkflowService.RefillType
            &&(r.Status=="PENDING_OPERATIONS_MANAGER"||r.Status=="PENDING_SUPERVISOR")
            &&db.StaffSupplyAllocations.Any(a=>a.AllocationId==r.AllocationId&&a.Status!="CLOSED"
                &&db.WorkdayAssignments.Any(d=>d.AssignmentId==a.AssignmentId&&d.Status=="WORKING"))).ToArrayAsync(ct);
        if(flows.Length==0)return;
        var storeRole=await db.Roles.Where(r=>r.Code=="STOREKEEPER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
        foreach(var flow in flows){
            var previous=new{flow.Status,flow.QueueRoleId,flow.HandlerUserId};
            flow.Status="PENDING_STOREKEEPER";flow.QueueRoleId=storeRole;flow.HandlerUserId=null;flow.UpdatedAt=Now;
            var key=Guid.NewGuid();
            db.AuditLogs.Add(new AuditLog{BranchId=flow.BranchId,ActorUserId=null,EntityType="RequestWorkflow",EntityKey=flow.WorkId.ToString(),
                Action="REFILL_ROUTED_TO_STORE",OperationKey=key,OccurredAt=Now,
                BeforeJson=System.Text.Json.JsonSerializer.Serialize(previous),AfterJson=System.Text.Json.JsonSerializer.Serialize(new{flow.Status,flow.QueueRoleId})});
            await new NotificationService(db,actor,access,atomic,clock).ForPermission(flow.BranchId,"MR_FULFILL","REQUEST_WORKFLOW",
                "Yêu cầu bổ sung giữa ca đang chờ thủ kho xác nhận số thực cấp.",flow.WorkId,null,key,ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
