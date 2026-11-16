using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class CleaningClosureNotificationOptions
{
    public bool Enabled { get; set; }
    public bool CancellationEnabled { get; set; }
    public bool AssignmentEnabled { get; set; }
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
        if (!options.Value.Enabled && !options.Value.CancellationEnabled && !options.Value.AssignmentEnabled) return;
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
        if(options.Value.AssignmentEnabled)
        {
            try
            {
                for(var attempt=0;attempt<100;attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    await using var scope=scopes.CreateAsyncScope();
                    var result=await scope.ServiceProvider.GetRequiredService<ProvisionalCleaningDelivery>().DeliverAssignmentOnlyAsync(ct);
                    if(result is "EMPTY" or "DISABLED")break;
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception error){logger.LogWarning("Cleaning assignment delivery retained after {ErrorType}",error.GetType().Name);}
        }
        if (options.Value.Enabled) await DrainIndependentlyAsync(false, ct);
        if (options.Value.CancellationEnabled) await DrainIndependentlyAsync(true, ct);
    }

    private async Task DrainIndependentlyAsync(bool cancellation, CancellationToken ct)
    {
        try { await DrainKindAsync(cancellation, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            // Retain durable intents for retry without starving the other workflow.
            logger.LogWarning("Cleaning {Workflow} delivery retained after {ErrorType}",
                cancellation ? "cancellation" : "closure", error.GetType().Name);
        }
    }

    private async Task DrainKindAsync(bool cancellation, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            // A failed command/ambiguous commit must not poison the next intent's context.
            await using var scope = scopes.CreateAsyncScope();
            var delivery = scope.ServiceProvider.GetRequiredService<ProvisionalCleaningDelivery>();
            var result = cancellation ? await delivery.DeliverCancellationOnlyAsync(ct)
                : await delivery.DeliverClosureOnlyAsync(ct);
            if (result is "EMPTY" or "DISABLED") break;
        }
    }
}
