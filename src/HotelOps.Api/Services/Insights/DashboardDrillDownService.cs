using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class DashboardDrillQuery : InsightQuery
{
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [StringLength(60)] public string? Metric { get; set; }
}
public sealed record DashboardDrillResult(DateTimeOffset Snapshot, InsightFilters Filters, string Source,
    int Page, int PageSize, int TotalCount, ReportSection Records, string[] MissingSources);

// Separate read-only population endpoint. Its authorization never authorizes
// the module's mutation endpoints or substitutes for their resource guards.
public sealed class DashboardDrillDownService(HotelOpsDbContext db, InsightAccess access,
    DashboardSessionGuard session, TimeProvider clock)
{
    public async Task<DashboardDrillResult> ReadAsync(string source, DashboardDrillQuery query, CancellationToken ct)
    {
        var permission = source switch
        {
            "incidents" => "INCIDENT_VIEW", "occurrences" => "MAINTENANCE_VIEW",
            "stock" or "transfers" => "INVENTORY_VIEW", "requests" => "MR_VIEW", "replenishment" => "REPLENISHMENT_VIEW",
            _ => throw new InsightException(404, "SOURCE_NOT_FOUND", "Unknown drill-down source.")
        };
        Validator.ValidateObject(query, new ValidationContext(query), true);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var now = clock.GetUtcNow();
        var filters = await access.FiltersAsync("DASHBOARD_VIEW", query, now, ct);
        var actor = new TaskDashboardAccess(db, access, clock);
        var dependencies = new HashSet<long>();
        // An aggregate population requires source BRANCH/ALL scope. OWN alone
        // does not authorize a branch-wide list; its module reader remains separate.
        async Task Admit()
        {
            var dashboard = await access.FiltersAsync("DASHBOARD_VIEW", new() { From = filters.From, To = filters.To }, clock.GetUtcNow(), ct);
            var sourceScope = await access.BranchesAsync(permission, null, ct);
            foreach (var branch in filters.BranchIds.Concat(dependencies).Distinct())
                if (!dashboard.BranchIds.Contains(branch) || !sourceScope.Contains(branch) || !await actor.AllowedAsync(branch, ct))
                    throw new InsightException(403, "ACCESS_DENIED", "Dashboard and source read access are both required for this population.");
            if (filters.BranchIds.Length == 0 && !await actor.AllowedAsync(null, ct))
                throw new InsightException(403, "ACCESS_DENIED", "Dashboard actor unavailable.");
        }
        if (filters.DepartmentId is {} department)
            filters = filters with { BranchIds = await db.Departments.AsNoTracking().Where(d => d.DepartmentId == department
                && filters.BranchIds.Contains(d.BranchId)).Select(d => d.BranchId).ToArrayAsync(ct) };
        await Admit();
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new InsightUtcJson()); options.Converters.Add(new InsightDecimalJson());
        ReportSection Section<T>(IEnumerable<T> rows, string[] columns) => new(source, columns, rows.Select(row =>
            JsonSerializer.SerializeToElement(row, options).EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())).ToArray());
        async Task<(int, ReportSection)> Page<T>(IQueryable<T> rows, string[] columns)
        {
            var count = await rows.CountAsync(ct);
            return (count, Section(await rows.Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize).ToArrayAsync(ct), columns));
        }
        var branches = filters.BranchIds;
        var missing = new List<string>();
        (int Total, ReportSection Data) result;
        switch (source)
        {
            case "incidents":
                var incident = db.Incidents.AsNoTracking().Where(i => branches.Contains(i.BranchId)
                    && (i.RoomId == null || i.Room!.BranchId == i.BranchId)
                    && (i.AreaId == null || i.Area!.BranchId == i.BranchId)
                    && (i.AssetId == null || i.Asset!.BranchId == i.BranchId));
                if (filters.DepartmentId != null)
                {
                    var assignments = IncidentCurrentAssignment.Query(db);
                    incident = incident.Where(i => assignments.Any(w => w.IncidentId == i.IncidentId && w.DepartmentId == filters.DepartmentId));
                }
                if (query.Metric is "responseHours" or "resolutionHours" or "slaCompliance" or "reopenedIncidents")
                {
                    var cycles = await ReportIncidentCycles.ReadAsync(db, incident, now.UtcDateTime, ct);
                    var start = InsightAccess.Start(filters); var end = InsightAccess.End(filters);
                    var currentCycles = cycles.GroupBy(c => c.IncidentId).SelectMany(g => g.Where(c =>
                        (c.ReopenEventId ?? 0) == g.Max(r => r.ReopenEventId ?? 0)));
                    IEnumerable<ReportIncidentCycles.Row> selectedCycles;
                    if (query.Metric == "responseHours") selectedCycles = currentCycles.Where(c => c.AcceptedAt >= start && c.AcceptedAt < end && c.ResponseSeconds != null);
                    else if (query.Metric == "resolutionHours") selectedCycles = currentCycles.Where(c => c.ResolvedAt >= start && c.ResolvedAt < end && c.ResolutionSeconds != null);
                    else if (query.Metric == "slaCompliance") selectedCycles = cycles.Where(c => c.ResolvedAt >= start && c.ResolvedAt < end);
                    else
                    {
                        var incidentIds = incident.Select(i => i.IncidentId);
                        var events = await db.IncidentEvents.AsNoTracking().Where(e => incidentIds.Contains(e.IncidentId)
                            && e.EventType == "REOPENED" && e.OccurredAt >= start && e.OccurredAt < end && e.OccurredAt <= now.UtcDateTime)
                            .Select(e => e.EventId).ToArrayAsync(ct);
                        selectedCycles = cycles.Where(c => c.ReopenEventId != null && events.Contains(c.ReopenEventId.Value));
                    }
                    var population = selectedCycles.ToArray();
                    result = (population.Length, Section(population.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
                        ["incidentId", "workId", "reopenEventId", "discoveredAt", "acceptedAt", "resolvedAt", "responseSeconds",
                            "resolutionSeconds", "resolutionDeadlineAt", "withinResolutionSla", "missingReason"]));
                    break;
                }
                if (query.Metric == "openIncidents") incident = incident.Where(i => i.Status != "CLOSED" && i.Status != "CANCELLED");
                if (query.Metric == "incidentBlockedRooms") incident = incident.Where(i => db.RoomBlocks.Any(b => b.IncidentId == i.IncidentId
                    && b.Room.BranchId == i.BranchId && b.Room.IsActive && b.BlockedAt <= now.UtcDateTime
                    && (b.ReleasedAt == null || b.ReleasedAt > now.UtcDateTime)));
                result = await Page(incident.OrderBy(i => i.IncidentId).Select(i => new { i.IncidentId, i.IncidentNo,
                    i.BranchId, i.Status, i.SeverityId, i.DiscoveredAt, i.ResolvedAt, i.ClosedAt }),
                    ["incidentId", "incidentNo", "branchId", "status", "severityId", "discoveredAt", "resolvedAt", "closedAt"]);
                break;
            case "occurrences":
                var occurrences = await ReportPreventiveOccurrences.ReadAsync(db, filters, now.UtcDateTime, ct);
                if (query.Metric == "preventiveOverdue") occurrences = occurrences.Where(o => o.Overdue == true).ToArray();
                result = (occurrences.Length, Section(occurrences.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
                    ["runId", "scheduleId", "branchId", "departmentId", "workflowStatus", "dueAt", "performedAt", "overdue", "missingReason"]));
                break;
            case "stock":
                if (query.Metric is "stockReceived" or "stockIssued" or "netStockMovement")
                {
                    var start = InsightAccess.Start(filters); var end = InsightAccess.End(filters);
                    var movements = db.StockMovements.AsNoTracking().Where(m => branches.Contains(m.Warehouse.BranchId)
                        && (filters.DepartmentId == null || m.Warehouse.DepartmentId == filters.DepartmentId)
                        && (m.Warehouse.DepartmentId == null || m.Warehouse.Department!.BranchId == m.Warehouse.BranchId)
                        && m.OccurredAt >= start && m.OccurredAt < end);
                    if (query.Metric == "stockReceived") movements = movements.Where(m => m.SignedBaseQty > 0);
                    if (query.Metric == "stockIssued") movements = movements.Where(m => m.SignedBaseQty < 0);
                    result = await Page(movements.OrderBy(m => m.MovementId).Select(m => new { m.MovementId,
                        m.WarehouseId, m.ItemId, m.Item.CategoryId, m.Item.BaseUomId, m.DocumentLineId, m.SignedBaseQty, m.OccurredAt }),
                        ["movementId", "warehouseId", "itemId", "categoryId", "baseUomId", "documentLineId", "signedBaseQty", "occurredAt"]);
                    break;
                }
                result = await Page(db.StockThresholds.AsNoTracking().Where(t => branches.Contains(t.Warehouse.BranchId)
                    && (t.Warehouse.DepartmentId == null || t.Warehouse.Department!.BranchId == t.Warehouse.BranchId)
                    && (filters.DepartmentId == null || t.Warehouse.DepartmentId == filters.DepartmentId)
                    && (query.Metric != "lowStockItems" || (db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId)
                        .Sum(m => (decimal?)m.SignedBaseQty) ?? 0) < t.MinimumQty))
                    .OrderBy(t => t.WarehouseId).ThenBy(t => t.ItemId).Select(t => new { t.WarehouseId, t.ItemId, t.Item.ItemName,
                        t.Item.BaseUomId, t.MinimumQty, Available = db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId)
                            .Sum(m => (decimal?)m.SignedBaseQty) ?? 0 }),
                    ["warehouseId", "itemId", "itemName", "baseUomId", "minimumQty", "available"]);
                break;
            case "transfers":
                var dashboardScope = await access.FiltersAsync("DASHBOARD_VIEW", new() { From = filters.From, To = filters.To }, now, ct);
                var readScope = await access.BranchesAsync(permission, null, ct);
                var allowed = new List<long>();
                foreach (var branch in dashboardScope.BranchIds.Intersect(readScope))
                    if (await actor.AllowedAsync(branch, ct)) allowed.Add(branch);
                var losses = await ReportTransferLosses.ReadAsync(db, new() { BranchId = query.BranchId, DepartmentId = filters.DepartmentId,
                    From = filters.From, To = filters.To }, filters, allowed.ToArray(), ct);
                if (losses.MissingEvidence) missing.Add("Some transfers are withheld: event evidence or read access at both branches is unavailable. This is not a complete transfer population.");
                foreach (var row in losses.Rows) { dependencies.Add(row.SourceBranchId); dependencies.Add(row.DestinationBranchId); }
                result = (losses.Rows.Length, Section(losses.Rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
                    ["receiptLineId", "receiptId", "documentId", "itemId", "uomId", "sourceWarehouseId", "destinationWarehouseId",
                        "sourceBranchId", "destinationBranchId", "receivedAt", "receivedQty", "damagedQty", "missingQty"]));
                break;
            case "requests":
                result = await Page(db.MaterialRequests.AsNoTracking().Where(r => branches.Contains(r.BranchId)
                    && r.Department.BranchId == r.BranchId && (filters.DepartmentId == null || r.DepartmentId == filters.DepartmentId)
                    && (r.Status == "APPROVED" || r.Status == "PARTIALLY_ISSUED"))
                    .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.RequestId).Select(r => new { r.RequestId, r.RequestNo,
                        r.BranchId, r.DepartmentId, r.Status, r.CreatedAt, r.CompletedAt }),
                    ["requestId", "requestNo", "branchId", "departmentId", "status", "createdAt", "completedAt"]);
                break;
            default:
                result = await Page(db.MaterialReplenishments.AsNoTracking().Where(r => branches.Contains(r.Warehouse.BranchId)
                    && (r.Warehouse.DepartmentId == null || r.Warehouse.Department!.BranchId == r.Warehouse.BranchId)
                    && (filters.DepartmentId == null || r.Warehouse.DepartmentId == filters.DepartmentId)
                    && r.MaterialReplenishmentLines.Any(l => l.IsOpenCoverage && l.WarehouseId == r.WarehouseId))
                    .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.ReplenishmentId).Select(r => new { r.ReplenishmentId,
                        r.ReplenishmentNo, r.WarehouseId, r.Status, r.CreatedAt, r.ExpectedOn, r.ClosedAt }),
                    ["replenishmentId", "replenishmentNo", "warehouseId", "status", "createdAt", "expectedOn", "closedAt"]);
                break;
        }
        await Admit(); await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return new(now, filters, source, query.Page, query.PageSize, result.Total, result.Data, missing.ToArray());
    }
}
