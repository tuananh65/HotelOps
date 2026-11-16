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
    private long reassignmentCursor;
    private readonly Dictionary<CleaningDeliveryQueue,long> cursors = new();
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
            await DrainIndependentlyAsync(CleaningDeliveryQueue.Assignment,ct);
            try
            {
                ProvisionalCleaningReassignmentDelivery.PendingIntent[] pending;
                await using(var discovery=scopes.CreateAsyncScope())
                    pending=await discovery.ServiceProvider.GetRequiredService<ProvisionalCleaningReassignmentDelivery>().PendingAsync(ct,reassignmentCursor);
                foreach(var intent in pending)
                {
                    try
                    {
                        // A failed recipient must not prevent delivery to the other recipient.
                        await using var scope=scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<ProvisionalCleaningReassignmentDelivery>()
                            .DeliverOneAsync(intent.Actor,intent.Operation,ct,intent.Id);
                    }
                    catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Cleaning reassignment delivery retained after {ErrorType}",error.GetType().Name);}
                }
                // Revisit retained failures after scanning the backlog, including after process restart.
                reassignmentCursor=pending.Length==100?pending[^1].Id:0;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception error){reassignmentCursor=0;logger.LogWarning("Cleaning reassignment discovery retained after {ErrorType}",error.GetType().Name);}
        }
        if (options.Value.Enabled) await DrainIndependentlyAsync(CleaningDeliveryQueue.Closure, ct);
        if (options.Value.CancellationEnabled) await DrainIndependentlyAsync(CleaningDeliveryQueue.Cancellation, ct);
    }

    private async Task DrainIndependentlyAsync(CleaningDeliveryQueue queue, CancellationToken ct)
    {
        try { await DrainKindAsync(queue, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            cursors[queue]=0;
            logger.LogWarning("Cleaning {Workflow} delivery retained after {ErrorType}",
                queue, error.GetType().Name);
        }
    }

    private async Task DrainKindAsync(CleaningDeliveryQueue queue, CancellationToken ct)
    {
        long[] pending;
        await using(var discovery=scopes.CreateAsyncScope())
            pending=await discovery.ServiceProvider.GetRequiredService<ProvisionalCleaningDelivery>()
                .PendingAsync(queue,cursors.GetValueOrDefault(queue),ct);
        foreach(var id in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Retain failed intents, but continue this page with independent transaction contexts.
                await using var scope=scopes.CreateAsyncScope();
                var delivery=scope.ServiceProvider.GetRequiredService<ProvisionalCleaningDelivery>();
                _=queue switch
                {
                    CleaningDeliveryQueue.Assignment=>await delivery.DeliverAssignmentOnlyAsync(ct,id),
                    CleaningDeliveryQueue.Closure=>await delivery.DeliverClosureOnlyAsync(ct,id),
                    CleaningDeliveryQueue.Cancellation=>await delivery.DeliverCancellationOnlyAsync(ct,id),
                    _=>throw new InvalidOperationException("Unknown cleaning delivery queue.")
                };
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception error){logger.LogWarning("Cleaning {Workflow} intent {IntentId} retained after {ErrorType}",queue,id,error.GetType().Name);}
        }
        cursors[queue]=pending.Length==100?pending[^1]:0;
    }
}
