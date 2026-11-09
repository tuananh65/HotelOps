namespace HotelOps.Api.Services.Notifications;

// Internal application commands: never model-bound or exposed as a publish API.
public sealed record NotificationEvent(string EventType, short Version, long OccurrenceId);
public sealed record ResolvedNotification(long SourceId, long BranchId, long RecipientId,
    string ReadPolicy, string Title, string Message, string ActionPath);
public sealed record NotificationStaged(int Recipients);
public interface INotificationPublisher
{
    // Executable C04 candidate signature, not the unapproved V12 reservation DTO.
    Task<NotificationStaged> PublishRequiredIntentsAsync(NotificationEvent sourceEvent, CancellationToken ct);
    // Caller must own this scoped context's local transaction, flush, then commit.
    Task<NotificationStaged> StageAsync(NotificationEvent sourceEvent, CancellationToken ct);
}
public interface INotificationEventProfile
{
    string EventType { get; }
    short Version { get; }
    Task<ResolvedNotification> ResolveAsync(long occurrenceId, CancellationToken ct);
}
public sealed record NotificationItem(long Id, string Title, string Message, string Priority,
    DateTime CreatedAt, DateTime? ReadAt, string ActionPath, string RowVersion);
public sealed record NotificationPage(IReadOnlyList<NotificationItem> Items, int TotalCount,
    int UnreadCount, int Page, int PageSize);
public sealed record NotificationReadRequest(bool IsRead, string ExpectedRowVersion);
public sealed class NotificationFilter : System.ComponentModel.DataAnnotations.IValidatableObject
{
    [System.ComponentModel.DataAnnotations.MaxLength(100)] public string? Type { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(20)] public string? Priority { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(200)] public string? Search { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(System.ComponentModel.DataAnnotations.ValidationContext context)
    {
        if (From > To) yield return new("From must be earlier than or equal to To.", [nameof(From), nameof(To)]);
    }
}
public sealed class NotificationConflictException : Exception;
public sealed class NotificationContractUnavailableException : Exception;
