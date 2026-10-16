using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public string? ActionUrl { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<NotificationRecipient> NotificationRecipients { get; set; } = new List<NotificationRecipient>();
}
