using System.Security.Cryptography;
using System.Text.Json;

namespace HotelOps.Api.Services.Notifications;

// Internal integration candidates, never HTTP request DTOs or approved schema keys.
public enum Fe05NotificationKind { InspectionFailed = 1, CleaningReworkRequested = 2 }
public sealed record Fe05NotificationCommand(Fe05NotificationKind Kind, Guid OperationId,
    long BranchId, long InspectionId, string ExpectedRevision);
public sealed record Fe05NotificationSource(long ActorId, long BranchId, long RoomId,
    long InspectionId, long CleaningTaskId, long ChecklistVersionId, long? ReworkId,
    string Revision, long CurrentCleaningTaskId, string InspectionStatus, string InspectionResult,
    string WorkflowState, DateTimeOffset OccurredAt, IReadOnlyList<long> FailedResultIds, string? ReworkRevision = null);
public sealed record Fe05Recipient(long UserId, long BranchId, bool Active, bool CanReadSource);
public sealed record Fe05NotificationCandidate(string EventType, short Version, string Producer,
    Guid OperationId, long ActorId, long BranchId, long RoomId, long InspectionId,
    long CleaningTaskId, long ChecklistVersionId, long? ReworkId, string Revision,
    DateTimeOffset OccurredAt, IReadOnlyList<long> ResultIds, IReadOnlyList<long> RecipientIds,
    string ReadPolicy, string Title, string Message, string Fingerprint, string? ReworkRevision = null);
public sealed class Fe05NotificationUnavailableException(string gap) : Exception(gap);

public interface IFe05NotificationSource
{
    // DEV1 must reauthorize actor/session and load persisted post-transition state under the
    // exact caller transaction/locks. Do not adapt a client preview or uncommitted DTO blindly.
    Task<Fe05NotificationSource> LoadLockedAsync(Fe05NotificationCommand command, long actorId, CancellationToken ct);
}
public interface IFe05NotificationRecipientPolicy
{
    // Accepted resolver must find the original cleaner / supervisor or rework assignee,
    // and revalidate each user's active branch/source-read eligibility in the transaction.
    Task<IReadOnlyList<Fe05Recipient>> ResolveAsync(Fe05NotificationSource source, Fe05NotificationKind kind, CancellationToken ct);
}
public sealed class UnacceptedFe05Source : IFe05NotificationSource
{
    public Task<Fe05NotificationSource> LoadLockedAsync(Fe05NotificationCommand command, long actorId, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_LOCKED_SOURCE_ADAPTER_REQUIRED"); }
}
public sealed class UnacceptedFe05Recipients : IFe05NotificationRecipientPolicy
{
    public Task<IReadOnlyList<Fe05Recipient>> ResolveAsync(Fe05NotificationSource source, Fe05NotificationKind kind, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_RECIPIENT_POLICY_NOT_ACCEPTED"); }
}

public static class Fe05NotificationMapping
{
    public static void ValidateSource(Fe05NotificationCommand command, Fe05NotificationSource source, long actorId)
    {
        if (!Enum.IsDefined(command.Kind) || command.OperationId == Guid.Empty || command.BranchId <= 0 ||
            command.InspectionId <= 0 || string.IsNullOrWhiteSpace(command.ExpectedRevision) || command.ExpectedRevision.Length > 128)
            throw new Fe05NotificationUnavailableException("FE05_INVALID_OPERATION");
        // Generic unavailable prevents leaking which foreign source/user exists.
        if (actorId <= 0 || source.ActorId != actorId || source.BranchId != command.BranchId ||
            source.InspectionId != command.InspectionId || source.RoomId <= 0 || source.CleaningTaskId <= 0 ||
            source.ChecklistVersionId <= 0 || source.CurrentCleaningTaskId != source.CleaningTaskId)
            throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
        if (!StringComparer.Ordinal.Equals(source.Revision, command.ExpectedRevision))
            throw new NotificationConflictException();
        if (source.InspectionStatus != "COMPLETED" || source.InspectionResult != "FAIL" || source.WorkflowState != "FAILED" ||
            source.OccurredAt.Offset != TimeSpan.Zero || source.OccurredAt == default ||
            source.FailedResultIds is not { Count: > 0 and <= 200 } || source.FailedResultIds.Any(x => x <= 0))
            throw new Fe05NotificationUnavailableException("FE05_AUTHORITATIVE_FAILED_STATE_REQUIRED");
        if (command.Kind == Fe05NotificationKind.CleaningReworkRequested ? source.ReworkId is not > 0 : source.ReworkId is not null)
            throw new Fe05NotificationUnavailableException("FE05_REWORK_BINDING_REQUIRED");
        if(command.Kind==Fe05NotificationKind.CleaningReworkRequested ?
            source.ReworkRevision is not {Length:18} || source.ReworkRevision[0]!='"' || source.ReworkRevision[^1]!='"' ||
                !source.ReworkRevision[1..^1].All(Uri.IsHexDigit) : source.ReworkRevision is not null)
            throw new Fe05NotificationUnavailableException("FE05_REWORK_REVISION_REQUIRED");
    }
    public static Fe05NotificationCandidate Map(Fe05NotificationCommand command, Fe05NotificationSource source,
        long actorId, IReadOnlyList<Fe05Recipient> recipients)
    {
        ValidateSource(command,source,actorId);
        if (recipients is not { Count: > 0 and <= 200 } || recipients.Any(r => r.UserId <= 0 || !r.Active ||
            !r.CanReadSource || r.BranchId != source.BranchId))
            throw new Fe05NotificationUnavailableException("FE05_RECIPIENTS_UNAVAILABLE");
        var ids = recipients.Select(r => r.UserId).Distinct().Order().ToArray();
        var results = source.FailedResultIds.Distinct().Order().ToArray();
        var type = command.Kind == Fe05NotificationKind.InspectionFailed ? "inspection.failed" : "cleaning.rework.requested";
        var title = command.Kind == Fe05NotificationKind.InspectionFailed ? "Inspection failed" : "Cleaning rework requested";
        // No notes, names, evidence URLs or client-provided action URL in the display payload.
        var candidate = new Fe05NotificationCandidate(type, 1, "FE05", command.OperationId, actorId,
            source.BranchId, source.RoomId, source.InspectionId, source.CleaningTaskId, source.ChecklistVersionId,
            source.ReworkId, source.Revision, source.OccurredAt, Array.AsReadOnly(results), Array.AsReadOnly(ids),
            "fe05.source.read.v1.candidate", title, "Review the authorized source in HotelOps.", "", source.ReworkRevision);
        return candidate with { Fingerprint = Fingerprint(candidate) };
    }
    public static string Fingerprint(Fe05NotificationCandidate value) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value with { Fingerprint = "" })));
}
