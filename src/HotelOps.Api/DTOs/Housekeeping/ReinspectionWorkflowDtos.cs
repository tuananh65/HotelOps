namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record ReinspectionAssigneeDto(long Id,string Name);
public sealed record ReinspectionAssignmentOptionsDto(long ReworkId,long BranchId,long RoomId,long CleaningTaskId,
    long OriginalInspectionId,long VersionId,string ReworkETag,long? ExistingInspectionId,
    IReadOnlyList<ReinspectionAssigneeDto> Inspectors,string RoomNumber,string ChecklistName,int ChecklistVersionNumber,
    bool CanAssign,string? UnavailableReason=null);
public sealed record ReinspectionAssignmentResult(long Id,long ReworkId,long BranchId,string State,string SourceETag,
    Guid OperationId,string Notification="PENDING_DELIVERY",string Contract="FE05_REINSPECTION_ASSIGNMENT_V1");
public sealed record ReinspectionDefectInput(long DefectId,bool? Fixed,string? Note=null,IReadOnlyList<EvidenceUpload>? Photos=null);
public sealed record ConcludeReinspectionRequest(string ReworkETag,string BindingETag,string Reason,IReadOnlyList<ReinspectionDefectInput> Defects);
public sealed record ReinspectionDefectDto(long Id,string Description,string Remediation,bool? Fixed,string? Note,
    IReadOnlyList<long> OriginalPhotos,IReadOnlyList<long> RemediationPhotos,IReadOnlyList<long> ReinspectionPhotos);
public sealed record ReinspectionExecutionDto(long Id,long ReworkId,long BranchId,long RoomId,long CleaningTaskId,long OriginalInspectionId,
    long InspectorId,long VersionId,string State,string SourceETag,string ReworkETag,string BindingETag,
    IReadOnlyList<ReinspectionDefectDto> Defects,string EffectiveReadiness,string? MessageCode=null);
public sealed record ReinspectionConclusionResult(long Id,long ReworkId,long BranchId,long CleaningTaskId,long RoomId,
    string State,string ReworkState,string SourceETag,string ReworkETag,Guid OperationId,string EffectiveReadiness,
    string MessageCode,string Notification="PENDING_DELIVERY",string Contract="FE05_REINSPECTION_CONCLUSION_V1");
