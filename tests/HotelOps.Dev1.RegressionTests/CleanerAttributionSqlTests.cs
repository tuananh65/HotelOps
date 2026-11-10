using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleanerR04SqlTheoryAttribute : TheoryAttribute
{
    public CleanerR04SqlTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("FE04_R04_SQL_TRIAL") != "APPROVED_DISPOSABLE_ONLY")
            Skip = "Requires separately approved disposable R04 schema and explicit local connection; no DDL is run.";
    }
}

[Collection("HotelOps isolated SQL")]
public sealed class CleanerAttributionSqlTests
{
    private sealed class Seed
    {
        public long RoomId { get; set; }
        public long CleaningTypeId { get; set; }
        public long Cleaner { get; set; }
        public long Other { get; set; }
    }

    [CleanerR04SqlTheory]
    [InlineData("single")][InlineData("reassignment")][InlineData("multiple")]
    [InlineData("wrong-cycle")][InlineData("rollback")][InlineData("persisted-revision")]
    public async Task Candidate_preserves_exact_completion_and_rolls_back_with_caller(string scenario)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = budget.Token;
        var connection = Environment.GetEnvironmentVariable("FE04_R04_SQL_CONNECTION")
            ?? throw new InvalidOperationException("Explicit disposable SQL connection required.");
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        Assert.Equal(1,await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.FE04.UnblockR01.Owner' AND CONVERT(nvarchar(128),value)='DEV1_DISPOSABLE'
            """).SingleAsync(ct));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var seed = await db.Database.SqlQuery<Seed>($"""
            SELECT TOP(1) r.room_id AS RoomId,types.cleaning_type_id AS CleaningTypeId,
              u.user_id AS Cleaner,o.user_id AS Other FROM dbo.rooms r
            CROSS JOIN (SELECT DISTINCT cleaning_type_id FROM dbo.cleaning_tasks) types
            CROSS JOIN dbo.users u CROSS JOIN dbo.users o
            WHERE u.deleted_at IS NULL AND o.user_id<>u.user_id AND o.deleted_at IS NULL
              AND r.is_active=1 AND NOT EXISTS(SELECT 1 FROM dbo.cleaning_tasks t
                WHERE t.room_id=r.room_id AND t.status NOT IN('COMPLETED','CANCELLED'))
            ORDER BY r.room_id,types.cleaning_type_id,u.user_id,o.user_id
            """).SingleAsync(ct);
        // Synthetic component fixture, entirely inside this rollback transaction. Never an authority receipt.
        var no = "R04-" + Guid.NewGuid().ToString("N")[..20];
        var task = await db.Database.SqlQuery<long>($"""
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,created_by,created_at,actual_start_at,actual_end_at)
            OUTPUT INSERTED.cleaning_task_id AS Value
            VALUES({no},{seed.RoomId},{seed.CleaningTypeId},'IN_PROGRESS','PENDING_INSPECTION',{seed.Cleaner},SYSUTCDATETIME(),DATEADD(minute,-5,SYSUTCDATETIME()),SYSUTCDATETIME())
            """).ToArrayAsync(ct);
        var id = Assert.Single(task);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id={id} WHERE room_id={seed.RoomId};
            IF @@ROWCOUNT=0 INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({seed.RoomId},{id});
            INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status)
            VALUES({id},{seed.Cleaner},{seed.Other},DATEADD(minute,-5,SYSUTCDATETIME()),'ACTIVE');
            INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,logged_by,logged_at)
            SELECT {id},100,'COMPLETED',{seed.Cleaner},actual_end_at FROM dbo.cleaning_tasks WHERE cleaning_task_id={id};
            """,ct);
        var assignment = await db.CleaningAssignments.Where(a => a.CleaningTaskId == id).Select(a => a.AssignmentId).SingleAsync(ct);
        var progress = await db.CleaningProgressLogs.Where(p => p.CleaningTaskId == id).Select(p => p.ProgressId).SingleAsync(ct);
        var revision = await db.Database.SqlQuery<byte[]>($"SELECT CONVERT(binary(8),row_version) AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={id}").SingleAsync(ct);
        if (scenario == "multiple") await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,ended_at,assignment_status)
            VALUES({id},{seed.Other},{seed.Other},DATEADD(minute,-10,SYSUTCDATETIME()),DATEADD(minute,-6,SYSUTCDATETIME()),'REASSIGNED')
            """,ct);
        if (scenario == "wrong-cycle") await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={seed.RoomId}",ct);
        async Task Capture() => await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe04_capture_cleaner_attribution @task={id},@assignment={assignment},@cleaner={seed.Cleaner},@progress={progress}",ct);
        if (scenario == "wrong-cycle")
        {
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(Capture);
            await tx.RollbackAsync(ct);
            return;
        }
        await Capture();
        if (scenario == "multiple")
            Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={id}").SingleAsync(ct));
        else
        {
            if (scenario == "reassignment") await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='REASSIGNED' WHERE assignment_id={assignment};
                INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status)
                VALUES({id},{seed.Other},{seed.Other},SYSUTCDATETIME(),'ACTIVE')
                """,ct);
            if (scenario == "persisted-revision") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={id}",ct);
            Assert.Equal(seed.Cleaner,await db.Database.SqlQuery<long>($"SELECT cleaner_user_id AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={id}").SingleAsync(ct));
            Assert.Equal(revision,await db.Database.SqlQuery<byte[]>($"SELECT completion_revision AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={id}").SingleAsync(ct));
            Assert.Equal(await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS Value").SingleAsync(ct),
                await db.Database.SqlQuery<long>($"SELECT writer_transaction_id AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={id}").SingleAsync(ct));
        }
        await tx.RollbackAsync(ct);
        Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={id}").SingleAsync(ct));
        Assert.False(await db.CleaningTasks.AnyAsync(t => t.CleaningTaskId == id,ct));
    }
}
