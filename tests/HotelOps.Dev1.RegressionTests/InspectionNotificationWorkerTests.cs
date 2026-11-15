using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InspectionNotificationWorkerTests
{
    [Fact]
    public async Task Failure_does_not_poison_next_context_or_starve_later_events()
    {
        var attempts=new List<long>(); var instances=new List<Fake>();
        await using var services=new ServiceCollection().AddScoped<IInspectionNotificationDelivery>(_ =>
        {var value=new Fake(attempts,100);instances.Add(value);return value;}).BuildServiceProvider();
        using var worker=Worker(services);
        Assert.Equal(100,await worker.ScanAsync(0,default));
        Assert.Equal(Enumerable.Range(1,100).Select(i=>(long)i),attempts);
        Assert.Equal(101,instances.Count);
        Assert.All(instances,x=>Assert.True(x.Disposed));
        Assert.Equal(101,instances.Distinct().Count());
        Assert.All(instances.Skip(1),x=>Assert.Equal(1,x.Writes));
    }

    [Fact]
    public async Task End_of_scan_wraps_to_retry_pending_events_after_restart_or_access_recovery()
    {
        var attempts=new List<long>();
        await using var services=new ServiceCollection().AddScoped<IInspectionNotificationDelivery>(_=>new Fake(attempts,2)).BuildServiceProvider();
        using var worker=Worker(services);
        Assert.Equal(0,await worker.ScanAsync(100,default));
        Assert.Equal(new long[]{101,102},attempts);
        Assert.Equal(0,await worker.ScanAsync(0,default));
        Assert.Equal(new long[]{101,102,1,2},attempts);
    }

    [Fact]
    public async Task Disabled_worker_never_resolves_database_delivery()
    {
        await using var services=new ServiceCollection().AddScoped<IInspectionNotificationDelivery>(_=>throw new Exception("Must not resolve")).BuildServiceProvider();
        using var worker=Worker(services);
        await worker.StartAsync(default);
        await worker.ExecuteTask!;
        await worker.StopAsync(default);
    }

    [Fact]
    public async Task Cancellation_stops_scan_without_swallowing_shutdown()
    {
        var attempts=new List<long>();
        await using var services=new ServiceCollection().AddScoped<IInspectionNotificationDelivery>(_=>new Fake(attempts,2)).BuildServiceProvider();
        using var worker=Worker(services);using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>worker.ScanAsync(0,cancellation.Token));
        Assert.Empty(attempts);
    }

    private static InspectionNotificationWorker Worker(ServiceProvider services)=>new(services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new InspectionNotificationWorkerOptions()),TimeProvider.System,NullLogger<InspectionNotificationWorker>.Instance);

    private sealed class Fake(List<long> attempts,int count):IInspectionNotificationDelivery,IDisposable
    {
        public bool Disposed {get;private set;}
        public int Writes {get;private set;}
        public Task<InspectionNotificationHint[]> ReadPendingAsync(int limit,long cursor,CancellationToken ct)
            => Task.FromResult(Enumerable.Range(1,count).Select(i=>new InspectionNotificationHint(cursor+i,7)).ToArray());
        public Task<string> DeliverAsync(long audit,long recipient,CancellationToken ct)
        {
            Writes++;attempts.Add(audit);
            if(audit%2==1)throw new InvalidOperationException("Synthetic delivery fault");
            return Task.FromResult("RECIPIENT_READ_DENIED");
        }
        public void Dispose()=>Disposed=true;
    }
}
