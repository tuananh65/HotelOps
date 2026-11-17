using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.PreventiveMaintenance;

internal static class PreventiveOccurrenceEndOfShift
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
          AND name='HotelOps.PreventiveHold.V1' AND CONVERT(nvarchar(10),value)='1')
          AND OBJECT_ID('dbo.preventive_occurrence_holds','U') IS NOT NULL
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_pm_hold_immutable') AND is_disabled=0)
          AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_details')
            AND name IN('CK_pm_occurrence_state','CK_pm_occurrence_assignee') AND is_disabled=0 AND is_not_trusted=0)=2
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_holds')
            AND name IN('FK_pm_hold_interval','FK_pm_hold_run','FK_pm_hold_actor','FK_pm_hold_audit')
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)=4
          THEN 1 ELSE 0 END AS Value
        """).AnyAsync(x => x == 1, ct);

    // Caller owns the authenticated Serializable End Workday transaction and actor lock.
    internal static async Task<WorkdayResult> HoldAsync(HotelOpsDbContext db, SqlWorkTimeTransactionParticipant time,
        long actor, long branch, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new WorkTimeContractException("TIME_TRANSACTION_REQUIRED");
        if (!await InstalledAsync(db, ct)) return new(503, Code: "PREVENTIVE_HOLD_STORAGE_UNAVAILABLE");
        if (!await MaintenanceExecutionAccess.GrantsAsync(db, actor, branch, ct)) return new(403, Code: "MAINTENANCE_EXECUTE_REQUIRED");
        var read = await MaintenanceReadAccess.ReadAsync(db, actor, branch, ct);
        if (!read.Lead && !read.Staff) return new(403, Code: "MAINTENANCE_VIEW_REQUIRED");
        var matching = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.preventive_occurrence_details WITH(UPDLOCK,HOLDLOCK)
            WHERE run_id={id} AND branch_id={branch} AND assignee_id={actor} AND workflow_status='IN_PROGRESS'
            """).AnyAsync(ct);
        if (!matching) return new(409, Code: "OCCURRENCE_STATE_CHANGED");
        var run = await db.PreventiveMaintenanceRuns.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_runs WITH(UPDLOCK,HOLDLOCK) WHERE run_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (run is null || run.PerformedAt != null || run.PerformedBy != null || run.Result != null || run.MaintenanceWorkId != null)
            return new(409, Code: "OCCURRENCE_EXECUTION_CONFLICT");
        var open = await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == actor && i.ClosedAt == null).Take(2).ToArrayAsync(ct);
        if (open.Length != 1 || open[0].WorkItemKind != "OCCURRENCE" || open[0].WorkItemId != id || open[0].BranchId != branch)
            return new(409, Code: "OCCURRENCE_INTERVAL_REQUIRED");
        var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "OCCURRENCE", id), open[0].IntervalId, open[0].RowVersion, ct);
        var now = closed.ClosedAtUtc ?? throw new WorkTimeContractException("TIME_SOURCE_UNRESOLVED");
        if (await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.preventive_occurrence_details SET workflow_status='ON_HOLD'
            WHERE run_id={id} AND branch_id={branch} AND assignee_id={actor} AND workflow_status='IN_PROGRESS'
            """, ct) != 1) return new(412, Code: "OCCURRENCE_STATE_CHANGED");
        var audit = new AuditLog { BranchId = branch, ActorUserId = actor, EntityType = "preventive_maintenance_runs",
            EntityId = id.ToString(CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { status = "IN_PROGRESS" }),
            NewValues = JsonSerializer.Serialize(new { eventType = "PREVENTIVE_OCCURRENCE_HELD", operation = "WORKDAY_PAUSE_ALL",
                reasonCode = "END_OF_SHIFT", status = "ON_HOLD", runId = id, intervalId = closed.IntervalId, closed.WorkdayId, occurredAtUtc = now }) };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.preventive_occurrence_holds(interval_id,run_id,actor_id,held_at,audit_id,reason_code)
            VALUES({closed.IntervalId},{id},{actor},{now},{audit.AuditId},'END_OF_SHIFT')
            """, ct);
        read = await MaintenanceReadAccess.ReadAsync(db, actor, branch, ct);
        if (!read.Lead && !read.Staff) return new(403, Code: "MAINTENANCE_VIEW_REQUIRED");
        return await MaintenanceExecutionAccess.GrantsAsync(db, actor, branch, ct)
            ? new(200) : new(403, Code: "MAINTENANCE_EXECUTE_REQUIRED");
    }

    internal static Task<bool> HasResumeEvidenceAsync(HotelOpsDbContext db, long actor, long branch, long id, CancellationToken ct)
        => db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.preventive_occurrence_holds h
            JOIN dbo.work_intervals i ON i.interval_id=h.interval_id
            WHERE h.run_id={id} AND h.actor_id={actor} AND h.reason_code='END_OF_SHIFT'
              AND i.work_item_kind='OCCURRENCE' AND i.work_item_id={id} AND i.user_id={actor} AND i.branch_id={branch}
              AND i.closed_at=h.held_at AND i.closed_at>=i.opened_at
              AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals newer WHERE newer.work_item_kind='OCCURRENCE'
                AND newer.work_item_id={id} AND (newer.closed_at IS NULL OR newer.interval_id>i.interval_id))
            """).AnyAsync(ct);
}
