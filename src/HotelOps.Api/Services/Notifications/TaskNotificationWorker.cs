using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskNotificationOptions
{
    public bool Enabled {get;set;}
    public int PollSeconds {get;set;}=30;
}
internal sealed class TaskNotificationWorker(IServiceScopeFactory scopes,IOptions<TaskNotificationOptions> options,
    TimeProvider clock,ILogger<TaskNotificationWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Value.Enabled)return;
        long cursor=0,verificationCursor=0;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                long[] pending;
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskSubmissionNotificationDelivery>().ReadPendingAsync(cursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskSubmissionNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                cursor=pending.Length==100?pending[^1]:0;
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskVerificationNotificationDelivery>().ReadPendingAsync(verificationCursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskVerificationNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task verification notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                verificationCursor=pending.Length==100?pending[^1]:0;
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception error){logger.LogWarning("Task notification discovery retained after {ErrorType}",error.GetType().Name);cursor=0;verificationCursor=0;}
            try{await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds),clock,ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
        }
    }
}
