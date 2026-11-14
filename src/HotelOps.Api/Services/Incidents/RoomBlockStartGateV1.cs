using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace HotelOps.Api.Services.Incidents;

// DEV2 FE06 FASTTRACK-112D. Source-only; no registration, writer or runtime activation.
public enum EffectiveRoomBlockState { Clear, Effective, Unknown }

public sealed record RoomBlockIntervalV1(DateTime BlockedAtUtc, DateTime? ReleasedAtUtc);

public sealed record RoomBlockStartDecisionV1(
    EffectiveRoomBlockState State,
    DateTime AsOfUtc,
    DateTime? NextScheduledAtUtc,
    string EvidenceCode)
{
    public bool AllowsFreshStart => State == EffectiveRoomBlockState.Clear;
}

public sealed class RoomBlockStartDeniedException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// Pure UTC rules for verified SQL room_blocks source rows. A release is an actual
/// recorded release, not expected_release_at. No source read may substitute a
/// historical readiness flag for this evaluation.
/// </summary>
public static class RoomBlockTemporalRulesV1
{
    public static RoomBlockStartDecisionV1 Evaluate(
        IReadOnlyCollection<RoomBlockIntervalV1>? intervals, DateTime asOfUtc)
    {
        if (intervals is null || asOfUtc.Kind != DateTimeKind.Utc || asOfUtc == DateTime.MinValue)
            return new(EffectiveRoomBlockState.Unknown, asOfUtc, null, "ROOM_BLOCK_TIME_UNVERIFIED");

        var effective = false;
        DateTime? nextScheduled = null;
        foreach (var interval in intervals)
        {
            if (interval is null || interval.BlockedAtUtc.Kind != DateTimeKind.Utc ||
                interval.ReleasedAtUtc is { Kind: not DateTimeKind.Utc } ||
                interval.BlockedAtUtc == DateTime.MinValue ||
                (interval.ReleasedAtUtc is { } released &&
                 (released < interval.BlockedAtUtc || released > asOfUtc)))
                return new(EffectiveRoomBlockState.Unknown, asOfUtc, null, "ROOM_BLOCK_INTERVAL_INVALID");

            if (interval.BlockedAtUtc > asOfUtc)
            {
                if (!nextScheduled.HasValue || interval.BlockedAtUtc < nextScheduled.Value)
                    nextScheduled = interval.BlockedAtUtc;
                continue;
            }
            if (interval.ReleasedAtUtc is null || interval.ReleasedAtUtc > asOfUtc)
                effective = true;
        }
        return new(effective ? EffectiveRoomBlockState.Effective : EffectiveRoomBlockState.Clear,
            asOfUtc, nextScheduled, effective ? "ROOM_BLOCK_EFFECTIVE" : "ROOM_BLOCK_CLEAR");
    }
}

/// <summary>
/// Called by FE04 only after existing User/Work-Time/branch/catalog locks.
/// Both required calls use its exact physical Serializable transaction.
/// The final recheck must be in FE04 beforeCommit, after business writes.
/// Use one gate instance for one START operation, never a singleton or across rooms.
/// Any failure requires caller rollback/disposal; catching a denial does not authorize commit.
/// </summary>
public interface IRoomBlockStartGateV1
{
    Task<RoomBlockStartDecisionV1> RequireClearForFreshStartAsync(
        DbConnection callerConnection, DbTransaction callerTransaction,
        long branchId, long roomId, CancellationToken ct);

    Task<RoomBlockStartDecisionV1> RevalidateBeforeCommitAsync(
        DbConnection callerConnection, DbTransaction callerTransaction,
        long branchId, long roomId, CancellationToken ct);
}

/// <summary>
/// No new transaction, independent commit, EF mutation, DI registration or HTTP route.
/// MVP uses an intentionally conservative full-table X lock, since the existing
/// UX_room_blocks_active is filtered and cannot prove the full historical range.
/// All Block/Release/Reblock writers must ultimately follow the approved shared
/// parent-room -> room-block writer lock protocol.
/// INTEGRATION BLOCKED: FE03/FE04 owners must approve source/cycle/history/index lock
/// ordering and a writer protocol before wiring this candidate into runtime.
/// Parent X removes the reported same-room base-row S/X cycle, not all deadlocks.
/// A reader may still hold a secondary status-index S key while waiting for the
/// clustered row; FE03's later status UPDATE needs that secondary key. FE04's
/// earlier task/cycle locks also precede this room lock, while Directory reads
/// room then source facts. Neither ordering is approved by this candidate.
/// In particular, do not acquire another room after taking the global block lock.
/// SQL concurrency acceptance and a scheduled-activation commit contract are pending.
/// </summary>
public sealed class SqlRoomBlockStartGateV1 : IRoomBlockStartGateV1
{
    private DbConnection? checkedConnection;
    private DbTransaction? checkedTransaction;
    private long checkedBranchId;
    private long checkedRoomId;
    private int phase; // 0 = unused, 1 = initial CLEAR, 2 = revalidated, -1 = failed.
    private int busy;

    public Task<RoomBlockStartDecisionV1> RequireClearForFreshStartAsync(
        DbConnection connection, DbTransaction tx, long branchId, long roomId, CancellationToken ct)
        => CheckAsync(connection, tx, branchId, roomId, false, ct);

    public Task<RoomBlockStartDecisionV1> RevalidateBeforeCommitAsync(
        DbConnection connection, DbTransaction tx, long branchId, long roomId, CancellationToken ct)
        => CheckAsync(connection, tx, branchId, roomId, true, ct);

    private async Task<RoomBlockStartDecisionV1> CheckAsync(
        DbConnection connection, DbTransaction tx, long branchId, long roomId, bool final, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            throw new RoomBlockStartDeniedException("ROOM_BLOCK_CONCURRENT_GATE_USE_DENIED");
        try
        {
            ct.ThrowIfCancellationRequested();
            if (phase != (final ? 1 : 0))
                throw new RoomBlockStartDeniedException("ROOM_BLOCK_CHECK_SEQUENCE_INVALID");
            if (final && (!ReferenceEquals(checkedConnection, connection) ||
                !ReferenceEquals(checkedTransaction, tx) || checkedBranchId != branchId || checkedRoomId != roomId))
                throw new RoomBlockStartDeniedException("ROOM_BLOCK_REVALIDATION_IDENTITY_MISMATCH");

            var decision = await RequireClearAsync(connection, tx, branchId, roomId, ct);
            ct.ThrowIfCancellationRequested();
            checkedConnection = connection;
            checkedTransaction = tx;
            checkedBranchId = branchId;
            checkedRoomId = roomId;
            phase = final ? 2 : 1;
            return decision;
        }
        catch
        {
            phase = -1;
            throw;
        }
        finally { Volatile.Write(ref busy, 0); }
    }

    private static async Task<RoomBlockStartDecisionV1> RequireClearAsync(
        DbConnection connection, DbTransaction tx, long branchId, long roomId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(tx);
        if (branchId <= 0 || roomId <= 0 || connection is not SqlConnection || tx is not SqlTransaction ||
            connection.State != ConnectionState.Open ||
            !ReferenceEquals(tx.Connection, connection) ||
            tx.IsolationLevel != IsolationLevel.Serializable || System.Transactions.Transaction.Current != null)
            throw new RoomBlockStartDeniedException("ROOM_BLOCK_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED");
        ct.ThrowIfCancellationRequested();
        try
        {
            await VerifyCatalogAsync(connection, tx, ct);

            // FE04 retains User -> FE12 Work-Time -> branch/catalog order.
            // Both this reader and the future FE06 Block/Release/Reblock writer
            // must order room parent X -> room app lock -> full table/range.
            // INDEX(1) targets the verified clustered PK, not a covering index key.
            // A Directory reader holding this room's base S lock must finish before
            // we can hold room_blocks X. Earlier FE04 locks still need owner review.
            await using (var parent = Command(connection, tx, """
                SELECT COUNT_BIG(*) FROM dbo.rooms WITH(XLOCK,HOLDLOCK,INDEX(1))
                WHERE room_id=@roomId AND branch_id=@branchId AND is_active=1
                """, branchId, roomId))
            {
                if (Convert.ToInt64(await parent.ExecuteScalarAsync(ct)) != 1)
                    throw new RoomBlockStartDeniedException("ROOM_BLOCK_ROOM_SCOPE_UNVERIFIED");
            }
            await using (var mutex = Command(connection, tx, """
                DECLARE @rc int;
                EXEC @rc=sys.sp_getapplock @Resource=@lockName,
                     @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
                SELECT @rc;
                """))
            {
                Add(mutex, "@lockName", "HotelOps:RoomBlock:Room:v1:" + roomId);
                if (Convert.ToInt32(await mutex.ExecuteScalarAsync(ct)) < 0)
                    throw new RoomBlockStartDeniedException("ROOM_BLOCK_SCOPE_LOCK_UNAVAILABLE");
            }

            // TABLOCKX is deliberate MVP serialization over *all* RoomBlock
            // history. A filtered index scan cannot establish CLEAR or exclude
            // future timestamps, inconsistencies or concurrent inserts.
            var intervals = new List<RoomBlockIntervalV1>();
            await using (var rows = Command(connection, tx, """
                SELECT block_id,blocked_at,released_at
                FROM dbo.room_blocks WITH(TABLOCKX,HOLDLOCK)
                WHERE room_id=@roomId ORDER BY block_id
                """, roomId: roomId))
            await using (var reader = await rows.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.GetInt64(0) <= 0)
                        throw new RoomBlockStartDeniedException("ROOM_BLOCK_SOURCE_UNVERIFIED");
                    intervals.Add(new(
                        DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                        reader.IsDBNull(2) ? null : DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc)));
                }
            }

            // Repeat after both table resources are locked: do not accept a catalog
            // check made before waiting for locks as proof of the schema now read.
            await VerifyCatalogAsync(connection, tx, ct);

            // One SQL UTC timestamp for this evaluation, only AFTER locks.
            // FE04 must call RevalidateBeforeCommitAsync near its single commit.
            // Even this final sample does not lock time: a scheduled block can become
            // effective before the caller commits. No commit-safety assertion is made.
            await using var utc = Command(connection, tx, "SELECT SYSUTCDATETIME()");
            var instant = DateTime.SpecifyKind((DateTime)(await utc.ExecuteScalarAsync(ct)
                ?? throw new RoomBlockStartDeniedException("ROOM_BLOCK_UTC_UNVERIFIED")), DateTimeKind.Utc);
            var decision = RoomBlockTemporalRulesV1.Evaluate(intervals, instant);
            if (!decision.AllowsFreshStart)
                throw new RoomBlockStartDeniedException(decision.EvidenceCode);
            return decision;
        }
        catch (OperationCanceledException) { throw; }
        catch (RoomBlockStartDeniedException) { throw; }
        catch (DbException error) when (ct.IsCancellationRequested)
        { throw new OperationCanceledException("RoomBlock SQL check cancelled; caller rollback required.", error, ct); }
        catch (DbException) { throw new RoomBlockStartDeniedException("ROOM_BLOCK_SOURCE_UNVERIFIED"); }
    }

    private static async Task VerifyCatalogAsync(DbConnection connection, DbTransaction tx, CancellationToken ct)
    {
        await using var metadata = Command(connection, tx, CatalogSql);
        if (await metadata.ExecuteScalarAsync(ct) is not int verified || verified != 1)
            throw new RoomBlockStartDeniedException("ROOM_BLOCK_CATALOG_UNVERIFIED");
    }

    // Only the columns used by this gate are asserted. No schema repair or fallback.
    // Database VIEW DEFINITION is required so invisible constraints/security policies
    // cannot make a partial catalog look verified. This is a prerequisite, not a grant.
    private const string CatalogSql = """
        WITH expected_columns AS (
            SELECT * FROM (VALUES
              ('room_blocks','block_id','bigint',8,0,0,1),
              ('room_blocks','room_id','bigint',8,0,0,0),
              ('room_blocks','blocked_at','datetime2',8,7,0,0),
              ('room_blocks','released_at','datetime2',8,7,1,0),
              ('rooms','room_id','bigint',8,0,0,1),
              ('rooms','branch_id','bigint',8,0,0,0),
              ('rooms','is_active','bit',1,0,0,0)
            ) v(table_name,column_name,type_name,max_length,scale,is_nullable,is_identity)
        )
        SELECT CASE WHEN HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION')=1
          AND HAS_PERMS_BY_NAME('dbo.rooms','OBJECT','VIEW DEFINITION')=1
          AND HAS_PERMS_BY_NAME('dbo.room_blocks','OBJECT','VIEW DEFINITION')=1
          AND (SELECT COUNT(*) FROM sys.tables
            WHERE object_id IN (OBJECT_ID('dbo.rooms','U'),OBJECT_ID('dbo.room_blocks','U'))
              AND is_ms_shipped=0 AND is_memory_optimized=0 AND temporal_type=0)=2
          AND NOT EXISTS(SELECT 1 FROM expected_columns e
            WHERE NOT EXISTS(SELECT 1 FROM sys.columns c
              WHERE c.object_id=OBJECT_ID('dbo.'+e.table_name,'U') AND c.name=e.column_name
                AND c.system_type_id=TYPE_ID(e.type_name) AND c.user_type_id=c.system_type_id
                AND c.max_length=e.max_length AND c.scale=e.scale AND c.is_nullable=e.is_nullable
                AND c.is_identity=e.is_identity AND c.is_computed=0 AND c.is_hidden=0
                AND c.generated_always_type=0 AND c.encryption_type IS NULL))
          AND (SELECT COUNT(*) FROM sys.key_constraints k
            JOIN sys.indexes i ON i.object_id=k.parent_object_id AND i.index_id=k.unique_index_id
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE k.type='PK' AND i.type=1 AND i.index_id=1 AND i.is_primary_key=1
              AND i.is_unique=1 AND i.is_disabled=0 AND i.is_hypothetical=0
              AND i.has_filter=0 AND i.ignore_dup_key=0
              AND ic.key_ordinal=1 AND ic.is_descending_key=0 AND ic.is_included_column=0
              AND (SELECT COUNT(*) FROM sys.index_columns x
                   WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>0)=1
              AND ((i.object_id=OBJECT_ID('dbo.room_blocks') AND c.name='block_id')
                OR (i.object_id=OBJECT_ID('dbo.rooms') AND c.name='room_id')))=2
          AND EXISTS(SELECT 1 FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID('dbo.room_blocks') AND i.name='UX_room_blocks_active'
              AND i.type=2 AND i.is_unique=1 AND i.is_disabled=0 AND i.is_hypothetical=0
              AND i.has_filter=1 AND i.ignore_dup_key=0 AND c.name='room_id'
              AND ic.key_ordinal=1 AND ic.is_descending_key=0 AND ic.is_included_column=0
              AND (SELECT COUNT(*) FROM sys.index_columns x
                   WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>0)=1
              AND NOT EXISTS(SELECT 1 FROM sys.index_columns x
                   WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.is_included_column=1)
              AND LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                i.filter_definition,'[',''),']',''),' ',''),'(',''),')',''),CHAR(9),''),CHAR(10),''),CHAR(13),''))
                ='released_atisnull')
          AND EXISTS(SELECT 1 FROM sys.foreign_keys f
            JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
            JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id
            JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
            WHERE f.parent_object_id=OBJECT_ID('dbo.room_blocks')
              AND f.referenced_object_id=OBJECT_ID('dbo.rooms') AND f.name='FK_room_blocks_room_id'
              AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.is_not_for_replication=0
              AND f.delete_referential_action=0 AND f.update_referential_action=0 AND f.key_index_id=1
              AND fc.constraint_column_id=1 AND pc.name='room_id' AND rc.name='room_id'
              AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1)
          AND NOT EXISTS(SELECT 1 FROM sys.foreign_keys
            WHERE parent_object_id IN (OBJECT_ID('dbo.room_blocks'),OBJECT_ID('dbo.rooms'))
              AND (is_disabled=1 OR is_not_trusted=1 OR is_not_for_replication=1))
          AND NOT EXISTS(SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id IN (OBJECT_ID('dbo.room_blocks'),OBJECT_ID('dbo.rooms'))
              AND (is_disabled=1 OR is_not_trusted=1 OR is_not_for_replication=1))
          AND NOT EXISTS(SELECT 1 FROM sys.security_predicates p
            JOIN sys.security_policies s ON s.object_id=p.object_id
            WHERE s.is_enabled=1 AND p.target_object_id IN (OBJECT_ID('dbo.room_blocks'),OBJECT_ID('dbo.rooms')))
        THEN 1 ELSE 0 END
        """;

    private static DbCommand Command(DbConnection connection, DbTransaction tx, string sql,
        long? branchId = null, long? roomId = null)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = 15;
        if (branchId.HasValue) Add(cmd, "@branchId", branchId.Value);
        if (roomId.HasValue) Add(cmd, "@roomId", roomId.Value);
        return cmd;
    }

    private static void Add(DbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
