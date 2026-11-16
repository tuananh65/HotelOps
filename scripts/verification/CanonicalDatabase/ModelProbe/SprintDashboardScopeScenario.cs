using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintDashboardScopeScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, List<object> checks, CancellationToken ct, string password)
    {
        // Called only within the parent probe's identity-verified disposable clone.
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { dashboardScopeCheck = label }); }
        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User { EmployeeCode = "D3S" + suffix, Username = "scope_" + suffix, Email = suffix + "@example.invalid",
            FullName = "Synthetic dashboard scope", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        var branches = new[] { "A", "B" }.Select(code => new Branch { BranchCode = "D3S" + code + suffix,
            BranchName = "Synthetic scope " + code, Timezone = "Asia/Ho_Chi_Minh", IsActive = true, CreatedAt = now }).ToArray();
        db.Users.Add(user); db.Branches.AddRange(branches); await db.SaveChangesAsync(ct);
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "OPERATIONS_MANAGER" && r.IsActive, ct);
        foreach (var code in new[] { "DASHBOARD_VIEW", "TASK_VIEW" })
        {
            var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == code, ct);
            if (!await db.RolePermissions.AnyAsync(g => g.RoleId == role.RoleId && g.PermissionId == permission.PermissionId, ct))
                db.RolePermissions.Add(new() { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now });
        }
        var departments = branches.Select(b => new Department { BranchId = b.BranchId, DepartmentCode = "D3S" + suffix,
            DepartmentName = "Synthetic scope department", IsActive = true }).ToArray();
        db.Departments.AddRange(departments);
        foreach (var branch in branches)
        {
            db.UserRoles.Add(new() { UserId = user.UserId, RoleId = role.RoleId, BranchId = branch.BranchId, IsActive = true, AssignedAt = now });
            db.UserBranchAccesses.Add(new() { UserId = user.UserId, BranchId = branch.BranchId, AccessLevel = "STANDARD", GrantedAt = now });
        }
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        foreach (var index in new[] { 0, 0, 1 })
        {
            var task = new OperationalTask { TaskCode = "D3S" + Guid.NewGuid().ToString("N"), Title = "Synthetic closed scope task",
                BranchId = branches[index].BranchId, DepartmentId = departments[index].DepartmentId, CreatedBy = user.UserId,
                CreatedAt = now.AddDays(-501), Status = "COMPLETED", CompletedAt = index == 0 ? now.AddDays(-400) : now.AddMinutes(-1) };
            db.OperationalTasks.Add(task); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},'CLOSED')", ct);
        }
        using var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new("https://localhost:7142") };
        async Task<int> Post(string path, object? body = null)
        {
            var token = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        async Task<(int Status, JsonElement Body)> Read(string path)
        {
            using var response = await http.GetAsync(path, ct);
            return ((int)response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>(ct));
        }
        Check(await Post("/api/auth/login", new { username = user.Username, password }) == 200, "multi-branch actor authenticated through public HTTPS");
        var from = now.AddDays(-500).ToString("yyyy-MM-dd"); var to = now.ToString("yyyy-MM-dd");
        var query = $"from={from}&to={to}";
        var dashboard = await Read("/api/dashboards/operations?" + query);
        Check(dashboard.Status == 200 && dashboard.Body.GetProperty("filters").GetProperty("branchIds").GetArrayLength() == 2
            && dashboard.Body.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("key").GetString() == "closedTasksInPeriod").GetProperty("value").GetInt32() == 3,
            "authorized aggregate includes both branches and closures older than 366 days");
        var scoped = await Read($"/api/dashboards/operations?{query}&departmentId={departments[0].DepartmentId}");
        Check(scoped.Status == 200 && scoped.Body.GetProperty("filters").GetProperty("branchIds").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual(new[] { branches[0].BranchId }),
            "department filter echoes its exact authorized branch intersection");
        for (var i = 0; i < 2; i++)
        {
            var path = $"/api/branches/{branches[i].BranchId}/task-workload/tasks?{query}&calendar=UTC&taskSet=CLOSED_IN_PERIOD";
            var page = await Read(path);
            Check(page.Status == 200 && page.Body.GetProperty("totalCount").GetInt32() == (i == 0 ? 2 : 1)
                && page.Body.GetProperty("from").GetString() == from, "UTC task drill-down preserves long period and branch population");
            Check((await Read(path.Replace("calendar=UTC", "calendar=Asia/Ho_Chi_Minh"))).Status == 400,
                "hotel-calendar one-year guard remains intact");
        }
        async Task Browser(bool revoked)
        {
            var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("frontend/scripts/sprintDashboardScopeBrowser.mjs");
            using var browser = Process.Start(start)!;
            try
            {
                var output = browser.StandardOutput.ReadToEndAsync(ct); var errors = browser.StandardError.ReadToEndAsync(ct);
                await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username = user.Username, password, branches = branches.Select(b => b.BranchId), from, to, revoked }));
                browser.StandardInput.Close(); await browser.WaitForExitAsync(ct);
                var result = JsonSerializer.Deserialize<JsonElement>(await output); _ = await errors;
                checks.Add(new { multiBranchBrowser = result });
                Check(browser.ExitCode == 0 && result.GetProperty("success").GetBoolean(), "multi-branch browser scope/revocation verified; revoked=" + revoked);
            }
            finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
        }
        await Browser(false);
        await db.UserRoles.Where(r => r.UserId == user.UserId && r.BranchId == branches[1].BranchId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        Check((await Read($"/api/branches/{branches[1].BranchId}/task-workload/tasks?{query}&calendar=UTC&taskSet=CLOSED_IN_PERIOD")).Status == 403,
            "revoked branch assignment cannot be restored by a stale multi-branch URL");
        await Browser(true);
        Check(await Post("/api/auth/logout") == 204, "multi-branch probe session revoked through public logout");
    }
}
