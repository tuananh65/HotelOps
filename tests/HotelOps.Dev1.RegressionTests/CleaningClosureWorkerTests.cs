using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningClosureWorkerTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Cancellation_opt_in_cannot_enable_non_development_runtime(string environment)
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var publisher = new ProvisionalCleaningNotification(db,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CleaningCancellation:Capability"] = "VERIFIED_V1",
                ["CleaningCancellation:Candidate"] = "FS018_PROVISIONAL"
            }).Build(), new Environment { EnvironmentName = environment });
        Assert.False(await publisher.CancellationAvailableAsync(default));
    }

    [Fact]
    public async Task Default_disabled_worker_does_not_resolve_a_delivery_service()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var worker = new CleaningClosureNotificationWorker(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CleaningClosureNotificationOptions()), TimeProvider.System,
            NullLogger<CleaningClosureNotificationWorker>.Instance);
        await worker.StartAsync(default);
        await worker.StopAsync(default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_capability_stops_drain_without_assignment_fallback_or_sql(bool cancellation)
    {
        var attempts = 0;
        var registrations = new ServiceCollection();
        registrations.AddScoped(_ => new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options));
        registrations.AddScoped(sp => new ProvisionalCleaningNotification(sp.GetRequiredService<HotelOpsDbContext>(),
            new ConfigurationBuilder().Build(), new Environment()));
        registrations.AddScoped(sp =>
        {
            attempts++;
            return new ProvisionalCleaningDelivery(sp.GetRequiredService<HotelOpsDbContext>(),
                sp.GetRequiredService<ProvisionalCleaningNotification>(), null!, TimeProvider.System);
        });
        await using var services = registrations.BuildServiceProvider();
        using var worker = new CleaningClosureNotificationWorker(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CleaningClosureNotificationOptions { Enabled = !cancellation, CancellationEnabled = cancellation }), TimeProvider.System,
            NullLogger<CleaningClosureNotificationWorker>.Instance);
        await worker.DrainAsync(default);
        Assert.Equal(1, attempts);
    }

    private sealed class Environment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "HotelOps.Api";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
