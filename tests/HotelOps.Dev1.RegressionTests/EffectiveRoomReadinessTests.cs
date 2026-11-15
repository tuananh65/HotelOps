using HotelOps.Api.Services.Rooms;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class EffectiveRoomReadinessTests
{
    private static EffectiveRoomReadiness.Facts Passed() => new() { RoomId=1, IsActive=true, SourceAvailable=true,
        Cleaning="COMPLETED", Inspection="PASSED", EverPassed=true };

    [Fact]
    public void InactiveAndEffectiveBlockOverridePassedInspection()
    {
        var source=Passed(); Assert.Equal("READY",EffectiveRoomReadiness.Calculate(source));
        source.Blocked=true; Assert.Equal("BLOCKED",EffectiveRoomReadiness.Calculate(source));
        source.IsActive=false; Assert.Equal("INACTIVE",EffectiveRoomReadiness.Calculate(source));
    }

    [Fact]
    public void RemovalMustRecalculateCurrentCycleRatherThanRestoreReady()
    {
        var source=Passed(); source.Blocked=true;source.Cleaning="REWORK_REQUIRED";source.ReworkOutstanding=true;
        Assert.Equal("BLOCKED",EffectiveRoomReadiness.Calculate(source));source.Blocked=false;
        Assert.Equal("REWORK_REQUIRED",EffectiveRoomReadiness.Calculate(source));
        source.ReworkOutstanding=false;source.ReworkSubmitted=true;
        Assert.Equal("PENDING_INSPECTION",EffectiveRoomReadiness.Calculate(source));
    }

    [Theory]
    [InlineData("IN_PROGRESS","CLEANING_IN_PROGRESS")]
    [InlineData("ASSIGNED","PENDING_CLEANING")]
    [InlineData("PENDING_ASSIGNMENT","PENDING_CLEANING")]
    [InlineData("ON_HOLD","PENDING_CLEANING")]
    [InlineData("PENDING_INSPECTION","PENDING_INSPECTION")]
    [InlineData("UNKNOWN","UNKNOWN")]
    public void CleaningPrioritiesFollowGbr044(string cleaning,string expected)
    {
        var source=Passed();source.Cleaning=cleaning;Assert.Equal(expected,EffectiveRoomReadiness.Calculate(source));
    }

    [Fact]
    public void MissingSourceAndFailedCurrentInspectionCannotAuthorizeUse()
    {
        var source=Passed();source.SourceAvailable=false;
        Assert.Equal("UNKNOWN",EffectiveRoomReadiness.Calculate(source));source.SourceAvailable=true;source.Inspection="FAILED";
        Assert.Equal("UNKNOWN",EffectiveRoomReadiness.Calculate(source));source.EverPassed=false;
        Assert.Equal("PENDING_CLEANING",EffectiveRoomReadiness.Calculate(source));
        Assert.False(new EffectiveRoomState(1,"UNKNOWN").CanUse);
        Assert.False(new EffectiveRoomState(1,"BLOCKED").CanUse);
    }
}
