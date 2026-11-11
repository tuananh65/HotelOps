namespace HotelOps.Api.Services.Dependencies;

public enum DependencyClassification { BLOCKING, TRANSFER_REQUIRED, HISTORICAL, UNKNOWN_POLICY }
public sealed record DependencyFact(string Category,long RecordId,long BranchId,string? State,
    DependencyClassification Classification,string? SourceFingerprint=null);
public sealed record DependencySnapshot(string Resource,long ResourceId,long BranchId,
    IReadOnlyList<DependencyFact> Facts,IReadOnlyList<string> Unresolved,string Fingerprint,
    bool CanDeactivate=false,string Coordination="FINAL_OWNER_REVALIDATION_AND_WRITER_ACCEPTANCE_REQUIRED");
public sealed class DependencyScopeException : Exception;
public sealed class DependencySnapshotConflictException : Exception;
public sealed record UnscopedResponsibility(string Category,long RecordId,string State,string SourceFingerprint);
public sealed record GlobalUserDependencySnapshot(long UserId,IReadOnlyList<DependencySnapshot> Branches,
    IReadOnlyList<UnscopedResponsibility> Unscoped,string Fingerprint,bool CanDeactivate=false);
