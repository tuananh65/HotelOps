using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceEndOfShift
{
    // Internal participant in the authenticated Workday transaction, not a legacy controller
    // invocation or a substitute permit for any other Maintenance action.
    internal static async Task<WorkdayResult> HoldAsync(HotelOpsDbContext db, SqlWorkTimeTransactionParticipant time,
        long actor, long branch, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new WorkTimeContractException("TIME_TRANSACTION_REQUIRED");
        // Do not strand held work while Resume is available only through a demo binding.
        if (!HotelOps.Api.Services.Dev2Authorization.Dev2ActionContracts.DecisionIds.Contains("A004A-028"))
            return new(503, Code: "MAINTENANCE_RESUME_UNAVAILABLE");
        if (!await MaintenanceExecutionVersion.InstalledAsync(db, ct)) return new(503, Code: "MAINTENANCE_RESUME_UNAVAILABLE");
        if (!await MaintenanceExecutionAccess.AllowsAsync(db, actor, branch, id, ct)) return new(403, Code: "MAINTENANCE_EXECUTE_DENIED");
        var incidentId = await db.Database.SqlQuery<long?>($"SELECT incident_id AS Value FROM dbo.maintenance_works WITH(READUNCOMMITTED) WHERE maintenance_work_id={id}").SingleOrDefaultAsync(ct);
        if (incidentId is null) return new(503, Code: "MAINTENANCE_SOURCE_UNAVAILABLE");
        var incident = await db.Incidents.FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={incidentId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var work = await db.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM dbo.maintenance_works WITH(UPDLOCK,HOLDLOCK) WHERE maintenance_work_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (work is null || incident is null || work.IncidentId != incidentId || incident.BranchId != branch
            || incident.Status != "IN_PROGRESS" || work.Status != "IN_PROGRESS" || work.AssigneeId != actor)
            return new(409, Code: "MAINTENANCE_STATE_CHANGED");
        var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == actor && i.ClosedAt == null).Take(2).ToArrayAsync(ct);
        if (intervals.Length != 1 || intervals[0].WorkItemKind != "MAINTENANCE" || intervals[0].WorkItemId != id || intervals[0].BranchId != branch)
            return new(409, Code: "TIME_SOURCE_UNRESOLVED");
        var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "MAINTENANCE", id), intervals[0].IntervalId, intervals[0].RowVersion, ct);
        if (await db.MaintenanceWorks.Where(w => w.MaintenanceWorkId == id && w.Status == "IN_PROGRESS"
                && w.AssigneeId == actor && w.IncidentId == incidentId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, "ON_HOLD"), ct) != 1)
            return new(412, Code: "MAINTENANCE_STATE_CHANGED");
        var now = closed.ClosedAtUtc!.Value;
        var log = new MaintenanceWorkLog { MaintenanceWorkId = id, WorkNote = "End of Shift", SpentMinutes = 0,
            Status = "ON_HOLD", LoggedBy = actor, LoggedAt = now };
        db.MaintenanceWorkLogs.Add(log);
        await db.SaveChangesAsync(ct);
        var receipt = new { operation = "WORKDAY_PAUSE_ALL", reasonCode = "END_OF_SHIFT", intervalId = closed.IntervalId,
            closed.WorkdayId, log.LogId, occurredAtUtc = now, status = "ON_HOLD" };
        db.AuditLogs.Add(new AuditLog { BranchId = branch, ActorUserId = actor, EntityType = "maintenance_works",
            EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { status = "IN_PROGRESS" }), NewValues = JsonSerializer.Serialize(receipt) });
        db.AuditLogs.Add(new AuditLog { BranchId = branch, ActorUserId = actor, EntityType = "maintenance_work_logs",
            EntityId = log.LogId.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "CREATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(new { maintenanceWorkId = id, reasonCode = "END_OF_SHIFT", workNote = log.WorkNote,
                log.SpentMinutes, log.Status, log.LoggedBy, log.LoggedAt, intervalId = closed.IntervalId }) });
        await db.SaveChangesAsync(ct);
        return await MaintenanceExecutionAccess.AllowsAsync(db, actor, branch, id, ct)
            ? new(200) : new(403, Code: "MAINTENANCE_EXECUTE_DENIED");
    }
}
