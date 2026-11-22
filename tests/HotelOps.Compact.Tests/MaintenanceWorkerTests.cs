using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class MaintenanceDatabaseFactAttribute : FactAttribute
{
    public MaintenanceDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_COMPACT_MAINTENANCE_TEST")))
            Skip = "Run Test-Backend.ps1 -MaintenanceLead against an isolated API fixture.";
    }
}

public sealed class MaintenanceWorkerTests
{
    [MaintenanceDatabaseFact]
    public async Task LeadPlanCreatesOneMaintenanceOccurrenceAcrossRepeatedTicks()
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_COMPACT_MAINTENANCE_TEST")!;
        Assert.Matches("^HotelOpsCompactApiTest[a-f0-9]{12}$", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var services = new ServiceCollection();
        services.AddDbContext<HotelOpsCompactDbContext>(o => o.UseSqlServer(connection));
        services.AddHttpContextAccessor();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<Actor>(); services.AddScoped<Access>(); services.AddScoped<Atomic>(); services.AddScoped<NotificationService>();
        await using var provider = services.BuildServiceProvider();
        var worker = new OperationsWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OperationsWorker>.Instance, new ConfigurationBuilder().Build());
        await worker.Tick(CancellationToken.None);
        await worker.Tick(CancellationToken.None);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
        var template = await db.WorkTemplates.AsNoTracking().SingleAsync(t => t.Code == "LEAD-PM-TEST");
        var task = await db.WorkItems.AsNoTracking().SingleAsync(w => w.WorkTemplateId == template.WorkTemplateId);
        Assert.Equal("MAINTENANCE", task.Kind);
        Assert.Equal("PENDING", task.Status);
        Assert.Equal(template.BranchId, task.BranchId);
        Assert.Equal(template.AssetId, task.AssetId);
        Assert.Equal(template.RoomId, task.RoomId);
        Assert.True(template.NextDueAt > DateTime.UtcNow);
        Assert.Single(await db.WorkEvents.Where(e => e.WorkId == task.WorkId && e.EventType == "SCHEDULE_GENERATED").ToArrayAsync());
        Assert.True(await db.Notifications.AnyAsync(n => n.WorkId == task.WorkId));
    }
}
