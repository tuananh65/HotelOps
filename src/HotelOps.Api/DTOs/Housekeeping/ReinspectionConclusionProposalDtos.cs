namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record ReinspectionConclusionProposalRequest(Guid OperationId,long ReworkId,string ReworkRevision,string BindingRevision,
    IReadOnlyList<long> SelectedDefectIds,bool Confirmed);
public sealed record ReinspectionConclusionProposal(Guid OperationId,long InspectionId,long ReworkId,long ActorUserId,
    long OriginalChecklistVersionId,long AssignedChecklistVersionId,string ProposedReworkState,
    string InspectionRevision,string ReworkRevision,string BindingRevision,string ProposalRevision,
    IReadOnlyList<long> NotFixedDefectIds,DateTime CreatedAt,bool Replayed,bool LifecycleApplied,bool NextRoundCreationAvailable);
public sealed record ReinspectionConclusionProposalRead(ReinspectionConclusionProposal? Proposal);
