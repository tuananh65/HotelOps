using System.Data;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintReportProbe
{
    internal static async Task<int> RunAsync(string reportPath)
    {
        if (File.Exists(reportPath)) throw new IOException("Evidence already exists");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var ct = timeout.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var checks = new List<string>(); string? failure = null; bool rollback = false;
        object? inventoryObservation = null;
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add(name); }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var queries = new ReportSnapshotQueries(db);
        bool rejected = false;
        try { await queries.MaterializeAsync("tasks", new(), new([], null, today, today), now, ct); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "query component refuses materialization outside SERIALIZABLE transaction");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = tx.GetDbTransaction();
            // Exact server/database/GUID/disposable run and empty-fixture checks precede every INSERT.
            command.CommandText = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql", ct);
            long actor, branch, inspection;
            await using (var reader = await command.ExecuteReaderAsync(ct))
            { if (!await reader.ReadAsync(ct)) throw new Exception("No fixture"); actor = reader.GetInt64(0); branch = reader.GetInt64(1); inspection = reader.GetInt64(2); }
            var prefix = "D3RPT" + Guid.NewGuid().ToString("N")[..8];
            var foreign = new Branch { BranchCode = prefix, BranchName = "Foreign synthetic", IsActive = true };
            db.Branches.Add(foreign); await db.SaveChangesAsync(ct);
            var ownDept = new Department { BranchId = branch, DepartmentCode = prefix, DepartmentName = "Own synthetic", IsActive = true };
            var otherDept = new Department { BranchId = foreign.BranchId, DepartmentCode = prefix, DepartmentName = "Other synthetic", IsActive = true };
            db.Departments.AddRange(ownDept, otherDept); await db.SaveChangesAsync(ct);
            var start = now.UtcDateTime;
            OperationalTask TaskRow(string suffix, long b, long d, DateTime created) => new()
            { TaskCode = prefix + suffix, Title = "Synthetic report task", BranchId = b, DepartmentId = d,
                Status = "TODO", ProgressPercent = 0, CreatedBy = actor, CreatedAt = created };
            var first = TaskRow("A", branch, ownDept.DepartmentId, start);
            var last = TaskRow("B", branch, ownDept.DepartmentId, start.AddDays(1).AddMilliseconds(-10));
            db.OperationalTasks.AddRange(first, last, TaskRow("C", branch, ownDept.DepartmentId, start.AddDays(1)),
                TaskRow("D", foreign.BranchId, otherDept.DepartmentId, start), TaskRow("E", branch, otherDept.DepartmentId, start));
            var type = new InternalRequestType { TypeCode = prefix, TypeName = "Synthetic report", IsActive = true };
            db.InternalRequestTypes.Add(type); await db.SaveChangesAsync(ct);
            db.InternalRequests.AddRange(new InternalRequest { RequestNo = prefix + "A", BranchId = branch, RequestTypeId = type.RequestTypeId,
                RequesterId = actor, RequesterDepartmentId = ownDept.DepartmentId, Subject = "Own", Description = "Synthetic", Status = "IN_PROGRESS", CreatedAt = start },
                new InternalRequest { RequestNo = prefix + "B", BranchId = branch, RequestTypeId = type.RequestTypeId,
                RequesterId = actor, RequesterDepartmentId = otherDept.DepartmentId, Subject = "Mismatched", Description = "Synthetic", Status = "IN_PROGRESS", CreatedAt = start });
            await db.SaveChangesAsync(ct);
            await db.RoomInspections.Where(i => i.InspectionId == inspection).ExecuteUpdateAsync(s => s.SetProperty(i => i.InspectedAt, start), ct);
            var category = new ItemCategory { CategoryCode = prefix, CategoryName = "Synthetic report", IsActive = true };
            var unit = new UnitsOfMeasure { UomCode = prefix, UomName = "Synthetic units", IsActive = true };
            db.ItemCategories.Add(category); db.UnitsOfMeasures.Add(unit); await db.SaveChangesAsync(ct);
            var item = new InventoryItem { ItemCode = prefix, ItemName = "Synthetic item", CategoryId = category.ItemCategoryId, BaseUomId = unit.UomId, IsActive = true, IsConsumable = true };
            var warehouse = new Warehouse { BranchId = branch, DepartmentId = ownDept.DepartmentId, WarehouseCode = prefix + "A", WarehouseName = "Own warehouse", IsActive = true };
            var inconsistent = new Warehouse { BranchId = branch, DepartmentId = otherDept.DepartmentId, WarehouseCode = prefix + "B", WarehouseName = "Mismatched warehouse", IsActive = true };
            db.InventoryItems.Add(item); db.Warehouses.AddRange(warehouse, inconsistent); await db.SaveChangesAsync(ct);
            var sequence = 0;
            void Movement(Warehouse wh, decimal quantity, DateTime at)
            {
                var document = new StockDocument { DocumentNo = prefix + (++sequence), DocumentType = quantity > 0 ? "RECEIPT" : "ISSUE", Status = "POSTED",
                    SourceWarehouseId = quantity < 0 ? wh.WarehouseId : null, DestinationWarehouseId = quantity > 0 ? wh.WarehouseId : null,
                    DocumentDate = DateOnly.FromDateTime(at), CreatedBy = actor, PostedBy = actor, CreatedAt = at, PostedAt = at };
                var line = new StockDocumentLine { Document = document, LineNo = 1, ItemId = item.ItemId, UomId = unit.UomId,
                    ConversionFactor = 1, ActualQty = Math.Abs(quantity), BaseQty = Math.Abs(quantity) };
                db.StockMovements.Add(new StockMovement { DocumentLine = line, WarehouseId = wh.WarehouseId, ItemId = item.ItemId,
                    SignedBaseQty = quantity, OccurredAt = at, CreatedBy = actor });
            }
            Movement(warehouse, 10.125m, start); Movement(warehouse, -2.005m, start.AddHours(1));
            Movement(warehouse, 5m, start.AddSeconds(-1)); Movement(inconsistent, 999m, start);
            db.StockThresholds.AddRange(new StockThreshold { WarehouseId = warehouse.WarehouseId, ItemId = item.ItemId, MinimumQty = 14m, AlertEnabled = true },
                new StockThreshold { WarehouseId = inconsistent.WarehouseId, ItemId = item.ItemId, MinimumQty = 1000m, AlertEnabled = true });
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            var filters = new InsightFilters([branch], null, today, today);
            var query = new ReportQuery { BranchId = branch, From = today, To = today };
            var results = new Dictionary<string, ReportResult>();
            foreach (var key in ReportSnapshotQueries.Keys)
            {
                var data = await queries.MaterializeAsync(key, query, filters, now, ct);
                Check(data.Snapshot == now && data.Filters == filters && data.TotalCount == data.Sections.Sum(s => s.Rows.Count), key + " executes SQL with consistent snapshot/filter/count metadata");
                Check(data.MissingSources.Count > 0 && data.ExportFormats.Length == 0, key + " remains partial and does not advertise authorized exports");
                results[key] = data;
            }
            Check(results["tasks"].TotalCount == 2 && results["tasks"].Rows.Select(r => ((JsonElement)r["id"]!).GetInt64()).SequenceEqual(new[] { first.TaskId, last.TaskId }),
                "tasks include UTC start/last instant; exclude next day, foreign branch and inconsistent department");
            Check(((JsonElement)results["tasks"].Rows[0]["workflowState"]!).ValueKind == JsonValueKind.Null
                && ((JsonElement)results["tasks"].Rows[0]["overdue"]!).ValueKind == JsonValueKind.Null, "unclassified task lifecycle is not invented by report");
            await db.OperationalTasks.Where(t => t.TaskId == first.TaskId).ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, "IN_PROGRESS").SetProperty(t => t.DueAt, start.AddSeconds(-1)), ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({first.TaskId},'IN_PROGRESS')", ct);
            foreach (var state in new[] { "IN_PROGRESS", "PAUSED" })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status={state} WHERE task_id={first.TaskId}", ct);
                var taskReport = await queries.MaterializeAsync("tasks", query, filters, now, ct);
                Check(((JsonElement)taskReport.Rows[0]["workflowState"]!).GetString() == state && ((JsonElement)taskReport.Rows[0]["overdue"]!).GetBoolean(), "canonical task state and overdue reported: " + state);
            }
            await db.OperationalTasks.Where(t => t.TaskId == first.TaskId).ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, "TODO"), ct);
            var inconsistentTask = await queries.MaterializeAsync("tasks", query, filters, now, ct);
            Check(((JsonElement)inconsistentTask.Rows[0]["workflowState"]!).ValueKind == JsonValueKind.Null
                && ((JsonElement)inconsistentTask.Rows[0]["overdue"]!).ValueKind == JsonValueKind.Null, "inconsistent physical/workflow state yields unknown rather than false overdue");
            Check(results["requests"].TotalCount == 1, "requests exclude mismatched department linkage");
            await tx.CreateSavepointAsync("MissingWorkflowCapability", ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Progress.V1'", ct);
                var unavailable = await queries.MaterializeAsync("tasks", query, filters, now, ct);
                Check(unavailable.Rows.Count == 2 && unavailable.Rows.All(r => ((JsonElement)r["workflowState"]!).ValueKind == JsonValueKind.Null
                    && ((JsonElement)r["overdue"]!).ValueKind == JsonValueKind.Null), "missing workflow capability preserves scoped rows with unknown lifecycle and overdue");
            }
            finally { await tx.RollbackToSavepointAsync("MissingWorkflowCapability", CancellationToken.None); }
            var requestRows = db.InternalRequests.Where(r => r.RequestNo == prefix + "A");
            Check(!((JsonElement)results["requests"].Rows[0]["overdue"]!).GetBoolean(), "no deadline is not overdue");
            ReportResult? frozenOverdue = null;
            foreach (var sample in new[] { ("IN_PROGRESS", -1, true), ("IN_PROGRESS", 0, false), ("IN_PROGRESS", 1, false),
                ("CLOSED", -1, false), ("REJECTED", -1, false), ("CANCELLED", -1, false) })
            {
                await requestRows.ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, sample.Item1).SetProperty(r => r.DueAt, start.AddSeconds(sample.Item2)), ct);
                var measured = await queries.MaterializeAsync("requests", query, filters, now, ct);
                Check(((JsonElement)measured.Rows[0]["overdue"]!).GetBoolean() == sample.Item3, $"GBR156 snapshot overdue boundary {sample.Item1}/{sample.Item2}");
                if (sample.Item3) frozenOverdue = measured;
            }
            Check(frozenOverdue != null && ((JsonElement)frozenOverdue.Rows[0]["overdue"]!).GetBoolean(), "overdue snapshot unchanged after deadline and terminal-state source updates");
            var frozenCsv = Encoding.UTF8.GetString(ReportCsvRenderer.Render(frozenOverdue!).Content);
            Check(frozenCsv.Contains("overdue") && frozenCsv.Contains("true"), "CSV includes the frozen overdue indicator");
            Check(results["rooms"].Rows.Count == 1 && results["rooms"].Sections.Single(s => s.Key == "cleaning_tasks").Rows.Count == 1, "room report contains scoped room and cleaning rows");
            Check(results["inspections"].Rows.Count == 1, "inspection report contains its scoped dated row");
            var inventory = results["items"];
            inventoryObservation = inventory.Sections;
            Check(inventory.Rows.Count == 1 && ((JsonElement)inventory.Rows[0]["onHand"]!).ValueKind == JsonValueKind.String
                && decimal.Parse(((JsonElement)inventory.Rows[0]["onHand"]!).GetString()!, System.Globalization.CultureInfo.InvariantCulture) == 13.120m,
                "inventory all-time balance preserves exact decimal and excludes inconsistent warehouse department");
            Check(inventory.Sections.Single(s => s.Key == "stock_movements").Rows.Count == 2, "inventory period movements exclude prior-day quantity");
            Check(inventory.Sections.Single(s => s.Key == "low_stock").Rows.Count == 1, "low stock uses scoped warehouse and exact aggregate");
            Check(((JsonElement)results["tasks"].Rows[0]["createdAt"]!).GetString()!.EndsWith('Z'), "SQL timestamp serialized explicitly in UTC");
            var store = new ReportSnapshotStore(TimeProvider.System); var snapshot = results["tasks"]; store.Put(actor, snapshot);
            await db.OperationalTasks.Where(t => t.TaskId == first.TaskId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ProgressPercent, 42), ct);
            var cached = store.Get(actor, snapshot.SnapshotId);
            Check(((JsonElement)cached.Rows[0]["progressPercent"]!).GetInt32() == 0, "cached snapshot remains unchanged after SQL source changes");
            var csv = Encoding.UTF8.GetString(ReportCsvRenderer.Render(cached).Content);
            Check(csv.Contains(snapshot.SnapshotId) && csv.Contains(first.TaskCode) && !csv.Contains(prefix + "D"), "CSV uses frozen scoped server rows");
            var scoped = await queries.MaterializeAsync("tasks", new() { BranchId = branch, DepartmentId = ownDept.DepartmentId }, filters with { DepartmentId = ownDept.DepartmentId }, now, ct);
            Check(scoped.TotalCount == 2, "supported department filter executes against SQL");
            foreach (var key in ReportSnapshotQueries.Keys)
            {
                var empty = await queries.MaterializeAsync(key, new(), filters with { BranchIds = [] }, now, ct);
                Check(empty.TotalCount == 0, key + " empty authorized branch set never broadens scope");
            }
            rejected = false;
            try { await queries.MaterializeAsync("tasks", query, filters with { BranchIds = [foreign.BranchId] }, now, ct); }
            catch (InsightException e) when (e.Code == "INVALID_FILTER_SCOPE") { rejected = true; }
            Check(rejected, "mismatched resolved branch filters refused");
            // Exercise the actual SQL materialization cap, not just the CSV renderer.
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT dbo.operational_tasks(task_code,title,branch_id,department_id,status,progress_percent,created_by,created_at)
                SELECT TOP (49998) CONCAT({prefix},'LIMIT',ROW_NUMBER() OVER(ORDER BY a.object_id,b.object_id)),N'Synthetic report cap',{branch},{ownDept.DepartmentId},'TODO',0,{actor},{start}
                FROM sys.all_objects a CROSS JOIN sys.all_objects b", ct);
            var atLimit = await queries.MaterializeAsync("tasks", query, filters, now, ct);
            Check(atLimit.TotalCount == 50000, "exactly 50000 SQL rows accepted");
            db.OperationalTasks.Add(TaskRow("OVER", branch, ownDept.DepartmentId, start)); await db.SaveChangesAsync(ct);
            rejected = false;
            try { await queries.MaterializeAsync("tasks", query, filters, now, ct); }
            catch (InsightException e) when (e.Code == "REPORT_ROW_LIMIT") { rejected = true; }
            Check(rejected, "50001 SQL rows refused without truncated snapshot");
        }
        catch (Exception e) { failure = e.GetBaseException().Message; }
        finally { await tx.RollbackAsync(CancellationToken.None); rollback = true; }
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { success = failure == null, checks, failure, rollback, inventoryObservation,
            scope = "SQL query/CSV component only; caller supplies scope; no HTTP, role admission or export audit acceptance" }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report SQL: {(failure == null ? "PASS" : "FAIL")}; checks={checks.Count}; rollback={rollback}");
        return failure == null ? 0 : 1;
    }
}
