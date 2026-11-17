using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetConditionInput(string Action, string Note, string? Condition, long? ClosureAuditId, string? OpenIncidentReason);
public sealed class AssetConditionWorkflow(HotelOpsDbContext db, InsightAccess access, AssetProfileAccess profile, AssetConditionAuthority authority)
{
    private async Task<Asset> LoadAsync(long id, CancellationToken ct)
    {
        var asset = await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={id}").AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await authority.RequireAsync(asset.BranchId, asset.CurrentDepartmentId, ct);
        if (!await profile.Query(asset.BranchId).AnyAsync(a => a.AssetId == id, ct)) throw new InsightException(409, "ASSET_SCOPE_INCONSISTENT", "Asset scope requires correction.");
        if (asset.LifecycleStatus is not ("IN_USE" or "UNDER_REPAIR")) throw new InsightException(409, "ASSET_NOT_ACTIVE", "Condition changes require an active asset.");
        return asset;
    }
    private sealed record RepairProof(long AuditId, long IncidentId, string IncidentNo, DateTime AcceptedAt, long SubmissionAuditId);
    private async Task<RepairProof[]> RepairsAsync(Asset asset, CancellationToken ct)
    {
        var scope = await IncidentReadAccess.ReadAsync(db, access.UserId, asset.BranchId, ct, sourceRecord: true);
        var incidents = scope.Apply(db.Incidents.AsNoTracking()).Where(i => i.AssetId == asset.AssetId && i.Status == "CLOSED");
        var latestChange = await db.AssetConditionHistories.Where(h => h.AssetId == asset.AssetId).MaxAsync(h => (DateTime?)h.ChangedAt, ct);
        var works = await db.MaintenanceWorks.AsNoTracking().Where(w => w.IncidentId != null && incidents.Any(i => i.IncidentId == w.IncidentId)
            && w.Status == "CLOSED" && w.ReviewedAt != null && (latestChange == null || w.ReviewedAt >= latestChange)).ToArrayAsync(ct);
        var ids = works.Select(w => w.MaintenanceWorkId.ToString()).ToArray();
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "maintenance_works" && a.Action == "UPDATE"
            && a.BranchId == asset.BranchId && ids.Contains(a.EntityId)).ToArrayAsync(ct);
        var result = new List<RepairProof>();
        foreach (var audit in audits)
        {
            var closure = MaintenanceClosureSource.Parse(audit);
            if (closure == null || await MaintenanceClosureSource.ReadAsync(db, audit.AuditId, ct) == null) continue;
            var work = works.SingleOrDefault(w => w.MaintenanceWorkId == closure.WorkId);
            if (work == null || work.ResolvedAt == null || work.ActualStartAt == null || work.ResolvedAt < work.ActualStartAt || work.ReviewedAt < work.ResolvedAt) continue;
            var submissions = audits.Where(a => a.EntityId == audit.EntityId && a.OccurredAt == work.ResolvedAt && MaintenanceReviewSnapshot.Parse(a) != null).ToArray();
            if (submissions.Length != 1) continue;
            var submission = MaintenanceSubmissionSource.Parse(submissions[0]);
            if (submission == null || submission.WorkId != work.MaintenanceWorkId || submission.IncidentId != closure.IncidentId) continue;
            var number = await incidents.Where(i => i.IncidentId == closure.IncidentId).Select(i => i.IncidentNo).SingleAsync(ct);
            result.Add(new(audit.AuditId, closure.IncidentId, number, DateTime.SpecifyKind(closure.At, DateTimeKind.Utc), submissions[0].AuditId));
        }
        return result.OrderByDescending(r => r.AcceptedAt).ToArray();
    }
    public async Task<object> ReadAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await LoadAsync(id, ct);
        var scope = await IncidentReadAccess.ReadAsync(db, access.UserId, asset.BranchId, ct, sourceRecord: true);
        var open = await scope.Apply(db.Incidents.AsNoTracking()).Where(i => i.AssetId == id && i.Status != "CLOSED" && i.Status != "CANCELLED")
            .OrderBy(i => i.IncidentId).Select(i => new { i.IncidentId, i.IncidentNo, i.Status }).ToArrayAsync(ct);
        var result = new { asset.AssetId, asset.AssetCode, asset.AssetName, asset.ConditionStatus, asset.LifecycleStatus,
            Version = await AssetVersion.ReadAsync(db, id, ct), Incidents = open, Repairs = await RepairsAsync(asset, ct),
            IncidentAccess = scope.Lead || scope.Staff ? "ALLOWED" : "ACCESS_DENIED" };
        await authority.RequireAsync(asset.BranchId, asset.CurrentDepartmentId, ct);
        if (scope != await IncidentReadAccess.ReadAsync(db, access.UserId, asset.BranchId, ct, sourceRecord: true)) throw new InsightException(403, "ACCESS_DENIED", "Incident authority changed.");
        await tx.CommitAsync(ct); return result;
    }
    public async Task<object> SaveAsync(long id, string? version, AssetConditionInput input, CancellationToken ct)
    {
        var note = input.Note?.Trim(); var reason = input.OpenIncidentReason?.Trim();
        if (string.IsNullOrWhiteSpace(note) || note.Length > 4000 || reason?.Length > 4000 || input.Action is not ("DAMAGED" or "UNDER_REPAIR" or "REPAIRED" or "LOST"))
            throw new InsightException(400, "MSG03", "Select an action and enter a note (maximum 4000 characters).");
        if (input.Action != "REPAIRED" && (input.Condition != null || input.ClosureAuditId != null)) throw new InsightException(400, "MSG03", "Condition and repair receipt are only applicable to Repaired.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await LoadAsync(id, ct); var token = await AssetVersion.RequireAsync(db, id, version, ct);
        var lifecycle = asset.LifecycleStatus; var condition = asset.ConditionStatus; RepairProof? proof = null;
        if (input.Action == "DAMAGED") condition = "FAULTY";
        if (input.Action == "UNDER_REPAIR") lifecycle = "UNDER_REPAIR";
        if (input.Action == "LOST")
        {
            if (note.Length < 10) throw new InsightException(400, "MSG03", "Loss reason requires at least 10 characters.");
            if (!await db.Database.SqlQuery<int>($"""
                SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                    AND name='HotelOps.AssetCondition.V1' AND CONVERT(nvarchar(10),value)='1')
                  AND (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND name IN
                    ('TR_incidents_asset_available_v1','TR_pm_schedule_asset_available_v1','TR_pm_run_asset_available_v1'))=3
                  THEN 1 ELSE 0 END AS Value
                """).AnyAsync(n => n == 1, ct))
                throw new InsightException(503, "ASSET_CONDITION_STORAGE_UNAVAILABLE", "Asset condition development installer required for Lost.");
            lifecycle = "LOST";
        }
        if (input.Action == "REPAIRED")
        {
            if (input.Condition is not ("GOOD" or "FAIR" or "POOR")) throw new InsightException(400, "MSG03", "Select the inspected post-repair condition.");
            proof = (await RepairsAsync(asset, ct)).SingleOrDefault(p => p.AuditId == input.ClosureAuditId)
                ?? throw new InsightException(409, "REPAIR_PROOF_REQUIRED", "A readable, current accepted repair and submission receipt are required.");
            if (await db.Incidents.AnyAsync(i => i.AssetId == id && i.Status != "CLOSED" && i.Status != "CANCELLED", ct) && (reason?.Length ?? 0) < 10)
                throw new InsightException(400, "MSG03", "Returning to In Use while incidents remain open requires a reason of at least 10 characters.");
            lifecycle = "IN_USE"; condition = input.Condition;
        }
        if (condition == asset.ConditionStatus && lifecycle == asset.LifecycleStatus) throw new InsightException(409, "NO_STATE_CHANGE", "The selected state is already current.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.assets SET condition_status={condition},lifecycle_status={lifecycle},updated_at={now} WHERE asset_id={id} AND row_version={token}", ct) != 1)
            throw new InsightException(412, "MSG04", "Asset changed. Reload before saving.");
        var history = new AssetConditionHistory { AssetId = id, OldCondition = asset.ConditionStatus, NewCondition = condition, Note = note,
            RelatedIncidentId = proof?.IncidentId, ChangedBy = access.UserId, ChangedAt = now };
        db.AssetConditionHistories.Add(history); await db.SaveChangesAsync(ct);
        var after = AssetEditService.Snapshot(await db.Assets.AsNoTracking().SingleAsync(a => a.AssetId == id, ct));
        after["condition_action"] = input.Action; after["reason"] = note; after["open_incident_reason"] = reason;
        after["closure_audit_id"] = proof?.AuditId; after["submission_audit_id"] = proof?.SubmissionAuditId;
        db.AuditLogs.Add(new AuditLog { EntityType = "assets", EntityId = id.ToString(), BranchId = asset.BranchId, ActorUserId = access.UserId,
            Action = "UPDATE", OccurredAt = now, OldValues = JsonSerializer.Serialize(AssetEditService.Snapshot(asset)), NewValues = JsonSerializer.Serialize(after),
            ChangedFields = JsonSerializer.Serialize(new[] { "condition_status", "lifecycle_status" }) });
        db.AuditLogs.Add(new AuditLog { EntityType = "asset_condition_history", EntityId = history.ConditionHistoryId.ToString(), BranchId = asset.BranchId,
            ActorUserId = access.UserId, Action = "CREATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { asset_id = id,
                old_condition = asset.ConditionStatus, new_condition = condition, old_lifecycle = asset.LifecycleStatus, new_lifecycle = lifecycle,
                note, related_incident_id = proof?.IncidentId, closure_audit_id = proof?.AuditId, submission_audit_id = proof?.SubmissionAuditId,
                changed_by = access.UserId, changed_at = now, condition_action = input.Action }) });
        await db.SaveChangesAsync(ct); await authority.RequireAsync(asset.BranchId, asset.CurrentDepartmentId, ct);
        await tx.CommitAsync(ct); return new { AssetId = id, Message = "MSG08", OfferIncidentReport = input.Action == "DAMAGED" };
    }
}
