using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class SupplyMathTests
{
    [Fact] public void AvailabilityExcludesReservedButRemainingIncludesIt()
    {
        var balance=new StaffSupplyBalance{InitialIssued=10,AdditionalIssued=3,Used=4,Returned=2,Reserved=5};
        Assert.Equal(7,SupplyMath.Remaining(balance));
        Assert.Equal(2,SupplyMath.Available(balance));
    }
    [Theory]
    [InlineData(1,1,1)]
    [InlineData(4,1,4)]
    [InlineData(3,2,6)]
    public void RoomCapacityDeterminesRequiredSupplies(int capacity,int perGuest,int expected)=>Assert.Equal(expected,SupplyMath.Required(capacity,perGuest));
    [Theory]
    [InlineData(0,1)]
    [InlineData(2,0)]
    [InlineData(-1,1)]
    public void InvalidCapacityOrRateIsRejected(int capacity,int rate)=>Assert.Throws<ApiException>(()=>SupplyMath.Required(capacity,rate));
    [Fact] public void QuantitiesRespectDatabasePrecision()
    {
        Assert.True(SupplyMath.ValidQuantity(0.0001m));
        Assert.False(SupplyMath.ValidQuantity(0.00001m));
        Assert.False(SupplyMath.ValidQuantity(-1));
        Assert.False(SupplyMath.ValidQuantity(100000000000000m));
    }
}
