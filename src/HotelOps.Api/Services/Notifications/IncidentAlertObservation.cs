using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed class IncidentAlertObservation(HotelOpsDbContext db)
{
    internal async Task<WorkAlertObservation> ObserveAsync(long id,decimal threshold,CancellationToken ct,string metric="RESOLUTION")
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Independent alert context required.");
        if(!await WorkAlertEpisodeStore.InstalledAsync(db,ct))throw new InvalidOperationException("Alert storage required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var row=await IncidentAlertSource.ReadAsync(db,id,ct);
        if(row==null)return new(null,WorkAlertCondition.Unknown);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(metric is not ("RESOLUTION" or "RESPONSE"))throw new ArgumentException("Invalid incident SLA metric.");
        var due=metric=="RESPONSE"?row.ResponseDueAt:row.DueAt;
        var condition=WorkAlertWindow.Evaluate(row.State is "CLOSED" or "CANCELLED"||metric=="RESPONSE"&&row.ResponseAccepted,due,row.AllowedFrom,now,threshold);
        var validStart=due>row.AllowedFrom?(DateTime?)row.AllowedFrom:null;
        var episode=await WorkAlertEpisodeStore.ObserveAsync(db,"INCIDENT",id,row.BranchId,condition,due,validStart,now,ct,row.CycleId,metric);
        await tx.CommitAsync(ct);return new(episode,condition);
    }
}
