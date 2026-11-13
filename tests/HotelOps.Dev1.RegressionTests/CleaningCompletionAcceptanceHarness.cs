using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Metadata describes an EXTERNALLY approved lease; constructing it grants nothing.
public sealed record CleaningCompletionAcceptanceLease(string LeaseId, string Owner, string DataSource,
    string Database, string DatabaseUser, string FixtureDatabaseUser, DateTime ExpiresAtUtc, string SourceSha,
    string NotificationSourceSha, string? Fe05ConsumerSha)
{
    public void Validate(DateTime now)
    {
        static bool Sha(string? value) => value is { Length: 40 } && value.Any(c => c != '0') && value.All(Uri.IsHexDigit);
        if (Owner != "DEV1_FE04" || string.IsNullOrWhiteSpace(LeaseId) || string.IsNullOrWhiteSpace(DataSource)
            || string.IsNullOrWhiteSpace(DatabaseUser) || string.IsNullOrWhiteSpace(FixtureDatabaseUser)
            || DatabaseUser == FixtureDatabaseUser || ExpiresAtUtc.Kind != DateTimeKind.Utc || ExpiresAtUtc <= now
            || Database is not ("HotelOps_Canonical_FS015_Cleaning_V1_Fresh" or "HotelOps_Canonical_FS016_Rework_V1_Fresh")
            || !Sha(SourceSha) || !Sha(NotificationSourceSha) || Fe05ConsumerSha != null && !Sha(Fe05ConsumerSha))
            throw new InvalidOperationException("UC034_APPROVED_TARGET_LEASE_AND_CONTRACT_PINS_REQUIRED");
    }
}

// Owner supplies fresh connection contexts and authoritative central lease validation.
// No default authority implementation, embedded connection string, flags or SQL auto-run.
public sealed record CleaningCompletionAcceptanceRuntime(CleaningCompletionAcceptanceLease Lease,
    Func<CleaningCompletionAcceptanceLease, CancellationToken, Task> ValidateExternalLeaseAsync,
    Func<IReadOnlyList<IInterceptor>, HotelOpsDbContext> CreateContext,
    IConfiguration Configuration, IWebHostEnvironment Environment, string BranchCode, string EvidenceRoot,
    Func<IReadOnlyList<IInterceptor>, HotelOpsDbContext> CreateFixtureContext);

public sealed class CleaningCompletionAcceptanceHarness
{
    private readonly CleaningCompletionAcceptanceRuntime runtime;
    public CleaningCompletionAcceptanceHarness(CleaningCompletionAcceptanceRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime.ValidateExternalLeaseAsync);
        ArgumentNullException.ThrowIfNull(runtime.CreateContext);
        ArgumentNullException.ThrowIfNull(runtime.CreateFixtureContext);
        this.runtime = runtime;
    }

    public async Task ValidateAuthorityAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        runtime.Lease.Validate(DateTime.UtcNow);
        await runtime.ValidateExternalLeaseAsync(runtime.Lease, ct);
        // The validator may take time. An expired envelope must never reach context creation.
        ct.ThrowIfCancellationRequested();
        runtime.Lease.Validate(DateTime.UtcNow);
    }

    private Task<HotelOpsDbContext> ContextAsync(CancellationToken ct, params IInterceptor[] faults)
        => ContextCoreAsync(false, ct, faults);
    private Task<HotelOpsDbContext> FixtureContextAsync(CancellationToken ct)
        => ContextCoreAsync(true, ct, []);
    private async Task<HotelOpsDbContext> ContextCoreAsync(bool fixture, CancellationToken ct, IReadOnlyList<IInterceptor> faults)
    {
        await ValidateAuthorityAsync(ct);
        var factory = fixture ? runtime.CreateFixtureContext : runtime.CreateContext;
        var db = factory([new CleaningCompletionLeaseCommitGuard(ValidateAuthorityAsync), .. faults]);
        var connection = db.Database.GetDbConnection();
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || !string.Equals(connection.DataSource, runtime.Lease.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(connection.Database, runtime.Lease.Database, StringComparison.Ordinal))
        {
            await db.DisposeAsync();
            throw new InvalidOperationException("UC034_RUNTIME_TARGET_MISMATCH");
        }
        var expectedUser = fixture ? runtime.Lease.FixtureDatabaseUser : runtime.Lease.DatabaseUser;
        try
        {
            if (await db.Database.SqlQuery<string>($"SELECT USER_NAME() AS [Value]").SingleAsync(ct) != expectedUser)
                throw new InvalidOperationException("UC034_EXECUTION_IDENTITY_MISMATCH");
            return db;
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private ProvisionalCleaningNotification Publisher(HotelOpsDbContext db)
        => new(db, runtime.Configuration, runtime.Environment);

    private RoomTypeCreationService Service(HotelOpsDbContext db, CreatedSqlSession session)
    {
        // Real committed SQL session, production principal/accessor and SQL revalidation.
        // Direct service harness: NOT proof of HTTPS cookie encryption/CSRF middleware.
        var http = new DefaultHttpContext { User = HotelOpsCookieTicket.Create(session, TimeProvider.System).Principal };
        http.Items[typeof(SessionSnapshot)] = session.Session;
        return new(db, new CurrentUserAccessor(new HttpContextAccessor { HttpContext = http }), null!,
            new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System),
            TimeProvider.System, Publisher(db));
    }

    private sealed class Fixture
    {
        public long Actor, Branch, Task, Room, Assignment, Supervisor;
        public required CreatedSqlSession Session;
        public string TaskTag = "", ProgressTag = "", EvidenceRoot = "";
    }

    private async Task<Fixture> FreshFixtureAsync(CancellationToken ct)
    {
        await using var db = await FixtureContextAsync(ct);
        Assert.True(await Publisher(db).ClosureAvailableAsync(ct), "Existing closure target/schema gate must pass without modification.");
        Assert.True(await ProgressSchema.InstalledAsync(db, ct));
        Assert.True(await HousekeepingSchema.InstalledAsync(db, ct));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NOT NULL
             AND OBJECT_ID('dbo.fe04_capture_cleaner_attribution','P') IS NOT NULL
             AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
               AND name='HotelOps.FE04.CleanerAttribution' AND CONVERT(nvarchar(128),value)='R04_V1')
             THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct));

        // Existing append-only fixture: fresh actor/room/task/checklist/shift/workday,
        // ON_HOLD with an explicit blocker and closed historical interval. Never reset it.
        var seed = await CleaningResumeSqlTests.CreateFixtureAsync(db, ct, runtime.BranchCode);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == seed.Actor, ct);
        var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        var password = "Uc034!" + Guid.NewGuid().ToString("N"); // memory only, never log/assert credential
        var hash = passwords.HashPassword(user, password);
        await ValidateAuthorityAsync(ct);
        await db.Users.Where(u => u.UserId == seed.Actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash), ct);
        var sessions = new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
        var login = new AuthenticationLoginService(db, new CredentialLockoutService(db, passwords, TimeProvider.System),
            sessions, passwords, TimeProvider.System);
        var authenticated = await login.LoginAsync(new LoginRequest { Username = user.Username, Password = password }, ct);
        password = "";
        Assert.Equal(LoginStatus.Succeeded, authenticated.Status);
        var created = authenticated.CreatedSession ?? throw new InvalidOperationException("REAL_SQL_SESSION_REQUIRED");
        Assert.False(created.Session.IsRestricted);

        var supervisorCode = "U34S" + Guid.NewGuid().ToString("N")[..12];
        await ValidateAuthorityAsync(ct);
        var supervisor = await db.Database.SqlQuery<long>($"""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            DECLARE @now datetime2(7)=SYSUTCDATETIME(),@user bigint;
            INSERT dbo.users(employee_code,username,email,full_name,primary_branch_id,employment_status,account_status,created_at)
            VALUES({supervisorCode},{supervisorCode},{supervisorCode + "@example.invalid"},{supervisorCode},{seed.Branch},'ACTIVE','ACTIVE',@now);
            SET @user=SCOPE_IDENTITY();
            INSERT dbo.user_branch_access(user_id,branch_id,access_level,granted_at)
            VALUES(@user,{seed.Branch},'STANDARD',DATEADD(hour,-1,@now));
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,is_active)
              SELECT @user,role_id,{seed.Branch},DATEADD(hour,-1,@now),1 FROM dbo.roles WHERE role_code='HK_SUPERVISOR';
            COMMIT; SELECT @user AS [Value];
            """).ToArrayAsync(ct);
        var f = new Fixture
        {
            Actor = seed.Actor, Branch = seed.Branch, Task = seed.TaskId, Supervisor = Assert.Single(supervisor),
            Session = created, EvidenceRoot = Path.Combine(runtime.EvidenceRoot, Guid.NewGuid().ToString("N")),
            Room = await db.CleaningTasks.Where(t => t.CleaningTaskId == seed.TaskId).Select(t => t.RoomId).SingleAsync(ct),
            Assignment = await db.CleaningAssignments.Where(a => a.CleaningTaskId == seed.TaskId).Select(a => a.AssignmentId).SingleAsync(ct)
        };
        Assert.Contains(f.Supervisor, await Publisher(db).ClosureRecipientsAsync(f.Branch, ct));
        var start = await Service(db, created).StartCleaningAsync(created.Ticket, created.Session.SessionId,
            f.Branch, f.Task, await CleaningClosureWorkflowTests.Tag(db, f.Task, false, ct),
            new(true, "UC034 owned fixture blocker resolved"), ct);
        Assert.Equal(RoomTypeBatchStatus.Success, start.Status);
        await ValidateAuthorityAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET is_done=1 WHERE cleaning_task_id={f.Task}", ct);
        await RefreshTagsAsync(db, f, ct);
        Assert.Single(await db.WorkIntervals.AsNoTracking().Where(i => i.UserId == f.Actor && i.ClosedAt == null
            && i.WorkItemKind == "CLEANING" && i.WorkItemId == f.Task && i.BranchId == f.Branch).ToArrayAsync(ct));
        return f;
    }

    private static async Task RefreshTagsAsync(HotelOpsDbContext db, Fixture f, CancellationToken ct)
    {
        f.TaskTag = await CleaningClosureWorkflowTests.Tag(db, f.Task, false, ct);
        f.ProgressTag = await CleaningClosureWorkflowTests.Tag(db, f.Task, true, ct);
    }

    private async Task PrepareCaseAsync(CleaningCompletionAcceptanceCase scenario, Fixture f, CancellationToken ct)
    {
        await using var db = await FixtureContextAsync(ct);
        switch (scenario)
        {
            case CleaningCompletionAcceptanceCase.MissingAssignment:
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='REASSIGNED' WHERE assignment_id={f.Assignment}", ct); break;
            case CleaningCompletionAcceptanceCase.WrongAssignee:
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET assignee_user_id={f.Supervisor} WHERE assignment_id={f.Assignment}", ct); break;
            case CleaningCompletionAcceptanceCase.ChecklistIncomplete:
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET is_done=0 WHERE cleaning_task_id={f.Task}", ct); break;
            case CleaningCompletionAcceptanceCase.ActiveBlocker:
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE b SET resolved_at=NULL,resolved_by=NULL,resolution_note=NULL
                    FROM dbo.fs014_cleaning_blocker_resolutions b JOIN dbo.cleaning_progress_logs p ON p.progress_id=b.progress_id
                    WHERE p.cleaning_task_id={f.Task}
                    """, ct); break;
            case CleaningCompletionAcceptanceCase.NoInterval:
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.work_intervals SET closed_at=SYSUTCDATETIME(),closed_by={f.Actor},effective_ended_at=SYSUTCDATETIME()
                    WHERE user_id={f.Actor} AND work_item_kind='CLEANING' AND work_item_id={f.Task} AND closed_at IS NULL
                    """, ct); break;
        }
        await RefreshTagsAsync(db, f, ct);
        if (scenario == CleaningCompletionAcceptanceCase.StaleTask) f.TaskTag = "\"0000000000000000\"";
        if (scenario == CleaningCompletionAcceptanceCase.StaleProgress)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET note='UC034 newer progress' WHERE cleaning_task_id={f.Task}", ct);
    }

    // Snapshot after each case-specific setup, before the command. Session setup audit
    // and fixture mutations are baseline, not command rollback evidence.
    private async Task<string> SnapshotAsync(Fixture f, CancellationToken ct)
    {
        await using var db = await ContextAsync(ct);
        return await db.Database.SqlQuery<string>($"""
            SELECT (SELECT
              (SELECT * FROM dbo.cleaning_tasks WHERE cleaning_task_id={f.Task} FOR JSON PATH,INCLUDE_NULL_VALUES) AS task,
              (SELECT * FROM dbo.cleaning_assignments WHERE cleaning_task_id={f.Task} ORDER BY assignment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS assignments,
              (SELECT * FROM dbo.cleaning_progress_logs WHERE cleaning_task_id={f.Task} ORDER BY progress_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS progress,
              (SELECT * FROM dbo.cleaning_task_items WHERE cleaning_task_id={f.Task} ORDER BY item_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS items,
              (SELECT * FROM dbo.cleaning_progress_versions WHERE cleaning_task_id={f.Task} FOR JSON PATH,INCLUDE_NULL_VALUES) AS progressVersion,
              (SELECT * FROM dbo.room_cleaning_cycle_state WHERE room_id={f.Room} FOR JSON PATH,INCLUDE_NULL_VALUES) AS cycle,
              (SELECT b.* FROM dbo.fs014_cleaning_blocker_resolutions b JOIN dbo.cleaning_progress_logs p ON p.progress_id=b.progress_id
                WHERE p.cleaning_task_id={f.Task} ORDER BY b.progress_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS blockers,
              (SELECT * FROM dbo.work_intervals WHERE user_id={f.Actor} ORDER BY interval_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS intervals,
              (SELECT * FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={f.Task} FOR JSON PATH,INCLUDE_NULL_VALUES) AS attribution,
              (SELECT * FROM dbo.room_readiness_events WHERE cleaning_task_id={f.Task} ORDER BY event_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS readiness,
              (SELECT * FROM dbo.audit_logs WHERE actor_user_id={f.Actor} ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS audits,
              (SELECT * FROM dbo.notification_operation_reservations WHERE actor_user_id={f.Actor} ORDER BY operation_key FOR JSON PATH,INCLUDE_NULL_VALUES) AS operations,
              (SELECT * FROM dbo.notification_publications WHERE actor_user_id={f.Actor} ORDER BY publication_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS publications,
              (SELECT i.* FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
                WHERE p.actor_user_id={f.Actor} ORDER BY i.intent_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS intents,
              (SELECT * FROM dbo.attachments WHERE uploaded_by={f.Actor} ORDER BY attachment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS attachments,
              (SELECT e.* FROM dbo.progress_evidence_links e JOIN dbo.cleaning_progress_logs p ON p.progress_id=e.cleaning_progress_id
                WHERE p.cleaning_task_id={f.Task} ORDER BY e.attachment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS evidence,
              (SELECT operational_status FROM dbo.rooms WHERE room_id={f.Room} FOR JSON PATH) AS room,
              (SELECT * FROM dbo.room_inspections WHERE room_id={f.Room} ORDER BY inspection_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS inspections
            FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
            """).SingleAsync(ct);
    }

    private async Task<RoomTypeBatchResult> InvokeAsync(Fixture f, Guid key, string note, bool invalidEvidence, bool validEvidence,
        IReadOnlyList<IInterceptor> faults, CancellationToken ct)
    {
        await using var db = await ContextAsync(ct, [.. faults]);
        // Every scenario invokes COMPLETE, including concurrency and all failure cases.
        return await Service(db, f.Session).CompleteCleaningAsync(f.Session.Ticket, f.Session.Session.SessionId,
            f.Branch, f.Task, f.TaskTag, key.ToString("D"), new(note, f.ProgressTag,
                invalidEvidence ? [new("invalid.png", "image/png", "YWJj")] : validEvidence ? [new("evidence.png", "image/png", "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lXcAAAAASUVORK5CYII=")] : null), ct, f.EvidenceRoot);
    }

    public static bool IsSuccessfulCase(CleaningCompletionAcceptanceCase scenario)
        => scenario is CleaningCompletionAcceptanceCase.Authorized or CleaningCompletionAcceptanceCase.ValidEvidence or CleaningCompletionAcceptanceCase.Replay
            or CleaningCompletionAcceptanceCase.ConflictingReplay or CleaningCompletionAcceptanceCase.SameKeyConcurrent
            or CleaningCompletionAcceptanceCase.DifferentKeyConcurrent or CleaningCompletionAcceptanceCase.LostCommitAcknowledgement;

    public async Task RunAsync(CleaningCompletionAcceptanceCase scenario, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(scenario)) throw new ArgumentOutOfRangeException(nameof(scenario));
        await ValidateAuthorityAsync(cancellationToken);
        if (runtime.Lease.ExpiresAtUtc - DateTime.UtcNow < TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("UC034_LEASE_TOO_SHORT_NO_RENEWAL");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(90));
        var ct = budget.Token;
        var f = await FreshFixtureAsync(ct);
        await PrepareCaseAsync(scenario, f, ct);
        var before = await SnapshotAsync(f, ct);
        var writeFault = new CleaningCompletionWriteFault(scenario);
        var commitFault = new CleaningCompletionCommitFault(scenario);
        IInterceptor[] faults = [writeFault, commitFault];
        var validEvidence = scenario is CleaningCompletionAcceptanceCase.ValidEvidence
            or CleaningCompletionAcceptanceCase.AttributionFailure or CleaningCompletionAcceptanceCase.CompletionAuditFailure
            or CleaningCompletionAcceptanceCase.ReadinessFailure or CleaningCompletionAcceptanceCase.NotificationPublicationFailure
            or CleaningCompletionAcceptanceCase.NotificationIntentFailure or CleaningCompletionAcceptanceCase.LostCommitAcknowledgement;
        var key = Guid.NewGuid();
        Task<RoomTypeBatchResult> Invoke(Guid? operation = null, string note = "UC034 accepted-scope completion")
            => InvokeAsync(f, operation ?? key, note, scenario == CleaningCompletionAcceptanceCase.InvalidEvidence, validEvidence, faults, ct);

        if (scenario is CleaningCompletionAcceptanceCase.AttributionFailure or CleaningCompletionAcceptanceCase.CompletionAuditFailure
            or CleaningCompletionAcceptanceCase.ReadinessFailure or CleaningCompletionAcceptanceCase.NotificationPublicationFailure
            or CleaningCompletionAcceptanceCase.NotificationIntentFailure or CleaningCompletionAcceptanceCase.BeforeCommitFailure)
        {
            await Assert.ThrowsAsync<CleaningCompletionInjectedFaultException>(() => Invoke());
            Assert.True(writeFault.Fired || commitFault.Fired, "Case-specific callback was not reached.");
        }
        else if (scenario == CleaningCompletionAcceptanceCase.InvalidEvidence)
            await Assert.ThrowsAsync<ValidationException>(() => Invoke());
        else if (scenario is CleaningCompletionAcceptanceCase.SameKeyConcurrent or CleaningCompletionAcceptanceCase.DifferentKeyConcurrent)
        {
            // Independent contexts/transactions, identical actor/session and original ETags.
            var results = await Task.WhenAll(Invoke(), Invoke(scenario == CleaningCompletionAcceptanceCase.SameKeyConcurrent ? key : Guid.NewGuid()));
            Assert.Equal(scenario == CleaningCompletionAcceptanceCase.SameKeyConcurrent ? 2 : 1,
                results.Count(r => r.Status == RoomTypeBatchStatus.Success));
            if (scenario == CleaningCompletionAcceptanceCase.DifferentKeyConcurrent)
                Assert.Single(results, r => r.Status == RoomTypeBatchStatus.Stale);
            else Assert.Equal(results[0].Value, results[1].Value);
        }
        else
        {
            var result = await Invoke();
            var expected = scenario switch
            {
                CleaningCompletionAcceptanceCase.MissingAssignment or CleaningCompletionAcceptanceCase.WrongAssignee => RoomTypeBatchStatus.Forbidden,
                CleaningCompletionAcceptanceCase.StaleTask or CleaningCompletionAcceptanceCase.StaleProgress => RoomTypeBatchStatus.Stale,
                CleaningCompletionAcceptanceCase.ChecklistIncomplete or CleaningCompletionAcceptanceCase.ActiveBlocker
                    or CleaningCompletionAcceptanceCase.NoInterval => RoomTypeBatchStatus.Inactive,
                _ => RoomTypeBatchStatus.Success
            };
            Assert.Equal(expected, result.Status);
        }

        if (!IsSuccessfulCase(scenario))
        {
            Assert.Equal(before, await SnapshotAsync(f, ct));
            Assert.False(Directory.Exists(f.EvidenceRoot) && Directory.EnumerateFiles(f.EvidenceRoot).Any());
            return;
        }
        if (scenario == CleaningCompletionAcceptanceCase.LostCommitAcknowledgement) Assert.True(commitFault.Fired);
        var persisted = await SnapshotAsync(f, ct);
        var replay = await Invoke();
        Assert.Equal(RoomTypeBatchStatus.Success, replay.Status);
        Assert.Equal(persisted, await SnapshotAsync(f, ct));
        var receipt = Assert.IsType<CleaningClosureResult>(replay.Value);
        Assert.Equal("PENDING_INSPECTION", receipt.State);
        Assert.Equal("PENDING_INSPECTION", receipt.Readiness);
        var conflict = await Invoke(note: "UC034 conflicting payload");
        Assert.Equal(RoomTypeBatchStatus.Duplicate, conflict.Status);
        Assert.Equal(persisted, await SnapshotAsync(f, ct));
        await AssertCommittedAsync(f, receipt, validEvidence, ct);
    }

    private async Task AssertCommittedAsync(Fixture f, CleaningClosureResult receipt, bool evidence, CancellationToken ct)
    {
        await using var db = await ContextAsync(ct);
        var row = await db.CleaningTasks.AsNoTracking().SingleAsync(t => t.CleaningTaskId == f.Task, ct);
        Assert.NotNull(row.ActualEndAt);
        Assert.NotNull(row.ActualStartAt);
        Assert.True(row.ActualEndAt >= row.ActualStartAt);
        Assert.Equal("UC034 accepted-scope completion", row.CompletionNote);
        Assert.Equal(0, await db.WorkIntervals.CountAsync(i => i.UserId == f.Actor && i.ClosedAt == null, ct));
        Assert.Equal(1, await db.CleaningProgressLogs.CountAsync(p => p.CleaningTaskId == f.Task && p.Status == "COMPLETED", ct));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.fe04_cleaner_attributions a
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
            WHERE a.cleaning_task_id={f.Task} AND a.cleaner_user_id={f.Actor} AND a.assignment_id={f.Assignment}
              AND a.completion_progress_id={receipt.ProgressId} AND a.completed_at=t.actual_end_at
              AND a.completion_revision=t.row_version AND a.writer_transaction_id>0 AND t.workflow_status='PENDING_INSPECTION'
            """).SingleAsync(ct));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.notification_publications
            WHERE actor_user_id={f.Actor} AND event_type='CLEANING_TASK_COMPLETED'
              AND publication_id={receipt.PublicationId} AND operation_key={receipt.OperationId}
            """).SingleAsync(ct));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.ActorUserId == f.Actor && a.NewValues != null
            && a.NewValues.Contains("COMPLETE_CLEANING"), ct));
        Assert.Equal(evidence ? 1 : 0, await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.progress_evidence_links e
            JOIN dbo.cleaning_progress_logs p ON p.progress_id=e.cleaning_progress_id
            JOIN dbo.attachments a ON a.attachment_id=e.attachment_id
            WHERE p.cleaning_task_id={f.Task} AND p.progress_id={receipt.ProgressId} AND a.uploaded_by={f.Actor}
            """).SingleAsync(ct));
        Assert.Equal(evidence ? 1 : 0, Directory.Exists(f.EvidenceRoot) ? Directory.GetFiles(f.EvidenceRoot).Length : 0);
        Assert.Equal(0, await db.RoomInspections.CountAsync(i => i.RoomId == f.Room, ct));
        Assert.Equal("DIRTY", await db.Rooms.Where(r => r.RoomId == f.Room).Select(r => r.OperationalStatus).SingleAsync(ct));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.room_readiness_events
            WHERE cleaning_task_id={f.Task} AND readiness='PENDING_INSPECTION'
            """).SingleAsync(ct));
        // Persisted mandatory proof, in the same real SQL session transaction.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var validated = await new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System)
            .ValidateSessionAsync(f.Session.Ticket, ct);
        Assert.True(validated.IsValid);
        var intents = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WHERE publication_id={receipt.PublicationId}
            """).SingleAsync(ct);
        Assert.True(intents >= 1);
        await Publisher(db).VerifyClosureAsync(f.Actor, receipt.OperationId, intents, ct);
        await tx.CommitAsync(ct);
    }
}