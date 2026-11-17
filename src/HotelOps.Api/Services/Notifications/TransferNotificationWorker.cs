using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TransferNotificationOptions { public bool Enabled { get; set; } }

internal sealed class TransferNotificationWorker(IServiceScopeFactory scopes, IOptions<TransferNotificationOptions> options,
    ILogger<TransferNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        long cursor = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                long[] pending;
                await using (var discovery = scopes.CreateAsyncScope())
                    pending = await discovery.ServiceProvider.GetRequiredService<TransferNotifications>().PendingAsync(cursor, ct);
                foreach (var id in pending)
                {
                    try
                    {
                        await using var delivery = scopes.CreateAsyncScope();
                        await delivery.ServiceProvider.GetRequiredService<TransferNotifications>().DeliverAsync(id, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception e) { logger.LogWarning("Transfer notice {NotificationId} deferred after {FailureType}", id, e.GetType().Name); }
                }
                cursor = pending.Length == 100 ? pending[^1] : 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("Transfer delivery deferred after {FailureType}", e.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
