using System.Reflection;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class MaintenanceChecklistConfigurationTests
{
    private static string[] Kinds(params string[] roles) => (string[])typeof(TemplateService)
        .GetMethod("ChecklistKindsForRoles",BindingFlags.NonPublic|BindingFlags.Static)!
        .Invoke(null,[roles])!;

    [Fact]
    public void MaintenanceSupervisorCannotConfigureAnyChecklist() =>
        Assert.Empty(Kinds("MAINTENANCE_SUPERVISOR"));

    [Fact]
    public void MissingConfigurationRoleCannotConfigureAnyChecklist() =>
        Assert.Empty(Kinds());

    [Fact]
    public void HousekeepingRightsRemainLimitedWhenMaintenanceRoleIsAlsoAssigned() =>
        Assert.Equal(new[]{"CLEANING","INSPECTION"},Kinds("HOUSEKEEPING_SUPERVISOR","MAINTENANCE_SUPERVISOR"));

    [Fact]
    public void OtherGrantedConfigurationRolesRetainTheirRights() =>
        Assert.Equal(new[]{"CLEANING","INSPECTION","MAINTENANCE","OPERATIONAL"},Kinds("OPERATION_MANAGER","MAINTENANCE_SUPERVISOR"));
}
