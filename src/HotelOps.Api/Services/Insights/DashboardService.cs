using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class DashboardService(HotelOpsDbContext db, InsightAccess access, TimeProvider clock)
{
    public async Task<DashboardResult> ReadAsync(InsightQuery query, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var now = clock.GetUtcNow();
        var filters = await access.FiltersAsync("DASHBOARD_VIEW", query, now, ct);
        var branches = filters.BranchIds;
        var department = filters.DepartmentId;
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        var metrics = new List<MetricRow>();
        var groups = new List<DistributionRow>();
        var quantities = new List<QuantityRow>();
        var missing = new List<string>
        {
            "UC131: stored room codes differ from SRS display states; no lifecycle translation is inferred",
            "KPI-03: first-inspection cleaning-cycle contract unavailable",
            "KPI-04/05: task CLOSED and overdue policy differ from stored task states; status distribution only",
            "KPI-06: approved closed WorkInterval source unavailable",
            "KPI-08/09/10: accepted timestamp, current-cycle SLA deadlines and reopened source contract unavailable",
            "UC133: preventive occurrence lifecycle is not approved; no inferred due/overdue counts"
        };

        if (department is null)
        {
            var rooms = db.Rooms.AsNoTracking().Where(r => branches.Contains(r.BranchId) && r.IsActive);
            var roomGroups = await rooms.GroupBy(r => r.OperationalStatus)
                .Select(g => new { Status = g.Key, Count = g.LongCount() }).OrderBy(g => g.Status).ToArrayAsync(ct);
            groups.AddRange(roomGroups.Select(g => new DistributionRow("rooms.operational_status", g.Status, g.Count)));
            var total = roomGroups.Sum(g => g.Count);
            var ready = roomGroups.Where(g => g.Status == "READY").Sum(g => g.Count);
            metrics.Add(new("activeRooms", "Active rooms", total, "count", "rooms", "/room-readiness"));
            metrics.Add(new("readyRooms", "Ready rooms", ready, "count", "rooms.operational_status=READY", "/room-readiness?operationalStatusCode=READY"));
            metrics.Add(new("readyRatio", "Ready rooms ratio", total == 0 ? null : 100m * ready / total, "percent", "KPI-01"));
            var cleaning = db.CleaningTasks.AsNoTracking().Where(t => branches.Contains(t.Room.BranchId));
            groups.AddRange(await cleaning.GroupBy(t => t.Status)
                .Select(g => new DistributionRow("cleaning_tasks.status", g.Key, g.LongCount(), null)).ToArrayAsync(ct));
        }
        else
        {
            metrics.Add(new("activeRooms", "Active rooms", null, "count", "rooms: no department relationship"));
            metrics.Add(new("readyRooms", "Ready rooms", null, "count", "rooms: no department relationship"));
            metrics.Add(new("readyRatio", "Ready rooms ratio", null, "percent", "rooms: no department relationship"));
            missing.Add("UC131: rooms/cleaning have no approved department mapping; department-filtered figures withheld");
        }

        var tasks = db.OperationalTasks.AsNoTracking().Where(t => branches.Contains(t.BranchId)
            && (department == null || t.DepartmentId == department));
        groups.AddRange(await tasks.GroupBy(t => new { t.Status, t.PriorityId })
            .Select(g => new DistributionRow("operational_tasks.status/priority_id", g.Key.Status, g.LongCount(),
                g.Key.PriorityId == null ? null : g.Key.PriorityId.ToString())).ToArrayAsync(ct));

        // Incident has no authoritative department field. Do not derive it from
        // reporter identity or duplicate it across related maintenance works.
        if (department is null)
        {
            var incidents = db.Incidents.AsNoTracking().Where(i => branches.Contains(i.BranchId)
                && i.Status != "CLOSED" && i.Status != "CANCELLED");
            var incidentGroups = await incidents.GroupBy(i => new { i.Status, i.SeverityId })
                .Select(g => new { g.Key.Status, g.Key.SeverityId, Count = g.LongCount() }).ToArrayAsync(ct);
            groups.AddRange(incidentGroups.Select(g => new DistributionRow("incidents.status/severity_id", g.Status,
                g.Count, g.SeverityId?.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            metrics.Add(new("openIncidents", "Open incidents", incidentGroups.Sum(g => g.Count), "count", "KPI-07", "/incidents"));
        }
        else
        {
            metrics.Add(new("openIncidents", "Open incidents", null, "count", "incidents: no department relationship"));
            missing.Add("UC133: incident department relationship unavailable; incident figures withheld");
        }
        var works = MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking())
            .Where(w => w.BranchId != null && branches.Contains(w.BranchId.Value)
                && (department == null || w.Work.DepartmentId == department)).Select(w => w.Work);
        groups.AddRange(await works.GroupBy(w => w.Status)
            .Select(g => new DistributionRow("maintenance_works.status", g.Key, g.LongCount(), null)).ToArrayAsync(ct));

        var thresholds = db.StockThresholds.AsNoTracking().Where(t => branches.Contains(t.Warehouse.BranchId)
            && (department == null || t.Warehouse.DepartmentId == department));
        var low = thresholds.Where(t => (db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId)
            .Sum(m => (decimal?)m.SignedBaseQty) ?? 0) < t.MinimumQty);
        metrics.Add(new("lowStockItems", "Items below minimum", await low.LongCountAsync(ct), "warehouse-item pairs",
            "KPI-11: same available=sum(signed_base_qty) semantics as frozen Inventory", "/inventory/dashboard"));
        groups.AddRange(await low.GroupBy(t => t.WarehouseId)
            .Select(g => new DistributionRow("low_stock.warehouse_id", g.Key.ToString(), g.LongCount(), null)).ToArrayAsync(ct));

        var movements = db.StockMovements.AsNoTracking().Where(m => branches.Contains(m.Warehouse.BranchId)
            && (department == null || m.Warehouse.DepartmentId == department) && m.OccurredAt >= start && m.OccurredAt < end);
        quantities.AddRange(await movements.GroupBy(m => new { m.ItemId, m.Item.BaseUomId, m.WarehouseId })
            .Select(g => new QuantityRow("netStockMovement", g.Key.ItemId, g.Key.BaseUomId, g.Key.WarehouseId,
                g.Sum(m => m.SignedBaseQty))).ToArrayAsync(ct));
        // Preserve quantities per item AND unit. Summing heterogeneous units is invalid.
        var requestLines = db.MaterialRequestLines.AsNoTracking().Where(l => l.IsActive
            && branches.Contains(l.Request.BranchId) && (department == null || l.Request.DepartmentId == department)
            && (l.Request.Status == "APPROVED" || l.Request.Status == "PARTIALLY_ISSUED"));
        quantities.AddRange(await requestLines.GroupBy(l => new { l.ItemId, l.UomId })
            .Select(g => new QuantityRow("outstandingApproved", g.Key.ItemId, g.Key.UomId, null,
                g.Sum(l => (l.ApprovedQty ?? 0) > l.FulfilledQty ? (l.ApprovedQty ?? 0) - l.FulfilledQty : 0))).ToArrayAsync(ct));
        var replenishment = db.MaterialReplenishmentLines.AsNoTracking().Where(l => l.IsOpenCoverage
            && branches.Contains(l.MaterialReplenishment.Warehouse.BranchId)
            && (department == null || l.MaterialReplenishment.Warehouse.DepartmentId == department));
        quantities.AddRange(await replenishment.GroupBy(l => new { l.ItemId, l.BaseUomId, l.WarehouseId })
            .Select(g => new QuantityRow("openReplenishment", g.Key.ItemId, g.Key.BaseUomId, g.Key.WarehouseId,
                g.Sum(l => l.RequestedBaseQty > l.ReceivedBaseQty ? l.RequestedBaseQty - l.ReceivedBaseQty : 0))).ToArrayAsync(ct));
        missing.Add("KPI-13: cross-branch transfer-loss visibility policy unresolved; losses withheld");
        foreach (var (key, label) in new[] {
            ("firstInspectionPassRate", "First-time inspection pass rate"), ("overdueTasks", "Overdue tasks"),
            ("averageTaskCompletionHours", "Average task completion time"), ("taskEffortHours", "Task effort"),
            ("responseHours", "Average response time"), ("resolutionHours", "Average resolution time"),
            ("slaCompliance", "SLA compliance"), ("reopenedIncidents", "Reopened incidents"), ("transferLosses", "Transfer losses") })
            metrics.Add(new(key, label, null, "unavailable", "See missingSources"));
        await tx.CommitAsync(ct);
        return new(now, filters, metrics, groups.OrderBy(g => g.Source).ThenBy(g => g.Status).ThenBy(g => g.Severity).ToArray(),
            quantities.OrderBy(q => q.Kind).ThenBy(q => q.WarehouseId).ThenBy(q => q.ItemId).ThenBy(q => q.UomId).ToArray(), missing);
    }
}
