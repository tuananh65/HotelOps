namespace HotelOps.Api.DTOs.Housekeeping;
public sealed record RejectInspectionRequest(string Reason, bool Confirmed);
public sealed record InspectionMutationBlocked(string Code, IReadOnlyList<string> Dependencies);
public sealed record RejectInspectionPreview(long InspectionId,long BranchId,long RoomId,long CleaningTaskId,long VersionId,
    string State,string ETag,IReadOnlyList<long> DefectIds,IReadOnlyList<long> EvidenceIds,
    bool MutationAvailable,IReadOnlyList<string> Dependencies);

// Persisted facts only. AuthorityEstablished=false is not a source receipt for FE04.
public sealed record ReworkSourcePreview(long InspectionId,long BranchId,long RoomId,long CleaningTaskId,long VersionId,string ETag,
    IReadOnlyList<long> DefectIds,IReadOnlyList<long> EvidenceIds,bool AuthorityEstablished,IReadOnlyList<string> Dependencies);