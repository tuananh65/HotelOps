using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

[Collection("Note2511 SQL")]
public sealed class MissingItemsDatabaseTests
{
    private static readonly Type Custody = typeof(WorkService).Assembly.GetType("HotelOps.Api.Compact.Services.ReusableMissingCustody")!;
    private static readonly Type Accounting = typeof(WorkService).Assembly.GetType("HotelOps.Api.Compact.Services.ReusableAccounting")!;
    private sealed class FixtureClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 27, 8, 25, 0, TimeSpan.Zero);
    }

    [TaskSupplyDatabaseFact]
    public async Task PendingReportProtectsDirtyAndConfirmedCorrectionLeavesCleanAndOtherTaskUntouched()
    {
        await WithFixture(async (db, room, user, item, first, second, used, dirty, clean, accounting, ct) =>
        {
            await Move(accounting, used, dirty, 2, user, "REPLACEMENT", first.WorkId, ct);
            await Move(accounting, used, dirty, 2, user, "REPLACEMENT", second.WorkId, ct);
            var sources = await Available(db, room, first.WorkId, item, ct);
            Assert.Equal(2, Assert.Single(sources).Quantity);
            var cleanBefore = clean.Quantity;
            var stockBefore = await db.StockMovements.CountAsync(ct);
            var incident = NewWork(room, user, "INCIDENT");
            db.WorkItems.Add(incident);
            await db.SaveChangesAsync(ct);
            db.WorkEvents.Add(new WorkEvent {
                BranchId = room.BranchId, WorkId = incident.WorkId, ActorUserId = user,
                EventType = "ROOM_ISSUE_REPORTED", OperationKey = Guid.NewGuid(), OccurredAt = new FixtureClock().GetUtcNow().UtcDateTime,
                SnapshotJson = JsonSerializer.Serialize(new { Kind = "MISSING", ItemId = item, Quantity = 1m, Reusable = true,
                    DirtySources = new[] { sources[0] with { Quantity = 1 } } })
            });
            await db.SaveChangesAsync(ct);
            Assert.Equal(1, Assert.Single(await Available(db, room, first.WorkId, item, ct)).Quantity);
            Assert.Equal(2, Assert.Single(await Available(db, room, second.WorkId, item, ct)).Quantity);
            var writtenOff = await Position(accounting, room, item, "WRITTEN_OFF", ct);
            var laundry = await Position(accounting, room, item, "LAUNDRY", ct);
            var blocked = await Assert.ThrowsAsync<ApiException>(() => Move(accounting, dirty, laundry, 1, user, "LAUNDRY", null, ct));
            Assert.Equal("MISSING_USED_VERIFICATION_PENDING", blocked.Code);
            Assert.Equal(4, dirty.Quantity);
            Assert.Equal(0, laundry.Quantity);
            await Move(accounting, dirty, writtenOff, 1, user, "WRITE_OFF", incident.WorkId, ct);
            incident.Status = "CLOSED";
            incident.VerifiedBy = await db.Users.Where(u => u.UserId != user).Select(u => u.UserId).FirstAsync(ct);
            incident.VerifiedAt = new FixtureClock().GetUtcNow().UtcDateTime;
            incident.ClosedAt = new FixtureClock().GetUtcNow().UtcDateTime;
            incident.VerifiedBy = user;
            incident.VerifiedAt = incident.ClosedAt;
            await db.SaveChangesAsync(ct);
            Assert.Equal(3, dirty.Quantity);
            Assert.Equal(1, writtenOff.Quantity);
            Assert.Equal(cleanBefore, clean.Quantity);
            Assert.Equal(stockBefore, await db.StockMovements.CountAsync(ct));
            Assert.Equal(1, Assert.Single(await Available(db, room, first.WorkId, item, ct)).Quantity);
            Assert.Equal(2, Assert.Single(await Available(db, room, second.WorkId, item, ct)).Quantity);
            var ledger = Assert.Single(await db.ReusableMovements.AsNoTracking().Where(m => m.WorkId == incident.WorkId).ToArrayAsync(ct));
            Assert.Equal("WRITE_OFF", ledger.Kind);
            Assert.Equal(dirty.PositionId, ledger.SourcePositionId);
            Assert.Equal(writtenOff.PositionId, ledger.DestinationPositionId);
            Assert.Equal(1, ledger.Quantity);
        });
    }

    [TaskSupplyDatabaseFact]
    public async Task WholeDirtyHandoverPreservesSourceButPartialTransferMakesRemainderUnreportable()
    {
        await WithFixture(async (db, room, user, item, first, second, used, dirty, clean, accounting, ct) =>
        {
            await Move(accounting, used, dirty, 4, user, "REPLACEMENT", first.WorkId, ct);
            var destination = await Position(accounting, room, item, "DIRTY", ct, user);
            var original = Assert.Single(await Available(db, room, first.WorkId, item, ct));
            await Move(accounting, dirty, destination, 4, user, "HANDOVER", null, ct);
            var moved = Assert.Single(await Available(db, room, first.WorkId, item, ct));
            Assert.Equal(original.MovementId, moved.MovementId);
            Assert.Equal(destination.PositionId, moved.PositionId);
            Assert.Equal(4, moved.Quantity);
            await Move(accounting, destination, dirty, 1, user, "HANDOVER", null, ct);
            Assert.Empty(await Available(db, room, first.WorkId, item, ct));
            Assert.Equal(1, dirty.Quantity);
            Assert.Equal(3, destination.Quantity);
            Assert.Equal(0, clean.Quantity);
        });
    }

    private static WorkItem NewWork(Room room, long user, string kind = "CLEANING") => new() {
        BranchId = room.BranchId, RoomId = room.RoomId, TurnoverNo = room.TurnoverNo,
        WorkNo = Guid.NewGuid().ToString("N"), Kind = kind, Status = "IN_PROGRESS",
        Title = "Rollback-only missing custody SQL verification", CreatedBy = user,
        CreatedAt = new FixtureClock().GetUtcNow().UtcDateTime
    };

    private static async Task WithFixture(Func<HotelOpsCompactDbContext, Room, long, long, WorkItem, WorkItem,
        ReusablePosition, ReusablePosition, ReusablePosition, object, CancellationToken, Task> verify)
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127", new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = timeout.Token;
        await using var db = new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.StaffSupplies',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;", ct);
        var profile = await db.ItemLifecycleProfiles.AsNoTracking().FirstAsync(p => p.Classification == "REUSABLE", ct);
        var room = await db.Rooms.AsNoTracking().FirstAsync(r => r.BranchId == profile.BranchId && r.IsActive
            && !db.ReusablePositions.Any(p => p.RoomId == r.RoomId && p.ItemId == profile.ItemId), ct);
        var user = await db.UserRoles.Where(r => r.BranchId == room.BranchId && r.RevokedAt == null)
            .Select(r => r.UserId).FirstAsync(ct);
        var accounting = Activator.CreateInstance(Accounting, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [db, new FixtureClock()], null)!;
        var used = await Position(accounting, room, profile.ItemId, "IN_USE", ct);
        var dirty = await Position(accounting, room, profile.ItemId, "DIRTY", ct);
        var clean = await Position(accounting, room, profile.ItemId, "READY", ct);
        // Establish a labelled pre-existing room set; this is fixture history, not a production receipt.
        used.Quantity = 4;
        db.ReusableMovements.Add(new ReusableMovement {
            BranchId = room.BranchId, ItemId = profile.ItemId, DestinationPositionId = used.PositionId,
            Quantity = 4, Kind = "RECEIPT", ActorUserId = user, OperationKey = Guid.NewGuid(),
            Reason = "Rollback-only initial room set", OccurredAt = new FixtureClock().GetUtcNow().UtcDateTime
        });
        var first = NewWork(room, user);
        var second = NewWork(room, user);
        db.WorkItems.AddRange(first, second);
        await db.SaveChangesAsync(ct);
        await verify(db, room, user, profile.ItemId, first, second, used, dirty, clean, accounting, ct);
        await transaction.RollbackAsync(ct);
    }

    private static Task<ReusablePosition> Position(object accounting, Room room, long item, string status, CancellationToken ct, long? user = null) =>
        (Task<ReusablePosition>)Accounting.GetMethod("Position", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(accounting, [room.BranchId, item, status, null, user, user == null ? (long?)room.RoomId : null, ct])!;
    private static Task Move(object accounting, ReusablePosition source, ReusablePosition destination, decimal quantity,
        long user, string kind, long? work, CancellationToken ct) =>
        (Task)Accounting.GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(accounting, [source, destination, quantity, user, Guid.NewGuid(), kind, "Missing custody SQL verification", ct, work, null, true])!;
    private static Task<MissingDirtySource[]> Available(HotelOpsCompactDbContext db, Room room, long work, long item, CancellationToken ct) =>
        (Task<MissingDirtySource[]>)Custody.GetMethod("Available", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [db, room.BranchId, room.RoomId, work, item, ct])!;
}

