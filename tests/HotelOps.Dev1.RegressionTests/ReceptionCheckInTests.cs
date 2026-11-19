using HotelOps.Api.Services.Rooms;
using HotelOps.Api.DTOs.Rooms;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReceptionCheckInTests
{
    [Theory]
    [InlineData(true, "OCCUPIED", false, true)]
    [InlineData(true, "OCCUPIED", true, false)]
    [InlineData(false, "OCCUPIED", false, false)]
    [InlineData(true, "READY", false, false)]
    [InlineData(true, "DIRTY", false, false)]
    public void DepartureRequiresOccupiedActiveRoomWithoutOpenWork(bool active, string status, bool open, bool expected)
        => Assert.Equal(expected, RoomCheckInService.CanDepart(active, status, open));

    [Theory]
    [InlineData("OCCUPIED", true, "OCCUPIED")]
    [InlineData("OCCUPIED", false, "OCCUPIED")]
    [InlineData("CLEANING", true, "READY")]
    [InlineData("CLEANING", false, "CLEANING")]
    public void InspectionCannotReleaseOccupiedRoom(string before, bool ready, string expected)
        => Assert.Equal(expected, EffectiveRoomReadiness.InspectionOperationalStatus(before, ready));

    [Fact]
    public void CheckoutRequiresANewCycleEvenWhenEarlierInspectionPassed()
    {
        var facts = new EffectiveRoomReadiness.Facts { IsActive = true, SourceAvailable = true,
            EverPassed = true, AwaitingNewCycle = true };
        Assert.Equal("PENDING_CLEANING", EffectiveRoomReadiness.Calculate(facts));
        facts.Blocked = true;
        Assert.Equal("BLOCKED", EffectiveRoomReadiness.Calculate(facts));
        facts.Blocked = false;
        facts.AwaitingNewCycle = false; facts.Cleaning = "ASSIGNED";
        Assert.Equal("PENDING_CLEANING", EffectiveRoomReadiness.Calculate(facts));
        facts.Cleaning = "PENDING_INSPECTION";
        Assert.Equal("PENDING_INSPECTION", EffectiveRoomReadiness.Calculate(facts));
        facts.Cleaning = "COMPLETED"; facts.Inspection = "PASSED"; facts.CompletedCycleVerified = true;
        Assert.Equal("READY", EffectiveRoomReadiness.Calculate(facts));
    }

    [Theory]
    [InlineData(true, "READY", "READY", true)]
    [InlineData(true, "AVAILABLE", "READY", true)]
    [InlineData(true, "OCCUPIED", "READY", false)]
    [InlineData(true, "OUT_OF_SERVICE", "READY", false)]
    [InlineData(true, "DIRTY", "READY", false)]
    [InlineData(true, "READY", "BLOCKED", false)]
    [InlineData(true, "READY", "PENDING_CLEANING", false)]
    [InlineData(true, "READY", "PENDING_INSPECTION", false)]
    [InlineData(true, "READY", "REWORK_REQUIRED", false)]
    [InlineData(true, "READY", "UNKNOWN", false)]
    [InlineData(false, "READY", "READY", false)]
    public void ReceivingGuestsRequiresBothRecordedAndEffectiveReadiness(bool active, string recorded, string readiness, bool expected)
        => Assert.Equal(expected, RoomCheckInService.Eligible(active, recorded, readiness));

    [Fact]
    public void PassedInspectionDoesNotMakeOccupiedRoomUsable()
    {
        var room = new RoomDirectoryItemDto(1, 1, "DEMO", new(1, "F1", "Tầng 1", 1), null,
            new(1, "DEMO", "Demo"), "OCCUPIED", true) { EffectiveReadiness = "READY" };
        Assert.False(room.CanUseRoom);
        Assert.True((room with { OperationalStatusCode = "READY" }).CanUseRoom);
    }
}
