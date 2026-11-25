namespace HotelOps.Api.Compact.Entities;
public sealed class AccountInvitation
{
    public long InvitationId { get; set; }
    public long UserId { get; set; }
    public long CreatedBy { get; set; }
    public string Recipient { get; set; } = "";
    public byte[] TokenHash { get; set; } = [];
    public byte[] PasswordFingerprint { get; set; } = [];
    public string? ProtectedToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string DeliveryStatus { get; set; } = "PENDING";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
