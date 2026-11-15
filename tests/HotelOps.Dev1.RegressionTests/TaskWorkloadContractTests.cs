using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.WorkTime;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskWorkloadContractTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 18, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void Default_period_uses_hotel_day_and_half_open_utc_bounds()
    {
        var period = WorkTimePeriod.Create(null, null, Now);
        Assert.Equal(new DateOnly(2026, 11, 16), period.From);
        Assert.Equal(new DateTime(2026, 11, 15, 17, 0, 0, DateTimeKind.Utc), period.Start);
        Assert.Equal(period.Start.AddDays(1), period.End);
    }
    [Fact]
    public void Closed_and_open_duration_are_clipped_without_borrowing_outside_period()
    {
        var period = WorkTimePeriod.Create(null, null, Now);
        Assert.Equal(3600, period.Seconds(period.Start.AddHours(-2), Now, Now));
        Assert.Equal(3600, period.Seconds(period.Start.AddHours(-2), null, Now));
        Assert.Equal(0, period.Seconds(period.Start.AddDays(-1), period.Start, Now));
        Assert.Equal(86400, period.Seconds(period.Start.AddDays(-1), period.End.AddDays(1), Now));
        Assert.Throws<ValidationException>(() => period.Seconds(Now, Now.AddSeconds(-1), Now));
        Assert.Throws<ValidationException>(() => period.Seconds(Now.AddSeconds(1), null, Now));
    }
    [Fact]
    public void Seconds_match_existing_sql_boundary_contract()
    {
        var period = WorkTimePeriod.Create(null, null, Now);
        Assert.Equal(1, period.Seconds(period.Start.AddMilliseconds(900), period.Start.AddMilliseconds(1100), Now));
    }
    [Fact]
    public void Invalid_ranges_and_unbounded_pages_are_rejected()
    {
        Assert.Throws<ValidationException>(() => WorkTimePeriod.Create(new(2026, 11, 16), new(2026, 11, 15), Now));
        Assert.Throws<ValidationException>(() => WorkTimePeriod.Create(new(2020, 1, 1), new(2026, 1, 1), Now));
        Assert.Throws<ValidationException>(() => WorkTimePeriod.Create(DateOnly.MaxValue, DateOnly.MaxValue, Now));
        foreach (var query in new[] { new TaskWorkloadQuery(Page: int.MaxValue), new(PageSize: 101), new(EmployeeId: 0), new(Status: "COMPLETED"), new(Search: "bad\u0001") })
            Assert.Throws<ValidationException>(() => RoomTypeCreationService.ValidateWorkloadQuery(query));
    }
}
