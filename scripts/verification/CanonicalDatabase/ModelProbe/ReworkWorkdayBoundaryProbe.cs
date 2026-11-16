using System.Data;
using System.Reflection;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class ReworkWorkdayBoundaryProbe
{
    internal static async Task<int> RunAsync()
    {
        const string server = @"meita\MSSQLSERVER01", database = "HotelOps_DEV3_142A_Recovery_2b221f40";
        var guid = Environment.GetEnvironmentVariable("HOTELOPS_DISPOSABLE_EXPECTED_GUID") ?? throw new InvalidOperationException("Verified disposable GUID required.");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
            IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = false };
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))={server} AND DB_NAME()={database}
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid=CONVERT(uniqueidentifier,{guid})
                AND database_guid<>CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000000')) THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new InvalidOperationException("Disposable identity mismatch.");
        var method = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.WorkTime.ReworkWorkdayBoundary")!
            .GetMethod("IsStoppedAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var assigned = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.WorkTime.AssignedWorkdayBoundary")!;
        Task<bool> AssignedStopped(string name, long actor) => (Task<bool>)assigned.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [db, actor, ct])!;
        Task<bool> Stopped(long actor) => (Task<bool>)method.Invoke(null, [db, actor, ct])!;
        var prefix = "D3BOUND" + Guid.NewGuid().ToString("N")[..12];
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var installer = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ReworkEndOfShift.sql", ct);
            installer = installer.Replace("$(ExpectedServer)", server).Replace("$(ExpectedDatabase)", database)
                .Replace("$(ExpectedDatabaseGuid)", guid).Replace("$(ApplyChanges)", "1");
            await db.Database.ExecuteSqlRawAsync(installer, ct); // nested commit; owning transaction rolls back
            var ids = await db.Database.SqlQuery<Fixture>($"""
                DECLARE @actor bigint,@branch bigint,@floor bigint,@type bigint,@room bigint,@cleanType bigint,
                  @task bigint,@list bigint,@version bigint,@inspection bigint,@rework bigint;
                INSERT dbo.users(employee_code,username,email,full_name,created_at,password_hash)
                  VALUES({prefix},{prefix},{prefix}+N'@example.invalid',N'Synthetic boundary',SYSUTCDATETIME(),'Disabled synthetic account');
                SET @actor=SCOPE_IDENTITY();
                INSERT dbo.branches(branch_code,branch_name) VALUES({prefix},N'Synthetic boundary'); SET @branch=SCOPE_IDENTITY();
                INSERT dbo.floors(branch_id,floor_code,floor_name) VALUES(@branch,{prefix},N'Synthetic'); SET @floor=SCOPE_IDENTITY();
                INSERT dbo.room_types(type_code,type_name,standard_capacity) VALUES({prefix},N'Synthetic',1); SET @type=SCOPE_IDENTITY();
                INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status)
                  VALUES(@branch,@floor,@type,{prefix},'INSPECTION'); SET @room=SCOPE_IDENTITY();
                INSERT dbo.reference_values(group_code,value_code,display_name) VALUES('CLEANING_TYPE',{prefix},N'Synthetic'); SET @cleanType=SCOPE_IDENTITY();
                INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at)
                  VALUES({prefix},@room,@cleanType,'IN_PROGRESS',@actor,'PENDING_INSPECTION',SYSUTCDATETIME()); SET @task=SCOPE_IDENTITY();
                INSERT dbo.inspection_checklists(checklist_code,checklist_name,room_type_id) VALUES({prefix},N'Synthetic',@type); SET @list=SCOPE_IDENTITY();
                INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@list,1,'DRAFT'); SET @version=SCOPE_IDENTITY();
                INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at)
                  VALUES({prefix},@room,@task,@version,@actor,'IN_PROGRESS','INSPECTING',SYSUTCDATETIME()); SET @inspection=SCOPE_IDENTITY();
                INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at)
                  VALUES(@task,@inspection,@actor,N'Synthetic boundary','IN_PROGRESS','IN_PROGRESS',SYSUTCDATETIME()); SET @rework=SCOPE_IDENTITY();
                INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status)
                  VALUES(@task,@actor,@actor,SYSUTCDATETIME(),'ACTIVE');
                DECLARE @department bigint,@operational bigint;
                INSERT dbo.departments(branch_id,department_code,department_name) VALUES(@branch,{prefix},N'Synthetic'); SET @department=SCOPE_IDENTITY();
                INSERT dbo.operational_tasks(task_code,branch_id,department_id,title,status,progress_percent,created_by,created_at)
                  VALUES({prefix},@branch,@department,N'Synthetic boundary','IN_PROGRESS',0,@actor,SYSUTCDATETIME()); SET @operational=SCOPE_IDENTITY();
                INSERT dbo.operational_task_assignments(task_id,assignee_id,assigned_by,assigned_at,assignment_status)
                  VALUES(@operational,@actor,@actor,SYSUTCDATETIME(),'ACTIVE');
                SELECT @actor AS Actor,@rework AS Rework,@task AS Cleaning,@operational AS Task;
                """).ToArrayAsync(ct);
            var f = ids.Single();
            if (await Stopped(f.Actor)) throw new InvalidOperationException("Running rework without interval accepted.");
            foreach (var state in new string?[] { "ON_HOLD", "PENDING_REINSPECTION", null, "IN_PROGRESS" })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET workflow_status={state} WHERE rework_id={f.Rework}", ct);
                if (await Stopped(f.Actor) != (state is "ON_HOLD" or "PENDING_REINSPECTION"))
                    throw new InvalidOperationException("Rework boundary disagrees with canonical workflow.");
            }
            if (!await Stopped(long.MaxValue)) throw new InvalidOperationException("Foreign source blocked unrelated actor.");
            // Execute the production EF methods, including the missing-version LEFT JOIN.
            if (await AssignedStopped("TasksStoppedAsync", f.Actor)) throw new InvalidOperationException("Missing task authority was treated as stopped.");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({f.Task},'IN_PROGRESS')", ct);
            foreach (var state in new[] { "IN_PROGRESS", "PAUSED" })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status={state} WHERE task_id={f.Task}", ct);
                if (await AssignedStopped("TasksStoppedAsync", f.Actor) != (state != "IN_PROGRESS")) throw new InvalidOperationException("Task stop predicate mismatch.");
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET completed_at=SYSUTCDATETIME() WHERE task_id={f.Task}", ct);
            if (await AssignedStopped("TasksStoppedAsync", f.Actor)) throw new InvalidOperationException("Inconsistent task completion accepted.");
            foreach (var state in new string?[] { "IN_PROGRESS", "ON_HOLD", "PENDING_INSPECTION", "REWORK_REQUIRED", null })
            {
                foreach (var ended in new[] { false, true })
                {
                    DateTime? end = ended ? DateTime.UtcNow : null;
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={state},actual_end_at={end} WHERE cleaning_task_id={f.Cleaning}", ct);
                    var expected = state == "ON_HOLD" && !ended || (state is "PENDING_INSPECTION" or "REWORK_REQUIRED") && ended;
                    if (await AssignedStopped("CleaningStoppedAsync", f.Actor) != expected) throw new InvalidOperationException("Cleaning stop predicate mismatch.");
                }
            }
            if (!await AssignedStopped("TasksStoppedAsync", long.MaxValue) || !await AssignedStopped("CleaningStoppedAsync", long.MaxValue))
                throw new InvalidOperationException("Foreign assignment blocked unrelated employee.");
            await tx.RollbackAsync(ct);
        }
        if (await db.Users.AsNoTracking().AnyAsync(u => u.Username == prefix, ct)
            || await db.Branches.AsNoTracking().AnyAsync(b => b.BranchCode == prefix, ct))
            throw new InvalidOperationException("Synthetic fixture survived rollback.");
        Console.WriteLine("NOT_RUN: Task PENDING_VERIFICATION requires the TaskSubmission installer; current disposable CHECK allows only IN_PROGRESS/PAUSED.");
        Console.WriteLine("PASS: production EF boundary query; Rework and assigned Task/Cleaning running/held/submitted/null/inconsistent/foreign actor; fixture rollback. Not HTTP authorization or full Pause All acceptance.");
        return 0;
    }
    private sealed class Fixture { public long Actor { get; set; } public long Rework { get; set; } public long Cleaning { get; set; } public long Task { get; set; } }
}
