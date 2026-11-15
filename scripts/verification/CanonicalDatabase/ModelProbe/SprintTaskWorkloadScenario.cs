using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskWorkloadScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient manager, HttpClient executor,
        long branch, long foreign, long task, List<object> checks, CancellationToken ct, string managerUsername, string password, HttpClient verifier, long actor)
    {
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add(new { workloadCheck = name }); }
        async Task<(int Status, JsonElement Body)> Read(HttpClient http, string path)
        {
            using var response = await http.GetAsync(path, ct);
            return ((int)response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>(ct));
        }
        var row = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        var root = $"/api/branches/{branch}/task-workload";
        var filter = "?search=" + Uri.EscapeDataString(row.TaskCode);
        var auditCount = await db.AuditLogs.CountAsync(ct);
        var read = await Read(manager, root + filter);
        Check(read.Status == 200, "manager workload uses current branch and canonical workflow");
        var items = read.Body.GetProperty("items").EnumerateArray().ToArray();
        Check(items.Length == 2 && items.All(x => x.GetProperty("openTasks").GetInt32() == 0
            && x.GetProperty("closedTasks").GetInt32() == 1), "closed workload preserves each executor's accepted cycles without double counting task per executor");
        foreach (var item in items)
        {
            var user = item.GetProperty("userId").GetInt64();
            var detail = await Read(manager, root + "/" + user + filter);
            Check(detail.Status == 200 && detail.Body.GetProperty("taskCount").GetInt32() == 1, "employee task detail uses historical participation");
            var seconds = await db.Database.SqlQuery<long>($"SELECT COALESCE(SUM(DATEDIFF_BIG(second,effective_started_at,effective_ended_at)),0) AS Value FROM dbo.work_intervals WHERE user_id={user} AND work_item_kind='TASK' AND work_item_id={task} AND closed_at IS NOT NULL").SingleAsync(ct);
            Check(item.GetProperty("closedEffortSeconds").GetInt64() == seconds
                && item.GetProperty("provisionalSeconds").GetInt64() == 0, "closed effort equals persisted per-executor intervals; no provisional time after closure");
            Check(detail.Body.GetProperty("intervals").EnumerateArray().Sum(x => x.GetProperty("closedSeconds").GetInt64()) == seconds,
                "time-log details and workload use the same seconds contract");
        }
        Check((await Read(executor, root + filter)).Status == 403, "own TASK_VIEW cannot read manager workload");
        Check((await Read(manager, $"/api/branches/{foreign}/task-workload")).Status == 403, "foreign branch workload denied");
        Check((await Read(manager, root + "?from=2026-11-16&to=2026-11-15")).Status == 400, "reversed period rejected");
        Check((await Read(manager, root + "?status=COMPLETED")).Status == 400, "physical status cannot replace workflow filter");
        Check((await Read(manager, root + "?pageSize=101")).Status == 400, "oversize pagination rejected");
        var empty = await Read(manager, root + "?search=no-matching-workload-fixture");
        Check(empty.Status == 200 && empty.Body.GetProperty("totalCount").GetInt32() == 0
            && empty.Body.GetProperty("messageCode").GetString() == "MSG10", "empty workload preserves filters and MSG10");
        Check(await db.AuditLogs.CountAsync(ct) == auditCount, "workload and time-log reads do not mutate audit or workflow");
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintTaskWorkloadBrowser.mjs");
        using var process = Process.Start(start)!;
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { manager = managerUsername, password, branch, task })); process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(ct); var errors = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); using var parsed = JsonDocument.Parse(await output); _ = await errors;
            checks.Add(parsed.RootElement.Clone()); Check(parsed.RootElement.GetProperty("success").GetBoolean(), "canonical browser workload filters and employee drill-down use HTTPS and SQL");
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        await SprintTaskWorkloadLifecycleScenario.RunAsync(db, manager, executor, verifier, branch, task, actor, managerUsername, checks, ct);
    }
}
