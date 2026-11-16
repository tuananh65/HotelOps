using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskExecutionUserLock
{
    // Verified disposable caller only. No role/account mutation and no release-fuse override.
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient executor, long user, long task,
        string path, string tag, List<object> checks, CancellationToken ct)
    {
        var token = await executor.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
        request.Headers.TryAddWithoutValidation("If-Match", tag);
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options;
        await using var blocker = new HotelOpsDbContext(options);
        await using var competitor = new HotelOpsDbContext(options);
        await using var observer = new HotelOpsDbContext(options);
        await using var blockedTransaction = await blocker.Database.BeginTransactionAsync(ct);
        await using var competingTransaction = await competitor.Database.BeginTransactionAsync(ct);
        Task<HttpResponseMessage>? pending = null;
        Task? competingLock = null;
        using var competingCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var released = false;
        try
        {
            // This row is reached only after BatchAsync acquired the executor User lock.
            _ = await blocker.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={task}")
                .AsNoTracking().SingleAsync(ct);
            var blockerSpid = await blocker.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
            var competitorSpid = await competitor.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
            pending = executor.SendAsync(request, ct);
            var timer = Stopwatch.StartNew();
            int apiSpid = 0;
            while (timer.Elapsed < TimeSpan.FromSeconds(8) && !pending.IsCompleted)
            {
                apiSpid = await observer.Database.SqlQuery<int>($"SELECT COALESCE(MIN(CONVERT(int,session_id)),0) AS Value FROM sys.dm_exec_requests WHERE blocking_session_id={blockerSpid} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct);
                if (apiSpid != 0) break;
                await Task.Delay(50, ct);
            }
            if (apiSpid == 0 || pending.IsCompleted) throw new Exception("Execution API did not wait on held task row: " + path);
            competingLock = new UserEligibilityLock(competitor).RequireEligibleAsync(user, competingCancellation.Token);
            timer.Restart();
            var observed = false;
            while (timer.Elapsed < TimeSpan.FromSeconds(8) && !competingLock.IsCompleted)
            {
                observed = await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={competitorSpid} AND blocking_session_id={apiSpid} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) == 1;
                if (observed) break;
                await Task.Delay(50, ct);
            }
            if (!observed || competingLock.IsCompleted) throw new Exception("Execution API did not retain User lock before task write: " + path);
            await blockedTransaction.RollbackAsync(ct); released = true;
            using var response = await pending;
            if ((int)response.StatusCode != 200) throw new Exception("Execution failed after lock release: " + (int)response.StatusCode);
            await competingLock;
            var state = await competitor.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.task_progress_versions WHERE task_id={task}").SingleAsync(ct);
            var open = await competitor.WorkIntervals.CountAsync(i => i.UserId == user && i.WorkItemKind == "TASK" && i.WorkItemId == task && i.ClosedAt == null, ct);
            if (state != "IN_PROGRESS" || open != 1) throw new Exception("Competing User lock acquired without complete committed execution state");
            checks.Add(new { executionUserLock = path.EndsWith("/resume", StringComparison.Ordinal) ? "Resume" : "Start",
                taskWaitObserved = true, competingUserLockBlockedByApi = true, httpStatus = 200, committedState = state, openIntervals = open });
        }
        finally
        {
            if (!released) await blockedTransaction.RollbackAsync(CancellationToken.None);
            // Release a competitor that unexpectedly acquired first before draining the API.
            // Otherwise a failed assertion could itself leave the request waiting on this probe.
            competingCancellation.Cancel();
            if (competingLock != null) { try { await competingLock; } catch { } }
            await competingTransaction.RollbackAsync(CancellationToken.None);
            if (pending != null) { try { using var drained = await pending; } catch { } }
        }
    }
}
