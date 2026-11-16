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
        long cursor = 0, cancellationCursor = 0, decisionCursor = 0, remainderCursor = 0;
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
                long[] cancellations;
                await using (var scope = scopes.CreateAsyncScope())
                    cancellations = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingCancellationsAsync(cancellationCursor, ct);
                foreach (var id in cancellations)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverCancellationAsync(id, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material cancellation notice {NotificationId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                cancellationCursor = cancellations.Length == 100 ? cancellations[^1] : 0;
                long[] decisions;
                await using (var scope = scopes.CreateAsyncScope())
                    decisions = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingDecisionsAsync(decisionCursor, ct);
                foreach (var id in decisions)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverDecisionAsync(id, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material decision notice {ApprovalId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                decisionCursor = decisions.Length == 100 ? decisions[^1] : 0;
                long[] remainders;
                await using (var scope = scopes.CreateAsyncScope())
                    remainders = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingDecisionsAsync(remainderCursor, ct, remainder: true);
                foreach (var id in remainders)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverDecisionAsync(id, ct, remainder: true); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material remainder notice {RequestId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                remainderCursor = remainders.Length == 100 ? remainders[^1] : 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Material approval notification scan retained after {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, ct);
        }
    }
}
