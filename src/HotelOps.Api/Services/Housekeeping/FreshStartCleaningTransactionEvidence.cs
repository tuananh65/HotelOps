using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Housekeeping;

// The SQL admission participant implements these ports inside the caller's transaction.
// These must be trusted server-side transaction participants, never HTTP/request-provided flags.
public interface IFreshStartD03FinalAuthorization
{
    Task VerifyAsync(HotelOpsDbContext db, DbConnection connection, DbTransaction transaction,
        SessionReference ticket, long sessionId, long actor, long branch, long room, long task,
        long assignment, CancellationToken ct);
}

public interface IFreshStartD04GlobalLockOrder
{
    Task VerifyHeldAsync(HotelOpsDbContext db, DbConnection connection, DbTransaction transaction,
        long actor, long room, long task, CancellationToken ct);
}

public interface IFreshStartD05RoomBlockStartGate
{
    // Clear is meaningful only if owner-approved parent room/range/absence locks are held until commit.
    Task<RoomStartBlockEvidence> VerifyAsync(HotelOpsDbContext db, DbConnection connection,
        DbTransaction transaction, long branch, long room, DateTime atUtc, CancellationToken ct);
}

public sealed record FreshStartCleaningSourceReceipt(
    long Actor, long Branch, long Room, long Task, long Assignment, long Cycle,
    string BeforeRevision, string AfterRevision, Guid OperationId, long WriterTransactionId,
    DateTime OccurredAtUtc, DbConnection Connection, DbTransaction Transaction);

/// <summary>
/// FE04 source bridge to the canonical FE03 participant; NEVER authorizes a public START by itself.
/// No fallback to ReleasedAt, synthetic Clear, or a request ETag as transaction proof.
/// Missing transaction participants still fail closed; production HTTP input cannot supply them.
/// </summary>
public sealed class FreshStartCleaningTransactionEvidence : IFreshStartCleaningTransactionEvidence
{
    private readonly SessionReference ticket;
    private readonly long sessionId;
    private readonly IFreshStartD03FinalAuthorization? d03;
    private readonly IFreshStartD04GlobalLockOrder? d04;
    private readonly IFreshStartD05RoomBlockStartGate? d05;
    private FreshStartCleaningSourceReceipt? source;

    public FreshStartCleaningTransactionEvidence(SessionReference ticket, long sessionId,
        IFreshStartD03FinalAuthorization? d03 = null, IFreshStartD04GlobalLockOrder? d04 = null,
        IFreshStartD05RoomBlockStartGate? d05 = null)
    {
        this.ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));
        if (sessionId <= 0 || ticket.UserId <= 0 || ticket.IsRestricted)
            throw new RoomStatusTransitionException("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED");
        this.sessionId = sessionId;
        this.d03 = d03;
        this.d04 = d04;
        this.d05 = d05;
    }

    public static void RequireOwnerContracts(IFreshStartD03FinalAuthorization? finalAuthorization,
        IFreshStartD04GlobalLockOrder? globalLockOrder, IFreshStartD05RoomBlockStartGate? startGate)
    {
        if (finalAuthorization is null) throw new RoomStatusTransitionException("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED");
        if (globalLockOrder is null) throw new RoomStatusTransitionException("ROOM_STATUS_GLOBAL_LOCK_ORDER_REQUIRED");
        if (startGate is null) throw new RoomStatusTransitionException("ROOM_STATUS_START_BLOCK_LOCK_AUTHORITY_REQUIRED");
    }

    public async Task<RoomStartBlockEvidence> RequireAdmissionAsync(HotelOpsDbContext db,
        long actor, long branch, long room, long task, long assignment, CancellationToken ct)
    {
        // Fail before any cleaning mutation while the cross-owner protocols are unavailable.
        RequireOwnerContracts(d03, d04, d05);
        var connection = db.Database.GetDbConnection();
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        RoomOperationalStatusTransitionParticipant.RequireTransactionIdentity(db.Database.IsSqlServer(),
            connection, transaction, connection, transaction);
        if (actor != ticket.UserId || branch <= 0 || room <= 0 || task <= 0 || assignment <= 0)
            throw new RoomStatusTransitionException("ROOM_STATUS_SOURCE_IDENTITY_INVALID");
        await d04!.VerifyHeldAsync(db, connection, transaction!, actor, room, task, ct);
        await d03!.VerifyAsync(db, connection, transaction!, ticket, sessionId, actor, branch, room, task, assignment, ct);
        var at = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>(
            $"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct), DateTimeKind.Utc);
        var block = await d05!.VerifyAsync(db, connection, transaction!, branch, room, at, ct);
        RequireClear(block);
        return block;
    }

    public static void RequireClear(RoomStartBlockEvidence evidence)
    {
        if (evidence == RoomStartBlockEvidence.Effective)
            throw new RoomStatusTransitionException("ROOM_STATUS_EFFECTIVE_BLOCK");
        if (evidence != RoomStartBlockEvidence.Clear)
            throw new RoomStatusTransitionException("ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED");
    }

    public void BindSource(FreshStartCleaningSourceReceipt receipt)
    {
        if (source != null) throw new RoomStatusTransitionException("ROOM_STATUS_START_ALREADY_RECORDED");
        RequireSourceReceipt(receipt);
        source = receipt;
    }

    public async Task<VerifiedFreshStartCleaning> VerifyAsync(HotelOpsDbContext db, DbConnection connection,
        DbTransaction transaction, FreshStartCleaningTransitionContext context, CancellationToken ct)
    {
        RequireOwnerContracts(d03, d04, d05);
        var receipt = source ?? throw new RoomStatusTransitionException("ROOM_STATUS_START_PROOF_REQUIRED");
        RoomOperationalStatusTransitionParticipant.RequireTransactionIdentity(db.Database.IsSqlServer(),
            db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction(), connection, transaction);
        if (!ReferenceEquals(receipt.Connection, connection) || !ReferenceEquals(receipt.Transaction, transaction)
            || receipt.Actor != ticket.UserId || receipt.Actor != context.ActorUserId
            || receipt.Branch != context.BranchId || receipt.Room != context.RoomId
            || receipt.Task != context.CleaningTaskId || receipt.Assignment != context.AssignmentId
            || receipt.Cycle != context.CleaningTaskId || receipt.BeforeRevision != context.ExpectedSourceRevision)
            throw new RoomStatusTransitionException("ROOM_STATUS_SOURCE_TRANSACTION_MISMATCH");
        var actualWriter = await db.Database.SqlQuery<long>(
            $"SELECT CURRENT_TRANSACTION_ID() AS [Value]").SingleAsync(ct);
        if (actualWriter != receipt.WriterTransactionId)
            throw new RoomStatusTransitionException("ROOM_STATUS_SOURCE_TRANSACTION_MISMATCH");
        // Final owner revalidation is transaction-bound and happens after FE04's task UPDATE,
        // before FE03's room UPDATE; the owner's locks must survive until the FE04 commit.
        await d04!.VerifyHeldAsync(db, connection, transaction, receipt.Actor, receipt.Room, receipt.Task, ct);
        await d03!.VerifyAsync(db, connection, transaction, ticket, sessionId, receipt.Actor,
            receipt.Branch, receipt.Room, receipt.Task, receipt.Assignment, ct);
        var block = await d05!.VerifyAsync(db, connection, transaction, receipt.Branch,
            receipt.Room, receipt.OccurredAtUtc, ct);
        RequireClear(block);
        return new(db, connection, transaction, receipt.Actor, receipt.Branch, receipt.Room,
            receipt.Task, receipt.Assignment, receipt.Cycle, "ASSIGNED", "IN_PROGRESS",
            receipt.BeforeRevision, receipt.AfterRevision, receipt.OperationId,
            receipt.WriterTransactionId, receipt.OccurredAtUtc, block);
    }

    public static void RequireSourceReceipt(FreshStartCleaningSourceReceipt? receipt)
    {
        if (receipt is null || receipt.Actor <= 0 || receipt.Branch <= 0 || receipt.Room <= 0
            || receipt.Task <= 0 || receipt.Assignment <= 0 || receipt.Cycle != receipt.Task
            || receipt.OperationId == Guid.Empty || receipt.WriterTransactionId <= 0
            || receipt.OccurredAtUtc.Kind != DateTimeKind.Utc
            || receipt.BeforeRevision == receipt.AfterRevision
            || !ValidRevision(receipt.BeforeRevision) || !ValidRevision(receipt.AfterRevision)
            || receipt.Connection is null || receipt.Transaction is null
            || !ReferenceEquals(receipt.Transaction.Connection, receipt.Connection))
            throw new RoomStatusTransitionException("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING");
    }

    private static bool ValidRevision(string? value) => value is { Length: 18 }
        && value[0] == '"' && value[^1] == '"'
        && value[1..^1].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
