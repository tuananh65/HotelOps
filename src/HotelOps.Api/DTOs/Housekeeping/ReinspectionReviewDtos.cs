namespace HotelOps.Api.DTOs.Housekeeping;

// Review-only transport. No role/permission, evidence bytes or persisted authority from HTTP.
public sealed record ReinspectionDefectReviewDto(long DefectId,string? RemediationNote,long? RemediatedBy,DateTime? RemediatedAt);
public sealed record ReinspectionReviewDto(long InspectionId,long ReworkId,long OriginalInspectionId,
    long AssignedChecklistVersionId,long OriginalChecklistVersionId,string InspectionRevision,string ReworkRevision,
    IReadOnlyList<ReinspectionDefectReviewDto> Defects,bool SourceBindingAccepted,bool MutationAvailable,IReadOnlyList<string> Dependencies);
