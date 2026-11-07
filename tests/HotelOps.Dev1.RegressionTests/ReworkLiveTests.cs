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

public sealed class ReworkLiveFactAttribute : FactAttribute
{
    public ReworkLiveFactAttribute()
    { if (Environment.GetEnvironmentVariable("FS016_LIVE") != "APPROVED_LOCAL_HTTPS") Skip = "Opt-in full API process, trusted local HTTPS/cookie/CSRF/SQL."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class ReworkLiveTests
{
    [ReworkLiveFact]
    public async Task Real_HTTPS_rework_synthetic_source_accept_start_submit_and_SQL()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(180)); var ct = budget.Token;
        await using var db = ReworkWorkflowSqlTests.Context();
        Assert.True(await ReworkWorkflowSqlTests.Publisher(db).ReworkAvailableAsync(ct));
        var f = await ReworkWorkflowSqlTests.CreateFixture(db, ct);
        // Source is an explicit synthetic trial fixture, not accepted Inspection writer output.
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
        info.Environment["ConnectionStrings__HotelOpsDb"] = ReworkWorkflowSqlTests.Connection;
        info.Environment["CleaningClosure__Candidate"] = "FS015_PROVISIONAL";
        info.Environment["CleaningRework__Candidate"] = "FS016_PROVISIONAL";
        info.Environment["CleaningAssignment__Candidate"] = "";
        info.Environment["Evidence__PrivateRoot"] = Path.Combine(Path.GetTempPath(), "HotelOps-FS016-" + Guid.NewGuid().ToString("N"));
        using var process = Process.Start(info) ?? throw new InvalidOperationException("FS016_API_PROCESS_NOT_STARTED");
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
            var source = $"/api/branches/{f.Branch}/reworks/{f.Id}";
            async Task<(ReworkExecutionDto Data, string Tag)> Metadata()
            {
                using var response=await client.GetAsync(source+"/execution",ct);
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                return ((await response.Content.ReadFromJsonAsync<ReworkExecutionDto>(ct))!,response.Headers.ETag!.ToString());
            }
            var initial=await Metadata();
            Assert.Equal("SYNTHETIC_TEST_ONLY",initial.Data.SourceAuthority);
            var acceptKey=Guid.NewGuid().ToString();var acceptBody=new ReworkActionRequest("Accept synthetic source");
            using var noCsrf=await Post(source+"/accept",acceptBody,initial.Tag,acceptKey,csrf:false);
            Assert.Equal(HttpStatusCode.BadRequest,noCsrf.StatusCode);
            using var noTag=await Post(source+"/accept",acceptBody,operation:acceptKey);
            Assert.Equal((HttpStatusCode)428,noTag.StatusCode);
            using var accept=await Post(source+"/accept",acceptBody,initial.Tag,acceptKey);
            Assert.Equal(HttpStatusCode.OK,accept.StatusCode);
            using var acceptReplay=await Post(source+"/accept",acceptBody,initial.Tag,acceptKey);
            Assert.Equal(HttpStatusCode.OK,acceptReplay.StatusCode);
            var accepted=await Metadata();Assert.Equal("ACCEPTED",accepted.Data.State);
            using var start=await Post(source+"/start",new ReworkActionRequest("Start rework"),accepted.Tag,Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.OK,start.StatusCode);
            var active=await Metadata();Assert.Equal("IN_PROGRESS",active.Data.State);
            using var stale=await Post(source+"/complete",new ReworkActionRequest("Submit",active.Data.Defects.Select(d=>new ReworkRemediation(d.Id,"Fixed")).ToArray()),initial.Tag,Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
            var notes=active.Data.Defects.Select(d=>new ReworkRemediation(d.Id,"Synthetic live remediation")).ToArray();
            notes[0]=notes[0] with{Photos=[new("proof.png","image/png","iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lZ0AAAAASUVORK5CYII=")]};
            var completeBody=new ReworkActionRequest("Submit for reinspection",notes);var key=Guid.NewGuid().ToString();
            using var complete=await Post(source+"/complete",completeBody,active.Tag,key);
            Assert.Equal(HttpStatusCode.OK,complete.StatusCode);
            var receipt=await complete.Content.ReadFromJsonAsync<ReworkCommandResult>(ct);
            Assert.Equal("PENDING_REINSPECTION",receipt!.State);Assert.Equal("PENDING_INSPECTION",receipt.Readiness);
            using var replay=await Post(source+"/complete",completeBody,active.Tag,key);
            Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(receipt,await replay.Content.ReadFromJsonAsync<ReworkCommandResult>(ct));
            var submitted=await Metadata();var file=Assert.Single(submitted.Data.Defects.SelectMany(d=>d.RemediationAttachmentIds));
            using var evidence=await client.GetAsync(source+"/evidence/"+file,ct);Assert.Equal(HttpStatusCode.OK,evidence.StatusCode);
            Assert.Equal("image/png",evidence.Content.Headers.ContentType!.MediaType);
            Assert.Equal(0,await db.WorkIntervals.CountAsync(w=>w.UserId==f.Actor&&w.ClosedAt==null,ct));
            Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_publications WHERE rework_id={f.Id}").SingleAsync(ct));
            Assert.Equal("FAIL",await db.RoomInspections.Where(i=>i.InspectionId==f.InspectionId).Select(i=>i.Result).SingleAsync(ct));
            Assert.Null(await db.CleaningReworks.Where(w=>w.ReworkId==f.Id).Select(w=>w.CompletedAt).SingleAsync(ct));
            using var logout = await Post("/api/auth/logout", new { }); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }
}
