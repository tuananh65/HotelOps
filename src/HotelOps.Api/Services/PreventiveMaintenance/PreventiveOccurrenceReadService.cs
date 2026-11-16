using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed record PreventiveOccurrenceView(long RunId,long ScheduleId,string Title,string Instruction,
    PreventiveCheckItem[] CheckItems,long? AssetId,long? RoomId,long? AreaId,long? AssigneeId,string Status,
    DateTime DueAt,DateTime? PerformedAt,string? Result,string? Note,string Version,bool CanAssign,bool CanCancel);

public sealed class PreventiveOccurrenceReadService(HotelOpsDbContext db,ISqlSessionService sessions,ICurrentUserAccessor current)
{
    private sealed class Row
    {
        public long RunId { get; set; }
        public long ScheduleId { get; set; }
        public long? AssetId { get; set; }
        public long? RoomId { get; set; }
        public long? AreaId { get; set; }
        public string Definition { get; set; } = "";
        public long? AssigneeId { get; set; }
        public string Status { get; set; } = "";
        public DateTime DueAt { get; set; }
        public DateTime? PerformedAt { get; set; }
        public string? Result { get; set; }
        public string? Note { get; set; }
        public string Version { get; set; } = "";
    }
    private sealed record Snapshot(int SchemaVersion,long ScheduleId,string Title,string Instruction,
        PreventiveCheckItem[] CheckItems,long? AssetId,long? RoomId,long? AreaId,DateTime ScheduledDueAt);
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true};
    private async Task<MaintenanceReadAccess.Scope> AccessAsync(SessionReference ticket,long sid,long branch,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var access=await MaintenanceReadAccess.ReadAsync(db,ticket.UserId,branch,ct);
        if(!access.Lead&&!access.Staff)throw new InternalRequestReadException(403,"MAINTENANCE_VIEW_REQUIRED");
        return access;
    }
    public async Task<IReadOnlyList<PreventiveOccurrenceView>> ReadAsync(SessionReference ticket,long sid,long branch,
        long? id,long afterId,CancellationToken ct)
    {
        if(branch<=0||id is <=0||afterId<0)throw new InternalRequestReadException(400,"INVALID_OCCURRENCE_PAGE");
        await using var tx=db.Database.CurrentTransaction==null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct) : null;
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        var scope=await AccessAsync(ticket,sid,branch,ct);
        if(!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct))
            throw new InternalRequestReadException(503,"PREVENTIVE_OCCURRENCE_STORAGE_UNAVAILABLE");
        var rows=await db.Database.SqlQuery<Row>($"""
            SELECT TOP(50) r.run_id AS RunId,r.schedule_id AS ScheduleId,d.definition_json AS Definition,
              d.assignee_id AS AssigneeId,d.workflow_status AS Status,r.planned_at AS DueAt,r.performed_at AS PerformedAt,
              r.result AS Result,r.note AS Note,CONVERT(varchar(16),d.row_version,2) AS Version,
              s.asset_id AS AssetId,s.room_id AS RoomId,s.area_id AS AreaId
            FROM dbo.preventive_occurrence_details d
            JOIN dbo.preventive_maintenance_runs r ON r.run_id=d.run_id
            JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
            WHERE d.branch_id={branch} AND ({scope.Lead}=1 OR ({scope.Staff}=1 AND d.assignee_id={ticket.UserId}))
              AND r.run_id>{afterId} AND ({id} IS NULL OR r.run_id={id})
              AND ((s.asset_id IS NOT NULL AND s.room_id IS NULL AND s.area_id IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.assets a WHERE a.asset_id=s.asset_id AND a.branch_id={branch}))
                OR (s.room_id IS NOT NULL AND s.asset_id IS NULL AND s.area_id IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.rooms a WHERE a.room_id=s.room_id AND a.branch_id={branch}))
                OR (s.area_id IS NOT NULL AND s.asset_id IS NULL AND s.room_id IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.areas a WHERE a.area_id=s.area_id AND a.branch_id={branch})))
            ORDER BY r.run_id
            """).ToArrayAsync(ct);
        var result=new List<PreventiveOccurrenceView>();
        var canAssign=scope.Lead&&await PreventiveOccurrenceManagementService.AllowedAsync(db,ticket.UserId,branch,"MAINTENANCE_ASSIGN",ct);
        var canCancel=scope.Lead&&await PreventiveOccurrenceManagementService.AllowedAsync(db,ticket.UserId,branch,"PM_MANAGE",ct);
        foreach(var row in rows)
        {
            Snapshot? snapshot;
            try{snapshot=JsonSerializer.Deserialize<Snapshot>(row.Definition,JsonOptions);}
            catch(JsonException){throw new InternalRequestReadException(409,"OCCURRENCE_SNAPSHOT_INVALID");}
            if(snapshot==null||snapshot.SchemaVersion!=1||snapshot.ScheduleId!=row.ScheduleId||snapshot.ScheduledDueAt!=row.DueAt
                ||string.IsNullOrWhiteSpace(snapshot.Title)||string.IsNullOrWhiteSpace(snapshot.Instruction)||snapshot.CheckItems==null
                ||snapshot.CheckItems.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Key)||string.IsNullOrWhiteSpace(c.Description))
                ||new[]{snapshot.AssetId,snapshot.RoomId,snapshot.AreaId}.Count(v=>v!=null)!=1
                ||snapshot.AssetId!=row.AssetId||snapshot.RoomId!=row.RoomId||snapshot.AreaId!=row.AreaId)
                throw new InternalRequestReadException(409,"OCCURRENCE_SNAPSHOT_INVALID");
            result.Add(new(row.RunId,row.ScheduleId,snapshot.Title,snapshot.Instruction,snapshot.CheckItems,
                snapshot.AssetId,snapshot.RoomId,snapshot.AreaId,row.AssigneeId,row.Status,
                DateTime.SpecifyKind(row.DueAt,DateTimeKind.Utc),row.PerformedAt is {} performed?DateTime.SpecifyKind(performed,DateTimeKind.Utc):null,
                row.Result,row.Note,'"'+row.Version+'"',canAssign&&row.Status is "PENDING_ASSIGNMENT" or "ASSIGNED",
                canCancel&&row.Status is "PENDING_ASSIGNMENT" or "ASSIGNED"));
        }
        var finalScope=await AccessAsync(ticket,sid,branch,ct);
        if(result.Any(r=>!finalScope.Lead&&(!finalScope.Staff||r.AssigneeId!=ticket.UserId)))
            throw new InternalRequestReadException(403,"MAINTENANCE_VIEW_REQUIRED");
        if(id!=null&&result.Count==0)throw new InternalRequestReadException(404,"OCCURRENCE_UNAVAILABLE");
        if(tx!=null)await tx.CommitAsync(ct);
        return result;
    }
}
