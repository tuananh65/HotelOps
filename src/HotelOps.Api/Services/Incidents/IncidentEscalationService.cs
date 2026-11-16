using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentEscalationInput(string Reason, string RequestedSupport);
public sealed record IncidentEscalationSaved(long IncidentId, long EventId, string Status);
public sealed class IncidentEscalationItem
{
    public long EscalationId { get; set; }
    public string State { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RequestedSupport { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
public sealed record IncidentEscalationContext(string? ETag, bool CanCreate, string? Severity,
    DateTime? ResponseDeadlineAt, DateTime? ResolutionDeadlineAt, DateTime ServerNow, IncidentEscalationTiming Timing, IncidentEscalationItem[] Items);

public sealed class IncidentEscalationService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor) || actor.UserId!=ticket.UserId || actor.SessionId!=sid || actor.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid || valid.Session?.SessionId!=sid || valid.Session.IsRestricted) throw new InternalRequestReadException(401,"SESSION_INVALID");
    }
    private static Task<DateTime> NowAsync(HotelOpsDbContext db,CancellationToken ct)
        => db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
    private async Task RequireReadAsync(long user,Incident incident,CancellationToken ct)
    {
        var scope=await IncidentReadAccess.ReadAsync(db,user,incident.BranchId,ct,sourceRecord:true);
        if(!await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i=>i.IncidentId==incident.IncidentId,ct)) throw new InternalRequestReadException(404,"INCIDENT_UNAVAILABLE");
    }
    private Task<string?> SeverityAsync(Incident incident,CancellationToken ct)
        => db.ReferenceValues.AsNoTracking().Where(r=>r.ReferenceId==incident.SeverityId && r.GroupCode=="SEVERITY")
            .Select(r=>(string?)r.ValueCode).SingleOrDefaultAsync(ct);
    public async Task<IncidentEscalationContext> ReadAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await SessionAsync(ticket,sid,ct);
        var incident=await db.Incidents.AsNoTracking().SingleOrDefaultAsync(i=>i.IncidentId==id,ct) ?? throw new InternalRequestReadException(404,"INCIDENT_UNAVAILABLE");
        await RequireReadAsync(ticket.UserId,incident,ct);
        if(!await IncidentEscalationClosure.InstalledAsync(db,ct)) throw new InternalRequestReadException(503,"ESCALATION_STORAGE_UNAVAILABLE");
        var items=await db.Database.SqlQuery<IncidentEscalationItem>($"""
            SELECT escalation_id AS EscalationId,state AS State,reason AS Reason,requested_support AS RequestedSupport,
                opened_at AS OpenedAt,closed_at AS ClosedAt FROM dbo.incident_escalations WHERE incident_id={id}
            """).OrderByDescending(e=>e.EscalationId).ToArrayAsync(ct);
        foreach(var item in items) { item.OpenedAt=DateTime.SpecifyKind(item.OpenedAt,DateTimeKind.Utc); if(item.ClosedAt is {} closed) item.ClosedAt=DateTime.SpecifyKind(closed,DateTimeKind.Utc); }
        var now=DateTime.SpecifyKind(await NowAsync(db,ct),DateTimeKind.Utc);
        var sla=await IncidentSlaRead.ReadAsync(db,id,ct);
        var severity=await SeverityAsync(incident,ct);
        var cycle=await IncidentReopenStore.ReadAsync(db,id,ct);
        var cycleDiscovery=cycle?.DiscoveredAt ?? incident.DiscoveredAt;
        var timing=IncidentEscalationEligibility.Calculate(incident.Status,severity,cycleDiscovery,sla.ResolutionDeadlineAt,now);
        var tag=await IncidentTriageStore.TagAsync(db,id,ct);
        var can=tag!=null && timing.Eligible && !items.Any(e=>e.State=="OPEN")
            && await NotificationAcknowledgmentStore.InstalledAsync(db,ct)
            && await IncidentEscalationAccess.AllowsAsync(db,ticket.UserId,incident.BranchId,id,ct);
        await SessionAsync(ticket,sid,ct);
        await RequireReadAsync(ticket.UserId,incident,ct);
        await tx.CommitAsync(ct);
        return new(tag,can,severity,sla.ResponseDeadlineAt,sla.ResolutionDeadlineAt,now,timing,items);
    }
    public async Task<IncidentEscalationSaved> CreateAsync(SessionReference ticket,long sid,long id,string tag,IncidentEscalationInput input,CancellationToken ct)
    {
        var reason=input.Reason?.Trim(); var support=input.RequestedSupport?.Trim();
        if(reason==null || reason.Length<10 || reason.Length>4000 || string.IsNullOrWhiteSpace(support) || support.Length>4000)
            throw new ValidationException("Reason must contain 10–4000 characters; requested support is required, up to 4000 characters.");
        if(db.Database.CurrentTransaction!=null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean escalation context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await SessionAsync(ticket,sid,ct);
        var incident=await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .AsTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404,"INCIDENT_UNAVAILABLE");
        if(!await IncidentEscalationAccess.AllowsAsync(db,ticket.UserId,incident.BranchId,id,ct)) throw new InternalRequestReadException(403,"SCOPED_INCIDENT_ESCALATE_REQUIRED");
        if(!await IncidentEscalationClosure.InstalledAsync(db,ct) || !await NotificationAcknowledgmentStore.InstalledAsync(db,ct))
            throw new InternalRequestReadException(503,"ESCALATION_STORAGE_UNAVAILABLE");
        var version=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"INCIDENT_VERSION_UNAVAILABLE");
        if(version!=tag) throw new InternalRequestReadException(412,"INCIDENT_CHANGED");
        var now=DateTime.SpecifyKind(await NowAsync(db,ct),DateTimeKind.Utc);
        var sla=await IncidentSlaRead.ReadAsync(db,id,ct);
        var severity=await SeverityAsync(incident,ct);
        var cycle=await IncidentReopenStore.ReadAsync(db,id,ct);
        var cycleDiscovery=cycle?.DiscoveredAt ?? incident.DiscoveredAt;
        var timing=IncidentEscalationEligibility.Calculate(incident.Status,severity,cycleDiscovery,sla.ResolutionDeadlineAt,now);
        if(!timing.Eligible) throw new InternalRequestReadException(409,"CRITICAL_AT_RISK_OR_OVERDUE_REQUIRED");
        if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.incident_escalations WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id} AND state='OPEN'").SingleAsync(ct)!=0)
            throw new InternalRequestReadException(409,"OPEN_ESCALATION_EXISTS");
        var ev=new IncidentEvent {IncidentId=id,EventType="ESCALATED",FromStatus=incident.Status,ToStatus=incident.Status,ActorId=ticket.UserId,OccurredAt=now,Note=reason};
        db.IncidentEvents.Add(ev);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.incident_escalations(incident_id,opened_event_id,state,opened_at,reason,requested_support)
            VALUES({id},{ev.EventId},'OPEN',{now},{reason},{support});
            UPDATE dbo.incidents SET status=status WHERE incident_id={id};
            """,ct);
        db.AuditLogs.Add(new AuditLog {EntityType="incidents",EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",BranchId=incident.BranchId,
            ActorUserId=ticket.UserId,OccurredAt=now,OldValues=JsonSerializer.Serialize(new{status=incident.Status}),
            NewValues=JsonSerializer.Serialize(new{status=incident.Status,event_id=ev.EventId,reason,requested_support=support,
                severity,cycle_discovered_at=cycleDiscovery,reopen_event_id=cycle?.EventId,
                resolution_deadline_at=sla.ResolutionDeadlineAt,eligibility=timing.Classification,consumed_version=tag,notification_kind="INCIDENT_ESCALATED",notification_version=1})});
        await db.SaveChangesAsync(ct);
        await SessionAsync(ticket,sid,ct);
        if(!await IncidentEscalationAccess.AllowsAsync(db,ticket.UserId,incident.BranchId,id,ct)) throw new InternalRequestReadException(403,"SCOPED_INCIDENT_ESCALATE_REQUIRED");
        await tx.CommitAsync(ct);
        return new(id,ev.EventId,"OPEN");
    }
}
