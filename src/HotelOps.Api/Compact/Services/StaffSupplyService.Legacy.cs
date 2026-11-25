using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    // Only an explicitly reconciled, already-started pre-cutover cleaning job may initialize
    // missing attendance supplies. This is an issue NOW, never a historical stock adjustment.
    internal async Task InitializeLegacyCleaning(long workId,long userId,Guid key,string reason,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"SUPPLY_TRANSACTION_REQUIRED");
        await Lock(db,ct);
        var work=await db.WorkItems.SingleAsync(w=>w.WorkId==workId,ct);
        await access.RequireRolePermission("HOUSEKEEPING_SUPERVISOR","CLEANING_ASSIGN",work.BranchId,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
        if(await db.StaffSupplyAllocations.AnyAsync(a=>a.UserId==userId&&a.BranchId==work.BranchId&&a.Status!="CLOSED"
            &&db.WorkdayAssignments.Any(d=>d.AssignmentId==a.AssignmentId&&d.Status=="WORKING"),ct))return;
        var cutover=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="P0_ROLE_MIGRATION"&&a.EntityKey=="261124-v1"&&a.Action=="APPLIED")
            .OrderBy(a=>a.OccurredAt).Select(a=>(DateTime?)a.OccurredAt).FirstOrDefaultAsync(ct);
        var days=await (from attendance in db.WorkdayAssignments join template in db.WorkdayShifts on attendance.ShiftId equals template.ShiftId
            join role in db.Roles on template.RoleId equals role.RoleId
            where attendance.UserId==userId&&attendance.Status=="WORKING"&&attendance.EndedAt==null&&template.BranchId==work.BranchId&&role.Code=="HK_STAFF"
            select attendance).ToArrayAsync(ct);
        Output.Require(days.Length==1,"ACTIVE_SHIFT_REQUIRED");
        var day=days[0];
        Output.Require(LegacySupplyRecoveryPolicy.CanInitialize(cutover,day.StartedAt,work.ActualStartAt),"LEGACY_SUPPLY_INITIALIZATION_NOT_ALLOWED");
        Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==workId&&a.AssigneeUserId==userId&&a.AssignmentStatus=="ACTIVE"&&a.AssignedAt<cutover,ct),"LEGACY_SUPPLY_INITIALIZATION_NOT_ALLOWED");
        Output.Require(!await db.StaffSupplyAllocations.AnyAsync(a=>a.AssignmentId==day.AssignmentId,ct),"LEGACY_SUPPLY_INITIALIZATION_NOT_ALLOWED");
        var shift=await db.WorkdayShifts.SingleAsync(s=>s.ShiftId==day.ShiftId,ct);
        await ActiveShift(day,shift,ct,allowFinishing:true);
        var allocation=await Allocate(day,shift,ct);
        audit.Add("StaffSupplyAllocation",allocation.AllocationId,"LEGACY_TASK_SNAPSHOT",work.BranchId,
            after:new{workId,userId,day.AssignmentId,reason,cutover,configurationSnapshot="Current configuration at explicit reconciliation; no historical consumption inferred"},key:key);
        await db.SaveChangesAsync(ct);
    }
}

public static class LegacySupplyRecoveryPolicy
{
    public static bool CanInitialize(DateTime? cutover,DateTime? shiftStarted,DateTime? workStarted)=>
        cutover.HasValue&&shiftStarted.HasValue&&workStarted.HasValue&&shiftStarted<cutover&&workStarted<cutover;
}
