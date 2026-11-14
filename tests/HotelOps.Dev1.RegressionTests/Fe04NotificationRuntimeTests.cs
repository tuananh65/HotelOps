using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Services.Housekeeping.Acceptance;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// All records/keys are synthetic unit fixtures, never owner or SQL runtime evidence.
public sealed class Fe04NotificationRuntimeTests
{
    private static readonly DateTimeOffset Now = NotifWriter004AContract.HardExpiry.AddMinutes(-30);
    private static readonly string Hash = new('A', 64);
    private static Fe04NotificationTrialContract Contract()
    {
        var permissions = new List<Fe04Permission>();
        foreach (var table in new[] { "notification_operation_reservations", "notification_publications", "notification_delivery_intents", "cleaning_assignments" })
        {
            permissions.Add(new("dbo." + table, "INSERT", null, false));
            permissions.Add(new("dbo." + table, "DELETE", null, false));
            if (table != "notification_delivery_intents") permissions.Add(new("dbo." + table, "UPDATE", null, false));
        }
        permissions.Add(new("dbo.fs013_assign_with_evidence", "EXECUTE", null, true));
        permissions.Add(new("dbo.fs009_verify_assignment_guard", "EXECUTE", null, true));
        return new(Fe04NotificationApproval.Version, NotifWriter004AContract.Server, NotifWriter004AContract.Database,
            NotifWriter004AContract.DatabaseGuid, NotifWriter004AContract.Owner,
            Fe04NotificationApproval.Login, Fe04NotificationApproval.Login, "SQL_LOGIN",
            "dbo.fs013_assign_with_evidence", "CALLER_STATIC_DBO_CHAIN", Guid.NewGuid(), Now.AddMinutes(-30),
            NotifWriter004AContract.HardExpiry, new('a', 40), Hash, Hash,
            new() { ["scripts/test-only.sql"] = Hash },
            new() { ["dbo.fs013_assign_with_evidence"] = Hash, ["dbo.fs009_verify_assignment_guard"] = Hash, ["dbo.TR_fs009_publication_source"] = Hash },
            permissions.ToArray(), Hash, "SYNTHETIC_UNIT_FIXTURE_ONLY",
            Enumerable.Range(1, 15).Select(i => i.ToString("D2")).ToArray(),
            Fe04NotificationApproval.Decisions.ToDictionary(d => d, _ => "ACCEPTED"));
    }

    [Fact]
    public void Synthetic_complete_contract_passes_structural_validation_only() => Fe04NotificationApproval.Require(Contract(), Now);

    [Theory]
    [InlineData("server")][InlineData("database")][InlineData("guid")][InlineData("owner")]
    [InlineData("login")][InlineData("user")][InlineData("auth")][InlineData("module")][InlineData("context")]
    public void Wrong_identity_never_passes(string field)
    {
        var c = Contract();
        c = field switch
        {
            "server" => c with { Server = "KIENPC" }, "database" => c with { Database = "HotelOpsDB" },
            "guid" => c with { DatabaseGuid = Guid.Empty }, "owner" => c with { Owner = "other" },
            "login" => c with { Login = "DESKTOP-JI8U50T\\hax" }, "user" => c with { DatabaseUser = "dbo" },
            "auth" => c with { Authentication = "WINDOWS" }, "module" => c with { Module = "dbo.fs013_unapproved_writer_probe" },
            _ => c with { ExecutionContext = "OWNER" }
        };
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(c, Now));
    }

    [Theory]
    [InlineData("authentication")][InlineData("mapping")][InlineData("permissions")][InlineData("commonTarget")]
    [InlineData("schema")][InlineData("fixtures")][InlineData("lease")][InlineData("mutationScope")]
    public void Pending_owner_decision_cannot_be_accepted(string decision)
    {
        var c = Contract(); c.Decisions[decision] = "PENDING";
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(c, Now));
    }

    [Fact]
    public void Missing_expired_future_or_extended_lease_rejected()
    {
        var c = Contract();
        foreach (var bad in new[] { c with { LeaseId = Guid.Empty }, c with { LeaseEnd = Now },
            c with { LeaseStart = Now.AddMinutes(1) }, c with { LeaseEnd = NotifWriter004AContract.HardExpiry.AddTicks(1) } })
            Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(bad, Now));
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(c, c.LeaseEnd));
    }

    [Fact]
    public void Incomplete_mutation_scope_or_missing_deny_rejected()
    {
        var c = Contract();
        foreach (var bad in new[] { c with { MutationCases = ["01"] }, c with { Permissions = c.Permissions.Skip(1).ToArray() },
            c with { Permissions = c.Permissions.Concat([c.Permissions[0]]).ToArray() }, c with { FixtureManifestSha256 = "expected-is-not-evidence" } })
            Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(bad, Now));
    }

    [Theory]
    [InlineData("../outside.sql")][InlineData("C:/outside.sql")][InlineData("scripts/../../outside.sql")]
    [InlineData("scripts//ambiguous.sql")]
    public void Source_path_escape_rejected(string path)
    {
        var c = Contract() with { Files = new() { [path] = Hash } };
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(c, Now));
    }

    [Fact]
    public void Caller_approval_records_need_real_pinned_owner_signatures()
    {
        using var rsa = RSA.Create(3072);
        var payload = JsonSerializer.Serialize(Contract());
        var signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(Fe04NotificationApproval.Version + "\n" + payload),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var signatures = Fe04NotificationApproval.Owners.Select(o => new Fe04OwnerSignature(o, rsa.ExportSubjectPublicKeyInfoPem(), signature)).ToArray();
        var pins = Fe04NotificationApproval.Owners.ToDictionary(o => o, _ => Fe04NotificationApproval.Hash(rsa.ExportSubjectPublicKeyInfo()));
        var packet = new Fe04SignedApproval(payload, signatures);
        Assert.Equal(Fe04NotificationApproval.Version, Fe04NotificationApproval.Verify(packet, pins, Now).Version);
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Verify(packet, new Dictionary<string, string>(), Now));
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Verify(packet with { Payload = payload + " " }, pins, Now));
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Verify(packet with { Signatures = signatures.Skip(1).ToArray() }, pins, Now));
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Verify(packet with { Signatures = signatures.Concat([signatures[0]]).ToArray() }, pins, Now));
    }

    [Fact]
    public void Launcher_has_exact_target_no_integrated_or_tls_fallback()
    {
        var cs = new SqlConnectionStringBuilder(Fe04NotificationRuntimeAdapter.ConnectionString());
        Assert.Equal("tcp:DESKTOP-JI8U50T,1433", cs.DataSource);
        Assert.Equal(NotifWriter004AContract.Database, cs.InitialCatalog);
        Assert.False(cs.IntegratedSecurity); Assert.False(cs.TrustServerCertificate);
        Assert.False(cs.PersistSecurityInfo); Assert.False(cs.Pooling);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, cs.Encrypt);
        Assert.Empty(cs.Password); Assert.Empty(cs.UserID);
    }

    [Fact]
    public async Task Actual_source_fixture_and_loaded_assembly_bytes_are_bound()
    {
        var root = Path.Combine(Path.GetTempPath(), "fe04-final002-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "source.sql"); var fixture = Path.Combine(root, "fixture.json");
        try
        {
            await File.WriteAllTextAsync(file, "UNIT_TEST_ONLY_SOURCE");
            await File.WriteAllTextAsync(fixture, "UNIT_TEST_ONLY_FIXTURE");
            var c = Contract() with
            {
                SourceCommit = typeof(Fe04NotificationApproval).Assembly
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+').Last(),
                Files = new() { ["source.sql"] = Fe04NotificationApproval.Hash(await File.ReadAllBytesAsync(file)) },
                FixtureManifestSha256 = Fe04NotificationApproval.Hash(await File.ReadAllBytesAsync(fixture)),
                AssemblySha256 = Fe04NotificationApproval.Hash(await File.ReadAllBytesAsync(typeof(Fe04NotificationApproval).Assembly.Location))
            };
            await Fe04NotificationApproval.VerifyFilesAsync(c, root, fixture, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Fe04NotificationApproval.VerifyFilesAsync(c with { SourceCommit = new('0', 40) }, root, fixture, default));
            await File.AppendAllTextAsync(file, "MUTATION");
            await Assert.ThrowsAsync<InvalidOperationException>(() => Fe04NotificationApproval.VerifyFilesAsync(c, root, fixture, default));
            c.Files["source.sql"] = Fe04NotificationApproval.Hash(await File.ReadAllBytesAsync(file));
            await File.AppendAllTextAsync(fixture, "MUTATION");
            await Assert.ThrowsAsync<InvalidOperationException>(() => Fe04NotificationApproval.VerifyFilesAsync(c, root, fixture, default));
            c = c with { FixtureManifestSha256 = Fe04NotificationApproval.Hash(await File.ReadAllBytesAsync(fixture)), AssemblySha256 = Hash };
            await Assert.ThrowsAsync<InvalidOperationException>(() => Fe04NotificationApproval.VerifyFilesAsync(c, root, fixture, default));
        }
        finally
        {
            File.Delete(file); File.Delete(fixture); Directory.Delete(root);
        }
    }

    [Fact]
    public void Missing_execute_or_column_graph_grants_cannot_weaken_denials()
    {
        var c = Contract();
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(c with
        { Permissions = c.Permissions.Where(p => p.Object != c.Module).ToArray() }, Now));
        Assert.Throws<InvalidOperationException>(() => Fe04NotificationApproval.Require(c with
        { Permissions = c.Permissions.Concat([new Fe04Permission("dbo.notification_publications", "UPDATE", "payload_json", true)]).ToArray() }, Now));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Rollback_is_mandatory_on_success_and_failure(bool fail)
    {
        using var connection = new ProbeConnection(); using var transaction = new ProbeTransaction(connection);
        var task = Fe04AcceptanceTransaction.RollbackOnExitAsync(connection, transaction, _ => fail ? Task.FromException(new InvalidOperationException("stage failed")) : Task.CompletedTask, default);
        if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() => task); else await task;
        Assert.Equal(1, transaction.Rollbacks); Assert.Equal(0, transaction.Commits);
    }

    [Fact]
    public async Task Cancellation_rolls_back_with_uncancelled_token()
    {
        using var connection = new ProbeConnection(); using var transaction = new ProbeTransaction(connection);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fe04AcceptanceTransaction.RollbackOnExitAsync(connection, transaction,
            _ => throw new Exception("must not run"), cancelled.Token));
        Assert.Equal(1, transaction.Rollbacks); Assert.False(transaction.RollbackTokenCancelled);
    }

    [Fact]
    public async Task Wrong_connection_isolation_and_early_commit_cannot_pass()
    {
        using var connection = new ProbeConnection(); using var other = new ProbeConnection();
        using var wrong = new ProbeTransaction(other);
        Assert.Throws<InvalidOperationException>(() => Fe04AcceptanceTransaction.Require(connection, wrong));
        using var weak = new ProbeTransaction(connection, IsolationLevel.ReadCommitted);
        Assert.Throws<InvalidOperationException>(() => Fe04AcceptanceTransaction.Require(connection, weak));
        using var premature = new ProbeTransaction(connection);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Fe04AcceptanceTransaction.RollbackOnExitAsync(connection, premature,
            _ => { premature.Commit(); return Task.CompletedTask; }, default));
    }

    private sealed class ProbeConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "NO_SQL"; public override string DataSource => "NO_SQL";
        public override string ServerVersion => "NO_SQL"; public override ConnectionState State => ConnectionState.Open;
        public override void Open() => throw new NotSupportedException(); public override void Close() { }
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
    private sealed class ProbeTransaction(ProbeConnection connection, IsolationLevel isolation = IsolationLevel.Serializable) : DbTransaction
    {
        private bool completed;
        public int Rollbacks { get; private set; } public int Commits { get; private set; }
        public bool RollbackTokenCancelled { get; private set; }
        protected override DbConnection? DbConnection => completed ? null : connection;
        public override IsolationLevel IsolationLevel => isolation;
        public override void Commit() { Commits++; completed = true; }
        public override void Rollback() { Rollbacks++; completed = true; }
        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        { RollbackTokenCancelled = cancellationToken.IsCancellationRequested; Rollback(); return Task.CompletedTask; }
    }
}
