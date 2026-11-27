using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    // Upgrade already accepted work using real custody. Never rewrite posted historical USE,
    // backdate a movement, or charge an unaccepted assignment during synchronization.
    internal async Task RecoverAcceptedConsumption(long? branchId,CancellationToken ct)
    {
        var candidates=await (from allocation in db.StaffSupplyAllocations
            join day in db.WorkdayAssignments on allocation.AssignmentId equals day.AssignmentId
            where allocation.Status!="CLOSED"&&day.Status=="WORKING"
                &&(branchId==null||allocation.BranchId==branchId)
            select new{Allocation=allocation,day.CreatedBy}).ToArrayAsync(ct);
        foreach(var candidate in candidates){
            var allocation=candidate.Allocation;
            var work=await db.WorkItems.Where(w=>w.BranchId==allocation.BranchId&&w.Kind=="CLEANING"
                &&(w.Status=="ASSIGNED"||w.Status=="IN_PROGRESS"||w.Status=="ON_HOLD")
                &&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssignmentStatus=="ACTIVE"
                    &&a.AssigneeUserId==allocation.UserId&&a.AcceptedAt!=null)
                &&!db.StaffSupplyLedgers.Any(l=>l.WorkId==w.WorkId&&l.Kind=="USE")
                &&!db.AuditLogs.Any(a=>a.EntityType=="WorkItem"&&a.EntityKey==w.WorkId.ToString()
                    &&(a.Action=="SUPPLIES_CONFIRMED"||a.Action=="SUPPLIES_CONSUMED_AT_ACCEPTANCE")))
                .OrderBy(w=>w.ActualStartAt).ThenBy(w=>w.WorkId).ToArrayAsync(ct);
            foreach(var item in work){
                try{
                    await ConsumeAcceptedWork(item,allocation.UserId,Guid.NewGuid(),ct,allocation,candidate.CreatedBy);
                }catch(ApiException error) when(error.Code is "STAFF_SUPPLIES_INSUFFICIENT" or "SUPPLY_CONFIGURATION_REQUIRED"
                    or "REUSABLE_ROOM_DISCREPANCY" or "REUSABLE_SHELF_REVIEW_REQUIRED"){
                    // No stock/hold writes occur until every item passes preflight. An actual
                    // shortage must not stop attendance expiry or fabricate consumption.
                    if(!await db.AuditLogs.AnyAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==item.WorkId.ToString()
                        &&a.Action=="ACCEPTED_SUPPLIES_SHORT"&&a.AfterJson!=null&&a.AfterJson.Contains("\"allocationId\":"+allocation.AllocationId+","),ct)){
                        db.AuditLogs.Add(new AuditLog{BranchId=item.BranchId,ActorUserId=null,EntityType="WorkItem",
                            EntityKey=item.WorkId.ToString(),Action="ACCEPTED_SUPPLIES_SHORT",OccurredAt=Now,
                            AfterJson=System.Text.Json.JsonSerializer.Serialize(new{allocationId=allocation.AllocationId,error.Code})});
                        await db.SaveChangesAsync(ct);
                    }
                }
            }
        }
    }
}
