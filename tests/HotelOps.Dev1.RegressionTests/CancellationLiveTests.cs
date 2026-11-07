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

public sealed class CancellationLiveFactAttribute : FactAttribute
{
    public CancellationLiveFactAttribute()
    { if (Environment.GetEnvironmentVariable("FS018_LIVE") != "APPROVED_LOCAL_HTTPS") Skip = "Opt-in full API process, trusted local HTTPS/cookie/CSRF/SQL."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class CancellationLiveTests
{
    [CancellationLiveFact]
    public async Task Real_HTTPS_cancellation_authorization_preconditions_replay_and_SQL()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(180)); var ct = budget.Token;
        await using var db = CleaningCancellationSqlTests.Context();
        Assert.True(await CleaningCancellationSqlTests.Publisher(db).CancellationAvailableAsync(ct));
        var f = await CleaningCancellationSqlTests.CreateFixture(db, ct);
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
        info.Environment["ConnectionStrings__HotelOpsDb"] = CleaningCancellationSqlTests.Connection;
        info.Environment["CleaningClosure__Candidate"] = "";
        info.Environment["CleaningCancellation__Candidate"] = "FS018_PROVISIONAL";
        info.Environment["CleaningAssignment__Candidate"] = "";
        info.Environment["Evidence__PrivateRoot"] = Path.Combine(Path.GetTempPath(), "HotelOps-FS018-" + Guid.NewGuid().ToString("N"));
        using var process = Process.Start(info) ?? throw new InvalidOperationException("FS018_API_PROCESS_NOT_STARTED");
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
                if (process.HasExited) throw new InvalidOperationException("FS018_API_STARTUP_EXITED; no credentials or process output logged");
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
            using var metadata = await client.GetAsync(source+"/cancellation",ct);
            Assert.Equal(HttpStatusCode.OK,metadata.StatusCode);
            var options = (await metadata.Content.ReadFromJsonAsync<CleaningCancellationOptions>(ct))!;
            Assert.Equal("PROVISIONAL_V1",options.Capability);
            var tag=metadata.Headers.ETag!.ToString();var key=Guid.NewGuid().ToString();var body=new CancelCleaningRequest(true,"Synthetic live cancellation");
            using var noCsrf=await Post(source+"/cancel",body,tag,key,csrf:false);Assert.Equal(HttpStatusCode.BadRequest,noCsrf.StatusCode);
            using var noTag=await Post(source+"/cancel",body,operation:key);Assert.Equal((HttpStatusCode)428,noTag.StatusCode);
            using var weak=await Post(source+"/cancel",body,"W/"+tag,key);Assert.Equal(HttpStatusCode.BadRequest,weak.StatusCode);
            using var noKey=await Post(source+"/cancel",body,tag);Assert.Equal(HttpStatusCode.BadRequest,noKey.StatusCode);
            using var unconfirmed=await Post(source+"/cancel",body with{Confirmed=false},tag,key);Assert.Equal(HttpStatusCode.BadRequest,unconfirmed.StatusCode);
            using var stale=await Post(source+"/cancel",body,"\"0000000000000000\"",key);Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
            using var wrongBranch=await Post($"/api/branches/{f.Branch+99999}/cleaning-tasks/{f.TaskId}/cancel",body,tag,key);Assert.Equal(HttpStatusCode.Forbidden,wrongBranch.StatusCode);
            using var cancel=await Post(source+"/cancel",body,tag,key);Assert.Equal(HttpStatusCode.OK,cancel.StatusCode);
            var receipt=(await cancel.Content.ReadFromJsonAsync<CleaningCancellationResult>(ct))!;
            using var replay=await Post(source+"/cancel",body,tag,key);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);
            Assert.Equal(receipt,(await replay.Content.ReadFromJsonAsync<CleaningCancellationResult>(ct))!);
            using var conflict=await Post(source+"/cancel",body with{Reason="Changed"},tag,key);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
            Assert.Equal("CANCELLED",await db.CleaningTasks.Where(t=>t.CleaningTaskId==f.TaskId).Select(t=>t.Status).SingleAsync(ct));
            Assert.Equal("DIRTY",await db.Rooms.Where(r=>r.RoomId==f.Room).Select(r=>r.OperationalStatus).SingleAsync(ct));
            Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WHERE publication_id={receipt.PublicationId} AND user_id={f.Assignee} AND status='PENDING'").SingleAsync(ct));
            using var logout=await Post("/api/auth/logout",new{});Assert.Equal(HttpStatusCode.NoContent,logout.StatusCode);
        }
        finally
        {
            if(!process.HasExited)process.Kill(entireProcessTree:true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }
}