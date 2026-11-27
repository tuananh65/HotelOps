using System.Text.Json;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    internal static DateTime ReturnDeadline(DateTime scheduledAt)=>scheduledAt.AddMinutes(5);
    // Caller owns Workday before StaffSupplies. Never invoke from a supply-only transaction.
    // All returns, releases, cancellations and attendance changes commit or roll back together.
    internal async Task CloseEndedAllocations(long? branchId,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"SUPPLY_TRANSACTION_REQUIRED");
        await Lock(db,ct);
        var allocations=await db.StaffSupplyAllocations.Where(a=>a.Status!="CLOSED"&&(branchId==null||a.BranchId==branchId)
            &&db.WorkdayAssignments.Any(d=>d.AssignmentId==a.AssignmentId&&d.Status=="ENDED"&&d.StartedAt!=null&&d.EndedAt!=null))
            .OrderBy(a=>a.AllocationId).ToArrayAsync(ct);
        foreach(var allocation in allocations){
            var scheduled=await db.AuditLogs.Where(a=>a.EntityType=="StaffSupplyAllocation"&&a.EntityKey==allocation.AllocationId.ToString()
                &&a.Action=="RETURN_SCHEDULED").OrderBy(a=>a.AuditId).FirstOrDefaultAsync(ct);
            if(scheduled==null){await ScheduleReturn(allocation,Guid.NewGuid(),ct);continue;}
            if(Now<ReturnDeadline(scheduled.OccurredAt))continue;
            await CloseAllocation(allocation,Guid.NewGuid(),true,ct);
        }
    }

    private async Task ScheduleReturn(StaffSupplyAllocation allocation,Guid key,CancellationToken ct)
    {
        if(await db.AuditLogs.AnyAsync(a=>a.EntityType=="StaffSupplyAllocation"&&a.EntityKey==allocation.AllocationId.ToString()&&a.Action=="RETURN_SCHEDULED",ct))return;
        await CancelEndedRefills(allocation,key,ct);
        SystemSupplyAudit(allocation,"RETURN_SCHEDULED",key,new{allocation.AssignmentId,dueAt=ReturnDeadline(Now)});
        await db.SaveChangesAsync(ct);
    }

    // Both manual end and expiry hold Workday -> StaffSupplies in the outer transaction.
    private async Task CloseAllocation(StaffSupplyAllocation allocation,Guid key,bool automatic,CancellationToken ct)
    {
        var assignment=await db.WorkdayAssignments.SingleAsync(a=>a.AssignmentId==allocation.AssignmentId,ct);
        await CancelEndedRefills(allocation,key,ct);
        var balances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
        var ids=balances.Select(b=>b.BalanceId).ToArray();
        var holds=await (from r in db.SupplyReservations join work in db.WorkItems on r.WorkId equals work.WorkId
            where ids.Contains(r.BalanceId)&&r.Status=="RESERVED"
            select new{Reservation=r,work.CompletedAt,work.Status}).ToArrayAsync(ct);
        // A reservation is not posted consumption, even for completed legacy work.
        // Release the hold without inventing USE; return only the canonical remainder.
        foreach(var hold in holds){
            var reservation=hold.Reservation;
            var balance=balances.Single(b=>b.BalanceId==reservation.BalanceId);
            Output.Require(balance.Reserved>=reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
            balance.Reserved-=reservation.Quantity;
            reservation.Status="RELEASED";reservation.ResolvedAt=Now;
            if(reservation.Quantity>0)Ledger(balance,"RELEASE",reservation.Quantity,key,reservation.WorkId,reservation.ReservationId,authorizingUserId:automatic?assignment.CreatedBy:null);
        }
        Output.Require(balances.All(b=>b.Reserved==0),"SUPPLY_BALANCE_INCONSISTENT");
        // Persist the released holds before the canonical stock provenance trigger reads them.
        await db.SaveChangesAsync(ct);
        var consumables=await db.ItemLifecycleProfiles.Where(p=>p.BranchId==allocation.BranchId&&p.Classification=="CONSUMABLE")
            .Select(p=>p.ItemId).ToArrayAsync(ct);
        var remaining=balances.Where(b=>consumables.Contains(b.ItemId)&&SupplyMath.Remaining(b)>0)
            .Select(b=>(b,SupplyMath.Remaining(b))).ToArray();
        if(remaining.Length>0){
            var lines=await documents.PostStaffSupply(allocation,remaining,"RETURN",key,null,ct,automatic?assignment.CreatedBy:null);
            foreach(var (balance,quantity) in remaining){
                balance.Returned+=quantity;
                Ledger(balance,"RETURN",quantity,key,documentLineId:lines[balance.BalanceId],authorizingUserId:automatic?assignment.CreatedBy:null);
            }
        }
        await ReturnReusable(allocation,key,ct,automatic?assignment.CreatedBy:null);
        allocation.Status="CLOSED";allocation.ClosedAt=Now;allocation.ShortageReason=null;
        SystemSupplyAudit(allocation,"RETURNED_CLOSED",key,new{allocation.AssignmentId,assignment.CreatedBy,automatic,
            returned=remaining.Select(x=>new{x.b.ItemId,quantity=x.Item2}),
            releasedReservations=holds.Select(h=>new{h.Reservation.ReservationId,h.Reservation.WorkId,h.CompletedAt,h.Status}),
            legacyPolicy="RESERVED is not USE; release remaining holds without changing posted consumption"});
        await db.SaveChangesAsync(ct);
    }

    private async Task CancelEndedRefills(StaffSupplyAllocation allocation,Guid key,CancellationToken ct)
    {
        var flows=await db.RequestWorkflows.Where(r=>r.AllocationId==allocation.AllocationId&&r.RequestType==RequestWorkflowService.RefillType
            &&r.Status!="FULFILLED"&&r.Status!="COMPLETED"&&r.Status!="REJECTED"&&r.Status!="CANCELLED").ToArrayAsync(ct);
        foreach(var flow in flows){
            var before=new{flow.Status,flow.QueueRoleId,flow.HandlerUserId};
            var work=await db.WorkItems.SingleAsync(w=>w.WorkId==flow.WorkId,ct);
            flow.Status="CANCELLED";flow.QueueRoleId=null;flow.HandlerUserId=null;flow.UpdatedAt=Now;
            work.Status="CANCELLED";work.ClosedAt=Now;
            // Request events require a human role/actor. Preserve them and use the existing
            // nullable-actor audit contract for a truthful system cancellation instead.
            db.AuditLogs.Add(new AuditLog{BranchId=allocation.BranchId,ActorUserId=null,EntityType="RequestWorkflow",
                EntityKey=flow.WorkId.ToString(),Action="AUTO_CANCEL_SHIFT_ENDED",OperationKey=key,
                BeforeJson=JsonSerializer.Serialize(before),AfterJson=JsonSerializer.Serialize(new{flow.Status,allocation.AssignmentId,
                    reason="Shift ended before refill fulfillment"}),OccurredAt=Now});
            new NotificationService(db,actor,access,atomic,clock).Add(allocation.BranchId,allocation.UserId,"REQUEST_WORKFLOW",
                "Yêu cầu cấp bù đã hủy vì ca làm việc kết thúc.",flow.WorkId,null,key);
        }
        await db.SaveChangesAsync(ct);
    }

    private void SystemSupplyAudit(StaffSupplyAllocation allocation,string action,Guid key,object after)=>
        db.AuditLogs.Add(new AuditLog{BranchId=allocation.BranchId,ActorUserId=null,EntityType="StaffSupplyAllocation",
            EntityKey=allocation.AllocationId.ToString(),Action=action,OperationKey=key,AfterJson=JsonSerializer.Serialize(after),OccurredAt=Now});
}
