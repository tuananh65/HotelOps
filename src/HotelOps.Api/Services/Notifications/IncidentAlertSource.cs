using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed record IncidentAlertWindow(long BranchId,long CycleId,DateTime AllowedFrom,DateTime? DueAt,string State,
    DateTime? ResponseDueAt,bool ResponseAccepted);

internal static class IncidentAlertSource
{
    internal static async Task<IncidentAlertWindow?> ReadAsync(HotelOpsDbContext db,long id,CancellationToken ct)
    {
        if(!await IncidentListSla.InstalledAsync(db,ct))return null;
        var incident=await db.Incidents.AsNoTracking().SingleOrDefaultAsync(i=>i.IncidentId==id,ct);
        if(incident==null)return null;
        var cycle=await IncidentReopenStore.ReadAsync(db,id,ct);
        var sla=await IncidentListSla.Query(db).SingleOrDefaultAsync(i=>i.IncidentId==id,ct);
        if(sla==null)return null;
        // UC054 combines acceptance and start. Bind its persisted start to the
        // current work and committed OPEN -> IN_PROGRESS transition, not reassignment.
        var works=await db.MaintenanceWorks.AsNoTracking().Where(w=>w.IncidentId==id&&!w.PreventiveMaintenanceRuns.Any()
            &&!db.IncidentEvents.Any(e=>e.IncidentId==id&&e.EventType=="REOPENED"&&e.OccurredAt>w.CreatedAt))
            .Select(w=>new{w.CreatedAt,w.ActualStartAt}).ToArrayAsync(ct);
        if(works.Length>1)throw new InvalidOperationException("Ambiguous current incident work.");
        var accepted=false;
        if(works.SingleOrDefault() is {} work&&work.ActualStartAt is {} started)
        {
            accepted=await db.IncidentEvents.AsNoTracking().AnyAsync(e=>e.IncidentId==id&&e.EventType=="STATUS_CHANGED"
                &&e.FromStatus=="OPEN"&&e.ToStatus=="IN_PROGRESS"&&e.OccurredAt==started,ct);
            if(!accepted||started<work.CreatedAt)throw new InvalidOperationException("Incident response evidence inconsistent.");
        }
        return new(incident.BranchId,cycle?.EventId??0,cycle?.DiscoveredAt??incident.DiscoveredAt,
            sla.ResolutionDeadlineAt,incident.Status,sla.ResponseDeadlineAt,accepted);
    }

    internal static async Task<WorkAlertCondition> ConditionAsync(HotelOpsDbContext db,WorkAlertSourceRow episode,DateTime now,decimal threshold,CancellationToken ct)
    {
        var row=await ReadAsync(db,episode.SourceId,ct);
        return row==null||row.BranchId!=episode.BranchId||row.CycleId!=episode.CycleId?WorkAlertCondition.Unknown
            :WorkAlertWindow.Evaluate(row.State is "CLOSED" or "CANCELLED"||episode.Metric=="RESPONSE"&&row.ResponseAccepted,
                episode.Metric=="RESPONSE"?row.ResponseDueAt:row.DueAt,row.AllowedFrom,now,threshold);
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db,long user,WorkAlertSourceRow episode,CancellationToken ct)
    {
        var scope=await IncidentReadAccess.ReadAsync(db,user,episode.BranchId,ct,sourceRecord:true);
        var readable=scope.Apply(db.Incidents.AsNoTracking()).Where(i=>i.IncidentId==episode.SourceId);
        if(!await readable.AnyAsync(ct))return false;
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user&&r.BranchId==episode.BranchId&&r.IsActive&&r.Role.IsActive
            &&r.Role.RoleCode=="MAINTENANCE_LEAD"&&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now),ct))return true;
        if(await IncidentEscalationClosure.InstalledAsync(db,ct)
            &&await IncidentEscalationHandlingAccess.AllowsAsync(db,user,episode.BranchId,ct)
            &&await db.Database.SqlQuery<int>($"""
                SELECT 1 AS Value FROM dbo.incident_escalations x
                WHERE x.incident_id={episode.SourceId} AND x.state='OPEN' AND x.opened_at<={now}
                """).AnyAsync(ct))return true;
        return await IncidentCurrentAssignment.AssignedTo(readable,user,true).AnyAsync(ct);
    }
}
