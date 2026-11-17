using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.PreventiveMaintenance;

internal sealed class PreventiveOccurrenceGeneration(HotelOpsDbContext db,IOptions<PreventiveGenerationOptions> options)
{
    private sealed class Definition
    {
        public string? Title { get; set; }
        public string? Checklist { get; set; }
        public long? Assignee { get; set; }
        public string Version { get; set; } = "";
    }
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.PreventiveOccurrence.V1' AND CONVERT(nvarchar(10),value)='1')
              AND OBJECT_ID('dbo.preventive_occurrence_details','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_pm_occurrence_provenance') AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).AnyAsync(v=>v==1,ct);

    internal async Task<long[]> PendingAsync(long after,CancellationToken ct)
    {
        if(!await InstalledAsync(db,ct))throw new InvalidOperationException("Preventive occurrence storage unavailable.");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var horizon=now.AddDays(options.Value.LeadDays);
        return await db.PreventiveMaintenanceSchedules.AsNoTracking()
            .Where(s=>s.ScheduleId>after && s.IsActive && s.NextDueAt<=horizon)
            .OrderBy(s=>s.ScheduleId).Take(100).Select(s=>s.ScheduleId).ToArrayAsync(ct);
    }

    internal async Task<int> GenerateAsync(long id,CancellationToken ct)
    {
        if(db.ChangeTracker.HasChanges())throw new InvalidOperationException("Clean generation context required.");
        if(!await InstalledAsync(db,ct))throw new InvalidOperationException("Preventive occurrence storage unavailable.");
        // Read only the assignee hint before ordered user locks. Recheck it under the schedule lock.
        var hint=await DefinitionAsync(id,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if(hint.Assignee is {} user)await new UserEligibilityLock(db).LockAsync([user],ct);
        var row=await db.PreventiveMaintenanceSchedules.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_schedules WITH(UPDLOCK,HOLDLOCK) WHERE schedule_id={id}")
            .SingleOrDefaultAsync(ct);
        if(row==null || !row.IsActive){await tx.CommitAsync(ct);return 0;}
        var definition=await DefinitionAsync(id,ct);
        if(hint.Assignee!=definition.Assignee)throw new InvalidOperationException("Default assignee changed; retry next run.");
        if(string.IsNullOrWhiteSpace(definition.Title) || string.IsNullOrWhiteSpace(row.Instruction) || definition.Checklist==null)
            throw new ValidationException("Historical schedule lacks an approved definition.");
        var checks=JsonSerializer.Deserialize<PreventiveCheckItem[]>(definition.Checklist)
            ?? throw new ValidationException("Checklist unavailable.");
        if(checks.Length>100 || checks.Any(c=>c==null || string.IsNullOrWhiteSpace(c.Key) || string.IsNullOrWhiteSpace(c.Description))
            || checks.Select(c=>c.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=checks.Length)
            throw new ValidationException("Checklist invalid.");
        if(new[]{row.AssetId,row.RoomId,row.AreaId}.Count(v=>v!=null)!=1)throw new ValidationException("Ambiguous schedule target.");
        long? branch=row.AssetId is {} asset ? await db.Assets.Where(a=>a.AssetId==asset && (a.LifecycleStatus == "IN_USE" || a.LifecycleStatus == "UNDER_REPAIR")).Select(a=>(long?)a.BranchId).SingleOrDefaultAsync(ct)
            : row.RoomId is {} room ? await db.Rooms.Where(r=>r.RoomId==room && r.IsActive).Select(r=>(long?)r.BranchId).SingleOrDefaultAsync(ct)
            : await db.Areas.Where(a=>a.AreaId==row.AreaId && a.IsActive).Select(a=>(long?)a.BranchId).SingleOrDefaultAsync(ct);
        if(branch==null || !await db.Branches.AnyAsync(b=>b.BranchId==branch && b.IsActive,ct))throw new ValidationException("Schedule target unavailable.");
        // Never assign work to a revoked/inactive default technician. Retain this schedule for correction/retry.
        if(definition.Assignee is {} assignee && !await PreventiveTechnicians.EligibleAsync(db,assignee,branch.Value,ct))
            throw new ValidationException("Default assignee no longer eligible.");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var horizon=now.AddDays(options.Value.LeadDays);
        var created=0;
        while(row.NextDueAt<=horizon && created<100)
        {
            var due=row.NextDueAt;
            var next=PreventiveScheduleService.NextDue(due,row.Frequency,row.FrequencyInterval);
            // Existing historical occurrences are not adopted without their generation snapshot.
            if(await db.PreventiveMaintenanceRuns.AnyAsync(r=>r.ScheduleId==id && r.PlannedAt==due,ct))
                throw new ValidationException("Occurrence already exists for the current due date; reconcile its provenance.");
            var run=new PreventiveMaintenanceRun{ScheduleId=id,PlannedAt=due,NextDueAt=next};
            db.PreventiveMaintenanceRuns.Add(run);
            await db.SaveChangesAsync(ct);
            var snapshot=JsonSerializer.Serialize(new{schemaVersion=1,scheduleId=id,scheduleVersion=definition.Version,
                title=definition.Title,instruction=row.Instruction,checkItems=checks,frequency=row.Frequency,interval=row.FrequencyInterval,
                assetId=row.AssetId,roomId=row.RoomId,areaId=row.AreaId,scheduledDueAt=due,nextDueAt=next,defaultAssigneeId=definition.Assignee});
            var status=definition.Assignee==null?"PENDING_ASSIGNMENT":"ASSIGNED";
            var audit=new AuditLog{BranchId=branch,ActorUserId=null,EntityType="preventive_maintenance_runs",
                EntityId=run.RunId.ToString(CultureInfo.InvariantCulture),Action="CREATE",OccurredAt=now,
                NewValues=JsonSerializer.Serialize(new{eventType="PREVENTIVE_OCCURRENCE_GENERATED",runId=run.RunId,scheduleId=id,
                    assigneeId=definition.Assignee,status,scheduledDueAt=due,nextDueAt=next,notificationPending=definition.Assignee!=null,definition=snapshot})};
            db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.preventive_occurrence_details(run_id,branch_id,definition_json,assignee_id,workflow_status,generated_at,generation_audit_id)
                VALUES({run.RunId},{branch.Value},{snapshot},{definition.Assignee},{status},{now},{audit.AuditId})
                """,ct);
            row.NextDueAt=next;
            await db.SaveChangesAsync(ct);
            created++;
        }
        await tx.CommitAsync(ct);return created;
    }
    private Task<Definition> DefinitionAsync(long id,CancellationToken ct)=>db.Database.SqlQuery<Definition>($"""
        SELECT title AS Title,check_items_json AS Checklist,default_assignee_id AS Assignee,CONVERT(varchar(16),row_version,2) AS Version
        FROM dbo.preventive_maintenance_schedules WHERE schedule_id={id}
        """).SingleAsync(ct);
}
