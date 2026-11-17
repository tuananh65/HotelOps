using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace HotelOps.Api.Services.Insights;

public sealed class RoomDashboardService(HotelOpsDbContext db, InsightAccess access, TimeProvider clock)
{
    private TaskDashboardAccess Actor => new(db, access, clock);

    public async Task<InsightOptions> OptionsAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var options = await access.OptionsAsync("DASHBOARD_VIEW", ct);
        var permitted = new List<InsightBranch>();
        foreach (var branch in options.Branches)
            if (await Actor.AllowedAsync(branch.Id, ct)) permitted.Add(branch);
        if (permitted.Count == 0 && !await Actor.AllowedAsync(null, ct)) Deny();
        await tx.CommitAsync(ct);
        return options with { Branches = permitted, Departments = options.Departments
            .Where(d => permitted.Any(b => b.Id == d.BranchId)).ToArray() };
    }

    public async Task<DashboardResult> ReadAsync(InsightQuery query, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = clock.GetUtcNow();
        var filters = await access.FiltersAsync("DASHBOARD_VIEW", query, now, ct);
        var permitted = new List<long>();
        if (filters.BranchIds.Length == 0 && !await Actor.AllowedAsync(null, ct)) Deny();
        foreach (var branch in filters.BranchIds)
            if (await Actor.AllowedAsync(branch, ct)) permitted.Add(branch);
        if (query.BranchId != null && !permitted.Contains(query.BranchId.Value)
            || permitted.Count == 0 && !await Actor.AllowedAsync(null, ct)) Deny();
        if (filters.DepartmentId is { } department)
        {
            var branch = await db.Departments.AsNoTracking().Where(d => d.DepartmentId == department)
                .Select(d => d.BranchId).SingleAsync(ct);
            if (!permitted.Contains(branch)) Deny();
            permitted = [branch];
        }
        filters = filters with { BranchIds = permitted.ToArray() };
        var metrics = new List<MetricRow>();
        var groups = new List<DistributionRow>();
        var missing = new List<string>();
        await AppendAsync(filters, now.UtcDateTime, metrics, groups, missing, ct);
        foreach (var branch in permitted) if (!await Actor.AllowedAsync(branch, ct)) Deny();
        await tx.CommitAsync(ct);
        return new(now, filters, metrics, groups, [], missing);
    }

    private static void Deny() => throw new InsightException(403, "ROOM_DASHBOARD_SCOPE",
        "Operations Manager or Owner with DASHBOARD_VIEW is required in the selected scope.");

    internal async Task AppendAsync(InsightFilters filters, DateTime now, List<MetricRow> metrics,
        List<DistributionRow> groups, List<string> missing, CancellationToken ct)
    {
        if (filters.BranchIds.Length == 0 && !await Actor.AllowedAsync(null, ct)) Deny();
        foreach (var branch in filters.BranchIds)
            if (!await Actor.AllowedAsync(branch, ct))
            {
                missing.Add("UC131: Operations Manager or Owner with DASHBOARD_VIEW required in every selected branch.");
                return;
            }
        if (filters.DepartmentId != null && !await RoomDepartmentSchema.InstalledAsync(db, ct))
        {
            foreach (var (key, label, unit) in new[] { ("activeRooms", "Active rooms", "count"),
                ("readyRooms", "Ready rooms", "count"), ("readyRatio", "Ready ratio", "percent"),
                ("overdueCleaningTasks", "Overdue cleaning tasks", "count"),
                ("firstInspectionPassRate", "First-time inspection pass rate", "percent") })
                metrics.Add(new(key, label, null, unit, "Room-department mapping schema unavailable"));
            missing.Add("UC131: explicit room-department mapping storage is unavailable.");
            return;
        }
        var mapped = filters.DepartmentId is { } selected
            ? await db.Database.SqlQuery<long>($"SELECT room_id AS Value FROM dbo.room_department_mappings WITH(HOLDLOCK) WHERE department_id={selected} AND is_active=1").ToArrayAsync(ct)
            : null;
        var ids = await db.Rooms.AsNoTracking().Where(r => r.IsActive && filters.BranchIds.Contains(r.BranchId) && (mapped == null || mapped.Contains(r.RoomId)))
            .Select(r => r.RoomId).ToArrayAsync(ct);
        IReadOnlyDictionary<long, EffectiveRoomState> effective;
        try { effective = await new EffectiveRoomReadiness(db).ReadAsync(ids, now, ct); }
        catch (SqlException error) when (MissingSource(error))
        {
            effective = ids.ToDictionary(id => id, id => new EffectiveRoomState(id, "UNKNOWN"));
            missing.Add("UC131: room lifecycle/block source could not be read.");
        }
        var unknown = ids.Length != effective.Count || effective.Values.Any(r => r.Readiness == "UNKNOWN");
        foreach (var status in new[] { "READY", "PENDING_CLEANING", "CLEANING_IN_PROGRESS", "PENDING_INSPECTION", "REWORK_REQUIRED", "BLOCKED", "UNKNOWN" })
        {
            var count = effective.Values.LongCount(r => r.Readiness == status);
            if (!unknown || count > 0) groups.Add(new("rooms.effective_readiness", status, count));
        }
        var ready = effective.Values.LongCount(r => r.CanUse);
        metrics.Add(new("activeRooms", "Active rooms", ids.Length, "count", "Active rooms at snapshot"));
        metrics.Add(new("readyRooms", "Ready rooms", unknown ? null : ready, "count", "GBR-044"));
        metrics.Add(new("readyRatio", "Ready ratio", unknown || ids.Length == 0 ? null : 100m * ready / ids.Length, "percent", "KPI-01"));
        if (unknown) missing.Add("UC131: some room lifecycle evidence is unavailable; status counts are incomplete and UNKNOWN never permits room use.");
        try
        {
            var tasks = db.CleaningTasks.AsNoTracking().Where(t => filters.BranchIds.Contains(t.Room.BranchId) && (mapped == null || mapped.Contains(t.RoomId)));
            var open = tasks.Where(t => t.Status != "COMPLETED" && t.Status != "CANCELLED");
            var overdue = await open.LongCountAsync(t => t.DueAt != null && t.DueAt < now, ct);
            var missingDue = await open.AnyAsync(t => t.DueAt == null, ct);
            metrics.Add(new("overdueCleaningTasks", "Overdue cleaning tasks", missingDue ? null : overdue, "count", "Persisted due time; open tasks at snapshot"));
            if (missingDue) missing.Add("UC131: some open cleaning tasks lack due time; complete overdue count is unavailable.");
        }
        catch (SqlException error) when (MissingSource(error))
        {
            metrics.Add(new("overdueCleaningTasks", "Overdue cleaning tasks", null, "count", "Cleaning source unavailable"));
            missing.Add("UC131: cleaning source could not be read.");
        }
        var today = DateOnly.FromDateTime(now);
        foreach (var period in new[] { (Filters: filters, Key: "firstInspectionPassRate"),
            (Filters: filters with { From = today, To = today }, Key: "todayFirstInspectionPassRate") })
        {
            try { await FirstPassAsync(period.Filters, mapped, now, period.Key, metrics, missing, ct); }
            catch (SqlException error) when (MissingSource(error))
            {
                metrics.Add(new(period.Key, period.Key == "todayFirstInspectionPassRate" ? "Today's first-time inspection pass rate (UTC)" : "First-time inspection pass rate (period)", null, "percent", "Inspection source unavailable"));
                missing.Add("KPI-03: inspection or initial assignment evidence could not be read.");
            }
        }
    }

    private static bool MissingSource(SqlException error) => error.Number is 207 or 208 or 229 or 2812;

    private async Task FirstPassAsync(InsightFilters filters, long[]? mapped, DateTime now, string key, List<MetricRow> metrics, List<string> missing, CancellationToken ct)
    {
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        var inspections = await db.RoomInspections.AsNoTracking()
            .Where(i => filters.BranchIds.Contains(i.Room.BranchId) && i.Status != "CANCELLED" && (mapped == null || mapped.Contains(i.RoomId)))
            .Select(i => new { i.InspectionId, i.CleaningTaskId, i.RoomId, i.Status, i.Result, i.InspectedAt }).ToArrayAsync(ct);
        var period = inspections.Where(i => i.InspectedAt >= start && i.InspectedAt < end && i.InspectedAt <= now).ToArray();
        var cycles = period.Where(i => i.CleaningTaskId != null).Select(i => i.CleaningTaskId!.Value).Distinct().ToArray();
        var incomplete = period.Any(i => i.CleaningTaskId == null);
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "INSPECTION_ASSIGNMENT"
            && a.Action == "CREATE" && a.BranchId != null && filters.BranchIds.Contains(a.BranchId.Value)).ToArrayAsync(ct);
        var initial = new Dictionary<long, List<long>>();
        foreach (var audit in audits)
        {
            var parsed = InitialInspectionAssignmentSource.Parse(audit.NewValues);
            if (parsed == null || !cycles.Contains(parsed.Entity.TaskId)) continue;
            var verified = await InitialInspectionAssignmentSource.VerifyAsync(db, audit, ct);
            if (verified == null) { incomplete = true; continue; }
            if (!initial.TryGetValue(verified.Entity.TaskId, out var list)) initial[verified.Entity.TaskId] = list = [];
            list.Add(verified.InspectionId);
        }
        long passed = 0;
        foreach (var cycle in cycles)
        {
            if (!initial.TryGetValue(cycle, out var first) || first.Count != 1) { incomplete = true; continue; }
            var inspection = inspections.SingleOrDefault(i => i.InspectionId == first[0] && i.CleaningTaskId == cycle);
            if (inspection == null || inspection.InspectedAt == null || inspection.Status != "COMPLETED"
                || inspection.Result is not ("PASS" or "FAIL")) { incomplete = true; continue; }
            if (inspection.Result == "PASS") passed++;
        }
        metrics.Add(new(key, key == "todayFirstInspectionPassRate" ? "Today's first-time inspection pass rate (UTC)" : "First-time inspection pass rate (selected period)", incomplete || cycles.Length == 0
            ? null : 100m * passed / cycles.Length, "percent", "KPI-03: distinct cycles inspected in period; verified initial assignment"));
        if (incomplete) missing.Add("KPI-03: missing or ambiguous initial-inspection receipt, cycle linkage or final inspection result; no first-inspection inference.");
    }
}
