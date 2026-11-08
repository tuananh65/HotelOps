using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReadReworkSourcePreviewAsync(SessionReference ticket,long sid,long branch,long inspection,CancellationToken ct)
        => BatchAsync(ticket,sid,branch,async(_,token)=>await ReworkSourceSnapshotAsync(branch,inspection,null,token),ct,
            requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");

    private async Task<RoomTypeBatchResult> ReworkSourceSnapshotAsync(long branch,long inspection,string? expected,CancellationToken ct)
    {
        if(!await HousekeepingInstalled(ct)||!await LifecycleInstalled(ct))return new(RoomTypeBatchStatus.SchemaRequired);
        if(!await InspectionEvidenceRevisionInstalled(ct))return new(RoomTypeBatchStatus.SchemaRequired);
        var rooms=await HistoryRooms(ct);if(rooms==null)return new(RoomTypeBatchStatus.SchemaRequired);
        var scope=rooms.Where(r=>r.BranchId==branch&&r.IsActive&&r.Floor.IsActive&&(r.AreaId==null||r.Area!.IsActive)).Select(r=>r.RoomId);
        var row=await db.RoomInspections.AsNoTracking().SingleOrDefaultAsync(i=>i.InspectionId==inspection&&scope.Contains(i.RoomId),ct);
        if(row==null)return new(RoomTypeBatchStatus.NotFound);
        var state=await InspectionState(inspection,ct);if(expected!=null&&expected!=WorkflowTag(state.Revision))return new(RoomTypeBatchStatus.Stale);
        if(row.Status!="COMPLETED"||row.Result!="FAIL"||state.State!="FAILED"||row.InspectedAt==null||row.CleaningTaskId==null)return new(RoomTypeBatchStatus.Inactive);
        var task=await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(t=>t.CleaningTaskId==row.CleaningTaskId&&t.RoomId==row.RoomId,ct);
        if(task==null||(await CleaningState(task.CleaningTaskId,ct)).State!="REWORK_REQUIRED")return new(RoomTypeBatchStatus.Inactive);
        var current=await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(ct);
        if(current!=task.CleaningTaskId)return new(RoomTypeBatchStatus.Inactive);
        if(await db.RoomInspections.AnyAsync(i=>i.CleaningTaskId==task.CleaningTaskId&&i.InspectionId!=inspection&&i.Status!="COMPLETED"&&i.Status!="CANCELLED",ct))return new(RoomTypeBatchStatus.Inactive);
        var defects=await db.InspectionDefects.AsNoTracking().Where(d=>d.InspectionId==inspection&&d.ChecklistItemId!=null
            && d.ChecklistItem!.VersionId==row.VersionId&&db.InspectionResults.Any(r=>r.InspectionId==inspection&&r.ChecklistItemId==d.ChecklistItemId&&r.Outcome=="FAIL"))
            .OrderBy(d=>d.DefectId).Select(d=>d.DefectId).ToArrayAsync(ct);
        if(defects.Length==0)return new(RoomTypeBatchStatus.InvalidReference);
        var evidence=await db.Attachments.AsNoTracking().Where(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection&&a.DeletedAt==null)
            .OrderBy(a=>a.AttachmentId).Select(a=>a.AttachmentId).ToArrayAsync(ct);
        return new(RoomTypeBatchStatus.Success,new ReworkSourcePreview(inspection,branch,row.RoomId,task.CleaningTaskId,row.VersionId,
            WorkflowTag(state.Revision),defects,evidence,false,ReworkWriteDependencies),WorkflowTag(state.Revision));
    }
}
