using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private Task<bool> IsUnrepairable(long id,CancellationToken ct)=>db.WorkItems.AsNoTracking()
        .AnyAsync(w=>w.WorkId==id&&w.Kind=="MAINTENANCE"&&w.Status=="COMPLETED"&&w.Result=="FAIL",ct);

    private Task<object> CompleteUnrepairable(long id,ActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await StaffSupplyService.Lock(db,ct);
        var work=await Load(id,"unrepairable",ct);
        Output.Require(work.Kind=="MAINTENANCE"&&work.AssetId!=null,"ASSET_MAINTENANCE_REQUIRED",400);
        Output.Key(request.OperationKey);Output.Reason(request.Reason);
        var previous=await db.WorkEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(previous!=null){
            Output.Require(previous.EventType=="MAINTENANCE_UNREPAIRABLE"&&previous.Reason==request.Reason&&CleaningReportMatches(previous.SnapshotJson,request.AttachmentIds),"IDEMPOTENCY_KEY_REUSED");
            return (object)Output.Record(work);
        }
        Output.Version(work.RowVersion,request.RowVersion);
        Output.Require(work.Status=="IN_PROGRESS","WORK_NOT_EXECUTING");
        Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt!=null,ct),"CURRENT_ASSIGNEE_REQUIRED",403);
        var assignment=await db.WorkAssignments.SingleAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE",ct);
        var report=await ValidateMaintenanceCompletionReport(work,assignment,request,ct);
        await RequireReplacementSettled(id,ct);
        await CloseTime(work,"COMPLETE",ct);
        var before=work.Status;
        work.Status="COMPLETED";work.Result="FAIL";work.CompletedAt=Now;work.ProgressPercent=100;work.CompletionNote=request.Reason;
        await EndAssignments(work,Now,ct);
        Event(work,"MAINTENANCE_UNREPAIRABLE",request.OperationKey,request.Reason,before);
        db.WorkEvents.Local.Single(e=>e.WorkId==id&&e.OperationKey==request.OperationKey).SnapshotJson=report;
        await SetMaintenanceAssetCondition(work,"FAULTY",request.OperationKey,request.Reason,ct);
        await NotifyReplacement(work,"MAINTENANCE_SUPERVISOR",request.OperationKey,ct);
        // End the attempt, not its incident. Keep blocks, incident state and room readiness unchanged.
        await db.SaveChangesAsync(ct);
        return (object)Output.Record(work);
    },ct);

    private async Task MaintenanceSucceeded(WorkItem work,ActionRequest request,CancellationToken ct,string actingRole="MAINTENANCE_STAFF")
    {
        await SetMaintenanceAssetCondition(work,"GOOD",request.OperationKey,request.Reason,ct);
        Event(work,"REPAIR_SUCCEEDED",Guid.NewGuid(),request.Reason,work.Status);
        if(work.ParentWorkId is long parentId){
            var incident=await db.WorkItems.SingleAsync(w=>w.WorkId==parentId&&w.BranchId==work.BranchId&&w.Kind=="INCIDENT",ct);
            Output.Require(!WorkRules.Terminal.Contains(incident.Status),"ACTIVE_INCIDENT_REQUIRED");
            if(await IsRoomIssue(parentId,ct)){
                Output.Require(await IssueStage(parentId,ct)=="ROOM_ISSUE_REPAIR_CREATED","ROOM_ISSUE_WRONG_STAGE");
                // This is the worker's recorded result; HK still independently checks room readiness.
                Event(work,"TECHNICAL_REVIEW_PASSED",Guid.NewGuid(),request.Reason,work.Status);
                var before=incident.Status;incident.Status="RESOLVED";
                var handoffKey=Guid.NewGuid();
                Event(incident,"ROOM_ISSUE_INSPECTION_REQUESTED",handoffKey,request.Reason,before);
                db.Entry(incident).Property(w=>w.Title).IsModified=true;
                await db.SaveChangesAsync(ct);
                await requests.RecordDomainTransition(incident,"ROOM_EQUIPMENT_DAMAGE","PENDING_REINSPECTION","HOUSEKEEPING_SUPERVISOR","repair-succeeded",actingRole,handoffKey,request.Reason,ct);
                var input=new RoomIssueActionRequest(Convert.ToBase64String(incident.RowVersion),handoffKey,request.Reason);
                await RecordRoomIssueChildren(incident,"BROKEN","report-remedy",actingRole,input,null,ct);
                await NotifyIssueRole(incident,"HOUSEKEEPING_SUPERVISOR","ROOM_ISSUE_INSPECTION_REQUESTED",handoffKey,ct);
            }else{
                Output.Require(!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==parentId&&w.WorkId!=work.WorkId&&w.Kind=="MAINTENANCE"&&w.Status!="CANCELLED"&&w.Status!="CLOSED"&&(w.Status!="COMPLETED"||w.Result!="PASS")
                    &&!(w.Status=="COMPLETED"&&w.Result=="FAIL"&&db.WorkLinks.Any(l=>l.TargetWorkId==w.WorkId&&l.SourceWorkId==work.WorkId&&l.Relation=="SOURCE")),ct),"MAINTENANCE_STILL_REQUIRED");
                var before=incident.Status;incident.Status="RESOLVED";incident.CompletionNote=request.Reason;
                Event(incident,"REPAIR_SUCCEEDED",Guid.NewGuid(),request.Reason,before);
                await notifications.ForPermission(work.BranchId,"INSPECTION_PERFORM","REPAIR_SUCCEEDED",$"Đã sửa chữa: {work.Title}",parentId,null,request.OperationKey,ct);
            }
        }else await notifications.ForPermission(work.BranchId,"INSPECTION_PERFORM","REPAIR_SUCCEEDED",$"Đã sửa chữa: {work.Title}",work.WorkId,null,request.OperationKey,ct);
        // No READY transition or block release occurs here.
        await db.SaveChangesAsync(ct);
    }

    private async Task SetMaintenanceAssetCondition(WorkItem work,string condition,Guid key,string? note,CancellationToken ct)
    {
        if(work.AssetId==null)return;
        var asset=await db.Assets.SingleAsync(a=>a.AssetId==work.AssetId&&a.BranchId==work.BranchId,ct);
        if(condition=="GOOD"&&await db.WorkItems.AnyAsync(w=>w.BranchId==work.BranchId&&w.AssetId==work.AssetId&&w.Kind=="INCIDENT"
            &&w.WorkId!=work.ParentWorkId&&w.Status!="CLOSED"&&w.Status!="CANCELLED"&&w.Status!="RESOLVED",ct))return;
        if(asset.ConditionCode==condition)return;
        db.AssetEvents.Add(new AssetEvent{BranchId=work.BranchId,AssetId=asset.AssetId,EventType="CONDITION",SourceWorkId=work.WorkId,
            OldCondition=asset.ConditionCode,NewCondition=condition,ActorUserId=actor.Id,OperationKey=key,Note=note,OccurredAt=Now});
        asset.ConditionCode=condition;
        audit.Add("Asset",asset.AssetId,"MAINTENANCE_RESULT",work.BranchId,after:new{condition,work.WorkId},key:key);
    }
}
