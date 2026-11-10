namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record ReinspectionWorkflowItem(long InspectionId,long ReworkId,long OriginalInspectionId,long RoomId,long AssignedChecklistVersionId);
public sealed record ReinspectionDecisionDto(long DefectId,bool? Fixed);
public sealed record ReinspectionBindCandidateRequest(long ReworkId,string InspectionRevision,string ReworkRevision);
public sealed record ReinspectionSaveCandidateRequest(long ReworkId,string ReworkRevision,string BindingRevision,IReadOnlyList<ReinspectionDecisionDto> Decisions);
public sealed record ReinspectionWorkflowDefect(long DefectId,string Description,string? OriginalCriterion,string? RemediationNote,bool? Fixed,long? RecordedBy,DateTime? RecordedAt);
public sealed record ReinspectionWorkflowCriterion(long ItemId,string Criterion,string? PassCondition,bool IsRequired,bool IsActive);
public sealed record ReinspectionWorkflowDetails(long InspectionId,long ReworkId,long OriginalInspectionId,
    long OriginalChecklistVersionId,long AssignedChecklistVersionId,string InspectionRevision,string ReworkRevision,string BindingRevision,
    IReadOnlyList<ReinspectionWorkflowDefect> Defects,IReadOnlyList<ReinspectionWorkflowCriterion> Criteria,bool CandidateStorageAvailable,bool LifecycleConclusionAvailable);
