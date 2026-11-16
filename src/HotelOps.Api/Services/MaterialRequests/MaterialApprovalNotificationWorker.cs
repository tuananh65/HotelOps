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
        long cursor = 0, cancellationCursor = 0, decisionCursor = 0, remainderCursor = 0, fulfillmentCursor = 0;
        long[] storeCursors = [0, 0];
        long replenishmentCursor = 0, replenishmentCreatedCursor = 0;
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
                    remainders = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingDecisionsAsync(remainderCursor, ct, MaterialApprovalNotificationDelivery.NoticeType.Remainder);
                foreach (var id in remainders)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverDecisionAsync(id, ct, MaterialApprovalNotificationDelivery.NoticeType.Remainder); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material remainder notice {RequestId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                remainderCursor = remainders.Length == 100 ? remainders[^1] : 0;
                long[] fulfillments;
                await using (var scope = scopes.CreateAsyncScope())
                    fulfillments = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingDecisionsAsync(fulfillmentCursor, ct, MaterialApprovalNotificationDelivery.NoticeType.Fulfillment);
                foreach (var id in fulfillments)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverDecisionAsync(id, ct, MaterialApprovalNotificationDelivery.NoticeType.Fulfillment); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material pickup notice {FulfillmentId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                fulfillmentCursor = fulfillments.Length == 100 ? fulfillments[^1] : 0;
                long[] replenishments;
                await using (var scope = scopes.CreateAsyncScope())
                    replenishments = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>()
                        .PendingDecisionsAsync(replenishmentCursor, ct, MaterialApprovalNotificationDelivery.NoticeType.ReplenishmentReceipt);
                foreach (var id in replenishments)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>()
                        .DeliverDecisionAsync(id, ct, MaterialApprovalNotificationDelivery.NoticeType.ReplenishmentReceipt); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Material replenishment notice {CreditId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                replenishmentCursor = replenishments.Length == 100 ? replenishments[^1] : 0;
                long[] createdReplenishments;
                await using (var scope = scopes.CreateAsyncScope())
                    createdReplenishments = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>()
                        .PendingReplenishmentCreatedAsync(replenishmentCreatedCursor, ct);
                foreach (var id in createdReplenishments)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverReplenishmentCreatedAsync(id, ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error) { logger.LogWarning("Replenishment creation notice {ReplenishmentId} retained after {ErrorType}", id, error.GetType().Name); }
                }
                replenishmentCreatedCursor = createdReplenishments.Length == 100 ? createdReplenishments[^1] : 0;
                for (var mode = 0; mode < 2; mode++)
                {
                    long[] events;
                    await using (var scope = scopes.CreateAsyncScope())
                        events = await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().PendingStoreAsync(storeCursors[mode], mode == 1, ct);
                    foreach (var id in events)
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        try { await scope.ServiceProvider.GetRequiredService<MaterialApprovalNotificationDelivery>().DeliverStoreAsync(id, mode == 1, ct); }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception error) { logger.LogWarning("Material storekeeper notice {EventId} retained after {ErrorType}", id, error.GetType().Name); }
                    }
                    storeCursors[mode] = events.Length == 100 ? events[^1] : 0;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Material approval notification scan retained after {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, ct);
        }
    }
}
