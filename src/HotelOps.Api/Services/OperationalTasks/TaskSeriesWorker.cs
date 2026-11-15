using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class TaskSeriesOptions
{
    public bool Enabled { get; set; }
}

internal sealed class TaskSeriesWorker(IServiceScopeFactory scopes, IOptions<TaskSeriesOptions> options,
    TimeProvider clock, ILogger<TaskSeriesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        DateOnly? lastRun = null;
        while (!ct.IsCancellationRequested)
        {
            var local = TaskRecurrenceSchedule.Local(clock.GetUtcNow().UtcDateTime);
            var today = DateOnly.FromDateTime(local);
            if (local.TimeOfDay >= TimeSpan.FromMinutes(45) && lastRun != today)
            {
                try
                {
                    await RunAsync(ct);
                    lastRun = today;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    // Discovery failures retry; individual series failures are
                    // isolated below and retried on the next daily job run.
                    logger.LogWarning("JOB05 discovery retained after {ErrorType}", error.GetType().Name);
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(30), clock, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    internal async Task<int> RunAsync(CancellationToken ct)
    {
        long cursor = 0;
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            long[] pending;
            await using (var scope = scopes.CreateAsyncScope())
                pending = await scope.ServiceProvider.GetRequiredService<TaskSeriesGeneration>().PendingAsync(cursor, 100, ct);
            foreach (var id in pending)
            {
                try
                {
                    // Each service scope owns one independent serializable
                    // transaction. A failing series cannot roll back its peers.
                    await using var scope = scopes.CreateAsyncScope();
                    var count = await scope.ServiceProvider.GetRequiredService<TaskSeriesGeneration>().GenerateAsync(id, ct);
                    total += count;
                    if (count == 100) logger.LogWarning("JOB05 series {SeriesId} retains a possible backlog for the next run", id);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception error) { logger.LogWarning("JOB05 series {SeriesId} rolled back after {ErrorType}", id, error.GetType().Name); }
            }
            if (pending.Length < 100) break;
            cursor = pending[^1];
        }
        logger.LogInformation("JOB05 created {TaskCount} recurring task occurrences", total);
        return total;
    }
}
