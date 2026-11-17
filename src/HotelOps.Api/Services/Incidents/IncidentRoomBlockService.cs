using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Rooms;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentRoomBlockInput(string Action,long[] RoomIds,string Reason);
public sealed record IncidentRoomBlockSaved(long IncidentId,string Version,string Action);
public sealed record IncidentBlockRoom(long RoomId,string Number,string Readiness,bool InScope,bool BlockedByIncident);
public sealed record IncidentRoomBlockContext(long IncidentId,string Version,int Page,bool HasMore,IReadOnlyList<IncidentBlockRoom> Rooms);

public sealed partial class IncidentRoomBlockService(HotelOpsDbContext db,TimeProvider clock,ICurrentUserAccessor current)
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IncidentAffectedRooms.V1' AND CONVERT(nvarchar(10),value)='1')
         AND COL_LENGTH('dbo.incident_affected_rooms','added_at')=8
         AND COL_LENGTH('dbo.room_status_history','effective_from')=40 AND COL_LENGTH('dbo.room_status_history','effective_to')=40
         AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_affected_rooms') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
        THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;
    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor) || actor.UserId!=ticket.UserId || actor.SessionId!=sid || actor.IsRestricted || ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await Dev2SessionValidation.ValidateAsync(db,ticket,clock,ct);
        if(!valid.IsValid || valid.Session?.SessionId!=sid || valid.Session.IsRestricted) throw new InternalRequestReadException(401,"SESSION_INVALID");
    }
    private async Task AuthorizeAsync(long actor,Incident incident,CancellationToken ct)
    {
        if(!await IncidentRoomBlockAccess.AllowsAsync(db,actor,incident.BranchId,incident.IncidentId,ct)) throw new InternalRequestReadException(403,"SCOPED_ROOM_BLOCK_MANAGE_REQUIRED");
        if(!await InstalledAsync(db,ct)) throw new InternalRequestReadException(503,"AFFECTED_ROOM_STORAGE_UNAVAILABLE");
        if(incident.Status is "CLOSED" or "CANCELLED") throw new InternalRequestReadException(409,"INCIDENT_CLOSED");
    }
    private async Task<HashSet<long>> ScopeAsync(Incident incident,CancellationToken ct)
    {
        var mapped=await db.Database.SqlQuery<long>($"SELECT room_id AS Value FROM dbo.incident_affected_rooms WITH(HOLDLOCK) WHERE incident_id={incident.IncidentId}").ToArrayAsync(ct);
        return mapped.Concat(incident.RoomId is {} room ? [room] : Array.Empty<long>()).ToHashSet();
    }
    public async Task<IncidentRoomBlockContext> ReadAsync(SessionReference ticket,long sid,long id,int page,CancellationToken ct)
    {
        if(page<1 || page>100000) throw new ValidationException("Invalid room page.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);
        var incident=await db.Incidents.AsNoTracking().SingleOrDefaultAsync(i=>i.IncidentId==id,ct) ?? throw new InternalRequestReadException(404,"INCIDENT_UNAVAILABLE");
        await AuthorizeAsync(ticket.UserId,incident,ct);
        var version=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"INCIDENT_VERSION_UNAVAILABLE");
        var scoped=await ScopeAsync(incident,ct);
        var rooms=await db.Rooms.AsNoTracking().Where(r=>r.BranchId==incident.BranchId && r.Floor.BranchId==r.BranchId
            && (r.Area==null || r.Area.BranchId==r.BranchId)).OrderBy(r=>r.RoomId).Skip((page-1)*50).Take(51)
            .Select(r=>new{r.RoomId,r.RoomNumber}).ToArrayAsync(ct);
        var chosen=rooms.Take(50).ToArray();var ids=chosen.Select(r=>r.RoomId).ToArray();
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var states=await new EffectiveRoomReadiness(db).ReadAsync(ids,now,ct);
        var blocks=await db.RoomBlocks.AsNoTracking().Where(b=>b.IncidentId==id && ids.Contains(b.RoomId) && b.BlockedAt<=now && (b.ReleasedAt==null || b.ReleasedAt>now)).Select(b=>b.RoomId).ToArrayAsync(ct);
        await SessionAsync(ticket,sid,ct);await AuthorizeAsync(ticket.UserId,incident,ct);
        await tx.CommitAsync(ct);
        return new(id,version,page,rooms.Length>50,chosen.Select(r=>new IncidentBlockRoom(r.RoomId,r.RoomNumber,states[r.RoomId].Readiness,scoped.Contains(r.RoomId),blocks.Contains(r.RoomId))).ToArray());
    }
    public async Task<IncidentRoomBlockSaved> ChangeAsync(SessionReference ticket,long sid,long id,string tag,IncidentRoomBlockInput input,CancellationToken ct)
    {
        var reason=input.Reason?.Trim();
        if(input.Action is not ("MAP" or "BLOCK" or "REMOVE") || input.RoomIds is not {Length:>0 and <=50}
            || input.RoomIds.Any(r=>r<=0) || input.RoomIds.Distinct().Count()!=input.RoomIds.Length || reason==null || reason.Length<10)
            throw new ValidationException("Choose an action, one to fifty distinct rooms and a reason of at least ten characters.");
        if(db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction!=null) throw new InvalidOperationException("Clean block context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);
        var incident=await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}").AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new InternalRequestReadException(404,"INCIDENT_UNAVAILABLE");
        await AuthorizeAsync(ticket.UserId,incident,ct);
        var version=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"INCIDENT_VERSION_UNAVAILABLE");
        if(tag!=version) throw new InternalRequestReadException(412,"INCIDENT_CHANGED");
        var scope=await ScopeAsync(incident,ct);
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        foreach(var roomId in input.RoomIds.Order())
        {
            var room=await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={roomId}").AsTracking().SingleOrDefaultAsync(ct);
            if(room==null || room.BranchId!=incident.BranchId || !await db.Rooms.AsNoTracking().AnyAsync(r=>r.RoomId==roomId && r.Floor.BranchId==incident.BranchId
                && (r.Area==null || r.Area.BranchId==incident.BranchId),ct)) throw new InternalRequestReadException(404,"ROOM_UNAVAILABLE");
            if(input.Action=="MAP")
            {
                if(scope.Contains(roomId)) throw new ValidationException("Room is already in the affected scope.");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.incident_affected_rooms(incident_id,room_id,added_by,added_at,reason) VALUES({id},{roomId},{ticket.UserId},{now},{reason})",ct);
                Audit("incidents",id,incident.BranchId,ticket.UserId,now,new{affected_room_id=(long?)null},new{affected_room_id=roomId,reason,action="ADD_AFFECTED_ROOM"});
            }
            else
            {
                if(!scope.Contains(roomId)) throw new InternalRequestReadException(409,"ROOM_OUTSIDE_AFFECTED_SCOPE");
                await ChangeRoomAsync(incident,room,ticket.UserId,input.Action,reason,now,ct);
            }
        }
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.incidents SET status=status WHERE incident_id={id}",ct);
        var next=await IncidentTriageStore.TagAsync(db,id,ct) ?? throw new InternalRequestReadException(503,"INCIDENT_VERSION_UNAVAILABLE");
        await SessionAsync(ticket,sid,ct);await AuthorizeAsync(ticket.UserId,incident,ct);
        await tx.CommitAsync(ct);return new(id,next,input.Action);
    }
    private void Audit(string entity,long id,long branch,long actor,DateTime at,object before,object after)
        => db.AuditLogs.Add(new AuditLog{EntityType=entity,EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",BranchId=branch,ActorUserId=actor,OccurredAt=at,
            OldValues=JsonSerializer.Serialize(before),NewValues=JsonSerializer.Serialize(after)});
}
