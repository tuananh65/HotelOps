using HotelOps.Api.Services.OperationalTasks;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.PreventiveMaintenance;

internal sealed class PreventiveGenerationOptions
{
    public bool Enabled { get; set; }
    public int LeadDays { get; set; } = 3; // SYS-PAR-16
}

internal sealed class PreventiveGenerationWorker(IServiceScopeFactory scopes,IOptions<PreventiveGenerationOptions> options,
    TimeProvider clock,ILogger<PreventiveGenerationWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!options.Value.Enabled)return;
        DateOnly? completedDay=null;
        while(!ct.IsCancellationRequested)
        {
            var local=TaskRecurrenceSchedule.Local(clock.GetUtcNow().UtcDateTime);
            var today=DateOnly.FromDateTime(local);
            if(local.TimeOfDay>=TimeSpan.FromMinutes(30) && completedDay!=today)
            {
                try{await RunAsync(ct);completedDay=today;}
                catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
                catch(Exception e){logger.LogWarning("JOB04 discovery deferred after {ErrorType}",e.GetType().Name);}
            }
            try{await Task.Delay(TimeSpan.FromSeconds(30),clock,ct);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
        }
    }
    private async Task RunAsync(CancellationToken ct)
    {
        long cursor=0;var count=0;
        while(!ct.IsCancellationRequested)
        {
            long[] pending;
            await using(var scope=scopes.CreateAsyncScope())
                pending=await scope.ServiceProvider.GetRequiredService<PreventiveOccurrenceGeneration>().PendingAsync(cursor,ct);
            foreach(var id in pending)
            {
                try
                {
                    await using var scope=scopes.CreateAsyncScope();
                    var generated=await scope.ServiceProvider.GetRequiredService<PreventiveOccurrenceGeneration>().GenerateAsync(id,ct);
                    count+=generated;
                    if(generated==100)logger.LogWarning("JOB04 schedule {ScheduleId} retains a possible backlog",id);
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
                catch(Exception e){logger.LogWarning("JOB04 schedule {ScheduleId} rolled back after {ErrorType}",id,e.GetType().Name);}
            }
            if(pending.Length<100)break;
            cursor=pending[^1];
        }
        logger.LogInformation("JOB04 created {OccurrenceCount} preventive maintenance occurrences",count);
    }
}
