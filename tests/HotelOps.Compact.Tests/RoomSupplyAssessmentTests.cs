using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class RoomSupplyAssessmentTests
{
    [Fact] public void OnlyUsableExistingItemsReduceRoomNeed()=>Assert.Equal(2,RoomSupplyAssessmentRules.Validate(3,new(1,1,"version",3,1,1,2)));
    [Fact] public void PartialActualRefillCanBeRecordedWithoutInventingFullConsumption()=>Assert.Equal(2,RoomSupplyAssessmentRules.Validate(3,new(1,1,"version",1,0,0,1)));
    [Fact] public void ExpiredAndUnusableCannotBeCountedTwice()=>Assert.Throws<ApiException>(()=>RoomSupplyAssessmentRules.Validate(3,new(1,2,"version",3,1,1,1)));
    [Fact] public void RefillCannotExceedActualMissing()=>Assert.Throws<ApiException>(()=>RoomSupplyAssessmentRules.Validate(3,new(1,2,"version",2,0,0,2)));
    [Fact] public void LegacyAssessmentRequiresActualCounts()=>Assert.Throws<ApiException>(()=>RoomSupplyAssessmentRules.Validate(3,new(1,1,"version")));
    [Fact] public void NoRefillNeededWhenUsableExceedsTarget()=>Assert.Equal(0,RoomSupplyAssessmentRules.Validate(3,new(1,4,"version",4,0,0,0)));
}
