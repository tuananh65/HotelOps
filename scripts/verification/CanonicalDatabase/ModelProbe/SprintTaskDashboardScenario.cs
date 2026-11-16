using System.Text.Json;
using System.Diagnostics;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskDashboardScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, long actor, long branch,
        Func<HttpMethod, string, object?, string?, bool, Task<(int Status, JsonElement Body)>> send,
        List<object> checks, CancellationToken ct, string username, string password)
    {
        // Parent verifies exact clone identity and owns whole-database cleanup.
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "OPERATIONS_MANAGER" && r.IsActive, ct);
        var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == "DASHBOARD_VIEW", ct);
        if (!await db.RolePermissions.AnyAsync(g => g.RoleId == role.RoleId && g.PermissionId == permission.PermissionId, ct))
            db.RolePermissions.Add(new() { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now });
        var assignment = await db.UserRoles.SingleOrDefaultAsync(r => r.UserId == actor && r.RoleId == role.RoleId && r.BranchId == branch, ct);
        var createdAssignment = assignment == null;
        var wasActive = assignment?.IsActive ?? false;
        if (assignment == null)
        {
            assignment = new UserRole { UserId = actor, RoleId = role.RoleId, BranchId = branch, AssignedAt = now.AddMinutes(-1), IsActive = true };
            db.UserRoles.Add(assignment);
        }
        else assignment.IsActive = true;
        var department = new Department { BranchId = branch, DepartmentCode = "D3KPI" + Guid.NewGuid().ToString("N")[..8], DepartmentName = "Synthetic task dashboard", IsActive = true };
        db.Departments.Add(department); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        void Check(bool value, string label)
        {
            if (!value) throw new Exception(label);
            checks.Add(new { taskDashboardCheck = label });
        }
        var path = $"/api/dashboards/operations?branchId={branch}&departmentId={department.DepartmentId}&from={now.AddDays(-1):yyyy-MM-dd}&to={now:yyyy-MM-dd}";
        async Task<JsonElement> Read()
        {
            var response = await send(HttpMethod.Get, path, null, null, true);
            Check(response.Status == 200, "authorized scoped dashboard HTTP200");
            return response.Body;
        }
        static JsonElement Metric(JsonElement response, string key) => response.GetProperty("metrics").EnumerateArray()
            .Single(m => m.GetProperty("key").GetString() == key).GetProperty("value");
        var empty = await Read();
        Check(Metric(empty, "openTasks").GetInt32() == 0 && Metric(empty, "overdueTasks").GetInt32() == 0,
            "complete empty department reports zero open and overdue tasks");
        async Task<OperationalTask> Seed(string? workflow, DateTime? due, DateTime? closed = null)
        {
            var task = new OperationalTask { TaskCode = "D3KPI" + Guid.NewGuid().ToString("N"), BranchId = branch,
                DepartmentId = department.DepartmentId, Title = "Synthetic lifecycle aggregate", CreatedBy = actor, CreatedAt = now.AddDays(-3),
                Status = workflow == "CLOSED" ? "COMPLETED" : workflow == "CANCELLED" ? "CANCELLED" : workflow == null ? "TODO" : "IN_PROGRESS",
                CompletedAt = closed, DueAt = due };
            db.OperationalTasks.Add(task); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            if (workflow != null)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},{workflow})", ct);
            return task;
        }
        await Seed("PAUSED", now.AddHours(-1));
        await Seed("PENDING_VERIFICATION", now.AddHours(-1));
        var effortTask = await Seed("IN_PROGRESS", now.AddDays(1));
        await Seed("IN_PROGRESS", null);
        await Seed("CANCELLED", now.AddHours(-1));
        await Seed("CLOSED", now.AddHours(-1), now.AddMinutes(-1));
        await Seed("CLOSED", now.AddDays(-5), now.AddDays(-4));
        var day = new Workday { UserId = actor, BranchId = branch, DepartmentId = department.DepartmentId,
            WorkDate = DateOnly.FromDateTime(now.AddHours(7)), Status = "WORKING", IsUnscheduled = true,
            UnscheduledReason = "Synthetic dashboard effort", StartedAt = now.AddHours(-2) };
        db.Workdays.Add(day); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        WorkInterval Interval(int startMinutes, int? endMinutes) => new() { WorkdayId = day.WorkdayId,
            UserId = actor, BranchId = branch, WorkItemKind = "TASK", WorkItemId = effortTask.TaskId,
            OpenedBy = actor, OpenedAt = now.AddMinutes(startMinutes), EffectiveStartedAt = now.AddMinutes(startMinutes),
            ClosedBy = endMinutes == null ? null : actor, ClosedAt = endMinutes == null ? null : now.AddMinutes(endMinutes.Value),
            EffectiveEndedAt = endMinutes == null ? null : now.AddMinutes(endMinutes.Value) };
        db.WorkIntervals.AddRange(Interval(-60, -50), Interval(-40, -20), Interval(-10, null), Interval(-35, -35));
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var boundary = now.AddDays(-1).Date;
        var historicalDay = new Workday { UserId = actor, BranchId = branch, DepartmentId = department.DepartmentId,
            WorkDate = DateOnly.FromDateTime(boundary.AddHours(7)), Status = "ENDED", IsUnscheduled = true,
            UnscheduledReason = "Synthetic UTC boundary", StartedAt = boundary.AddMinutes(-20), EndedAt = boundary.AddMinutes(20) };
        db.Workdays.Add(historicalDay); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var crossing = Interval(-60, -50);
        crossing.WorkdayId = historicalDay.WorkdayId;
        crossing.OpenedAt = crossing.EffectiveStartedAt = boundary.AddMinutes(-10);
        crossing.ClosedAt = crossing.EffectiveEndedAt = boundary.AddMinutes(10);
        db.WorkIntervals.Add(crossing); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var complete = await Read();
        Check(Metric(complete, "taskEffortHours").GetDecimal() == 2m / 3m
            && Metric(complete, "taskProvisionalHours").GetDecimal() >= 1m / 6m
            && Metric(complete, "taskOpenIntervals").GetInt32() == 1,
            "effort clips crossing interval at UTC boundary, excludes pause/workday duration and separates open effort");
        var earlier = await send(HttpMethod.Get,
            $"/api/dashboards/operations?branchId={branch}&departmentId={department.DepartmentId}&from={boundary.AddDays(-1):yyyy-MM-dd}&to={boundary.AddDays(-1):yyyy-MM-dd}", null, null, true);
        Check(earlier.Status == 200 && Metric(earlier.Body, "taskEffortHours").GetDecimal() == 1m / 6m
            && Metric(earlier.Body, "taskOpenIntervals").GetInt32() == 0,
            "opposite UTC period receives only the other ten minutes of crossing interval");
        var future = await send(HttpMethod.Get,
            $"/api/dashboards/operations?branchId={branch}&departmentId={department.DepartmentId}&from={now.AddDays(1):yyyy-MM-dd}&to={now.AddDays(1):yyyy-MM-dd}", null, null, true);
        Check(future.Status == 200 && Metric(future.Body, "taskEffortHours").GetDecimal() == 0
            && Metric(future.Body, "taskProvisionalHours").GetDecimal() == 0 && Metric(future.Body, "taskOpenIntervals").GetInt32() == 0,
            "future period cannot project provisional work beyond server snapshot");
        Check(Metric(complete, "openTasks").GetInt32() == 4 && Metric(complete, "overdueTasks").GetInt32() == 2,
            "Paused and Pending Verification remain open/overdue; Closed and Cancelled excluded; null/future deadlines not overdue");
        Check(Metric(complete, "closedTasksInPeriod").GetInt32() == 1,
            "closed count uses completion period instead of creation period");
        var states = complete.GetProperty("distributions").EnumerateArray()
            .Where(g => g.GetProperty("source").GetString() == "operational_tasks.workflow_status/priority_id")
            .Select(g => g.GetProperty("status").GetString()).ToArray();
        Check(states.Contains("PAUSED") && states.Contains("PENDING_VERIFICATION") && states.Contains("CLOSED"),
            "canonical lifecycle distribution distinguishes physical IN_PROGRESS stages");
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintTaskDashboardBrowser.mjs");
        using (var browser = Process.Start(start) ?? throw new Exception("Dashboard browser unavailable"))
        {
            var output = browser.StandardOutput.ReadToEndAsync(ct);
            var errors = browser.StandardError.ReadToEndAsync(ct);
            try
            {
                await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, branch,
                    department = department.DepartmentId, from = now.AddDays(-1).ToString("yyyy-MM-dd"), to = now.ToString("yyyy-MM-dd") }));
                browser.StandardInput.Close(); await browser.WaitForExitAsync(ct);
                var result = JsonSerializer.Deserialize<JsonElement>(await output); _ = await errors;
                if (browser.ExitCode != 0 || !result.GetProperty("success").GetBoolean())
                    throw new Exception("Dashboard browser failed at " + result.GetProperty("stage").GetString());
                checks.Add(new { taskDashboardBrowser = result });
            }
            finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
        }
        var legacy = await Seed(null, now.AddHours(-1));
        var overlap = Interval(-30, -25);
        db.WorkIntervals.Add(overlap); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var incomplete = await Read();
        Check(Metric(incomplete, "taskEffortHours").ValueKind == JsonValueKind.Null
            && Metric(incomplete, "taskProvisionalHours").ValueKind == JsonValueKind.Null,
            "overlapping intervals withhold effort instead of double counting");
        await db.WorkIntervals.Where(i => i.IntervalId == overlap.IntervalId).ExecuteDeleteAsync(ct);
        Check(Metric(incomplete, "openTasks").ValueKind == JsonValueKind.Null
            && Metric(incomplete, "overdueTasks").ValueKind == JsonValueKind.Null
            && Metric(incomplete, "closedTasksInPeriod").ValueKind == JsonValueKind.Null
            && incomplete.GetProperty("missingSources").EnumerateArray().Any(s => s.GetString()!.Contains("UNKNOWN")),
            "missing canonical lifecycle withholds misleading totals and reports incomplete source");
        var foreign = await db.Branches.Where(b => b.BranchId != branch).Select(b => b.BranchId).FirstAsync(ct);
        var denied = await send(HttpMethod.Get, $"/api/dashboards/operations?branchId={foreign}&departmentId={department.DepartmentId}", null, null, true);
        Check(denied.Status == 403, "foreign dashboard branch cannot expose task aggregate");
        Check(await db.OperationalTasks.CountAsync(t => t.DepartmentId == department.DepartmentId, ct) == 8
            && !await db.AuditLogs.AnyAsync(a => a.EntityType == "operational_tasks" && a.EntityId == legacy.TaskId.ToString(), ct),
            "dashboard reads do not mutate task lifecycle or create business audit events");
        if (createdAssignment)
            await db.UserRoles.Where(r => r.UserRoleId == assignment.UserRoleId).ExecuteDeleteAsync(ct);
        else
            await db.UserRoles.Where(r => r.UserRoleId == assignment.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, wasActive), ct);
    }
}
