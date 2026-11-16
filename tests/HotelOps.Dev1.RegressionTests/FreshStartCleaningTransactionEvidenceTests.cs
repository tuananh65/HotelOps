using System.Data;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Rooms;
using Microsoft.Data.SqlClient;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// These tests verify offline fail-closed source policies. They NEVER provide authoritative
// D03/D04/D05 approvals, execute SQL, or establish a public fresh START success path.
public sealed class FreshStartCleaningTransactionEvidenceTests
{
    private static void Reject(string expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<RoomStatusTransitionException>(action).Code);

    [Fact]
    public void Missing_final_D03_authority_fails_closed_before_mutation() =>
        Reject("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED", () =>
            FreshStartCleaningTransactionEvidence.RequireOwnerContracts(null, null, null));

    [Fact]
    public void D04_global_order_is_required_even_if_D03_port_exists() =>
        Reject("ROOM_STATUS_GLOBAL_LOCK_ORDER_REQUIRED", () =>
            FreshStartCleaningTransactionEvidence.RequireOwnerContracts(new D03(), null, null));

    [Fact]
    public void D05_authoritative_startgate_is_required_even_if_D03_D04_ports_exist() =>
        Reject("ROOM_STATUS_START_BLOCK_LOCK_AUTHORITY_REQUIRED", () =>
            FreshStartCleaningTransactionEvidence.RequireOwnerContracts(new D03(), new D04(), null));

    [Theory]
    [InlineData(RoomStartBlockEvidence.Unknown, "ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED")]
    [InlineData((RoomStartBlockEvidence)99, "ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED")]
    public void Unknown_or_unrecognized_gate_does_not_authorize_start(
        RoomStartBlockEvidence evidence, string error) =>
        Reject(error, () => FreshStartCleaningTransactionEvidence.RequireKnownBlock(evidence));

    [Fact]
    public void Clear_is_only_a_policy_value_not_an_owner_acceptance()
    {
        FreshStartCleaningTransactionEvidence.RequireKnownBlock(RoomStartBlockEvidence.Clear);
        FreshStartCleaningTransactionEvidence.RequireKnownBlock(RoomStartBlockEvidence.Effective);
        Reject("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED", () =>
            FreshStartCleaningTransactionEvidence.RequireOwnerContracts(null, null, null));
    }

    [Fact]
    public void Missing_source_receipt_rejects_start() =>
        Reject("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING", () =>
            FreshStartCleaningTransactionEvidence.RequireSourceReceipt(null));

    [Theory]
    [InlineData("operation")]
    [InlineData("writer")]
    [InlineData("revision-before")]
    [InlineData("revision-after")]
    [InlineData("no-transition")]
    [InlineData("actor")]
    [InlineData("assignment")]
    [InlineData("cycle")]
    [InlineData("time")]
    public void Fabricated_or_stale_source_facts_fail_closed(string field)
    {
        using var connection = new SqlConnection();
        using var tx = new FakeTransaction(connection);
        var source = Capture(connection, tx);
        source = field switch
        {
            "operation" => source with { OperationId = Guid.Empty },
            "writer" => source with { WriterTransactionId = 0 },
            "revision-before" => source with { BeforeRevision = "invalid" },
            "revision-after" => source with { AfterRevision = "invalid" },
            "no-transition" => source with { AfterRevision = source.BeforeRevision },
            "actor" => source with { Actor = 0 },
            "assignment" => source with { Assignment = 0 },
            "cycle" => source with { Cycle = 999 },
            _ => source with { OccurredAtUtc = DateTime.SpecifyKind(source.OccurredAtUtc, DateTimeKind.Unspecified) }
        };
        Reject("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING", () =>
            FreshStartCleaningTransactionEvidence.RequireSourceReceipt(source));
    }

    [Fact]
    public void Physical_connection_mismatch_rejects_receipt()
    {
        using var connection = new SqlConnection();
        using var different = new SqlConnection();
        using var tx = new FakeTransaction(connection);
        Reject("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING", () =>
            FreshStartCleaningTransactionEvidence.RequireSourceReceipt(Capture(different, tx)));
    }

    [Fact]
    public void Well_formed_receipt_is_only_a_shape_check_not_authorization()
    {
        using var connection = new SqlConnection();
        using var tx = new FakeTransaction(connection);
        FreshStartCleaningTransactionEvidence.RequireSourceReceipt(Capture(connection, tx));
        // No owner contract is implemented or DI-registered by this source increment.
        Reject("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED", () =>
            FreshStartCleaningTransactionEvidence.RequireOwnerContracts(null, null, null));
    }

    private static FreshStartCleaningSourceReceipt Capture(DbConnection connection, DbTransaction tx) =>
        new(11, 22, 33, 44, 55, 44, "\"0000000000000001\"", "\"0000000000000002\"",
            Guid.Parse("f238c811-4e8f-4b1a-95f0-50b74a403bee"), 123,
            new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), connection, tx);

    // Deliberately only test ports, not production authorities. No returned 'Clear' or approval.
    private sealed class D03 : IFreshStartD03FinalAuthorization
    {
        public Task VerifyAsync(HotelOpsDbContext db, DbConnection connection, DbTransaction tx,
            SessionReference ticket, long sessionId, long actor, long branch, long room, long task,
            long assignment, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class D04 : IFreshStartD04GlobalLockOrder
    {
        public Task VerifyHeldAsync(HotelOpsDbContext db, DbConnection connection, DbTransaction tx,
            long actor, long room, long task, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class FakeTransaction(DbConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;
        protected override DbConnection DbConnection => connection;
        public override void Commit() => throw new NotSupportedException();
        public override void Rollback() => throw new NotSupportedException();
    }
}
