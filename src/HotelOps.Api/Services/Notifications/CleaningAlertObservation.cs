using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class CleaningAlertObservation(HotelOpsDbContext db)
{
    internal async Task<WorkAlertObservation> ObserveAsync(string kind,long id,decimal threshold,CancellationToken ct)
    {
        if(kind is not ("CLEANING" or "REWORK"))throw new ArgumentException("Invalid cleaning alert source.");
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Clean independent alert observation required.");
        if(!await WorkAlertEpisodeStore.InstalledAsync(db,ct))throw new InvalidOperationException("Alert storage required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var row=await CleaningAlertSource.Query(db,kind=="REWORK").SingleOrDefaultAsync(r=>r.Id==id,ct);
        if(row==null)return new(null,WorkAlertCondition.Unknown);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var condition=WorkAlertWindow.Evaluate(row.State is "COMPLETED" or "CANCELLED",row.DueAt,row.AllowedFrom,now,threshold);
        var validStart=row.AllowedFrom is {} start&&row.DueAt>start?row.AllowedFrom:null;
        var episode=await WorkAlertEpisodeStore.ObserveAsync(db,kind,id,row.BranchId,condition,row.DueAt,validStart,now,ct);
        await tx.CommitAsync(ct);return new(episode,condition);
    }
}
