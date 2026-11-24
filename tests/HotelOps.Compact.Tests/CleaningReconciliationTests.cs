using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class CleaningReconciliationTests
{
    [Theory]
    [InlineData(-2,-1,true)]
    [InlineData(0,-1,false)]
    [InlineData(-1,0,false)]
    [InlineData(1,-1,false)]
    [InlineData(-1,1,false)]
    public void InitialAllocationOnlyForStrictlyPreCutoverWorkAndAttendance(int shiftOffset,int workOffset,bool expected)
    {
        var cutover=new DateTime(2026,11,24,0,0,0,DateTimeKind.Utc);
        Assert.Equal(expected,LegacySupplyRecoveryPolicy.CanInitialize(cutover,cutover.AddTicks(shiftOffset),cutover.AddTicks(workOffset)));
    }

    [Fact]
    public void UnknownCutoverOrMissingActualStartCannotAuthorizeStockIssue()
    {
        var now=DateTime.UtcNow;
        Assert.False(LegacySupplyRecoveryPolicy.CanInitialize(null,now,now));
        Assert.False(LegacySupplyRecoveryPolicy.CanInitialize(now,null,now.AddDays(-1)));
        Assert.False(LegacySupplyRecoveryPolicy.CanInitialize(now,now.AddDays(-1),null));
    }
    [Theory]
    [InlineData("IN_PROGRESS")]
    [InlineData("ON_HOLD")]
    public void StartedCleaningCanBeReconciledWithoutRestarting(string status)=>
        Assert.True(CleaningReconciliationPolicy.CanReconcile("CLEANING",status,DateTime.UtcNow.AddDays(-1)));

    [Theory]
    [InlineData("ASSIGNED")]
    [InlineData("PENDING_INSPECTION")]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    [InlineData("REWORK_REQUIRED")]
    public void UnstartedSubmittedOrTerminalWorkCannotBeReconciled(string status)=>
        Assert.False(CleaningReconciliationPolicy.CanReconcile("CLEANING",status,DateTime.UtcNow));

    [Fact]
    public void MissingActualStartCannotBeReconciled()=>
        Assert.False(CleaningReconciliationPolicy.CanReconcile("CLEANING","IN_PROGRESS",null));

    [Theory]
    [InlineData("REWORK")]
    [InlineData("MAINTENANCE")]
    [InlineData("OPERATIONAL")]
    public void OtherKindsCannotReserveCleaningSupplies(string kind)=>
        Assert.False(CleaningReconciliationPolicy.CanReconcile(kind,"IN_PROGRESS",DateTime.UtcNow));
}
