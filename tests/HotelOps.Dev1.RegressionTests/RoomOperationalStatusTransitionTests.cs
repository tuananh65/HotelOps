using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Rooms;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Offline policy/contract tests only. FakeTransaction does not execute SQL or prove atomicity.
// Clear evidence below is used ONLY to test attribution/identity policies, never to run a writer
// or bypass D05. No accepted FE04/FE06/FE12 adapter exists in this increment.
public sealed class RoomOperationalStatusTransitionTests
{
    private static FreshStartCleaningTransitionContext Context =>
        new("START_CLEANING", 11, 22, 33, 44, 55, "\"0000000000000001\"", "Begin assigned cleaning");

    private static HotelOpsDbContext Database() => new(new DbContextOptionsBuilder<HotelOpsDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);

    private static VerifiedFreshStartCleaning Source(HotelOpsDbContext db, DbConnection connection, DbTransaction transaction) =>
        new(db, connection, transaction, 11, 22, 33, 44, 55, 44, "ASSIGNED", "IN_PROGRESS",
            Context.ExpectedSourceRevision, "\"0000000000000002\"", Guid.Parse("97cfd8fa-282f-48df-bda8-5159fd3c82f5"),
            123, new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), RoomStartBlockEvidence.Clear);

    private static void Reject(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<RoomStatusTransitionException>(action).Code);

    [Fact]
    public void V1_permits_only_the_approved_dirty_start_policy()
    {
        RoomOperationalStatusTransitionParticipant.RequireContext(Context);
        RoomOperationalStatusTransitionParticipant.RequireTransition("START_CLEANING", "DIRTY");
    }

    [Theory]
    [InlineData("AVAILABLE")]
    [InlineData("OCCUPIED")]
    [InlineData("CLEANING")]
    [InlineData("INSPECTION")]
    [InlineData("READY")]
    [InlineData("OUT_OF_SERVICE")]
    [InlineData("dirty")]
    [InlineData("")]
    public void Wrong_from_state_including_repeated_start_is_rejected(string state) =>
        Reject("ROOM_STATUS_FROM_STATE_CONFLICT", () => RoomOperationalStatusTransitionParticipant.RequireTransition("START_CLEANING", state));

    [Theory]
    [InlineData("RESUME_CLEANING")]
    [InlineData("COMPLETE")]
    [InlineData("READY")]
    [InlineData("REWORK")]
    [InlineData("MANUAL_CORRECTION")]
    [InlineData("start_cleaning")]
    public void Unsupported_events_cannot_request_other_transitions(string sourceEvent) =>
        Reject("ROOM_STATUS_EVENT_UNSUPPORTED", () => RoomOperationalStatusTransitionParticipant.RequireContext(Context with { Event = sourceEvent }));

    [Theory]
    [InlineData("actor")]
    [InlineData("branch")]
    [InlineData("room")]
    [InlineData("task")]
    [InlineData("assignment")]
    [InlineData("revision")]
    [InlineData("reason")]
    public void Invalid_source_context_is_rejected(string field)
    {
        var value = field switch
        {
            "actor" => Context with { ActorUserId = 0 },
            "branch" => Context with { BranchId = -1 },
            "room" => Context with { RoomId = 0 },
            "task" => Context with { CleaningTaskId = -1 },
            "assignment" => Context with { AssignmentId = 0 },
            "revision" => Context with { ExpectedSourceRevision = "W/\"0000000000000001\"" },
            _ => Context with { Reason = new string('x', 501) }
        };
        Reject("ROOM_STATUS_SOURCE_CONTEXT_INVALID", () => RoomOperationalStatusTransitionParticipant.RequireContext(value));
    }

    [Fact]
    public async Task Missing_caller_transaction_fails_without_opening_connection_or_tracking_writes()
    {
        await using var db = Database();
        var connection = db.Database.GetDbConnection();
        using var supplied = new FakeTransaction(connection);
        var error = await Assert.ThrowsAsync<RoomStatusTransitionException>(() =>
            new RoomOperationalStatusTransitionParticipant(db).ApplyFreshStartCleaningAsync(connection, supplied, Context, default));
        Assert.Equal("ROOM_STATUS_CALLER_TRANSACTION_REQUIRED", error.Code);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(0, supplied.Commits);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("transaction")]
    [InlineData("transactionConnection")]
    public void Physical_identity_mismatches_are_rejected(string mismatch)
    {
        using var connection = new SqlConnection();
        using var other = new SqlConnection();
        using var current = new FakeTransaction(connection);
        using var different = new FakeTransaction(mismatch == "transactionConnection" ? other : connection);
        Reject("ROOM_STATUS_TRANSACTION_IDENTITY_MISMATCH", () =>
            RoomOperationalStatusTransitionParticipant.RequireTransactionIdentity(true, connection,
                mismatch == "transactionConnection" ? different : current,
                mismatch == "connection" ? other : connection, mismatch == "connection" ? current : different));
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Snapshot)]
    [InlineData(IsolationLevel.Unspecified)]
    public void Non_serializable_transaction_is_rejected(IsolationLevel isolation)
    {
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection, isolation);
        Reject("ROOM_STATUS_SERIALIZABLE_REQUIRED", () =>
            RoomOperationalStatusTransitionParticipant.RequireTransactionIdentity(true, connection, transaction, connection, transaction));
    }

    [Fact]
    public void Non_sql_server_provider_is_rejected()
    {
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        Reject("ROOM_STATUS_SQL_SERVER_REQUIRED", () =>
            RoomOperationalStatusTransitionParticipant.RequireTransactionIdentity(false, connection, transaction, connection, transaction));
    }

    [Theory]
    [InlineData(RoomStartBlockEvidence.Unknown, "ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED")]
    [InlineData(RoomStartBlockEvidence.Effective, "ROOM_STATUS_EFFECTIVE_BLOCK")]
    [InlineData((RoomStartBlockEvidence)99, "ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED")]
    public void Blocked_unknown_and_unrecognized_block_evidence_fail_closed(RoomStartBlockEvidence block, string code)
    {
        using var db = Database();
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        Reject(code, () => RoomOperationalStatusTransitionParticipant.RequireEvidence(db, connection, transaction,
            Context, Source(db, connection, transaction) with { RoomBlock = block }));
    }

    [Fact]
    public void Ids_and_etag_cannot_replace_transaction_bound_start_proof()
    {
        using var db = Database();
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        Reject("ROOM_STATUS_START_PROOF_REQUIRED", () =>
            RoomOperationalStatusTransitionParticipant.RequireEvidence(db, connection, transaction, Context, null));
        Assert.DoesNotContain(typeof(FreshStartCleaningTransitionContext).GetProperties(), p => p.PropertyType == typeof(bool)
            || p.PropertyType == typeof(RoomStartBlockEvidence));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("branch")]
    [InlineData("room")]
    [InlineData("task")]
    [InlineData("assignment")]
    [InlineData("cycle")]
    [InlineData("operation")]
    [InlineData("writer")]
    [InlineData("time")]
    public void Wrong_authoritative_source_identity_is_rejected(string field)
    {
        using var db = Database();
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        var source = Source(db, connection, transaction);
        source = field switch
        {
            "actor" => source with { ActorUserId = 999 },
            "branch" => source with { BranchId = 999 },
            "room" => source with { RoomId = 999 },
            "task" => source with { CleaningTaskId = 999 },
            "assignment" => source with { AssignmentId = 999 },
            "cycle" => source with { CurrentCycleTaskId = 999 },
            "operation" => source with { SourceOperationId = Guid.Empty },
            "writer" => source with { WriterTransactionId = 0 },
            _ => source with { OccurredAt = DateTime.SpecifyKind(source.OccurredAt, DateTimeKind.Unspecified) }
        };
        Reject("ROOM_STATUS_SOURCE_IDENTITY_INVALID", () =>
            RoomOperationalStatusTransitionParticipant.RequireEvidence(db, connection, transaction, Context, source));
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("from")]
    [InlineData("to")]
    [InlineData("unchanged")]
    public void Stale_conflicting_and_non_fresh_start_proofs_are_rejected(string field)
    {
        using var db = Database();
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        var source = Source(db, connection, transaction);
        source = field switch
        {
            "before" => source with { BeforeRevision = "\"0000000000000009\"" },
            "after" => source with { AfterRevision = "invalid" },
            "from" => source with { FromTaskStatus = "ON_HOLD" },
            "to" => source with { ToTaskStatus = "COMPLETED" },
            _ => source with { AfterRevision = source.BeforeRevision }
        };
        Reject("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING", () =>
            RoomOperationalStatusTransitionParticipant.RequireEvidence(db, connection, transaction, Context, source));
    }

    [Theory]
    [InlineData("db")]
    [InlineData("connection")]
    [InlineData("transaction")]
    public void Evidence_from_another_scope_or_transaction_is_rejected(string mismatch)
    {
        using var db = Database();
        using var otherDb = Database();
        using var connection = new SqlConnection();
        using var otherConnection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        using var otherTransaction = new FakeTransaction(connection);
        var source = Source(db, connection, transaction);
        source = mismatch switch
        {
            "db" => source with { Db = otherDb },
            "connection" => source with { Connection = otherConnection },
            _ => source with { Transaction = otherTransaction }
        };
        Reject("ROOM_STATUS_SOURCE_TRANSACTION_MISMATCH", () =>
            RoomOperationalStatusTransitionParticipant.RequireEvidence(db, connection, transaction, Context, source));
    }

    [Fact]
    public void History_and_distinct_room_audit_preserve_actor_source_reason_time_and_exact_transition()
    {
        using var db = Database();
        using var connection = new SqlConnection();
        using var transaction = new FakeTransaction(connection);
        var source = Source(db, connection, transaction);
        var (history, audit) = RoomOperationalStatusTransitionParticipant.CreateRecords(Context, source);
        Assert.Equal((33L, "DIRTY", "CLEANING", "CLEANING_TASK", (long?)44, (long?)11, source.OccurredAt, Context.Reason),
            (history.RoomId, history.FromStatus, history.ToStatus, history.TriggerType, history.TriggerId, history.ChangedBy, history.ChangedAt, history.Reason));
        Assert.Equal(("rooms", "33", "UPDATE", (long?)22, (long?)11, source.OccurredAt),
            (audit.EntityType, audit.EntityId, audit.Action, audit.BranchId, audit.ActorUserId, audit.OccurredAt));
        Assert.Equal(source.SourceOperationId.ToString("D"), audit.RequestId);
        using var oldValues = JsonDocument.Parse(audit.OldValues!);
        using var newValues = JsonDocument.Parse(audit.NewValues!);
        Assert.Equal("DIRTY", oldValues.RootElement.GetProperty("operationalStatus").GetString());
        Assert.Equal("CLEANING", newValues.RootElement.GetProperty("operationalStatus").GetString());
        Assert.Equal(55, newValues.RootElement.GetProperty("assignmentId").GetInt64());
        Assert.Equal(source.BeforeRevision, newValues.RootElement.GetProperty("beforeRevision").GetString());
        Assert.Equal(source.AfterRevision, newValues.RootElement.GetProperty("afterRevision").GetString());
        Assert.Equal(Context.Reason, newValues.RootElement.GetProperty("reason").GetString());
        Assert.Equal(new[] { "operationalStatus" }, JsonSerializer.Deserialize<string[]>(audit.ChangedFields!));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(0, transaction.Commits);
    }

    [Fact]
    public void Contract_exposes_one_source_operation_and_no_manual_or_readiness_writer()
    {
        var operation = Assert.Single(typeof(IRoomOperationalStatusTransitionParticipant).GetMethods());
        Assert.Equal("ApplyFreshStartCleaningAsync", operation.Name);
        Assert.Equal(typeof(Task<RoomStatusTransitionResult>), operation.ReturnType);
        Assert.Equal(new[] { typeof(DbConnection), typeof(DbTransaction), typeof(FreshStartCleaningTransitionContext), typeof(CancellationToken) },
            operation.GetParameters().Select(x => x.ParameterType));
        Assert.False(typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(typeof(RoomOperationalStatusTransitionParticipant)));
        Assert.DoesNotContain(typeof(RoomOperationalStatusTransitionParticipant).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => m.Name.Contains("Commit") || m.Name.Contains("Readiness") || m.Name.Contains("Ready") || m.Name.Contains("Manual"));
    }

    [Fact]
    public async Task Cancellation_does_not_open_a_transaction_or_write()
    {
        await using var db = Database();
        var connection = db.Database.GetDbConnection();
        using var transaction = new FakeTransaction(connection);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RoomOperationalStatusTransitionParticipant(db)
            .ApplyFreshStartCleaningAsync(connection, transaction, Context, new CancellationToken(true)));
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(0, transaction.Commits);
    }

    [Fact(Skip = "RUNTIME_AUTHORIZATION_REQUIRED: accepted FE04 START proof, FE06 D05 and FE12 lock adapter plus isolated SQL lease required. Verify conditional update + one history + one room audit, no readiness write, no independent commit, repeated/concurrent START rejection and rollback on either insert failure. Offline tests do not exercise mutation.")]
    public void Sql_atomicity_concurrency_and_persisted_ownership_acceptance() { }

    private sealed class FakeTransaction(DbConnection connection, IsolationLevel isolation = IsolationLevel.Serializable) : DbTransaction
    {
        public int Commits { get; private set; }
        public override IsolationLevel IsolationLevel => isolation;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { Commits++; throw new InvalidOperationException("Offline transaction must never commit."); }
        public override void Rollback() => throw new InvalidOperationException("Offline transaction must never roll back.");
    }
}
