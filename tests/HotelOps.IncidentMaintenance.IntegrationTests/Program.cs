using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

await new IntegritySuite().Run();

sealed partial class IntegritySuite
{
    readonly string connection = Environment.GetEnvironmentVariable("ConnectionStrings__HotelOpsDb") ?? throw new Exception("Supply ConnectionStrings__HotelOpsDb in process environment.");
    readonly string prefix = "ICITEST_" + Guid.NewGuid().ToString("N")[..12];
    readonly HttpClient http = new() { BaseAddress = new("http://127.0.0.1:5195/api/"), Timeout = TimeSpan.FromSeconds(60) };
    readonly System.Collections.Concurrent.ConcurrentQueue<string> errors = new();
    readonly List<long> owned = [];
    long actor, verifier, branch, severity, priority;
    int assertions, requests, races, faults, forbidden;
    bool verified;
    string? baseline;
    Process? api;
    HotelOpsDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options);
    void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    async Task<T> Send<T>(string method, string path, object? input = null, int status = 200)
    {
        using var message = new HttpRequestMessage(new(method), path); if (input != null) message.Content = JsonContent.Create(input);
        using var response = await http.SendAsync(message); requests++; var text = await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode == status, $"{method} {path}: {(int)response.StatusCode}, expected {status}: {text}\n" + string.Join('\n', errors.TakeLast(30)));
        if (status >= 400) { if (status == 400) Check(JsonDocument.Parse(text).RootElement.GetProperty("status").GetInt32() == 400, "ProblemDetails"); return default!; }
        return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    Task Bad(string method, string path, object input) => Send<object>(method, path, input, 400);
    async Task<IncidentDetailDto> Create()
    {
        var result = await Send<IncidentDetailDto>("POST", "incidents", new CreateIncidentRequest { BranchId = branch, ReportedBy = actor, IncidentTitle = prefix, Description = prefix, DiscoveredAt = DateTime.UtcNow }, 201);
        owned.Add(result.IncidentId); Check(result.Status == "OPEN", "Create OPEN"); return result;
    }
    ChangeIncidentStatusRequest Change(string status, string? note = null) => new() { ActorUserId = actor, Status = status, Note = note };
    Task<IncidentDetailDto> Status(long id, string status, string? note = null) => Send<IncidentDetailDto>("PATCH", $"incidents/{id}/status", Change(status, note));
    Task<MaintenanceWorkDetailDto> Command(long work, string action, object? input = null) => Send<MaintenanceWorkDetailDto>("POST", $"maintenance-works/{work}/{action}", input ?? new { ActorUserId = actor });
    async Task<MaintenanceWorkDetailDto> Pair(long incident) => await Send<MaintenanceWorkDetailDto>("POST", $"incidents/{incident}/maintenance-works", new CreateMaintenanceWorkRequest { ActorUserId = actor, Title = prefix }, 201);
    async Task<MaintenanceWorkDetailDto> Assign(MaintenanceWorkDetailDto w) => await Send<MaintenanceWorkDetailDto>("PATCH", $"maintenance-works/{w.MaintenanceWorkId}/assignment", new AssignMaintenanceWorkRequest { ActorUserId = verifier, AssigneeId = actor });
    async Task AssertPair(long work, string expectedWork, string expectedIncident)
    {
        await using var d = Db(); var w = await d.MaintenanceWorks.AsNoTracking().Include(x => x.Incident).SingleAsync(x => x.MaintenanceWorkId == work);
        Check(w.Status == expectedWork && w.Incident!.Status == expectedIncident, $"Pair {expectedWork}/{expectedIncident}");
    }
    async Task NoOp(long incident, string status)
    {
        var before = await Footprint(); await Status(incident, "  " + status.ToLowerInvariant() + "  "); Check(before == await Footprint(), "Same-status no-op preserves all timestamps/business/history");
    }
    async Task Reject(long incident, string target)
    {
        var before = await Footprint();
        using var response = await http.PatchAsJsonAsync($"incidents/{incident}/status", Change(target, prefix)); requests++;
        var text = await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode == 400 && text.Contains("corrective Maintenance workflow"), "Clear business failure for linked work");
        Check(JsonDocument.Parse(text).RootElement.GetProperty("status").GetInt32() == 400, "Bypass ProblemDetails");
        Check(before == await Footprint(), "Rejected bypass leaves incident/work/log/event/audit/timestamps unchanged"); forbidden++;
    }
    public async Task Run()
    {
        try
        {
            await using (var d = Db())
            {
                Check(await d.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'/'+DB_NAME() AS Value").SingleAsync() == "KIENPC/HotelOpsDB", "Wrong SQL target"); verified = true;
                baseline = await LegacySnapshot();
                var users = await d.Users.Where(x => x.PrimaryBranchId != null && x.DeletedAt == null && x.AccountStatus == "ACTIVE" && x.EmploymentStatus == "ACTIVE").OrderBy(x => x.UserId).ToArrayAsync();
                var group = users.GroupBy(x => x.PrimaryBranchId!.Value).First(x => x.Count() >= 2); branch = group.Key; actor = group.First().UserId; verifier = group.Skip(1).First().UserId;
                severity = await d.ReferenceValues.Where(x => x.GroupCode == "SEVERITY" && x.IsActive).Select(x => x.ReferenceId).FirstAsync();
                priority = await d.ReferenceValues.Where(x => x.GroupCode == "PRIORITY" && x.IsActive).Select(x => x.ReferenceId).FirstAsync();
            }
            var start = new ProcessStartInfo("dotnet", "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll --urls http://127.0.0.1:5195") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["ConnectionStrings__HotelOpsDb"] = connection; start.Environment["Logging__LogLevel__Default"] = "Warning";
            api = Process.Start(start)!; api.OutputDataReceived += (_, e) => { if (e.Data != null) errors.Enqueue(e.Data); }; api.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.Enqueue(e.Data); }; api.BeginOutputReadLine(); api.BeginErrorReadLine();
            var ready = false; for (var i = 0; i < 100; i++) { try { using var r = await http.GetAsync("incidents?pageSize=1"); ready = r.IsSuccessStatusCode; if (ready) break; } catch (HttpRequestException) { } await Task.Delay(100); } Check(ready, "API startup");
            await Generic(); await Lifecycle(); await Drift(); await Concurrency(); await Rollbacks();
            await Send<JsonElement>("GET", "incidents?pageSize=1"); await Send<JsonElement>("GET", "maintenance-works?pageSize=1");
        }
        finally
        {
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); } api?.Dispose();
            if (verified) { await Cleanup(); if (baseline != null) Check(baseline == await LegacySnapshot(), "Legacy/live or other DEV2 data changed"); }
        }
        Console.WriteLine($"PASS requests={requests} assertions={assertions} forbidden_cases={forbidden} SQL_ordered_races={races} rollback_cases={faults}; legacy 15-table snapshot unchanged; owned cleanup complete.");
    }
    async Task Generic()
    {
        var i = await Create(); await NoOp(i.IncidentId, "OPEN");
        foreach (var next in new[] { "RESOLVED", "CLOSED", "UNKNOWN" }) await Bad("PATCH", $"incidents/{i.IncidentId}/status", Change(next, prefix));
        await Bad("PATCH", $"incidents/{i.IncidentId}/status", Change("CANCELLED", "   "));
        var detail = await Status(i.IncidentId, " in_progress "); Check(detail.Status == "IN_PROGRESS" && detail.ResolvedAt == null, "Normalized OPEN to IN_PROGRESS");
        detail = await Status(i.IncidentId, "RESOLVED"); Check(detail.ResolvedAt != null && detail.ClosedAt == null, "Resolution timestamp");
        await Bad("PATCH", $"incidents/{i.IncidentId}/status", Change("IN_PROGRESS")); await Bad("PATCH", $"incidents/{i.IncidentId}/status", Change("CLOSED"));
        await NoOp(i.IncidentId, "RESOLVED"); detail = await Status(i.IncidentId, "IN_PROGRESS", " rework "); Check(detail.ResolvedAt == null, "Return clears resolved time");
        await Status(i.IncidentId, "RESOLVED"); detail = await Status(i.IncidentId, "CLOSED", " closed "); Check(detail.ResolvedAt != null && detail.ClosedAt != null && detail.ClosureNote == "closed", "Closure metadata");
        await NoOp(i.IncidentId, "CLOSED"); await Bad("PATCH", $"incidents/{i.IncidentId}/status", Change("OPEN", prefix));
        i = await Create(); await Status(i.IncidentId, "CANCELLED", prefix); await NoOp(i.IncidentId, "CANCELLED");
        i = await Create(); await Status(i.IncidentId, "IN_PROGRESS"); await Status(i.IncidentId, "CANCELLED", prefix);
        await Send<object>("PATCH", "incidents/9223372036854775807/status", Change("IN_PROGRESS"), 404);
        await using var d = Db(); var id = owned[0].ToString(); var events = await d.IncidentEvents.Where(x => x.IncidentId == owned[0]).OrderBy(x => x.EventId).Select(x => x.ToStatus).ToArrayAsync();
        Check(events.SequenceEqual(new[] { "OPEN", "IN_PROGRESS", "RESOLVED", "IN_PROGRESS", "RESOLVED", "CLOSED" }), "Exact generic event sequence");
        var audits = await d.AuditLogs.Where(x => x.EntityType == "incidents" && x.EntityId == id).OrderBy(x => x.AuditId).ToArrayAsync();
        Check(audits.Length == 6 && audits[0].Action == "CREATE" && audits.Skip(1).All(x => x.Action == "UPDATE" && x.OldValues != null && x.ActorUserId == actor && x.BranchId == branch), "Generic audit sequence/actor/branch/before-after");
    }
    async Task Lifecycle()
    {
        var i = await Create(); var w = await Pair(i.IncidentId); var id = w.MaintenanceWorkId;
        await AssertPair(id, "PENDING", "OPEN"); await NoOp(i.IncidentId, "OPEN"); await Reject(i.IncidentId, "IN_PROGRESS"); await Reject(i.IncidentId, "CANCELLED");
        await Bad("POST", $"incidents/{i.IncidentId}/maintenance-works", new CreateMaintenanceWorkRequest { ActorUserId = actor, Title = prefix });
        await Bad("POST", $"incidents/{i.IncidentId}/maintenance-works", new CreateMaintenanceWorkRequest { ActorUserId = actor, Title = prefix, PlannedStartAt = DateTime.UtcNow, PlannedEndAt = DateTime.UtcNow.AddDays(-1) });
        var triage = await Send<IncidentDetailDto>("PATCH", $"incidents/{i.IncidentId}/triage", new TriageIncidentRequest { ActorUserId = verifier, SeverityId = severity, PriorityId = priority }); Check(triage.Status == "OPEN", "Triage keeps paired OPEN");
        w = await Assign(w); await AssertPair(id, "PENDING", "OPEN"); await Bad("POST", $"maintenance-works/{id}/start", new { ActorUserId = verifier });
        await Command(id, "start"); await AssertPair(id, "IN_PROGRESS", "IN_PROGRESS"); await NoOp(i.IncidentId, "IN_PROGRESS");
        await Reject(i.IncidentId, "RESOLVED"); await Reject(i.IncidentId, "CANCELLED");
        foreach (var action in new[] { "hold", "logs", "submit-result" }) await Bad("POST", $"maintenance-works/{id}/{action}", new { ActorUserId = verifier, Reason = prefix, WorkNote = prefix, ResolutionNote = prefix });
        await Bad("POST", $"maintenance-works/{id}/hold", new { ActorUserId = actor, Reason = "short" });
        await Command(id, "hold", new { ActorUserId = actor, Reason = prefix }); await AssertPair(id, "ON_HOLD", "IN_PROGRESS");
        await Reject(i.IncidentId, "RESOLVED"); await Reject(i.IncidentId, "CANCELLED"); await Bad("POST", $"maintenance-works/{id}/resume", new { ActorUserId = verifier });
        await Command(id, "resume", new { ActorUserId = actor, Note = prefix }); await AssertPair(id, "IN_PROGRESS", "IN_PROGRESS");
        await Send<MaintenanceWorkLogDto>("POST", $"maintenance-works/{id}/logs", new { ActorUserId = actor, WorkNote = prefix, SpentMinutes = 5 }, 201); await AssertPair(id, "IN_PROGRESS", "IN_PROGRESS");
        await Bad("POST", $"maintenance-works/{id}/submit-result", new { ActorUserId = actor, ResolutionNote = " " });
        await Command(id, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix }); await AssertPair(id, "COMPLETED", "RESOLVED"); await NoOp(i.IncidentId, "RESOLVED");
        await Reject(i.IncidentId, "CLOSED"); await Reject(i.IncidentId, "IN_PROGRESS");
        await Bad("POST", $"maintenance-works/{id}/cancel", new { ActorUserId = verifier, Reason = prefix });
        foreach (var action in new[] { "verify", "return" })
        {
            await Bad("POST", $"maintenance-works/{id}/{action}", new { ActorUserId = actor, Note = prefix, Reason = prefix });
            await Bad("POST", $"maintenance-works/{id}/{action}", new { ActorUserId = verifier, Note = "short", Reason = "short" });
        }
        await Command(id, "return", new { ActorUserId = verifier, Reason = prefix }); await AssertPair(id, "IN_PROGRESS", "IN_PROGRESS");
        await using (var d = Db()) { var row = await d.MaintenanceWorks.AsNoTracking().SingleAsync(x => x.MaintenanceWorkId == id); Check(row.ResolvedAt == null && row.ResolutionNote == null && row.ReviewedAt == null && row.ReviewedBy == null, "Return clears result/review metadata"); }
        await Command(id, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix }); await AssertPair(id, "COMPLETED", "RESOLVED");
        await Command(id, "verify", new { ActorUserId = verifier, Note = prefix }); await AssertPair(id, "CLOSED", "CLOSED"); await NoOp(i.IncidentId, "CLOSED");
        await using (var d = Db())
        {
            var row = await d.MaintenanceWorks.AsNoTracking().SingleAsync(x => x.MaintenanceWorkId == id); Check(row.ReviewedBy == verifier && row.ReviewedAt != null && row.ActualStartAt != null, "Review/start metadata");
            var wid = id.ToString(); var iid = i.IncidentId.ToString(); var audits = await d.AuditLogs.Where(x => x.EntityType == "maintenance_works" && x.EntityId == wid || x.EntityType == "incidents" && x.EntityId == iid).ToArrayAsync();
            Check(audits.Count(x => x.EntityType == "maintenance_works") == 9 && audits.Count(x => x.EntityType == "incidents") == 7, "Exact paired lifecycle audit counts including triage");
            Check(audits.All(x => x.BranchId == branch && (x.ActorUserId == actor || x.ActorUserId == verifier)), "Paired audit identity/scope");
        }
        foreach (var state in new[] { "PENDING", "IN_PROGRESS", "ON_HOLD" })
        {
            var pair = await Prepare(state); await Bad("POST", $"maintenance-works/{pair.Work}/cancel", new { ActorUserId = verifier, Reason = "short" });
            await Command(pair.Work, "cancel", new { ActorUserId = verifier, Reason = prefix }); await AssertPair(pair.Work, "CANCELLED", "CANCELLED"); await NoOp(pair.Incident, "CANCELLED");
        }
        var planned = await Create(); w = await Send<MaintenanceWorkDetailDto>("POST", $"incidents/{planned.IncidentId}/maintenance-works", new CreateMaintenanceWorkRequest { ActorUserId = actor, Title = prefix, PlannedStartAt = DateTime.UtcNow.AddDays(1) }, 201);
        await Bad("PATCH", $"maintenance-works/{w.MaintenanceWorkId}/assignment", new AssignMaintenanceWorkRequest { ActorUserId = verifier, AssigneeId = actor, PlannedEndAt = DateTime.UtcNow });
    }
    async Task<(long Incident, long Work)> Prepare(string state)
    {
        var i = await Create(); var w = await Assign(await Pair(i.IncidentId));
        if (state != "PENDING") await Command(w.MaintenanceWorkId, "start");
        if (state == "ON_HOLD") await Command(w.MaintenanceWorkId, "hold", new { ActorUserId = actor, Reason = prefix });
        if (state == "COMPLETED") await Command(w.MaintenanceWorkId, "submit-result", new { ActorUserId = actor, ResolutionNote = prefix });
        return (i.IncidentId, w.MaintenanceWorkId);
    }
    async Task Drift()
    {
        var pair = await Prepare("PENDING");
        await using (var d = Db()) await d.Incidents.Where(x => x.IncidentId == pair.Incident).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "IN_PROGRESS"));
        await Reject(pair.Incident, "RESOLVED"); await Reject(pair.Incident, "OPEN"); await NoOp(pair.Incident, "IN_PROGRESS");
        await Bad("POST", $"maintenance-works/{pair.Work}/start", new { ActorUserId = actor }); await AssertPair(pair.Work, "PENDING", "IN_PROGRESS");
        foreach (var status in new[] { "CLOSED", "CANCELLED" })
        {
            var i = await Create(); var w = await Pair(i.IncidentId);
            // Only owned fixtures; terminal history must not be excluded by the guard.
            await using (var d = Db()) await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == w.MaintenanceWorkId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));
            await Reject(i.IncidentId, "IN_PROGRESS"); await NoOp(i.IncidentId, "OPEN");
        }
        var unknown = await Prepare("PENDING"); var before = await Footprint(); var checkRejected = false;
        try { await using var d = Db(); await d.MaintenanceWorks.Where(x => x.MaintenanceWorkId == unknown.Work).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "LEGACY_UNKNOWN")); }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 547 && e.Message.Contains("CK_maintenance_works_status")) { checkRejected = true; }
        Check(checkRejected && before == await Footprint(), "SQL CHECK prevents unknown persisted status without changing owned row");
        var evidence = new LinkageQueryEvidence();
        await using (var d = Db(evidence))
        {
            var rejected = false; try { await new IncidentService(d).ChangeIncidentStatusAsync(unknown.Incident, Change("IN_PROGRESS"), default); } catch (System.ComponentModel.DataAnnotations.ValidationException) { rejected = true; }
            Check(rejected && evidence.Observed && !evidence.StatusFiltered, "Any-link guard SQL has no active/terminal/unknown status filter");
        }
        await Reject(unknown.Incident, "IN_PROGRESS");
    }
}
