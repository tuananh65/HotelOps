using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ClosureTrialTheoryAttribute : TheoryAttribute
{
    public ClosureTrialTheoryAttribute()
    { if (Environment.GetEnvironmentVariable("FS015_SQL_TRIAL") != "APPROVED_LOCAL_ONLY" && Environment.GetEnvironmentVariable("FS016_SQL_TRIAL") != "APPROVED_LOCAL_ONLY") Skip = "Opt-in FS015/FS016 service/SQL component; session validation double."; }
}

[CollectionDefinition("HotelOps isolated SQL", DisableParallelization = true)]
public sealed class IsolatedCleaningSqlCollection { }

[Collection("HotelOps isolated SQL")]
public sealed class CleaningClosureWorkflowTests
{
    internal const string Connection = "Data Source=.\\SQLEXPRESS;Initial Catalog=HotelOps_Canonical_FS015_Cleaning_V1_Fresh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5";
    internal static HotelOpsDbContext Context(params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("FS016_SQL_TRIAL") == "APPROVED_LOCAL_ONLY" ? ReworkWorkflowSqlTests.Connection : Connection).AddInterceptors(interceptors).Options);
    internal static ProvisionalCleaningNotification Publisher(HotelOpsDbContext db, bool enabled = true)
        => new(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["CleaningClosure:Candidate"] = enabled ? "FS015_PROVISIONAL" : null }).Build(), new TestEnvironment());
    internal static RoomTypeCreationService Service(HotelOpsDbContext db, long actor, bool enabled = true)
        => new(db, new Identity(actor), null!, new Sessions(actor), TimeProvider.System, Publisher(db, enabled));
    internal static SessionReference Ticket(long actor) => SessionReference.FromProtectedTicket(actor, SessionCredential.Generate(), false, DateTime.UtcNow);
    internal static async Task<string> Tag(HotelOpsDbContext db, long task, bool progress, CancellationToken ct)
        => progress ? await db.Database.SqlQuery<string>($"SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) AS [Value] FROM dbo.cleaning_progress_versions WHERE cleaning_task_id={task}").SingleAsync(ct)
            : await db.Database.SqlQuery<string>($"SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={task}").SingleAsync(ct);

    [Fact]
    public void Closure_fingerprint_binds_actor_source_etags_payload_and_photos()
    {
        var original = RoomTypeCreationService.ClosureFingerprint(1, 2, 3, "tag", 4, "note", null, null);
        Assert.Equal(original, RoomTypeCreationService.ClosureFingerprint(1, 2, 3, "tag", 4, " note ", null, []));
        Assert.NotEqual(original, RoomTypeCreationService.ClosureFingerprint(2, 2, 3, "tag", 4, "note", null, null));
        Assert.NotEqual(original, RoomTypeCreationService.ClosureFingerprint(1, 2, 3, "new-tag", 4, "note", null, null));
        Assert.NotEqual(original, RoomTypeCreationService.ClosureFingerprint(1, 2, 3, "tag", 5, "note", null, null));
        Assert.NotEqual(original, RoomTypeCreationService.ClosureFingerprint(1, 2, 3, "tag", null, "note", "progress", null));
        Assert.NotEqual(original, RoomTypeCreationService.ClosureFingerprint(1, 2, 3, "tag", 4, "note", null, [new("photo.png", "image/png", "AAAA")]));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("branch")]
    [InlineData("assignment")]
    [InlineData("operation")]
    [InlineData("publication")]
    [InlineData("progress")]
    [InlineData("interval")]
    [InlineData("timestamp")]
    [InlineData("state")]
    [InlineData("readiness")]
    [InlineData("etag")]
    [InlineData("notification")]
    [InlineData("contract")]
    public void Replay_rejects_malformed_or_foreign_receipt(string field)
    {
        var operation = Guid.NewGuid();
        var good = new CleaningClosureResult(1, 2, 3, operation, Guid.NewGuid(), 4, 5, "PENDING_INSPECTION", DateTime.UtcNow,
            "PENDING_INSPECTION", false, "\"0000000000000001\"");
        Assert.True(RoomTypeCreationService.ValidClosureReceipt(good, 2, 1, 3, operation, true));
        var bad = field switch { "id" => good with { Id = 9 }, "branch" => good with { BranchId = 9 }, "assignment" => good with { AssignmentId = 9 },
            "operation" => good with { OperationId = Guid.NewGuid() }, "publication" => good with { PublicationId = Guid.Empty },
            "progress" => good with { ProgressId = 0 }, "interval" => good with { ClosedIntervalId = 0 },
            "timestamp" => good with { OccurredAtUtc = DateTime.SpecifyKind(good.OccurredAtUtc, DateTimeKind.Unspecified) },
            "state" => good with { State = "READY" }, "readiness" => good with { Readiness = "READY" },
            "etag" => good with { SourceETag = "*" }, "notification" => good with { Notification = "DELIVERED" }, _ => good with { Contract = "ACCEPTED" } };
        Assert.False(RoomTypeCreationService.ValidClosureReceipt(bad, 2, 1, 3, operation, true));
    }

    [Theory]
    [InlineData("blockers")]
    [InlineData("complete")]
    public async Task Closure_rejects_more_than_five_photos_before_persistence(string action)
    {
        var service = new RoomTypeCreationService(null!, null!, null!, null!, TimeProvider.System);
        var photos = Enumerable.Repeat(new EvidenceUpload("image.png", "image/png", "AAAA"), 6).ToArray();
        var tag = "\"0000000000000001\"";
        if (action == "blockers") await Assert.ThrowsAsync<ValidationException>(() => service.ReportCleaningBlockerAsync(null!, 0, 1, 1, tag, Guid.NewGuid().ToString(), new(1, "note", photos), default));
        else await Assert.ThrowsAsync<ValidationException>(() => service.CompleteCleaningAsync(null!, 0, 1, 1, tag, Guid.NewGuid().ToString(), new("note", tag, photos), default));
    }

    [ClosureTrialTheory]
    [InlineData("report")]
    [InlineData("complete")]
    [InlineData("cycle")]
    [InlineData("concurrent")]
    [InlineData("same-key")]
    [InlineData("stale")]
    [InlineData("progress-stale")]
    [InlineData("checklist")]
    [InlineData("default-off")]
    [InlineData("wrong-owner")]
    [InlineData("linked")]
    [InlineData("unreconciled")]
    [InlineData("publication-fault")]
    [InlineData("intent-fault")]
    [InlineData("audit-fault")]
    [InlineData("readiness-fault")]
    [InlineData("commit-ack")]
    [InlineData("invalid-evidence")]
    [InlineData("delivery")]
    [InlineData("no-workday")]
    [InlineData("no-interval")]
    [InlineData("no-supervisor")]
    [InlineData("bad-reference")]
    public async Task Closure_service_SQL_atomicity_authorization_replay_and_lifecycle(string scenario)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var ct = budget.Token;
        await using var db = Context();
        Assert.True(await Publisher(db).ClosureAvailableAsync(ct));
        var f = await CleaningResumeSqlTests.CreateFixtureAsync(db, ct, scenario == "no-supervisor" ? "BR002" : "BR001");
        if (scenario != "no-supervisor") await EnsureSupervisor(db, f.Branch, ct);
        var ticket = Ticket(f.Actor);
        var start = await Service(db, f.Actor).StartCleaningAsync(ticket, 1, f.Branch, f.TaskId, await Tag(db, f.TaskId, false, ct), new(true, "Guest has left"), ct);
        Assert.Equal(RoomTypeBatchStatus.Success, start.Status);
        if (scenario != "checklist") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET is_done=1 WHERE cleaning_task_id={f.TaskId}", ct);
        var tag = await Tag(db, f.TaskId, false, ct); var progressTag = await Tag(db, f.TaskId, true, ct);
        if (scenario == "progress-stale") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET note='new progress' WHERE cleaning_task_id={f.TaskId}", ct);
        if (scenario == "linked") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs014_cleaning_blocker_resolutions SET incident_id=1 WHERE progress_id={f.ProgressId}", ct);
        if (scenario == "unreconciled") await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,blocker_type_id,note,logged_by,logged_at) SELECT cleaning_task_id,progress_percent,status,blocker_type_id,note,logged_by,logged_at FROM dbo.cleaning_progress_logs WHERE progress_id={f.ProgressId}", ct);
        if (scenario == "wrong-owner") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='REASSIGNED' WHERE cleaning_task_id={f.TaskId}", ct);
        if (scenario == "no-workday") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.workdays SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE user_id={f.Actor}", ct);
        if (scenario == "no-interval") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.work_intervals SET closed_by={f.Actor},closed_at=SYSUTCDATETIME(),effective_ended_at=SYSUTCDATETIME() WHERE user_id={f.Actor} AND closed_at IS NULL", ct);
        if (scenario == "linked") { tag = await Tag(db, f.TaskId, false, ct); progressTag = await Tag(db, f.TaskId, true, ct); }
        var before = await Snapshot(db, f.TaskId, f.Actor, ct);
        var firstStart = await db.CleaningTasks.Where(x => x.CleaningTaskId == f.TaskId).Select(x => x.ActualStartAt).SingleAsync(ct);
        var reason = await db.ReferenceValues.Where(x => x.GroupCode == "BLOCKER" && x.ValueCode == "GUEST_IN_ROOM").Select(x => x.ReferenceId).SingleAsync(ct);
        if (scenario == "bad-reference") reason = await db.ReferenceValues.Where(x => x.GroupCode == "CLEANING_TYPE" && x.ValueCode == "CHECKOUT").Select(x => x.ReferenceId).SingleAsync(ct);
        var key = Guid.NewGuid().ToString();
        var complete = scenario is "complete" or "checklist" or "progress-stale" or "linked" or "unreconciled";
        var photos = scenario == "invalid-evidence" ? new[] { new EvidenceUpload("bad.png", "image/png", "YWJj") } : null;
        var writeFault = scenario.EndsWith("fault") ? new MandatoryFault(scenario) : null;
        async Task<RoomTypeBatchResult> Invoke(string? operation = null, string note = "FS015 completion or blocker note", bool fault = true)
        {
            IInterceptor[] interceptors = !fault ? [] : writeFault != null ? [writeFault] : scenario == "commit-ack" ? [new LostCommitAcknowledgement()] : [];
            await using var commandDb = Context(interceptors);
            var service = Service(commandDb, f.Actor, scenario != "default-off");
            var expected = scenario == "stale" ? "\"0000000000000000\"" : tag;
            return complete ? await service.CompleteCleaningAsync(ticket, 1, f.Branch, f.TaskId, expected, operation ?? key, new(note, progressTag, photos), ct)
                : await service.ReportCleaningBlockerAsync(ticket, 1, f.Branch, f.TaskId, expected, operation ?? key, new(reason, note, photos), ct, Path.GetTempPath());
        }
        if (scenario.EndsWith("fault"))
        {
            try { Assert.Equal(RoomTypeBatchStatus.Unavailable, (await Invoke()).Status); }
            catch (InvalidOperationException error) { Assert.Contains("FS015_MANDATORY_WRITE_FAULT", error.ToString()); }
            Assert.True(writeFault!.Fired);
        }
        else if (scenario == "invalid-evidence") await Assert.ThrowsAsync<ValidationException>(() => Invoke());
        else if (scenario is "concurrent" or "same-key")
        {
            var results = await Task.WhenAll(Invoke(), Invoke(scenario == "same-key" ? key : Guid.NewGuid().ToString()));
            Assert.Equal(scenario == "same-key" ? 2 : 1, results.Count(x => x.Status == RoomTypeBatchStatus.Success));
            if (scenario == "concurrent") Assert.Single(results, x => x.Status == RoomTypeBatchStatus.Stale);
        }
        else
        {
            var result = await Invoke();
            Assert.Equal(scenario switch { "stale" or "progress-stale" => RoomTypeBatchStatus.Stale,
                "checklist" or "no-workday" or "no-interval" => RoomTypeBatchStatus.Inactive, "default-off" or "linked" or "unreconciled" or "no-supervisor" => RoomTypeBatchStatus.Unavailable,
                "bad-reference" => RoomTypeBatchStatus.InvalidReference,
                "wrong-owner" => RoomTypeBatchStatus.Forbidden, _ => RoomTypeBatchStatus.Success }, result.Status);
        }
        var success = scenario is "report" or "complete" or "cycle" or "concurrent" or "same-key" or "commit-ack" or "delivery";
        if (!success) { Assert.Equal(before, await Snapshot(db, f.TaskId, f.Actor, ct)); return; }
        var receipt = Assert.IsType<CleaningClosureResult>((await Invoke(fault: false)).Value);
        Assert.Equal(complete ? "PENDING_INSPECTION" : "ON_HOLD", receipt.State);
        Assert.Equal(firstStart, await db.CleaningTasks.Where(t => t.CleaningTaskId == f.TaskId).Select(t => t.ActualStartAt).SingleAsync(ct));
        Assert.Equal(0, await db.WorkIntervals.CountAsync(w => w.UserId == f.Actor && w.ClosedAt == null, ct));
        var after = await Snapshot(db, f.TaskId, f.Actor, ct);
        Assert.Equal(RoomTypeBatchStatus.Duplicate, (await Invoke(note: "changed payload", fault: false)).Status);
        Assert.Equal(after, await Snapshot(db, f.TaskId, f.Actor, ct));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_publications WHERE publication_id={receipt.PublicationId}").SingleAsync(ct));
        if (scenario == "delivery")
        {
            var delivery = new ProvisionalCleaningDelivery(db, Publisher(db), Service(db, f.Actor), TimeProvider.System);
            Assert.Equal("DELIVERED", await delivery.DeliverOneAsync(ct, selectedPublication: receipt.PublicationId));
            Assert.Equal("EMPTY", await delivery.DeliverOneAsync(ct, selectedPublication: receipt.PublicationId));
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WHERE publication_id={receipt.PublicationId} AND status='DELIVERED'").SingleAsync(ct));
        }
        if (scenario == "cycle")
        {
            Assert.Equal(RoomTypeBatchStatus.Success, (await Service(db, f.Actor).StartCleaningAsync(ticket, 1, f.Branch, f.TaskId,
                await Tag(db, f.TaskId, false, ct), new(true, "Guest has left again"), ct)).Status);
            var result = await Service(db, f.Actor).CompleteCleaningAsync(ticket, 1, f.Branch, f.TaskId, await Tag(db, f.TaskId, false, ct),
                Guid.NewGuid().ToString(), new("Room cleaned", await Tag(db, f.TaskId, true, ct)), ct);
            Assert.Equal(RoomTypeBatchStatus.Success, result.Status);
            Assert.Equal("PENDING_INSPECTION", Assert.IsType<CleaningClosureResult>(result.Value).Readiness);
            Assert.Equal(0, await db.WorkIntervals.CountAsync(w => w.UserId == f.Actor && w.ClosedAt == null, ct));
        }
    }
    internal static async Task EnsureSupervisor(HotelOpsDbContext db, long branch, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS(SELECT 1 FROM dbo.users WHERE username='fs015.supervisor') BEGIN
            DECLARE @now datetime2(7)=SYSUTCDATETIME(),@user bigint;
            INSERT dbo.users(employee_code,username,email,full_name,primary_branch_id,employment_status,account_status,created_at)
            VALUES('FS015_SUPERVISOR','fs015.supervisor','fs015.supervisor@example.invalid','FS015 synthetic supervisor',{branch},'ACTIVE','ACTIVE',@now);
            SET @user=SCOPE_IDENTITY();
            INSERT dbo.user_branch_access(user_id,branch_id,access_level,granted_at) VALUES(@user,{branch},'STANDARD',DATEADD(hour,-1,@now));
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,is_active) SELECT @user,role_id,{branch},DATEADD(hour,-1,@now),1 FROM dbo.roles WHERE role_code='HK_SUPERVISOR';
            END
            """, ct);
    }
    internal static Task<string> Snapshot(HotelOpsDbContext db, long task, long actor, CancellationToken ct) => db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT * FROM dbo.cleaning_tasks WHERE cleaning_task_id={task} FOR JSON PATH,INCLUDE_NULL_VALUES) AS task,
        (SELECT * FROM dbo.cleaning_progress_logs WHERE cleaning_task_id={task} ORDER BY progress_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS logs,
        (SELECT b.* FROM dbo.fs014_cleaning_blocker_resolutions b JOIN dbo.cleaning_progress_logs p ON p.progress_id=b.progress_id WHERE p.cleaning_task_id={task} ORDER BY b.progress_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS blockers,
        (SELECT * FROM dbo.work_intervals WHERE user_id={actor} ORDER BY interval_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS intervals,
        (SELECT COUNT(*) FROM dbo.audit_logs WHERE actor_user_id={actor}) AS audits,
        (SELECT COUNT(*) FROM dbo.room_readiness_events WHERE cleaning_task_id={task}) AS readiness,
        (SELECT COUNT(*) FROM dbo.notification_operation_reservations WHERE actor_user_id={actor}) AS operations,
        (SELECT COUNT(*) FROM dbo.notification_publications WHERE actor_user_id={actor}) AS publications,
        (SELECT COUNT(*) FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id WHERE p.actor_user_id={actor}) AS intents
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);
    private sealed class Identity(long actor) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(actor, 1, false); return true; } }
    private sealed class Sessions(long actor) : ISqlSessionService
    {
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default)
            => Task.FromResult(new SessionValidationResult(true, new(1, actor, ticket.CreatedAtUtc, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult credential, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "HotelOps.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class MandatoryFault(string kind) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }
        private void Check(DbCommand command)
        {
            var needle = kind switch { "publication-fault" => "INSERT dbo.notification_publications", "intent-fault" => "INSERT dbo.notification_delivery_intents",
                "audit-fault" => "INSERT INTO [audit_logs]", _ => "INSERT dbo.room_readiness_events" };
            if (command.CommandText.Contains(needle, StringComparison.Ordinal)) { Fired = true; throw new InvalidOperationException("FS015_MANDATORY_WRITE_FAULT"); }
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Check(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Check(command); return ValueTask.FromResult(result); }
    }
    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        private bool fired;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        { if (!fired) { fired = true; throw new OperationCanceledException("FS015_COMMIT_ACK_COMPONENT_FAULT"); } return Task.CompletedTask; }
    }
}
