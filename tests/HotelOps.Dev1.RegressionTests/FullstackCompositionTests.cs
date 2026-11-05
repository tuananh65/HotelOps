using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class FullstackCompositionTests
{
    [Fact]
    public void Model_composes_legacy_checklists_and_approved_worktime_without_database_access()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused.invalid;Database=NoConnection;Integrated Security=true;TrustServerCertificate=false").Options);
        var model = db.Model;
        Assert.NotNull(model.FindEntityType(typeof(ChecklistScopeV1)));
        Assert.NotNull(model.FindEntityType(typeof(RoomTypeBranchV1)));
        Assert.NotNull(model.FindEntityType(typeof(Workday)));
        Assert.NotNull(model.FindEntityType(typeof(WorkInterval)));
        Assert.True(model.FindEntityType(typeof(Workday))!.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Null(model.FindEntityType(typeof(OperationalTask))!.FindProperty("RowVersion"));
    }

    [Fact]
    public void Pending_recovery_controllers_are_not_discovered_as_live_routes()
    {
        var parts = new ApplicationPartManager();
        parts.ApplicationParts.Add(new AssemblyPart(typeof(AuthController).Assembly));
        parts.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature();
        parts.PopulateFeature(feature);
        var active = feature.Controllers.Select(t => t.AsType()).ToArray();
        Assert.Contains(typeof(AuthController), active);
        Assert.Contains(typeof(BranchMaintenanceController), active);
        Assert.Contains(typeof(RoomTypeBatchController), active);
        Assert.Contains(typeof(OperationalHistoryController), active);
        Assert.DoesNotContain(typeof(BranchAdministrationController), active);
        Assert.DoesNotContain(typeof(RoomTypeDirectoryController), active);
        Assert.DoesNotContain(typeof(TurnoverHistoryController), active);
        Assert.DoesNotContain(typeof(CleaningMonitorController), active);
        Assert.DoesNotContain(typeof(WorkdaysController), active);
    }
}
