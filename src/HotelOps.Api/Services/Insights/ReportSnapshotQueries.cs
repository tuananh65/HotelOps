using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

// Query component only. The caller must authorize the exact filters and retain a
// SERIALIZABLE transaction across authorization, materialization and final admission.
// ReportService owns admission under the approved UC135 actor restriction.
public sealed class ReportSnapshotQueries(HotelOpsDbContext db)
{
    public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(new[] { "rooms", "inspections", "incidents", "assets", "items", "tasks", "requests" });

    public async Task<ReportResult> MaterializeAsync(string key, ReportQuery query,
        InsightFilters filters, DateTimeOffset now, CancellationToken ct, long[]? authorizedBranches = null)
    {
        if (!Keys.Contains(key)) throw new InsightException(404, "REPORT_NOT_FOUND", "Unknown report type.");
        ValidateFilters(key, query);
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable)
            throw new InvalidOperationException("Report materialization requires the caller's SERIALIZABLE transaction.");
        if (filters.From > filters.To || filters.To == DateOnly.MaxValue || filters.Calendar != "UTC"
            || filters.BranchIds.Any(id => id <= 0) || filters.BranchIds.Distinct().Count() != filters.BranchIds.Length
            || query.BranchId is {} branch && !filters.BranchIds.SequenceEqual(new[] { branch })
            || query.DepartmentId != filters.DepartmentId
            || query.From is {} from && from != filters.From || query.To is {} to && to != filters.To)
            throw new InsightException(400, "INVALID_FILTER_SCOPE", "Resolved filters do not match the authorized query.");
        var branches = filters.BranchIds;
        authorizedBranches ??= branches;
        var requiredBranches = new HashSet<long>(branches);
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        var department = filters.DepartmentId;
        var sections = new List<ReportSection>();
        var missing = new List<string>();
        var rowCount = 0;
        var rowLimit = await ReportLimits.RowsAsync(db, ct);
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        jsonOptions.Converters.Add(new InsightUtcJson());
        jsonOptions.Converters.Add(new InsightDecimalJson());
        async Task Add<T>(string source, IQueryable<T> rows, params string[] columns)
        {
            var data = await rows.Take(rowLimit + 1 - rowCount).ToArrayAsync(ct);
            AddMaterialized(source, data, columns);
        }
        void AddMaterialized<T>(string source, T[] data, params string[] columns)
        {
            rowCount += data.Length;
            if (rowCount > rowLimit) throw new InsightException(422, "REPORT_ROW_LIMIT", $"Report exceeds {rowLimit} rows. Narrow the filters; no data was truncated.");
            var values = data.Select(row =>
            {
                var json = JsonSerializer.SerializeToElement(row, jsonOptions);
                return json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
            }).ToArray();
            sections.Add(new(source, columns, values));
        }

        async Task AddHousekeepingState(bool rework)
        {
            var section = sections[^1];
            var states = await ReportHousekeepingStates.ReadAsync(db, rework,
                section.Rows.Select(r => ((JsonElement)r["id"]!).GetInt64()).ToArray(), ct);
            foreach (var row in section.Rows)
                row["workflowState"] = JsonSerializer.SerializeToElement(states.GetValueOrDefault(((JsonElement)row["id"]!).GetInt64()));
            sections[^1] = section with { Columns = [.. section.Columns, "workflowState"] };
            if (states.Values.Any(v => v == null)) missing.Add("Some housekeeping workflow states are unavailable or inconsistent; physical status is not substituted.");
        }

        switch (key)
        {
            case "rooms":
            {
                if (department is not null) throw new InsightException(400, "ROOM_DEPARTMENT_UNAVAILABLE", "Department is not a filter for the room report; use the room dashboard for mapped department scope.");
                var rooms = db.Rooms.AsNoTracking().Where(r => branches.Contains(r.BranchId)
                    && r.Floor.BranchId == r.BranchId && (r.AreaId == null || r.Area!.BranchId == r.BranchId)
                    && (query.FloorId == null || r.FloorId == query.FloorId) && (query.RoomTypeId == null || r.RoomTypeId == query.RoomTypeId));
                await Add("rooms", rooms.OrderBy(r => r.RoomId).Select(r => new { Id = r.RoomId, r.BranchId,
                    r.RoomNumber, r.FloorId, r.RoomTypeId, r.OperationalStatus, r.IsActive }),
                    "id", "branchId", "roomNumber", "floorId", "roomTypeId", "operationalStatus", "isActive");
                // Preserve the recorded code for history, but use the same GBR-044
                // authority as the room board for any room-usability interpretation.
                var roomSection = sections[^1];
                var readiness = await new HotelOps.Api.Services.Rooms.EffectiveRoomReadiness(db).ReadAsync(
                    roomSection.Rows.Select(r => ((JsonElement)r["id"]!).GetInt64()).ToArray(), now.UtcDateTime, ct);
                foreach (var row in roomSection.Rows)
                {
                    var id = ((JsonElement)row["id"]!).GetInt64();
                    var state = readiness.GetValueOrDefault(id)?.Readiness ?? "UNKNOWN";
                    row["effectiveReadiness"] = JsonSerializer.SerializeToElement(state);
                    row["canUse"] = JsonSerializer.SerializeToElement(state == "READY");
                }
                sections[^1] = roomSection with { Columns = [.. roomSection.Columns, "effectiveReadiness", "canUse"] };
                var ids = rooms.Select(r => r.RoomId);
                await Add("cleaning_tasks", db.CleaningTasks.AsNoTracking().Where(t => ids.Contains(t.RoomId)
                    && t.CreatedAt >= start && t.CreatedAt < end).OrderBy(t => t.CleaningTaskId)
                    .Select(t => new { Id = t.CleaningTaskId, t.TaskNo, t.RoomId, t.Status, t.CreatedAt, t.ActualStartAt, t.ActualEndAt, t.DueAt,
                        DurationSeconds = t.ActualStartAt != null && t.ActualEndAt >= t.ActualStartAt
                            ? (long?)EF.Functions.DateDiffSecond(t.ActualStartAt, t.ActualEndAt) : null }),
                    "id", "taskNo", "roomId", "status", "createdAt", "actualStartAt", "actualEndAt", "dueAt", "durationSeconds");
                await AddHousekeepingState(false);
                await Add("room_blocks", db.RoomBlocks.AsNoTracking().Where(b => ids.Contains(b.RoomId)
                    && b.BlockedAt < end && (b.ReleasedAt == null || b.ReleasedAt >= start)).OrderBy(b => b.BlockId)
                    .Select(b => new { Id = b.BlockId, b.RoomId, b.BlockedAt, b.ReleasedAt, b.Reason, b.ExpectedReleaseAt }), "id", "roomId", "blockedAt", "releasedAt", "reason", "expectedReleaseAt");
                AddMaterialized("room_readiness_distribution", roomSection.Rows.GroupBy(r => ((JsonElement)r["effectiveReadiness"]!).GetString())
                    .OrderBy(g => g.Key).Select(g => new { EffectiveReadiness = g.Key, RoomCount = g.Count() }).ToArray(),
                    "effectiveReadiness", "roomCount");
                missing.Add("Room usability uses effectiveReadiness at the snapshot; UNKNOWN is not usable and exports do not authorize later room use");
                break;
            }
            case "inspections":
            {
                if (department is not null) throw new InsightException(400, "INSPECTION_DEPARTMENT_UNAVAILABLE", "Inspections have no approved department relationship.");
                var inspections = db.RoomInspections.AsNoTracking().Where(i => branches.Contains(i.Room.BranchId)
                    && i.Room.Floor.BranchId == i.Room.BranchId
                    && (i.Room.AreaId == null || i.Room.Area!.BranchId == i.Room.BranchId)
                    && (i.CleaningTaskId == null || i.CleaningTask!.RoomId == i.RoomId)
                    && i.InspectedAt >= start && i.InspectedAt < end && (query.InspectorId == null || i.InspectorUserId == query.InspectorId));
                if (query.HousekeeperId is {} housekeeper)
                {
                    if (!await HotelOps.Api.Services.HotelStructure.RoomTypeCreationService.CleanerAttributionInstalled(db, ct))
                        throw new InsightException(503, "HOUSEKEEPER_CYCLE_UNAPPROVED", "Official cleaner attribution is unavailable.");
                    var proof = HotelOps.Api.Services.HotelStructure.RoomTypeCreationService.HistoricalInspectionCleaners(db);
                    // Do not silently drop unknown historical attribution and present a complete report.
                    if (await inspections.AnyAsync(i => !proof.Any(p => p.InspectionId == i.InspectionId), ct))
                        throw new InsightException(503, "HOUSEKEEPER_HISTORY_INCOMPLETE", "Selected period contains inspections without verified cleaner attribution.");
                    inspections = inspections.Where(i => proof.Any(p => p.InspectionId == i.InspectionId && p.UserId == housekeeper));
                }
                await Add("room_inspections", inspections.OrderBy(i => i.InspectionId).Select(i => new { Id = i.InspectionId,
                    i.RoomId, i.CleaningTaskId, i.InspectorUserId, i.Status, i.Result, i.InspectedAt }),
                    "id", "roomId", "cleaningTaskId", "inspectorUserId", "status", "result", "inspectedAt");
                var ids = inspections.Select(i => i.InspectionId);
                await Add("inspection_defects", db.InspectionDefects.AsNoTracking().Where(d => ids.Contains(d.InspectionId))
                    .OrderBy(d => d.DefectId).Select(d => new { Id = d.DefectId, d.InspectionId, d.ChecklistItemId, d.SeverityId, d.Status }),
                    "id", "inspectionId", "checklistItemId", "severityId", "status");
                // Related records belong to the selected inspections, including later rework.
                // Never join a rework from a different cleaning task into this inspection's report.
                await Add("cleaning_reworks", db.CleaningReworks.AsNoTracking()
                    .Where(w => ids.Contains(w.InspectionId) && w.Inspection.CleaningTaskId == w.CleaningTaskId
                        && w.CleaningTask.RoomId == w.Inspection.RoomId)
                    .OrderBy(w => w.RequestedAt).ThenBy(w => w.ReworkId)
                    .Select(w => new { Id = w.ReworkId, w.InspectionId, w.CleaningTaskId, w.AssigneeUserId,
                        StoredStatus = w.Status, w.RequestedAt, w.CompletedAt }),
                    "id", "inspectionId", "cleaningTaskId", "assigneeUserId", "storedStatus", "requestedAt", "completedAt");
                await AddHousekeepingState(true);
                await Add("inspection_outcomes", inspections.GroupBy(i => new { i.Status, i.Result })
                    .OrderBy(g => g.Key.Status).ThenBy(g => g.Key.Result)
                    .Select(g => new { StoredStatus = g.Key.Status, g.Key.Result, InspectionCount = g.LongCount() }),
                    "storedStatus", "result", "inspectionCount");
                await Add("defects_by_criterion", db.InspectionDefects.AsNoTracking().Where(d => ids.Contains(d.InspectionId))
                    .GroupBy(d => new { d.ChecklistItemId, d.SeverityId, d.Status })
                    .OrderBy(g => g.Key.ChecklistItemId).ThenBy(g => g.Key.SeverityId).ThenBy(g => g.Key.Status)
                    .Select(g => new { g.Key.ChecklistItemId, g.Key.SeverityId, StoredStatus = g.Key.Status, DefectCount = g.LongCount() }),
                    "checklistItemId", "severityId", "storedStatus", "defectCount");
                await Add("rework_rounds", db.CleaningReworks.AsNoTracking()
                    .Where(w => ids.Contains(w.InspectionId) && w.Inspection.CleaningTaskId == w.CleaningTaskId
                        && w.CleaningTask.RoomId == w.Inspection.RoomId)
                    .GroupBy(w => w.CleaningTaskId).OrderBy(g => g.Key)
                    .Select(g => new { CleaningTaskId = g.Key, ReworkRounds = g.Count() }), "cleaningTaskId", "reworkRounds");
                if (await HotelOps.Api.Services.HotelStructure.RoomTypeCreationService.CleanerAttributionInstalled(db, ct))
                    await Add("inspection_cleaners", HotelOps.Api.Services.HotelStructure.RoomTypeCreationService.HistoricalInspectionCleaners(db)
                        .Where(p => ids.Contains(p.InspectionId)).OrderBy(p => p.InspectionId),
                        "inspectionId", "userId", "name", "assignmentId", "completedAt");
                missing.Add("Related rework rows follow the selected inspection period; storedStatus is not the rework workflow state");
                if (query.HousekeeperId is null)
                    missing.Add("Inspections without an official inspection_cleaners row have N/A cleaner attribution; current assignees are not substituted");
                break;
            }
            case "incidents":
            {
                if (department is not null)
                    throw new InsightException(503, "INCIDENT_FILTER_CONTRACT_UNAVAILABLE", "Incident department source contract unavailable.");
                var categoriesAvailable = await IncidentCategoryStore.InstalledAsync(db, ct);
                if (query.CategoryId is not null && !categoriesAvailable)
                    throw new InsightException(503, "INCIDENT_CATEGORY_STORAGE_UNAVAILABLE", "Incident category storage unavailable.");
                var incidents = db.Incidents.AsNoTracking().Where(i => branches.Contains(i.BranchId)
                    && (i.RoomId == null || i.Room!.BranchId == i.BranchId)
                    && (i.AreaId == null || i.Area!.BranchId == i.BranchId)
                    && (i.AssetId == null || i.Asset!.BranchId == i.BranchId)
                    && i.ReportedAt >= start && i.ReportedAt < end && (query.SeverityId == null || i.SeverityId == query.SeverityId));
                if (query.CategoryId is not null)
                {
                    var categorized = IncidentCategoryStore.Rows(db).Where(c => c.CategoryId == query.CategoryId).Select(c => c.IncidentId);
                    incidents = incidents.Where(i => categorized.Contains(i.IncidentId));
                }
                await Add("incidents", incidents.OrderBy(i => i.IncidentId).Select(i => new { Id = i.IncidentId,
                    i.IncidentNo, i.BranchId, i.RoomId, i.AssetId, i.SeverityId, i.Status, i.DiscoveredAt, i.ReportedAt, i.ResolvedAt, i.ClosedAt }),
                    "id", "incidentNo", "branchId", "roomId", "assetId", "severityId", "status", "discoveredAt", "reportedAt", "resolvedAt", "closedAt");
                var incidentIds = incidents.Select(i => i.IncidentId);
                if (categoriesAvailable)
                {
                    await Add("incident_categories", IncidentCategoryStore.Rows(db)
                        .Where(c => incidentIds.Contains(c.IncidentId)).OrderBy(c => c.IncidentId),
                        "incidentId", "categoryId", "categoryCode", "categoryName");
                    missing.Add("Incident category is N/A where no valid official category was recorded; historical values are not inferred.");
                }
                else missing.Add("Incident category unavailable: approved additive storage is not installed.");
                var works = MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking(),
                    await Assets.AssetTransferStorage.InstalledAsync(db, ct) ? db.AssetWorkSourceScopes.AsNoTracking() : null)
                    .Where(s => s.BranchId != null && branches.Contains(s.BranchId.Value) && s.Work.IncidentId != null
                        && (s.Work.DepartmentId == null || s.Work.Department!.BranchId == s.BranchId)
                        && incidentIds.Contains(s.Work.IncidentId.Value)).Select(s => s.Work);
                await Add("maintenance_works", works.OrderBy(w => w.MaintenanceWorkId).Select(w => new { Id = w.MaintenanceWorkId,
                    w.WorkNo, w.IncidentId, w.DepartmentId, w.AssigneeId, w.Status, w.ActualStartAt, w.ResolvedAt }),
                    "id", "workNo", "incidentId", "departmentId", "assigneeId", "status", "actualStartAt", "resolvedAt");
                var workIds = works.Select(w => w.MaintenanceWorkId);
                await Add("maintenance_material_usages", db.MaintenanceMaterialUsages.AsNoTracking()
                    .Where(u => workIds.Contains(u.MaintenanceWorkId)).OrderBy(u => u.UsageId)
                    .Select(u => new { Id = u.UsageId, u.MaintenanceWorkId, u.ItemId, u.BaseUomId, u.UsedBaseQty }),
                    "id", "maintenanceWorkId", "itemId", "baseUomId", "usedBaseQty");
                var cycles = await ReportIncidentCycles.ReadAsync(db, incidents, now.UtcDateTime, ct);
                AddMaterialized("incident_processing_cycles", cycles, "incidentId", "workId", "reopenEventId", "discoveredAt",
                    "acceptedAt", "resolvedAt", "responseDeadlineAt", "resolutionDeadlineAt", "responseSeconds", "resolutionSeconds",
                    "withinResolutionSla", "missingReason");
                if (cycles.Any(c => c.MissingReason != null))
                    missing.Add("Some Incident cycles lack proven timestamps/SLA; see missingReason per cycle. No planned dates or inferred durations are substituted.");
                if (query.CategoryId == null && query.SeverityId == null)
                {
                    var occurrences = await ReportPreventiveOccurrences.ReadAsync(db, filters, now.UtcDateTime, ct);
                    AddMaterialized("preventive_occurrences", occurrences, "runId", "scheduleId", "branchId", "departmentId", "workId",
                        "assetId", "roomId", "areaId", "assigneeId", "workflowStatus", "dueAt", "performedAt", "result", "overdue", "missingReason");
                    if (occurrences.Any(o => o.MissingReason != null)) missing.Add("Some preventive occurrence lifecycle evidence is unavailable; see per-row missingReason.");
                    var preventiveWork = occurrences.Where(o => o.WorkId != null).Select(o => o.WorkId!.Value).Distinct().ToArray();
                    await Add("preventive_material_usages", db.MaintenanceMaterialUsages.AsNoTracking()
                        .Where(u => preventiveWork.Contains(u.MaintenanceWorkId) && u.MaintenanceWork.IncidentId == null)
                        .OrderBy(u => u.UsageId).Select(u => new { Id = u.UsageId, u.MaintenanceWorkId, u.ItemId, u.BaseUomId, u.UsedBaseQty }),
                        "id", "maintenanceWorkId", "itemId", "baseUomId", "usedBaseQty");
                }
                else missing.Add("Preventive occurrence totals are not applicable to Incident category/severity filters; no category relationship is inferred.");
                break;
            }
            case "assets":
            {
                var assets = db.Assets.AsNoTracking().Where(a => branches.Contains(a.BranchId)
                    && (a.CurrentRoomId == null || a.CurrentRoom!.BranchId == a.BranchId)
                    && (a.CurrentAreaId == null || a.CurrentArea!.BranchId == a.BranchId)
                    && (a.CurrentDepartmentId == null || a.CurrentDepartment!.BranchId == a.BranchId)
                    && (department == null || a.CurrentDepartmentId == department)
                    && (query.RoomId == null || a.CurrentRoomId == query.RoomId)
                    && (query.AreaId == null || a.CurrentAreaId == query.AreaId)
                    && (query.CategoryId == null || a.AssetTypeId == query.CategoryId)
                    && (query.Condition == null || a.ConditionStatus == query.Condition));
                await Add("assets", assets.OrderBy(a => a.AssetId).Select(a => new { Id = a.AssetId, a.AssetCode,
                    a.AssetName, a.BranchId, a.AssetTypeId, a.CurrentRoomId, a.CurrentAreaId, a.CurrentDepartmentId,
                    a.CustodianUserId, a.ConditionStatus, a.LifecycleStatus }),
                    "id", "assetCode", "assetName", "branchId", "assetTypeId", "currentRoomId", "currentAreaId", "currentDepartmentId", "custodianUserId", "conditionStatus", "lifecycleStatus");
                var history = await ReportAssetHistory.ReadAsync(db, assets, authorizedBranches, start, end, ct);
                AddMaterialized("asset_history", history.Rows, "auditId", "assetId", "eventType", "recordId",
                    "sourceBranchId", "destinationBranchId", "occurredAt", "fromLocationType", "fromLocationId",
                    "toLocationType", "toLocationId", "fromDepartmentId", "toDepartmentId", "fromUserId", "toUserId", "status");
                foreach (var entry in history.Rows)
                {
                    requiredBranches.Add(entry.SourceBranchId);
                    requiredBranches.Add(entry.DestinationBranchId);
                }
                if (history.MissingEvidence) missing.Add("Some asset history is unavailable: event-time branch evidence or REPORT_VIEW at both ends is missing. Current ownership is never substituted.");
                missing.Add("Asset history follows the current asset filters and the event period; HANDOVER_SUBMITTED is submission evidence, not acceptance.");
                break;
            }
            case "items":
            {
                var movements = db.StockMovements.AsNoTracking().Where(m => branches.Contains(m.Warehouse.BranchId)
                    && (m.Warehouse.DepartmentId == null || m.Warehouse.Department!.BranchId == m.Warehouse.BranchId)
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
                    && (t.Warehouse.DepartmentId == null || t.Warehouse.Department!.BranchId == t.Warehouse.BranchId)
                    && (department == null || t.Warehouse.DepartmentId == department)
                    && (query.WarehouseId == null || t.WarehouseId == query.WarehouseId) && (query.ItemId == null || t.ItemId == query.ItemId));
                await Add("low_stock", thresholds.Where(t => (db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId)
                        .Sum(m => (decimal?)m.SignedBaseQty) ?? 0) < t.MinimumQty).OrderBy(t => t.WarehouseId).ThenBy(t => t.ItemId)
                    .Select(t => new { t.WarehouseId, t.ItemId, t.MinimumQty, Available = db.StockMovements.Where(m => m.WarehouseId == t.WarehouseId && m.ItemId == t.ItemId).Sum(m => (decimal?)m.SignedBaseQty) ?? 0 }),
                    "warehouseId", "itemId", "minimumQty", "available");
                var transfers = await ReportTransferLosses.ReadAsync(db, query, filters, authorizedBranches, ct);
                AddMaterialized("transfer_receipts_and_losses", transfers.Rows, "receiptLineId", "receiptId", "documentId",
                    "itemId", "uomId", "sourceWarehouseId", "destinationWarehouseId", "sourceBranchId", "destinationBranchId",
                    "receivedAt", "receivedQty", "damagedQty", "missingQty");
                foreach (var entry in transfers.Rows)
                {
                    requiredBranches.Add(entry.SourceBranchId);
                    requiredBranches.Add(entry.DestinationBranchId);
                }
                if (transfers.MissingEvidence) missing.Add("Some transfer history is unavailable: event-time branch/receipt evidence or REPORT_VIEW at both ends is missing.");
                missing.Add("Transfer quantities retain the recorded line UOM; receipt lines are counted once, not once per branch audit.");
                var periodMovements = movements.Where(m => m.OccurredAt >= start && m.OccurredAt < end);
                await Add("material_issues", periodMovements.Where(m => m.DocumentLine.Document.DocumentType == "ISSUE"
                    && m.SignedBaseQty < 0).OrderBy(m => m.MovementId).Select(m => new
                    {
                        Id = m.MovementId, m.DocumentLineId, m.WarehouseId, m.ItemId, m.Item.BaseUomId,
                        IssuedBaseQty = -m.SignedBaseQty, m.OccurredAt,
                        DepartmentId = m.DocumentLine.MaterialFulfillmentLine != null
                            && m.DocumentLine.MaterialFulfillmentLine.RequestLine.Request.BranchId == m.Warehouse.BranchId
                            && m.DocumentLine.MaterialFulfillmentLine.RequestLine.Request.Department.BranchId == m.Warehouse.BranchId
                            ? (long?)m.DocumentLine.MaterialFulfillmentLine.RequestLine.Request.DepartmentId : null
                    }), "id", "documentLineId", "warehouseId", "itemId", "baseUomId", "issuedBaseQty", "occurredAt", "departmentId");
                await Add("material_returns", db.StockDocumentLines.AsNoTracking()
                    .Where(l => l.Document.DocumentType == "RETURN" && l.Document.PostedAt >= start && l.Document.PostedAt < end
                        && l.Document.DestinationWarehouseId != null && branches.Contains(l.Document.DestinationWarehouse!.BranchId)
                        && (query.WarehouseId == null || l.Document.DestinationWarehouseId == query.WarehouseId)
                        && (query.ItemId == null || l.ItemId == query.ItemId)
                        && (department == null || l.Document.DestinationWarehouse!.DepartmentId == department))
                    .OrderBy(l => l.LineId).Select(l => new
                    {
                        DocumentLineId = l.LineId, WarehouseId = l.Document.DestinationWarehouseId, l.ItemId, l.Item.BaseUomId,
                        ReturnedBaseQty = l.BaseQty, OccurredAt = l.Document.PostedAt, l.Document.Status,
                        OriginalIssueLineId = l.StockReturnLineDetailStockDocumentLine == null ? (long?)null
                            : l.StockReturnLineDetailStockDocumentLine.OriginalIssueLineId,
                        DamagedQty = l.StockReturnLineDetailStockDocumentLine == null ? (decimal?)null
                            : l.StockReturnLineDetailStockDocumentLine.DamagedQty,
                        l.UomId
                    }), "documentLineId", "warehouseId", "itemId", "baseUomId", "returnedBaseQty", "occurredAt", "status", "originalIssueLineId", "damagedQty", "uomId");
                var issueLines = periodMovements.Where(m => m.DocumentLine.Document.DocumentType == "ISSUE").Select(m => m.DocumentLineId);
                var scopedWorks = MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking(),
                    await Assets.AssetTransferStorage.InstalledAsync(db, ct) ? db.AssetWorkSourceScopes.AsNoTracking() : null)
                    .Where(w => w.BranchId != null && branches.Contains(w.BranchId.Value)
                        && (w.Work.DepartmentId == null || w.Work.Department!.BranchId == w.BranchId)).Select(w => w.Work.MaintenanceWorkId);
                await Add("material_usage_by_work", db.MaintenanceMaterialUsages.AsNoTracking()
                    .Where(u => issueLines.Contains(u.StockDocumentLineId) && scopedWorks.Contains(u.MaintenanceWorkId))
                    .OrderBy(u => u.UsageId).Select(u => new { Id = u.UsageId, u.StockDocumentLineId, u.MaintenanceWorkId,
                        u.ItemId, u.BaseUomId, u.UsedBaseQty }), "id", "stockDocumentLineId", "maintenanceWorkId", "itemId", "baseUomId", "usedBaseQty");
                missing.Add("Issue department and work attribution use recorded fulfillment/usage links. Missing links are N/A; warehouse department and current employee membership are never substituted.");
                break;
            }
            case "tasks":
            {
                var tasks = db.OperationalTasks.AsNoTracking().Where(t => branches.Contains(t.BranchId)
                    && t.Department.BranchId == t.BranchId
                    && (t.RoomId == null || t.Room!.BranchId == t.BranchId)
                    && (t.AreaId == null || t.Area!.BranchId == t.BranchId)
                    && (department == null || t.DepartmentId == department) && t.CreatedAt >= start && t.CreatedAt < end);
                if (query.EmployeeId is {} employee)
                    tasks = tasks.Where(t => t.OperationalTaskAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null) == 1
                        && t.OperationalTaskAssignments.Any(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null
                            && a.AssignedAt <= now.UtcDateTime && a.AssigneeId == employee));
                var workflowInstalled = await ProgressSchema.InstalledAsync(db, ct);
                var workflow = workflowInstalled ? TaskWorkflowRead.Query(db, now.UtcDateTime)
                    : db.OperationalTasks.Where(t => false).Select(t => new TaskWorkflowRow { TaskId = t.TaskId, State = "" });
                var rows = from task in tasks
                    join state in workflow on task.TaskId equals state.TaskId into states
                    from state in states.DefaultIfEmpty()
                    select new { Id = task.TaskId, task.TaskCode, task.BranchId, task.DepartmentId, task.Status,
                        task.PriorityId, task.ProgressPercent, task.CreatedAt, task.DueAt, task.ActualStartAt, task.CompletedAt,
                        WorkflowState = workflowInstalled ? state.State : null,
                        Overdue = !workflowInstalled || state.State == null ? (bool?)null : task.DueAt != null && task.DueAt < now.UtcDateTime
                            && state.State != "CLOSED" && state.State != "CANCELLED" };
                await Add("operational_tasks", rows.OrderBy(t => t.Id),
                    "id", "taskCode", "branchId", "departmentId", "status", "priorityId", "progressPercent", "createdAt", "dueAt", "actualStartAt", "completedAt", "workflowState", "overdue");
                await Add("task_status_summary", rows.GroupBy(t => new { t.WorkflowState, t.Overdue })
                    .OrderBy(g => g.Key.WorkflowState).ThenBy(g => g.Key.Overdue)
                    .Select(g => new { g.Key.WorkflowState, g.Key.Overdue, TaskCount = g.LongCount() }),
                    "workflowState", "overdue", "taskCount");
                missing.Add("Task status summary uses the same created-in-period task population as detail rows; null lifecycle/overdue is an unknown bucket, not a non-overdue task");
                var effort = await TaskEffortRead.ReadAsync(db, filters, now.UtcDateTime, ct, query.EmployeeId);
                if (effort.Rows is {} effortRows)
                    AddMaterialized("task_effort_by_employee", effortRows,
                        "employeeId", "branchId", "closedEffortSeconds", "provisionalEffortSeconds", "openIntervals");
                else missing.Add("Task effort unavailable: " + effort.MissingReason);
                missing.Add("Employee task effort uses effective TASK intervals overlapping the selected UTC period regardless of task creation date; open effort is provisional, not completed effort");
                var workdayTime = await ReportWorkdayTime.ReadAsync(db, filters, now.UtcDateTime, ct, query.EmployeeId);
                if (workdayTime.Rows is {} workdayRows)
                    AddMaterialized("workday_time_by_employee", workdayRows,
                        "employeeId", "branchId", "closedWorkdaySeconds", "provisionalWorkdaySeconds", "openWorkdays");
                else missing.Add("Workday time unavailable: " + workdayTime.MissingReason);
                missing.Add("Workday time is actual StartedAt/EndedAt clipped to the UTC period, including unscheduled workdays; it is separate from task effort and planned shift duration");
                missing.Add("Employee filter: task detail/status uses the current assignee; task effort uses the interval performer; workday time uses the workday employee. Reassignment does not transfer historical time");
                break;
            }
            case "requests":
            {
                var requests = db.InternalRequests.AsNoTracking().Where(r => branches.Contains(r.BranchId)
                    && (r.RequesterDepartmentId == null || r.RequesterDepartment!.BranchId == r.BranchId)
                    && (r.ReceivingDepartmentId == null || r.ReceivingDepartment!.BranchId == r.BranchId)
                    && (r.RoomId == null || r.Room!.BranchId == r.BranchId)
                    && (r.AreaId == null || r.Area!.BranchId == r.BranchId)
                    && (department == null || r.ReceivingDepartmentId == department) && r.CreatedAt >= start && r.CreatedAt < end
                    && (query.Type == null || r.RequestType.TypeCode == query.Type));
                await Add("internal_requests", requests.OrderBy(r => r.InternalRequestId).Select(r => new { Id = r.InternalRequestId,
                    r.RequestNo, r.BranchId, r.RequestTypeId, r.RequesterDepartmentId, r.ReceivingDepartmentId, r.Status,
                    r.CreatedAt, r.SubmittedAt, r.DueAt, r.CompletedAt, r.ClosedAt,
                    Overdue = r.DueAt != null && r.DueAt < now.UtcDateTime && r.Status != "CLOSED" && r.Status != "REJECTED" && r.Status != "CANCELLED" }),
                    "id", "requestNo", "branchId", "requestTypeId", "requesterDepartmentId", "receivingDepartmentId", "status", "createdAt", "submittedAt", "dueAt", "completedAt", "closedAt", "overdue");
                var responseEvents = await (from e in db.InternalRequestEvents.AsNoTracking()
                    join r in requests on e.InternalRequestId equals r.InternalRequestId
                    where e.EventType == "ACCEPT" || e.EventType == "REJECT" || e.EventType == "REOPEN"
                        || e.EventType == "ASSIGN" || e.EventType == "REASSIGN" || e.EventType == "COMPLETE"
                        || e.OldStatus == "CLOSED" && e.NewStatus != "CLOSED"
                    orderby e.EventId
                    select new { e.InternalRequestId, e.EventId, e.EventType, e.OldStatus, e.NewStatus, e.OccurredAt,
                        AssignmentVerified = db.InternalRequestAssignments.Count(a => a.InternalRequestId == e.InternalRequestId
                            && a.AssignedAt == e.OccurredAt && a.AssignedBy == e.ActorId
                            && (a.Status == "ACTIVE" && a.EndedAt == null
                                || (a.Status == "REASSIGNED" || a.Status == "COMPLETED") && a.EndedAt != null)
                            && (a.EndedAt == null || a.EndedAt >= a.AssignedAt)) == 1 })
                    .Take(50001).ToArrayAsync(ct);
                if (responseEvents.Length > 50000)
                    throw new InsightException(422, "REPORT_ROW_LIMIT", "Request decision evidence exceeds 50,000 events. Narrow the filters; no data was truncated.");
                var byRequest = responseEvents.ToLookup(e => e.InternalRequestId);
                var requestSection = sections[^1];
                var processingTotals = new List<(long Type, long? Department, bool Closed, decimal? Seconds)>();
                foreach (var row in requestSection.Rows)
                {
                    var id = ((JsonElement)row["id"]!).GetInt64();
                    var submitted = (JsonElement)row["submittedAt"]!;
                    var history = byRequest[id].Select(e => new ReportRequestResponse.Event(e.EventId, e.EventType, e.OldStatus, e.NewStatus, e.OccurredAt, e.AssignmentVerified)).ToArray();
                    var response = ReportRequestResponse.Read(((JsonElement)row["createdAt"]!).GetDateTime(),
                        submitted.ValueKind == JsonValueKind.Null ? null : submitted.GetDateTime(), now.UtcDateTime,
                        history);
                    foreach (var property in JsonSerializer.SerializeToElement(response, jsonOptions).EnumerateObject())
                        row[property.Name] = property.Value.Clone();
                    DateTime? Timestamp(string name) => ((JsonElement)row[name]!).ValueKind == JsonValueKind.Null ? null : ((JsonElement)row[name]!).GetDateTime();
                    var processing = ReportRequestProcessing.Read(((JsonElement)row["status"]!).GetString()!,
                        Timestamp("createdAt")!.Value, Timestamp("completedAt"), Timestamp("closedAt"), now.UtcDateTime, history);
                    foreach (var property in JsonSerializer.SerializeToElement(processing, jsonOptions).EnumerateObject())
                        row[property.Name] = property.Value.Clone();
                    var receiving = (JsonElement)row["receivingDepartmentId"]!;
                    processingTotals.Add((((JsonElement)row["requestTypeId"]!).GetInt64(),
                        receiving.ValueKind == JsonValueKind.Null ? null : receiving.GetInt64(),
                        ((JsonElement)row["status"]!).GetString() == "CLOSED", processing.CompletionSeconds));
                }
                sections[^1] = requestSection with { Columns = [.. requestSection.Columns, "responseEventId", "responseAt", "responseSeconds", "responseEvidence", "completionSeconds", "completedCycles", "reopenCount", "completionEvidence"] };
                AddMaterialized("request_type_department_summary", processingTotals.GroupBy(r => new { r.Type, r.Department })
                    .OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Department).Select(g => new {
                        RequestTypeId = g.Key.Type, ReceivingDepartmentId = g.Key.Department, RequestCount = g.Count(),
                        ClosedRequestCount = g.Count(r => r.Closed), MeasuredClosedRequests = g.Count(r => r.Closed && r.Seconds != null),
                        MissingClosedRequests = g.Count(r => r.Closed && r.Seconds == null),
                        AverageCompletionSeconds = !g.Any(r => r.Closed) || g.Any(r => r.Closed && r.Seconds == null)
                            ? null : g.Where(r => r.Closed).Average(r => r.Seconds)
                    }).ToArray(), "requestTypeId", "receivingDepartmentId", "requestCount", "closedRequestCount", "measuredClosedRequests", "missingClosedRequests", "averageCompletionSeconds");
                missing.Add("Response time uses SubmittedAt to the recorded UC116 decision; missing, inconsistent or reopened-cycle evidence returns null with a reason, never zero");
                missing.Add("Completion time sums proven assignment/reopen-to-complete cycles per request, excluding Closed-to-Reopen gaps; open or inconsistent cycles return N/A. Department uses current receiving department; overdue uses the snapshot time under GBR-156");
                break;
            }
        }
        var first = sections[0];
        var appliedFilters = JsonSerializer.SerializeToElement(query, jsonOptions).EnumerateObject()
            .Where(p => p.Value.ValueKind != JsonValueKind.Null).ToDictionary(p => p.Name, p => (string?)p.Value.ToString());
        appliedFilters["from"] = filters.From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        appliedFilters["to"] = filters.To.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var report = new ReportResult(Guid.NewGuid().ToString("N"), key, now, filters, first.Columns, first.Rows,
            rowCount, sections, missing, [], appliedFilters, requiredBranches.Order().ToArray());
        return report;
    }

    public static void ValidateFilters(string key, ReportQuery query)
    {
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(query,
            new System.ComponentModel.DataAnnotations.ValidationContext(query), validateAllProperties: true);
        if (query.Condition?.Any(char.IsControl) == true || query.Type?.Any(char.IsControl) == true)
            throw new InsightException(400, "INVALID_FILTER", "Filter contains control characters.");
        var filters = new Dictionary<string, bool>
        {
            ["room"] = query.RoomId != null, ["area"] = query.AreaId != null,
            ["floor"] = query.FloorId != null, ["roomType"] = query.RoomTypeId != null,
            ["inspector"] = query.InspectorId != null, ["housekeeper"] = query.HousekeeperId != null,
            ["employee"] = query.EmployeeId != null, ["warehouse"] = query.WarehouseId != null,
            ["item"] = query.ItemId != null, ["severity"] = query.SeverityId != null,
            ["category"] = query.CategoryId != null, ["condition"] = query.Condition != null, ["type"] = query.Type != null
        };
        string[] allowed = key switch
        {
            "rooms" => ["floor", "roomType"], "inspections" => ["inspector", "housekeeper"],
            "incidents" => ["severity", "category"], "assets" => ["category", "condition", "room", "area"],
            "items" => ["warehouse", "item"], "tasks" => ["employee"], "requests" => ["type"], _ => []
        };
        if (filters.Any(f => f.Value && !allowed.Contains(f.Key)))
            throw new InsightException(400, "UNSUPPORTED_FILTER", "Filter is not supported for this report type.");
        if (query.RoomId != null && query.AreaId != null)
            throw new InsightException(400, "INVALID_LOCATION", "Select either a room or an area.");
    }
}
