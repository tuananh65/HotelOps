using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class CleaningClosureNotificationOptions
{
    public bool Enabled { get; set; }
    public int PollSeconds { get; set; } = 30;
}

// Reuses the durable closure intents and recipient checks; no second publication ledger.
// Deployment opt-in is independent of the source capability: both must be enabled.
internal sealed class CleaningClosureNotificationWorker(IServiceScopeFactory scopes,
    IOptions<CleaningClosureNotificationOptions> options, TimeProvider clock,
    ILogger<CleaningClosureNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try { await DrainAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogWarning("Cleaning closure delivery retained after {ErrorType}", error.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    internal async Task DrainAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            // A failed command/ambiguous commit must not poison the next intent's context.
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ProvisionalCleaningDelivery>()
                .DeliverClosureOnlyAsync(ct);
            if (result is "EMPTY" or "DISABLED") break;
        }
    }
}
