using System.Data.Common;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Tests the PREPARATION harness and fault/lease controls only. No SQL command executes.
// Synthetic metadata is never an acceptance receipt or an externally granted lease.
public sealed class CleaningCompletionAcceptanceHarnessTests
{
    private static CleaningCompletionAcceptanceLease Metadata => new("no-sql-test-only", "DEV1_FE04", "UC034_NoSql",
        "HotelOps_Canonical_FS015_Cleaning_V1_Fresh", "UC034_NoSql_User", "UC034_NoSql_FixtureOwner", DateTime.UtcNow.AddMinutes(10),
        "db12691295ffe0382b005c686ac7478326b0c0d6",
        "54d75a634005afb8d42704f761a820ed16b1b9d4", "95f7422e284bc81bcfcf9f460dace52022de5cc7");

    public static IEnumerable<object[]> AllCases => Enum.GetValues<CleaningCompletionAcceptanceCase>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(AllCases))]
    public async Task Every_case_refuses_to_create_context_without_external_authority(CleaningCompletionAcceptanceCase scenario)
    {
        var calls = 0; var contexts = 0;
        var runtime = new CleaningCompletionAcceptanceRuntime(Metadata, (_, _) =>
        {
            calls++;
            throw new InvalidOperationException("External authority deliberately denied in no-SQL test.");
        }, _ => { contexts++; throw new InvalidOperationException("SQL context forbidden."); }, null!, null!, "", "", _ => throw new InvalidOperationException("Fixture context forbidden."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CleaningCompletionAcceptanceHarness(runtime).RunAsync(scenario, default));
        Assert.Equal(1, calls); Assert.Equal(0, contexts);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("local-time")]
    [InlineData("owner")]
    [InlineData("lease-id")]
    [InlineData("database-user")]
    [InlineData("shared-database")]
    [InlineData("unsupported-trial")]
    [InlineData("fresh-r05-not-supported-by-existing-gates")]
    [InlineData("notification-pin")]
    [InlineData("fe05-pin")]
    [InlineData("source-pin")]
    public async Task Unsafe_or_incomplete_envelope_never_reaches_authority_or_context(string field)
    {
        var meta = Metadata;
        meta = field switch
        {
            "expired" => meta with { ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            "local-time" => meta with { ExpiresAtUtc = DateTime.SpecifyKind(meta.ExpiresAtUtc, DateTimeKind.Local) },
            "owner" => meta with { Owner = "OTHER" },
            "lease-id" => meta with { LeaseId = "" },
            "database-user" => meta with { DatabaseUser = "" },
            "shared-database" => meta with { Database = "ENVNOTIF001" },
            "unsupported-trial" => meta with { Database = "HotelOps_Canonical_FS018_Cancellation_V1_Fresh" },
            "fresh-r05-not-supported-by-existing-gates" => meta with { Database = "HotelOps_Canonical_FE04_R05_20261110_Fresh" },
            "notification-pin" => meta with { NotificationSourceSha = "TO_VERIFY" },
            "fe05-pin" => meta with { Fe05ConsumerSha = new string('0', 40) },
            _ => meta with { SourceSha = "" }
        };
        var authorityCalls = 0;
        var runtime = new CleaningCompletionAcceptanceRuntime(meta, (_, _) => { authorityCalls++; return Task.CompletedTask; },
            _ => throw new InvalidOperationException("Context forbidden."), null!, null!, "", "", _ => throw new InvalidOperationException("Fixture context forbidden."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CleaningCompletionAcceptanceHarness(runtime).ValidateAuthorityAsync(default));
        Assert.Equal(0, authorityCalls);
    }

    [Fact]
    public async Task UC034_independent_preparation_does_not_require_FE05_consumer_but_still_requires_lease_authority()
    {
        var authorityCalls = 0;
        var runtime = new CleaningCompletionAcceptanceRuntime(Metadata with { Fe05ConsumerSha = null },
            (_, _) => { authorityCalls++; throw new InvalidOperationException("No approved lease."); },
            _ => throw new InvalidOperationException("Context forbidden."), null!, null!, "", "",
            _ => throw new InvalidOperationException("Fixture context forbidden."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CleaningCompletionAcceptanceHarness(runtime).ValidateAuthorityAsync(default));
        Assert.Equal(1, authorityCalls);
    }
    [Fact]
    public async Task Cancelled_run_does_not_contact_authority_or_create_context()
    {
        var runtime = new CleaningCompletionAcceptanceRuntime(Metadata,
            (_, _) => throw new InvalidOperationException("Authority must not be called."),
            _ => throw new InvalidOperationException("Context must not be created."), null!, null!, "", "", _ => throw new InvalidOperationException("Fixture context forbidden."));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CleaningCompletionAcceptanceHarness(runtime).RunAsync(CleaningCompletionAcceptanceCase.Authorized, cancellation.Token));
    }

    [Fact]
    public async Task Changed_physical_target_is_refused_before_connection_open()
    {
        var network = new ForbiddenConnection();
        HotelOpsDbContext ForeignContext(IReadOnlyList<IInterceptor> _)
            => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
                .UseSqlServer("Server=unused.invalid;Database=ENVNOTIF001;Integrated Security=true")
                .AddInterceptors(network).Options);
        var runtime = new CleaningCompletionAcceptanceRuntime(Metadata, (_, _) => Task.CompletedTask,
            ForeignContext, null!, null!, "", "", ForeignContext);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CleaningCompletionAcceptanceHarness(runtime).RunAsync(CleaningCompletionAcceptanceCase.Authorized, default));
        Assert.Equal("UC034_RUNTIME_TARGET_MISMATCH", error.Message);
        Assert.False(network.Contacted);
    }

    [Theory]
    [InlineData(CleaningCompletionAcceptanceCase.AttributionFailure, "EXEC dbo.fe04_capture_cleaner_attribution @task=1")]
    [InlineData(CleaningCompletionAcceptanceCase.ReadinessFailure, "INSERT dbo.room_readiness_events")]
    [InlineData(CleaningCompletionAcceptanceCase.NotificationPublicationFailure, "INSERT dbo.notification_publications")]
    [InlineData(CleaningCompletionAcceptanceCase.NotificationIntentFailure, "INSERT dbo.notification_delivery_intents")]
    [InlineData(CleaningCompletionAcceptanceCase.CompletionAuditFailure, "INSERT INTO [audit_logs]")]
    public async Task Case_specific_SQL_fault_fires_on_reader_and_nonquery_paths(CleaningCompletionAcceptanceCase scenario, string sql)
    {
        using var command = new SqlCommand(sql);
        if (scenario == CleaningCompletionAcceptanceCase.CompletionAuditFailure)
            command.Parameters.AddWithValue("@new", "{\"action\":\"COMPLETE_CLEANING\"}");
        var reader = new CleaningCompletionWriteFault(scenario);
        await Assert.ThrowsAsync<CleaningCompletionInjectedFaultException>(async () =>
            await reader.ReaderExecutingAsync(command, null!, default));
        Assert.True(reader.Fired);
        var nonquery = new CleaningCompletionWriteFault(scenario);
        await Assert.ThrowsAsync<CleaningCompletionInjectedFaultException>(async () =>
            await nonquery.NonQueryExecutingAsync(command, null!, default));
        Assert.True(nonquery.Fired);
    }

    [Fact]
    public async Task Completion_audit_fault_does_not_fire_at_earlier_WorkTime_interval_audit()
    {
        using var command = new SqlCommand("INSERT INTO [audit_logs]");
        command.Parameters.AddWithValue("@new", "{\"intervalId\":6,\"ClosedAt\":\"2026-11-11\"}");
        var fault = new CleaningCompletionWriteFault(CleaningCompletionAcceptanceCase.CompletionAuditFailure);
        Assert.False(fault.Matches(command));
        await fault.ReaderExecutingAsync(command, null!, default);
        Assert.False(fault.Fired);
    }

    [Fact]
    public async Task Faults_do_not_fire_for_unrelated_SELECTs()
    {
        using var command = new SqlCommand("SELECT * FROM dbo.audit_logs");
        foreach (var scenario in Enum.GetValues<CleaningCompletionAcceptanceCase>())
        {
            var fault = new CleaningCompletionWriteFault(scenario);
            await fault.ReaderExecutingAsync(command, null!, default);
            Assert.False(fault.Fired);
        }
    }

    [Fact]
    public async Task Precommit_fault_occurs_before_ack_callback_and_is_one_shot()
    {
        var fault = new CleaningCompletionCommitFault(CleaningCompletionAcceptanceCase.BeforeCommitFailure);
        await Assert.ThrowsAsync<CleaningCompletionInjectedFaultException>(async () =>
            await fault.TransactionCommittingAsync(null!, null!, default));
        Assert.True(fault.Fired);
        await fault.TransactionCommittingAsync(null!, null!, default);
        await fault.TransactionCommittedAsync(null!, null!);
    }

    [Fact]
    public async Task Lost_ack_occurs_only_after_commit_and_recovery_can_commit()
    {
        var fault = new CleaningCompletionCommitFault(CleaningCompletionAcceptanceCase.LostCommitAcknowledgement);
        await fault.TransactionCommittingAsync(null!, null!, default);
        Assert.False(fault.Fired);
        await Assert.ThrowsAsync<OperationCanceledException>(() => fault.TransactionCommittedAsync(null!, null!));
        Assert.True(fault.Fired);
        await fault.TransactionCommittingAsync(null!, null!, default);
        await fault.TransactionCommittedAsync(null!, null!);
    }

    [Fact]
    public async Task Lease_revocation_callback_prevents_provider_commit()
    {
        var calls = 0;
        var guard = new CleaningCompletionLeaseCommitGuard(_ => { calls++; throw new InvalidOperationException("Lease revoked."); });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await guard.TransactionCommittingAsync(null!, null!, default));
        Assert.Equal(1, calls);
    }

    private sealed class ForbiddenConnection : DbConnectionInterceptor
    {
        public bool Contacted { get; private set; }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        { Contacted = true; throw new InvalidOperationException("Network forbidden in no-SQL tests."); }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        { Contacted = true; throw new InvalidOperationException("Network forbidden in no-SQL tests."); }
    }
}