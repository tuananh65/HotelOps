using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;
public class AttendanceSummaryTests
{
    private static readonly DateOnly Day = new(2026, 11, 23);
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void OnlyEndedActualIntervalsCountNotPlannedOrWorkingHours()
    {
        AttendanceEntry[] rows = [new(Day,"ENDED",Start,Start.AddHours(2.5)),
            new(Day,"WORKING",Start,null),new(Day,"PLANNED",null,null),new(Day,"CANCELLED",null,null)];
        var result=WorkdayService.AttendanceTotal("WEEK",Day,Day.AddDays(6),rows);
        Assert.Equal(1,result.CompletedShifts);Assert.Equal(2.5m,result.WorkedHours);
        Assert.Equal(9000,result.WorkedSeconds);Assert.Equal(1,result.WorkingShifts);
    }
    [Fact]
    public void MissingInvalidAndOutsidePeriodEntriesAreExcluded()
    {
        AttendanceEntry[] rows=[new(Day,"ENDED",null,Start),new(Day,"ENDED",Start,Start.AddSeconds(-1)),
            new(Day.AddDays(-1),"ENDED",Start,Start.AddHours(8))];
        var result=WorkdayService.AttendanceTotal("MONTH",Day,Day.AddDays(6),rows);
        Assert.Equal(0,result.CompletedShifts);Assert.Equal(0,result.WorkedSeconds);
    }
    [Fact]
    public void TotalsKeepSecondsBeforeRoundingAndWeekMayCrossMonth()
    {
        var sunday=new DateOnly(2026,12,6);var monday=WorkdayService.WeekStart(sunday);
        var rows=new[]{new AttendanceEntry(monday,"ENDED",Start,Start.AddSeconds(90)),new AttendanceEntry(sunday,"ENDED",Start,Start.AddSeconds(90))};
        var result=WorkdayService.AttendanceTotal("WEEK",monday,sunday,rows);
        Assert.Equal(new DateOnly(2026,11,30),monday);Assert.Equal(180,result.WorkedSeconds);Assert.Equal(.05m,result.WorkedHours);
    }
}
