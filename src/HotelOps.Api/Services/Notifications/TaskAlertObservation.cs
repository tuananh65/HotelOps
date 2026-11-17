using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class TaskAlertObservation(HotelOpsDbContext db)
{
    internal async Task<WorkAlertObservation> ObserveAsync(long taskId,decimal threshold,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Clean independent alert observation required.");
        if(!await WorkAlertEpisodeStore.InstalledAsync(db,ct))throw new InvalidOperationException("Alert storage required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var task=await db.OperationalTasks.AsNoTracking().SingleOrDefaultAsync(t=>t.TaskId==taskId,ct);
        if(task==null)return new(null,WorkAlertCondition.Unknown);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var condition=await TaskAlertSource.ConditionAsync(db,taskId,now,threshold,ct);
        var cycle=await WorkAlertCycle.ReadAsync(db,"TASK",taskId,now,ct);
        var validStart=task.PlannedStartAt is {} start&&task.DueAt>start?task.PlannedStartAt:null;
        var episode=await WorkAlertEpisodeStore.ObserveAsync(db,"TASK",taskId,task.BranchId,condition,task.DueAt,validStart,now,ct,cycle);
        await tx.CommitAsync(ct);return new(episode,condition);
    }
}
