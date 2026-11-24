using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // Migration recovery is explicit: never pretend supplies were issued or consumed historically.
    // An unstarted task uses ordinary reassignment, which establishes a fresh attendance link.
    public Task<object> ReconcileCleaningSupplies(long id,ActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var work=await Load(id,"assign",ct);
        await access.RequireRolePermission("HOUSEKEEPING_SUPERVISOR","CLEANING_ASSIGN",work.BranchId,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
        Output.Key(request.OperationKey);
        Output.Reason(request.Reason);
        var previous=await db.WorkEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(previous!=null){
            Output.Require(previous.EventType=="SUPPLIES_RECONCILED"&&previous.Reason==request.Reason,"IDEMPOTENCY_KEY_REUSED");
            return (object)Output.Record(work);
        }
        Output.Version(work.RowVersion,request.RowVersion);
        Output.Require(CleaningReconciliationPolicy.CanReconcile(work.Kind,work.Status,work.ActualStartAt),"CLEANING_RECONCILIATION_NOT_ALLOWED");
        Output.Require(await db.Rooms.AnyAsync(r=>r.RoomId==work.RoomId&&r.BranchId==work.BranchId&&r.IsActive&&r.TurnoverNo==work.TurnoverNo,ct),"ROOM_CYCLE_NO_LONGER_CURRENT");
        var assignment=await db.WorkAssignments.SingleOrDefaultAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE",ct)
            ??throw new ApiException(409,"CURRENT_ASSIGNEE_REQUIRED");
        await access.EligibleFor(assignment.AssigneeUserId,work.BranchId,"CLEANING_EXECUTE",ct);
        Output.Require(await HousekeepingStaffIds(work.BranchId).ContainsAsync(assignment.AssigneeUserId,ct),"CLEANING_STAFF_REQUIRED");
        await StaffSupplyService.Lock(db,ct);
        Output.Require(!await db.SupplyReservations.AnyAsync(r=>r.WorkId==id&&(r.Status=="RESERVED"||r.Status=="CONSUMED"),ct),"CLEANING_SUPPLIES_ALREADY_RECONCILED");
        await supplies.InitializeLegacyCleaning(id,assignment.AssigneeUserId,request.OperationKey,request.Reason!,ct);
        // Uses only the employee's real, available current allocation. No stock/balance is fabricated.
        // Continuing existing work is allowed after the scheduled end while attendance remains open.
        await supplies.Reserve(id,assignment.AssigneeUserId,request.OperationKey,ct,allowFinishing:true);
        Event(work,"SUPPLIES_RECONCILED",request.OperationKey,request.Reason,work.Status,assignment.AssigneeUserId);
        audit.Add("WorkAssignment",assignment.AssignmentId,"CLEANING_SUPPLIES_RECONCILED",work.BranchId,
            after:new{work.WorkId,assignment.AssigneeUserId,assignment.AssignedAt},key:request.OperationKey);
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        await db.SaveChangesAsync(ct);
        return (object)Output.Record(work);
    },ct);
}

public static class CleaningReconciliationPolicy
{
    public static bool CanReconcile(string kind,string status,DateTime? startedAt)=>
        kind=="CLEANING"&&(status is "IN_PROGRESS" or "ON_HOLD")&&startedAt!=null;
}
