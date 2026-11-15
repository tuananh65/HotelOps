using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class SprintTaskSeriesScenario
{
    private sealed class TrialClock(DateTimeOffset at) : TimeProvider { public override DateTimeOffset GetUtcNow() => at; }
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient executor, HttpClient managerHttp, long actor, User manager,
        long branch, long foreign, CreateOperationalTaskRequest definition, List<object> checks, CancellationToken ct, string username, string password)
    {
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add(new { seriesCheck = name }); }
        var root = $"/api/branches/{branch}/operational-task-series";
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
        async Task<string> Fingerprint(bool series = true) => JsonSerializer.Serialize(new {
            tasks = await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT * FROM dbo.operational_tasks ORDER BY task_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            definitions = await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT * FROM dbo.operational_task_definitions ORDER BY task_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            criteria = await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT * FROM dbo.task_completion_criteria ORDER BY criterion_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            assignments = await db.OperationalTaskAssignments.AsNoTracking().OrderBy(a => a.AssignmentId).ToArrayAsync(ct),
            progress = await db.OperationalTaskProgresses.AsNoTracking().OrderBy(p => p.ProgressId).ToArrayAsync(ct),
            intervals = await db.WorkIntervals.AsNoTracking().OrderBy(i => i.IntervalId).ToArrayAsync(ct),
            versions = await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT task_id,workflow_status,CONVERT(varchar(16),row_version,2) AS version FROM dbo.task_progress_versions ORDER BY task_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            audits = await db.AuditLogs.AsNoTracking().Where(a => series || a.EntityType != "operational_task_series").OrderBy(a => a.AuditId).ToArrayAsync(ct),
            series = series ? await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT *,CONVERT(varchar(16),row_version,2) AS version FROM dbo.operational_task_series ORDER BY series_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct) : null,
            occurrences = await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT * FROM dbo.task_series_occurrences ORDER BY series_id,due_date FOR JSON PATH),'[]') AS Value").SingleAsync(ct) });
        async Task<int> Generate(long id, DateTimeOffset at)
        {
            await using var fresh = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.OperationalTasks.TaskSeriesGeneration", true)!;
            return await (Task<int>)type.GetMethod("GenerateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Activator.CreateInstance(type, fresh, new TrialClock(at)), [id, ct])!;
        }
        async Task<long[]> Occurrences(long id) => await db.Database.SqlQuery<long>($"SELECT task_id AS Value FROM dbo.task_series_occurrences WHERE series_id={id} ORDER BY due_date").ToArrayAsync(ct);
        async Task<JsonElement> Read(long id)
        {
            var list = await Send(managerHttp, HttpMethod.Get, root);
            Check(list.Status == 200, "manager can read current recurring page");
            return list.Body.GetProperty("items").EnumerateArray().Single(s => s.GetProperty("seriesId").GetInt64() == id).Clone();
        }
        var first = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7).Date.AddDays(1).AddHours(3), DateTimeKind.Utc));
        CreateTaskSeriesRequest Input(DateTimeOffset? due = null, bool finite = false)
        {
            var deadline = due ?? first;
            return new(definition with { RequestKey = Guid.NewGuid(), Title = "Public recurring trial", Description = "First line\nSecond line", RequestedResult = "Reviewed\nRecorded",
                DueAt = deadline, PlannedStartAt = deadline.AddHours(-2) }, new("DAILY", [], null, actor,
                    finite ? DateOnly.FromDateTime(deadline.UtcDateTime.AddHours(7)) : null));
        }
        var input = Input(); var before = await Fingerprint();
        Check((await Send(executor, HttpMethod.Get, root)).Status == 403 && (await Send(executor, HttpMethod.Post, root, input)).Status == 403, "executor cannot create or list manager series");
        Check((await Send(managerHttp, HttpMethod.Post, root, input, csrf: false)).Status == 400, "recurring creation requires CSRF");
        using (var anonymous = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = executor.BaseAddress })
            Check((await Send(anonymous, HttpMethod.Post, root, input)).Status == 401, "anonymous recurring creation denied");
        Check((await Send(managerHttp, HttpMethod.Post, $"/api/branches/{foreign}/operational-task-series", input)).Status == 403, "recurring creation respects branch authority");
        foreach (var bad in new[] { input with { Recurrence = input.Recurrence with { DefaultAssigneeId = long.MaxValue } },
            input with { Recurrence = input.Recurrence with { Rule = "WEEKLY", Weekdays = [] } },
            input with { Recurrence = input.Recurrence with { Rule = "MONTHLY", MonthDay = 32 } },
            input with { Recurrence = input.Recurrence with { EndDate = DateOnly.FromDateTime(first.UtcDateTime.AddHours(7)).AddDays(-1) } },
            input with { Template = input.Template with { DueAt = null } } })
            Check((await Send(managerHttp, HttpMethod.Post, root, bad)).Status == 400, "invalid recurring definition rejected");
        Check(await Fingerprint() == before, "invalid recurring commands preserve task and series storage");
        try {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_SeriesFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51872,'Synthetic recurring audit failure',1; END", ct);
            Check((await Send(managerHttp, HttpMethod.Post, root, input)).Status >= 500, "recurring creation audit failure rejected");
        } finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_SeriesFault", CancellationToken.None); }
        Check(await Fingerprint() == before, "recurring creation failure rolls back series task criteria assignment audit and cursor");
        var creates = await Task.WhenAll(Send(managerHttp, HttpMethod.Post, root, input), Send(managerHttp, HttpMethod.Post, root, input));
        Check(creates.All(r => r.Status == 201), "concurrent public recurring creation succeeds idempotently");
        var id = creates[0].Body.GetProperty("series").GetProperty("seriesId").GetInt64();
        Check(creates[1].Body.GetProperty("series").GetProperty("seriesId").GetInt64() == id && (await Occurrences(id)).Length == 1, "same recurring key creates one series and one due-date occurrence");
        Check((await Send(managerHttp, HttpMethod.Post, root, input with { Template = input.Template with { Title = "Conflicting definition" } })).Status == 409, "recurring key cannot overwrite definition");
        var task = (await Occurrences(id)).Single();
        var managerPlan = await Send(managerHttp, HttpMethod.Get, $"/api/branches/{branch}/operational-tasks/{task}/plan");
        var executorPlan = await Send(executor, HttpMethod.Get, $"/api/branches/{branch}/operational-tasks/{task}/plan");
        Check(managerPlan.Status == 200 && managerPlan.Body.GetProperty("seriesId").GetInt64() == id && executorPlan.Status == 200
            && executorPlan.Body.GetProperty("seriesId").ValueKind == JsonValueKind.Null, "task plan exposes manager Stop Series context without executor series authority");
        var taskRow = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskId == task, ct);
        Check(taskRow.DueAt == first.UtcDateTime && taskRow.PlannedStartAt == first.AddHours(-2).UtcDateTime && taskRow.CreatedBy == manager.UserId && taskRow.Status == "TODO", "generated task retains approved creator plan and physical status");
        Check(await db.OperationalTaskAssignments.AnyAsync(a => a.TaskId == task && a.AssigneeId == actor && a.AssignmentStatus == "ACTIVE" && a.AssignedBy == manager.UserId, ct)
            && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_progress_versions WHERE task_id={task} AND workflow_status='ASSIGNED'").SingleAsync(ct) == 1, "default assignment uses existing sole workflow and responsibility");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_completion_criteria WHERE task_id={task} AND is_required=1 AND outcome IS NULL").SingleAsync(ct) == 1
            && !await db.WorkIntervals.AnyAsync(i => i.WorkItemKind == "TASK" && i.WorkItemId == task, ct), "generated criteria are immutable definitions with no invented work time");
        var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "operational_tasks" && a.EntityId == task.ToString() && a.NewValues!.Contains("TASK_ASSIGNED")).SingleAsync(ct);
        async Task<string> Deliver()
        {
            await using var fresh = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskAssignmentNotificationDelivery", true)!;
            return await (Task<string>)type.GetMethod("DeliverAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type, fresh, TimeProvider.System), [audit.AuditId, ct])!;
        }
        Check((await Task.WhenAll(Deliver(), Deliver())).All(s => s == "DELIVERED"), "generated assignment durable notification accepts concurrent retry");
        var notification = await db.Notifications.SingleAsync(n => n.SourceType == "TASK_ASSIGNMENT" && n.SourceId == audit.AuditId, ct);
        Check(await db.NotificationRecipients.CountAsync(n => n.NotificationId == notification.NotificationId, ct) == 1
            && (await Send(executor, HttpMethod.Get, $"/api/notifications/{notification.NotificationId}/source")).Status == 200, "generated assignment delivers once and authorizes source opening");
        var counts = await Task.WhenAll(Generate(id, DateTimeOffset.UtcNow.AddDays(2)), Generate(id, DateTimeOffset.UtcNow.AddDays(2)));
        Check(counts.Sum() == 2 && counts.Count(n => n == 0) == 1 && (await Occurrences(id)).Length == 3, "simulated future JOB05 concurrent runs create exactly one occurrence per hotel due date");
        Check(await Generate(id, DateTimeOffset.UtcNow.AddDays(2)) == 0, "repeated JOB05 with same simulated horizon creates nothing");
        var cancelledOccurrence = (await Occurrences(id))[1];
        var cancelledPath = $"/api/branches/{branch}/operational-tasks/{cancelledOccurrence}/plan";
        var cancellable = await Send(managerHttp, HttpMethod.Get, cancelledPath);
        Check((await Send(managerHttp, HttpMethod.Post, $"/api/branches/{branch}/operational-tasks/{cancelledOccurrence}/cancel", new { reason = "Cancel only this occurrence" }, cancellable.Tag)).Status == 200,
            "one recurring occurrence can be cancelled without stopping its series");
        var cancelledPlan = await Send(managerHttp, HttpMethod.Get, cancelledPath);
        Check(cancelledPlan.Status == 200 && !cancelledPlan.Body.GetProperty("canManage").GetBoolean() && cancelledPlan.Body.GetProperty("seriesId").ValueKind == JsonValueKind.Null,
            "terminal task plan hides Stop Series under UC103 preconditions");
        var row = await Read(id); var tag = row.GetProperty("eTag").GetString()!;
        Check(row.GetProperty("isActive").GetBoolean() && (await Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Invalid terminal context", taskId = cancelledOccurrence }, tag)).Status == 409,
            "Stop Series rejects a cancelled task context without altering active series");
        Check((await Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Stop trial", taskId = long.MaxValue }, tag)).Status == 404, "Stop cannot bind another task as its notification source");
        Check((await Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Stop trial" })).Status == 428
            && (await Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Stop trial" }, tag, false)).Status == 400, "Stop Series requires strong ETag and CSRF");
        Check((await Send(executor, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Stop trial" }, tag)).Status == 403, "executor cannot stop manager series");
        before = await Fingerprint();
        try {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_SeriesFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51872,'Synthetic stop audit failure',1; END", ct);
            Check((await Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Stop trial", taskId = task }, tag)).Status >= 500, "Stop Series audit failure rejected");
        } finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_SeriesFault", CancellationToken.None); }
        Check(await Fingerprint() == before, "Stop Series audit failure rolls back inactive state and version");
        before = await Fingerprint(false);
        var stops = await Task.WhenAll(Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Routine retired", taskId = task }, tag), Send(managerHttp, HttpMethod.Post, root + $"/{id}/stop", new { reason = "Routine retired", taskId = task }, tag));
        Check(stops.Count(s => s.Status == 200) == 1 && stops.Count(s => s.Status == 412) == 1, "concurrent Stop Series commits once and rejects stale version");
        Check(await Fingerprint(false) == before && await Generate(id, DateTimeOffset.UtcNow.AddDays(10)) == 0, "Stop Series preserves every generated task and blocks future generation");
        var stopAudit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "operational_task_series" && a.EntityId == id.ToString() && a.Action == "UPDATE").SingleAsync(ct);
        async Task<string> DeliverStop()
        {
            await using var fresh = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskSeriesNotificationDelivery", true)!;
            return await (Task<string>)type.GetMethod("DeliverAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type, fresh, TimeProvider.System), [stopAudit.AuditId, ct])!;
        }
        await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try { Check(await DeliverStop() == "RECIPIENT_READ_DENIED", "Stop notification rechecks current recipient authority before delivery"); }
        finally { await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct); }
        Check((await Task.WhenAll(DeliverStop(), DeliverStop())).All(s => s == "DELIVERED"), "Stop notification concurrent retries deliver once");
        var stopNotice = await db.Notifications.SingleAsync(n => n.SourceType == "TASK_SERIES" && n.SourceId == stopAudit.AuditId, ct);
        Check(await db.NotificationRecipients.CountAsync(r => r.NotificationId == stopNotice.NotificationId, ct) == 1 &&
            (await Send(executor, HttpMethod.Get, $"/api/notifications/{stopNotice.NotificationId}/source")).Status == 200, "Stop notification resolves the still-authorized existing task");
        await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try { Check((await Send(executor, HttpMethod.Get, $"/api/notifications/{stopNotice.NotificationId}/source")).Status == 404, "Stop notification source opening rechecks revoked access"); }
        finally { await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct); }
        var future = Input(first.AddDays(4), true);
        var futureCreated = await Send(managerHttp, HttpMethod.Post, root, future);
        Check(futureCreated.Status == 201 && futureCreated.Body.GetProperty("series").GetProperty("occurrenceCount").GetInt32() == 0, "future series waits for its one-day lead horizon");
        var futureId = futureCreated.Body.GetProperty("series").GetProperty("seriesId").GetInt64();
        await SprintTaskSeriesUserRace.RunAsync(db, manager.UserId, actor, () => Generate(futureId, first.AddDays(4)),
            () => Fingerprint(), checks, ct);
        before = await Fingerprint();
        try {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_SeriesFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE JSON_VALUE(new_values,'$.operation')='GENERATE') THROW 51872,'Synthetic JOB05 audit failure',1; END", ct);
            var rejected = false; try { await Generate(futureId, first.AddDays(4)); } catch { rejected = true; }
            Check(rejected, "JOB05 persistence failure surfaces to its isolated series runner");
        } finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_SeriesFault", CancellationToken.None); }
        Check(await Fingerprint() == before, "JOB05 failure rolls back task criteria assignment audit occurrence and next pointer");
        await db.UserRoles.Where(r => r.UserId == manager.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        try { var denied = false; try { await Generate(futureId, first.AddDays(4)); } catch { denied = true; } Check(denied, "JOB05 rechecks initiating manager current role and permission"); }
        finally { await db.UserRoles.Where(r => r.UserId == manager.UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct); }
        await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmploymentStatus, "INACTIVE"), ct);
        try { var denied = false; try { await Generate(futureId, first.AddDays(4)); } catch { denied = true; } Check(denied, "JOB05 rejects inactive default executor without changing cursor"); }
        finally { await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmploymentStatus, "ACTIVE"), ct); }
        Check(await Generate(futureId, first.AddDays(4)) == 1 && await Generate(futureId, first.AddDays(20)) == 0, "JOB05 retries after restored eligibility and honors inclusive series end date");
        row = await Read(futureId);
        Check(row.GetProperty("isActive").GetBoolean() && row.GetProperty("nextDueAt").ValueKind == JsonValueKind.Null, "exhausted series has no next occurrence without inventing a new status");
        var weekly = Input(first.AddDays(14), true);
        weekly = weekly with { Recurrence = weekly.Recurrence with { Rule = "WEEKLY", Weekdays = [(int)weekly.Template.DueAt!.Value.AddHours(7).DayOfWeek] } };
        var monthly = Input(first.AddMonths(2), true);
        monthly = monthly with { Recurrence = monthly.Recurrence with { Rule = "MONTHLY", MonthDay = monthly.Template.DueAt!.Value.AddHours(7).Day } };
        foreach (var calendar in new[] { weekly, monthly }) {
            var created = await Send(managerHttp, HttpMethod.Post, root, calendar);
            Check(created.Status == 201, "public recurring calendar accepted: " + calendar.Recurrence.Rule);
            var calendarId = created.Body.GetProperty("series").GetProperty("seriesId").GetInt64();
            Check(await Generate(calendarId, calendar.Template.DueAt!.Value.AddDays(-1)) == 1 && await Generate(calendarId, calendar.Template.DueAt.Value.AddMonths(3)) == 0, "calendar occurrence respects first deadline and end: " + calendar.Recurrence.Rule);
        }
        var raceInput = Input(first.AddDays(6), true);
        var raceCreated = await Send(managerHttp, HttpMethod.Post, root, raceInput);
        var raceId = raceCreated.Body.GetProperty("series").GetProperty("seriesId").GetInt64();
        var stopping = Send(managerHttp, HttpMethod.Post, root + $"/{raceId}/stop", new { reason = "Concurrent scheduler stop" }, raceCreated.Tag);
        var generating = Generate(raceId, raceInput.Template.DueAt!.Value);
        await Task.WhenAll(stopping, generating);
        Check(stopping.Result.Status is 200 or 412 && generating.Result is 0 or 1, "Stop and JOB05 serialize without partial occurrences");
        if (stopping.Result.Status == 412) { row = await Read(raceId); Check((await Send(managerHttp, HttpMethod.Post, root + $"/{raceId}/stop", new { reason = "Confirmed after generation" }, row.GetProperty("eTag").GetString())).Status == 200, "fresh Stop after committed generation succeeds"); }
        Check(await Generate(raceId, first.AddDays(30)) == 0, "stopped racing series remains stopped on later runs");
        // Exercise the actual worker's discovery/isolation loop with a simulated
        // future scheduling clock. This does not claim a wall-clock 00:45 run.
        var badCategory = new WorkCategory { CategoryCode = "D3-SERIES-FAULT", CategoryName = "Synthetic isolated failure", DepartmentId = definition.DepartmentId, IsActive = true };
        db.WorkCategories.Add(badCategory); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_category_processes(category_id,process_code) VALUES({badCategory.CategoryId},'OPERATIONAL')", ct);
        var badInput = Input(first.AddDays(8), true); badInput = badInput with { Template = badInput.Template with { CategoryId = badCategory.CategoryId } };
        var goodInput = Input(first.AddDays(8), true);
        var badCreated = await Send(managerHttp, HttpMethod.Post, root, badInput); var goodCreated = await Send(managerHttp, HttpMethod.Post, root, goodInput);
        Check(badCreated.Status == 201 && goodCreated.Status == 201, "public series prepared for independent JOB05 failure isolation");
        var badId = badCreated.Body.GetProperty("series").GetProperty("seriesId").GetInt64();
        var goodId = goodCreated.Body.GetProperty("series").GetProperty("seriesId").GetInt64();
        async Task<int> RunWorker()
        {
            var assembly = typeof(HotelOpsDbContext).Assembly;
            var generationType = assembly.GetType("HotelOps.Api.Services.OperationalTasks.TaskSeriesGeneration", true)!;
            var workerType = assembly.GetType("HotelOps.Api.Services.OperationalTasks.TaskSeriesWorker", true)!;
            var optionType = assembly.GetType("HotelOps.Api.Services.OperationalTasks.TaskSeriesOptions", true)!;
            var trialClock = new TrialClock(first.AddDays(8));
            var services = new ServiceCollection();
            services.AddDbContext<HotelOpsDbContext>(options => options.UseSqlServer(db.Database.GetConnectionString()!));
            services.AddScoped(generationType, provider => Activator.CreateInstance(generationType, provider.GetRequiredService<HotelOpsDbContext>(), trialClock)!);
            await using var provider = services.BuildServiceProvider();
            var options = Activator.CreateInstance(typeof(Microsoft.Extensions.Options.OptionsWrapper<>).MakeGenericType(optionType), Activator.CreateInstance(optionType))!;
            var logger = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(workerType).GetField("Instance")!.GetValue(null)!;
            using var worker = (IDisposable)Activator.CreateInstance(workerType, provider.GetRequiredService<IServiceScopeFactory>(), options, trialClock, logger)!;
            return await (Task<int>)workerType.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(worker, [ct])!;
        }
        await db.WorkCategories.Where(c => c.CategoryId == badCategory.CategoryId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct);
        try { Check(await RunWorker() == 1 && (await Occurrences(badId)).Length == 0 && (await Occurrences(goodId)).Length == 1, "actual JOB05 loop with simulated horizon rolls back invalid series and continues eligible peer"); }
        finally { await db.WorkCategories.Where(c => c.CategoryId == badCategory.CategoryId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, true), ct); }
        Check(await RunWorker() == 1 && await RunWorker() == 0 && (await Occurrences(badId)).Length == 1, "JOB05 next-run retry succeeds once after catalog restoration");
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintTaskSeriesBrowser.mjs");
        using var process = Process.Start(start)!;
        try {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { manager = manager.Username, executor = username, password, branch,
                department = definition.DepartmentId, category = definition.CategoryId, priority = definition.PriorityId, assignee = actor, due = first })); process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); using var json = JsonDocument.Parse(await output); _ = await error; checks.Add(json.RootElement.Clone());
            Check(json.RootElement.GetProperty("success").GetBoolean(), "public browser Repeat to generated assignment and Stop Series");
            var browserId = json.RootElement.GetProperty("seriesId").GetInt64();
            Check(await Generate(browserId, first.AddDays(30)) == 0 && (await Occurrences(browserId)).Length == 1, "browser Stop persists SQL and prevents future occurrences");
        } finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
}
