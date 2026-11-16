using System.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed record PreventiveLinkedIncident(long IncidentId,string Number,string Title,string Status,string CheckKey,DateTime LinkedAt);
public sealed record PreventiveOccurrenceHistory(IReadOnlyList<PreventiveOccurrenceView> PreviousOccurrences,
    IReadOnlyList<PreventiveLinkedIncident> Incidents);

public sealed partial class PreventiveOccurrenceReadService
{
    private sealed class IncidentLink
    {
        public long IncidentId { get; set; }
        public string CheckKey { get; set; } = "";
        public DateTime LinkedAt { get; set; }
    }
    public async Task<PreventiveOccurrenceHistory> HistoryAsync(SessionReference ticket,long sid,long branch,long id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        var source=(await ReadAsync(ticket,sid,branch,id,0,ct)).Single();
        var scope=await AccessAsync(ticket,sid,branch,ct);
        // Prior occurrences retain independent OWN scope. A current assignment is not authority over earlier technicians' work.
        var previousIds=await db.Database.SqlQuery<long>($"""
            SELECT TOP(10) r.run_id AS Value FROM dbo.preventive_maintenance_runs r
            JOIN dbo.preventive_occurrence_details d ON d.run_id=r.run_id
            WHERE r.schedule_id={source.ScheduleId} AND r.planned_at<{source.DueAt} AND d.branch_id={branch}
              AND ({scope.Lead}=1 OR ({scope.Staff}=1 AND d.assignee_id={ticket.UserId}))
            ORDER BY r.planned_at DESC,r.run_id DESC
            """).ToArrayAsync(ct);
        var previous=new List<PreventiveOccurrenceView>();
        foreach(var prior in previousIds)previous.Add((await ReadAsync(ticket,sid,branch,prior,0,ct)).Single());
        var incidents=new List<PreventiveLinkedIncident>();
        if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.preventive_occurrence_incidents','U') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync(ct)==1)
        {
            var incidentScope=await IncidentReadAccess.ReadAsync(db,ticket.UserId,branch,ct,sourceRecord:true);
            var links=db.Database.SqlQuery<IncidentLink>($"""
                SELECT l.incident_id AS IncidentId,l.check_key AS CheckKey,l.linked_at AS LinkedAt
                FROM dbo.preventive_occurrence_incidents l
                JOIN dbo.audit_logs a ON a.audit_id=l.audit_id
                WHERE l.run_id={id} AND a.entity_type='preventive_maintenance_runs'
                  AND a.entity_id=CONVERT(varchar(30),l.run_id) AND a.actor_user_id=l.actor_id
                  AND a.branch_id={branch} AND a.occurred_at=l.linked_at
                """);
            var visible=await (from link in links
                join incident in incidentScope.Apply(db.Incidents.AsNoTracking()) on link.IncidentId equals incident.IncidentId
                orderby link.LinkedAt descending,link.IncidentId descending
                select new {incident.IncidentId,incident.IncidentNo,incident.IncidentTitle,incident.Status,link.CheckKey,link.LinkedAt})
                .Take(50).ToArrayAsync(ct);
            incidents.AddRange(visible.Select(i=>new PreventiveLinkedIncident(i.IncidentId,i.IncidentNo,i.IncidentTitle,i.Status,i.CheckKey,
                DateTime.SpecifyKind(i.LinkedAt,DateTimeKind.Utc))));
        }
        var finalScope=await AccessAsync(ticket,sid,branch,ct);
        if(previous.Any(p=>!finalScope.Lead&&(!finalScope.Staff||p.AssigneeId!=ticket.UserId)))
            throw new InternalRequestReadException(403,"MAINTENANCE_VIEW_REQUIRED");
        if(incidents.Count>0)
        {
            var finalIncidents=await IncidentReadAccess.ReadAsync(db,ticket.UserId,branch,ct,sourceRecord:true);
            var ids=incidents.Select(i=>i.IncidentId).ToArray();
            if(await finalIncidents.Apply(db.Incidents.AsNoTracking()).CountAsync(i=>ids.Contains(i.IncidentId),ct)!=ids.Length)
                throw new InternalRequestReadException(403,"INCIDENT_VIEW_CHANGED");
        }
        await tx.CommitAsync(ct);return new(previous,incidents);
    }
}
