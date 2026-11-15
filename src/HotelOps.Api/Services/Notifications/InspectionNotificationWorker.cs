using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class InspectionNotificationWorkerOptions
{
    // Explicit deployment opt-in; merely upgrading the application starts no background writes.
    public bool Enabled { get; set; }
    public int PollSeconds { get; set; } = 30;
}

// Audit events survive restart. The cursor is only a bounded scan optimization, never a
// delivery receipt: wrap after each pass so revoked access or temporary faults can recover.
internal sealed class InspectionNotificationWorker(IServiceScopeFactory scopes,
    IOptions<InspectionNotificationWorkerOptions> options, TimeProvider clock,
    ILogger<InspectionNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        long cursor = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { cursor = await ScanAsync(cursor, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                // Do not log payloads, credentials or SQL exception details.
                logger.LogWarning("Inspection notification discovery failed ({ErrorType}); durable events retained", error.GetType().Name);
                cursor = 0;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    internal async Task<long> ScanAsync(long cursor, CancellationToken ct)
    {
        InspectionNotificationHint[] pending;
        await using (var discovery = scopes.CreateAsyncScope())
            pending = await discovery.ServiceProvider.GetRequiredService<IInspectionNotificationDelivery>()
                .ReadPendingAsync(100, cursor, ct);
        foreach (var hint in pending)
        {
            ct.ThrowIfCancellationRequested();
            // Fresh context/transaction per event. A failed SaveChanges or lost commit acknowledgement
            // cannot poison the next event, and no discovery hint bypasses source/recipient checks.
            await using var attempt = scopes.CreateAsyncScope();
            try
            {
                await attempt.ServiceProvider.GetRequiredService<IInspectionNotificationDelivery>()
                    .DeliverAsync(hint.AuditId, hint.RecipientUserId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                logger.LogWarning("Inspection notification event {AuditId} pending after {ErrorType}", hint.AuditId, error.GetType().Name);
            }
        }
        // Advance even past denied/failing events, otherwise the first 100 starve later approvals.
        return pending.Length == 100 ? pending[^1].AuditId : 0;
    }
}
