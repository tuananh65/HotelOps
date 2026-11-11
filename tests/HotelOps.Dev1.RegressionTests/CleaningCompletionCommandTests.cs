using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Transactions;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Models;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;
using IsolationLevel = System.Data.IsolationLevel;

namespace HotelOps.Dev1.RegressionTests;

// No SQL connection, synthetic durable receipts and a provider transaction double.
// These tests do NOT establish database atomicity, successful notification delivery,
// full-command completion, SQL concurrency or FE05 domain-writer acceptance.
public sealed class CleaningCompletionCommandTests
{
    private const string Tag = "\"0000000000000001\"";
    private static readonly Guid Operation = Guid.Parse("12c44400-7ca4-4da1-8990-8d2fc258f211");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static CleaningClosureResult Receipt => new(3, 2, 4, Operation, Guid.NewGuid(), 5, 6,
        "PENDING_INSPECTION", new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc),
        "PENDING_INSPECTION", false, Tag);

    private static NotificationOperationCandidate Stored(string? json) => new()
    {
        ActorUserId = 1, OperationKey = Operation, FingerprintVersion = 1,
        Fingerprint = new byte[32], Completed = true, ResultJson = json
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"invalid\"}")]
    [InlineData("{\"occurredAtUtc\":\"invalid\"}")]
    public void Corrupt_durable_replay_receipt_fails_closed_without_JSON_exception(string? json)
        => Assert.Null(RoomTypeCreationService.ReadClosureReceipt(Stored(json), 2, 3, 4, Operation, true));

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 2)]
    public void Matching_result_does_not_promote_incomplete_or_unsupported_operation(bool completed, int version)
    {
        var stored = Stored(JsonSerializer.Serialize(Receipt, Json));
        stored.Completed = completed; stored.FingerprintVersion = version;
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 2, 3, 4, Operation, true));
    }

    [Fact]
    public void Valid_synthetic_replay_preserves_original_receipt_and_cannot_be_retargeted()
    {
        var expected = Receipt;
        var stored = Stored(JsonSerializer.Serialize(expected, Json));
        Assert.Equal(expected, RoomTypeCreationService.ReadClosureReceipt(stored, 2, 3, 4, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 9, 3, 4, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 2, 9, 4, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 2, 3, 9, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 2, 3, 4, Guid.NewGuid(), true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 2, 3, 4, Operation, false));
    }

    [Theory]
    [InlineData("READY")]
    [InlineData("COMPLETED")]
    [InlineData("PASSED")]
    [InlineData("PENDING_REINSPECTION")]
    public void Replay_never_promotes_foreign_lifecycle_state_or_readiness(string value)
    {
        var receipt = Receipt;
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(
            Stored(JsonSerializer.Serialize(receipt with { State = value }, Json)), 2, 3, 4, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(
            Stored(JsonSerializer.Serialize(receipt with { Readiness = value }, Json)), 2, 3, 4, Operation, true));
    }

    [Fact]
    public void Completion_fingerprint_binds_progress_cycle_scope_and_evidence_content()
    {
        var photo = new EvidenceUpload("evidence.png", "image/png", "AAAA");
        byte[] Fingerprint(long branch = 2, long task = 3, string progress = Tag,
            IReadOnlyList<EvidenceUpload>? photos = null, long? reason = null)
            => RoomTypeCreationService.ClosureFingerprint(1, branch, task, Tag, reason, "completed", progress, photos);
        var original = Fingerprint(photos: [photo]);
        Assert.NotEqual(original, Fingerprint(branch: 8, photos: [photo]));
        Assert.NotEqual(original, Fingerprint(task: 8, photos: [photo]));
        Assert.NotEqual(original, Fingerprint(progress: "\"0000000000000002\"", photos: [photo]));
        Assert.NotEqual(original, Fingerprint(photos: [photo with { Base64 = "AAAB" }]));
        Assert.NotEqual(original, Fingerprint(photos: [photo with { Name = "other.png" }]));
        Assert.NotEqual(original, Fingerprint(photos: [photo with { MimeType = "image/jpeg" }]));
        Assert.NotEqual(original, Fingerprint(photos: [photo], reason: 7));
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("actor")]
    [InlineData("session")]
    [InlineData("restricted-actor")]
    [InlineData("restricted-ticket")]
    public async Task Actual_completion_command_rejects_invalid_identity_before_SQL(string scenario)
    {
        using var connection = new NoSqlConnection();
        await using var db = Context(connection);
        var current = new Identity(scenario != "absent",
            new(scenario == "actor" ? 99 : 1, scenario == "session" ? 99 : 1, scenario == "restricted-actor"));
        var ticket = SessionReference.FromProtectedTicket(1, SessionCredential.Generate(),
            scenario == "restricted-ticket", DateTime.UtcNow);
        var service = new RoomTypeCreationService(db, current, null!, null!, TimeProvider.System);
        var result = await service.CompleteCleaningAsync(ticket, 1, 2, 3, Tag, Operation.ToString(), new("completed", Tag), default);
        Assert.Equal(RoomTypeBatchStatus.SessionInvalid, result.Status);
        Assert.Null(connection.Transaction);
        Assert.Equal(0, connection.Commands);
    }

    [Theory]
    [InlineData("existing-transaction")]
    [InlineData("tracked-entity")]
    [InlineData("ambient-transaction")]
    public async Task Actual_completion_command_refuses_nonclean_transaction_context(string scenario)
    {
        using var connection = new NoSqlConnection();
        await using var db = Context(connection);
        await using var tx = scenario == "existing-transaction" ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        using var ambient = scenario == "ambient-transaction"
            ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : null;
        if (scenario == "tracked-entity") db.CleaningTasks.Add(new CleaningTask());
        var service = new RoomTypeCreationService(db, new Identity(true, new(1, 1, false)), null!, null!, TimeProvider.System);
        var ticket = SessionReference.FromProtectedTicket(1, SessionCredential.Generate(), false, DateTime.UtcNow);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteCleaningAsync(
            ticket, 1, 2, 3, Tag, Operation.ToString(), new("completed", Tag), default));
        Assert.Contains("clean local SQL context", error.Message);
        Assert.False(connection.Transaction?.Committed ?? false);
        Assert.Equal(0, connection.Commands);
    }

    [Theory]
    [InlineData("foreign-connection")]
    [InlineData("foreign-transaction")]
    [InlineData("read-committed")]
    [InlineData("ambient-transaction")]
    public async Task Actual_FE05_reader_rejects_unsafe_transaction_bindings_before_SQL(string scenario)
    {
        using var connection = new NoSqlConnection();
        using var foreign = new NoSqlConnection();
        await using var db = Context(connection);
        await using var tx = await db.Database.BeginTransactionAsync(
            scenario == "read-committed" ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable);
        using var foreignTx = foreign.BeginTransaction(IsolationLevel.Serializable);
        using var ambient = scenario == "ambient-transaction"
            ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : null;
        var service = new RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadInspectionCleanerAttributionAsync(
            scenario == "foreign-connection" ? foreign : connection,
            scenario == "foreign-transaction" ? foreignTx : tx.GetDbTransaction(), 1, 2, default));
        Assert.Equal(0, connection.Commands); Assert.Equal(0, foreign.Commands);
        Assert.False(connection.Transaction!.Committed);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public async Task Actual_FE05_reader_accepts_caller_transaction_but_rejects_invalid_scope_without_SQL(long inspection, long branch)
    {
        using var connection = new NoSqlConnection();
        await using var db = Context(connection);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var service = new RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
        Assert.Null(await service.ReadInspectionCleanerAttributionAsync(connection, tx.GetDbTransaction(), inspection, branch, default));
        Assert.Equal(0, connection.Commands);
        Assert.False(connection.Transaction!.Committed);
    }

    [Theory]
    [InlineData("Production", "FS015_PROVISIONAL")]
    [InlineData("Development", null)]
    [InlineData("Development", "FS015_PROVISIONAL")]
    public async Task Existing_closure_gate_rejects_fresh_R05_target_without_SQL(string environment, string? flag)
    {
        using var connection = new NoSqlConnection();
        await using var db = Context(connection);
        var publisher = Publisher(db, environment, flag);
        Assert.False(await publisher.ClosureAvailableAsync(default));
        Assert.Equal(0, connection.Commands);
    }

    [Fact]
    public async Task Actual_mandatory_staging_and_proof_refuse_missing_caller_transaction()
    {
        using var connection = new NoSqlConnection();
        await using var db = Context(connection);
        var publisher = Publisher(db, "Development", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.StageClosureAsync(1, new byte[32], 7, [8], Receipt, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.VerifyClosureAsync(1, Operation, 1, default));
        Assert.Equal(0, connection.Commands); Assert.Null(connection.Transaction);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("encoding")]
    [InlineData("mime")]
    [InlineData("extension")]
    [InlineData("empty")]
    public async Task Actual_evidence_validator_rejects_invalid_files_before_storage_or_SQL(string scenario)
    {
        using var connection = new NoSqlConnection();
        await using var db = Context(connection);
        var service = new RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
        var photo = new EvidenceUpload("evidence.png", "image/png", "iVBORw0KGgo=");
        photo = scenario switch
        {
            "path" => photo with { Name = "../evidence.png" },
            "encoding" => photo with { Base64 = "!invalid!" },
            "mime" => photo with { MimeType = "image/jpeg" },
            "extension" => photo with { Name = "evidence.jpg" },
            _ => photo with { Base64 = "" }
        };
        var owned = new List<string>();
        // Invoke the unchanged production validator used by UC034. No substitute validator.
        var method = typeof(RoomTypeCreationService).GetMethod("StoreFeatureEvidence", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await (Task<long>)method.Invoke(service, ["CLEANING_TASK", 3L, 1L, photo,
                Path.Combine(Path.GetTempPath(), "UC034-MUST-NOT-CREATE-" + Guid.NewGuid()), owned, CancellationToken.None])!);
        Assert.Empty(owned); Assert.Equal(0, connection.Commands);
    }

    private static HotelOpsDbContext Context(DbConnection connection)
        => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);

    private static ProvisionalCleaningNotification Publisher(HotelOpsDbContext db, string environment, string? flag)
        => new(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["CleaningClosure:Candidate"] = flag }).Build(), new EnvironmentStub { EnvironmentName = environment });

    private sealed class Identity(bool present, CurrentUserInfo value) : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = value; return present; }
    }

    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "UC034-NoSQL";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NoSqlConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "HotelOps_Canonical_FE04_R05_20261110_Fresh";
        public override string DataSource => "UC034-NoSQL";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => ConnectionState.Open;
        public NoSqlTransaction? Transaction { get; private set; }
        public int Commands { get; private set; }
        public override void Open() { }
        public override void Close() { }
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand()
        {
            Commands++;
            throw new InvalidOperationException("SQL forbidden in UC034 no-SQL component tests.");
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => Transaction = new(this, isolationLevel);
    }

    private sealed class NoSqlTransaction(DbConnection connection, IsolationLevel isolation) : DbTransaction
    {
        public bool Committed { get; private set; }
        protected override DbConnection DbConnection => connection;
        public override IsolationLevel IsolationLevel => isolation;
        public override void Commit() => Committed = true;
        public override void Rollback() { }
    }
}