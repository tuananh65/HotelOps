using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Rooms;

public interface IRoomOperationalStatusTransitionParticipant
{
    Task<RoomStatusTransitionResult> ApplyFreshStartCleaningAsync(
        DbConnection connection,
        DbTransaction transaction,
        FreshStartCleaningTransitionContext context,
        CancellationToken cancellationToken);
}

// These are source-operation facts, never HTTP authorization or eligibility assertions.
public sealed record FreshStartCleaningTransitionContext(string Event, long ActorUserId, long BranchId,
    long RoomId, long CleaningTaskId, long AssignmentId, string ExpectedSourceRevision, string? Reason);

// Applied means flushed inside the caller transaction, NOT committed or replayed successfully.
public sealed record RoomStatusTransitionResult(long RoomId, string FromStatus, string ToStatus,
    Guid SourceOperationId, DateTime OccurredAt);

public enum RoomStartBlockEvidence { Unknown, Effective, Clear }

/// <summary>
/// Server-only dependency port. No implementation or registration is supplied in FASTTRACK-107A.
/// The future FE04 adapter must verify fresh ASSIGNED -> IN_PROGRESS in this physical transaction,
/// pre/post source revision, authenticated actor, current assignment/cycle, final permission and
/// user-first/FE12 locks. FE06 must accept the temporal block predicate AND absence/range locking
/// protocol before returning Clear. A client ETag, task ID or ReleasedAt alone cannot implement it.
/// The adapter must retain all accepted dependency locks until the caller commits or rolls back.
/// </summary>
public interface IFreshStartCleaningTransactionEvidence
{
    Task<VerifiedFreshStartCleaning> VerifyAsync(HotelOpsDbContext db, DbConnection connection,
        DbTransaction transaction, FreshStartCleaningTransitionContext context, CancellationToken cancellationToken);
}

public sealed record VerifiedFreshStartCleaning(HotelOpsDbContext Db, DbConnection Connection,
    DbTransaction Transaction, long ActorUserId, long BranchId, long RoomId, long CleaningTaskId,
    long AssignmentId, long CurrentCycleTaskId, string FromTaskStatus, string ToTaskStatus,
    string BeforeRevision, string AfterRevision, Guid SourceOperationId, long WriterTransactionId,
    DateTime OccurredAt, RoomStartBlockEvidence RoomBlock);

public sealed class RoomStatusTransitionException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// Canonical FE03 status/history/audit participant. FE04 alone owns START, readiness and commit.
/// Missing accepted dependency adapter fails closed BEFORE any database query or write.
/// Exceptions must escape to the caller's transaction rollback boundary; never retry an unknown
/// commit outcome. There is no endpoint, default adapter, DI registration or production activation.
/// </summary>
public sealed class RoomOperationalStatusTransitionParticipant(
    HotelOpsDbContext db, IFreshStartCleaningTransactionEvidence? evidence = null)
    : IRoomOperationalStatusTransitionParticipant
{
    public async Task<RoomStatusTransitionResult> ApplyFreshStartCleaningAsync(
        DbConnection connection, DbTransaction transaction, FreshStartCleaningTransitionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireContext(context);
        RequireTransactionIdentity(db.Database.IsSqlServer(), db.Database.GetDbConnection(),
            db.Database.CurrentTransaction?.GetDbTransaction(), connection, transaction);
        if (System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw Failure("ROOM_STATUS_TRANSACTION_POLICY_REQUIRED");
        // Do not accidentally flush or leave stale tracked Room/history/audit entities for FE04.
        if (db.ChangeTracker.Entries().Any(x => x.Entity is Room or RoomStatusHistory or AuditLog))
            throw Failure("ROOM_STATUS_UNTRACKED_CONTEXT_REQUIRED");
        if (evidence == null) throw Failure("ROOM_STATUS_START_BLOCK_LOCK_AUTHORITY_REQUIRED");

        var source = await evidence.VerifyAsync(db, connection, transaction, context, cancellationToken);
        RequireEvidence(db, connection, transaction, context, source);
        // A dependency must not replace the scoped transaction or enable retries while verifying.
        RequireTransactionIdentity(db.Database.IsSqlServer(), db.Database.GetDbConnection(),
            db.Database.CurrentTransaction?.GetDbTransaction(), connection, transaction);

        var writer = await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS [Value]")
            .SingleAsync(cancellationToken);
        if (source.WriterTransactionId != writer) throw Failure("ROOM_STATUS_SOURCE_TRANSACTION_MISMATCH");

        // Entered only after owner-accepted user/Work-Time/source/block lock ordering. No user or
        // FE12 locks are acquired here, and no independent context/transaction is created.
        var room = await db.Rooms.FromSqlInterpolated(
            $"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={context.RoomId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (room == null || room.BranchId != context.BranchId) throw Failure("ROOM_STATUS_ROOM_SCOPE_INVALID");
        RequireTransition(context.Event, room.OperationalStatus);
        if (!room.IsActive
            || !await db.Branches.AsNoTracking().AnyAsync(x => x.BranchId == room.BranchId && x.IsActive, cancellationToken)
            || !await db.Floors.AsNoTracking().AnyAsync(x => x.FloorId == room.FloorId && x.BranchId == room.BranchId && x.IsActive, cancellationToken)
            || room.AreaId is { } area && !await db.Areas.AsNoTracking().AnyAsync(x => x.AreaId == area
                && x.BranchId == room.BranchId && x.IsActive && (x.FloorId == null || x.FloorId == room.FloorId), cancellationToken)
            || !await db.BranchRoomTypes.AsNoTracking().AnyAsync(x => x.RoomTypeId == room.RoomTypeId
                && x.BranchId == room.BranchId && x.IsActive, cancellationToken))
            throw Failure("ROOM_STATUS_ROOM_INELIGIBLE");

        var task = await db.Database.SqlQuery<StartTaskRow>($"""
            SELECT t.room_id AS RoomId,t.workflow_status AS WorkflowStatus,t.status AS Status,
                t.row_version AS Revision,t.actual_start_at AS StartedAt,t.actual_end_at AS EndedAt,
                c.cleaning_task_id AS CurrentCycleTaskId
            FROM dbo.cleaning_tasks t WITH(HOLDLOCK)
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=t.room_id
            WHERE t.cleaning_task_id={context.CleaningTaskId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (task == null || task.RoomId != room.RoomId || task.CurrentCycleTaskId != context.CleaningTaskId
            || task.WorkflowStatus != "IN_PROGRESS" || task.Status != "IN_PROGRESS" || task.EndedAt != null
            || task.StartedAt != source.OccurredAt || Tag(task.Revision) != source.AfterRevision)
            throw Failure("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING");
        var assignments = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == context.CleaningTaskId
            && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null).Take(2).ToArrayAsync(cancellationToken);
        if (assignments.Length != 1 || assignments[0].AssignmentId != context.AssignmentId
            || assignments[0].AssigneeUserId != context.ActorUserId || assignments[0].AssignedAt > source.OccurredAt)
            throw Failure("ROOM_STATUS_ASSIGNMENT_INVALID");
        // A task ID is NOT an immutable event key. V1 conservatively rejects any previous START
        // for this task as well as conflicting source-operation audit evidence; no historical replay.
        if (await db.RoomStatusHistories.AsNoTracking().AnyAsync(x => x.TriggerType == "CLEANING_TASK"
                && x.TriggerId == context.CleaningTaskId && x.FromStatus == "DIRTY" && x.ToStatus == "CLEANING", cancellationToken)
            || await db.AuditLogs.AsNoTracking().AnyAsync(x => x.EntityType == "rooms"
                && x.RequestId == source.SourceOperationId.ToString("D"), cancellationToken))
            throw Failure("ROOM_STATUS_START_ALREADY_RECORDED");

        var records = CreateRecords(context, source);
        // All statements use the caller's physical connection/transaction. XACT_ABORT makes a
        // server error (including row-count conflict or either insert failing) abort the transaction.
        // Cancellation/network failures still require caller rollback/disposal, never a retry.
        // Raw inserts deliberately avoid SaveChanges flushing unrelated FE04 tracked state.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET XACT_ABORT ON;
            UPDATE dbo.rooms SET operational_status='CLEANING'
            WHERE room_id={room.RoomId} AND branch_id={context.BranchId}
              AND is_active=1 AND operational_status='DIRTY'
              AND EXISTS(SELECT 1 FROM dbo.cleaning_tasks t
                JOIN dbo.room_cleaning_cycle_state c ON c.room_id=t.room_id
                WHERE t.cleaning_task_id={context.CleaningTaskId} AND t.room_id={room.RoomId}
                  AND c.cleaning_task_id=t.cleaning_task_id AND t.workflow_status='IN_PROGRESS'
                  AND t.status='IN_PROGRESS' AND t.row_version={task.Revision}
                  AND t.actual_start_at={source.OccurredAt} AND t.actual_end_at IS NULL);
            IF @@ROWCOUNT<>1 THROW 51803,'ROOM_STATUS_CONDITIONAL_UPDATE_FAILED',1;
            INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,trigger_id,changed_by,changed_at,reason)
            VALUES({records.History.RoomId},{records.History.FromStatus},{records.History.ToStatus},
              {records.History.TriggerType},{records.History.TriggerId},{records.History.ChangedBy},
              {records.History.ChangedAt},{records.History.Reason});
            IF @@ROWCOUNT<>1 THROW 51803,'ROOM_STATUS_HISTORY_INSERT_FAILED',1;
            INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,request_id,occurred_at)
            VALUES({records.Audit.BranchId},{records.Audit.ActorUserId},{records.Audit.EntityType},
              {records.Audit.EntityId},{records.Audit.Action},{records.Audit.OldValues},{records.Audit.NewValues},
              {records.Audit.ChangedFields},{records.Audit.RequestId},{records.Audit.OccurredAt});
            IF @@ROWCOUNT<>1 THROW 51803,'ROOM_STATUS_AUDIT_INSERT_FAILED',1;
            """, cancellationToken);
        return new(room.RoomId, "DIRTY", "CLEANING", source.SourceOperationId, source.OccurredAt);
    }

    // Public policy seams follow existing FE04 contract-test conventions. They do not authorize a
    // command, access a database, accept arbitrary statuses or bypass the transaction evidence port.
    public static void RequireContext(FreshStartCleaningTransitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Event != "START_CLEANING") throw Failure("ROOM_STATUS_EVENT_UNSUPPORTED");
        if (context.ActorUserId <= 0 || context.BranchId <= 0 || context.RoomId <= 0
            || context.CleaningTaskId <= 0 || context.AssignmentId <= 0 || !StrongTag(context.ExpectedSourceRevision)
            || context.Reason?.Length > 500 || context.Reason?.Any(char.IsControl) == true)
            throw Failure("ROOM_STATUS_SOURCE_CONTEXT_INVALID");
    }

    public static void RequireTransition(string sourceEvent, string currentStatus)
    {
        if (sourceEvent != "START_CLEANING") throw Failure("ROOM_STATUS_EVENT_UNSUPPORTED");
        if (currentStatus != "DIRTY") throw Failure("ROOM_STATUS_FROM_STATE_CONFLICT");
    }

    public static void RequireTransactionIdentity(bool sqlServer, DbConnection scopedConnection,
        DbTransaction? scopedTransaction, DbConnection? connection, DbTransaction? transaction)
    {
        if (!sqlServer || connection is not SqlConnection) throw Failure("ROOM_STATUS_SQL_SERVER_REQUIRED");
        if (scopedTransaction == null || transaction == null) throw Failure("ROOM_STATUS_CALLER_TRANSACTION_REQUIRED");
        if (!ReferenceEquals(scopedConnection, connection) || !ReferenceEquals(scopedTransaction, transaction)
            || !ReferenceEquals(transaction.Connection, connection)) throw Failure("ROOM_STATUS_TRANSACTION_IDENTITY_MISMATCH");
        if (transaction.IsolationLevel != IsolationLevel.Serializable) throw Failure("ROOM_STATUS_SERIALIZABLE_REQUIRED");
    }

    public static void RequireEvidence(HotelOpsDbContext db, DbConnection connection, DbTransaction transaction,
        FreshStartCleaningTransitionContext context, VerifiedFreshStartCleaning? source)
    {
        RequireContext(context);
        if (source == null) throw Failure("ROOM_STATUS_START_PROOF_REQUIRED");
        if (!ReferenceEquals(source.Db, db) || !ReferenceEquals(source.Connection, connection)
            || !ReferenceEquals(source.Transaction, transaction)) throw Failure("ROOM_STATUS_SOURCE_TRANSACTION_MISMATCH");
        if (source.ActorUserId != context.ActorUserId || source.BranchId != context.BranchId
            || source.RoomId != context.RoomId || source.CleaningTaskId != context.CleaningTaskId
            || source.AssignmentId != context.AssignmentId || source.CurrentCycleTaskId != context.CleaningTaskId
            || source.SourceOperationId == Guid.Empty || source.WriterTransactionId <= 0
            || source.OccurredAt.Kind != DateTimeKind.Utc || source.OccurredAt == DateTime.MinValue)
            throw Failure("ROOM_STATUS_SOURCE_IDENTITY_INVALID");
        if (source.FromTaskStatus != "ASSIGNED" || source.ToTaskStatus != "IN_PROGRESS"
            || source.BeforeRevision != context.ExpectedSourceRevision || !StrongTag(source.AfterRevision)
            || source.BeforeRevision == source.AfterRevision)
            throw Failure("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING");
        if (source.RoomBlock == RoomStartBlockEvidence.Effective) throw Failure("ROOM_STATUS_EFFECTIVE_BLOCK");
        if (source.RoomBlock != RoomStartBlockEvidence.Clear) throw Failure("ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED");
    }

    public static (RoomStatusHistory History, AuditLog Audit) CreateRecords(
        FreshStartCleaningTransitionContext context, VerifiedFreshStartCleaning source)
    {
        RequireEvidence(source.Db, source.Connection, source.Transaction, context, source);
        var history = new RoomStatusHistory
        {
            RoomId = context.RoomId,
            FromStatus = "DIRTY",
            ToStatus = "CLEANING",
            TriggerType = "CLEANING_TASK",
            TriggerId = context.CleaningTaskId,
            ChangedBy = context.ActorUserId,
            ChangedAt = source.OccurredAt,
            Reason = context.Reason
        };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var audit = new AuditLog
        {
            BranchId = context.BranchId,
            ActorUserId = context.ActorUserId,
            EntityType = "rooms",
            EntityId = context.RoomId.ToString(CultureInfo.InvariantCulture),
            Action = "UPDATE",
            OccurredAt = source.OccurredAt,
            RequestId = source.SourceOperationId.ToString("D"),
            OldValues = JsonSerializer.Serialize(new { operationalStatus = "DIRTY" }, json),
            NewValues = JsonSerializer.Serialize(new
            {
                operationalStatus = "CLEANING",
                action = context.Event,
                context.CleaningTaskId,
                context.AssignmentId,
                source.SourceOperationId,
                source.BeforeRevision,
                source.AfterRevision,
                source.WriterTransactionId,
                context.Reason,
                source.OccurredAt
            }, json),
            ChangedFields = JsonSerializer.Serialize(new[] { "operationalStatus" }, json)
        };
        return (history, audit);
    }

    private static bool StrongTag(string? tag) => tag is { Length: 18 } && tag[0] == '"' && tag[^1] == '"'
        && tag.Skip(1).Take(16).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string Tag(byte[] revision) => '"' + Convert.ToHexString(revision) + '"';
    private static RoomStatusTransitionException Failure(string code) => new(code);

    private sealed class StartTaskRow
    {
        public long RoomId { get; set; }
        public string WorkflowStatus { get; set; } = null!;
        public string Status { get; set; } = null!;
        public byte[] Revision { get; set; } = [];
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public long CurrentCycleTaskId { get; set; }
    }
}
