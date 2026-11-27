using System.Reflection;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class HistoricalSupplyEvidenceTests
{
    private static bool Valid(SupplyReservation[] rows,StaffSupplyLedger[] uses)=>(bool)typeof(StaffSupplyService)
        .GetMethod("HasValidHistoricalConsumption",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[rows,uses])!;

    [Theory]
    [InlineData(0,false)]
    [InlineData(1,false)]
    [InlineData(2,true)]
    [InlineData(3,false)]
    public void HistoricalConsumptionRequiresExactMatchingLedger(decimal quantity,bool expected)
    {
        var row=new SupplyReservation{ReservationId=1,BalanceId=2,Status="CONSUMED",Quantity=2};
        var uses=quantity==0?Array.Empty<StaffSupplyLedger>():[new StaffSupplyLedger{ReservationId=1,BalanceId=2,Kind="USE",Quantity=quantity}];
        Assert.Equal(expected,Valid([row],uses));
    }

    [Fact]
    public void OrphanedLedgerAndUnconsumedHoldsAreNotConfirmedHistory()
    {
        var row=new SupplyReservation{ReservationId=1,BalanceId=2,Status="CONSUMED",Quantity=0};
        Assert.True(Valid([row],[]));
        Assert.False(Valid([row],[new StaffSupplyLedger{ReservationId=3,BalanceId=2,Kind="USE",Quantity=1}]));
        row.Status="RESERVED";
        Assert.False(Valid([row],[]));
    }
}
