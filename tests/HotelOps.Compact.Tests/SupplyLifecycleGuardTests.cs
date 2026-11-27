using System.Reflection;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class SupplyLifecycleGuardTests
{
    [Fact]
    public void ReturnDelayIsBasedOnRecordedClosureAndCrossesMidnight()
    {
        var closure=new DateTime(2026,11,27,23,58,0,DateTimeKind.Utc);
        var deadline=(DateTime)typeof(StaffSupplyService).GetMethod("ReturnDeadline",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[closure])!;
        Assert.Equal(new DateTime(2026,11,28,0,3,0,DateTimeKind.Utc),deadline);
        Assert.True(closure.AddMinutes(5).AddTicks(-1)<deadline);
        Assert.False(closure.AddMinutes(5)<deadline);
    }
    [Theory]
    [InlineData(-1,false,false)]
    [InlineData(-1,true,false)]
    [InlineData(0,false,true)]
    [InlineData(479,false,true)]
    [InlineData(480,false,false)]
    [InlineData(481,false,false)]
    [InlineData(480,true,true)]
    [InlineData(600,true,true)]
    public void OrdinarySupplyStopsAtScheduledEndWhileExistingWorkCanFinish(int minute,bool finishing,bool expected)
    {
        var start=new DateTime(2026,11,27,1,0,0,DateTimeKind.Utc);
        var result=(bool)typeof(StaffSupplyService).GetMethod("IsWithinSupplyWindow",BindingFlags.NonPublic|BindingFlags.Static)!
            .Invoke(null,[start.AddMinutes(minute),start,start.AddHours(8),finishing])!;
        Assert.Equal(expected,result);
    }

    [Theory]
    [InlineData("MISSING",3,0,3)]
    [InlineData("MISSING",3,3,3)]
    [InlineData("BROKEN",3,0,3)]
    [InlineData("BROKEN",3,1,2)]
    [InlineData("BROKEN",3,3,0)]
    public void OrdinaryExchangeExcludesUnreceivedIncidentStock(string kind,decimal reported,decimal returned,decimal expected)
    {
        var result=(decimal)typeof(ReusableInventoryService).GetMethod("IncidentReservedQuantity",BindingFlags.NonPublic|BindingFlags.Static)!
            .Invoke(null,[kind,reported,returned])!;
        Assert.Equal(expected,result);
    }
}
