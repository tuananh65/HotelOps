using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<object> SupervisorHandover(long id,string command,ActionRequest request,CancellationToken ct)
    {
        await StaffSupplyService.Lock(db,ct);
        var work=await Load(id,"read",ct);
        Output.Require(work.Kind is "CLEANING" or "REWORK","CLEANING_WORK_REQUIRED",400);
        var assignment=await db.WorkAssignments.SingleOrDefaultAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE",ct);
        var supervisor=command=="accept-handover-request";
        if(supervisor){
            await access.RequireRolePermission("HOUSEKEEPING_SUPERVISOR","CLEANING_ASSIGN",work.BranchId,ct);
            await ShiftCommandAccess.Require(db,actor.Id,work.BranchId,Now,ct);
        }else{
            await access.Require("CLEANING_EXECUTE",work.BranchId,true,ct);
            Output.Require(await access.HasRole("HK_STAFF",work.BranchId,ct),"HK_STAFF_REQUIRED",403);
            Output.Require(assignment?.AssigneeUserId==actor.Id,"CURRENT_ASSIGNEE_REQUIRED",403);
        }
        Output.Key(request.OperationKey);Output.Reason(request.Reason);
        var payload=JsonSerializer.Serialize(new{command,request});
        var prior=await db.WorkEvents.SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(prior!=null){Output.Require(prior.SnapshotJson==payload,"IDEMPOTENCY_KEY_REUSED");return Output.Record(work);}
        Output.Version(work.RowVersion,request.RowVersion);
        Output.Require(CleaningContinuationAccess.IsBeforeAcceptance(work,assignment,
            await db.WorkIntervals.AnyAsync(t=>t.WorkId==id,ct)||await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.EventType=="START",ct)),"HOUSEKEEPING_HANDOVER_BEFORE_ACCEPT_ONLY");
        var offered=await db.WorkEvents.Where(e=>e.WorkId==id&&e.EventType=="SUPERVISOR_HANDOVER_REQUESTED")
            .OrderByDescending(e=>e.WorkEventId).FirstOrDefaultAsync(ct);
        var pending=offered!=null&&assignment!=null&&offered.ActorUserId==assignment.AssigneeUserId&&offered.OccurredAt>=assignment.AssignedAt
            &&!await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.EventType=="SUPERVISOR_HANDOVER_ACCEPTED"&&e.RelatedEventId==offered.WorkEventId,ct);
        var before=work.Status;
        if(supervisor){
            Output.Require(pending,"HANDOVER_REQUEST_REQUIRED");
            await CloseTime(work,"REASSIGN",ct);
            assignment!.AssignmentStatus="REASSIGNED";assignment.EndedAt=Now;
            work.Status=work.Kind=="CLEANING"?"PENDING_ASSIGNMENT":"PENDING_ACCEPTANCE";
            Event(work,"SUPERVISOR_HANDOVER_ACCEPTED",request.OperationKey,request.Reason,before,null,offered!.WorkEventId);
            notifications.Add(work.BranchId,assignment.AssigneeUserId,"WORK_HANDOVER_ACCEPTED","Giám sát đã nhận bàn giao; bạn có thể kết thúc ca khi không còn việc mở.",id,null,request.OperationKey);
        }else{
            Output.Require(!pending,"HANDOVER_REQUEST_ALREADY_OPEN");
            Output.Require(offered==null||offered.ActorUserId!=assignment!.AssigneeUserId||offered.OccurredAt<assignment.AssignedAt,"HANDOVER_ALREADY_REQUESTED");
            Event(work,"SUPERVISOR_HANDOVER_REQUESTED",request.OperationKey,request.Reason,before);
            await notifications.ForPermission(work.BranchId,"CLEANING_ASSIGN","WORK_HANDOVER_REQUESTED","Nhân viên đề nghị bàn giao: "+request.Reason,id,null,request.OperationKey,ct);
        }
        db.WorkEvents.Local.Single(e=>e.WorkId==id&&e.OperationKey==request.OperationKey).SnapshotJson=payload;
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        await db.SaveChangesAsync(ct);
        return Output.Record(work);
    }
}
