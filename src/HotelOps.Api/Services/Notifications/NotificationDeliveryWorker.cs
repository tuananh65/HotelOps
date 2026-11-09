using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

public interface INotificationDeliveryBatch
{
    Task<int> RunBatchAsync(CancellationToken ct);
}

public sealed class NotificationWorkerOptions
{
    public bool Enabled { get; init; }
    // An enabled rollout must explicitly choose its polling cadence. No production default.
    public int PollIntervalSeconds { get; init; }
}

public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, TimeProvider clock,
    IOptions<NotificationWorkerOptions> options, ILogger<NotificationDeliveryWorker>? logger = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        if (options.Value.PollIntervalSeconds is < 1 or > 3600)
            throw new InvalidOperationException("Explicit NotificationDelivery polling interval must be 1..3600 seconds.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<INotificationDeliveryBatch>().RunBatchAsync(stoppingToken);
                }
                catch (Exception error) when (NotificationDeliveryFailures.IsRetryable(error))
                {
                    logger?.LogWarning("Notification batch temporarily unavailable; pending intents remain durable.");
                }
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        // Permanent failures and SQL1205 fault the worker; never log them as a successful retry.
    }
}
