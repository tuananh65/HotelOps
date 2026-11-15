using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskEntryScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long branch, long foreign,
        long department, long task, long other, List<object> checks, CancellationToken ct, string username, string password)
    {
        var root = $"/api/branches/{branch}/operational-tasks/{task}";
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add(new { entryCheck = name }); }
        async Task<string> Tag()
        {
            using var response = await http.GetAsync(root + "/progress", ct);
            response.EnsureSuccessStatusCode(); return response.Headers.ETag!.ToString();
        }
        async Task<(int Status, string? Code)> Post(string action, string? tag, bool csrf = true, string? path = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path ?? root + "/" + action);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (csrf)
            {
                var token = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
                request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
            }
            using var response = await http.SendAsync(request, ct);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return ((int)response.StatusCode, json.RootElement.TryGetProperty("messageCode", out var code) ? code.GetString() : null);
        }
        async Task<string> Fingerprint()
        {
            var row = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
            return JsonSerializer.Serialize(new { row.Status, row.ActualStartAt, row.UpdatedAt, row.ProgressPercent,
                tag = await Tag(), progress = await db.OperationalTaskProgresses.CountAsync(p => p.TaskId == task, ct),
                intervals = await db.WorkIntervals.CountAsync(i => i.UserId == actor, ct),
                audits = await db.AuditLogs.CountAsync(a => a.ActorUserId == actor && (a.EntityType == "operational_tasks" || a.EntityType == "work_intervals"), ct) });
        }
        var tag = await Tag(); var before = await Fingerprint();
        using (var anonymous = new HttpClient { BaseAddress = http.BaseAddress })
        {
            // Global CSRF runs before cookie authorization. Supply an anonymous
            // antiforgery token to test authentication independently of that guard.
            var anonymousCsrf = await anonymous.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            foreach (var action in new[] { "accept", "start" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, root + "/" + action);
                request.Headers.Add("X-CSRF-TOKEN", anonymousCsrf.GetProperty("csrfToken").GetString());
                using var denied = await anonymous.SendAsync(request, ct);
                Check((int)denied.StatusCode == 401, "unauthenticated " + action + " denied");
            }
        }
        Check((await Post("accept", null)).Status == 428 && (await Post("start", null)).Status == 428, "both mutations require ETag");
        Check((await Post("accept", tag, false)).Status == 400 && (await Post("start", tag, false)).Status == 400, "both mutations require CSRF");
        Check((await Post("accept", tag, path: $"/api/branches/{branch}/operational-tasks/{other}/accept")).Status == 403, "unassigned acceptance denied");
        Check((await Post("start", tag, path: $"/api/branches/{branch}/operational-tasks/{other}/start")).Status == 403, "unassigned start denied");
        Check((await Post("start", tag, path: $"/api/branches/{foreign}/operational-tasks/{task}/start")).Status == 403, "foreign branch denied");
        Check((await Post("start", tag)).Status == 409, "Assigned task cannot skip Accept");
        Check(await Fingerprint() == before, "entry denials do not change task version, history, interval or audit");
        try
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskEntry.V1'", ct);
            Check((await Post("accept", tag)).Status == 503 && (await Post("start", tag)).Status == 503, "missing entry capability fails closed");
        }
        finally { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskEntry.V1',@value=N'1'", CancellationToken.None); }
        await db.UserRoles.Where(r => r.UserId == actor && r.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try { Check((await Post("accept", tag)).Status == 403 && (await Post("start", tag)).Status == 403, "revoked executor permission denies both entry operations"); }
        finally { await db.UserRoles.Where(r => r.UserId == actor && r.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None); }
        await db.Departments.Where(d => d.DepartmentId == department).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), ct);
        try { Check((await Post("accept", tag)).Status == 409, "inactive source department denies acceptance"); }
        finally { await db.Departments.Where(d => d.DepartmentId == department).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, true), CancellationToken.None); }
        using (var request = new HttpRequestMessage(HttpMethod.Put, root + "/progress") { Content = JsonContent.Create(new { percent = 40, items = Array.Empty<object>(), note = "Must not save before Start" }) })
        {
            request.Headers.TryAddWithoutValidation("If-Match", tag);
            var token = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
            using var denied = await http.SendAsync(request, ct);
            Check((int)denied.StatusCode == 409, "prestart progress mutation refused server-side");
        }
        before = await Fingerprint();
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskEntryFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='operational_tasks' AND new_values LIKE '%ACCEPT%') THROW 51864,'Synthetic task acceptance audit failure',1; END", ct);
            Check((await Post("accept", tag)).Status >= 500 && await Fingerprint() == before, "failed acceptance audit rolls back state, version, parent and history");
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskEntryFault", CancellationToken.None); }
        var race = await Task.WhenAll(Post("accept", tag), Post("accept", tag));
        Check(race.Select(x => x.Status).Order().SequenceEqual(new[] { 200, 412 }), "concurrent Accept produces exactly one success and one stale response");
        Check((await Post("accept", await Tag())).Status == 409, "fresh retry cannot accept twice");
        Check(!await db.WorkIntervals.AnyAsync(ct) && (await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct)).ActualStartAt == null,
            "Accept does not start time or require a workday");
        tag = await Tag(); before = await Fingerprint();
        Check(await Post("start", tag) == (409, "MSG30"), "Start requires Working workday and returns MSG30");
        Check(await Fingerprint() == before, "missing workday rolls back workflow and parent writes");
        var day = new Workday { UserId = actor, BranchId = branch, DepartmentId = department,
            WorkDate = DateOnly.FromDateTime(DateTime.UtcNow), IsUnscheduled = true,
            UnscheduledReason = "Synthetic isolated task entry trial", Status = "WORKING", StartedAt = DateTime.UtcNow.AddMinutes(-5) };
        db.Workdays.Add(day); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var occupied = new WorkInterval { WorkdayId = day.WorkdayId, UserId = actor, BranchId = branch,
            WorkItemKind = "TASK", WorkItemId = other, OpenedBy = actor, OpenedAt = DateTime.UtcNow.AddMinutes(-1), EffectiveStartedAt = DateTime.UtcNow.AddMinutes(-1) };
        db.WorkIntervals.Add(occupied); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        before = await Fingerprint();
        Check(await Post("start", tag) == (409, "MSG31") && await Fingerprint() == before, "another open interval returns MSG31 and rolls back all writes");
        await db.WorkIntervals.Where(i => i.IntervalId == occupied.IntervalId).ExecuteDeleteAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='COMPLETED' WHERE task_id={task}", ct);
        Check((await Post("start", tag)).Status == 403, "lost current responsibility prevents Start");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_assignments SET ended_at=NULL,assignment_status='ACTIVE' WHERE task_id={task}", ct);
        Check((await Post("start", tag)).Status == 412, "assignment changes invalidate the old ETag");
        tag = await Tag(); before = await Fingerprint();
        // Owned fixture only; the trigger is always removed, even on a failed assertion.
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskEntryFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='operational_tasks' AND new_values LIKE '%START%') THROW 51864,'Synthetic task entry audit failure',1; END", ct);
            Check((await Post("start", tag)).Status >= 500, "injected final task audit failure rejects Start");
            Check(await Fingerprint() == before, "audit failure rolls back workflow, first start, progress, interval and both audits");
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskEntryFault", CancellationToken.None); }
        race = await Task.WhenAll(Post("start", tag), Post("start", tag));
        Check(race.Select(x => x.Status).Order().SequenceEqual(new[] { 200, 412 }), "concurrent Start creates exactly one interval");
        var interval = await db.WorkIntervals.AsNoTracking().SingleAsync(i => i.UserId == actor, ct);
        var started = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        Check(started.Status == "IN_PROGRESS" && started.ActualStartAt == interval.OpenedAt && interval.EffectiveStartedAt == interval.OpenedAt
            && interval.ClosedAt == null && interval.WorkItemId == task, "first actual start equals authoritative server interval timestamp");
        Check((await Post("start", await Tag())).Status == 409 && (await Post("accept", await Tag())).Status == 409, "running task rejects fresh Accept and Start retries");
        Check(await db.AuditLogs.CountAsync(a => a.ActorUserId == actor && a.EntityType == "operational_tasks", ct) == 2
            && await db.AuditLogs.CountAsync(a => a.ActorUserId == actor && a.EntityType == "work_intervals", ct) == 1,
            "exactly two entry audits and one interval audit persist");
        var hkRole = await db.Roles.Where(r => r.RoleCode == "HK_STAFF" && r.IsActive).Select(r => r.RoleId).SingleAsync(ct);
        foreach (var roleCode in new[] { "MAINTENANCE_STAFF", "STOREKEEPER" })
        {
            // Each role uses its existing catalog permission; no catalog or role ID is invented.
            var role = await db.Roles.Where(r => r.RoleCode == roleCode && r.IsActive).Select(r => r.RoleId).SingleAsync(ct);
            await db.WorkIntervals.Where(i => i.UserId == actor).ExecuteDeleteAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status='TODO',actual_start_at=NULL WHERE task_id={task}; UPDATE dbo.task_progress_versions SET workflow_status='ASSIGNED' WHERE task_id={task}", ct);
            await db.UserRoles.Where(r => r.UserId == actor && r.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleId, role), ct);
            try
            {
                Check((await Post("accept", await Tag())).Status == 200 && (await Post("start", await Tag())).Status == 200,
                    roleCode + " accepts and starts under its real scoped executor permission");
            }
            finally { await db.UserRoles.Where(r => r.UserId == actor && r.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleId, hkRole), CancellationToken.None); }
        }
        // UC108 sets actual start only the first time: an Accepted task may
        // retain it after a future UC105 Return. This is a synthetic precondition,
        // not a claim that Return itself has been implemented.
        await db.WorkIntervals.Where(i => i.UserId == actor).ExecuteDeleteAsync(ct);
        var firstStart = (await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct)).ActualStartAt;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status='TODO' WHERE task_id={task}; UPDATE dbo.task_progress_versions SET workflow_status='ACCEPTED' WHERE task_id={task}", ct);
        Check((await Post("start", await Tag())).Status == 200
            && (await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct)).ActualStartAt == firstStart,
            "starting an Accepted task with a prior first start preserves that timestamp");
        // Reset synthetic task to its stated precondition for an independent real browser path.
        await db.WorkIntervals.Where(i => i.UserId == actor).ExecuteDeleteAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status='TODO',actual_start_at=NULL WHERE task_id={task}; UPDATE dbo.task_progress_versions SET workflow_status='ASSIGNED' WHERE task_id={task}", ct);
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintTaskEntryBrowser.mjs");
        using var process = Process.Start(start) ?? throw new Exception("Browser unavailable");
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, branch, code = started.TaskCode, expectCriteria = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_completion_criteria WHERE task_id={task}").SingleAsync(ct) > 0 })); process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); using var json = JsonDocument.Parse(await stdout); _ = await stderr;
            checks.Add(json.RootElement.Clone()); Check(json.RootElement.GetProperty("success").GetBoolean(), "canonical browser Accept/Start/Progress/Pause/Resume workflow accepted");
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == actor).OrderBy(i => i.OpenedAt).ToArrayAsync(ct);
        started = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        Check(intervals.Length == 2 && intervals[0].ClosedAt != null && intervals[1].ClosedAt == null
            && started.ActualStartAt == intervals[0].OpenedAt && started.ProgressPercent == 40,
            "browser persists progress, two work intervals and original first start across pause/resume");
    }
}
