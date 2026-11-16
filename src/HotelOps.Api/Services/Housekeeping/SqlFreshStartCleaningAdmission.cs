using System.Data;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Housekeeping;

// Server-owned, single-command participant. Caller acquires User/Work-Time, branch and
// catalog locks first. Room UPDATE lock precedes task UPDATE lock; block absence is
// protected by SERIALIZABLE range locks. No request can supply a lock/authorization proof.
internal sealed class SqlFreshStartCleaningAdmission(HotelOpsDbContext ownerDb,
    SessionReference ownerTicket, long ownerSession, Func<CancellationToken, Task<bool>> finalAuthority)
    : IFreshStartD03FinalAuthorization, IFreshStartD04GlobalLockOrder, IFreshStartD05RoomBlockStartGate
{
    private DbTransaction? heldTransaction;
    private long heldRoom, heldTask, heldBranch;

    internal async Task<bool> AcquireAsync(long branch, long task, CancellationToken ct)
    {
        var tx = ownerDb.Database.CurrentTransaction?.GetDbTransaction();
        RequireTransaction(ownerDb, ownerDb.Database.GetDbConnection(), tx);
        if (heldTransaction != null || branch <= 0 || task <= 0 || ownerTicket.UserId <= 0)
            throw Failure("ROOM_STATUS_SOURCE_IDENTITY_INVALID");
        await RequireTimeLockAsync(ct);
        // Discovery only: never authorizes or mutates from this tentative value. After the
        // room lock, re-read the task under UPDATE/HOLDLOCK and bind the exact room again.
        var room = await ownerDb.Database.SqlQuery<long>($"SELECT room_id AS [Value] FROM dbo.cleaning_tasks WITH(READUNCOMMITTED) WHERE cleaning_task_id={task}")
            .SingleOrDefaultAsync(ct);
        if (room <= 0) return false;
        if (!await ownerDb.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}")
            .AsNoTracking().AnyAsync(r => r.BranchId == branch && r.IsActive, ct)) return false;
        if (!await ownerDb.CleaningTasks.FromSqlInterpolated($"SELECT * FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={task}")
            .AsNoTracking().AnyAsync(t => t.RoomId == room, ct)) return false;
        heldTransaction = tx; heldRoom = room; heldTask = task; heldBranch = branch;
        return true;
    }

    public async Task VerifyHeldAsync(HotelOpsDbContext db, DbConnection connection, DbTransaction transaction,
        long actor, long room, long task, CancellationToken ct)
    {
        RequireTransaction(db, connection, transaction);
        if (!ReferenceEquals(heldTransaction, transaction) || actor != ownerTicket.UserId
            || room != heldRoom || task != heldTask || heldRoom <= 0)
            throw Failure("ROOM_STATUS_GLOBAL_LOCK_ORDER_REQUIRED");
        await RequireTimeLockAsync(ct);
    }

    public async Task VerifyAsync(HotelOpsDbContext db, DbConnection connection, DbTransaction transaction,
        SessionReference ticket, long sessionId, long actor, long branch, long room, long task,
        long assignment, CancellationToken ct)
    {
        await VerifyHeldAsync(db, connection, transaction, actor, room, task, ct);
        if (ticket != ownerTicket || sessionId != ownerSession || ticket.IsRestricted || branch != heldBranch
            || !await finalAuthority(ct)) throw Failure("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED");
        var now = await NowAsync(ct);
        var owners = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == task
            && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null).Take(2).ToArrayAsync(ct);
        if (owners.Length != 1 || owners[0].AssignmentId != assignment || owners[0].AssigneeUserId != actor
            || owners[0].AssignedAt > now) throw Failure("ROOM_STATUS_ASSIGNMENT_INVALID");
        if (await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}")
            .SingleOrDefaultAsync(ct) != task) throw Failure("ROOM_STATUS_SOURCE_STALE_OR_CONFLICTING");
    }

    public async Task<RoomStartBlockEvidence> VerifyAsync(HotelOpsDbContext db, DbConnection connection,
        DbTransaction transaction, long branch, long room, DateTime atUtc, CancellationToken ct)
    {
        await VerifyHeldAsync(db, connection, transaction, ownerTicket.UserId, room, heldTask, ct);
        if (branch != heldBranch || atUtc.Kind != DateTimeKind.Utc || atUtc > await NowAsync(ct))
            throw Failure("ROOM_STATUS_BLOCK_AUTHORITY_REQUIRED");
        // Lock the whole room range, including scheduled/future blocks and an empty range.
        var blocks = await db.RoomBlocks.FromSqlInterpolated($"SELECT * FROM dbo.room_blocks WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}")
            .AsNoTracking().ToArrayAsync(ct);
        return blocks.Any(b => b.BlockedAt <= atUtc && (b.ReleasedAt == null || b.ReleasedAt > atUtc))
            ? RoomStartBlockEvidence.Effective : RoomStartBlockEvidence.Clear;
    }

    private void RequireTransaction(HotelOpsDbContext db, DbConnection connection, DbTransaction? transaction)
    {
        RoomOperationalStatusTransitionParticipant.RequireTransactionIdentity(db.Database.IsSqlServer(),
            ownerDb.Database.GetDbConnection(), ownerDb.Database.CurrentTransaction?.GetDbTransaction(), connection, transaction);
        if (!ReferenceEquals(db, ownerDb) || transaction?.IsolationLevel != IsolationLevel.Serializable)
            throw Failure("ROOM_STATUS_CALLER_TRANSACTION_REQUIRED");
    }

    private async Task RequireTimeLockAsync(CancellationToken ct)
    {
        var resource = "HotelOps:WorkTime:User:" + ownerTicket.UserId;
        if (await ownerDb.Database.SqlQuery<string>($"SELECT APPLOCK_MODE('public',{resource},'Transaction') AS [Value]")
            .SingleAsync(ct) != "Exclusive") throw Failure("ROOM_STATUS_GLOBAL_LOCK_ORDER_REQUIRED");
    }

    private async Task<DateTime> NowAsync(CancellationToken ct) => DateTime.SpecifyKind(
        await ownerDb.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct), DateTimeKind.Utc);
    private static RoomStatusTransitionException Failure(string code) => new(code);
}
