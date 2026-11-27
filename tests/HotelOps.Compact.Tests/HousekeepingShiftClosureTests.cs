using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class HousekeepingShiftClosureTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1440)]
    public void HousekeepingAttendanceNeverAutomaticallyCloses(int minutesAfterEnd)
    {
        var day = new WorkdayAssignment { WorkDate = new(2026, 11, 27), Status = "WORKING",
            StartedAt = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc) };
        var started = day.StartedAt;
        var end = ShiftExpiryService.Boundary(day.WorkDate, 960, "Asia/Ho_Chi_Minh");
        Assert.False(ShiftExpiryService.TryExpire(day, 960, "Asia/Ho_Chi_Minh", end.AddMinutes(minutesAfterEnd), "HK_STAFF"));
        Assert.Equal("WORKING", day.Status);
        Assert.Equal(started, day.StartedAt);
        Assert.Null(day.EndedAt);
    }

    [Fact]
    public void UnstartedHousekeepingShiftStillCancelsWithoutAttendance()
    {
        var day = new WorkdayAssignment { WorkDate = new(2026, 11, 27), Status = "PLANNED" };
        var end = ShiftExpiryService.Boundary(day.WorkDate, 960, "Asia/Ho_Chi_Minh");
        Assert.True(ShiftExpiryService.TryExpire(day, 960, "Asia/Ho_Chi_Minh", end.AddHours(1), "HK_STAFF"));
        Assert.Equal("CANCELLED", day.Status);
        Assert.Null(day.StartedAt);
        Assert.Null(day.EndedAt);
    }

    [Theory]
    [InlineData("HK_SUPERVISOR")]
    [InlineData("MAINTENANCE_STAFF")]
    public void OtherRolesKeepExistingScheduledExpiry(string role)
    {
        var day = new WorkdayAssignment { WorkDate = new(2026, 11, 27), Status = "WORKING",
            StartedAt = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc) };
        var end = ShiftExpiryService.Boundary(day.WorkDate, 960, "Asia/Ho_Chi_Minh");
        Assert.True(ShiftExpiryService.TryExpire(day, 960, "Asia/Ho_Chi_Minh", end, role));
        Assert.Equal(end, day.EndedAt);
    }
}
