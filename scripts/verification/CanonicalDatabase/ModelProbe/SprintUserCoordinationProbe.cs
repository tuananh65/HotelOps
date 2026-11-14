using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

internal static class SprintUserCoordinationProbe
{
    private static HotelOpsDbContext Context()=>new(new DbContextOptionsBuilder<HotelOpsDbContext>()
        .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
    internal static async Task<int> RunAsync(string report)
    {
        if(File.Exists(report))throw new IOException("Evidence already exists");
        using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(2));var ct=budget.Token;
        await using var observer=Context();await observer.Database.OpenConnectionAsync(ct);
        var identity=await observer.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
             AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
             AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
               AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
             AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
               AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if(identity!=1||await observer.Users.AnyAsync(ct)||await observer.UserSessions.AnyAsync(ct)||await observer.AuditLogs.AnyAsync(ct))
            throw new InvalidOperationException("Exact owned empty fixture required");
        var prefix="D3-LOCK-"+Guid.NewGuid().ToString("N")[..8];
        var user=new User {EmployeeCode=prefix,Username=prefix,Email=prefix+"@example.invalid",FullName="Synthetic coordination fixture",
            AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        var checks=new List<object>();string? failure=null;var cleaned=false;
        async Task ObserveWait(int waiter,int blocker)
        {
            for(var attempt=0;attempt<100;attempt++)
            {
                var found=await observer.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={blocker} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct);
                if(found==1)return;
                await Task.Delay(50,ct);
            }
            throw new Exception("SQL DMV did not confirm the expected physical lock wait");
        }
        try
        {
            observer.Users.Add(user);await observer.SaveChangesAsync(ct);observer.ChangeTracker.Clear();
            // A: actual global snapshot table lock blocks actual eligibility helper. Fixture UPDATE
            // models the committed account change; this does NOT invoke/approve the deactivation API.
            await using(var writer=Context()) await using(var admin=Context())
            {
                await writer.Database.OpenConnectionAsync(ct);await admin.Database.OpenConnectionAsync(ct);
                var writerId=await writer.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                var adminId=await admin.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                var stale=await writer.Users.SingleAsync(u=>u.UserId==user.UserId,ct);
                await using var adminTx=await admin.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                var guard=await new GlobalAdminSerialization(admin,TimeProvider.System).AcquireAsync(ct:ct);
                await guard.ReadSnapshotAsync(ct);
                await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={user.UserId}",ct);
                await using var writerTx=await writer.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                var waiting=new UserEligibilityLock(writer).LockAsync([user.UserId],ct);
                try {await ObserveWait(writerId,adminId);}
                finally {await adminTx.CommitAsync(CancellationToken.None);}
                var locked=await waiting;
                if(stale.AccountStatus!="ACTIVE"||locked[user.UserId]?.AccountStatus!="INACTIVE")throw new Exception("Eligibility used stale tracked state after waiting");
                var rejected=false;
                try{UserEligibilityLock.RequireEligible(user.UserId,locked);}catch(ValidationException){rejected=true;}
                if(!rejected)throw new Exception("Inactive receiver admitted");
                await writerTx.RollbackAsync(ct);
                checks.Add(new {scenario="global-first",physicalWait=true,freshSqlEligibility=true,inactiveRejected=true});
            }
            await observer.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='ACTIVE' WHERE user_id={user.UserId}",ct);
            // B: receiver ownership blocks global snapshot before authority/catalog reads.
            await using(var writer=Context()) await using(var admin=Context())
            {
                await writer.Database.OpenConnectionAsync(ct);await admin.Database.OpenConnectionAsync(ct);
                var writerId=await writer.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                var adminId=await admin.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                await using var writerTx=await writer.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                await new UserEligibilityLock(writer).RequireEligibleAsync(user.UserId,ct);
                await using var adminTx=await admin.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                var guard=await new GlobalAdminSerialization(admin,TimeProvider.System).AcquireAsync(ct:ct);
                var waiting=guard.ReadSnapshotAsync(ct);
                try{await ObserveWait(adminId,writerId);}
                finally{await writerTx.RollbackAsync(CancellationToken.None);}
                await waiting;await adminTx.RollbackAsync(ct);
                checks.Add(new {scenario="receiver-first",physicalWait=true,completedAfterRelease=true});
            }
            // C: cancellation while waiting must not become eligibility approval or retain a transaction.
            await using(var holder=Context()) await using(var writer=Context())
            {
                await holder.Database.OpenConnectionAsync(ct);await writer.Database.OpenConnectionAsync(ct);
                var holderId=await holder.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                var writerId=await writer.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS Value").SingleAsync(ct);
                await using var holdTx=await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                await new UserEligibilityLock(holder).RequireEligibleAsync(user.UserId,ct);
                await using var writeTx=await writer.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                using var cancel=CancellationTokenSource.CreateLinkedTokenSource(ct);
                var waiting=new UserEligibilityLock(writer).RequireEligibleAsync(user.UserId,cancel.Token);
                await ObserveWait(writerId,holderId);cancel.Cancel();
                var cancelled=false;
                try{await waiting;}catch(OperationCanceledException){cancelled=true;}
                if(!cancelled)throw new Exception("Cancelled eligibility wait accepted");
                await writeTx.RollbackAsync(CancellationToken.None);await holdTx.RollbackAsync(CancellationToken.None);
                checks.Add(new {scenario="cancelled-wait",physicalWait=true,cancellationObserved=true,rolledBack=true});
            }
        }
        catch(Exception error){failure=error.GetType().Name+": "+error.Message;}
        finally
        {
            observer.ChangeTracker.Clear();
            if(user.UserId>0)await observer.Users.Where(u=>u.UserId==user.UserId&&u.Username==prefix).ExecuteDeleteAsync(CancellationToken.None);
            cleaned=!await observer.Users.AnyAsync()&&!await observer.UserSessions.AnyAsync()&&!await observer.AuditLogs.AnyAsync();
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new {success=failure==null&&cleaned,checks,failure,cleanup=cleaned,
                scope="Physical SQL coordination primitives only; synthetic account status commit compensated by deletion; no deactivation/revocation release acceptance"},new JsonSerializerOptions{WriteIndented=true}));
        }
        Console.WriteLine($"User coordination: {checks.Count} cases; failure={failure??"none"}; cleanup={cleaned}");
        return failure==null&&cleaned?0:1;
    }
}
