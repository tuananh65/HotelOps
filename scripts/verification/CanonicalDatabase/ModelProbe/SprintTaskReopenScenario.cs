using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskReopenScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient executor, HttpClient managerHttp, long actor, User manager,
        User next, long branch, long foreign, long task, List<object> checks, CancellationToken ct, string username, string password)
    {
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add(new { reopenCheck = name }); }
        var root = $"/api/branches/{branch}/operational-tasks/{task}";
        await SprintTaskCompletionDashboardScenario.RunAsync(db, managerHttp, manager.UserId, branch, task, 1, checks, ct, manager.Username, password);
        async Task<(int Status, JsonElement Body, string? Tag)> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? tag = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null) request.Content = JsonContent.Create(body);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (csrf && method != HttpMethod.Get) { var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct); request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString()); }
            using var response = await client.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            return ((int)response.StatusCode, json.RootElement.Clone(), response.Headers.ETag?.ToString());
        }
        async Task<string> Json(string table, string order)
        {
            var allowed = new Dictionary<string, string> { ["operational_task_assignments"] = "assignment_id", ["operational_task_progress"] = "progress_id",
                ["task_progress_versions"] = "task_id", ["task_completion_criteria"] = "criterion_id", ["task_reopen_events"] = "previous_submission_id",
                ["task_submission_events"] = "progress_id", ["task_verification_events"] = "submission_id", ["work_intervals"] = "interval_id" };
            if (!allowed.TryGetValue(table, out var key) || key != order) throw new InvalidOperationException("Unreviewed fingerprint identifier");
            var sql = "SELECT COALESCE((SELECT * FROM dbo." + table + " ORDER BY " + order + " FOR JSON PATH),'[]') AS Value";
            return await db.Database.SqlQueryRaw<string>(sql).SingleAsync(ct);
        }
        async Task<string> Fingerprint() => JsonSerializer.Serialize(new {
            task = await db.OperationalTasks.AsNoTracking().Where(t => t.TaskId == task).Select(t => new { t.Status, t.ActualStartAt, t.CompletedAt, t.ProgressPercent, t.UpdatedAt }).SingleAsync(ct),
            assignment = await Json("operational_task_assignments", "assignment_id"), progress = await Json("operational_task_progress", "progress_id"),
            audit = await db.AuditLogs.CountAsync(ct), versions = await Json("task_progress_versions", "task_id"), criteria = await Json("task_completion_criteria", "criterion_id"),
            cycles = await Json("task_reopen_events", "previous_submission_id"), submissions = await Json("task_submission_events", "progress_id"),
            verification = await Json("task_verification_events", "submission_id"), intervals = await Json("work_intervals", "interval_id") });
        async Task<string> Deliver(long id) {
            await using var fresh = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskReopenNotificationDelivery", true)!;
            return await (Task<string>)type.GetMethod("DeliverAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type, fresh, TimeProvider.System), [id, ct])!;
        }
        using var nextHttp = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = executor.BaseAddress };
        Check((await Send(nextHttp, HttpMethod.Post, "/api/auth/login", new { username = next.Username, password })).Status == 200, "independent verifier session established");
        var read = await Send(managerHttp, HttpMethod.Get, root + "/reopen");
        Check(read.Status == 200 && read.Body.GetProperty("canReopen").GetBoolean() && read.Body.GetProperty("previousAssigneeId").GetInt64() == actor, "public browser Closed task exposes eligible Reopen command");
        var input = new ReopenOperationalTaskRequest(actor, "Result needs further remediation", "Inspect the issue again\nRecord the corrected result");
        var before = await Fingerprint();
        using (var anonymous = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = executor.BaseAddress })
            Check((await Send(anonymous, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status == 401, "anonymous Reopen denied");
        Check((await Send(executor, HttpMethod.Get, root + "/reopen")).Body.GetProperty("canReopen").GetBoolean() == false, "executor may read history without manager command");
        Check((await Send(executor, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status == 403, "executor cannot reopen own Closed task");
        Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input)).Status == 428, "Reopen requires strong ETag");
        Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag, false)).Status == 400, "Reopen requires CSRF");
        Check((await Send(managerHttp, HttpMethod.Post, $"/api/branches/{foreign}/operational-tasks/{task}/reopen", input, read.Tag)).Status == 403, "cross-branch Reopen denied");
        foreach (var bad in new[] { input with { Reason = "short" }, input with { Instructions = " \n " }, input with { Reason = new('r', 4001) }, input with { Instructions = "bad\u0001value" }, input with { AssigneeId = 0 } })
            Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", bad, read.Tag)).Status == 400, "invalid reason instructions or identity rejected");
        Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input with { AssigneeId = manager.UserId }, read.Tag)).Status == 400, "manager without executor role and department rejected as assignee");
        var original = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == actor, ct);
        await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmploymentStatus, "INACTIVE"), ct);
        try { Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status == 400, "inactive executor rejected"); }
        finally { await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmploymentStatus, original.EmploymentStatus), CancellationToken.None); }
        await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try { Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status == 400, "revoked executor role rejected"); }
        finally { await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None); }
        Check(await Fingerprint() == before, "invalid and unauthorized Reopen preserve all workflow tables");
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskReopen.V1'", ct);
        try { Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status == 503, "missing Reopen schema fails closed"); }
        finally { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskReopen.V1',@value=N'1'", CancellationToken.None); }
        foreach (var target in new[] { "audit_logs", "task_completion_criteria" }) {
            before = await Fingerprint();
            try {
                var fault = target == "audit_logs"
                    ? "CREATE TRIGGER dbo.TR_DEV3_ReopenFault ON dbo.audit_logs AFTER INSERT,UPDATE AS BEGIN THROW 51876,'Synthetic Reopen rollback',1; END"
                    : "CREATE TRIGGER dbo.TR_DEV3_ReopenFault ON dbo.task_completion_criteria AFTER INSERT,UPDATE AS BEGIN THROW 51876,'Synthetic Reopen rollback',1; END";
                await db.Database.ExecuteSqlRawAsync(fault, ct);
                Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status >= 500, "Reopen injected failure rejected: " + target);
            } finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_ReopenFault", CancellationToken.None); }
            Check(await Fingerprint() == before, "Reopen atomic rollback after " + target);
        }
        var closedTask = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        var oldResult = await Send(managerHttp, HttpMethod.Get, root + "/submission");
        var oldSubmission = oldResult.Body.GetProperty("id").GetInt64();
        var oldIntervals = await Json("work_intervals", "interval_id");
        var race = await Task.WhenAll(Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag), Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag));
        Check(race.Count(r => r.Status == 200) == 1 && race.Count(r => r.Status == 412) == 1, "same-version concurrent Reopen commits one cycle and one assignment");
        Check((await Send(managerHttp, HttpMethod.Post, root + "/reopen", input, read.Tag)).Status == 412, "retry after ambiguous Reopen cannot create another cycle");
        var progress = await Send(executor, HttpMethod.Get, root + "/progress");
        Check(progress.Status == 200 && progress.Body.GetProperty("state").GetString() == "ASSIGNED"
            && progress.Body.GetProperty("items").EnumerateArray().All(i => i.GetProperty("outcome").ValueKind == JsonValueKind.Null), "new cycle resets current results under explicit criterion guard");
        Check(await db.OperationalTasks.AnyAsync(t => t.TaskId == task && t.Status == "TODO" && t.CompletedAt == null && t.ProgressPercent == 0 && t.ActualStartAt == closedTask.ActualStartAt, ct)
            && await Json("work_intervals", "interval_id") == oldIntervals, "Reopen preserves first actual start and historical intervals without opening work");
        Check((await Send(managerHttp, HttpMethod.Get, root + "/submission")).Body.GetRawText() == oldResult.Body.GetRawText(), "previous completion snapshot remains unchanged");
        await SprintTaskCompletionDashboardScenario.VerifyExcludedAsync(db, managerHttp, branch, task, "Reopened", checks, ct);
        Check((await Send(executor, HttpMethod.Post, root + "/start", tag: progress.Tag)).Status == 409, "Reopen cannot skip Accept");
        var audit = await db.AuditLogs.Where(a => a.EntityType == "operational_tasks" && a.EntityId == task.ToString()).OrderByDescending(a => a.AuditId).FirstAsync(ct);
        await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try { Check(await Deliver(audit.AuditId) == "RECIPIENT_READ_DENIED", "Reopen notification delivery rechecks current recipient authority"); }
        finally { await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None); }
        Check((await Task.WhenAll(Deliver(audit.AuditId), Deliver(audit.AuditId))).All(r => r == "DELIVERED"), "Reopen concurrent notification retries converge");
        var notice = await db.Notifications.SingleAsync(n => n.SourceType == "TASK_REOPEN" && n.SourceId == audit.AuditId, ct);
        Check(await db.NotificationRecipients.CountAsync(n => n.NotificationId == notice.NotificationId, ct) == 1
            && (await Send(executor, HttpMethod.Get, $"/api/notifications/{notice.NotificationId}/source")).Status == 200, "one notification recipient opens currently authorized source");
        Check((await Send(executor, HttpMethod.Post, root + "/accept", tag: progress.Tag)).Status == 200, "reopened executor accepts new cycle");
        progress = await Send(executor, HttpMethod.Get, root + "/progress");
        Check((await Send(executor, HttpMethod.Post, root + "/start", tag: progress.Tag)).Status == 200, "new cycle Start opens a fresh interval under workday guard");
        progress = await Send(executor, HttpMethod.Get, root + "/progress");
        var items = progress.Body.GetProperty("items").EnumerateArray().Select(i => new { id = i.GetProperty("id").GetInt64(), outcome = "PASS", note = "Corrected result" }).ToArray();
        Check((await Send(executor, HttpMethod.Post, root + "/submit", new { items, resultNote = "Second processing cycle corrected", handoverNote = "Independent check required" }, progress.Tag)).Status == 200, "new cycle submits its own result snapshot");
        await SprintTaskCompletionDashboardScenario.VerifyExcludedAsync(db, managerHttp, branch, task, "Pending Verification", checks, ct);
        var verification = await Send(nextHttp, HttpMethod.Get, root + "/verification");
        var returnedSubmission = verification.Body.GetProperty("submissionId").GetInt64();
        Check((await Send(nextHttp, HttpMethod.Post, root + "/verification", new { submissionId = returnedSubmission, decision = "RETURN", note = "Correct the second cycle before acceptance" }, verification.Tag)).Status == 200, "second processing cycle Return retains its original Start boundary");
        await SprintTaskCompletionDashboardScenario.VerifyExcludedAsync(db, managerHttp, branch, task, "Returned", checks, ct);
        progress = await Send(executor, HttpMethod.Get, root + "/progress");
        Check((await Send(executor, HttpMethod.Post, root + "/start", tag: progress.Tag)).Status == 200, "returned task starts another interval within the same processing cycle");
        progress = await Send(executor, HttpMethod.Get, root + "/progress");
        Check((await Send(executor, HttpMethod.Post, root + "/submit", new { items, resultNote = "Second cycle final corrected submission", handoverNote = "Verify corrected work" }, progress.Tag)).Status == 200, "returned cycle resubmits immutable corrected result");
        verification = await Send(nextHttp, HttpMethod.Get, root + "/verification");
        Check(verification.Body.GetProperty("submissionId").GetInt64() != returnedSubmission, "accepted cycle endpoint is a new submission, not the returned snapshot");
        Check((await Send(nextHttp, HttpMethod.Post, root + "/verification", new { submissionId = verification.Body.GetProperty("submissionId").GetInt64(), decision = "ACCEPT", note = "Correction independently checked" }, verification.Tag)).Status == 200, "same-assignee new cycle independently verified Closed");
        Check((await Send(managerHttp, HttpMethod.Get, root + "/submission?submission=" + oldSubmission)).Body.GetProperty("resultNote").GetString() == oldResult.Body.GetProperty("resultNote").GetString(), "old cycle result remains addressable after later submission");
        Check((await Send(managerHttp, HttpMethod.Get, root + "/submission?submission=" + long.MaxValue)).Status == 404, "unknown historical submission fails closed");
        Check(await db.OperationalTasks.AnyAsync(t => t.TaskId == task && t.ActualStartAt == closedTask.ActualStartAt, ct), "first actual start survives second completion");
        await SprintTaskCompletionDashboardScenario.RunAsync(db, managerHttp, manager.UserId, branch, task, 2, checks, ct, manager.Username, password);
        var managerRole = await db.Roles.Where(r => r.RoleCode == "DEPT_MANAGER").Select(r => r.RoleId).SingleAsync(ct);
        var actorManager = new UserRole { UserId = actor, RoleId = managerRole, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddMinutes(-1) };
        db.UserRoles.Add(actorManager); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        // The next browser cycle changes executor. The previous performer can
        // verify only after this proven new-cycle boundary, never on Return.
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintTaskReopenBrowser.mjs");
        using (var process = Process.Start(start)!) { try {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { manager = manager.Username, executor = next.Username, verifier = username, password, branch, task, assignee = next.UserId, oldSubmission, previousResultNote = "Second cycle final corrected submission" })); process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); using var json = JsonDocument.Parse(await output); _ = await error; checks.Add(json.RootElement.Clone());
            Check(json.RootElement.GetProperty("success").GetBoolean(), "browser Reopen new assignee through independent Closed and preserved prior results");
        } finally { if (!process.HasExited) process.Kill(entireProcessTree: true); } }
        var decisions = await db.Database.SqlQuery<string>($"SELECT performed_by_json AS Value FROM dbo.task_verification_events WHERE submission_id IN(SELECT progress_id FROM dbo.task_submission_events WHERE task_id={task}) ORDER BY submission_id").ToArrayAsync(ct);
        Check(decisions.Length == 4 && JsonSerializer.Deserialize<long[]>(decisions[^1])!.SequenceEqual([next.UserId]), "three Accepts and one Return retain new-cycle performer segregation");
        var effort = await db.Database.SqlQuery<long>($"SELECT COALESCE(SUM(DATEDIFF_BIG(second,effective_started_at,effective_ended_at)),0) AS Value FROM dbo.work_intervals WHERE work_item_kind='TASK' AND work_item_id={task}").SingleAsync(ct);
        var latest = await Send(managerHttp, HttpMethod.Get, root + "/verification");
        Check(latest.Body.GetProperty("actualEffortSeconds").GetInt64() == effort && effort >= 0, "actual effort remains cumulative across cycles and excludes paused time");
        Check(await db.OperationalTaskAssignments.CountAsync(a => a.TaskId == task && a.AssignmentStatus == "COMPLETED", ct) == 3
            && !await db.WorkIntervals.AnyAsync(i => i.WorkItemKind == "TASK" && i.WorkItemId == task && i.ClosedAt == null, ct), "three completed historical assignments and no leaked open intervals");
        await db.UserRoles.Where(r => r.UserRoleId == actorManager.UserRoleId).ExecuteDeleteAsync(ct);
        await SprintTaskCompletionDashboardScenario.RunAsync(db, managerHttp, manager.UserId, branch, task, 3, checks, ct, manager.Username, password);
        Check((await Send(executor, HttpMethod.Get, $"/api/notifications/{notice.NotificationId}/source")).Status == 404, "old Reopen notice cannot restore previous executor access after reassignment");
        await SprintTaskWorkloadScenario.RunAsync(db, managerHttp, executor, branch, foreign, task, checks, ct, manager.Username, password, nextHttp, actor);
    }
}
