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
    DateTime DueAt,DateTime? PerformedAt,string? Result,string? Note,string Version,bool CanAssign,bool CanCancel,bool CanStart,
    bool CanComplete,PreventiveCheckResult[]? Results,bool IsOverdue=false);

public sealed partial class PreventiveOccurrenceReadService(HotelOpsDbContext db,ISqlSessionService sessions,ICurrentUserAccessor current)
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
    public async Task<PreventiveTechnicianPage> TechniciansAsync(SessionReference ticket,long sid,long branch,long id,long afterId,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var source=(await ReadAsync(ticket,sid,branch,id,0,ct)).Single();
        if(!source.CanAssign)throw new InternalRequestReadException(403,"OCCURRENCE_ASSIGN_REQUIRED");
        var result=await PreventiveTechnicians.ReadAsync(db,branch,afterId,ct);
        if(!(await ReadAsync(ticket,sid,branch,id,0,ct)).Single().CanAssign)
            throw new InternalRequestReadException(403,"OCCURRENCE_ASSIGN_REQUIRED");
        await tx.CommitAsync(ct);return result;
    }
    public async Task<IReadOnlyList<PreventiveOccurrenceView>> ReadAsync(SessionReference ticket,long sid,long branch,
        long? id,long afterId,CancellationToken ct,bool mine=false,string? status=null,DateTimeOffset? dueFrom=null,DateTimeOffset? dueTo=null,bool? overdue=null)
    {
        if(dueFrom>dueTo)throw new InternalRequestReadException(400,"INVALID_DUE_RANGE");
        var fromUtc=dueFrom?.UtcDateTime;var toUtc=dueTo?.UtcDateTime;
        if(branch<=0||id is <=0||afterId<0)throw new InternalRequestReadException(400,"INVALID_OCCURRENCE_PAGE");
        if(status is not (null or "OPEN" or "PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "COMPLETED" or "CANCELLED"))
            throw new InternalRequestReadException(400,"INVALID_OCCURRENCE_STATUS");
        await using var tx=db.Database.CurrentTransaction==null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct) : null;
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        var scope=await AccessAsync(ticket,sid,branch,ct);
        if(!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct))
            throw new InternalRequestReadException(503,"PREVENTIVE_OCCURRENCE_STORAGE_UNAVAILABLE");
        // Resolve the cursor through the same source authorization and snapshot checks.
        // Its business due date, not its allocation ID, determines the next page.
        DateTime? afterDue=null;
        if(afterId>0)
        {
            if(id!=null)throw new InternalRequestReadException(400,"INVALID_OCCURRENCE_PAGE");
            var cursor=(await ReadAsync(ticket,sid,branch,afterId,0,ct)).Single();
            afterDue=cursor.DueAt;
        }
        var evaluatedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var rows=await db.Database.SqlQuery<Row>($"""
            SELECT TOP(50) r.run_id AS RunId,r.schedule_id AS ScheduleId,d.definition_json AS Definition,
              d.assignee_id AS AssigneeId,d.workflow_status AS Status,r.planned_at AS DueAt,r.performed_at AS PerformedAt,
              r.result AS Result,r.note AS Note,CONVERT(varchar(16),d.row_version,2) AS Version,
              s.asset_id AS AssetId,s.room_id AS RoomId,s.area_id AS AreaId
            FROM dbo.preventive_occurrence_details d
            JOIN dbo.preventive_maintenance_runs r ON r.run_id=d.run_id
            JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
            WHERE d.branch_id={branch} AND ({scope.Lead}=1 OR ({scope.Staff}=1 AND d.assignee_id={ticket.UserId}))
              AND ({afterId}=0 OR r.planned_at>{afterDue} OR (r.planned_at={afterDue} AND r.run_id>{afterId}))
              AND ({id} IS NULL OR r.run_id={id})
              AND ({mine}=0 OR d.assignee_id={ticket.UserId})
              AND ({fromUtc} IS NULL OR r.planned_at>={fromUtc}) AND ({toUtc} IS NULL OR r.planned_at<={toUtc})
              AND ({overdue} IS NULL
                OR ({overdue}=1 AND r.planned_at<{evaluatedAt} AND d.workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','IN_PROGRESS'))
                OR ({overdue}=0 AND (r.planned_at>={evaluatedAt} OR d.workflow_status IN('COMPLETED','CANCELLED'))))
              AND ({status} IS NULL OR d.workflow_status={status}
                OR ({status}='OPEN' AND d.workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED','IN_PROGRESS')))
              AND ((s.asset_id IS NOT NULL AND s.room_id IS NULL AND s.area_id IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.assets a WHERE a.asset_id=s.asset_id AND a.branch_id={branch}))
                OR (s.room_id IS NOT NULL AND s.asset_id IS NULL AND s.area_id IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.rooms a WHERE a.room_id=s.room_id AND a.branch_id={branch}))
                OR (s.area_id IS NOT NULL AND s.asset_id IS NULL AND s.room_id IS NULL
                    AND EXISTS(SELECT 1 FROM dbo.areas a WHERE a.area_id=s.area_id AND a.branch_id={branch})))
            ORDER BY r.planned_at,r.run_id
            """).ToArrayAsync(ct);
        var result=new List<PreventiveOccurrenceView>();
        var canAssign=scope.Lead&&await PreventiveOccurrenceManagementService.AllowedAsync(db,ticket.UserId,branch,"MAINTENANCE_ASSIGN",ct);
        var canCancel=scope.Lead&&await PreventiveOccurrenceManagementService.AllowedAsync(db,ticket.UserId,branch,"PM_MANAGE",ct);
        var canExecute=await MaintenanceExecutionAccess.GrantsAsync(db,ticket.UserId,branch,ct);
        var completionInstalled=await PreventiveOccurrenceExecutionService.CompletionInstalledAsync(db,ct);
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
            PreventiveCheckResult[]? savedResults=null;
            if(row.Status=="COMPLETED")
            {
                if(!completionInstalled)throw new InternalRequestReadException(503,"PREVENTIVE_COMPLETION_STORAGE_UNAVAILABLE");
                var proofs=await db.Database.SqlQuery<string>($"""
                    SELECT c.results_json AS Value FROM dbo.preventive_occurrence_completions c
                    JOIN dbo.work_intervals i ON i.interval_id=c.interval_id
                    JOIN dbo.audit_logs a ON a.audit_id=c.audit_id
                    JOIN dbo.preventive_maintenance_runs r ON r.run_id=c.run_id
                    WHERE c.run_id={row.RunId} AND c.actor_id={row.AssigneeId} AND c.completed_at=r.performed_at
                      AND c.actor_id=r.performed_by AND c.next_due_at=r.next_due_at
                      AND i.work_item_kind='OCCURRENCE' AND i.work_item_id=c.run_id AND i.user_id=c.actor_id
                      AND i.branch_id={branch} AND i.closed_at=c.completed_at AND i.closed_by=c.actor_id
                      AND a.entity_type='preventive_maintenance_runs' AND a.entity_id=CONVERT(varchar(30),c.run_id)
                      AND a.actor_user_id=c.actor_id AND a.branch_id={branch} AND a.occurred_at=c.completed_at
                    """).ToArrayAsync(ct);
                if(proofs.Length!=1)throw new InternalRequestReadException(409,"COMPLETION_PROVENANCE_INVALID");
                try{savedResults=JsonSerializer.Deserialize<PreventiveCheckResult[]>(proofs[0]);}
                catch(JsonException){throw new InternalRequestReadException(409,"COMPLETION_PROVENANCE_INVALID");}
                if(savedResults==null||savedResults.Any(r=>r==null||r.Passed==null||!snapshot.CheckItems.Any(c=>c.Key==r.Key))
                    ||savedResults.Select(r=>r.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=savedResults.Length
                    ||snapshot.CheckItems.Any(c=>c.Required&&!savedResults.Any(r=>r.Key==c.Key)))
                    throw new InternalRequestReadException(409,"COMPLETION_PROVENANCE_INVALID");
            }
            result.Add(new(row.RunId,row.ScheduleId,snapshot.Title,snapshot.Instruction,snapshot.CheckItems,
                snapshot.AssetId,snapshot.RoomId,snapshot.AreaId,row.AssigneeId,row.Status,
                DateTime.SpecifyKind(row.DueAt,DateTimeKind.Utc),row.PerformedAt is {} performed?DateTime.SpecifyKind(performed,DateTimeKind.Utc):null,
                row.Result,row.Note,'"'+row.Version+'"',canAssign&&row.Status is "PENDING_ASSIGNMENT" or "ASSIGNED",
                canCancel&&row.Status is "PENDING_ASSIGNMENT" or "ASSIGNED",canExecute&&row.AssigneeId==ticket.UserId&&row.Status=="ASSIGNED",
                completionInstalled&&canExecute&&row.AssigneeId==ticket.UserId&&row.Status=="IN_PROGRESS",savedResults,
                row.DueAt<evaluatedAt && row.Status is "PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS"));
        }
        var finalScope=await AccessAsync(ticket,sid,branch,ct);
        if(result.Any(r=>!finalScope.Lead&&(!finalScope.Staff||r.AssigneeId!=ticket.UserId)))
            throw new InternalRequestReadException(403,"MAINTENANCE_VIEW_REQUIRED");
        if(id!=null&&result.Count==0)throw new InternalRequestReadException(404,"OCCURRENCE_UNAVAILABLE");
        if(tx!=null)await tx.CommitAsync(ct);
        return result;
    }
}
