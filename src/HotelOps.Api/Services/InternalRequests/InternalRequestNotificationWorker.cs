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
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception error){logger.LogWarning("Internal request notification scan retained after {ErrorType}",error.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds),clock,ct);
        }
    }
}
