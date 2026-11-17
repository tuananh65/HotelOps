using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.PreventiveMaintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetRetirementInput(string Reason, bool Confirmed);
public sealed record AssetRetirementBlocker(string Kind, int Count);
public sealed class AssetRetirementService(HotelOpsDbContext db, AssetProfileAccess profile, InsightAccess access)
{
    public async Task<bool> CanOpenAsync(long id, CancellationToken ct)
    {
        try { var asset = await LoadAsync(id, ct); return asset.LifecycleStatus != "RETIRED"; }
        catch (InsightException e) when (e.Status is 403 or 404 or 409) { return false; }
    }
    private async Task RequireAsync(long branch, CancellationToken ct)
    {
        await profile.RequireAsync(branch, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == access.UserId && r.Role.RoleCode == "OPERATIONS_MANAGER"
            && r.Role.IsActive && r.IsActive && (r.BranchId == null || r.BranchId == branch)
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current Operations Manager required.");
        await access.BranchesAsync("ASSET_RETIRE", branch, ct);
    }
    private async Task<Asset> LoadAsync(long id, CancellationToken ct)
    {
        var row = await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await RequireAsync(row.BranchId, ct);
        if (!await profile.Query(row.BranchId).AnyAsync(a => a.AssetId == id, ct)) throw new InsightException(409, "ASSET_SCOPE_INCONSISTENT", "Asset scope requires correction.");
        if (row.LifecycleStatus is not ("IN_USE" or "UNDER_REPAIR" or "RETIRED")) throw new InsightException(409, "LIFECYCLE_UNAVAILABLE", "Unsupported legacy lifecycle.");
        return row;
    }
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.AssetRetirement.V1' AND CONVERT(nvarchar(10),value)='1')
              AND (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND name IN
                ('TR_assets_retirement_dependencies_v1','TR_asset_handovers_retirement_v1','TR_occurrence_details_retirement_v1',
                 'TR_location_incidents_v1','TR_location_preventive_maintenance_schedules_v1',
                 'TR_location_preventive_maintenance_runs_v1','TR_location_maintenance_works_v1'))=7
              THEN 1 ELSE 0 END AS Value
            """).AnyAsync(v => v == 1, ct);

    private sealed class RunState { public long RunId { get; set; } public string? State { get; set; } public int ValidTerminal { get; set; } }
    private async Task<AssetRetirementBlocker[]> BlockersAsync(long id, CancellationToken ct)
    {
        var blocks = new List<AssetRetirementBlocker>();
        void Add(string kind, int count) { if (count > 0) blocks.Add(new(kind, count)); }
        Add("OPEN_INCIDENT", await db.Incidents.AsNoTracking().CountAsync(i => i.AssetId == id && i.Status != "CLOSED" && i.Status != "CANCELLED", ct));
        Add("OPEN_MAINTENANCE_WORK", await db.MaintenanceWorks.AsNoTracking().CountAsync(w => w.Incident != null && w.Incident.AssetId == id && w.Status != "CLOSED" && w.Status != "CANCELLED", ct));
        Add("ACTIVE_PREVENTIVE_SCHEDULE", await db.PreventiveMaintenanceSchedules.AsNoTracking().CountAsync(s => s.AssetId == id && s.IsActive, ct));
        Add("PENDING_HANDOVER", await db.AssetHandovers.AsNoTracking().CountAsync(h => h.AssetId == id && h.HandoverStatus == "PENDING", ct));
        if (await db.PreventiveMaintenanceRuns.AsNoTracking().AnyAsync(r => r.Schedule.AssetId == id, ct))
        {
            if (!await PreventiveOccurrenceGeneration.InstalledAsync(db, ct) || !await PreventiveOccurrenceExecutionService.CompletionInstalledAsync(db, ct))
                Add("OCCURRENCE_PROVENANCE_UNAVAILABLE", 1);
            else
            {
                var runs = await db.Database.SqlQuery<RunState>($"""
                    SELECT r.run_id AS RunId,d.workflow_status AS State,
                      CASE WHEN d.workflow_status='COMPLETED' AND EXISTS(
                        SELECT 1 FROM dbo.preventive_occurrence_completions c
                        JOIN dbo.audit_logs a ON a.audit_id=c.audit_id
                        JOIN dbo.work_intervals w ON w.interval_id=c.interval_id
                        WHERE c.run_id=r.run_id AND c.completed_at=r.performed_at AND c.actor_id=r.performed_by
                          AND a.entity_type='preventive_maintenance_runs' AND a.entity_id=CONVERT(varchar(30),r.run_id)
                          AND a.branch_id=d.branch_id AND a.actor_user_id=c.actor_id AND a.occurred_at=c.completed_at
                          AND w.work_item_kind='OCCURRENCE' AND w.work_item_id=r.run_id AND w.branch_id=d.branch_id
                          AND w.user_id=c.actor_id AND w.closed_at=c.completed_at AND w.closed_by=c.actor_id)
                        THEN 1 WHEN d.workflow_status='CANCELLED' AND EXISTS(
                          SELECT 1 FROM dbo.audit_logs a WHERE a.entity_type='preventive_maintenance_runs'
                            AND a.entity_id=CONVERT(varchar(30),r.run_id) AND a.action='UPDATE' AND a.branch_id=d.branch_id
                            AND ISJSON(a.new_values)=1 AND JSON_VALUE(a.new_values,'$.eventType')='PREVENTIVE_OCCURRENCE_CANCELLED'
                            AND JSON_VALUE(a.new_values,'$.status')='CANCELLED'
                            AND TRY_CONVERT(bigint,JSON_VALUE(a.new_values,'$.runId'))=r.run_id
                            AND ISJSON(a.old_values)=1
                            AND JSON_VALUE(a.old_values,'$.status') IN('PENDING_ASSIGNMENT','ASSIGNED')
                            AND LEN(LTRIM(RTRIM(JSON_VALUE(a.new_values,'$.reason'))))>=10
                            AND a.actor_user_id>0 AND a.occurred_at<=SYSUTCDATETIME()) THEN 1 ELSE 0 END AS ValidTerminal
                    FROM dbo.preventive_maintenance_runs r
                    JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
                    LEFT JOIN dbo.preventive_occurrence_details d ON d.run_id=r.run_id
                    WHERE s.asset_id={id}
                    """).ToArrayAsync(ct);
                Add("OPEN_OR_UNPROVEN_OCCURRENCE", runs.Count(r => r.ValidTerminal != 1));
            }
        }
        Add("OPEN_WORK_INTERVAL", await db.WorkIntervals.AsNoTracking().CountAsync(w => w.ClosedAt == null
            && (w.WorkItemKind == "MAINTENANCE" && db.MaintenanceWorks.Any(m => m.MaintenanceWorkId == w.WorkItemId && m.Incident != null && m.Incident.AssetId == id)
                || w.WorkItemKind == "OCCURRENCE" && db.PreventiveMaintenanceRuns.Any(r => r.RunId == w.WorkItemId && r.Schedule.AssetId == id)), ct));
        if (!await InstalledAsync(db, ct)) Add("RETIREMENT_WRITER_GUARDS_NOT_INSTALLED", 1);
        return blocks.ToArray();
    }
    public async Task<object> ReadAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await LoadAsync(id, ct); var version = await AssetVersion.ReadAsync(db, id, ct);
        var blocks = asset.LifecycleStatus == "RETIRED" ? [] : await BlockersAsync(id, ct);
        await RequireAsync(asset.BranchId, ct); await tx.CommitAsync(ct);
        // Dependency categories/counts explain the decision without granting source-record access.
        return new { asset.AssetId, asset.AssetCode, asset.AssetName, asset.LifecycleStatus, Version = version,
            Blockers = blocks, CanRetire = asset.LifecycleStatus != "RETIRED" && blocks.Length == 0 };
    }
    public async Task<object> SaveAsync(long id, string? version, AssetRetirementInput input, CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (!input.Confirmed || reason == null || reason.Length is < 10 or > 4000)
            throw new InsightException(400, "MSG03", "Confirmation and a reason of 10–4000 characters required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await LoadAsync(id, ct); var token = await AssetVersion.RequireAsync(db, id, version, ct);
        if (asset.LifecycleStatus == "RETIRED") throw new InsightException(409, "ALREADY_RETIRED", "Asset already retired; no duplicate event created.");
        var blocks = await BlockersAsync(id, ct);
        if (blocks.Length != 0) throw new InsightException(409, "MSG34", "Retirement blocked: " + string.Join(", ", blocks.Select(b => b.Kind)));
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.assets SET lifecycle_status='RETIRED',updated_at={now} WHERE asset_id={id} AND row_version={token}", ct) != 1)
            throw new InsightException(412, "MSG04", "Asset changed. Reload before retirement.");
        db.AuditLogs.Add(new AuditLog { EntityType = "assets", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActorUserId = access.UserId, BranchId = asset.BranchId, Action = "UPDATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { asset_id = id, branch_id = asset.BranchId, lifecycle_status = asset.LifecycleStatus }),
            NewValues = JsonSerializer.Serialize(new { asset_id = id, branch_id = asset.BranchId, lifecycle_status = "RETIRED", reason, updated_at = now }),
            ChangedFields = "[\"lifecycle_status\"]" });
        await db.SaveChangesAsync(ct); await RequireAsync(asset.BranchId, ct);
        var result = new { AssetId = id, Version = await AssetVersion.ReadAsync(db, id, ct), Message = "MSG08" };
        await tx.CommitAsync(ct); return result;
    }
}
