using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ClosureLiveFactAttribute : FactAttribute
{
    public ClosureLiveFactAttribute()
    { if (Environment.GetEnvironmentVariable("FS015_LIVE") != "APPROVED_LOCAL_HTTPS") Skip = "Opt-in full API process, trusted local HTTPS/cookie/CSRF/SQL."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class CleaningClosureLiveTests
{
    [ClosureLiveFact]
    public async Task Real_HTTPS_cookie_CSRF_Report_Resume_Complete_replay_and_SQL()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(180)); var ct = budget.Token;
        await using var db = CleaningClosureWorkflowTests.Context();
        Assert.True(await CleaningClosureWorkflowTests.Publisher(db).ClosureAvailableAsync(ct));
        var f = await CleaningResumeSqlTests.CreateFixtureAsync(db, ct);
        await CleaningClosureWorkflowTests.EnsureSupervisor(db, f.Branch, ct);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == f.Actor, ct);
        // Random synthetic trial credential stays in memory; never stdout, files, assertions or TRX.
        var password = "F15!" + Guid.NewGuid().ToString("N");
        var hash = new PasswordHasher<HotelOps.Api.Entities.User>().HashPassword(user, password);
        await db.Users.Where(u => u.UserId == f.Actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash), ct);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var dll = Path.Combine(root, "src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll");
        Assert.True(File.Exists(dll));
        var reservation = new TcpListener(System.Net.IPAddress.Loopback, 0); reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/HotelOps.Api"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(dll);
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        info.Environment["ASPNETCORE_URLS"] = $"https://localhost:{port}";
        info.Environment["ConnectionStrings__HotelOpsDb"] = CleaningClosureWorkflowTests.Connection;
        info.Environment["CleaningClosure__Candidate"] = "FS015_PROVISIONAL";
        info.Environment["CleaningAssignment__Candidate"] = "";
        info.Environment["Evidence__PrivateRoot"] = Path.Combine(Path.GetTempPath(), "HotelOps-FS015-" + Guid.NewGuid().ToString("N"));
        using var process = Process.Start(info) ?? throw new InvalidOperationException("FS015_API_PROCESS_NOT_STARTED");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try
        {
            using var handler = new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false };
            // No TLS bypass, callback, custom trust store or mocked HTTP handler.
            using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://localhost:{port}"), Timeout = TimeSpan.FromSeconds(20) };
            JsonElement bootstrap = default;
            var startup = Stopwatch.StartNew();
            while (startup.Elapsed < TimeSpan.FromSeconds(50))
            {
                if (process.HasExited) throw new InvalidOperationException("FS015_API_STARTUP_EXITED; no credentials or process output logged");
                try
                {
                    using var response = await client.GetAsync("/api/auth/csrf", ct);
                    if (response.IsSuccessStatusCode) { bootstrap = await response.Content.ReadFromJsonAsync<JsonElement>(ct); break; }
                }
                catch (HttpRequestException) { }
                await Task.Delay(250, ct);
            }
            Assert.Equal(JsonValueKind.Object, bootstrap.ValueKind);
            async Task<HttpResponseMessage> Post(string route, object body, string? tag = null, string? operation = null, bool csrf = true)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
                if (csrf)
                {
                    var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
                    request.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("csrfToken").GetString()!);
                }
                if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
                if (operation != null) request.Headers.Add("Idempotency-Key", operation);
                using (request) return await client.SendAsync(request, ct);
            }
            using var login = await Post("/api/auth/login", new { user.Username, Password = password });
            password = "";
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var source = $"/api/branches/{f.Branch}/cleaning-tasks/{f.TaskId}";
            async Task<(JsonElement Data, string Tag)> Metadata()
            {
                using var response = await client.GetAsync(source + "/execution", ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return (await response.Content.ReadFromJsonAsync<JsonElement>(ct), response.Headers.ETag!.ToString());
            }
            var initial = await Metadata();
            using var noCsrf = await Post(source + "/start", new { resolveBlocker = true, note = "Guest has left" }, initial.Tag, csrf: false);
            Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
            using var noTag = await Post(source + "/start", new { resolveBlocker = true, note = "Guest has left" });
            Assert.Equal((HttpStatusCode)428, noTag.StatusCode);
            using var start = await Post(source + "/start", new { resolveBlocker = true, note = "Guest has left" }, initial.Tag);
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);
            var active = await Metadata();
            Assert.Equal("PROVISIONAL_V1", active.Data.GetProperty("reportBlockerCapability").GetString());
            var reason = active.Data.GetProperty("blockerReasons")[0].GetProperty("id").GetInt64();
            var operation = Guid.NewGuid().ToString();
            var reportBody = new { reasonId = reason, note = "Live trial guest returned", photos = new[] { new EvidenceUpload("proof.png", "image/png", "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lZ0AAAAASUVORK5CYII=") } };
            using var report = await Post(source + "/blockers", reportBody, active.Tag, operation);
            Assert.Equal(HttpStatusCode.OK, report.StatusCode);
            var reportReceipt = await report.Content.ReadFromJsonAsync<CleaningClosureResult>(ct);
            Assert.Equal("ON_HOLD", reportReceipt!.State);
            using var replay = await Post(source + "/blockers", reportBody, active.Tag, operation);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(reportReceipt, await replay.Content.ReadFromJsonAsync<CleaningClosureResult>(ct));
            var onHold = await Metadata();
            using var resume = await Post(source + "/start", new { resolveBlocker = true, note = "Guest has left again" }, onHold.Tag);
            Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
            // Checklist saved through UC032 API with its own authoritative ETag.
            using var progress = await client.GetAsync(source + "/progress", ct);
            Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
            var progressData = await progress.Content.ReadFromJsonAsync<JsonElement>(ct);
            var items = progressData.GetProperty("items").EnumerateArray().Select(i => new { id = i.GetProperty("id").GetInt64(), done = true }).ToArray();
            using var saveRequest = new HttpRequestMessage(HttpMethod.Put, source + "/progress") { Content = JsonContent.Create(new { percent = 100, items, note = "Live cleaned" }) };
            var csrfToken = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            saveRequest.Headers.Add("X-CSRF-TOKEN", csrfToken.GetProperty("csrfToken").GetString());
            saveRequest.Headers.TryAddWithoutValidation("If-Match", progress.Headers.ETag!.ToString());
            using var saved = await client.SendAsync(saveRequest, ct); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var readyToComplete = await Metadata();
            var completeKey = Guid.NewGuid().ToString();
            var completeBody = new { note = "Live room cleaning complete", progressETag = readyToComplete.Data.GetProperty("progressETag").GetString() };
            using var completed = await Post(source + "/complete", completeBody, readyToComplete.Tag, completeKey);
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            var receipt = await completed.Content.ReadFromJsonAsync<CleaningClosureResult>(ct);
            Assert.Equal("PENDING_INSPECTION", receipt!.State); Assert.Equal("PENDING_INSPECTION", receipt.Readiness);
            using var completedReplay = await Post(source + "/complete", completeBody, readyToComplete.Tag, completeKey);
            Assert.Equal(HttpStatusCode.OK, completedReplay.StatusCode);
            Assert.Equal(receipt, await completedReplay.Content.ReadFromJsonAsync<CleaningClosureResult>(ct));
            Assert.Equal(0, await db.WorkIntervals.CountAsync(w => w.UserId == f.Actor && w.ClosedAt == null, ct));
            Assert.Equal(2, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_publications WHERE actor_user_id={f.Actor}").SingleAsync(ct));
            Assert.Equal(1, await db.Attachments.CountAsync(a => a.EntityType == "CLEANING_TASK" && a.EntityId == f.TaskId && a.DeletedAt == null, ct));
            using var logout = await Post("/api/auth/logout", new { }); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }
}
