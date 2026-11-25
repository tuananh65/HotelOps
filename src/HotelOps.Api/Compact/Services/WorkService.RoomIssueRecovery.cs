using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // Explicit physical verification of a pre-workflow incident. Preserve its author,
    // timestamps, blocks and earlier events; record recovery at the actual current time.
    public Task<object> RecoverLegacyRoomIssue(long id,LegacyRoomIssueRecoveryRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var work=await Load(id,"read",ct);
        await RequireRoomIssueSupervisor(work.BranchId,ct);
        Output.Require(!await access.HasRole("OWNER",work.BranchId,ct),"REQUEST_READ_ONLY",403);
        await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
        Output.Key(request.OperationKey);
        Output.Require(!string.IsNullOrWhiteSpace(request.Note)&&request.Note.Length<=2000,"REASON_REQUIRED",400);
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        Output.Version(work.RowVersion,request.RowVersion);
        Output.Require(work.Kind=="INCIDENT"&&work.RoomId!=null&&work.Status=="OPEN"&&!await IsRoomIssue(id,ct),"ROOM_ISSUE_WRONG_STAGE");
        Output.Require(!await db.RequestWorkflows.AnyAsync(w=>w.WorkId==id,ct)&&!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE"&&w.Status!="CANCELLED",ct),"LEGACY_INCIDENT_ALREADY_ROUTED");
        Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE",ct),"LEGACY_INCIDENT_ALREADY_ROUTED");
        var room=await db.Rooms.SingleAsync(r=>r.RoomId==work.RoomId&&r.BranchId==work.BranchId,ct);
        Output.Version(room.RowVersion,request.RoomRowVersion);
        var asset=await db.Assets.AsNoTracking().SingleOrDefaultAsync(a=>a.AssetId==request.AssetId&&a.RoomId==room.RoomId&&a.BranchId==room.BranchId&&a.IsActive&&a.RetiredAt==null,ct)
            ??throw new ApiException(409,"ROOM_ISSUE_OBJECT_NOT_IN_ROOM");
        Output.Require(work.AssetId==null||work.AssetId==asset.AssetId,"ROOM_ISSUE_OBJECT_NOT_IN_ROOM");
        var cleaningId=await LegacyIssueCleaningContext(work,ct);
        // Normalize only the ancestry validated above. Queue/detail queries share
        // SOURCE links; retaining just ParentWorkId would hide this inspection.
        var sourceLinkCreated=cleaningId!=null&&!await db.WorkLinks.AnyAsync(l=>l.SourceWorkId==id
            &&l.TargetWorkId==cleaningId&&l.BranchId==work.BranchId&&l.Relation=="SOURCE",ct);
        if(sourceLinkCreated)db.WorkLinks.Add(new WorkLink{BranchId=work.BranchId,SourceWorkId=id,
            TargetWorkId=cleaningId!.Value,Relation="SOURCE",CreatedBy=actor.Id,CreatedAt=Now});
        var before=new{work.Status,work.AssetId};
        work.AssetId=asset.AssetId;work.Status="IN_PROGRESS";
        Event(work,"LEGACY_ROOM_ISSUE_RECOVERED",request.OperationKey,request.Note,before.Status);
        db.WorkEvents.Local.Single(e=>e.WorkId==id&&e.EventType=="LEGACY_ROOM_ISSUE_RECOVERED").SnapshotJson=
            JsonSerializer.Serialize(new IssueData("BROKEN",null,asset.Name,CleaningWorkId:cleaningId,DetectedByUserId:null));
        Event(work,"ROOM_ISSUE_REPAIR_REQUESTED",Guid.NewGuid(),request.Note,before.Status);
        audit.Add("WorkItem",id,"LEGACY_ROOM_ISSUE_RECOVERED",work.BranchId,before,
            new{work.Status,work.AssetId,CleaningWorkId=cleaningId,SourceLinkCreated=sourceLinkCreated},request.OperationKey);
        await NotifyIssueRole(work,"MAINTENANCE_SUPERVISOR","ROOM_ISSUE_REPAIR_REQUESTED",request.OperationKey,ct);
        await db.SaveChangesAsync(ct);
        await requests.RecordDomainTransition(work,"ROOM_EQUIPMENT_DAMAGE","PENDING_MAINTENANCE_SUPERVISOR","MAINTENANCE_SUPERVISOR",
            "recover","HOUSEKEEPING_SUPERVISOR",request.OperationKey,request.Note,ct);
        return (object)Output.Record(work);
    },ct);

    private async Task<long?> LegacyIssueCleaningContext(WorkItem issue,CancellationToken ct)
    {
        // Recover only recorded ancestry, never the room's latest task. An old cycle's
        // context remains historical; CompleteCleaningAfterRemediation checks currentness.
        var candidates=await db.WorkItems.AsNoTracking().Where(w=>
            (w.Kind=="CLEANING"||w.Kind=="REWORK")&&(w.WorkId==issue.ParentWorkId||
                db.WorkLinks.Any(l=>l.SourceWorkId==issue.WorkId&&l.TargetWorkId==w.WorkId
                    &&l.BranchId==issue.BranchId&&l.Relation=="SOURCE"))).ToArrayAsync(ct);
        Output.Require(candidates.Length<=1,"LEGACY_CLEANING_CONTEXT_AMBIGUOUS");
        if(candidates.Length==0)return null;
        var source=candidates[0];
        Output.Require(source.BranchId==issue.BranchId&&source.RoomId==issue.RoomId
            &&(issue.TurnoverNo==null||source.TurnoverNo==issue.TurnoverNo),"LEGACY_CLEANING_CONTEXT_INVALID");
        if(source.Kind=="REWORK"){
            Output.Require(await db.WorkItems.AsNoTracking().AnyAsync(root=>root.WorkId==source.ParentWorkId
                &&root.Kind=="CLEANING"&&root.BranchId==issue.BranchId&&root.RoomId==issue.RoomId
                &&root.TurnoverNo==source.TurnoverNo,ct),"LEGACY_CLEANING_CONTEXT_INVALID");
        }
        return source.WorkId;
    }
}
