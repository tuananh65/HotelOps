namespace HotelOps.Api.Services.TrialPreparation;

// CORE08 C07/C08/C12 prototypes. No DI registration, HTTP action or grant.
// Decisions are supplied by an authoritative caller; these checks never grant access.
public sealed record ScopedActor(long UserId, long BranchId, bool Active, bool SessionValid,
    bool SourceReadGranted, bool ActionGranted);

public static class ScopedReadPreconditions
{
    public static bool CanRead(ScopedActor actor, long sourceBranchId) =>
        actor.UserId > 0 && actor.BranchId > 0 && sourceBranchId > 0 &&
        actor.BranchId == sourceBranchId && actor.Active && actor.SessionValid && actor.SourceReadGranted;

    public static bool CanConsiderHandover(ScopedActor actor, long sourceBranchId, long? designatedReceiver) =>
        designatedReceiver is > 0 && actor.UserId == designatedReceiver &&
        CanRead(actor, sourceBranchId) && actor.ActionGranted;

    public static bool CanConsiderResponsibility(ScopedActor actor, long sourceBranchId,
        bool roleCurrent, bool departmentCurrent, bool locationCurrent) =>
        CanRead(actor, sourceBranchId) && actor.ActionGranted && roleCurrent && departmentCurrent && locationCurrent;
}

public sealed record EvidenceContentPolicy(long MaximumBytes, IReadOnlySet<string> AllowedMediaTypes);

public static class EvidenceContentValidation
{
    // No file type/size default, storage choice, key, public URL or persisted metadata is invented.
    public static bool Accepts(long actualBytes, string? verifiedMediaType, EvidenceContentPolicy? policy) =>
        policy is { MaximumBytes: > 0 } && actualBytes > 0 && actualBytes <= policy.MaximumBytes &&
        verifiedMediaType is not null && policy.AllowedMediaTypes.Contains(verifiedMediaType);
}

public interface IIncidentEvidenceReadAuthorization
{
    // Must be called for every read, using current persisted source and session state.
    Task<bool> CanReadAsync(long incidentId, long evidenceId, CancellationToken cancellationToken);
}

public interface IIncidentSlaClock
{
    // Null means unavailable policy/calendar; it is never converted into zero or an invented deadline.
    Task<DateTimeOffset?> DueAtAsync(long incidentId, CancellationToken cancellationToken);
}
