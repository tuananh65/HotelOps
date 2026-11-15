using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskSeriesUserRace
{
    // The caller owns the verified disposable fixture; Generate invokes the real
    // JOB05 service with its own connection and transaction, not a fake writer.
    internal static async Task RunAsync(HotelOpsDbContext db, long creator, long executor,
        Func<Task<int>> generate, Func<Task<string>> fingerprint, List<object> checks, CancellationToken ct)
    {
        var before = await fingerprint();
        foreach (var target in new[] { (Id: creator, Kind: "creator"), (Id: executor, Kind: "executor") })
        foreach (var change in new[] { "account", "role" })
        {
            var activeRoles = await db.UserRoles.Where(r => r.UserId == target.Id && r.IsActive).Select(r => r.UserRoleId).ToArrayAsync(ct);
            if (activeRoles.Length == 0) throw new Exception("Race fixture must start with active roles.");
            await using var owner = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer(db.Database.GetConnectionString()!).Options);
            await using var transaction = await owner.Database.BeginTransactionAsync(ct);
            Task<int>? pending = null;
            var committed = false;
            try
            {
                await new UserEligibilityLock(owner).RequireEligibleAsync(target.Id, ct);
                var spid = await owner.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                if (change == "account")
                    await owner.Users.Where(u => u.UserId == target.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
                else
                    await owner.UserRoles.Where(r => activeRoles.Contains(r.UserRoleId)).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
                pending = generate();
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
                if (!blocked || pending.IsCompleted) throw new Exception("JOB05 did not wait for " + target.Kind + " " + change + " lock.");
                await transaction.CommitAsync(ct); committed = true;
                var denied = false;
                try { await pending; } catch (ValidationException) { denied = true; }
                if (!denied) throw new Exception("JOB05 failed to reject revoked " + target.Kind + " " + change + ".");
                if (await fingerprint() != before) throw new Exception("JOB05 denial changed persisted workflow.");
                checks.Add(new { seriesRace = target.Kind + "_" + change, userLockWaitObserved = true,
                    validationDenied = true, tasksAssignmentsAuditCursorUnchanged = true });
            }
            finally
            {
                if (!committed) await transaction.RollbackAsync(CancellationToken.None);
                if (pending != null) { try { await pending; } catch { } }
                await db.Users.Where(u => u.UserId == target.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), CancellationToken.None);
                await db.UserRoles.Where(r => activeRoles.Contains(r.UserRoleId)).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), CancellationToken.None);
            }
        }
    }
}
