using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class NotificationRecipient
{
    public long RecipientId { get; set; }

    public long NotificationId { get; set; }

    public long UserId { get; set; }

    public string DeliveryChannel { get; set; } = null!;

    public string DeliveryStatus { get; set; } = null!;

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? DismissedAt { get; set; }

    public string? FailureReason { get; set; }

    public virtual Notification Notification { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
