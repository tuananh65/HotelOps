using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class ReportReadService(HotelOpsDbContext db, InsightAccess access, TimeProvider clock, ReportSnapshotStore snapshots)
{
    public static readonly string[] Keys = ["rooms", "inspections", "incidents", "assets", "items", "tasks", "requests"];

    public async Task<ReportResult> ReadAsync(string key, ReportQuery query, CancellationToken ct)
    {
        if (!Keys.Contains(key)) throw new InsightException(404, "REPORT_NOT_FOUND", "Unknown report type.");
        ValidateFilters(key, query);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var now = clock.GetUtcNow();
        var filters = await access.FiltersAsync("REPORT_VIEW", query, now, ct);
        var branches = filters.BranchIds;
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        var department = filters.DepartmentId;
        var sections = new List<ReportSection>();
        var missing = new List<string> { "PROVISIONAL: RPT content/field approval and export authorization contract pending" };
        var rowCount = 0;
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        jsonOptions.Converters.Add(new InsightUtcJson());
        jsonOptions.Converters.Add(new InsightDecimalJson());
        async Task Add<T>(string source, IQueryable<T> rows, params string[] columns)
        {
            var data = await rows.Take(50001 - rowCount).ToArrayAsync(ct);
            rowCount += data.Length;
            if (rowCount > 50000) throw new InsightException(422, "REPORT_ROW_LIMIT", "Report exceeds 50,000 rows. Narrow the filters; no data was truncated.");
            var values = data.Select(row =>
            {
                var json = JsonSerializer.SerializeToElement(row, jsonOptions);
                return json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
            }).ToArray();
            sections.Add(new(source, columns, values));
        }

        switch (key)
        {
            case "rooms":
            {
                if (department is not null) throw new InsightException(400, "ROOM_DEPARTMENT_UNAVAILABLE", "Rooms have no approved department relationship.");
                var rooms = db.Rooms.AsNoTracking().Where(r => branches.Contains(r.BranchId)
                    && (query.FloorId == null || r.FloorId == query.FloorId) && (query.RoomTypeId == null || r.RoomTypeId == query.RoomTypeId));
                await Add("rooms", rooms.OrderBy(r => r.RoomId).Select(r => new { Id = r.RoomId, r.BranchId,
                    r.RoomNumber, r.FloorId, r.RoomTypeId, r.OperationalStatus, r.IsActive }),
                    "id", "branchId", "roomNumber", "floorId", "roomTypeId", "operationalStatus", "isActive");
                var ids = rooms.Select(r => r.RoomId);
                await Add("cleaning_tasks", db.CleaningTasks.AsNoTracking().Where(t => ids.Contains(t.RoomId)
                    && t.CreatedAt >= start && t.CreatedAt < end).OrderBy(t => t.CleaningTaskId)
                    .Select(t => new { Id = t.CleaningTaskId, t.TaskNo, t.RoomId, t.Status, t.CreatedAt, t.ActualStartAt, t.ActualEndAt, t.DueAt }),
                    "id", "taskNo", "roomId", "status", "createdAt", "actualStartAt", "actualEndAt", "dueAt");
                await Add("room_blocks", db.RoomBlocks.AsNoTracking().Where(b => ids.Contains(b.RoomId)
                    && b.BlockedAt < end && (b.ReleasedAt == null || b.ReleasedAt >= start)).OrderBy(b => b.BlockId)
                    .Select(b => new { Id = b.BlockId, b.RoomId, b.BlockedAt, b.ReleasedAt }), "id", "roomId", "blockedAt", "releasedAt");
                missing.Add("DEV1: stored status distribution is not translated to pending/rework SRS lifecycle states");
                break;
            }
            case "inspections":
            {
                if (department is not null) throw new InsightException(400, "INSPECTION_DEPARTMENT_UNAVAILABLE", "Inspections have no approved department relationship.");
                var inspections = db.RoomInspections.AsNoTracking().Where(i => branches.Contains(i.Room.BranchId)
                    && i.InspectedAt >= start && i.InspectedAt < end && (query.InspectorId == null || i.InspectorUserId == query.InspectorId));
                // Housekeeper membership in historical assignments is not a proven cycle actor.
                if (query.HousekeeperId is not null) throw new InsightException(503, "HOUSEKEEPER_CYCLE_UNAPPROVED", "Housekeeper-to-inspection cycle contract unavailable.");
                await Add("room_inspections", inspections.OrderBy(i => i.InspectionId).Select(i => new { Id = i.InspectionId,
                    i.RoomId, i.CleaningTaskId, i.InspectorUserId, i.Status, i.Result, i.InspectedAt }),
                    "id", "roomId", "cleaningTaskId", "inspectorUserId", "status", "result", "inspectedAt");
                var ids = inspections.Select(i => i.InspectionId);
                await Add("inspection_defects", db.InspectionDefects.AsNoTracking().Where(d => ids.Contains(d.InspectionId))
                    .OrderBy(d => d.DefectId).Select(d => new { Id = d.DefectId, d.InspectionId, d.ChecklistItemId, d.SeverityId, d.Status }),
                    "id", "inspectionId", "checklistItemId", "severityId", "status");
                missing.Add("DEV1: first inspection/cycle, housekeeper attribution and rework-round contract unavailable");
                break;
            }
            case "incidents":
            {
                if (department is not null || query.CategoryId is not null)
                    throw new InsightException(503, "INCIDENT_FILTER_CONTRACT_UNAVAILABLE", "Incident department/category source contract unavailable.");
                var incidents = db.Incidents.AsNoTracking().Where(i => branches.Contains(i.BranchId)
                    && i.ReportedAt >= start && i.ReportedAt < end && (query.SeverityId == null || i.SeverityId == query.SeverityId));
                await Add("incidents", incidents.OrderBy(i => i.IncidentId).Select(i => new { Id = i.IncidentId,
                    i.IncidentNo, i.BranchId, i.RoomId, i.AssetId, i.SeverityId, i.Status, i.DiscoveredAt, i.ReportedAt, i.ResolvedAt, i.ClosedAt }),
                    "id", "incidentNo", "branchId", "roomId", "assetId", "severityId", "status", "discoveredAt", "reportedAt", "resolvedAt", "closedAt");
                var incidentIds = incidents.Select(i => i.IncidentId);
                var works = MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking())
                    .Where(s => s.BranchId != null && branches.Contains(s.BranchId.Value) && s.Work.IncidentId != null
                        && incidentIds.Contains(s.Work.IncidentId.Value)).Select(s => s.Work);
                await Add("maintenance_works", works.OrderBy(w => w.MaintenanceWorkId).Select(w => new { Id = w.MaintenanceWorkId,
                    w.WorkNo, w.IncidentId, w.DepartmentId, w.AssigneeId, w.Status, w.ActualStartAt, w.ResolvedAt }),
                    "id", "workNo", "incidentId", "departmentId", "assigneeId", "status", "actualStartAt", "resolvedAt");
                var workIds = works.Select(w => w.MaintenanceWorkId);
                await Add("maintenance_material_usages", db.MaintenanceMaterialUsages.AsNoTracking()
                    .Where(u => workIds.Contains(u.MaintenanceWorkId)).OrderBy(u => u.UsageId)
                    .Select(u => new { Id = u.UsageId, u.MaintenanceWorkId, u.ItemId, u.BaseUomId, u.UsedBaseQty }),
                    "id", "maintenanceWorkId", "itemId", "baseUomId", "usedBaseQty");
                missing.Add("Current-cycle accepted/SLA/reopened semantics and PM occurrence lifecycle are not approved; elapsed values not invented");
                break;
            }
            case "assets":
            {
                var assets = db.Assets.AsNoTracking().Where(a => branches.Contains(a.BranchId)
                    && (department == null || a.CurrentDepartmentId == department)
                    && (query.CategoryId == null || a.AssetTypeId == query.CategoryId)
                    && (query.Condition == null || a.ConditionStatus == query.Condition));
                await Add("assets", assets.OrderBy(a => a.AssetId).Select(a => new { Id = a.AssetId, a.AssetCode,
                    a.AssetName, a.BranchId, a.AssetTypeId, a.CurrentRoomId, a.CurrentAreaId, a.CurrentDepartmentId,
                    a.CustodianUserId, a.ConditionStatus, a.LifecycleStatus }),
                    "id", "assetCode", "assetName", "branchId", "assetTypeId", "currentRoomId", "currentAreaId", "currentDepartmentId", "custodianUserId", "conditionStatus", "lifecycleStatus");
                missing.Add("Historical movement/handover branch provenance is not recorded on those rows; cross-branch history withheld pending approved access contract");
                break;
            }
            case "items":
            {
                var movements = db.StockMovements.AsNoTracking().Where(m => branches.Contains(m.Warehouse.BranchId)
                    && (department == null || m.Warehouse.DepartmentId == department)
                    && (query.WarehouseId == null || m.WarehouseId == query.WarehouseId) && (query.ItemId == null || m.ItemId == query.ItemId));
                await Add("stock_balances", movements.GroupBy(m => new { m.WarehouseId, m.ItemId, m.Item.BaseUomId })
                    .OrderBy(g => g.Key.WarehouseId).ThenBy(g => g.Key.ItemId)
                    .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, g.Key.BaseUomId, OnHand = g.Sum(m => m.SignedBaseQty) }),
                    "warehouseId", "itemId", "baseUomId", "onHand");
                await Add("stock_movements", movements.Where(m => m.OccurredAt >= start && m.OccurredAt < end).OrderBy(m => m.MovementId)
                    .Select(m => new { Id = m.MovementId, m.WarehouseId, m.ItemId, m.Item.BaseUomId, m.DocumentLineId, m.SignedBaseQty, m.OccurredAt }),
                    "id", "warehouseId", "itemId", "baseUomId", "documentLineId", "signedBaseQty", "occurredAt");
                var thresholds = db.StockThresholds.AsNoTracking().Where(t => branches.Contains(t.Warehouse.BranchId)
                    && (department == null || t.Warehouse.DepartmentId == department)
                    && (query.WarehouseId == null || t.WarehouseId == query.WarehouseId) && (query.ItemId == null || t.ItemId == query.ItemId));
                await Add("low_stock", thresholds.Where(t => (db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId)
                        .Sum(m => (decimal?)m.SignedBaseQty) ?? 0) < t.MinimumQty).OrderBy(t => t.WarehouseId).ThenBy(t => t.ItemId)
                    .Select(t => new { t.WarehouseId, t.ItemId, t.MinimumQty, Available = db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId).Sum(m => (decimal?)m.SignedBaseQty) ?? 0 }),
                    "warehouseId", "itemId", "minimumQty", "available");
                missing.Add("Department/work attribution of issues/returns and cross-branch transfer-loss export contract pending");
                break;
            }
            case "tasks":
            {
                var tasks = db.OperationalTasks.AsNoTracking().Where(t => branches.Contains(t.BranchId)
                    && (department == null || t.DepartmentId == department) && t.CreatedAt >= start && t.CreatedAt < end);
                if (query.EmployeeId is not null) throw new InsightException(503, "TASK_EMPLOYEE_CONTRACT_UNAVAILABLE", "Employee-to-task/interval attribution contract unavailable.");
                await Add("operational_tasks", tasks.OrderBy(t => t.TaskId).Select(t => new { Id = t.TaskId,
                    t.TaskCode, t.BranchId, t.DepartmentId, t.Status, t.PriorityId, t.ProgressPercent, t.CreatedAt, t.DueAt, t.ActualStartAt, t.CompletedAt }),
                    "id", "taskCode", "branchId", "departmentId", "status", "priorityId", "progressPercent", "createdAt", "dueAt", "actualStartAt", "completedAt");
                missing.Add("DEV1/DEV3: canonical task CLOSED/overdue semantics, WorkInterval and shift-effort source unavailable");
                break;
            }
            case "requests":
            {
                var requests = db.InternalRequests.AsNoTracking().Where(r => branches.Contains(r.BranchId)
                    && (department == null || r.ReceivingDepartmentId == department) && r.CreatedAt >= start && r.CreatedAt < end
                    && (query.Type == null || r.RequestType.TypeCode == query.Type));
                if (department is not null) throw new InsightException(503, "REQUEST_DEPARTMENT_CONTRACT_UNAVAILABLE", "Receiving versus requester department reporting contract unavailable.");
                await Add("internal_requests", requests.OrderBy(r => r.InternalRequestId).Select(r => new { Id = r.InternalRequestId,
                    r.RequestNo, r.BranchId, r.RequestTypeId, r.RequesterDepartmentId, r.ReceivingDepartmentId, r.Status,
                    r.CreatedAt, r.SubmittedAt, r.DueAt, r.CompletedAt, r.ClosedAt }),
                    "id", "requestNo", "branchId", "requestTypeId", "requesterDepartmentId", "receivingDepartmentId", "status", "createdAt", "submittedAt", "dueAt", "completedAt", "closedAt");
                missing.Add("DEV1: response/current-cycle completion, reopened and overdue contracts unavailable");
                break;
            }
        }
        await tx.CommitAsync(ct);
        var first = sections[0];
        var appliedFilters = JsonSerializer.SerializeToElement(query, jsonOptions).EnumerateObject()
            .Where(p => p.Value.ValueKind != JsonValueKind.Null).ToDictionary(p => p.Name, p => (string?)p.Value.ToString());
        appliedFilters["from"] = filters.From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        appliedFilters["to"] = filters.To.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var report = new ReportResult(Guid.NewGuid().ToString("N"), key, now, filters, first.Columns, first.Rows,
            rowCount, sections, missing, [], appliedFilters);
        snapshots.Put(access.UserId, report);
        return report;
    }

    public async Task<ExportFile> ExportAsync(string snapshotId, ExportRequest request, CancellationToken ct)
    {
        if (request.Format is not ("CSV" or "XLSX" or "PDF"))
            throw new InsightException(400, "INVALID_FORMAT", "Format must be CSV, XLSX or PDF.");
        await access.BranchesAsync("REPORT_VIEW", null, ct);
        var report = snapshots.Get(access.UserId, snapshotId);
        foreach (var branch in report.Filters.BranchIds)
            await access.BranchesAsync("REPORT_VIEW", branch, ct);
        // REPORT_VIEW authorizes preview. C21 does not yet bind export/resource
        // permissions or an audit action contract. Never mint an export grant.
        throw new InsightException(503, "REPORT_EXPORT_CONTRACT_UNAPPROVED",
            "Report export authorization, permitted fields and audit contract are not approved. Nothing was downloaded.");
    }

    public static void ValidateFilters(string key, ReportQuery query)
    {
        var filters = new Dictionary<string, bool>
        {
            ["floor"] = query.FloorId != null, ["roomType"] = query.RoomTypeId != null,
            ["inspector"] = query.InspectorId != null, ["housekeeper"] = query.HousekeeperId != null,
            ["employee"] = query.EmployeeId != null, ["warehouse"] = query.WarehouseId != null,
            ["item"] = query.ItemId != null, ["severity"] = query.SeverityId != null,
            ["category"] = query.CategoryId != null, ["condition"] = query.Condition != null, ["type"] = query.Type != null
        };
        string[] allowed = key switch
        {
            "rooms" => ["floor", "roomType"], "inspections" => ["inspector", "housekeeper"],
            "incidents" => ["severity", "category"], "assets" => ["category", "condition"],
            "items" => ["warehouse", "item"], "tasks" => ["employee"], "requests" => ["type"], _ => []
        };
        if (filters.Any(f => f.Value && !allowed.Contains(f.Key)))
            throw new InsightException(400, "UNSUPPORTED_FILTER", "Filter is not supported for this report type.");
    }
}
