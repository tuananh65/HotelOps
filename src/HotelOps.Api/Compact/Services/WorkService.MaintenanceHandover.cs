using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<IQueryable<WorkdayAssignment>> CurrentMaintenanceShift(long branchId,CancellationToken ct)
    {
        var timezone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branchId&&b.IsActive).Select(b=>b.Timezone).SingleAsync(ct);
        var local=TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById(timezone));
        var today=DateOnly.FromDateTime(local.DateTime);var yesterday=today.AddDays(-1);
        var minute=local.Hour*60+local.Minute;var now=Now;
        return from day in db.WorkdayAssignments.AsNoTracking()
            join shift in db.WorkdayShifts on day.ShiftId equals shift.ShiftId
            join role in db.Roles on shift.RoleId equals role.RoleId
            where shift.BranchId==branchId&&shift.IsActive&&role.IsActive&&role.Code=="MAINTENANCE_STAFF"
                &&day.Status=="WORKING"&&day.StartedAt!=null&&day.StartedAt<=now&&day.EndedAt==null
                &&((day.WorkDate==today&&shift.StartMinute<=minute&&minute<shift.EndMinute)
                    ||(day.WorkDate==yesterday&&shift.StartMinute<=minute+1440&&minute+1440<shift.EndMinute))
                &&db.Departments.Any(d=>d.DepartmentId==shift.DepartmentId&&d.BranchId==branchId&&d.IsActive)
                &&db.Memberships.Any(m=>m.UserId==day.UserId&&m.BranchId==branchId&&m.Kind=="EMPLOYMENT"
                    &&m.DepartmentId==shift.DepartmentId&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select day;
    }

    private async Task RequireMaintenanceAssignee(long userId,long branchId,CancellationToken ct)
    {
        await access.EligibleFor(userId,branchId,"MAINTENANCE_EXECUTE",ct);
        Output.Require(await (await CurrentMaintenanceShift(branchId,ct)).AnyAsync(a=>a.UserId==userId,ct),"MAINTENANCE_ASSIGNEE_NOT_ON_SHIFT");
        Output.Require(await EligibleMaintenanceAssignee(userId,branchId,ct),"MAINTENANCE_ASSIGNEE_REQUIRED",403);
    }

    internal static bool MaintenanceBeforeAcceptance(WorkItem work,WorkAssignment? assignment,bool hasStarted)=>
        work.Kind=="MAINTENANCE"&&work.Status=="PENDING"&&work.ActualStartAt==null&&!hasStarted
        &&assignment is {AssignmentStatus:"ACTIVE",AcceptedAt:null};

    private async Task<bool> CanRequestMaintenanceHandover(WorkItem work,CancellationToken ct)
    {
        if(work.Kind!="MAINTENANCE"||!await access.HasRole("MAINTENANCE_STAFF",work.BranchId,ct)
            ||await access.HasRole("MAINTENANCE_SUPERVISOR",work.BranchId,ct)
            ||!await access.Allows("MAINTENANCE_EXECUTE",work.BranchId,true,ct:ct)
            ||!await (await CurrentMaintenanceShift(work.BranchId,ct)).AnyAsync(a=>a.UserId==actor.Id,ct))return false;
        var assignment=await db.WorkAssignments.AsNoTracking().SingleOrDefaultAsync(a=>a.WorkId==work.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct);
        return MaintenanceBeforeAcceptance(work,assignment,await db.WorkIntervals.AnyAsync(t=>t.WorkId==work.WorkId,ct)
                ||await db.WorkEvents.AnyAsync(e=>e.WorkId==work.WorkId&&e.EventType=="START",ct))
            &&!await CleaningContinuationAccess.HasPendingHandover(db,work.WorkId,ct);
    }

    private async Task<object> MaintenanceHandover(long id,string command,ActionRequest request,CancellationToken ct)
    {
        await StaffSupplyService.Lock(db,ct);
        var work=await Load(id,"read",ct);
        Output.Require(work.Kind=="MAINTENANCE","MAINTENANCE_KIND_REQUIRED",400);
        await RequireWorkMutation(work.Kind,work.BranchId,ct);
        var supervisor=command=="accept-handover-request";
        if(supervisor){
            await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_ASSIGN",work.BranchId,ct);
            await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"MAINTENANCE_SUPERVISOR",ct);
        }else await Load(id,"execute",ct);
        Output.Key(request.OperationKey);Output.Reason(request.Reason);
        var payload=JsonSerializer.Serialize(new{command,request});
        var prior=await db.WorkEvents.SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(prior!=null){Output.Require(prior.SnapshotJson==payload,"IDEMPOTENCY_KEY_REUSED");return Output.Record(work);}
        Output.Version(work.RowVersion,request.RowVersion);
        var assignment=await db.WorkAssignments.SingleOrDefaultAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE",ct);
        if(!supervisor)Output.Require(assignment?.AssigneeUserId==actor.Id,"CURRENT_ASSIGNEE_REQUIRED",403);
        Output.Require(MaintenanceBeforeAcceptance(work,assignment,await db.WorkIntervals.AnyAsync(t=>t.WorkId==id,ct)
            ||await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.EventType=="START",ct)),"MAINTENANCE_HANDOVER_BEFORE_ACCEPT_ONLY");
        var pending=await CleaningContinuationAccess.HasPendingHandover(db,id,ct);
        if(supervisor){
            Output.Require(pending,"HANDOVER_REQUEST_REQUIRED");
            var offered=await db.WorkEvents.Where(e=>e.WorkId==id&&e.EventType=="SUPERVISOR_HANDOVER_REQUESTED"
                &&e.ActorUserId==assignment!.AssigneeUserId&&e.OccurredAt>=assignment.AssignedAt)
                .OrderByDescending(e=>e.WorkEventId).FirstAsync(ct);
            assignment!.AssignmentStatus="REASSIGNED";assignment.EndedAt=Now;
            Event(work,"SUPERVISOR_HANDOVER_ACCEPTED",request.OperationKey,request.Reason,work.Status,null,offered.WorkEventId);
            notifications.Add(work.BranchId,assignment.AssigneeUserId,"WORK_HANDOVER_ACCEPTED","Giám sát bảo trì đã nhận bàn giao.",id,null,request.OperationKey);
        }else{
            Output.Require(!pending,"HANDOVER_REQUEST_ALREADY_OPEN");
            Event(work,"SUPERVISOR_HANDOVER_REQUESTED",request.OperationKey,request.Reason,work.Status);
            await notifications.ForPermission(work.BranchId,"MAINTENANCE_ASSIGN","WORK_HANDOVER_REQUESTED","Nhân viên đề nghị bàn giao: "+request.Reason,id,null,request.OperationKey,ct);
        }
        db.WorkEvents.Local.Single(e=>e.WorkId==id&&e.OperationKey==request.OperationKey).SnapshotJson=payload;
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        await db.SaveChangesAsync(ct);return Output.Record(work);
    }
}
