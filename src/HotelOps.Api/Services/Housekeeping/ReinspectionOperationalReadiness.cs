using HotelOps.Api.DTOs.Housekeeping;
namespace HotelOps.Api.Services.HotelStructure;
internal sealed record ReinspectionOperationalPlan(long InspectionId,long ReworkId,long ActorUserId,long OriginalChecklistVersionId,long AssignedChecklistVersionId,string InspectionRevision,string ReworkRevision,string BindingRevision,string ProposedInspectionState,string ProposedReworkState,
    string ProposedCleaningTaskState,bool RequiresFe04Projection,bool RequiresNotificationContractDecision,bool CanApply,IReadOnlyList<long> RemainingOriginalDefectIds)
{
    internal static ReinspectionOperationalPlan From(ReinspectionConclusionProposal p)
    {
        if(p.LifecycleApplied||p.NextRoundCreationAvailable||p.ProposedReworkState is not ("COMPLETED" or "FAILED")
            ||p.ProposedReworkState=="COMPLETED"&&p.NotFixedDefectIds.Count!=0||p.ProposedReworkState=="FAILED"&&p.NotFixedDefectIds.Count==0)
            throw new InvalidOperationException("UC047_INVALID_PROPOSAL_PLAN");
        var passed=p.ProposedReworkState=="COMPLETED";
        return new(p.InspectionId,p.ReworkId,p.ActorUserId,p.OriginalChecklistVersionId,p.AssignedChecklistVersionId,p.InspectionRevision,p.ReworkRevision,p.BindingRevision,passed?"PASSED":"FAILED",p.ProposedReworkState,passed?"COMPLETED":"REWORK_REQUIRED",true,true,false,p.NotFixedDefectIds);
    }
}
public sealed partial class RoomTypeCreationService
{
    internal async Task<RoomTypeBatchResult> ReadReinspectionOperationalPlanAsync(HotelOps.Api.Services.Authentication.SessionReference ticket,long sid,long branch,long inspection,long rework,CancellationToken ct)
    {
        var read=await ReadReinspectionConclusionAsync(ticket,sid,branch,inspection,rework,ct);
        if(read.Status!=RoomTypeBatchStatus.Success)return read;
        var proposal=((ReinspectionConclusionProposalRead)read.Value!).Proposal;
        return proposal==null?new(RoomTypeBatchStatus.InvalidReference):new(RoomTypeBatchStatus.Success,ReinspectionOperationalPlan.From(proposal),read.ETag);
    }
}
