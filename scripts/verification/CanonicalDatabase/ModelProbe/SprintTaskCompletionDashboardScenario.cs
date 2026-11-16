using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskCompletionDashboardScenario
{
    internal static async Task VerifyExcludedAsync(HotelOpsDbContext db, HttpClient http, long branch, long task,
        string stage, List<object> checks, CancellationToken ct)
    {
        var row = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        using var response = await http.GetAsync($"/api/dashboards/operations?branchId={branch}&departmentId={row.DepartmentId}&from={today}&to={today}", ct);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if ((int)response.StatusCode != 200 || row.CompletedAt != null
            || body.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("key").GetString() == "averageTaskCompletionHours").GetProperty("value").ValueKind != JsonValueKind.Null)
            throw new Exception("KPI05 must exclude current " + stage + " task despite historical accepted cycles");
        checks.Add(new { taskCompletionCheck = "KPI05 excludes current " + stage + " task; historical Accept is not current completion" });
    }

    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long manager, long branch, long task,
        int expectedCycles, List<object> checks, CancellationToken ct, string username, string password, int expectedTasks = 1)
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { taskCompletionCheck = label, expectedCycles }); }
        var row = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        var role = await db.Roles.Where(r => r.RoleCode == "DEPT_MANAGER").Select(r => r.RoleId).SingleAsync(ct);
        var permission = await db.Permissions.Where(p => p.PermissionCode == "DASHBOARD_VIEW").Select(p => p.PermissionId).SingleAsync(ct);
        if (!await db.RolePermissions.AnyAsync(g => g.RoleId == role && g.PermissionId == permission, ct))
        {
            db.RolePermissions.Add(new() { RoleId = role, PermissionId = permission, DataScope = "BRANCH", GrantedAt = DateTime.UtcNow.AddMinutes(-1) });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        Check(await db.UserRoles.AnyAsync(r => r.UserId == manager && r.RoleId == role && r.BranchId == branch && r.IsActive, ct), "dashboard actor retains scoped Department Head authority");
        var cycles = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_verification_events v JOIN dbo.task_submission_events s ON s.progress_id=v.submission_id WHERE s.task_id={task} AND v.decision='ACCEPT'").SingleAsync(ct);
        Check(cycles == expectedCycles, "public producer persisted expected accepted processing cycles");
        var expectedSeconds = await db.Database.SqlQuery<decimal>($"""
            SELECT SUM(CONVERT(decimal(28,7),DATEDIFF_BIG(nanosecond,first_work.started_at,s.submitted_at))/1000000000) AS Value
            FROM dbo.task_submission_events s JOIN dbo.task_verification_events v ON v.submission_id=s.progress_id AND v.decision='ACCEPT'
            JOIN dbo.operational_tasks t ON t.task_id=s.task_id
            OUTER APPLY(SELECT MAX(r.interval_cutoff) AS cutoff FROM dbo.task_reopen_events r WHERE r.task_id=s.task_id AND r.reopened_at<=s.submitted_at) boundary
            CROSS APPLY(SELECT MIN(i.opened_at) AS started_at FROM dbo.work_intervals i WHERE i.work_item_kind='TASK' AND i.work_item_id=s.task_id AND i.interval_id>COALESCE(boundary.cutoff,0) AND i.interval_id<=s.interval_id) first_work
            WHERE t.branch_id={branch} AND t.department_id={row.DepartmentId} AND t.status='COMPLETED'
              AND t.completed_at>={row.CompletedAt!.Value.Date} AND t.completed_at<{row.CompletedAt.Value.Date.AddDays(1)}
            """).SingleAsync(ct);
        var date = row.CompletedAt!.Value.ToString("yyyy-MM-dd");
        var path = $"/api/dashboards/operations?branchId={branch}&departmentId={row.DepartmentId}&from={date}&to={date}";
        using var response = await http.GetAsync(path, ct);
        Check((int)response.StatusCode == 200, "authorized dashboard returns HTTP200 for real Closed task");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        decimal? Metric(string key) => body.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("key").GetString() == key).GetProperty("value") is var value && value.ValueKind != JsonValueKind.Null ? value.GetDecimal() : null;
        checks.Add(new { taskCompletionMetrics = body.GetProperty("metrics"), missingSources = body.GetProperty("missingSources"), expectedCycles });
        Check(await db.OperationalTasks.CountAsync(t => t.BranchId == branch && t.DepartmentId == row.DepartmentId
            && t.Status == "COMPLETED" && t.CompletedAt >= row.CompletedAt.Value.Date && t.CompletedAt < row.CompletedAt.Value.Date.AddDays(1), ct) == expectedTasks,
            $"{expectedTasks} persisted Closed tasks, never number of processing cycles, is KPI denominator");
        Check(Metric("averageTaskCompletionHours") is decimal actual && Math.Abs(actual - expectedSeconds / expectedTasks / 3600m) < 0.000000001m,
            "KPI05 matches persisted first-Start to accepted-Submit elapsed cycles, excluding verification and Closed gaps");
        var previous = row.CompletedAt.Value.Date.AddDays(-1).ToString("yyyy-MM-dd");
        using var outside = await http.GetAsync(path.Replace("from=" + date, "from=" + previous).Replace("to=" + date, "to=" + previous), ct);
        var empty = await outside.Content.ReadFromJsonAsync<JsonElement>(ct);
        Check((int)outside.StatusCode == 200 && empty.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("key").GetString() == "averageTaskCompletionHours").GetProperty("value").ValueKind == JsonValueKind.Null,
            "period without Closed tasks yields N/A instead of zero or historical cycle count");
        if (expectedCycles < 3) return;
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintTaskCompletionDashboardBrowser.mjs");
        using var browser = Process.Start(start)!;
        try
        {
            var output = browser.StandardOutput.ReadToEndAsync(ct); var error = browser.StandardError.ReadToEndAsync(ct);
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, branch, department = row.DepartmentId, date, expectedCycles, expectedTasks, expectedHours = expectedSeconds / expectedTasks / 3600m }));
            browser.StandardInput.Close(); await browser.WaitForExitAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(await output); _ = await error;
            Check(browser.ExitCode == 0 && result.GetProperty("success").GetBoolean(), $"browser displays KPI05 for {expectedTasks} Closed tasks including a task with {expectedCycles} processing cycles through HTTPS and SQL");
            checks.Add(new { taskCompletionBrowser = result });
        }
        finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
    }
}
