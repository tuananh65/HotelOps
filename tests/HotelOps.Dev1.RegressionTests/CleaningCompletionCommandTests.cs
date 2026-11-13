using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Models;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// No SQL test: validates fail-closed policy/receipt boundary, not committed SQL atomicity.
public sealed class CleaningCompletionCommandTests
{
    private const string Tag = "\"0000000000000001\"";
    private static readonly Guid Operation = Guid.Parse("12c44400-7ca4-4da1-8990-8d2fc258f211");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static byte[] Fingerprint => SHA256.HashData("authorized completion request"u8);
    private static CleaningClosureResult Receipt => new(3, 2, 4, Operation, Guid.NewGuid(), 5, 6,
        "PENDING_INSPECTION", new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc),
        "PENDING_INSPECTION", false, Tag);

    private static NotificationOperationCandidate Stored(string? json) => new()
    {
        ActorUserId = 1, OperationKey = Operation, FingerprintVersion = 1,
        Fingerprint = Fingerprint, Completed = true, ResultJson = json
    };

    private static CleaningClosureResult? Read(NotificationOperationCandidate candidate)
        => RoomTypeCreationService.ReadClosureReceipt(candidate, 1, Fingerprint, 2, 3, 4, Operation, true);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"invalid\"}")]
    [InlineData("{\"occurredAtUtc\":\"invalid\"}")]
    public void Invalid_durable_receipts_fail_closed_without_JSON_exceptions(string? json)
        => Assert.Null(Read(Stored(json)));

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 2)]
    public void Incomplete_or_wrong_version_operations_never_replay(bool completed, int version)
    {
        var operation = Stored(JsonSerializer.Serialize(Receipt, Json));
        operation.Completed = completed;
        operation.FingerprintVersion = version;
        Assert.Null(Read(operation));
    }

    [Fact]
    public void Durable_receipt_binds_actor_operation_and_exact_request_fingerprint()
    {
        var receipt = Receipt;
        var stored = Stored(JsonSerializer.Serialize(receipt, Json));
        Assert.Equal(receipt, Read(stored));
        stored.ActorUserId = 99;
        Assert.Null(Read(stored));
        stored.ActorUserId = 1;
        stored.OperationKey = Guid.NewGuid();
        Assert.Null(Read(stored));
        stored.OperationKey = Operation;
        stored.Fingerprint = SHA256.HashData("tampered"u8);
        Assert.Null(Read(stored));
        stored.Fingerprint = new byte[31];
        Assert.Null(Read(stored));
    }

    [Fact]
    public void Durable_receipt_rejects_foreign_scope_lifecycle_and_source_revision()
    {
        var receipt = Receipt;
        var stored = Stored(JsonSerializer.Serialize(receipt, Json));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, 7, 3, 4, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, 2, 7, 4, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, 2, 3, 7, Operation, true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, 2, 3, 4, Guid.NewGuid(), true));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, 2, 3, 4, Operation, false));
        Assert.Null(Read(Stored(JsonSerializer.Serialize(receipt with { State = "READY" }, Json))));
        Assert.Null(Read(Stored(JsonSerializer.Serialize(receipt with { Readiness = "READY" }, Json))));
        Assert.Null(Read(Stored(JsonSerializer.Serialize(receipt with { SourceETag = "weak" }, Json))));
    }

    [Fact]
    public void Durable_receipt_rejects_an_empty_operation_even_when_stored_fields_agree()
    {
        var stored = Stored(JsonSerializer.Serialize(Receipt with { OperationId = Guid.Empty }, Json));
        stored.OperationKey = Guid.Empty;
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, 2, 3, 4, Guid.Empty, true));
    }

    [Theory]
    [InlineData(0, 3, 4)]
    [InlineData(2, 0, 4)]
    [InlineData(2, 3, 0)]
    public void Durable_receipt_requires_positive_source_identity_even_when_stored_fields_agree(long branch, long task, long assignment)
    {
        var stored = Stored(JsonSerializer.Serialize(Receipt with { BranchId = branch, Id = task, AssignmentId = assignment }, Json));
        Assert.Null(RoomTypeCreationService.ReadClosureReceipt(stored, 1, Fingerprint, branch, task, assignment, Operation, true));
    }

    [Theory]
    [InlineData(false, true, true, "CLEANER_ATTRIBUTION_SCHEMA_REQUIRED")]
    [InlineData(true, false, true, "CLEANER_ATTRIBUTION_SOURCE_AMBIGUOUS")]
    [InlineData(true, true, false, "CLEANER_ATTRIBUTION_AS_OF_COMPLETION_AUTHORITY_REQUIRED")]
    public void Mandatory_attribution_policy_fails_closed(bool schema, bool unique, bool historical, string expected)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RoomTypeCreationService.RequireCleanerCapturePolicy(schema, unique, historical));
        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public void Mandatory_attribution_policy_accepts_only_verified_inputs()
        => RoomTypeCreationService.RequireCleanerCapturePolicy(true, true, true);

    [Fact]
    public void Multiple_historical_contributors_cannot_be_elected_as_original_cleaner()
    {
        Assert.False(CleanerAttributionContract.UniqueCleaner(7, [7, 8], [7]));
        Assert.False(CleanerAttributionContract.UniqueCleaner(7, [7], [7, 8]));
        Assert.False(CleanerAttributionContract.UniqueCleaner(7, [], [7]));
    }

    [Fact]
    public async Task Current_unauthorized_completion_never_enters_a_SQL_transaction()
    {
        using var connection = new NoSqlConnection();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection).Options);
        var service = new RoomTypeCreationService(db, new MissingCurrentUser(), null!, null!, TimeProvider.System);
        var ticket = SessionReference.FromProtectedTicket(1, SessionCredential.Generate(), false, DateTime.UtcNow);
        var result = await service.CompleteCleaningAsync(ticket, 1, 2, 3, Tag, Operation.ToString(),
            new("completed", Tag), CancellationToken.None);
        Assert.Equal(RoomTypeBatchStatus.SessionInvalid, result.Status);
        Assert.Equal(0, connection.Commands);
    }

    private sealed class MissingCurrentUser : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = default!; return false; }
    }

    private sealed class NoSqlConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "UC034_Test_Only";
        public override string DataSource => "NoSQL";
        public override string ServerVersion => "16.0";
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Open;
        public int Commands { get; private set; }
        public override void Open() { }
        public override void Close() { }
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand()
        {
            Commands++;
            throw new InvalidOperationException("SQL forbidden in deterministic receipt tests.");
        }
        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel level)
            => throw new InvalidOperationException("SQL transactions forbidden in deterministic receipt tests.");
    }
}
