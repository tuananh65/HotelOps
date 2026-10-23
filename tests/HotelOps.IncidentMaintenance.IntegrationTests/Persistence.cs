using System.Text.Json;
using Microsoft.EntityFrameworkCore;

sealed partial class IntegritySuite
{
    static void ExplainSnapshotDifference(string before, string after)
    {
        var a = JsonSerializer.Deserialize<string[]>(before)!; var b = JsonSerializer.Deserialize<string[]>(after)!;
        for (var i = 0; i < a.Length; i++) if (a[i] != b[i])
        {
            var left = JsonDocument.Parse(a[i]).RootElement.EnumerateArray().Select(x => x.GetRawText()).ToHashSet();
            var right = JsonDocument.Parse(b[i]).RootElement.EnumerateArray().Select(x => x.GetRawText()).ToHashSet();
            Console.WriteLine($"Snapshot table index {i}: removed {left.Except(right).Count()}, added {right.Except(left).Count()}");
            foreach (var row in left.Except(right).Concat(right.Except(left)).Take(4))
            {
                var key = JsonDocument.Parse(row).RootElement.EnumerateObject().First();
                Console.WriteLine($"Changed row key: {key.Name}={key.Value}");
            }
        }
    }
    async Task<string> Footprint()
    {
        await using var d = Db();
        var incidents = await d.Incidents.AsNoTracking().Where(x => owned.Contains(x.IncidentId)).OrderBy(x => x.IncidentId).Select(x => new { x.IncidentId, x.Status, x.SeverityId, x.PriorityId, x.ResolvedAt, x.ClosedAt, x.ClosureNote, x.ReportedAt }).ToArrayAsync();
        var works = await d.MaintenanceWorks.AsNoTracking().Where(x => x.IncidentId != null && owned.Contains(x.IncidentId.Value)).OrderBy(x => x.MaintenanceWorkId).Select(x => new { x.MaintenanceWorkId, x.IncidentId, x.Status, x.AssigneeId, x.DepartmentId, x.Title, x.Instruction, x.PriorityId, x.PlannedStartAt, x.PlannedEndAt, x.ActualStartAt, x.ResolvedAt, x.ResolutionNote, x.ReviewedBy, x.ReviewedAt, x.CreatedAt }).ToArrayAsync();
        var ids = works.Select(x => x.MaintenanceWorkId).ToArray();
        var logs = await d.MaintenanceWorkLogs.AsNoTracking().Where(x => ids.Contains(x.MaintenanceWorkId)).OrderBy(x => x.LogId).Select(x => new { x.LogId, x.MaintenanceWorkId, x.WorkNote, x.SpentMinutes, x.MaterialIssueId, x.Status, x.LoggedBy, x.LoggedAt }).ToArrayAsync();
        var events = await d.IncidentEvents.AsNoTracking().Where(x => owned.Contains(x.IncidentId)).OrderBy(x => x.EventId).Select(x => new { x.EventId, x.IncidentId, x.EventType, x.FromStatus, x.ToStatus, x.ActorId, x.Note, x.OccurredAt }).ToArrayAsync();
        var incidentIds = owned.Select(x => x.ToString()).ToArray(); var workIds = ids.Select(x => x.ToString()).ToArray(); var logIds = logs.Select(x => x.LogId.ToString()).ToArray();
        var audit = await d.AuditLogs.AsNoTracking().Where(x => x.EntityType == "incidents" && incidentIds.Contains(x.EntityId) || x.EntityType == "maintenance_works" && workIds.Contains(x.EntityId) || x.EntityType == "maintenance_work_logs" && logIds.Contains(x.EntityId)).OrderBy(x => x.AuditId).Select(x => new { x.AuditId, x.EntityType, x.EntityId, x.Action, x.ActorUserId, x.BranchId, x.OccurredAt, x.OldValues, x.NewValues, x.ChangedFields }).ToArrayAsync();
        var totals = new[] { await d.Incidents.CountAsync(), await d.MaintenanceWorks.CountAsync(), await d.MaintenanceWorkLogs.CountAsync(), await d.IncidentEvents.CountAsync(), await d.AuditLogs.CountAsync() };
        var usages = await d.MaintenanceMaterialUsages.AsNoTracking().OrderBy(x => x.UsageId).Select(x => new { x.UsageId, x.MaintenanceWorkId, x.MaintenanceWorkLogId, x.StockDocumentLineId, x.ItemId, x.UomId, x.BaseUomId, x.UsedQty, x.ConversionFactor, x.UsedBaseQty }).ToArrayAsync();
        var stock = await d.StockDocuments.AsNoTracking().OrderBy(x => x.DocumentId).Select(x => new { x.DocumentId, x.Status, x.RowVersion, x.PostedAt }).ToArrayAsync();
        var stockTotals = new[] { await d.StockDocuments.CountAsync(), await d.StockDocumentLines.CountAsync(), await d.StockMovements.CountAsync(), await d.StockReturnLineDetails.CountAsync() };
        var ledger = await d.StockMovements.SumAsync(x => (decimal?)x.SignedBaseQty) ?? 0;
        var cycles = await d.MaintenanceWorkCycles.AsNoTracking().OrderBy(x => x.CycleId).Select(x => new { x.CycleId, x.MaintenanceWorkId, x.CycleNo, x.IsLegacySnapshot, x.RecordedAt, x.RecordedBy, x.OpenedAt, x.OpenedBy, x.ReopenReason, x.StartingWorkStatus, x.StartingIncidentStatus, x.FinalizedAt, x.TerminalStatus, x.EndedAt, x.EndedBy, x.TerminalReason, x.AssigneeId, x.DepartmentId, x.PriorityId, x.Title, x.Instruction, x.PlannedStartAt, x.PlannedEndAt, x.ActualStartAt, x.ResolvedAt, x.ResolutionNote, x.ReviewedBy, x.ReviewedAt, x.IncidentResolvedAt, x.IncidentClosedAt, x.IncidentClosureNote }).ToArrayAsync();
        var logCycles = await d.MaintenanceWorkLogs.AsNoTracking().Where(x => ids.Contains(x.MaintenanceWorkId)).OrderBy(x => x.LogId).Select(x => new { x.LogId, x.CycleId }).ToArrayAsync();
        return JsonSerializer.Serialize(new { incidents, works, logs, events, audit, totals, usages, stock, stockTotals, ledger, cycles, logCycles });
    }
    async Task<string> LegacySnapshot()
    {
        await using var d = Db(); var values = new List<string>();
        foreach (var sql in new[] {
            "SELECT (SELECT * FROM dbo.maintenance_work_cycles ORDER BY cycle_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.maintenance_material_usages ORDER BY usage_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.incidents ORDER BY incident_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.maintenance_works ORDER BY maintenance_work_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.maintenance_work_logs ORDER BY log_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.incident_events ORDER BY event_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.audit_logs ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.stock_documents ORDER BY document_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.stock_document_lines ORDER BY line_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.stock_movements ORDER BY movement_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_requests ORDER BY request_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_request_lines ORDER BY line_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_fulfillments ORDER BY fulfillment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_replenishments ORDER BY replenishment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_replenishment_lines ORDER BY replenishment_line_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.material_replenishment_receipt_lines ORDER BY replenishment_receipt_line_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value",
            "SELECT (SELECT * FROM dbo.assets ORDER BY asset_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value" })
            values.Add(await d.Database.SqlQueryRaw<string>(sql).SingleAsync());
        return JsonSerializer.Serialize(values);
    }
    async Task<(int Events, int IncidentAudits, int WorkAudits)> HistoryCounts(long incident)
    {
        await using var d = Db(); var iid = incident.ToString(); var works = await d.MaintenanceWorks.Where(x => x.IncidentId == incident).Select(x => x.MaintenanceWorkId.ToString()).ToArrayAsync();
        return (await d.IncidentEvents.CountAsync(x => x.IncidentId == incident), await d.AuditLogs.CountAsync(x => x.EntityType == "incidents" && x.EntityId == iid), await d.AuditLogs.CountAsync(x => x.EntityType == "maintenance_works" && works.Contains(x.EntityId)));
    }
    async Task Cleanup()
    {
        await using var d = Db(); await using var tx = await d.Database.BeginTransactionAsync();
        var incidents = await d.Incidents.Where(x => x.IncidentTitle.StartsWith(prefix)).Select(x => x.IncidentId).ToArrayAsync();
        Check(owned.All(incidents.Contains), "Cleanup ownership prefix agrees with exact created IDs");
        var works = await d.MaintenanceWorks.Where(x => x.IncidentId != null && incidents.Contains(x.IncidentId.Value)).Select(x => x.MaintenanceWorkId).ToArrayAsync();
        var logs = await d.MaintenanceWorkLogs.Where(x => works.Contains(x.MaintenanceWorkId)).Select(x => x.LogId).ToArrayAsync();
        var ii = incidents.Select(x => x.ToString()).ToArray(); var wi = works.Select(x => x.ToString()).ToArray(); var li = logs.Select(x => x.ToString()).ToArray();
        var usageIds = await d.MaintenanceMaterialUsages.Where(x => works.Contains(x.MaintenanceWorkId)).Select(x => x.UsageId.ToString()).ToArrayAsync();
        await d.AuditLogs.Where(x => x.EntityType == "maintenance_material_usages" && usageIds.Contains(x.EntityId)).ExecuteDeleteAsync();
        await d.MaintenanceMaterialUsages.Where(x => works.Contains(x.MaintenanceWorkId)).ExecuteDeleteAsync();
        await CleanupMaterialFixtures(d, incidents);
        await d.AuditLogs.Where(x => x.EntityType == "incidents" && ii.Contains(x.EntityId) || x.EntityType == "maintenance_works" && wi.Contains(x.EntityId) || x.EntityType == "maintenance_work_logs" && li.Contains(x.EntityId)).ExecuteDeleteAsync();
        await d.MaintenanceWorkLogs.Where(x => logs.Contains(x.LogId)).ExecuteDeleteAsync();
        var cycleIds = await d.MaintenanceWorkCycles.Where(x => works.Contains(x.MaintenanceWorkId)).Select(x => x.CycleId.ToString()).ToArrayAsync();
        await d.AuditLogs.Where(x => x.EntityType == "maintenance_work_cycles" && cycleIds.Contains(x.EntityId)).ExecuteDeleteAsync();
        await d.MaintenanceWorkCycles.Where(x => works.Contains(x.MaintenanceWorkId)).ExecuteDeleteAsync();
        await d.MaintenanceWorks.Where(x => works.Contains(x.MaintenanceWorkId)).ExecuteDeleteAsync();
        await d.IncidentEvents.Where(x => incidents.Contains(x.IncidentId)).ExecuteDeleteAsync();
        await d.Incidents.Where(x => incidents.Contains(x.IncidentId)).ExecuteDeleteAsync();
        await tx.CommitAsync(); Console.WriteLine("Owned Incident/Maintenance fixture cleanup committed.");
    }
}
