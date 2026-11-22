using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long BranchId { get; set; }

    public long RecipientUserId { get; set; }

    public long? WorkId { get; set; }

    public long? DocumentId { get; set; }

    public long? RoomId { get; set; }

    public Guid EventKey { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool RequiresAcknowledgment { get; set; }

    public DateTime AvailableAt { get; set; }

    public Guid? LeaseId { get; set; }

    public DateTime? LeaseUntil { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;





}
