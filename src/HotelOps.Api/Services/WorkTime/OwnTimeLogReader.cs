using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.WorkTime;

public sealed record OwnTimeLogEntry(long IntervalId, string Kind, long WorkItemId, DateTimeOffset Start,
    DateTimeOffset? End, long ClosedSeconds, long ProvisionalSeconds, string Version, string? WorkItemName = null);
public sealed record OwnTimeLog(long BranchId, DateOnly Date, DateTimeOffset AsOf, long WorkdayClosedSeconds,
    long WorkdayProvisionalSeconds, long WorkClosedSeconds, long WorkProvisionalSeconds,
    int Total, int Page, int PageSize, OwnTimeLogEntry[] Items);

// Presentation inside the existing authenticated own-workday transaction, not a
// permission boundary. Work item IDs are own time provenance, not target read grants.
internal static class OwnTimeLogReader
{
    internal static async Task<OwnTimeLog> ReadAsync(HotelOpsDbContext db,long actor,long branch,
        DateOnly date,int page,int pageSize,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Own time log requires a serializable transaction.");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var period=WorkTimePeriod.Create(date,date,now);
        var days=await db.Workdays.AsNoTracking().Where(d=>d.UserId==actor&&d.BranchId==branch
            &&d.StartedAt<period.End&&(d.EndedAt==null||d.EndedAt>period.Start)).ToArrayAsync(ct);
        var intervals=await db.WorkIntervals.AsNoTracking().Where(i=>i.UserId==actor&&i.BranchId==branch
            &&i.EffectiveStartedAt<period.End&&(i.EffectiveEndedAt==null||i.EffectiveEndedAt>period.Start))
            .OrderBy(i=>i.EffectiveStartedAt).ThenBy(i=>i.IntervalId).ToArrayAsync(ct);
        var dayIds=intervals.Select(i=>i.WorkdayId).Distinct().ToArray();
        var owners=await db.Workdays.AsNoTracking().Where(d=>dayIds.Contains(d.WorkdayId)&&d.UserId==actor&&d.BranchId==branch)
            .ToDictionaryAsync(d=>d.WorkdayId,ct);
        if(days.Any(d=>d.StartedAt>now||d.EndedAt<d.StartedAt||d.EndedAt>now)
            ||intervals.Any(i=>!owners.TryGetValue(i.WorkdayId,out var day)
                ||i.EffectiveStartedAt<day.StartedAt||day.EndedAt!=null&&(i.EffectiveEndedAt==null||i.EffectiveEndedAt>day.EndedAt)
                ||i.EffectiveStartedAt>now||i.EffectiveEndedAt<i.EffectiveStartedAt||i.EffectiveEndedAt>now
                ||(i.ClosedAt==null)!=(i.EffectiveEndedAt==null)))
            throw new WorkTimeContractException("TIME_STATE_CONFLICT");
        ValidateNonOverlap(intervals,now);
        var result = Project(branch,period,now,days,intervals,page,pageSize);
        var taskIds = result.Items.Where(i => i.Kind == "TASK").Select(i => i.WorkItemId).ToArray();
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => taskIds.Contains(t.TaskId) && t.BranchId == branch)
            .Select(t => new { t.TaskId, t.Title }).ToDictionaryAsync(t => t.TaskId, t => t.Title, ct);
        var cleaningIds = result.Items.Where(i => i.Kind == "CLEANING").Select(i => i.WorkItemId).ToArray();
        var cleanings = await db.CleaningTasks.AsNoTracking().Where(t => cleaningIds.Contains(t.CleaningTaskId) && t.Room.BranchId == branch)
            .Select(t => new { t.CleaningTaskId, Name = "Vệ sinh phòng " + t.Room.RoomNumber }).ToDictionaryAsync(t => t.CleaningTaskId, t => t.Name, ct);
        var reworkIds = result.Items.Where(i => i.Kind == "REWORK").Select(i => i.WorkItemId).ToArray();
        var reworks = await db.CleaningReworks.AsNoTracking().Where(t => reworkIds.Contains(t.ReworkId) && t.CleaningTask.Room.BranchId == branch)
            .Select(t => new { t.ReworkId, Name = "Làm lại phòng " + t.CleaningTask.Room.RoomNumber }).ToDictionaryAsync(t => t.ReworkId, t => t.Name, ct);
        return result with { Items = result.Items.Select(i => i with { WorkItemName = i.Kind switch {
            "TASK" => tasks.GetValueOrDefault(i.WorkItemId), "CLEANING" => cleanings.GetValueOrDefault(i.WorkItemId),
            "REWORK" => reworks.GetValueOrDefault(i.WorkItemId), _ => null } }).ToArray() };
    }

    internal static void ValidateNonOverlap(WorkInterval[] intervals,DateTime now)
    {
        try
        {
            // Zero-duration closed events consume no time and may lie inside
            // another interval. Removing them must not hide a real overlap.
            WorkIntervalMath.Summarize(intervals.Select(i=>new RecordedWorkRange(Utc(i.EffectiveStartedAt),
                i.EffectiveEndedAt is {} end?Utc(end):null)),Utc(now));
        }
        catch(ArgumentException){throw new WorkTimeContractException("TIME_STATE_CONFLICT");}
    }

    internal static OwnTimeLog Project(long branch,WorkTimePeriod period,DateTime now,Workday[] days,
        WorkInterval[] intervals,int page,int pageSize)
    {
        var closed=intervals.Where(i=>i.EffectiveEndedAt!=null).Sum(i=>period.Seconds(i.EffectiveStartedAt,i.EffectiveEndedAt,now));
        var provisional=intervals.Where(i=>i.EffectiveEndedAt==null).Sum(i=>period.Seconds(i.EffectiveStartedAt,null,now));
        var items=intervals.Skip((page-1)*pageSize).Take(pageSize).Select(i=>new OwnTimeLogEntry(i.IntervalId,i.WorkItemKind,i.WorkItemId,
            Utc(i.EffectiveStartedAt),i.EffectiveEndedAt is {} end?Utc(end):null,
            i.EffectiveEndedAt!=null?period.Seconds(i.EffectiveStartedAt,i.EffectiveEndedAt,now):0,
            i.EffectiveEndedAt==null?period.Seconds(i.EffectiveStartedAt,null,now):0,Convert.ToBase64String(i.RowVersion))).ToArray();
        return new(branch,period.From,Utc(now),days.Where(d=>d.EndedAt!=null).Sum(d=>period.Seconds(d.StartedAt,d.EndedAt,now)),
            days.Where(d=>d.EndedAt==null).Sum(d=>period.Seconds(d.StartedAt,null,now)),closed,provisional,intervals.Length,page,pageSize,items);
    }
    private static DateTimeOffset Utc(DateTime value)=>new(DateTime.SpecifyKind(value,DateTimeKind.Utc));
}
