using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskWorkloadLifecycleScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient manager, HttpClient executor, HttpClient verifier,
        long branch, long task, long actor, string managerUsername, List<object> checks, CancellationToken ct)
    {
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add(new { workloadCheck = name }); }
        var row = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        var root = $"/api/branches/{branch}/operational-tasks/{task}";
        var workload = $"/api/branches/{branch}/task-workload?search=" + Uri.EscapeDataString(row.TaskCode);
        async Task<JsonElement> Read(string path)
        {
            using var response = await manager.GetAsync(path, ct);
            Check(response.IsSuccessStatusCode, "scoped workload query succeeds: " + path[(path.IndexOf('?') + 1)..]);
            return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        async Task<int> Post(HttpClient http, string suffix, string readSuffix, object? body = null)
        {
            using var current = await http.GetAsync(root + readSuffix, ct); current.EnsureSuccessStatusCode();
            var csrf = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, root + suffix);
            request.Headers.TryAddWithoutValidation("If-Match", current.Headers.ETag!.ToString());
            request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("csrfToken").GetString());
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        var first = await Read(workload + "&pageSize=1");
        var second = await Read(workload + "&pageSize=1&page=2");
        Check(first.GetProperty("totalCount").GetInt32() == 2 && first.GetProperty("items")[0].GetProperty("userId").GetInt64()
            != second.GetProperty("items")[0].GetProperty("userId").GetInt64(), "stable employee pagination has no duplicate boundary rows");
        var filtered = await Read(workload + $"&departmentId={row.DepartmentId}&priorityId={row.PriorityId}&employeeId={actor}");
        Check(filtered.GetProperty("totalCount").GetInt32() == 1, "department priority and employee filters intersect within branch");
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1).ToString("yyyy-MM-dd");
        Check((await Read(workload + $"&from={tomorrow}&to={tomorrow}")).GetProperty("totalCount").GetInt32() == 0, "future period does not borrow closed historical effort");
        var managerId = await db.Users.Where(u => u.Username == managerUsername).Select(u => u.UserId).SingleAsync(ct);
        var managerAssignment = await db.UserRoles.AsNoTracking().SingleAsync(r => r.UserId == managerId && r.Role.RoleCode == "DEPT_MANAGER", ct);
        var operationsRole = await db.Roles.Where(r => r.RoleCode == "OPERATIONS_MANAGER").Select(r => r.RoleId).SingleAsync(ct);
        var operations = new UserRole { UserId = managerId, RoleId = operationsRole, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddMinutes(-1) };
        db.UserRoles.Add(operations); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.UserRoles.Where(r => r.UserRoleId == managerAssignment.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try
        {
            Check((await Read(workload)).GetProperty("totalCount").GetInt32() == 2, "Operations Manager with existing TASK_VIEW branch grant is allowed by SRS113");
            await db.UserRoles.Where(r => r.UserRoleId == operations.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            using var denied = await manager.GetAsync(workload, ct); Check((int)denied.StatusCode == 403, "revoked manager roles deny workload without stale session authority");
        }
        finally
        {
            await db.UserRoles.Where(r => r.UserRoleId == managerAssignment.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, managerAssignment.IsActive), CancellationToken.None);
            await db.UserRoles.Where(r => r.UserRoleId == operations.UserRoleId).ExecuteDeleteAsync(CancellationToken.None);
        }
        var shiftGrant = await db.RolePermissions.AsNoTracking().SingleAsync(p => p.RoleId == managerAssignment.RoleId && p.Permission.PermissionCode == "SHIFT_VIEW", ct);
        await db.RolePermissions.Where(p => p.RolePermissionId == shiftGrant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DataScope, "OWN"), ct);
        try
        {
            var scoped = await Read($"/api/branches/{branch}/task-workload/{actor}?search=" + Uri.EscapeDataString(row.TaskCode));
            Check(scoped.GetProperty("shifts").ValueKind == JsonValueKind.Null, "TASK_VIEW does not grant branch shift-time access");
        }
        finally { await db.RolePermissions.Where(p => p.RolePermissionId == shiftGrant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DataScope, shiftGrant.DataScope), CancellationToken.None); }

        Check(await Post(manager, "/reopen", "/reopen", new { assigneeId = actor, reason = "Verify live workload interval", instructions = "Complete independent workload acceptance" }) == 200, "public Reopen begins live workload fixture");
        Check(await Post(executor, "/accept", "/progress") == 200 && await Post(executor, "/start", "/progress") == 200, "public Accept Start opens live provisional interval");
        await Task.Delay(1100, ct);
        var open = (await Read(workload)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("userId").GetInt64() == actor);
        Check(open.GetProperty("openTasks").GetInt32() == 1 && open.GetProperty("provisionalSeconds").GetInt64() > 0,
            "open interval contributes provisional time and open workload separately");
        // Synthetic overdue read fixture only; not evidence for a deadline mutation UC.
        await db.OperationalTasks.Where(t => t.TaskId == task).ExecuteUpdateAsync(s => s.SetProperty(t => t.DueAt, DateTime.UtcNow.AddSeconds(-5)), ct);
        try
        {
            var overdue = (await Read(workload + "&overdueOnly=true&status=IN_PROGRESS")).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("userId").GetInt64() == actor);
            Check(overdue.GetProperty("overdueTasks").GetInt32() == 1, "GBR135 flags an open past-deadline task");
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd");
            Check((await Read(workload + $"&deadlineFrom={today}&deadlineTo={today}")).GetProperty("totalCount").GetInt32() == 2,
                "hotel-date deadline filter includes the effective deadline");
        }
        finally { await db.OperationalTasks.Where(t => t.TaskId == task).ExecuteUpdateAsync(s => s.SetProperty(t => t.DueAt, row.DueAt), CancellationToken.None); }
        var progress = await executor.GetFromJsonAsync<JsonElement>(root + "/progress", ct);
        var criteria = progress.GetProperty("items").EnumerateArray().Select(i => new { id = i.GetProperty("id").GetInt64(), outcome = "PASS", note = "Workload cycle checked" }).ToArray();
        Check(await Post(executor, "/submit", "/progress", new { items = criteria, resultNote = "Fourth cycle workload acceptance", handoverNote = "Independent review" }) == 200, "live workload cycle closes interval through public Submit");
        var verification = await verifier.GetFromJsonAsync<JsonElement>(root + "/verification", ct);
        Check(await Post(verifier, "/verification", "/verification", new { submissionId = verification.GetProperty("submissionId").GetInt64(), decision = "ACCEPT", note = "Workload cycle independently verified" }) == 200, "live workload fixture independently closes");
        var final = (await Read(workload)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("userId").GetInt64() == actor);
        Check(final.GetProperty("provisionalSeconds").GetInt64() == 0 && final.GetProperty("closedTasks").GetInt32() == 1
            && final.GetProperty("closedEffortSeconds").GetInt64() > open.GetProperty("closedEffortSeconds").GetInt64(),
            "closing converts provisional to recorded effort without duplicating distinct task count");
    }
}
