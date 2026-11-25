using HotelOps.Api.Compact.Services;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class ReusableShelfLifeTests
{
 [Fact] public void DeadlineIncludesItsLocalCalendarDay(){var date=new DateOnly(2028,11,25);Assert.False(ReusableShelfLifePolicy.IsOverdue(date,date));Assert.True(ReusableShelfLifePolicy.IsOverdue(date,date.AddDays(1)));Assert.False(ReusableShelfLifePolicy.IsOverdue(null,date));}
 [Theory] [InlineData("READY","STAFF_HOLDING",true)] [InlineData("READY","IN_USE",true)] [InlineData("STAFF_HOLDING","IN_USE",true)] [InlineData("STAFF_HOLDING","READY",false)] [InlineData("IN_USE","DIRTY",false)] [InlineData("DIRTY","LAUNDRY",false)] [InlineData("READY","DAMAGED",false)]
 public void DeadlineBlocksDistributionButPreservesReturnsAndReconciliation(string source,string target,bool expected)=>Assert.Equal(expected,ReusableShelfLifePolicy.RequiresUsableStock(source,target));
}
