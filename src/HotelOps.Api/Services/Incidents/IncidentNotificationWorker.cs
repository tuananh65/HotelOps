using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Incidents;

internal sealed class IncidentNotificationOptions
{
    public bool Enabled { get; set; }
    public int PollSeconds { get; set; } = 30;
}

internal sealed class IncidentNotificationWorker(IServiceScopeFactory scopes, IOptions<IncidentNotificationOptions> options,
    TimeProvider clock, ILogger<IncidentNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        long cursor = 0, cancellationCursor = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                long[] pending;
                await using (var scope = scopes.CreateAsyncScope())
                    pending = await scope.ServiceProvider.GetRequiredService<IncidentReportedNotificationDelivery>().PendingAsync(cursor, ct);
                foreach (var id in pending)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<IncidentReportedNotificationDelivery>().DeliverAsync(id, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Incident notice {EventId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                cursor = pending.Length == 100 ? pending[^1] : 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Incident notification scan deferred after {ErrorType}", error.GetType().Name); }
            try
            {
                long[] pending;
                await using (var scope = scopes.CreateAsyncScope())
                    pending = await scope.ServiceProvider.GetRequiredService<IncidentCancelledNotificationDelivery>().PendingAsync(cancellationCursor, ct);
                foreach (var id in pending)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<IncidentCancelledNotificationDelivery>().DeliverAsync(id, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Incident cancellation notice {EventId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                cancellationCursor = pending.Length == 100 ? pending[^1] : 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Incident cancellation scan deferred after {ErrorType}", error.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
