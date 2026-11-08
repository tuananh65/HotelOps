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

public sealed class TurnoverLiveFactAttribute : FactAttribute
{
    public TurnoverLiveFactAttribute()
    { if (Environment.GetEnvironmentVariable("FS017_LIVE") != "APPROVED_LOCAL_HTTPS") Skip = "Opt-in full API process, trusted local HTTPS/cookie/CSRF/SQL."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class TurnoverLiveTests
{
    [TurnoverLiveFact]
    public async Task Real_HTTPS_turnover_reads_existing_trial_sources_without_business_writes()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(180)); var ct = budget.Token;
        await using var db = ReworkWorkflowSqlTests.Context();
        Assert.True(await ReworkWorkflowSqlTests.Publisher(db).ReworkAvailableAsync(ct));
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "fs015.supervisor", ct);
        var task = await db.CleaningTasks.AsNoTracking().Where(t => t.Room.BranchId == user.PrimaryBranchId).OrderByDescending(t => t.CleaningTaskId).FirstAsync(ct);
        var oldHash=user.PasswordHash;
        var businessBefore=await BusinessSnapshot(db,ct);
        // Random synthetic trial credential stays in memory; never stdout, files, assertions or TRX.
        var password = "F15!" + Guid.NewGuid().ToString("N");
        var hash = new PasswordHasher<HotelOps.Api.Entities.User>().HashPassword(user, password);
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
        info.Environment["ConnectionStrings__HotelOpsDb"] = ReworkWorkflowSqlTests.Connection;
        info.Environment["CleaningClosure__Candidate"] = "";
        info.Environment["CleaningRework__Candidate"] = "";
        info.Environment["CleaningAssignment__Candidate"] = "";
        info.Environment["Evidence__PrivateRoot"] = Path.Combine(Path.GetTempPath(), "HotelOps-FS016-" + Guid.NewGuid().ToString("N"));
        using var process = Process.Start(info) ?? throw new InvalidOperationException("FS016_API_PROCESS_NOT_STARTED");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try
        {
            await db.Users.Where(u => u.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash), ct);
            using var handler = new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false };
            // No TLS bypass, callback, custom trust store or mocked HTTP handler.
            using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://localhost:{port}"), Timeout = TimeSpan.FromSeconds(20) };
            JsonElement bootstrap = default;
            var startup = Stopwatch.StartNew();
            while (startup.Elapsed < TimeSpan.FromSeconds(50))
            {
                if (process.HasExited) throw new InvalidOperationException("FS016_API_STARTUP_EXITED; no credentials or process output logged");
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
            var source=$"/api/rooms/{task.RoomId}/turnover-history";
            using var listed=await client.GetAsync(source+$"?branchId={user.PrimaryBranchId}&pageSize=1",ct);
            Assert.Equal(HttpStatusCode.OK,listed.StatusCode);
            var list=await listed.Content.ReadFromJsonAsync<TurnoverPage>(ct);Assert.Single(list!.Items);Assert.Null(list.Items[0].Result);Assert.Equal("NOT_ACCEPTED",list.Coverage.Inspection);
            using var detail=await client.GetAsync(source+$"/{task.CleaningTaskId}?branchId={user.PrimaryBranchId}&timelinePageSize=3",ct);
            Assert.Equal(HttpStatusCode.OK,detail.StatusCode);
            var data=await detail.Content.ReadFromJsonAsync<TurnoverDetail>(ct);Assert.Equal(task.CleaningTaskId,data!.Cycle.Id);Assert.InRange(data.Timeline.Count,1,3);Assert.All(data.Inspections,i=>Assert.Null(i.Result));
            using var foreign=await client.GetAsync(source+$"/{long.MaxValue}?branchId={user.PrimaryBranchId}",ct);Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
            using var denied=await client.GetAsync(source+"?branchId=99999999",ct);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
            using var filter=await client.GetAsync(source+$"?branchId={user.PrimaryBranchId}&result=PASS",ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,filter.StatusCode);
            using var monitor=await client.GetAsync($"/api/branches/{user.PrimaryBranchId}/cleaning-monitor/cleaning/{task.CleaningTaskId}",ct);Assert.Equal(HttpStatusCode.OK,monitor.StatusCode);
            Assert.Equal(businessBefore,await BusinessSnapshot(db,ct));
            using var logout = await Post("/api/auth/logout", new { }); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await db.Users.Where(u=>u.UserId==user.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.PasswordHash,oldHash),CancellationToken.None);
        }
    }
    // Auth/session/login/logout audit writes are expected. Cleaning/Inspection/Rework/WorkTime/readiness/Notification remain unchanged.
    private static Task<string> BusinessSnapshot(HotelOps.Api.Data.HotelOpsDbContext db,CancellationToken ct)=>db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT COUNT(*) FROM dbo.cleaning_tasks) AS tasks,
        (SELECT SUM(CONVERT(bigint,CHECKSUM(*))) FROM dbo.cleaning_tasks) AS task_state,
        (SELECT SUM(CONVERT(bigint,CHECKSUM(*))) FROM dbo.cleaning_reworks) AS rework_state,
        (SELECT SUM(CONVERT(bigint,CHECKSUM(*))) FROM dbo.room_inspections) AS inspections,
        (SELECT SUM(CONVERT(bigint,CHECKSUM(*))) FROM dbo.room_cleaning_cycle_state) AS pointer,
        (SELECT COUNT(*) FROM dbo.cleaning_progress_logs) AS progress,(SELECT COUNT(*) FROM dbo.work_intervals) AS intervals,
        (SELECT COUNT(*) FROM dbo.room_readiness_events) AS readiness,(SELECT COUNT(*) FROM dbo.notification_publications) AS publications,
        (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type IN('cleaning_tasks','cleaning_reworks','room_inspections','work_intervals')) AS business_audit
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);
}
