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
        long cursor=0,verificationCursor=0,declineCursor=0,assignmentCursor=0,planCursor=0,seriesCursor=0,reopenCursor=0,workdayCursor=0,maintenanceCursor=0,maintenanceDeclineCursor=0,maintenanceReturnCursor=0,maintenanceClosureCursor=0,roomReleaseCursor=0,assetFollowupCursor=0;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                long[] pending;
                try
                {
                    await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<IncidentAssetFollowupDelivery>().PendingAsync(assetFollowupCursor,ct);
                    foreach(var id in pending)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<IncidentAssetFollowupDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Asset follow-up notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    assetFollowupCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Asset follow-up notification discovery retained after {ErrorType}",error.GetType().Name);assetFollowupCursor=0;}

                try
                {
                    await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<IncidentRoomReleaseDelivery>().PendingAsync(roomReleaseCursor,ct);
                    foreach(var id in pending)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<IncidentRoomReleaseDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Room release notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    roomReleaseCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Room release notification discovery retained after {ErrorType}",error.GetType().Name);roomReleaseCursor=0;}

                try
                {
                    await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<MaintenanceClosureDelivery>().PendingAsync(maintenanceClosureCursor,ct);
                    foreach(var id in pending)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<MaintenanceClosureDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Maintenance closure notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    maintenanceClosureCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Maintenance closure notification discovery retained after {ErrorType}",error.GetType().Name);maintenanceClosureCursor=0;}

                try
                {
                    await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<MaintenanceDeclineDelivery>().PendingAsync(maintenanceDeclineCursor,ct);
                    foreach(var id in pending)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<MaintenanceDeclineDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Maintenance decline notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    maintenanceDeclineCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Maintenance decline notification discovery retained after {ErrorType}",error.GetType().Name);maintenanceDeclineCursor=0;}

                try
                {
                    await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<MaintenanceReturnDelivery>().PendingAsync(maintenanceReturnCursor,ct);
                    foreach(var id in pending)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<MaintenanceReturnDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Maintenance return notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    maintenanceReturnCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Maintenance return notification discovery retained after {ErrorType}",error.GetType().Name);maintenanceReturnCursor=0;}

                try
                {
                    await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<MaintenanceSubmissionDelivery>().PendingAsync(maintenanceCursor,ct);
                    foreach(var id in pending)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<MaintenanceSubmissionDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Maintenance notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    maintenanceCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Maintenance notification discovery retained after {ErrorType}",error.GetType().Name);maintenanceCursor=0;}

                try
                {
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<UnscheduledWorkdayDelivery>().PendingAsync(workdayCursor,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<UnscheduledWorkdayDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Workday notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                workdayCursor=pending.Length==100?pending[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Workday notification discovery retained after {ErrorType}",error.GetType().Name);workdayCursor=0;}
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
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskDeclineNotificationDelivery>().ReadPendingAsync(declineCursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskDeclineNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task decline notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                declineCursor=pending.Length==100?pending[^1]:0;
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskAssignmentNotificationDelivery>().ReadPendingAsync(assignmentCursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskAssignmentNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task assignment notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                assignmentCursor=pending.Length==100?pending[^1]:0;
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskPlanNotificationDelivery>().ReadPendingAsync(planCursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskPlanNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task plan notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                planCursor=pending.Length==100?pending[^1]:0;
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskSeriesNotificationDelivery>().ReadPendingAsync(seriesCursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskSeriesNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task series notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                seriesCursor=pending.Length==100?pending[^1]:0;
                await using(var scope=scopes.CreateAsyncScope())pending=await scope.ServiceProvider.GetRequiredService<TaskReopenNotificationDelivery>().ReadPendingAsync(reopenCursor,100,ct);
                foreach(var id in pending)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    try{await scope.ServiceProvider.GetRequiredService<TaskReopenNotificationDelivery>().DeliverAsync(id,ct);}
                    catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                    catch(Exception error){logger.LogWarning("Task reopen notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                }
                reopenCursor=pending.Length==100?pending[^1]:0;
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception error){logger.LogWarning("Task notification discovery retained after {ErrorType}",error.GetType().Name);cursor=0;verificationCursor=0;declineCursor=0;assignmentCursor=0;planCursor=0;seriesCursor=0;reopenCursor=0;}
            try{await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds),clock,ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
        }
    }
}
