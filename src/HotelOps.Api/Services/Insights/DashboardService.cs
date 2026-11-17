using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class DashboardService(HotelOpsDbContext db, InsightAccess access, TimeProvider clock, DashboardSessionGuard session)
{
    public async Task<DashboardResult> ReadAsync(InsightQuery query, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await session.RequireAsync(ct);
        var now = clock.GetUtcNow();
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var filters = await access.FiltersAsync("DASHBOARD_VIEW", query, now, ct);
        var actor = new TaskDashboardAccess(db, access, clock);
        var permitted = new List<long>();
        foreach (var branch in filters.BranchIds) if (await actor.AllowedAsync(branch, ct)) permitted.Add(branch);
        if (query.BranchId != null && !permitted.Contains(query.BranchId.Value)
            || permitted.Count == 0 && !await actor.AllowedAsync(null, ct))
            throw new InsightException(403, "DASHBOARD_ACTOR", "Operations Manager or Owner with DASHBOARD_VIEW required.");
        if (query.DepartmentId is {} selected && !await db.Departments.AsNoTracking()
            .AnyAsync(d => d.DepartmentId == selected && permitted.Contains(d.BranchId), ct))
            throw new InsightException(403, "DEPARTMENT_SCOPE", "Department outside dashboard scope.");
        filters = filters with { BranchIds = permitted.ToArray() };
        // A department has one real branch. Echo the effective intersection so
        // drill-down does not request that department from unrelated branches.
        if (filters.DepartmentId is { } selectedDepartment)
            filters = filters with { BranchIds = await db.Departments.AsNoTracking()
                .Where(d => d.DepartmentId == selectedDepartment && filters.BranchIds.Contains(d.BranchId))
                .Select(d => d.BranchId).ToArrayAsync(ct) };
        var branches = filters.BranchIds;
        var department = filters.DepartmentId;
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        var metrics = new List<MetricRow>();
        var groups = new List<DistributionRow>();
        var quantities = new List<QuantityRow>();
        var categoryQuantities = new List<CategoryQuantityRow>();
        var missing = new List<string>();

        await new RoomDashboardService(db, access, clock).AppendAsync(filters, now.UtcDateTime, metrics, groups, missing, ct);

        var taskAccess = new TaskDashboardAccess(db, access, clock);
        var taskAllowed = true;
        foreach (var branch in branches)
            taskAllowed &= await taskAccess.AllowedAsync(branch, ct);
        if (branches.Length == 0) taskAllowed = await taskAccess.AllowedAsync(null, ct);
        if (taskAllowed)
        {
            var tasks = db.OperationalTasks.AsNoTracking().Where(t => branches.Contains(t.BranchId)
                && (department == null || t.DepartmentId == department));
            groups.AddRange(await tasks.GroupBy(t => new { t.Status, t.PriorityId })
                .Select(g => new DistributionRow("operational_tasks.status/priority_id", g.Key.Status, g.LongCount(),
                    g.Key.PriorityId == null ? null : g.Key.PriorityId.ToString())).ToArrayAsync(ct));
            await TaskDashboardQueries.AppendAsync(db, filters, now.UtcDateTime, metrics, groups, missing, ct);
            await TaskEffortDashboard.AppendAsync(db, filters, now.UtcDateTime, metrics, missing, ct);
            await TaskCompletionDashboard.AppendAsync(db, filters, now.UtcDateTime, metrics, missing, ct);
        }
        else missing.Add("UC132: task indicators require Operations Manager or Owner with DASHBOARD_VIEW in every selected branch");

        try { await IncidentDashboardQueries.AppendAsync(db, filters, now.UtcDateTime, metrics, groups, missing, ct); }
        catch (Exception error) when (SourceUnavailable(error))
        {
            missing.Add("UC133: an Incident/maintenance source could not be read; independent figures remain available.");
            foreach (var key in new[] { "openIncidents", "responseHours", "resolutionHours", "slaCompliance", "reopenedIncidents", "incidentBlockedRooms", "preventiveDue", "preventiveOverdue" })
                if (!metrics.Any(m => m.Key == key)) metrics.Add(new(key, key, null, "unavailable", "Source unavailable"));
        }
        ReportTransferLosses.Result losses = new([], false);
        try
        {

        var thresholds = db.StockThresholds.AsNoTracking().Where(t => branches.Contains(t.Warehouse.BranchId)
            && (t.Warehouse.DepartmentId == null || t.Warehouse.Department!.BranchId == t.Warehouse.BranchId)
            && (department == null || t.Warehouse.DepartmentId == department));
        var low = thresholds.Where(t => (db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId)
            .Sum(m => (decimal?)m.SignedBaseQty) ?? 0) < t.MinimumQty);
        metrics.Add(new("lowStockItems", "Items below minimum", await low.LongCountAsync(ct), "warehouse-item pairs",
            "KPI-11: same available=sum(signed_base_qty) semantics as frozen Inventory", "/inventory/dashboard"));
        groups.AddRange(await low.GroupBy(t => t.WarehouseId)
            .Select(g => new DistributionRow("low_stock.warehouse_id", g.Key.ToString(), g.LongCount(), null)).ToArrayAsync(ct));

        var movements = db.StockMovements.AsNoTracking().Where(m => branches.Contains(m.Warehouse.BranchId)
            && (m.Warehouse.DepartmentId == null || m.Warehouse.Department!.BranchId == m.Warehouse.BranchId)
            && (department == null || m.Warehouse.DepartmentId == department) && m.OccurredAt >= start && m.OccurredAt < end);
        quantities.AddRange(await movements.GroupBy(m => new { m.ItemId, m.Item.BaseUomId, m.WarehouseId })
            .Select(g => new QuantityRow("netStockMovement", g.Key.ItemId, g.Key.BaseUomId, g.Key.WarehouseId,
                g.Sum(m => m.SignedBaseQty))).ToArrayAsync(ct));
        // Preserve quantities per item AND unit. Summing heterogeneous units is invalid.
        var requestLines = db.MaterialRequestLines.AsNoTracking().Where(l => l.IsActive
            && branches.Contains(l.Request.BranchId) && l.Request.Department.BranchId == l.Request.BranchId && (department == null || l.Request.DepartmentId == department)
            && (l.Request.Status == "APPROVED" || l.Request.Status == "PARTIALLY_ISSUED"));
        if (await requestLines.AnyAsync(l => l.ApprovedQty == null, ct)) missing.Add("KPI-12: approved quantities are missing on some lines; those lines are unavailable, not zero.");
        quantities.AddRange(await requestLines.Where(l => l.ApprovedQty != null).GroupBy(l => new { l.ItemId, l.UomId })
            .Select(g => new QuantityRow("outstandingApproved", g.Key.ItemId, g.Key.UomId, null,
                g.Sum(l => (l.ApprovedQty ?? 0) > l.FulfilledQty ? (l.ApprovedQty ?? 0) - l.FulfilledQty : 0))).ToArrayAsync(ct));
        var replenishment = db.MaterialReplenishmentLines.AsNoTracking().Where(l => l.IsOpenCoverage
            && l.WarehouseId == l.MaterialReplenishment.WarehouseId && branches.Contains(l.MaterialReplenishment.Warehouse.BranchId)
            && (l.MaterialReplenishment.Warehouse.DepartmentId == null || l.MaterialReplenishment.Warehouse.Department!.BranchId == l.MaterialReplenishment.Warehouse.BranchId)
            && (department == null || l.MaterialReplenishment.Warehouse.DepartmentId == department));
        quantities.AddRange(await replenishment.GroupBy(l => new { l.ItemId, l.BaseUomId, l.WarehouseId })
            .Select(g => new QuantityRow("openReplenishment", g.Key.ItemId, g.Key.BaseUomId, g.Key.WarehouseId,
                g.Sum(l => l.RequestedBaseQty > l.ReceivedBaseQty ? l.RequestedBaseQty - l.ReceivedBaseQty : 0))).ToArrayAsync(ct));
        var allDashboard = await access.FiltersAsync("DASHBOARD_VIEW", new() { From = filters.From, To = filters.To }, now, ct);
        var lossAuthority = new List<long>();
        foreach (var branch in allDashboard.BranchIds) if (await actor.AllowedAsync(branch, ct)) lossAuthority.Add(branch);
        losses = await ReportTransferLosses.ReadAsync(db, new ReportQuery
            { BranchId = query.BranchId, DepartmentId = query.DepartmentId, From = filters.From, To = filters.To }, filters, lossAuthority.ToArray(), ct);
        if (losses.MissingEvidence) missing.Add("KPI-13: some transfer losses lack proven event branches/receipts or both dashboard branch scopes; totals are incomplete.");
        quantities.AddRange(losses.Rows.GroupBy(r => new { r.ItemId, r.UomId }).Select(g =>
            new QuantityRow("transferLosses", g.Key.ItemId, g.Key.UomId, null, g.Sum(r => r.DamagedQty + r.MissingQty))));
        metrics.Add(new("transferLosses", "Transfer loss evidence", losses.MissingEvidence ? null : losses.Rows.Count(r => r.DamagedQty + r.MissingQty > 0),
            "receipt lines", "KPI-13 quantities remain separate by item and unit below; both branch scopes required"));
        quantities.AddRange(await movements.Where(m => m.SignedBaseQty > 0).GroupBy(m => new { m.ItemId, m.Item.CategoryId, m.Item.BaseUomId, m.WarehouseId })
            .Select(g => new QuantityRow("stockReceived", g.Key.ItemId, g.Key.BaseUomId, g.Key.WarehouseId, g.Sum(m => m.SignedBaseQty)) { CategoryId = g.Key.CategoryId }).ToArrayAsync(ct));
        quantities.AddRange(await movements.Where(m => m.SignedBaseQty < 0).GroupBy(m => new { m.ItemId, m.Item.CategoryId, m.Item.BaseUomId, m.WarehouseId })
            .Select(g => new QuantityRow("stockIssued", g.Key.ItemId, g.Key.BaseUomId, g.Key.WarehouseId, -g.Sum(m => m.SignedBaseQty)) { CategoryId = g.Key.CategoryId }).ToArrayAsync(ct));
        categoryQuantities.AddRange(await movements.GroupBy(m => new { m.Item.CategoryId, m.Item.BaseUomId, m.WarehouseId })
            .Select(g => new CategoryQuantityRow(g.Key.CategoryId, g.Key.BaseUomId, g.Key.WarehouseId,
                g.Sum(m => m.SignedBaseQty > 0 ? m.SignedBaseQty : 0),
                g.Sum(m => m.SignedBaseQty < 0 ? -m.SignedBaseQty : 0))).ToArrayAsync(ct));
        groups.AddRange(await movements.GroupBy(m => new { m.Item.CategoryId, m.Item.BaseUomId })
            .Select(g => new DistributionRow("stock_movements.category", g.Key.CategoryId.ToString(), g.LongCount(), g.Key.BaseUomId.ToString())).ToArrayAsync(ct));
        }
        catch (Exception error) when (SourceUnavailable(error))
        {
            missing.Add("UC134: an inventory/material source could not be read; independent figures remain available.");
            if (!metrics.Any(m => m.Key == "lowStockItems")) metrics.Add(new("lowStockItems", "Items below minimum", null, "unavailable", "Source unavailable"));
            if (!metrics.Any(m => m.Key == "transferLosses")) metrics.Add(new("transferLosses", "Transfer losses", null, "unavailable", "Source unavailable"));
        }
        var final = await access.FiltersAsync("DASHBOARD_VIEW", query, clock.GetUtcNow(), ct);
        foreach (var branch in filters.BranchIds)
            if (!final.BranchIds.Contains(branch) || !await actor.AllowedAsync(branch, ct))
                throw new InsightException(403, "DASHBOARD_SCOPE_CHANGED", "Dashboard authority changed.");
        if (filters.BranchIds.Length == 0 && !await actor.AllowedAsync(null, ct))
            throw new InsightException(403, "DASHBOARD_SCOPE_CHANGED", "Dashboard authority changed.");
        foreach (var branch in losses.Rows.SelectMany(r => new[] { r.SourceBranchId, r.DestinationBranchId }).Distinct())
        {
            await access.FiltersAsync("DASHBOARD_VIEW", new() { BranchId = branch }, clock.GetUtcNow(), ct);
            if (!await actor.AllowedAsync(branch, ct)) throw new InsightException(403, "DASHBOARD_SCOPE_CHANGED", "Transfer scope changed.");
        }
        await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return new(now, filters, metrics, groups.OrderBy(g => g.Source).ThenBy(g => g.Status).ThenBy(g => g.Severity).ToArray(),
            quantities.OrderBy(q => q.Kind).ThenBy(q => q.WarehouseId).ThenBy(q => q.ItemId).ThenBy(q => q.UomId).ToArray(), missing)
            { CategoryQuantities = categoryQuantities.OrderBy(q => q.CategoryId).ThenBy(q => q.WarehouseId).ThenBy(q => q.UomId).ToArray() };
    }
    private static bool SourceUnavailable(Exception error) => error is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 207 or 208 or 229 or 2812
        || error is InsightException insight && insight.Code == "REPORT_ROW_LIMIT";
}
