using HotelOps.Api.Services.WorkTime;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class WorkdayMembershipTests
{
    private static readonly WorkdayDepartment First = new(3, "HK", "Housekeeping");
    private static readonly WorkdayDepartment Second = new(4, "MAINT", "Maintenance");

    [Fact] public void SingleMembershipIsSelectedWithoutClientDepartment()
        => Assert.Equal(3, WorkdayMembershipStore.Select([First], null));

    [Fact] public void MultipleMembershipsRequireExplicitSelection()
        => Assert.Equal("TIME_DEPARTMENT_SELECTION_REQUIRED", Assert.Throws<WorkTimeContractException>(
            () => WorkdayMembershipStore.Select([First, Second], null)).Code);

    [Fact] public void SelectionMustBelongToVerifiedMemberships()
    {
        Assert.Equal(4, WorkdayMembershipStore.Select([First, Second], 4));
        Assert.Equal("TIME_SCOPE_DENIED", Assert.Throws<WorkTimeContractException>(
            () => WorkdayMembershipStore.Select([First, Second], 9)).Code);
    }

    [Fact] public void NoMembershipCannotFallBackToUserDepartment()
        => Assert.Equal("TIME_MEMBERSHIP_REQUIRED", Assert.Throws<WorkTimeContractException>(
            () => WorkdayMembershipStore.Select([], 3)).Code);
}
