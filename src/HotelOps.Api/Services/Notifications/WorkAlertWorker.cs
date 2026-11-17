using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Notifications;

internal sealed class WorkAlertOptions { public bool Enabled { get; set; } }

internal sealed class WorkAlertWorker(IServiceScopeFactory scopes, IOptions<WorkAlertOptions> options,
    ILogger<WorkAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            var minutes = 5;
            try
            {
                WorkAlertParameters parameters;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
                    parameters = await WorkAlertParameters.ReadAsync(db, ct);
                    if (!await WorkAlertEpisodeStore.InstalledAsync(db, ct))
                        throw new InvalidOperationException("JOB01 episode storage required.");
                }
                minutes = parameters.ScanMinutes;
                var delivered = 0;
                foreach(var sourceKind in new[]{"INTERNAL_REQUEST","TASK","CLEANING","REWORK","MAINTENANCE_OCCURRENCE","INCIDENT"})
                {
                try
                {
                long cursor = 0;
                while (!ct.IsCancellationRequested)
                {
                    long[] ids;
                    await using (var scope = scopes.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
                        // Include terminal rows with open episodes so closure is
                        // observed. Keyset pagination cannot skip newly inserted IDs.
                        var openEpisodes = WorkAlertEpisodeStore.SourcesQuery(db).Where(e => e.SourceType == sourceKind && e.ClosedAt == null);
                        ids = sourceKind=="INCIDENT"
                            ? await db.Incidents.AsNoTracking().Where(i=>i.IncidentId>cursor
                                &&(i.Status!="CLOSED"&&i.Status!="CANCELLED"||openEpisodes.Any(e=>e.SourceId==i.IncidentId)))
                                .OrderBy(i=>i.IncidentId).Select(i=>i.IncidentId).Take(100).ToArrayAsync(ct)
                            : sourceKind=="MAINTENANCE_OCCURRENCE"
                            ? await PreventiveAlertSource.Query(db).Where(r=>r.Id>cursor
                                &&(r.State!="COMPLETED"&&r.State!="CANCELLED"||openEpisodes.Any(e=>e.SourceId==r.Id)))
                                .OrderBy(r=>r.Id).Select(r=>r.Id).Take(100).ToArrayAsync(ct)
                            : sourceKind is "CLEANING" or "REWORK"
                            ? await CleaningAlertSource.Query(db,sourceKind=="REWORK").Where(r=>r.Id>cursor
                                &&(r.State!="COMPLETED"&&r.State!="CANCELLED"||openEpisodes.Any(e=>e.SourceId==r.Id)))
                                .OrderBy(r=>r.Id).Select(r=>r.Id).Take(100).ToArrayAsync(ct)
                            : sourceKind=="TASK"
                            ? await db.OperationalTasks.AsNoTracking().Where(t=>t.TaskId>cursor
                                &&(t.Status!="COMPLETED"&&t.Status!="CANCELLED"||openEpisodes.Any(e=>e.SourceId==t.TaskId)))
                                .OrderBy(t=>t.TaskId).Select(t=>t.TaskId).Take(100).ToArrayAsync(ct)
                            : await db.InternalRequests.AsNoTracking().Where(r => r.InternalRequestId > cursor
                            && (r.Status != "DRAFT" && r.Status != "CLOSED" && r.Status != "REJECTED" && r.Status != "CANCELLED"
                                || openEpisodes.Any(e => e.SourceId == r.InternalRequestId)))
                            .OrderBy(r => r.InternalRequestId).Select(r => r.InternalRequestId).Take(100).ToArrayAsync(ct);
                    }
                    foreach (var id in ids)
                    {
                        try
                        {
                            await using var scope = scopes.CreateAsyncScope();
                            var db = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
                            var episode = sourceKind=="INCIDENT"
                                ? await new IncidentAlertObservation(db).ObserveAsync(id,parameters.ThresholdPercent,ct)
                                : sourceKind=="MAINTENANCE_OCCURRENCE"
                                ? await new PreventiveAlertObservation(db).ObserveAsync(id,parameters.ThresholdPercent,ct)
                                : sourceKind is "CLEANING" or "REWORK"
                                ? await new CleaningAlertObservation(db).ObserveAsync(sourceKind,id,parameters.ThresholdPercent,ct)
                                : sourceKind=="TASK"
                                ? await new TaskAlertObservation(db).ObserveAsync(id,parameters.ThresholdPercent,ct)
                                : await new InternalRequestAlertObservation(db).ObserveAsync(id, parameters.ThresholdPercent, ct);
                            RecordEvaluation(sourceKind,id,sourceKind=="INCIDENT"?"RESOLUTION":"DUE",episode.Condition);
                            if (episode.EpisodeId is { } value)
                                delivered += await new InternalRequestAlertDelivery(db,
                                    scope.ServiceProvider.GetRequiredService<IOptions<NotificationCenterOptions>>()).DeliverAsync(value, ct);
                            if(sourceKind=="INCIDENT")
                            {
                                var response=await new IncidentAlertObservation(db).ObserveAsync(id,parameters.ThresholdPercent,ct,"RESPONSE");
                                RecordEvaluation(sourceKind,id,"RESPONSE",response.Condition);
                                if(response.EpisodeId is {} responseId)
                                    delivered+=await new InternalRequestAlertDelivery(db,
                                        scope.ServiceProvider.GetRequiredService<IOptions<NotificationCenterOptions>>()).DeliverAsync(responseId,ct);
                            }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception error) { logger.LogWarning("JOB01 {SourceType} {SourceId} retained after {ErrorType}",sourceKind,id,error.GetType().Name); }
                    }
                    if (ids.Length < 100) break;
                    cursor = ids[^1];
                }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception error) { logger.LogWarning("JOB01 {SourceType} discovery unavailable after {ErrorType}; other sources continue",sourceKind,error.GetType().Name); }
                }
                logger.LogInformation("JOB01 scan created {AlertCount} recipient alerts", delivered);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning("JOB01 scan retained after {ErrorType}", error.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMinutes(minutes), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }

    private void RecordEvaluation(string source,long id,string metric,WorkAlertCondition condition)
    {
        if(condition==WorkAlertCondition.NotEvaluable)
            logger.LogInformation("JOB01 {SourceType} {SourceId} {Metric} AT_RISK=NOT_EVALUABLE: required source timing unavailable; overdue evaluated independently",source,id,metric);
        else if(condition==WorkAlertCondition.Unknown)
            logger.LogWarning("JOB01 {SourceType} {SourceId} {Metric} source evidence unavailable; no alert emitted",source,id,metric);
    }
}
