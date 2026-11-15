using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class OwnTimeLogTests
{
    [Fact]
    public void DailyTotalsExcludePauseAndKeepWorkdaySeparateAcrossPages()
    {
        var now=new DateTime(2026,11,16,4,0,0,DateTimeKind.Utc);
        var period=WorkTimePeriod.Create(new(2026,11,16),new(2026,11,16),now);
        WorkInterval Interval(long id,string kind,int from,int? to)=>new(){IntervalId=id,WorkItemKind=kind,WorkItemId=id,
            EffectiveStartedAt=now.AddHours(from),EffectiveEndedAt=to.HasValue?now.AddHours(to.Value):null,RowVersion=new byte[8]};
        var rows=new[]{Interval(1,"TASK",-4,-3),Interval(2,"CLEANING",-2,-1),Interval(3,"REWORK",-1,null)};
        var result=OwnTimeLogReader.Project(1,period,now,[new(){StartedAt=now.AddHours(-4)}],rows,2,1);
        Assert.Equal(14400,result.WorkdayProvisionalSeconds);
        Assert.Equal(7200,result.WorkClosedSeconds);
        Assert.Equal(3600,result.WorkProvisionalSeconds);
        Assert.Equal(3,result.Total);
        Assert.Equal("CLEANING",Assert.Single(result.Items).Kind);
        Assert.Equal(0,result.WorkdayClosedSeconds);
    }

    [Fact]
    public void OvernightIntervalsAreClippedToSelectedHotelDate()
    {
        var now=new DateTime(2026,11,16,4,0,0,DateTimeKind.Utc);
        var period=WorkTimePeriod.Create(new(2026,11,16),new(2026,11,16),now);
        var start=period.Start.AddHours(-1);var end=period.Start.AddHours(2);
        var result=OwnTimeLogReader.Project(1,period,now,[new(){StartedAt=start,EndedAt=end}],
            [new(){IntervalId=1,WorkItemKind="INCIDENT",EffectiveStartedAt=start,EffectiveEndedAt=end,RowVersion=new byte[8]}],1,50);
        Assert.Equal(7200,result.WorkClosedSeconds);
        Assert.Equal(7200,result.WorkdayClosedSeconds);
        Assert.Equal(new DateTimeOffset(start),Assert.Single(result.Items).Start);
        Assert.Equal(0,result.WorkProvisionalSeconds);
    }
}
