using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

// Runs only after Program's exact disposable identity/marker/startup gates. No test approval
// flag, replacement authorization evaluator, production schema or business gate modification.
internal static class Task58WriterAcceptanceSqlSuite
{
    internal sealed record Case(string Writer, string Scenario, string Result, string Detail);
    internal sealed record Wait(string Writer, string Scenario, int Waiter, int Owner, string Type, DateTime Utc);
    private sealed record Outcome(string Status, bool Success);
    private sealed class InjectedAuditFailure : Exception;

    internal static async Task RunAsync(string connection, string database, string receipt, CancellationToken ct)
    {
        var cases = new List<Case>(); var waits = new List<Wait>();
        HotelOpsDbContext Open(params IInterceptor[] hooks) => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).AddInterceptors(hooks).Options);
        await using var observer = Open();
        var department = await observer.Departments.AsNoTracking().Where(x => x.IsActive && x.Branch.IsActive && x.DepartmentCode == "HK").FirstAsync(ct);
        var branch = await observer.Branches.AsNoTracking().Where(x => x.IsActive && x.BranchId != department.BranchId).Select(x => x.BranchId).FirstAsync(ct);
        var role = await observer.Roles.AsNoTracking().Where(x => x.IsActive
            && x.RolePermissions.Any(p => p.Permission.PermissionCode == "USER_MANAGE" && p.DataScope == "ALL")
            && x.RolePermissions.Any(p => p.Permission.PermissionCode == "ROLE_MANAGE" && p.DataScope == "ALL")).Select(x => x.RoleId).FirstAsync(ct);
        var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
        { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        async Task<string> Catalog() => JsonSerializer.Serialize(new
        {
            roles = await observer.Roles.AsNoTracking().OrderBy(x => x.RoleId).ToArrayAsync(ct),
            grants = await observer.RolePermissions.AsNoTracking().OrderBy(x => x.RolePermissionId).ToArrayAsync(ct),
            permissions = await observer.Permissions.AsNoTracking().OrderBy(x => x.PermissionId).ToArrayAsync(ct)
        });
        var catalogBefore = await Catalog();
        async Task<string> AllMappedRows()
        {
            // Preserve every mapped table, including NULL-actor authentication audits and
            // business assignments. Compare only in memory; never emit this sensitive data.
            await using var db=Open(); await db.Database.OpenConnectionAsync(ct);
            var rows=new List<string>();
            static string Quote(string name)=>"["+name.Replace("]","]]")+"]";
            foreach(var entity in db.Model.GetEntityTypes().Where(e=>e.GetTableName()!=null).OrderBy(e=>e.GetTableName()))
            {
                var table=entity.GetTableName()!;var schema=entity.GetSchema()??"dbo";
                var store=StoreObjectIdentifier.Table(table,entity.GetSchema());
                var key=entity.FindPrimaryKey()??throw new InvalidOperationException("Expected mapped primary key");
                var order=string.Join(",",key.Properties.Select(p=>Quote(p.GetColumnName(store)!)));
                await using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.CommandTimeout=20;
                cmd.CommandText="SELECT COALESCE((SELECT * FROM "+Quote(schema)+"."+Quote(table)+" ORDER BY "+order+" FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]')";
                rows.Add(table+":"+(string)(await cmd.ExecuteScalarAsync(ct))!);
            }
            return JsonSerializer.Serialize(rows);
        }
        var databaseBefore=await AllMappedRows();
        async Task<int> Spid(HotelOpsDbContext db)
        { await db.Database.OpenConnectionAsync(ct); return await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct); }
        async Task Observe(string writer, string scenario, int waiter, int owner, Task pending)
        {
            for (var i = 0; i < 160 && !pending.IsCompleted; i++)
            {
                var kind = await observer.Database.SqlQuery<string>($"SELECT wait_type AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleOrDefaultAsync(ct);
                if (kind != null) { waits.Add(new(writer, scenario, waiter, owner, kind, DateTime.UtcNow)); return; }
                await Task.Delay(40, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("No DMV-confirmed SQL wait: " + writer + "/" + scenario);
        }
        void Record(string writer, string scenario, bool ok, string detail)
        { cases.Add(new(writer, scenario, ok ? "PASS" : "FAIL", detail)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {writer}/{scenario}: {detail}"); }
        var operations = new[] { "Create", "Activate", "Role", "Branch", "Profile", "Reset", "Login", "Logout", "PasswordChange", "Touch", "RevokeAll", "Reauthenticate" };
        foreach (var operation in operations)
        {
            var scenarios = new List<string> { "writer-first", "actual-deactivate-first-denial", "cancel", "timeout1222", "deadlock1205-retry", "audit-rollback" };
            if (operation is "Activate" or "Role" or "Branch" or "Profile" or "Reset") scenarios.Add("stale-target");
            if (operation is "Create" or "Activate" or "Role" or "Branch" or "Profile" or "Reset") scenarios.AddRange(["stale-actor", "revoked-actor-session"]);
            if (operation is "Create" or "Profile") scenarios.AddRange(["expired-actor-session", "final-session-expiry"]);
            if (operation is "Login" or "PasswordChange" or "Touch" or "Reauthenticate") scenarios.Add("inactive-user-after-wait");
            foreach (var scenario in scenarios)
            {
                var clock = new AdvancingClock();
                await using var fixture = new Fixture(connection, department.BranchId, department.DepartmentId, branch, role, passwords, operation, clock);
                await fixture.Prepare(ct);
                try
                {
                    if (scenario == "writer-first")
                    {
                        var barrier = new CommitHook();
                        await using var writer = Open(barrier); await using var contender = Open();
                        var owner = await Spid(writer); var waiter = await Spid(contender);
                        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var work = fixture.Invoke(writer, stop.Token); Task<IReadOnlyDictionary<long, UserEligibilityLock.LockedUser?>>? probe = null;
                        await using var tx = await contender.Database.BeginTransactionAsync(ct);
                        try
                        {
                            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(18), ct);
                            probe = new UserEligibilityLock(contender).LockAsync([fixture.Target.UserId], stop.Token);
                            await Observe(operation, scenario, waiter, owner, probe);
                            barrier.Release.TrySetResult(); var result = await work; await probe;
                            await tx.RollbackAsync(ct);
                            Record(operation, scenario, result.Success && await fixture.CommittedEffect(ct), "actual service commits; retained User waiter rereads after SQL wait; this is a standalone User protocol probe");
                        }
                        finally { barrier.Release.TrySetResult(); stop.Cancel(); await Drain(work); if (probe != null) await Drain(probe); }
                    }
                    else if (scenario == "actual-deactivate-first-denial")
                    {
                        var barrier = new DeactivateUserBarrier();
                        await using var deactivation = Open(barrier); await using var writer = Open();
                        var owner = await Spid(deactivation); var waiter = await Spid(writer);
                        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var denial = fixture.Deactivate(deactivation, stop.Token); Task<Outcome>? work = null;
                        try
                        {
                            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(18), ct);
                            work = fixture.Invoke(writer, stop.Token); await Observe(operation, scenario, waiter, owner, work);
                            barrier.Release.TrySetResult(); var result = await denial; var write = await work;
                            Record(operation, scenario, result is AccountDeactivationStatus.ResponsibilitiesUnresolved or AccountDeactivationStatus.Conflict
                                && write.Success && await fixture.CommittedEffect(ct), "actual Deactivate retained Users universe; writer waited; denial rolled back then writer committed; no INACTIVE Deactivate commit claimed");
                        }
                        finally { barrier.Release.TrySetResult(); stop.Cancel(); await Drain(denial); if (work != null) await Drain(work); }
                    }
                    else if (scenario is "cancel" or "timeout1222" or "stale-target" or "stale-actor" or "revoked-actor-session" or "expired-actor-session" or "inactive-user-after-wait")
                    {
                        await using var holder = Open(); await using var writer = Open();
                        var owner = await Spid(holder); var waiter = await Spid(writer);
                        if (scenario == "timeout1222") await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 700", ct);
                        await using var tx = await holder.Database.BeginTransactionAsync(ct);
                        // A standalone retained User transaction is the competing side, not a fake UC006 success.
                        var participant = scenario is "stale-actor" or "revoked-actor-session" or "expired-actor-session" ? fixture.Actor.UserId : fixture.Target.UserId;
                        await new UserEligibilityLock(holder).LockAsync([participant], ct);
                        if (scenario == "stale-target") await holder.Users.Where(x => x.UserId == participant).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "T58 competing version"), ct);
                        if (scenario is "stale-actor" or "inactive-user-after-wait") await holder.Users.Where(x => x.UserId == participant).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "INACTIVE"), ct);
                        if (scenario == "revoked-actor-session") await new SqlSessionService(holder, new SessionRevocationService(holder), TimeProvider.System).RevokeAllSessionsAsync(participant, ct);
                        if (scenario == "expired-actor-session") await holder.UserSessions.Where(x => x.UserId == participant)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMilliseconds(-1)), ct);
                        var before = scenario is "cancel" or "timeout1222" ? await fixture.State(ct) : null;
                        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var work = fixture.Invoke(writer, stop.Token);
                        try
                        {
                            await Observe(operation, scenario, waiter, owner, work);
                            if (scenario is "cancel" or "timeout1222")
                            {
                                if (scenario == "cancel") stop.Cancel();
                                var caught = false;
                                try { await work; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { caught = true; }
                                catch (Exception e) when (scenario == "timeout1222" && SqlNumber(e, 1222)) { caught = true; }
                                await tx.RollbackAsync(ct);
                                Record(operation, scenario, caught && writer.Database.CurrentTransaction == null && await fixture.State(ct) == before,
                                    "actual SQL wait/fault propagates; owned transaction disposed; aggregate/access/sessions/audits unchanged");
                            }
                            else
                            {
                                await tx.CommitAsync(ct); var committed = await fixture.State(ct); var result = await work;
                                Record(operation, scenario, !result.Success && await fixture.State(ct) == committed,
                                    "authoritative post-wait rejection: " + result.Status + "; competing edit retained, no partial writer mutation");
                            }
                        }
                        finally { stop.Cancel(); if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None); await Drain(work); }
                    }
                    else if (scenario == "final-session-expiry")
                    {
                        var before=await fixture.State(ct); var expiry=new ExpireAfterAudit(clock);
                        await using var writer=Open(expiry);
                        var result=await fixture.Invoke(writer,ct);
                        Record(operation,scenario,expiry.Applied && !result.Success && await fixture.State(ct)==before,
                            "actor session expires after actual audit SQL flush; final boundary revalidation rolls back all staged mutations");
                    }
                    else if (scenario == "audit-rollback")
                    {
                        var before = await fixture.State(ct);
                        // For audit-bearing operations fail their real audit INSERT after prior SQL work;
                        // for touch, fail at commit after the UPDATE. Never alter a SQL table or trigger.
                        var failure = new AuditHook(operation == "Touch");
                        failure.Configure();
                        await using var writer = Open(failure, failure.Commit);
                        var caught = false;
                        try { await fixture.Invoke(writer, ct); } catch (Exception e) when (ContainsAuditFault(e)) { caught = true; }
                        Record(operation, scenario, caught && failure.Applied && writer.Database.CurrentTransaction == null && await fixture.State(ct) == before,
                            operation == "Touch" ? "post-UPDATE commit fault rolls back touch; audit not applicable" : "real audit SQL insertion fault rolls back business/session/access/credential mutations atomically");
                    }
                    else if (scenario == "deadlock1205-retry")
                    {
                        var before = await fixture.State(ct); var fault = new CommitHook(fixture.SentinelId);
                        await using var holder = Open(); await using var writer = Open(fault);
                        var owner = await Spid(writer); var waiter = await Spid(holder);
                        await holder.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY HIGH", ct);
                        await writer.Database.ExecuteSqlRawAsync("SET DEADLOCK_PRIORITY LOW", ct);
                        await using var tx = await holder.Database.BeginTransactionAsync(ct);
                        await holder.Database.SqlQuery<long>($"SELECT audit_id AS [Value] FROM dbo.audit_logs WITH (XLOCK,ROWLOCK,HOLDLOCK) WHERE audit_id={fixture.SentinelId}").SingleAsync(ct);
                        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var work = fixture.Invoke(writer, stop.Token); Task? wait = null;
                        try
                        {
                            await fault.Entered.Task.WaitAsync(TimeSpan.FromSeconds(18), ct);
                            wait = new UserEligibilityLock(holder).LockAsync([fixture.Target.UserId], stop.Token);
                            await Observe(operation, scenario, waiter, owner, wait); fault.Release.TrySetResult();
                            var caught = false;
                            try { await work; } catch (Exception e) when (SqlNumber(e, 1205)) { caught = true; }
                            await wait; await tx.RollbackAsync(ct);
                            Record(operation, scenario, caught && writer.Database.CurrentTransaction == null && await fixture.State(ct) == before,
                                "test-only post-flush inverse cycle yields actual SQL 1205; no automatic retry/partial writes; not canonical-order safety proof");
                        }
                        finally { fault.Release.TrySetResult(); stop.Cancel(); if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None); await Drain(work); if (wait != null) await Drain(wait); }
                        await using var retry = Open();
                        Record(operation, "explicit-fresh-retry", (await fixture.Invoke(retry, ct)).Success && await fixture.CommittedEffect(ct),
                            "fresh context explicitly retries once, rereads current SQL state and commits; no stale replay or duplicate previous effect");
                    }
                }
                catch (Exception e)
                {
                    // Record exception types/numbers only: never serialize entity, SQL parameters, password or tickets.
                    Record(operation, scenario, false, "Harness/service exception " + e.GetType().Name + (SqlNumber(e,1205) ? "/1205" : SqlNumber(e,1222) ? "/1222" : ""));
                    Console.WriteLine("Failure location: " + e.StackTrace);
                }
            }
        }
        // Actual raw trusted credential reset must not remove the sole durable admin.
        await using (var fixture = new Fixture(connection, department.BranchId, department.DepartmentId, branch, role, passwords, "Reset"))
        {
            await fixture.Prepare(ct); await fixture.LastAdminCheck((ok, detail) => Record("Reset", "last-durable-admin", ok, detail), ct);
        }
        Record("All", "catalog-preservation", await Catalog() == catalogBefore, "roles/permissions/catalog grants unchanged");
        Record("All", "all-mapped-row-preservation", await AllMappedRows()==databaseBefore,
            "every mapped table equals pre-run rows after owned cleanup, including NULL-actor audit events and all business assignments; identity counters may advance");
        await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new
        {
            database, cases, waits, passed = cases.Count(x => x.Result == "PASS"), failed = cases.Count(x => x.Result == "FAIL"),
            productionServices = operations, fullUc006 = "BLOCKED", deferredWorkflows = "NOT RUN", gatesCleared = false,
            endpointEnabled = false, sa57Activated = false, fixtureCleanup = "Owned generated rows removed per scenario; database retained"
        }, new JsonSerializerOptions { WriteIndented = true }), ct);
        if (cases.Any(x => x.Result == "FAIL")) throw new InvalidOperationException("Task58 acceptance has failing cases; preserved receipt: " + receipt);
    }

    private sealed class Fixture(string connection, long branch, long department, long destination, long adminRole,
        IPasswordCredentialService passwords, string operation, TimeProvider? clock=null) : IAsyncDisposable
    {
        private readonly string prefix = "T58_" + Guid.NewGuid().ToString("N")[..12];
        private readonly string password = "T58!Aa9" + SessionCredential.Generate();
        private readonly ReauthenticationProofStore proofs = new(TimeProvider.System);
        private readonly TimeProvider writerClock = clock ?? TimeProvider.System;
        internal User Actor = null!, Target = null!;
        private SessionReference actorTicket = null!, targetTicket = null!;
        private long actorSession, targetSession;
        internal long SentinelId;
        private HotelOpsDbContext Open() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        private ISqlSessionService Sessions(HotelOpsDbContext db) => new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
        private AdminReauthenticationService Reauth(HotelOpsDbContext db) => new(db, Sessions(db), new CredentialLockoutService(db, passwords, TimeProvider.System), proofs, TimeProvider.System);
        private TemporaryCredentialService Credentials(HotelOpsDbContext db) => new(db, passwords, new SessionRevocationService(db), TimeProvider.System);
        private (Identity Identity, EfPermissionDataSource Data, PermissionService Permissions, UserCredentialResetAccess Authority) Access(HotelOpsDbContext db)
        {
            var identity = new Identity(Actor.UserId, actorSession); var data = new EfPermissionDataSource(db);
            var permission = new PermissionService(identity, data, new UnresolvedPermissionResourceResolver(), TimeProvider.System);
            return (identity, data, permission, new(db, identity, permission, data, TimeProvider.System));
        }
        internal async Task Prepare(CancellationToken ct)
        {
            await using var db = Open();
            User Person(string suffix) { var code = prefix + suffix; var p = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid",
                FullName = "Task58 owned fixture", PrimaryBranchId = branch, DepartmentId = department, AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
                p.PasswordHash = passwords.HashPassword(p, password); return p; }
            Actor = Person("a"); Target = Person("t"); if (operation == "Activate") Target.AccountStatus = "INACTIVE";
            db.AddRange(Actor, Target); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var now = DateTime.UtcNow;
            db.Add(new UserRole { UserId = Actor.UserId, RoleId = adminRole, IsActive = true, AssignedAt = now });
            foreach (var p in new[] { Actor, Target }) db.Add(new UserBranchAccess { UserId = p.UserId, BranchId = branch, AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now });
            (UserSession Row, SessionReference Ticket) Session(User p) { var raw = SessionCredential.Generate(); var s = new UserSession { UserId = p.UserId, SessionKeyHash = SessionCredential.Hash(raw),
                CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) }; return (s, SessionReference.FromProtectedTicket(p.UserId, raw, false, now)); }
            var a = Session(Actor); var t = Session(Target); var t2 = Session(Target);
            db.AddRange(a.Row, t.Row, t2.Row);
            var sentinel = new AuditLog { EntityType = "Task58Sentinel", EntityId = prefix, Action = "CREATE", OccurredAt = now };
            db.Add(sentinel); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            actorTicket = a.Ticket; actorSession = a.Row.SessionId; targetTicket = t.Ticket; targetSession = t.Row.SessionId; SentinelId = sentinel.AuditId;
            if (operation == "Reset") { var reauth = await Reauth(db).ReauthenticateAsync(actorTicket, actorSession, password, ct); if (reauth.Status != ReauthenticationStatus.Succeeded) throw new InvalidOperationException("Real reauth fixture failed"); }
        }
        internal async Task<Outcome> Invoke(HotelOpsDbContext db, CancellationToken ct)
        {
            var a = Access(db); var sessions = Sessions(db);
            switch (operation)
            {
                case "Create":
                    var credentials = Credentials(db);
                    using (var r = await new UserAccountCreationService(db, a.Identity, a.Permissions, credentials, credentials, writerClock)
                        .CreateAsync(new(prefix+"n",prefix+"n","Task58 created account",prefix+"n@example.invalid",null,branch,department,"HK_STAFF"), ct))
                        return new(r.Status.ToString(), r.Status == AccountCreationStatus.Success);
                case "Activate":
                    var active = await new UserAccountActivationService(db, new(a.Authority), a.Identity, sessions, TimeProvider.System)
                        .ActivateAsync(actorTicket, actorSession, Target.UserId, Target.RowVersion, new("ACTIVE", "Task58 actual writer acceptance"), ct);
                    return new(active.Status.ToString(), active.Status == AccountActivationStatus.Success);
                case "Role":
                    var role = await new UserRoleAssignmentService(db, a.Identity, a.Permissions, sessions, a.Authority, TimeProvider.System)
                        .AssignAsync(actorTicket, actorSession, Target.UserId, new("HK_STAFF",branch), Target.RowVersion, ct);
                    return new(role.Status.ToString(), role.Status == AssignRoleStatus.Success);
                case "Branch":
                    var membership = await new UserBranchAssignmentService(db, new(db,a.Identity,a.Permissions,a.Data,a.Authority,TimeProvider.System),a.Identity,sessions,TimeProvider.System)
                        .AddAsync(actorTicket,actorSession,Target.UserId,destination,Target.RowVersion,ct);
                    return new(membership.Status.ToString(),membership.Status == AddBranchStatus.Success);
                case "Profile":
                    var profile = await new UserProfileService(db,new(db,a.Identity,a.Permissions,a.Data,writerClock),a.Identity,writerClock)
                        .UpdateAsync(Target.UserId,Target.RowVersion,new(true,"Task58 changed profile",false,null,false,null),ct);
                    return new(profile.Status.ToString(),profile.Status == UserProfileStatus.Success);
                case "Reset":
                    using (var reset = await new AdminCredentialResetWorkflow(Credentials(db),a.Authority,a.Identity,Reauth(db))
                        .ResetAsync(actorTicket,actorSession,Target.UserId,Target.RowVersion,ct))
                        return new(reset.Status.ToString(),reset.Status == ConditionalCredentialResetStatus.Success);
                case "Login":
                    var login = await new AuthenticationLoginService(db,new CredentialLockoutService(db,passwords,TimeProvider.System),sessions,passwords,TimeProvider.System)
                        .LoginAsync(new() { Username=Target.Username,Password=password },ct);
                    return new(login.Status.ToString(),login.Status == LoginStatus.Succeeded);
                case "Logout":
                    await new AuthenticationCredentialWorkflow(db,sessions,new SessionRevocationService(db),passwords,TimeProvider.System).LogoutAsync(targetTicket,ct);
                    return new("Succeeded",true);
                case "PasswordChange":
                    var nextPassword="T58!ChangedAa9"+SessionCredential.Generate();
                    var change = await new AuthenticationCredentialWorkflow(db,sessions,new SessionRevocationService(db),passwords,TimeProvider.System)
                        .ChangePasswordAsync(targetTicket,targetSession,new() { CurrentPassword=password,NewPassword=nextPassword,ConfirmNewPassword=nextPassword },ct);
                    return new(change.Status.ToString(),change.Status == ChangePasswordStatus.Succeeded);
                case "Reauthenticate":
                    var authenticated = await Reauth(db).ReauthenticateAsync(targetTicket,targetSession,password,ct);
                    return new(authenticated.Status.ToString(),authenticated.Status == ReauthenticationStatus.Succeeded);
                default:
                    await using (var tx = await db.Database.BeginTransactionAsync(ct))
                    {
                        var success = operation == "Touch" ? (await sessions.TouchSessionAsync(targetTicket,ct)).IsValid
                            : await sessions.RevokeAllSessionsAsync(Target.UserId,ct) == 2;
                        await tx.CommitAsync(ct); return new(success ? "Succeeded" : "Rejected",success);
                    }
            }
        }
        internal async Task<AccountDeactivationStatus> Deactivate(HotelOpsDbContext db, CancellationToken ct)
        {
            var a=Access(db);
            return (await new UserAccountDeactivationService(db,new(a.Authority),a.Identity,Sessions(db),TimeProvider.System)
                .DeactivateAsync(actorTicket,actorSession,Target.UserId,Target.RowVersion,new("Task58 actual Deactivate denial lock lifetime"),ct)).Status;
        }
        internal async Task<string> State(CancellationToken ct)
        {
            await using var db=Open(); var ids=await db.Users.Where(x=>x.Username.StartsWith(prefix)).Select(x=>x.UserId).ToArrayAsync(ct);
            var entityIds=ids.Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            // Sensitive state compared only in memory, never serialized to receipt or console.
            return JsonSerializer.Serialize(new {
                users=await db.Users.AsNoTracking().Where(x=>ids.Contains(x.UserId)).OrderBy(x=>x.UserId).ToArrayAsync(ct),
                sessions=await db.UserSessions.AsNoTracking().Where(x=>ids.Contains(x.UserId)).OrderBy(x=>x.SessionId).ToArrayAsync(ct),
                roles=await db.UserRoles.AsNoTracking().Where(x=>ids.Contains(x.UserId)).OrderBy(x=>x.UserRoleId).ToArrayAsync(ct),
                access=await db.UserBranchAccesses.AsNoTracking().Where(x=>ids.Contains(x.UserId)).OrderBy(x=>x.AccessId).ToArrayAsync(ct),
                audit=await db.AuditLogs.AsNoTracking().Where(x=>ids.Contains(x.ActorUserId??0)
                    || x.EntityType=="users" && entityIds.Contains(x.EntityId)).OrderBy(x=>x.AuditId).ToArrayAsync(ct) });
        }
        internal async Task<bool> CommittedEffect(CancellationToken ct)
        {
            await using var db=Open(); var user=await db.Users.AsNoTracking().SingleAsync(x=>x.UserId==Target.UserId,ct);
            var sessions=await db.UserSessions.AsNoTracking().Where(x=>x.UserId==Target.UserId).ToArrayAsync(ct);
            var roles=await db.UserRoles.CountAsync(x=>x.UserId==Target.UserId,ct);
            var access=await db.UserBranchAccesses.CountAsync(x=>x.UserId==Target.UserId,ct);
            var targetEntity=Target.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var targetAudits=await db.AuditLogs.CountAsync(x=>x.EntityType=="users" && x.EntityId==targetEntity,ct);
            return operation switch {
                "Create" => await db.Users.AnyAsync(x=>x.Username==prefix+"n" && x.PasswordHash!=null && x.TemporaryPasswordExpiresAt!=null,ct),
                "Activate" => user.AccountStatus=="ACTIVE" && !user.RowVersion.SequenceEqual(Target.RowVersion) && targetAudits==1,
                "Role" => roles==1 && access==1,
                "Branch" => access==2 && roles==0,
                "Profile" => user.FullName=="Task58 changed profile" && user.PasswordHash==Target.PasswordHash && roles==0 && access==1 && targetAudits==1,
                "Reset" => user.PasswordHash!=Target.PasswordHash && user.TemporaryPasswordExpiresAt!=null && sessions.All(x=>x.RevokedAt!=null) && roles==0 && access==1 && targetAudits==1,
                "Login" => sessions.Length==3 && user.LastLoginAt!=null && targetAudits==2,
                "Logout" => sessions.Count(x=>x.RevokedAt!=null)==1 && targetAudits==1,
                "PasswordChange" => user.PasswordHash!=Target.PasswordHash && user.TemporaryPasswordExpiresAt==null && sessions.Length==3 && sessions.Count(x=>x.RevokedAt!=null)==2 && targetAudits==2,
                "Touch" => sessions.Single(x=>x.SessionId==targetSession).LastUsedAt>targetTicket.CreatedAtUtc && targetAudits==0,
                "RevokeAll" => sessions.All(x=>x.RevokedAt!=null) && targetAudits==1,
                "Reauthenticate" => targetAudits==1 && await db.AuditLogs.AnyAsync(x=>x.ActorUserId==Target.UserId && x.NewValues!=null && x.NewValues.Contains("REAUTHENTICATED"),ct),
                _=>false };
        }
        internal async Task LastAdminCheck(Action<bool,string> check,CancellationToken ct)
        {
            await using var db=Open();
            await db.UserRoles.Where(x=>x.UserId==Actor.UserId).ExecuteDeleteAsync(ct);
            db.Add(new UserRole{UserId=Target.UserId,RoleId=adminRole,AssignedAt=DateTime.UtcNow,IsActive=true}); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var before=await State(ct); var credentials=Credentials(db); using var prepared=credentials.Prepare(Target.UserId);
            using var result=await credentials.ResetAsync(prepared,Actor.UserId,Target.RowVersion,"TASK58_TRUSTED_CALLER",ct);
            check(result.Status==ConditionalCredentialResetStatus.LastAdministrator && await State(ct)==before,
                "actual trusted raw reset rejects sole durable administrator and rolls back credential/session/audit; not HTTP authorization or Deactivate poststate acceptance");
        }
        public async ValueTask DisposeAsync()
        {
            await using var db=Open(); var ids=await db.Users.Where(x=>x.Username.StartsWith(prefix)).Select(x=>x.UserId).ToArrayAsync();
            var entityIds=ids.Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            await db.AuditLogs.Where(x=>ids.Contains(x.ActorUserId??0) || x.AuditId==SentinelId
                || x.EntityType=="users" && entityIds.Contains(x.EntityId)).ExecuteDeleteAsync();
            await db.UserSessions.Where(x=>ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.UserRoles.Where(x=>ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.UserBranchAccesses.Where(x=>ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(x=>ids.Contains(x.UserId) && x.Username.StartsWith(prefix)).ExecuteDeleteAsync();
        }
    }
    private static async Task Drain(Task task) { try { await task; } catch { } }
    private static bool SqlNumber(Exception error,int number)
    { for(Exception? e=error;e!=null;e=e.InnerException) if(e is SqlException s && s.Errors.Cast<SqlError>().Any(x=>x.Number==number)) return true; return false; }
    private static bool ContainsAuditFault(Exception error)
    { for(Exception? e=error;e!=null;e=e.InnerException) if(e is InjectedAuditFailure) return true; return false; }
    private sealed class AdvancingClock : TimeProvider
    {
        internal TimeSpan Offset;
        public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow+Offset;
    }
    private sealed class ExpireAfterAudit(AdvancingClock clock) : DbCommandInterceptor
    {
        internal bool Applied;
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand cmd,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        {if(cmd.CommandText.Contains("INSERT INTO [audit_logs]")){Applied=true;clock.Offset=TimeSpan.FromMinutes(31);}return ValueTask.FromResult(result);}
    }
    private sealed class CommitHook(long? sentinel=null) : DbTransactionInterceptor
    {
        internal TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx,TransactionEventData data,InterceptionResult result,CancellationToken ct=default)
        {
            Entered.TrySetResult(); await Release.Task.WaitAsync(ct);
            if(sentinel.HasValue) {
                await using var cmd=tx.Connection!.CreateCommand(); cmd.Transaction=tx;
                cmd.CommandText="SELECT audit_id FROM dbo.audit_logs WITH (UPDLOCK,ROWLOCK,HOLDLOCK) WHERE audit_id=@id";
                var p=cmd.CreateParameter();p.ParameterName="@id";p.Value=sentinel.Value;cmd.Parameters.Add(p); await cmd.ExecuteScalarAsync(ct);
            }
            return result;
        }
    }
    private sealed class DeactivateUserBarrier : DbCommandInterceptor
    {
        internal TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand cmd,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        { if(cmd.CommandText.Contains("TABLOCKX,HOLDLOCK")) {Entered.TrySetResult();await Release.Task.WaitAsync(ct);}return result; }
    }
    private sealed class AuditHook(bool commitOnly) : DbCommandInterceptor
    {
        internal bool Applied;
        internal CommitAuditHook Commit {get;}=new();
        private void Inspect(DbCommand cmd) { if(!commitOnly && cmd.CommandText.Contains("INSERT INTO [audit_logs]")) {Applied=true;throw new InjectedAuditFailure();} }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand cmd,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {Inspect(cmd);return ValueTask.FromResult(result);}
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand cmd,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {Inspect(cmd);return ValueTask.FromResult(result);}
        internal sealed class CommitAuditHook : DbTransactionInterceptor
        {
            internal Action? Fail;
            public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx,TransactionEventData data,InterceptionResult result,CancellationToken ct=default)
            { Fail?.Invoke();return ValueTask.FromResult(result); }
        }
        internal void Configure() {if(commitOnly)Commit.Fail=()=>{Applied=true;throw new InjectedAuditFailure();};}
    }
}
