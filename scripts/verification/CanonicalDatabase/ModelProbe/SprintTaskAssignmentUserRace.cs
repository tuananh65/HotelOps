using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

// Called only inside the identity-verified disposable task fixture. These are
// assignment writer races, not acceptance of the unreleased deactivation API.
internal static class SprintTaskAssignmentUserRace
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient manager, long assignee,
        string path, string tag, Func<Task<string>> fingerprint, List<object> checks, CancellationToken ct)
    {
        var before = await fingerprint();
        foreach (var change in new[] { "account", "role" })
        {
            var csrf = await manager.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Content = JsonContent.Create(new { assigneeId = assignee });
            request.Headers.TryAddWithoutValidation("If-Match", tag);
            request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("csrfToken").GetString());
            await using var owner = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer(db.Database.GetConnectionString()!).Options);
            await using var transaction = await owner.Database.BeginTransactionAsync(ct);
            Task<HttpResponseMessage>? pending = null;
            var committed = false;
            try
            {
                await new UserEligibilityLock(owner).RequireEligibleAsync(assignee, ct);
                var spid = await owner.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                if (change == "account")
                    await owner.Users.Where(u => u.UserId == assignee).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
                else
                    await owner.UserRoles.Where(r => r.UserId == assignee).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                pending = manager.SendAsync(request, ct);
                var elapsed = Stopwatch.StartNew();
                var blocked = false;
                while (elapsed.Elapsed < TimeSpan.FromSeconds(8) && !pending.IsCompleted)
                {
                    blocked = await owner.Database.SqlQuery<int>($"""
                        SELECT COUNT(*) AS Value FROM sys.dm_exec_requests
                        WHERE blocking_session_id={spid} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'
                        """).SingleAsync(ct) > 0;
                    if (blocked) break;
                    await Task.Delay(50, ct);
                }
                if (!blocked || pending.IsCompleted) throw new Exception("Assignment did not demonstrably wait for " + change + " User lock.");
                await transaction.CommitAsync(ct);
                committed = true;
                using var response = await pending;
                if ((int)response.StatusCode != 400) throw new Exception("Assignment accepted changed " + change + " eligibility after lock wait: " + (int)response.StatusCode);
                if (await fingerprint() != before) throw new Exception("Denied assignment changed task state after " + change + " race.");
                checks.Add(new { assignmentRace = change, userLockWaitObserved = true, httpStatus = 400, taskUnchanged = true });
            }
            finally
            {
                if (!committed) await transaction.RollbackAsync(CancellationToken.None);
                // Drain the request before restoring fixture authority so a late
                // writer cannot see restoration and accidentally succeed.
                if (pending != null) { try { using var drained = await pending; } catch { } }
                await db.Users.Where(u => u.UserId == assignee).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), CancellationToken.None);
                await db.UserRoles.Where(r => r.UserId == assignee).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None);
            }
        }
    }
}
