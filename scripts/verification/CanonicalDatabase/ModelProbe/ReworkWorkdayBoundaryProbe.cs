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
        const string guid = "5270D07C-7C27-477B-A60E-86485ADD7FAD";
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
                SELECT @actor AS Actor,@rework AS Rework;
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
            await tx.RollbackAsync(ct);
        }
        if (await db.Users.AsNoTracking().AnyAsync(u => u.Username == prefix, ct)
            || await db.Branches.AsNoTracking().AnyAsync(b => b.BranchCode == prefix, ct))
            throw new InvalidOperationException("Synthetic fixture survived rollback.");
        Console.WriteLine("PASS: production EF boundary query; running/held/reinspection/null/foreign actor; fixture rollback. Not HTTP authorization or full Pause All acceptance.");
        return 0;
    }
    private sealed class Fixture { public long Actor { get; set; } public long Rework { get; set; } }
}
