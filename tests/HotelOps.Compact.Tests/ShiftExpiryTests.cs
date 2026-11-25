using HotelOps.Api.Compact.Services;
using HotelOps.Api.Compact.Entities;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class ShiftExpiryTests
{
    [Theory]
    [InlineData(0, "2026-11-22T17:00:00Z")]
    [InlineData(480, "2026-11-23T01:00:00Z")]
    [InlineData(1440, "2026-11-23T17:00:00Z")]
    public void VietnameseShiftBoundariesIncludeMidnight(int minute, string expected)
    {
        var actual = ShiftExpiryService.Boundary(new DateOnly(2026, 11, 23), minute, "Asia/Ho_Chi_Minh");
        Assert.Equal(DateTime.Parse(expected).ToUniversalTime(), actual);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
    }

    [Fact]
    public void MidnightExpiryIsExactAndIdempotent()
    {
        var row = new WorkdayAssignment { WorkDate = new(2026, 11, 24), Status = "WORKING", StartedAt = new DateTime(2026,11,24,9,0,0,DateTimeKind.Utc) };
        var boundary = new DateTime(2026,11,24,17,0,0,DateTimeKind.Utc);
        Assert.False(ShiftExpiryService.TryExpire(row,1440,"Asia/Ho_Chi_Minh",boundary.AddTicks(-1)));
        Assert.Equal("WORKING",row.Status);
        Assert.True(ShiftExpiryService.TryExpire(row,1440,"Asia/Ho_Chi_Minh",boundary));
        Assert.Equal("ENDED",row.Status);
        Assert.Equal(boundary,row.EndedAt);
        Assert.False(ShiftExpiryService.TryExpire(row,1440,"Asia/Ho_Chi_Minh",boundary.AddDays(1)));
        Assert.Equal(boundary,row.EndedAt);
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("ENDED")]
    public void ExpiryDoesNotInventAttendance(string status)
    {
        var row = new WorkdayAssignment { WorkDate = new(2026,11,24),Status = status };
        Assert.False(ShiftExpiryService.TryExpire(row,1440,"Asia/Ho_Chi_Minh",new DateTime(2026,11,25,1,0,0,DateTimeKind.Utc)));
        Assert.Equal(status,row.Status);
        Assert.Null(row.StartedAt);
        Assert.Null(row.EndedAt);
    }

    [Theory]
    [InlineData(1020)]
    [InlineData(1440)]
    public void UnstartedShiftExpiresAtOneHourAfterEndWithoutAttendance(int endMinute)
    {
        var row = new WorkdayAssignment { WorkDate = new(2026, 11, 24), Status = "PLANNED" };
        var end = ShiftExpiryService.Boundary(row.WorkDate, endMinute, "Asia/Ho_Chi_Minh");
        Assert.False(ShiftExpiryService.TryExpire(row, endMinute, "Asia/Ho_Chi_Minh", end.AddMinutes(59).AddSeconds(59)));
        Assert.Equal("PLANNED", row.Status);
        Assert.True(ShiftExpiryService.TryExpire(row, endMinute, "Asia/Ho_Chi_Minh", end.AddHours(1)));
        Assert.Equal("CANCELLED", row.Status);
        Assert.Null(row.StartedAt);
        Assert.Null(row.EndedAt);
        Assert.False(ShiftExpiryService.TryExpire(row, endMinute, "Asia/Ho_Chi_Minh", end.AddDays(1)));
    }

    [Fact]
    public void FuturePlannedShiftRemainsAssigned()
    {
        var row = new WorkdayAssignment { WorkDate = new(2026, 11, 26), Status = "PLANNED" };
        Assert.False(ShiftExpiryService.TryExpire(row, 1020, "Asia/Ho_Chi_Minh", new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("PLANNED", row.Status);
        Assert.Null(row.StartedAt);
        Assert.Null(row.EndedAt);
    }

    [Fact]
    public void WorkingShiftStillEndsAtScheduledEndWithoutGracePeriod()
    {
        var row = new WorkdayAssignment { WorkDate = new(2026, 11, 25), Status = "WORKING", StartedAt = new DateTime(2026, 11, 25, 6, 0, 0, DateTimeKind.Utc) };
        var end = ShiftExpiryService.Boundary(row.WorkDate, 1020, "Asia/Ho_Chi_Minh");
        Assert.False(ShiftExpiryService.TryExpire(row, 1020, "Asia/Ho_Chi_Minh", end.AddSeconds(-1)));
        Assert.Equal("WORKING", row.Status);
        Assert.True(ShiftExpiryService.TryExpire(row, 1020, "Asia/Ho_Chi_Minh", end));
        Assert.Equal("ENDED", row.Status);
        Assert.Equal(end, row.EndedAt);
    }

    [Fact]
    public void HistoricalLateStartPreservesTimestampInvariant()
    {
        var started = new DateTime(2026,11,24,18,0,0,DateTimeKind.Utc);
        var row = new WorkdayAssignment { WorkDate = new(2026,11,24),Status = "WORKING",StartedAt = started };
        Assert.False(ShiftExpiryService.TryExpire(row,1440,"Asia/Ho_Chi_Minh",started.AddMinutes(-1)));
        Assert.True(ShiftExpiryService.TryExpire(row,1440,"Asia/Ho_Chi_Minh",started.AddMinutes(1)));
        Assert.Equal(started,row.EndedAt);
    }
}
