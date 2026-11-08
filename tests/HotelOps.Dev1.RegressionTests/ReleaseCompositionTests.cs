using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReleaseCompositionTests
{
    [Fact]
    public void Release_model_preserves_legacy_and_composes_required_FE04_dependencies()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var model = db.Model;
        Assert.Equal("workdays", model.FindEntityType(typeof(Workday))!.GetTableName());
        Assert.Equal("work_intervals", model.FindEntityType(typeof(WorkInterval))!.GetTableName());
        Assert.True(model.FindEntityType(typeof(WorkInterval))!.FindProperty(nameof(WorkInterval.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("notification_operation_reservations", model.FindEntityType(typeof(NotificationOperationCandidate))!.GetTableName());
        Assert.Equal("notification_publications", model.FindEntityType(typeof(NotificationPublicationCandidate))!.GetTableName());
        Assert.NotNull(model.FindEntityType(typeof(ChecklistScopeV1)));
        Assert.NotNull(model.FindEntityType(typeof(RoomTypeBranchV1)));
        Assert.Null(model.FindEntityType(typeof(OperationalTask))!.FindProperty("RowVersion"));
    }

    [Fact]
    public async Task Missing_connection_configuration_fails_startup_schema_validation_without_SQL_target()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var result = await new StartupSchemaValidator(db).ValidateAsync(CancellationToken.None);
        Assert.Equal(SchemaCompatibility.UnexpectedValidationError, result);
    }
}
