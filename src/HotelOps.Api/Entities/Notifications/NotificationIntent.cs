namespace HotelOps.Api.Entities.Notifications;

// Additive Closure04 trial contract. Existing generated Notification entities stay intact.
public sealed class NotificationIntent
{
    public long IntentId { get; set; }
    public string EventType { get; set; } = "";
    public short EventVersion { get; set; }
    public long OccurrenceId { get; set; }
    public long SourceId { get; set; }
    public long BranchId { get; set; }
    public long RecipientUserId { get; set; }
    public string ReadPolicy { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string ActionPath { get; set; } = "";
    public byte[] PayloadHash { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public int Attempts { get; set; }
    public string State { get; set; } = "PENDING";
    public string? FailureCode { get; set; }
    public long? NotificationId { get; set; }
    public DateTime? ReadAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
