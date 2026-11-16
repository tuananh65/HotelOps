using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.WorkTime;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskWorkloadContractTests
{
    [Theory]
    [InlineData("OPEN")]
    [InlineData("OVERDUE")]
    [InlineData("CLOSED_IN_PERIOD")]
    public void Dashboard_population_is_explicit_and_unknown_values_are_rejected(string taskSet)
    {
        RoomTypeCreationService.ValidateWorkloadQuery(new(TaskSet: taskSet));
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.ValidateWorkloadQuery(new(TaskSet: "ALL")));
    }
    private static readonly DateTime Now = new(2026, 11, 15, 18, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void Dashboard_utc_workload_preserves_full_period_without_relaxing_workday_editor_bounds()
    {
        var from = new DateOnly(2020, 1, 1);
        var to = new DateOnly(2026, 11, 16);
        var period = WorkTimePeriod.ForWorkload(from, to, Now, "UTC");
        Assert.Equal(from, period.From);
        Assert.Equal(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), period.End);
        Assert.Throws<ValidationException>(() => WorkTimePeriod.Create(from, to, Now, "UTC"));
        Assert.Throws<ValidationException>(() => WorkTimePeriod.ForWorkload(from, to, Now, "Asia/Ho_Chi_Minh"));
        Assert.Throws<ValidationException>(() => WorkTimePeriod.ForWorkload(to, from, Now, "UTC"));
        Assert.Throws<ValidationException>(() => WorkTimePeriod.ForWorkload(from, DateOnly.MaxValue, Now, "UTC"));
    }
    [Fact]
    public void Explicit_utc_calendar_preserves_dashboard_boundaries_without_changing_hotel_default()
    {
        var utc = WorkTimePeriod.Create(null, null, Now, "UTC");
        Assert.Equal(new DateOnly(2026, 11, 15), utc.From);
        Assert.Equal(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc), utc.Start);
        Assert.Equal(utc.Start.AddDays(1), utc.End);
        Assert.Equal(600, utc.Seconds(utc.Start.AddMinutes(-10), utc.Start.AddMinutes(10), Now));
        Assert.Throws<ValidationException>(() => WorkTimePeriod.Create(null, null, Now, "CLIENT"));
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.ValidateWorkloadQuery(new(Calendar: "CLIENT")));
    }
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
