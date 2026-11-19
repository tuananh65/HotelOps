using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReceptionReadinessSqlFactAttribute : FactAttribute
{
    public ReceptionReadinessSqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RECEPTION_REVIEW_SQL_CONNECTION")))
            Skip = "Opt-in SQL verification against explicitly labelled demo data; all writes roll back.";
    }
}

public sealed class ReceptionReadinessSqlTests
{
    [ReceptionReadinessSqlFact]
    public async Task CheckoutIgnoresOldPassAndCancelledCyclesUntilNewCyclePasses()
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("RECEPTION_REVIEW_SQL_CONNECTION")).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var ct = CancellationToken.None;
        try
        {
            var room = await db.Rooms.AsNoTracking().SingleAsync(r => r.RoomNumber == "DEMO-LT-01"
                && r.RoomType.TypeCode == "DEMO-RECEPTION-READY", ct);
            var old = await db.CleaningTasks.AsNoTracking().Where(t => t.RoomId == room.RoomId).OrderByDescending(t => t.CreatedAt).FirstAsync(ct);
            var inspection = await db.RoomInspections.AsNoTracking().SingleAsync(i => i.CleaningTaskId == old.CleaningTaskId, ct);
            var reader = new EffectiveRoomReadiness(db);
            async Task<string> State() => (await reader.ReadAsync([room.RoomId], DateTime.UtcNow, ct))[room.RoomId].Readiness;
            Assert.Equal("READY", await State());
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,changed_at,reason,effective_from,effective_to)
                VALUES({room.RoomId},'OCCUPIED','DIRTY','CHECKOUT',SYSUTCDATETIME(),N'[DEMO] Rollback-only readiness test','READY','PENDING_CLEANING');
                """, ct);
            Assert.Equal("PENDING_CLEANING", await State());
            var code = "DEMO-TEST-" + Guid.NewGuid().ToString("N")[..20];
            var inserted = await db.Database.SqlQuery<long>($"""
                INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,created_at,created_by)
                VALUES({code},{room.RoomId},{old.CleaningTypeId},'PENDING','PENDING_ASSIGNMENT',SYSUTCDATETIME(),{old.CreatedBy});
                SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
                """).ToArrayAsync(ct);
            var task = inserted.Single();
            Assert.Equal("PENDING_CLEANING", await State());
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='CANCELLED',workflow_status='CANCELLED' WHERE cleaning_task_id={task}", ct);
            Assert.Equal("PENDING_CLEANING", await State()); // Must not fall back to the old passed cycle.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='IN_PROGRESS',workflow_status='PENDING_INSPECTION' WHERE cleaning_task_id={task}", ct);
            Assert.Equal("PENDING_INSPECTION", await State());
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,result,created_at,inspected_at)
                VALUES({code},{room.RoomId},{task},{inspection.VersionId},{inspection.InspectorUserId},'COMPLETED','PASSED','PASS',SYSUTCDATETIME(),SYSUTCDATETIME());
                UPDATE dbo.cleaning_tasks SET status='COMPLETED',workflow_status='COMPLETED' WHERE cleaning_task_id={task};
                """, ct);
            Assert.Equal("READY", await State());
        }
        finally { await tx.RollbackAsync(ct); }
    }
}
