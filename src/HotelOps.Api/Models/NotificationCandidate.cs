namespace HotelOps.Api.Models;

// FS009 PROVISIONAL_NOT_ACCEPTED: candidate schema, not the accepted A01 API.
public sealed class NotificationOperationCandidate
{
    public long ActorUserId { get; set; }
    public Guid OperationKey { get; set; }
    public int FingerprintVersion { get; set; } = 1;
    public byte[] Fingerprint { get; set; } = [];
    public bool Completed { get; set; }
    public string? ResultJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class NotificationPublicationCandidate
{
    public Guid PublicationId { get; set; }
    public long ActorUserId { get; set; }
    public Guid OperationKey { get; set; }
    public string EventType { get; set; } = "CLEANING_TASK_ASSIGNED";
    public int SchemaVersion { get; set; } = 1;
    public long AssignmentId { get; set; }
    public byte[] SourceRevision { get; set; } = [];
    public string PayloadJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
public sealed class NotificationIntentCandidate
{
    public long IntentId { get; set; }
    public Guid PublicationId { get; set; }
    public long UserId { get; set; }
    public string DeliveryChannel { get; set; } = "IN_APP";
    public string Status { get; set; } = "PENDING";
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? ClaimId { get; set; }
    public DateTime? ClaimedUntil { get; set; }
    public string? FailureCode { get; set; }
    public long? InboxNotificationId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
// Existing composite unique inbox key, additive read-only principal projection.
public sealed class CandidateInboxLink
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public string DeliveryChannel { get; set; } = "IN_APP";
}