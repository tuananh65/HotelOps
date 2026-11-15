using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintInitialInspectionProbe
{
    internal const string Connection = "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
    internal const string Role = "DEV3_142A_InitialInspection";
    internal static async Task<int> RunAsync(string report)
    {
        if (File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(6)); var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).Options);
        var start = new ProcessStartInfo("git") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true };
        start.ArgumentList.Add("rev-parse"); start.ArgumentList.Add("HEAD");
        using var git = Process.Start(start)!; var commit=(await git.StandardOutput.ReadToEndAsync(ct)).Trim(); await git.WaitForExitAsync(ct);
        if(git.ExitCode!=0||!System.Text.RegularExpressions.Regex.IsMatch(commit,"^[a-f0-9]{40}$"))throw new Exception("Actual source HEAD required");
        var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))+"Aa1!";
        var rolePassword=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))+"Aa1!";
        long actor=0,branch=0,version=0,task=0,room=0,other=0; bool committed=false,cleaned=false;
        var checks=new List<object>(); string? failure=null;
        async Task Seed()
        {
            await using(var cmd=db.Database.GetDbConnection().CreateCommand())
            {
                cmd.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
                cmd.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionFixture.sql",ct);
                await using var reader=await cmd.ExecuteReaderAsync(ct);
                if(!await reader.ReadAsync(ct))throw new Exception("Fixture identity missing");
                actor=reader.GetInt64(0);branch=reader.GetInt64(1);version=reader.GetInt64(2);task=reader.GetInt64(3);room=reader.GetInt64(4);
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'DEV3.SourceCommit',@value={commit}",ct);
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionWriter.sql",ct),ct);
            var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
            var hash=new PasswordHasher<User>().HashPassword(user,password);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor};
                INSERT dbo.users(employee_code,username,email,full_name,created_at) VALUES('D3-INITIAL-OTHER','D3-INITIAL-OTHER','d3-initial-other@example.invalid',N'Synthetic other inspector',SYSUTCDATETIME());
                DECLARE @other bigint=SCOPE_IDENTITY();
                INSERT dbo.user_branch_access(user_id,branch_id,granted_at) VALUES(@other,{branch},DATEADD(minute,-1,SYSUTCDATETIME()));
                INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at)
                  SELECT @other,role_id,{branch},DATEADD(minute,-1,SYSUTCDATETIME()) FROM dbo.roles WHERE role_code='HK_SUPERVISOR' AND is_active=1;
                IF @@ROWCOUNT<>1 THROW 51999,'Existing supervisor role required',1;
                DECLARE @password nvarchar(128)={rolePassword};
                EXEC(N'CREATE APPLICATION ROLE DEV3_142A_InitialInspection WITH PASSWORD='''+@password+N''', DEFAULT_SCHEMA=dbo');
                GRANT SELECT ON SCHEMA::dbo TO DEV3_142A_InitialInspection;
                GRANT VIEW DEFINITION TO DEV3_142A_InitialInspection;
                GRANT UPDATE ON dbo.users TO DEV3_142A_InitialInspection;
                GRANT INSERT,UPDATE ON dbo.user_sessions TO DEV3_142A_InitialInspection;
                GRANT UPDATE ON dbo.room_inspections TO DEV3_142A_InitialInspection;
                GRANT INSERT,UPDATE ON dbo.inspection_results TO DEV3_142A_InitialInspection;
                GRANT INSERT ON dbo.audit_logs TO DEV3_142A_InitialInspection;
                GRANT UPDATE ON dbo.notification_recipients TO DEV3_142A_InitialInspection;
                ALTER ROLE hotelops_initial_inspection_writer ADD MEMBER DEV3_142A_InitialInspection;
                """,ct);
            other=await db.Users.Where(u=>u.Username=="D3-INITIAL-OTHER").Select(u=>u.UserId).SingleAsync(ct);
        }
        async Task Cleanup(CancellationToken token)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                IF EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers WHERE issuer<>'FE05.UC041.INITIAL')
                  OR EXISTS(SELECT 1 FROM dbo.room_inspections WHERE cleaning_task_id<>{task}) THROW 51999,'Foreign fixture source; cleanup refused',1;
                DECLARE @list bigint=(SELECT checklist_id FROM dbo.inspection_checklist_versions WHERE version_id={version});
                DECLARE @type bigint=(SELECT room_type_id FROM dbo.rooms WHERE room_id={room});
                DECLARE @floor bigint=(SELECT floor_id FROM dbo.rooms WHERE room_id={room});
                DECLARE @cleanType bigint=(SELECT cleaning_type_id FROM dbo.cleaning_tasks WHERE cleaning_task_id={task});
                DELETE r FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id
                  WHERE n.notification_type='INSPECTION_ASSIGNED' AND n.created_by={actor};
                DELETE dbo.notifications WHERE notification_type='INSPECTION_ASSIGNED' AND created_by={actor};
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={actor};
                UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS' WHERE cleaning_task_id={task};
                DELETE r FROM dbo.inspection_results r JOIN dbo.room_inspections i ON i.inspection_id=r.inspection_id WHERE i.cleaning_task_id={task};
                DELETE dbo.room_inspections WHERE cleaning_task_id={task};
                DELETE dbo.cleaning_assignments WHERE cleaning_task_id={task};
                DELETE dbo.room_cleaning_cycle_state WHERE room_id={room} AND cleaning_task_id={task};
                DELETE dbo.cleaning_tasks WHERE cleaning_task_id={task};
                DELETE dbo.rooms WHERE room_id={room}; DELETE dbo.room_types WHERE room_type_id=@type; DELETE dbo.floors WHERE floor_id=@floor;
                DELETE dbo.inspection_checklist_items WHERE version_id={version};
                DELETE dbo.inspection_checklist_versions WHERE version_id={version};
                DELETE dbo.inspection_checklists WHERE checklist_id=@list AND branch_id IS NULL;
                DELETE dbo.reference_values WHERE reference_id=@cleanType AND value_code='D3-SYNTHETIC';
                DELETE dbo.user_roles WHERE user_id IN({actor},{other}); DELETE dbo.user_branch_access WHERE user_id IN({actor},{other});
                DELETE dbo.user_sessions WHERE user_id IN({actor},{other});
                DELETE dbo.audit_logs WHERE actor_user_id IN({actor},{other}) OR(entity_type='users' AND entity_id IN(CONVERT(nvarchar(50),{actor}),CONVERT(nvarchar(50),{other})));
                DELETE dbo.users WHERE user_id IN({actor},{other}) AND username IN('D3-EVIDENCE','D3-INITIAL-OTHER');
                DELETE dbo.branches WHERE branch_id={branch} AND branch_code='D3-EVIDENCE';
                DROP PROCEDURE dbo.fe05_assign_initial_inspection;
                DROP APPLICATION ROLE DEV3_142A_InitialInspection;
                DROP ROLE hotelops_initial_inspection_writer;
                DROP TABLE dbo.fe04_rework_authority_issuers;
                """,token);
        }
        try
        {
        await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        { await Seed(); await Cleanup(ct); await rehearsal.RollbackAsync(ct); }
        checks.Add(new { exactIdentityAndSchema=true,seedCleanupRehearsalRolledBack=true });
            await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
            { await Seed();await setup.CommitAsync(ct);committed=true; }
            checks.Add(await SprintInitialInspectionServiceProbe.VerifyAsync(db,actor,other,branch,task,room,version,password,rolePassword,ct));
            checks.Add(await SprintInitialInspectionHttpsProbe.VerifyAsync(db,actor,branch,task,password,rolePassword,ct));
        }
        catch(Exception e){failure=e.GetType().Name+": "+e.Message;}
        finally
        {
            if(committed)
            {
                await using var cleanup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,CancellationToken.None);
                await Cleanup(CancellationToken.None);await cleanup.CommitAsync(CancellationToken.None);
            }
            cleaned=!await db.Users.AnyAsync()&&!await db.Branches.AnyAsync()&&!await db.RoomInspections.AnyAsync()&&!await db.Notifications.AnyAsync()
                &&!await db.AuditLogs.AnyAsync()&&await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID('dbo.fe05_assign_initial_inspection') IS NULL AND OBJECT_ID('dbo.fe04_rework_authority_issuers') IS NULL AND DATABASE_PRINCIPAL_ID('DEV3_142A_InitialInspection') IS NULL THEN 1 ELSE 0 END AS Value").SingleAsync()==1;
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=failure==null&&cleaned,checks,failure,cleanup=cleaned,
                scope="Initial UC041 assignment and UC042 execution, disposable SQL and validated HTTPS; reassignment and full UC completion not asserted"},new JsonSerializerOptions{WriteIndented=true}));
        }
        return failure==null&&cleaned?0:1;
    }
    internal static HotelOpsDbContext Restricted(string password)
    {
        var connection=Connection+";Pooling=false;Enlist=false";
        return new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection)
            .AddInterceptors(new IsolatedSqlApplicationRole(connection,Role,password,"meita",Guid.Parse("1D5A9B53" + "-7D19-435A-96AB-9FDFF447B6E0"),"cc78c13ffc124efcad67b3d804baa84c",true)).Options);
    }
}
