using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestNotificationOptions
{
    public bool Enabled {get;set;}
    public int PollSeconds {get;set;}=30;
}

internal sealed class InternalRequestNotificationWorker(IServiceScopeFactory scopes,IOptions<InternalRequestNotificationOptions> options,
    TimeProvider clock,ILogger<InternalRequestNotificationWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Value.Enabled)return;
        long cursor=0;
        long assignmentCursor=0;
        long routeCursor=0;
        long progressCursor=0;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                long[] pending;
                await using(var scope=scopes.CreateAsyncScope())
                    pending=await scope.ServiceProvider.GetRequiredService<InternalRequestDecisionDelivery>().PendingAsync(cursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<InternalRequestDecisionDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Internal request notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                cursor=pending.Length==100?pending[^1]:0;
                long[] assignments;
                await using(var scope=scopes.CreateAsyncScope())
                    assignments=await scope.ServiceProvider.GetRequiredService<InternalRequestAssignmentDelivery>().PendingAsync(assignmentCursor,ct);
                foreach(var id in assignments)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<InternalRequestAssignmentDelivery>().DeliverEventAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Internal request assignment notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                assignmentCursor=assignments.Length==100?assignments[^1]:0;
                long[] routes;
                await using(var scope=scopes.CreateAsyncScope())
                    routes=await scope.ServiceProvider.GetRequiredService<InternalRequestRouteDelivery>().PendingAsync(routeCursor,ct);
                foreach(var id in routes)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<InternalRequestRouteDelivery>().DeliverEventAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Internal request route notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                routeCursor=routes.Length==100?routes[^1]:0;
                long[] progress;
                await using(var scope=scopes.CreateAsyncScope())
                    progress=await scope.ServiceProvider.GetRequiredService<InternalRequestProgressDelivery>().PendingAsync(progressCursor,100,ct);
                foreach(var id in progress)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<InternalRequestProgressDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Internal request progress notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                progressCursor=progress.Length==100?progress[^1]:0;
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception error){logger.LogWarning("Internal request notification scan retained after {ErrorType}",error.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds),clock,ct);
        }
    }
}
