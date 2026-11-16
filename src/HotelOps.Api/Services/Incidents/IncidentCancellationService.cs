using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentCancellationInput(string Kind, string Reason, long? OriginalIncidentId);
public sealed record IncidentCancellationSaved(long IncidentId, long EventId, string Status);

public sealed partial class IncidentCancellationService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static IncidentCancellationInput Validate(long id, IncidentCancellationInput input)
    {
        var reason = input.Reason?.Trim();
        if (reason == null || reason.Length < 10) throw new ValidationException("Cancellation reason must contain at least ten characters.");
        if (input.Kind is not ("DUPLICATE" or "INVALID")) throw new ValidationException("Choose Duplicate or Invalid.");
        if (input.Kind == "DUPLICATE" ? input.OriginalIncidentId is not > 0 || input.OriginalIncidentId == id : input.OriginalIncidentId != null)
            throw new ValidationException("Duplicate requires a different original incident; Invalid must not specify one.");
        return input with { Reason = reason };
    }
    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid || actor.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var result = await sessions.ValidateSessionAsync(ticket,ct);
        if (!result.IsValid || result.Session?.SessionId != sid || result.Session.IsRestricted) throw new InternalRequestReadException(401,"SESSION_INVALID");
    }
    public async Task<IncidentCancellationSaved> CancelAsync(SessionReference ticket,long sid,long id,string tag,IncidentCancellationInput input,CancellationToken ct)
    {
        input=Validate(id,input);
        if(db.Database.CurrentTransaction!=null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean cancellation context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await SessionAsync(ticket,sid,ct);
        // Deterministic Incident locks also protect the original from concurrent cancellation/deletion.
        var ids=new[]{id,input.OriginalIncidentId ?? id}.Distinct().Order().ToArray();
        var rows=new List<Incident>();
        foreach(var sourceId in ids)
        {
            var row=await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={sourceId}")
                .AsTracking().SingleOrDefaultAsync(ct);
            if(row==null) throw new InternalRequestReadException(404,"INCIDENT_UNAVAILABLE");
            rows.Add(row);
        }
        var incident=rows.Single(i=>i.IncidentId==id);
        if(!await IncidentTriageAccess.AllowsAsync(db,ticket.UserId,incident.BranchId,id,ct)) throw new InternalRequestReadException(403,"SCOPED_TRIAGE_REQUIRED");
        var version=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"INCIDENT_VERSION_UNAVAILABLE");
        if(version!=tag) throw new InternalRequestReadException(412,"INCIDENT_CHANGED");
        if(await IncidentAssignmentWorkflow.ReadAsync(db,incident,ct)!="PENDING_TRIAGE") throw new InternalRequestReadException(409,"PENDING_TRIAGE_REQUIRED");
        if(input.OriginalIncidentId is {} originalId)
        {
            var original=rows.Single(i=>i.IncidentId==originalId);
            var scope=await IncidentReadAccess.ReadAsync(db,ticket.UserId,original.BranchId,ct,sourceRecord:true);
            if(!await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i=>i.IncidentId==originalId,ct)) throw new InternalRequestReadException(404,"ORIGINAL_INCIDENT_UNAVAILABLE");
        }
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        incident.Status="CANCELLED";
        incident.ClosureNote=input.Reason;
        var ev=new IncidentEvent { IncidentId=id,EventType="STATUS_CHANGED",FromStatus="OPEN",ToStatus="CANCELLED",ActorId=ticket.UserId,OccurredAt=now,Note=input.Reason };
        db.IncidentEvents.Add(ev);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog { EntityType="incidents",EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",BranchId=incident.BranchId,
            ActorUserId=ticket.UserId,OccurredAt=now,OldValues=JsonSerializer.Serialize(new {status="OPEN",workflow="PENDING_TRIAGE"}),
            NewValues=JsonSerializer.Serialize(new {status="CANCELLED",cancellation_kind=input.Kind,original_incident_id=input.OriginalIncidentId,
                reason=input.Reason,event_id=ev.EventId,consumed_version=tag,notification_kind="INCIDENT_CANCELLED",notification_version=1}) });
        await ReleaseBlocksAsync(incident,ticket.UserId,input.Reason,now,ct);
        await db.SaveChangesAsync(ct);
        await SessionAsync(ticket,sid,ct);
        if(!await IncidentTriageAccess.AllowsAsync(db,ticket.UserId,incident.BranchId,id,ct)) throw new InternalRequestReadException(403,"SCOPED_TRIAGE_REQUIRED");
        await tx.CommitAsync(ct);
        return new(id,ev.EventId,"CANCELLED");
    }
}
