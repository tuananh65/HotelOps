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
    private async Task DeliverRecipientsAsync<T>(long eventId,
        Func<T,long,CancellationToken,Task<long[]>> pending,
        Func<T,long,long,CancellationToken,Task<string>> deliver,CancellationToken ct) where T : notnull
    {
        long[] recipients;
        await using (var scope = scopes.CreateAsyncScope())
            recipients = await pending(scope.ServiceProvider.GetRequiredService<T>(),eventId,ct);
        foreach (var recipient in recipients)
        {
            ct.ThrowIfCancellationRequested();
            // Each recipient owns its context and transaction; one corrupt receipt or
            // transient failure must not prevent another recipient's notification.
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await deliver(scope.ServiceProvider.GetRequiredService<T>(),eventId,recipient,ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                logger.LogWarning("Internal request {Delivery} event {EventId} recipient {RecipientId} retained after {ErrorType}",
                    typeof(T).Name,eventId,recipient,error.GetType().Name);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Value.Enabled)return;
        long cursor=0;
        long reopenCursor=0;
        long assignmentCursor=0;
        long routeCursor=0;
        long progressCursor=0;
        long completionCursor=0;
        long handoverCursor=0;
        long withdrawalCursor=0;
        long intakeCursor=0;
        long escalationCursor=0;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                try
                {
                    long[] reopens;
                    await using(var scope=scopes.CreateAsyncScope())reopens=await scope.ServiceProvider.GetRequiredService<InternalRequestReopenDelivery>().PendingAsync(reopenCursor,ct);
                    foreach(var id in reopens)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<InternalRequestReopenDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Reopen notification {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    reopenCursor=reopens.Length==100?reopens[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Reopen notification discovery retained after {ErrorType}",error.GetType().Name);reopenCursor=0;}
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
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request decision discovery retained after {ErrorType}",error.GetType().Name);cursor=0;}
                try
                {
                    long[] assignments;
                    await using(var scope=scopes.CreateAsyncScope())
                        assignments=await scope.ServiceProvider.GetRequiredService<InternalRequestAssignmentDelivery>().PendingAsync(assignmentCursor,ct);
                    foreach(var id in assignments)
                    {
                        try{await DeliverRecipientsAsync<InternalRequestAssignmentDelivery>(id,
                            (delivery,eventId,token)=>delivery.PendingRecipientsAsync(eventId,token),
                            (delivery,eventId,recipient,token)=>delivery.DeliverAsync(eventId,recipient,token),ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request assignment notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    assignmentCursor=assignments.Length==100?assignments[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request assignment discovery retained after {ErrorType}",error.GetType().Name);assignmentCursor=0;}
                try
                {
                    long[] routes;
                    await using(var scope=scopes.CreateAsyncScope())
                        routes=await scope.ServiceProvider.GetRequiredService<InternalRequestRouteDelivery>().PendingAsync(routeCursor,ct);
                    foreach(var id in routes)
                    {
                        try{await DeliverRecipientsAsync<InternalRequestRouteDelivery>(id,
                            (delivery,eventId,token)=>delivery.PendingRecipientsAsync(eventId,token),
                            (delivery,eventId,recipient,token)=>delivery.DeliverAsync(eventId,recipient,token),ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request route notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    routeCursor=routes.Length==100?routes[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request route discovery retained after {ErrorType}",error.GetType().Name);routeCursor=0;}
                try
                {
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
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request progress discovery retained after {ErrorType}",error.GetType().Name);progressCursor=0;}
                try
                {
                    long[] completion;
                    await using(var scope=scopes.CreateAsyncScope())
                        completion=await scope.ServiceProvider.GetRequiredService<InternalRequestCompletionDelivery>().PendingAsync(completionCursor,100,ct);
                    foreach(var id in completion)
                    {
                        await using var scope=scopes.CreateAsyncScope();
                        try{await scope.ServiceProvider.GetRequiredService<InternalRequestCompletionDelivery>().DeliverAsync(id,ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request completion notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    completionCursor=completion.Length==100?completion[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request completion discovery retained after {ErrorType}",error.GetType().Name);completionCursor=0;}
                try
                {
                    long[] handovers;
                    await using(var scope=scopes.CreateAsyncScope())
                        handovers=await scope.ServiceProvider.GetRequiredService<InternalRequestHandoverDelivery>().PendingAsync(handoverCursor,ct);
                    foreach(var id in handovers)
                    {
                        try{await DeliverRecipientsAsync<InternalRequestHandoverDelivery>(id,
                            (delivery,eventId,token)=>delivery.PendingRecipientsAsync(eventId,token),
                            (delivery,eventId,recipient,token)=>delivery.DeliverAsync(eventId,recipient,token),ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request handover notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    handoverCursor=handovers.Length==100?handovers[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request handover discovery retained after {ErrorType}",error.GetType().Name);handoverCursor=0;}
                try
                {
                    long[] withdrawals;
                    await using(var scope=scopes.CreateAsyncScope())
                        withdrawals=await scope.ServiceProvider.GetRequiredService<InternalRequestWithdrawalDelivery>().PendingAsync(withdrawalCursor,ct);
                    foreach(var id in withdrawals)
                    {
                        try{await DeliverRecipientsAsync<InternalRequestWithdrawalDelivery>(id,
                            (delivery,eventId,token)=>delivery.PendingRecipientsAsync(eventId,token),
                            (delivery,eventId,recipient,token)=>delivery.DeliverAsync(eventId,recipient,token),ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request withdrawal notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    withdrawalCursor=withdrawals.Length==100?withdrawals[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request withdrawal discovery retained after {ErrorType}",error.GetType().Name);withdrawalCursor=0;}
                try
                {
                    long[] intakes;
                    await using(var scope=scopes.CreateAsyncScope())
                        intakes=await scope.ServiceProvider.GetRequiredService<InternalRequestIntakeDelivery>().PendingAsync(intakeCursor,ct);
                    foreach(var id in intakes)
                    {
                        try{await DeliverRecipientsAsync<InternalRequestIntakeDelivery>(id,
                            (delivery,eventId,token)=>delivery.PendingRecipientsAsync(eventId,token),
                            (delivery,eventId,recipient,token)=>delivery.DeliverAsync(eventId,recipient,token),ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request intake notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    intakeCursor=intakes.Length==100?intakes[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request intake discovery retained after {ErrorType}",error.GetType().Name);intakeCursor=0;}
                try
                {
                    long[] escalations;
                    await using(var scope=scopes.CreateAsyncScope())
                        escalations=await scope.ServiceProvider.GetRequiredService<InternalRequestEscalationDelivery>().PendingAsync(escalationCursor,ct);
                    foreach(var id in escalations)
                    {
                        try{await DeliverRecipientsAsync<InternalRequestEscalationDelivery>(id,
                            (delivery,eventId,token)=>delivery.PendingRecipientsAsync(eventId,token),
                            (delivery,eventId,recipient,token)=>delivery.DeliverAsync(eventId,recipient,token),ct);}
                        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                        catch(Exception error){logger.LogWarning("Internal request escalation notice {EventId} retained after {ErrorType}",id,error.GetType().Name);}
                    }
                    escalationCursor=escalations.Length==100?escalations[^1]:0;
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){logger.LogWarning("Internal request escalation discovery retained after {ErrorType}",error.GetType().Name);escalationCursor=0;}
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception error){logger.LogWarning("Internal request notification scan retained after {ErrorType}",error.GetType().Name);}
            try{await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds),clock,ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
        }
    }
}
