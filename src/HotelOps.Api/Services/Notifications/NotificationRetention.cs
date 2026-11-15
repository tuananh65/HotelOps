namespace HotelOps.Api.Services.Notifications;

internal readonly record struct NotificationRetention(DateTime Cutoff, DateTime Now)
{
    internal static NotificationRetention At(DateTime now, NotificationCenterOptions options)
    {
        if (options.RetentionDays is < 1 or > 3650)
            throw new InvalidOperationException("Invalid notification retention configuration.");
        return new(now.AddDays(-options.RetentionDays), now);
    }

    internal bool Contains(DateTime occurredAt) => occurredAt >= Cutoff && occurredAt <= Now;
}
