using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

internal sealed record ReinspectionBindingRowCandidate(long InspectionId,long ReworkId,long OriginalInspectionId,
    long TaskId,long RoomId,long BranchId,string CycleRevision,long OriginalVersionId,long AssignedVersionId,
    string InspectionRevision,string ReworkRevision,long BoundBy,DateTime BoundAt);
internal sealed record ReinspectionResultRowCandidate(long InspectionId,long ReworkId,long DefectId,bool Fixed,long RecordedBy,DateTime RecordedAt);
internal sealed record ReinspectionPersistencePlan(ReinspectionBindingRowCandidate Binding,
    IReadOnlyList<ReinspectionResultRowCandidate> Results,ReinspectionLifecycleCandidate Lifecycle)
{
    internal bool PersistedOrAccepted=>false;
}
public sealed partial class RoomTypeCreationService
{
    // Prepare data for the proposed storage contract only. No INSERT/UPDATE or inferred backfill.
    internal async Task<ReinspectionPersistencePlan> PrepareReinspectionPersistenceCandidateAsync(SessionReference ticket,long sid,
        long branch,long inspection,long rework,string inspectionTag,string reworkTag,
        IReadOnlyList<ReinspectionDefectDecision> decisions,CancellationToken ct)
    {
        var source=await ReadReinspectionCandidateInCallerTransactionAsync(ticket,sid,branch,inspection,rework,inspectionTag,reworkTag,ct);
        var cycle=await db.Database.SqlQuery<ReinspectionCycleRow>($"""
            SELECT i.room_id AS RoomId,i.cleaning_task_id AS TaskId,c.row_version AS Revision
            FROM dbo.room_inspections i WITH(HOLDLOCK)
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch}
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
            WHERE i.inspection_id={inspection} AND i.inspector_user_id={ticket.UserId}
              AND i.row_version={Convert.FromHexString(inspectionTag.Trim('"'))}
            """).SingleAsync(ct);
        var validation=ReinspectionDecisionValidator.Validate(source.Defects.Select(d=>d.DefectId).ToArray(),decisions,
            Convert.FromHexString(source.ReworkRevision.Trim('"')),reworkTag,Convert.FromHexString(source.InspectionRevision.Trim('"')),inspectionTag);
        var lifecycle=ReinspectionLifecycleCandidate.From(validation);
        var now=clock.GetUtcNow().UtcDateTime;
        var binding=new ReinspectionBindingRowCandidate(inspection,rework,source.OriginalInspectionId,cycle.TaskId,cycle.RoomId,branch,
            WorkflowTag(cycle.Revision),source.OriginalChecklistVersionId,source.AssignedChecklistVersionId,inspectionTag,reworkTag,ticket.UserId,now);
        return new(binding,decisions.OrderBy(d=>d.DefectId).Select(d=>new ReinspectionResultRowCandidate(inspection,rework,d.DefectId,d.Fixed!.Value,ticket.UserId,now)).ToArray(),lifecycle);
    }
    private sealed class ReinspectionCycleRow {public long RoomId{get;set;}public long TaskId{get;set;}public byte[] Revision{get;set;}=[];}
}
