using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class TaskSeriesOptions
{
    public bool Enabled { get; set; }
    public int RetrySeconds { get; set; } = 300;
}

internal sealed class TaskSeriesWorker(IServiceScopeFactory scopes, IOptions<TaskSeriesOptions> options,
    TimeProvider clock, ILogger<TaskSeriesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        DateOnly? lastRun = null;
        DateTimeOffset? retryAt = null;
        while (!ct.IsCancellationRequested)
        {
            var local = TaskRecurrenceSchedule.Local(clock.GetUtcNow().UtcDateTime);
            var today = DateOnly.FromDateTime(local);
            if (local.TimeOfDay >= TimeSpan.FromMinutes(45) && lastRun != today
                && (retryAt == null || clock.GetUtcNow() >= retryAt))
            {
                try
                {
                    var result = await RunPassAsync(ct);
                    if (!result.Incomplete) { lastRun = today; retryAt = null; }
                    else retryAt = clock.GetUtcNow().AddSeconds(options.Value.RetrySeconds);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    retryAt = clock.GetUtcNow().AddSeconds(options.Value.RetrySeconds);
                    logger.LogWarning("JOB05 discovery retained after {ErrorType}", error.GetType().Name);
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(30), clock, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    internal async Task<int> RunAsync(CancellationToken ct)
        => (await RunPassAsync(ct)).Count;

    private async Task<(int Count, bool Incomplete)> RunPassAsync(CancellationToken ct)
    {
        long cursor = 0;
        var total = 0;
        var incomplete = false;
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
                    if (count == 100)
                    {
                        incomplete = true;
                        logger.LogWarning("JOB05 series {SeriesId} retains a possible backlog for retry", id);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    incomplete = true;
                    logger.LogWarning("JOB05 series {SeriesId} rolled back after {ErrorType}; retry retained", id, error.GetType().Name);
                }
            }
            if (pending.Length < 100) break;
            cursor = pending[^1];
        }
        logger.LogInformation("JOB05 created {TaskCount} recurring task occurrences", total);
        return (total, incomplete);
    }
}
