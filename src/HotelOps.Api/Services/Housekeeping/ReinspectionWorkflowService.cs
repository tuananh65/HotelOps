using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Candidate-only application workflow: owns one existing BatchAsync transaction, real
    // SQL session/grant rechecks and persisted source. Not mounted in production MVC.
    internal Task<RoomTypeBatchResult> ListReinspectionWorkflowAsync(SessionReference ticket,long sid,long branch,CancellationToken ct)
        => ReinspectionWorkflowBatch(ticket,sid,branch,async token=>{
            var rows=await db.Database.SqlQuery<WorkflowListRow>($"""
                SELECT b.inspection_id AS InspectionId,b.rework_id AS ReworkId,b.original_inspection_id AS OriginalInspectionId,
                  b.room_id AS RoomId,b.assigned_version_id AS VersionId
                FROM dbo.fe05_reinspection_bindings_v1 b
                JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id={ticket.UserId}
                JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.cleaning_task_id=b.cleaning_task_id
                JOIN dbo.rooms r ON r.room_id=b.room_id AND r.branch_id={branch} AND r.is_active=1
                JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.is_active=1
                LEFT JOIN dbo.areas a ON a.area_id=r.area_id
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=b.room_id AND c.cleaning_task_id=b.cleaning_task_id AND c.row_version=b.cycle_revision
                WHERE b.branch_id={branch} AND b.reinspector_user_id={ticket.UserId}
                  AND (r.area_id IS NULL OR a.is_active=1) AND w.workflow_status='PENDING_REINSPECTION' AND w.row_version=b.rework_revision
                  AND i.workflow_status IN('ASSIGNED','INSPECTING','PENDING_CONCLUSION')
                ORDER BY b.inspection_id,b.rework_id
                """).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success,rows.Select(x=>new ReinspectionWorkflowItem(x.InspectionId,x.ReworkId,x.OriginalInspectionId,x.RoomId,x.VersionId)).ToArray());
        },ct);

    internal Task<RoomTypeBatchResult> ReadReinspectionWorkflowAsync(SessionReference ticket,long sid,long branch,long inspection,long rework,CancellationToken ct)
        => ReinspectionWorkflowBatch(ticket,sid,branch,token=>WorkflowDetails(ticket,sid,branch,inspection,rework,token),ct);

    internal Task<RoomTypeBatchResult> BindReinspectionWorkflowAsync(SessionReference ticket,long sid,long branch,long inspection,ReinspectionBindCandidateRequest input,CancellationToken ct)
        => ReinspectionWorkflowBatch(ticket,sid,branch,async token=>{
            // The component performs authoritative persisted loading, not client snapshot acceptance.
            await BindReinspectionComponentInCallerTransactionAsync(ticket,sid,branch,inspection,input.ReworkId,input.InspectionRevision,input.ReworkRevision,token);
            return await WorkflowDetails(ticket,sid,branch,inspection,input.ReworkId,token);
        },ct);

    internal Task<RoomTypeBatchResult> SaveReinspectionWorkflowAsync(SessionReference ticket,long sid,long branch,long inspection,string expected,ReinspectionSaveCandidateRequest input,CancellationToken ct)
        => ReinspectionWorkflowBatch(ticket,sid,branch,async token=>{
            if(input.Decisions==null)throw new InvalidOperationException("REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED");
            await RequireUnreviewedProposalSource(ticket,sid,branch,inspection,input.ReworkId,token);
            await SaveReinspectionComponentInCallerTransactionAsync(ticket,sid,branch,inspection,input.ReworkId,expected,input.ReworkRevision,input.BindingRevision,
                input.Decisions.Select(d=>new ReinspectionDefectDecision(d.DefectId,d.Fixed)).ToArray(),token);
            return await WorkflowDetails(ticket,sid,branch,inspection,input.ReworkId,token);
        },ct);

    private Task<RoomTypeBatchResult> ReinspectionWorkflowBatch(SessionReference ticket,long sid,long branch,Func<CancellationToken,Task<RoomTypeBatchResult>> run,CancellationToken ct)
        => BatchAsync(ticket,sid,branch,async(_,token)=>{
            try{await RequireReinspectionComponentSchemaAsync(token);return await run(token);}
            catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number is 2601 or 2627){return new(RoomTypeBatchStatus.Duplicate);}
            catch(RejectInspectionOwnershipException){return new(RoomTypeBatchStatus.Forbidden);}
            catch(RejectInspectionSourceUnavailable){return new(RoomTypeBatchStatus.Unavailable);}
            catch(InvalidOperationException e)when(WorkflowError(e.Message) is not null){return new(WorkflowError(e.Message)!.Value,new InspectionMutationBlocked(e.Message,ReinspectionSourceCandidate.MissingContracts));}
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM",preserveCommitErrors:true);

    private static RoomTypeBatchStatus? WorkflowError(string code)=>ReinspectionReviewErrors.Status(code)??code switch{
        "REINSPECTION_CONCLUSION_STALE" or "REINSPECTION_BINDING_STALE_OR_WRONG_SOURCE"=>RoomTypeBatchStatus.Stale,
        "REINSPECTION_PROPOSAL_INPUT_INVALID" or "REINSPECTION_BINDING_REVISION_INVALID" or "REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED"=>RoomTypeBatchStatus.InvalidReference,
        "REINSPECTION_OPERATION_CONFLICT" or "REINSPECTION_RESULTS_ALREADY_REVIEWED"=>RoomTypeBatchStatus.Duplicate,
        "REINSPECTION_PROPOSAL_SCHEMA_REQUIRED" or "REINSPECTION_ISOLATED_COMPONENT_SCHEMA_REQUIRED"=>RoomTypeBatchStatus.SchemaRequired,
        "REINSPECTION_PERSISTED_BINDING_REQUIRED"=>RoomTypeBatchStatus.NotFound,
        _=>null};

    private async Task<RoomTypeBatchResult> WorkflowDetails(SessionReference ticket,long sid,long branch,long inspection,long rework,CancellationToken ct)
    {
        var rows=await db.Database.SqlQuery<WorkflowIdentity>($"""
            SELECT i.row_version AS InspectionRevision,w.row_version AS ReworkRevision,
              b.cycle_revision AS BoundCycleRevision,c.row_version AS CurrentCycleRevision,
              b.rework_revision AS BoundReworkRevision,b.reinspector_user_id AS InspectorId,
              b.original_inspection_id AS OriginalInspectionId,b.original_version_id AS OriginalVersionId,b.assigned_version_id AS AssignedVersionId
            FROM dbo.fe05_reinspection_bindings_v1 b WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK) ON i.inspection_id=b.inspection_id
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch}
            JOIN dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK) ON w.rework_id=b.rework_id AND w.cleaning_task_id=b.cleaning_task_id
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=b.room_id AND c.cleaning_task_id=b.cleaning_task_id
            WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.branch_id={branch}
            """).ToArrayAsync(ct);
        if(rows.Length!=1)throw new InvalidOperationException("REINSPECTION_PERSISTED_BINDING_REQUIRED");
        var row=rows[0];
        var source=await ReadReinspectionCandidateInCallerTransactionAsync(ticket,sid,branch,inspection,rework,WorkflowTag(row.InspectionRevision),WorkflowTag(row.ReworkRevision),ct);
        if(row.InspectorId!=ticket.UserId)throw new RejectInspectionOwnershipException();
        if(!row.BoundCycleRevision.SequenceEqual(row.CurrentCycleRevision)||!row.BoundReworkRevision.SequenceEqual(row.ReworkRevision)
            ||row.OriginalInspectionId!=source.OriginalInspectionId||row.OriginalVersionId!=source.OriginalChecklistVersionId||row.AssignedVersionId!=source.AssignedChecklistVersionId)
            throw new InvalidOperationException("REINSPECTION_BINDING_STALE_OR_WRONG_SOURCE");
        var stored=await ReadStoredReinspectionAsync(inspection,rework,ct);
        if(!stored.Decisions.Select(d=>d.DefectId).Order().SequenceEqual(source.Defects.Select(d=>d.DefectId).Order()))
            throw new InvalidOperationException("REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED");
        var selected=stored.Decisions.Select(d=>d.DefectId).ToArray();
        var descriptions=await db.InspectionDefects.AsNoTracking().Where(d=>d.InspectionId==source.OriginalInspectionId&&selected.Contains(d.DefectId))
            .Select(d=>new{d.DefectId,d.Description,Criterion=d.ChecklistItem==null?null:d.ChecklistItem.Criterion}).ToDictionaryAsync(d=>d.DefectId,ct);
        var criteria=await db.InspectionChecklistItems.AsNoTracking().Where(i=>i.VersionId==source.AssignedChecklistVersionId).OrderBy(i=>i.SortOrder).ThenBy(i=>i.ItemId)
            .Select(i=>new ReinspectionWorkflowCriterion(i.ItemId,i.Criterion,i.PassCondition,i.IsRequired,i.IsActive)).ToArrayAsync(ct);
        return new(RoomTypeBatchStatus.Success,new ReinspectionWorkflowDetails(inspection,rework,source.OriginalInspectionId,
            source.OriginalChecklistVersionId,source.AssignedChecklistVersionId,stored.InspectionETag,stored.ReworkETag,stored.BindingETag,
            stored.Decisions.Select(d=>new ReinspectionWorkflowDefect(d.DefectId,descriptions[d.DefectId].Description,descriptions[d.DefectId].Criterion,source.Defects.Single(x=>x.DefectId==d.DefectId).Note,d.Fixed,d.RecordedBy,d.RecordedAt)).ToArray(),criteria,true,false),stored.InspectionETag);
    }
    private sealed class WorkflowListRow {public long InspectionId{get;set;}public long ReworkId{get;set;}public long OriginalInspectionId{get;set;}public long RoomId{get;set;}public long VersionId{get;set;}}
    private sealed class WorkflowIdentity {public byte[] InspectionRevision{get;set;}=[];public byte[] ReworkRevision{get;set;}=[];public byte[] BoundCycleRevision{get;set;}=[];public byte[] CurrentCycleRevision{get;set;}=[];public byte[] BoundReworkRevision{get;set;}=[];public long InspectorId{get;set;}public long OriginalInspectionId{get;set;}public long OriginalVersionId{get;set;}public long AssignedVersionId{get;set;}}
}
