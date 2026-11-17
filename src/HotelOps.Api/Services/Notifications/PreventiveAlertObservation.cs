using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class PreventiveAlertObservation(HotelOpsDbContext db)
{
    internal async Task<WorkAlertObservation> ObserveAsync(long id,decimal threshold,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Independent alert context required.");
        if(!await WorkAlertEpisodeStore.InstalledAsync(db,ct)||!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct))
            throw new InvalidOperationException("Preventive alert storage required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var row=await PreventiveAlertSource.Query(db).SingleOrDefaultAsync(r=>r.Id==id,ct);
        if(row==null)return new(null,WorkAlertCondition.Unknown);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var source=new WorkAlertSourceRow{SourceType="MAINTENANCE_OCCURRENCE",SourceId=id,BranchId=row.BranchId};
        var condition=await PreventiveAlertSource.ConditionAsync(db,source,now,threshold,ct);
        var episode=await WorkAlertEpisodeStore.ObserveAsync(db,source.SourceType,id,row.BranchId,condition,row.DueAt,null,now,ct);
        await tx.CommitAsync(ct);return new(episode,condition);
    }
}
