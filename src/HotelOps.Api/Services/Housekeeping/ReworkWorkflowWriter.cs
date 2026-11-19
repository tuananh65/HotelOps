using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Housekeeping;

// Restricted SQL principal for committed rework and reinspection workflows.
public sealed class ReworkWorkflowWriter(IConfiguration configuration, ICurrentUserAccessor current, TimeProvider clock)
{
    public async Task<RoomTypeBatchResult> ExecuteAsync(
        Func<RoomTypeCreationService, Task<RoomTypeBatchResult>> action, CancellationToken ct)
    {
        var connection = configuration.GetConnectionString("ReworkWorkflowDb");
        var primary = configuration.GetConnectionString("HotelOpsDb");
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(primary))
            return new(RoomTypeBatchStatus.Unavailable);
        var writer = new SqlConnectionStringBuilder(connection);
        var main = new SqlConnectionStringBuilder(primary);
        if (!string.Equals(writer.DataSource, main.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(writer.InitialCatalog, main.InitialCatalog, StringComparison.OrdinalIgnoreCase))
            return new(RoomTypeBatchStatus.Unavailable);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var permissions = new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), clock);
        var sessions = new SqlSessionService(db, new SessionRevocationService(db), clock);
        ct.ThrowIfCancellationRequested();
        return await action(new RoomTypeCreationService(db, current, permissions, sessions, clock));
    }
}
