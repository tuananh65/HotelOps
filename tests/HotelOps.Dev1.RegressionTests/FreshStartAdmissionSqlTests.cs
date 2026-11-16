using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class DevelopmentRecoveryFactAttribute : FactAttribute
{
    public DevelopmentRecoveryFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEV3_RECOVERY_RECEIPT")))
            Skip = "Requires an explicitly selected, identity-verified development recovery clone.";
    }
}

[Collection("HotelOps isolated SQL")]
public sealed class FreshStartAdmissionSqlTests
{
    [DevelopmentRecoveryFact]
    public async Task RequiresTransactionLocksAndAuthorityAndUsesTemporalBlockRangeWithRollback()
    {
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("DEV3_RECOVERY_RECEIPT")!));
        var root = receipt.RootElement;
        Assert.Equal("RECOVERY_VERIFIED", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("developmentOwnershipConfirmed").GetBoolean());
        var target = root.GetProperty("recoveryDatabase").GetString()!;
        Assert.Matches("^HotelOps_DEV3_142A_Recovery_[a-f0-9]{8}$", target);
        var guid = Guid.Parse(root.GetProperty("recoveryGuid").GetString()!);
        Assert.NotEqual(Guid.Empty, guid);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer($"Server=localhost\\MSSQLSERVER01;Database={target};Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        Assert.Equal(root.GetProperty("expectedServer").GetString(), await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS [Value]").SingleAsync());
        Assert.Equal(target, await db.Database.SqlQuery<string>($"SELECT DB_NAME() AS [Value]").SingleAsync());
        Assert.Equal(guid, await db.Database.SqlQuery<Guid>($"SELECT database_guid AS [Value] FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync());
        Assert.True(await CleaningStartSchema.InstalledAsync(db, default));
        var task = await db.CleaningTasks.AsNoTracking().Where(t => t.Room.IsActive && !db.RoomBlocks.Any(b => b.RoomId == t.RoomId))
            .Where(t => !db.CleaningAssignments.Any(a => a.CleaningTaskId == t.CleaningTaskId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null))
            .Select(t => new { t.CleaningTaskId, t.RoomId, t.Room.BranchId, Actor = t.CreatedBy }).FirstAsync();
        var ticket = SessionReference.FromProtectedTicket(task.Actor, new string('A', 43), false, DateTime.UtcNow);
        var admission = new SqlFreshStartCleaningAdmission(db, ticket, 1, _ => Task.FromResult(false));
        var noTransaction = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admission.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
        Assert.Equal("ROOM_STATUS_CALLER_TRANSACTION_REQUIRED", noTransaction.Code);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var missingLock = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admission.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
            Assert.Equal("ROOM_STATUS_GLOBAL_LOCK_ORDER_REQUIRED", missingLock.Code);
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([task.Actor], default);
            var resource = "HotelOps:WorkTime:User:" + task.Actor;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000; IF @r<0 THROW 51990,'Test time lock failed',1;");
            Assert.True(await admission.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
            var connection = db.Database.GetDbConnection(); var transaction = tx.GetDbTransaction();
            await admission.VerifyHeldAsync(db, connection, transaction, task.Actor, task.RoomId, task.CleaningTaskId, default);
            var denied = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admission.VerifyAsync(db, connection, transaction,
                ticket, 1, task.Actor, task.BranchId, task.RoomId, task.CleaningTaskId, 1, default));
            Assert.Equal("ROOM_STATUS_FINAL_AUTHORITY_REQUIRED", denied.Code);
            // Component proof only: injected true does not certify real session/role authorization.
            // A synthetic current-cycle pointer is rolled back with the rest of this fixture.
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={task.RoomId}").SingleAsync());
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({task.RoomId},{task.CleaningTaskId})");
            // Only exercise the admission port's ownership proof; task lifecycle validation belongs to StartCleaningAsync.
            var syntheticAssignment = new CleaningAssignment { CleaningTaskId=task.CleaningTaskId,
                AssigneeUserId=task.Actor, AssignedBy=task.Actor, AssignedAt=DateTime.UtcNow.AddMinutes(-1),
                AssignmentStatus="ACTIVE", Reason="DEV3 rollback admission fixture" };
            db.CleaningAssignments.Add(syntheticAssignment);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var admitted = new SqlFreshStartCleaningAdmission(db, ticket, 1, _ => Task.FromResult(true));
            Assert.True(await admitted.AcquireAsync(task.BranchId, task.CleaningTaskId, default));
            await admitted.VerifyAsync(db, connection, transaction, ticket, 1, task.Actor, task.BranchId,
                task.RoomId, task.CleaningTaskId, syntheticAssignment.AssignmentId, default);
            var stale = await Assert.ThrowsAsync<RoomStatusTransitionException>(() => admitted.VerifyAsync(db, connection, transaction,
                ticket, 1, task.Actor, task.BranchId, task.RoomId, task.CleaningTaskId, long.MaxValue, default));
            Assert.Equal("ROOM_STATUS_ASSIGNMENT_INVALID", stale.Code);
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(), DateTimeKind.Utc);
            Assert.Equal(RoomStartBlockEvidence.Clear, await admission.VerifyAsync(db, connection, transaction, task.BranchId, task.RoomId, now, default));
            // Synthetic rows only, rolled back; existing room/task/user data is read-only.
            db.RoomBlocks.AddRange(
                new RoomBlock { RoomId=task.RoomId, BlockedBy=task.Actor, BlockedAt=now.AddMinutes(1), Reason="DEV3 future block" },
                new RoomBlock { RoomId=task.RoomId, BlockedBy=task.Actor, BlockedAt=now.AddMinutes(-1), ReleasedAt=now, ReleasedBy=task.Actor, Reason="DEV3 released boundary" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(RoomStartBlockEvidence.Clear, await admission.VerifyAsync(db, connection, transaction, task.BranchId, task.RoomId, now, default));
            db.RoomBlocks.Add(new RoomBlock { RoomId=task.RoomId, BlockedBy=task.Actor, BlockedAt=now, ReleasedAt=now.AddMinutes(1), ReleasedBy=task.Actor, Reason="DEV3 effective block" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(RoomStartBlockEvidence.Effective, await admission.VerifyAsync(db, connection, transaction, task.BranchId, task.RoomId, now, default));
            await tx.RollbackAsync();
        }
        Assert.False(await db.RoomBlocks.AnyAsync(b => b.RoomId == task.RoomId));
        Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={task.RoomId}").SingleAsync());
    }
}
