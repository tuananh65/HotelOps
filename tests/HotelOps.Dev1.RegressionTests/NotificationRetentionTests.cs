using HotelOps.Api.Services.Notifications;
using Xunit;

public sealed class NotificationRetentionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    [InlineData(3650)]
    public void DeliveryAndInboxUseInclusiveUtcWindow(int days)
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var window = NotificationRetention.At(now, new() { RetentionDays = days });
        Assert.Equal(now.AddDays(-days), window.Cutoff);
        Assert.True(window.Contains(window.Cutoff));
        Assert.False(window.Contains(window.Cutoff.AddTicks(-1)));
        Assert.True(window.Contains(now));
        Assert.False(window.Contains(now.AddTicks(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3651)]
    public void InvalidRetentionFailsClosed(int days) =>
        Assert.Throws<InvalidOperationException>(() => NotificationRetention.At(DateTime.UtcNow, new() { RetentionDays = days }));
}
