using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;

sealed partial class IntegritySuite
{
    ReopenMaintenanceWorkRequest ReopenInput(long? cycle = null) => new() { ActorUserId = verifier, Reason = prefix, ExpectedCycleId = cycle };
    Task<MaintenanceWorkDetailDto> Reopen(long work, long? cycle = null) => Send<MaintenanceWorkDetailDto>("POST", $"maintenance-works/{work}/reopen", ReopenInput(cycle));
    Task<MaintenanceCyclePage> Cycles(long work, string paging = "") => Send<MaintenanceCyclePage>("GET", $"maintenance-works/{work}/cycles" + paging);
    async Task RejectReopen(long work, ReopenMaintenanceWorkRequest? input = null)
    {
        var before = await Footprint(); await Bad("POST", $"maintenance-works/{work}/reopen", input ?? ReopenInput()); Check(before == await Footprint(), "Rejected reopen has no persisted effects");
    }
    async Task CycleCommand(long work, long cycle, string command, long? who = null)
        => await Send<MaintenanceWorkDetailDto>("POST", $"maintenance-works/{work}/{command}", new { ActorUserId = who ?? actor, ExpectedCycleId = cycle, Reason = prefix, Note = prefix, ResolutionNote = prefix, WorkNote = prefix });
    async Task ManagedStart(long work, long cycle)
    {
        await Send<MaintenanceWorkDetailDto>("PATCH", $"maintenance-works/{work}/assignment", new { ActorUserId = verifier, AssigneeId = actor, ExpectedCycleId = cycle });
        await CycleCommand(work, cycle, "start");
    }
    async Task ManagedClose(long work, long cycle)
    {
        await CycleCommand(work, cycle, "submit-result"); await CycleCommand(work, cycle, "verify", verifier);
    }
    async Task<string> Frozen(long work)
    {
        await using var d = Db(); return await d.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.maintenance_work_cycles WHERE maintenance_work_id={work} AND finalized_at IS NOT NULL ORDER BY cycle_no FOR JSON PATH,INCLUDE_NULL_VALUES) AS Value").SingleAsync();
    }
    async Task ReopenCycles()
    {
        await ReopenConfiguration();
        var p = await Prepare("CLOSED");
        await using (var seed = Db())
        {
            var planStart = DateTime.UtcNow.AddDays(-4); var planEnd = planStart.AddDays(1);
            await seed.MaintenanceWorks.Where(x => x.MaintenanceWorkId == p.Work).ExecuteUpdateAsync(s => s.SetProperty(x => x.PlannedStartAt, planStart).SetProperty(x => x.PlannedEndAt, planEnd).SetProperty(x => x.Instruction, prefix).SetProperty(x => x.DepartmentId, usageDepartment).SetProperty(x => x.PriorityId, priority));
            await seed.Incidents.Where(x => x.IncidentId == p.Incident).ExecuteUpdateAsync(s => s.SetProperty(x => x.SeverityId, severity).SetProperty(x => x.PriorityId, priority));
        }
        var before = await Send<MaintenanceWorkDetailDto>("GET", $"maintenance-works/{p.Work}");
        var oldLogs = before.MaintenanceWorkLogs.Select(x => x.LogId).ToArray();
        var opened = await Reopen(p.Work); var current = opened.CurrentCycleId!.Value;
        Check(opened.CurrentCycleNo == 2 && opened.Status == "PENDING" && opened.IncidentClosedAt == null, "First reopen current cycle/pair");
        Check(opened.AssigneeId == null && opened.PlannedStartAt == null && opened.PlannedEndAt == null && opened.ActualStartAt == null && opened.ResolvedAt == null && opened.ResolutionNote == null && opened.ReviewedAt == null && opened.ReviewedBy == null, "All current fields reset");
        Check(opened.WorkNo == before.WorkNo && opened.CreatedAt == before.CreatedAt && opened.CreatedBy == before.CreatedBy && opened.Title == before.Title && opened.Instruction == before.Instruction && opened.DepartmentId == before.DepartmentId && opened.PriorityId == before.PriorityId && opened.MaintenanceWorkLogs.Count == before.MaintenanceWorkLogs.Count, "Preserved identity/context, no fake reopen technical log");
        await AssertPair(p.Work, "PENDING", "OPEN");
        var history = await Cycles(p.Work); Check(history.TotalCount == 2 && history.Items[0].IsLegacySnapshot && history.Items[0].Snapshot.ResolutionNote == before.ResolutionNote && history.Items[0].Snapshot.ReviewedAt == before.ReviewedAt && history.Items[0].OpenedAt == null && history.Items[0].OpenedBy == null && history.Items[0].EndedAt == null && history.Items[0].EndedBy == null, "Exact legacy terminal snapshot without invented metadata");
        Check(before.PlannedStartAt != null && before.PlannedEndAt != null && history.Items[0].Snapshot.PlannedStartAt == before.PlannedStartAt && history.Items[0].Snapshot.PlannedEndAt == before.PlannedEndAt && history.Items[0].Snapshot.Instruction == prefix, "Non-null prior plans/context preserved in immutable snapshot");
        await using (var read = Db()) Check(await read.Incidents.Where(x => x.IncidentId == p.Incident).AllAsync(x => x.SeverityId == severity && x.PriorityId == priority), "Incident classifications preserved after reopen");
        Check(history.Items[0].LogCount == oldLogs.Length && history.Items[1].FinalizedAt == null, "Legacy logical grouping and active cycle");
        var frozen = await Frozen(p.Work);
        await CycleSqlConstraints(p.Work, current);
        foreach (var command in new[] { "assignment", "start", "hold", "resume", "logs", "submit-result", "verify", "return", "cancel" }) foreach (var token in new long?[] { null, long.MaxValue })
        {
            var footprint = await Footprint(); await Bad(command == "assignment" ? "PATCH" : "POST", $"maintenance-works/{p.Work}/{command}", new { ActorUserId = actor, ExpectedCycleId = token, AssigneeId = actor, Reason = prefix, Note = prefix, WorkNote = prefix, ResolutionNote = prefix }); Check(footprint == await Footprint(), "Missing/wrong token rejected before any mutation: " + command);
        }
        await RejectReopen(p.Work, ReopenInput(current));
        await ManagedStart(p.Work, current);
        await CycleCommand(p.Work, current, "hold"); await CycleCommand(p.Work, current, "resume");
        var log = await Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{p.Work}/logs", new CreateMaintenanceWorkLogRequest { ActorUserId = actor, ExpectedCycleId = current, WorkNote = prefix }, 201);
        Check(log.CycleId == current && log.MaterialIssueId == null, "Plain log has server cycle and null material header");
        await CycleCommand(p.Work, current, "submit-result"); await CycleCommand(p.Work, current, "return", verifier);
        Check((await Cycles(p.Work)).TotalCount == 2 && (await Cycles(p.Work)).Items[1].FinalizedAt == null, "Return-for-rework stays in active cycle");
        Check(frozen == await Frozen(p.Work), "Legacy snapshot immutable while current cycle executes");
        await ManagedClose(p.Work, current);
        for (var next = 3; next <= 4; next++)
        {
            frozen = await Frozen(p.Work); await RejectReopen(p.Work); await RejectReopen(p.Work, ReopenInput(long.MaxValue));
            opened = await Reopen(p.Work, current); Check(opened.CurrentCycleNo == next, "Exact subsequent cycle numbering");
            var oldCycle = current; current = opened.CurrentCycleId!.Value; Check(frozen == await Frozen(p.Work), "Prior finalized snapshots unchanged by reopen");
            await ManagedStart(p.Work, current);
            var footprint = await Footprint(); await Bad("POST", $"maintenance-works/{p.Work}/logs", new { ActorUserId = actor, ExpectedCycleId = oldCycle, WorkNote = prefix }); Check(footprint == await Footprint(), "Delayed previous-cycle log rejected in matching IN_PROGRESS state");
            await ManagedClose(p.Work, current);
        }
        history = await Cycles(p.Work); Check(history.Items.Select(x => x.CycleNo).SequenceEqual(new[] { 1, 2, 3, 4 }) && history.Items.All(x => x.TerminalStatus == "CLOSED"), "Four closed processing cycles");
        var page = await Cycles(p.Work, "?page=2&pageSize=2"); Check(page.Items.Select(x => x.CycleNo).SequenceEqual(new[] { 3, 4 }), "Deterministic history paging");
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=2147483647&pageSize=100" }) await Send<object>("GET", $"maintenance-works/{p.Work}/cycles{query}", status: 400);
        await Send<object>("GET", "maintenance-works/9223372036854775807/cycles", status: 404);
        await Send<object>("POST", "maintenance-works/9223372036854775807/reopen", ReopenInput(), 404);
        await using (var d = Db())
        {
            Check(await d.MaintenanceWorkLogs.Where(x => oldLogs.Contains(x.LogId)).AllAsync(x => x.CycleId == null), "Legacy log FK unchanged");
            Check(await d.MaintenanceWorkLogs.Where(x => x.MaintenanceWorkId == p.Work && !oldLogs.Contains(x.LogId)).AllAsync(x => x.CycleId != null), "All new automatic/plain logs assigned");
            var ids = history.Items.Select(x => x.CycleId.ToString()).ToArray();
            var audits = await d.AuditLogs.Where(x => x.EntityType == "maintenance_work_cycles" && ids.Contains(x.EntityId)).ToArrayAsync();
            Check(audits.Count(x => x.Action == "CREATE") == 4 && audits.Count(x => x.Action == "UPDATE") == 3 && audits.All(x => x.BranchId == branch && x.ActorUserId == verifier), "Exact cycle CREATE/finalize audits");
            Check(audits.Count(x => x.NewValues!.Contains("LEGACY_CYCLE_CAPTURED")) == 1, "One truthful legacy capture audit");
            Check(await d.IncidentEvents.CountAsync(x => x.IncidentId == p.Incident && x.FromStatus == "CLOSED" && x.ToStatus == "OPEN" && x.Note == prefix) == 3, "Exact paired reopen events");
        }
        await ReopenGuards(); await ReopenMaterials();
    }
    async Task ReopenGuards()
    {
        foreach (var state in new[] { "PENDING", "IN_PROGRESS", "ON_HOLD", "COMPLETED", "CANCELLED" }) { var p = await Prepare(state); await RejectReopen(p.Work); }
        var closed = await Prepare("CLOSED"); await RejectReopen(closed.Work, new() { ActorUserId = verifier, Reason = "short" }); await RejectReopen(closed.Work, ReopenInput(long.MaxValue));
        await using var d = Db(); var i = await d.Incidents.AsNoTracking().SingleAsync(x => x.IncidentId == closed.Incident);
        await d.Incidents.Where(x => x.IncidentId == i.IncidentId).ExecuteUpdateAsync(x => x.SetProperty(x => x.ClosedAt, (DateTime?)null));
        try { await RejectReopen(closed.Work); } finally { await d.Incidents.Where(x => x.IncidentId == i.IncidentId).ExecuteUpdateAsync(x => x.SetProperty(x => x.ClosedAt, i.ClosedAt)); }
        await d.Incidents.Where(x => x.IncidentId == i.IncidentId).ExecuteUpdateAsync(x => x.SetProperty(x => x.Status, "IN_PROGRESS"));
        try { await RejectReopen(closed.Work); } finally { await d.Incidents.Where(x => x.IncidentId == i.IncidentId).ExecuteUpdateAsync(x => x.SetProperty(x => x.Status, "CLOSED")); }
        await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == closed.Work).ExecuteUpdateAsync(x => x.SetProperty(x => x.IncidentId, (long?)null));
        try { await RejectReopen(closed.Work); } finally { await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == closed.Work).ExecuteUpdateAsync(x => x.SetProperty(x => x.IncidentId, closed.Incident)); }
        var other = new MaintenanceWork { WorkNo = prefix + "AMB", Title = prefix, Status = "CLOSED", IncidentId = i.IncidentId, CreatedBy = actor, CreatedAt = DateTime.UtcNow };
        d.MaintenanceWorks.Add(other); await d.SaveChangesAsync();
        try { await RejectReopen(closed.Work); } finally { await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == other.MaintenanceWorkId).ExecuteDeleteAsync(); d.ChangeTracker.Clear(); }
        var run = new PreventiveMaintenanceRun { MaintenanceWorkId = closed.Work, ScheduleId = await d.PreventiveMaintenanceSchedules.Select(x => x.ScheduleId).FirstAsync(), PlannedAt = DateTime.UtcNow };
        d.PreventiveMaintenanceRuns.Add(run); await d.SaveChangesAsync();
        try
        {
            await RejectReopen(closed.Work);
            await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == closed.Work).ExecuteUpdateAsync(x => x.SetProperty(x => x.IncidentId, (long?)null));
            try { await RejectReopen(closed.Work); }
            finally { await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == closed.Work).ExecuteUpdateAsync(x => x.SetProperty(x => x.IncidentId, closed.Incident)); }
        }
        finally { await d.PreventiveMaintenanceRuns.Where(x => x.RunId == run.RunId).ExecuteDeleteAsync(); d.ChangeTracker.Clear(); }
        // A truthful missing legacy review is preserved, never synthesized from the reopening actor.
        await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == closed.Work).ExecuteUpdateAsync(x => x.SetProperty(x => x.ReviewedAt, (DateTime?)null).SetProperty(x => x.ReviewedBy, (long?)null));
        var reopened = await Reopen(closed.Work); Check((await Cycles(closed.Work)).Items[0].Snapshot.ReviewedBy == null, "Missing legacy review stays unknown");
        await CycleCommand(closed.Work, reopened.CurrentCycleId!.Value, "cancel", verifier);
        Check((await Cycles(closed.Work)).Items[1].TerminalStatus == "CANCELLED", "Managed cancellation finalizes once"); await RejectReopen(closed.Work, ReopenInput(reopened.CurrentCycleId));
        var boundary = await Prepare("CLOSED");
        DateTime end; await using (var read = Db()) end = DateTime.SpecifyKind((await read.Incidents.Where(x => x.IncidentId == boundary.Incident).Select(x => x.ClosedAt).SingleAsync())!.Value, DateTimeKind.Utc).AddDays(maintenanceOptions.ReopenWindowDays);
        var footprint = await Footprint();
        await using (var expired = Db())
        {
            var denied = false; try { await Maintenance(expired, new FixedClock(end.AddTicks(1))).ReopenWorkAsync(boundary.Work, ReopenInput(), default); } catch (System.ComponentModel.DataAnnotations.ValidationException) { denied = true; }
            Check(denied && footprint == await Footprint(), "One tick beyond window rejected atomically");
        }
        await using (var custom = Db())
        {
            var service = new MaintenanceWorkService(custom, new HotelOps.Api.Services.Inventory.InventoryService(custom), Microsoft.Extensions.Options.Options.Create(new MaintenanceOptions { ReopenWindowDays = 1 }), new FixedClock(end.AddDays(-maintenanceOptions.ReopenWindowDays + 2)));
            var denied = false; try { await service.ReopenWorkAsync(boundary.Work, ReopenInput(), default); } catch (System.ComponentModel.DataAnnotations.ValidationException) { denied = true; }
            Check(denied && footprint == await Footprint(), "Service uses configured one-day window, not a magic 30");
        }
        await using (var exact = Db()) Check((await Maintenance(exact, new FixedClock(end)).ReopenWorkAsync(boundary.Work, ReopenInput(), default))!.CurrentCycleNo == 2, "Exact UTC window boundary inclusive");
    }
    async Task ReopenMaterials()
    {
        var source = await IssueSource(); var oldLog = await Use(source, 4); await Command(source.Work, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix }); await Command(source.Work, "verify", new { ActorUserId = verifier, Note = prefix });
        int movements; await using (var d = Db()) movements = await d.StockMovements.CountAsync();
        var opened = await Reopen(source.Work); var cycle = opened.CurrentCycleId!.Value;
        Check((await Cycles(source.Work)).Items[0].MaterialUsageCount == 1, "Old usage grouped through legacy log");
        await ManagedStart(source.Work, cycle);
        var input = new CreateMaintenanceWorkLogRequest { ActorUserId = actor, ExpectedCycleId = cycle, WorkNote = prefix, MaterialUsages = [new(source.Document.Lines[0].LineId, usageUnit.UomId, 5)] };
        var log = await Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{source.Work}/logs", input, 201);
        Check(log.CycleId == cycle && log.MaterialUsages.Single().UsedBaseQty == 5 && (await Available(source)).Items.Single().ConsumedBaseQty == 9, "Material cumulative capacity crosses cycles through logs");
        await RejectUsage(source, new() { ActorUserId = actor, ExpectedCycleId = cycle, WorkNote = prefix, MaterialUsages = [new(source.Document.Lines[0].LineId, usageUnit.UomId, 2)] });
        await RejectUsage(source, UsageInput(source.Document.Lines[0].LineId, 1));
        var ret = await ReturnPost(await ReturnDraft(source, 1), 1); Check((await Available(source)).Total == 0, "RETURN shares cross-cycle consumed cap");
        await ManagedClose(source.Work, cycle); var next = await Reopen(source.Work, cycle); await ManagedStart(source.Work, next.CurrentCycleId!.Value);
        await RejectUsage(source, input);
        await using (var d = Db()) Check(await d.MaintenanceWorkLogs.Where(x => x.LogId == oldLog.LogId).AllAsync(x => x.CycleId == null) && await d.StockMovements.CountAsync() == movements + 1, "Reopen/usage create no stock; old log stays null; only RETURN adds movement");
    }
    async Task ReopenConfiguration()
    {
        Check(maintenanceOptions.ReopenWindowDays == 30, "Repository configured default is 30");
        foreach (var invalid in new[] { "0", "-1", "not-an-integer" })
        {
            var start = new ProcessStartInfo("dotnet", "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll --urls http://127.0.0.1:0") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["Maintenance__ReopenWindowDays"] = invalid; start.Environment["ConnectionStrings__HotelOpsDb"] = connection;
            using var child = Process.Start(start)!;
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25)); Check(child.ExitCode != 0 && ((await output) + (await error)).Contains("ReopenWindowDays"), "Invalid config fails startup: " + invalid); }
            finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
        }
    }
    async Task CycleSqlConstraints(long work, long current)
    {
        var footprint = await Footprint();
        await using (var d = Db())
        {
            d.MaintenanceWorkCycles.Add(new() { MaintenanceWorkId = work, CycleNo = 99, RecordedAt = DateTime.UtcNow, RecordedBy = verifier,
                OpenedAt = DateTime.UtcNow, OpenedBy = verifier, ReopenReason = prefix, StartingWorkStatus = "PENDING", StartingIncidentStatus = "OPEN" });
            var blocked = false; try { await d.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627 && sql.Message.Contains("UX_maintenance_cycles_active")) { blocked = true; }
            Check(blocked && footprint == await Footprint(), "Filtered composite NULL index enforces one active cycle");
        }
        await using (var d = Db())
        {
            d.MaintenanceWorkCycles.Add(new() { MaintenanceWorkId = work, CycleNo = 99, IsLegacySnapshot = true, RecordedAt = DateTime.UtcNow, RecordedBy = verifier, FinalizedAt = DateTime.UtcNow, TerminalStatus = "CLOSED", Title = prefix });
            var blocked = false; try { await d.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number == 547 && sql.Message.Contains("CK_maintenance_cycles_origin")) { blocked = true; }
            Check(blocked && footprint == await Footprint(), "SQL rejects invented legacy cycle number");
        }
        var other = await Prepare("IN_PROGRESS"); footprint = await Footprint();
        await using (var d = Db())
        {
            d.MaintenanceWorkLogs.Add(new() { MaintenanceWorkId = other.Work, CycleId = current, WorkNote = prefix, LoggedBy = actor, LoggedAt = DateTime.UtcNow, Status = "IN_PROGRESS" });
            var blocked = false; try { await d.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number == 547 && sql.Message.Contains("FK_maintenance_logs_cycle_work")) { blocked = true; }
            Check(blocked && footprint == await Footprint(), "SQL composite FK prevents cross-Work cycle log");
        }
    }
}
sealed class FixedClock(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
}
