using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.MaterialRequests;

internal sealed class MaterialApprovalNotificationOptions
{
    public bool Enabled { get; set; }
    public int PollSeconds { get; set; } = 30;
}

internal sealed class MaterialApprovalNotificationWorker(IServiceScopeFactory scopes,
    IOptions<MaterialApprovalNotificationOptions> options, TimeProvider clock,
    ILogger<MaterialApprovalNotificationWorker> logger) : BackgroundService
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
                await using (var scope = scopes.CreateAsyncScope())
                    pending = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingAsync(cursor, ct);
                foreach (var id in pending)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverAsync(id, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material approval notice {CycleId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                // Wrap after each complete scan, including recipients temporarily denied.
                cursor = pending.Length == 100 ? pending[^1] : 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Material approval notification scan retained after {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, ct);
        }
    }
}
