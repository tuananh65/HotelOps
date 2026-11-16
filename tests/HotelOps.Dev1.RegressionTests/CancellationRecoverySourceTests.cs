using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed partial class FreshStartAdmissionSqlTests
{
    // Called only after the existing fixture verifies recovery server/database/GUID.
    // Component persistence evidence, not HTTP/session/command authorization acceptance.
    private static async Task VerifyCancellationRestoration(HotelOpsDbContext db, long actor, bool blocked)
    {
        var code = "CANCEL-" + Guid.NewGuid().ToString("N")[..12];
        var floor = await db.Floors.AsNoTracking().Where(f => f.IsActive && f.Branch.IsActive).FirstAsync();
        var type = await db.RoomTypes.AsNoTracking().Select(t => t.RoomTypeId).FirstAsync();
        var cleaningType = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "CLEANING_TYPE" && r.IsActive).Select(r => r.ReferenceId).FirstAsync();
        var version = await db.InspectionChecklistVersions.AsNoTracking().Where(v => v.Status == "PUBLISHED").Select(v => v.VersionId).FirstAsync();
        var operation = Guid.NewGuid(); var publication = Guid.NewGuid();
        long roomId;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var publisher = new ProvisionalCleaningNotification(db, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string,string?> { ["CleaningCancellation:Capability"]="VERIFIED_V1" }).Build(), new ClosureEnvironment());
            Assert.True(await publisher.CancellationReadinessAvailableAsync(default));
            var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(), DateTimeKind.Utc);
            var room = new Room { BranchId=floor.BranchId, FloorId=floor.FloorId, RoomTypeId=type, RoomNumber=code, OperationalStatus="DIRTY", IsActive=true };
            db.Rooms.Add(room); await db.SaveChangesAsync(); roomId=room.RoomId; db.ChangeTracker.Clear();
            var previous = new CleaningTask { TaskNo=code+"-P", RoomId=roomId, CleaningTypeId=cleaningType, Status="COMPLETED", CreatedBy=actor, CreatedAt=now.AddDays(-1) };
            var cancelled = new CleaningTask { TaskNo=code+"-C", RoomId=roomId, CleaningTypeId=cleaningType, Status="CANCELLED", CreatedBy=actor, CreatedAt=now };
            db.CleaningTasks.AddRange(previous,cancelled); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='COMPLETED' WHERE cleaning_task_id={previous.CleaningTaskId}; UPDATE dbo.cleaning_tasks SET workflow_status='CANCELLED' WHERE cleaning_task_id={cancelled.CleaningTaskId}; INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({roomId},{previous.CleaningTaskId});");
            var inspection = new RoomInspection { InspectionNo=code, RoomId=roomId, CleaningTaskId=previous.CleaningTaskId, VersionId=version, InspectorUserId=actor, Status="COMPLETED", Result="PASS", CreatedAt=now.AddHours(-1), InspectedAt=now.AddHours(-1) };
            db.RoomInspections.Add(inspection); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='PASSED' WHERE inspection_id={inspection.InspectionId}");
            if(blocked)
            {
                db.RoomBlocks.Add(new RoomBlock { RoomId=roomId, BlockedBy=actor, BlockedAt=now.AddMinutes(-1), Reason="Synthetic cancellation block" });
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            }
            var readiness=(await new EffectiveRoomReadiness(db).ReadAsync([roomId],now,default))[roomId].Readiness;
            Assert.Equal(blocked ? "BLOCKED" : "READY",readiness);
            var assignment = new CleaningAssignment { CleaningTaskId=cancelled.CleaningTaskId, AssigneeUserId=actor, AssignedBy=actor, AssignedAt=now.AddMinutes(-2), AssignmentStatus="CANCELLED", EndedAt=now };
            db.CleaningAssignments.Add(assignment); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var eventId=(await db.Database.SqlQuery<long>($"INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) OUTPUT INSERTED.event_id AS [Value] VALUES({roomId},{cancelled.CleaningTaskId},{readiness},{blocked},{actor},{now})").ToArrayAsync()).Single();
            var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={cancelled.CleaningTaskId}").SingleAsync();
            var receipt=new CleaningCancellationResult(cancelled.CleaningTaskId,floor.BranchId,roomId,assignment.AssignmentId,actor,operation,publication,eventId,now,'"'+Convert.ToHexString(revision)+'"',Readiness:readiness);
            db.AuditLogs.Add(new AuditLog { BranchId=floor.BranchId, ActorUserId=actor, EntityType="cleaning_tasks", EntityId=cancelled.CleaningTaskId.ToString(), Action="UPDATE", OccurredAt=now, NewValues=JsonSerializer.Serialize(new { operationId=operation, action="CANCEL_CLEANING" }) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await publisher.StageCancellationAsync(actor,new byte[32],receipt,default);
            Assert.NotNull(await CleaningCancellationNotificationSource.LoadAsync(db,publication,actor,default));
            Assert.Null(await CleaningCancellationNotificationSource.LoadAsync(db,publication,long.MaxValue,default));
            Assert.False(await ProvisionalCleaningNotification.VerifyCancellationSourceAsync(db,actor,receipt with { Readiness=blocked ? "READY" : "BLOCKED" },default));
            Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WHERE publication_id={publication} AND status='PENDING'").SingleAsync());
            await tx.RollbackAsync();
        }
        Assert.False(await db.Rooms.AnyAsync(r=>r.RoomId==roomId));
        Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_operation_reservations WHERE actor_user_id={actor} AND operation_key={operation}").SingleAsync());
        Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_publications WHERE publication_id={publication}").SingleAsync());
    }
}
