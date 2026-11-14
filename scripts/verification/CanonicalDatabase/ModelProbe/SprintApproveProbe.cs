using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class SprintApproveProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if (File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var fixture = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql", ct);
        var method = typeof(RoomTypeCreationService).GetMethod("ApproveDomainInCallerTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var checks = new List<object>();
        string? failure = null;
        foreach (var scenario in new[] { "execution-catalog", "approve", "duplicate", "mandatory-fail", "stale", "wrong-branch", "permission-revoked", "session-revoked", "cleaner-conflict", "invalid-state", "audit-failure" })
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Fixture verifies exact instance, database GUID and owned run marker before any INSERT.
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = tx.GetDbTransaction(); command.CommandText = fixture;
                long actor, branch, inspection, task, room;
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    if (!await reader.ReadAsync(ct)) throw new Exception("Missing fixture identity");
                    actor=reader.GetInt64(0); branch=reader.GetInt64(1); inspection=reader.GetInt64(2); task=reader.GetInt64(3); room=reader.GetInt64(4);
                }
                var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
                { CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3, IterationCount=PasswordCredentialService.BaselineIterationCount }));
                var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
                var user = await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
                var hash = passwords.HashPassword(user,password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}",ct);
                var credentials = new CredentialLockoutService(db,passwords,TimeProvider.System);
                var sessions = new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System);
                var verified = await credentials.VerifyAsync(actor,password,(u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"),ct);
                var login = await sessions.CreateSessionAsync(verified,ct);
                db.ChangeTracker.Clear();
                var current = new Identity(new(actor,login.Session.SessionId,false));
                var permission = new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
                var service = new RoomTypeCreationService(db,current,permission,sessions,TimeProvider.System);
                async Task<string> Tag()=> '"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct))+'"';
                async Task<object> Apply(string tag,long scope)
                {
                    var pending=(Task)method.Invoke(service,[login.Ticket,login.Session.SessionId,scope,inspection,tag,ct])!;
                    await pending;
                    return pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                }
                var tag=await Tag();
                var audits=await db.AuditLogs.CountAsync(ct);
                if(scenario=="mandatory-fail")
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection}; UPDATE dbo.inspection_results SET outcome='FAIL' WHERE inspection_id={inspection}; UPDATE dbo.room_inspections SET workflow_status='PENDING_CONCLUSION' WHERE inspection_id={inspection}",ct);
                    tag=await Tag();
                }
                if(scenario=="permission-revoked") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}",ct);
                if(scenario=="session-revoked") await sessions.RevokeSessionAsync(login.Ticket,ct);
                if(scenario=="cleaner-conflict") await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({task},{actor},{actor},SYSUTCDATETIME(),'ACTIVE')",ct);
                if(scenario=="invalid-state")
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection}",ct);
                    tag=await Tag();
                }
                audits=await db.AuditLogs.CountAsync(ct);
                if(scenario=="execution-catalog")
                {
                    await db.Database.ExecuteSqlRawAsync("""
                        IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='SEVERITY' AND value_code='MINOR')
                            INSERT dbo.reference_values(group_code,value_code,display_name,is_active) VALUES('SEVERITY','MINOR','Synthetic minor',1);
                        IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='SEVERITY' AND value_code='MAJOR')
                            INSERT dbo.reference_values(group_code,value_code,display_name,is_active) VALUES('SEVERITY','MAJOR','Synthetic major',1);
                        UPDATE dbo.reference_values SET is_active=1 WHERE group_code='SEVERITY' AND value_code IN('MINOR','MAJOR');
                        """,ct);
                    async Task<HotelOps.Api.DTOs.Housekeeping.InspectionExecutionDto> ReadExecution()
                    {
                        var stateTask = (Task<RoomTypeCreationService.HousekeepingState>)typeof(RoomTypeCreationService)
                            .GetMethod("InspectionState",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(service,[inspection,ct])!;
                        var state = await stateTask;
                        var entity = await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==inspection,ct);
                        return await (Task<HotelOps.Api.DTOs.Housekeeping.InspectionExecutionDto>)typeof(RoomTypeCreationService)
                            .GetMethod("ExecutionDto",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(service,[entity,state,branch,ct])!;
                    }
                    var expectedCatalog=await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode=="SEVERITY"&&(r.ValueCode=="MINOR"||r.ValueCode=="MAJOR"))
                        .OrderBy(r=>r.SortOrder).ThenBy(r=>r.ReferenceId).Select(r=>new HotelOps.Api.DTOs.Housekeeping.InspectionSeverityDto(r.ReferenceId,r.ValueCode,r.DisplayName)).ToArrayAsync(ct);
                    if(!(await ReadExecution()).Severities.SequenceEqual(expectedCatalog)) throw new Exception("Execution catalog did not preserve database IDs/labels/order");
                    await db.Database.ExecuteSqlRawAsync("UPDATE dbo.reference_values SET is_active=0 WHERE group_code='SEVERITY' AND value_code='MINOR'",ct);
                    var filtered=(await ReadExecution()).Severities;
                    if(filtered.Count!=1||filtered[0].Code!="MAJOR") throw new Exception("Inactive severity leaked into execution catalog");
                    if(await db.AuditLogs.CountAsync(ct)!=audits) throw new Exception("Catalog read wrote audit");
                }
                else if(scenario=="audit-failure")
                {
                    // Test-only trigger exists inside the same disposable transaction and is rolled back.
                    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_142A_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51994,'Synthetic audit fault',1; END",ct);
                    try { await Apply(tag,branch); throw new Exception("Audit fault was ignored"); }
                    catch(Microsoft.Data.SqlClient.SqlException error) when(error.Number==51994) { }
                }
                else if(scenario is "approve" or "duplicate")
                {
                    var result=await Apply(tag,branch);
                    var row=await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==inspection,ct);
                    var cleaning=await db.CleaningTasks.AsNoTracking().SingleAsync(t=>t.CleaningTaskId==task,ct);
                    if(row.Result!="PASS"||row.Status!="COMPLETED"||cleaning.Status!="COMPLETED"||await db.AuditLogs.CountAsync(ct)!=audits+1)
                        throw new Exception("Atomic inspection/task/audit mismatch");
                    if((bool)result.GetType().GetProperty("AuthoritativeCommitAccepted",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(result)!)
                        throw new Exception("Internal component claimed authoritative acceptance");
                    if(scenario=="duplicate") await Deny(()=>Apply(tag,branch),"Stale");
                    if(await db.AuditLogs.CountAsync(ct)!=audits+1) throw new Exception("Duplicate audit");
                    if(await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!="INSPECTION")
                        throw new Exception("Domain participant changed room projection");
                }
                else
                {
                    var expected=scenario switch {"mandatory-fail"=>"InvalidReference","stale"=>"Stale","session-revoked"=>"SessionInvalid","invalid-state"=>"Inactive",_=>"Forbidden"};
                    await Deny(()=>Apply(scenario=="stale"?"\"0000000000000000\"":tag,scenario=="wrong-branch"?branch+100000:branch),expected);
                    if(await db.AuditLogs.CountAsync(ct)!=audits || await db.RoomInspections.Where(i=>i.InspectionId==inspection).Select(i=>i.Result).SingleAsync(ct)!=null)
                        throw new Exception("Rejected operation mutated domain/audit");
                }
                if(!ReferenceEquals(db.Database.CurrentTransaction?.GetDbTransaction(),tx.GetDbTransaction())) throw new Exception("Caller transaction replaced");
            }
            catch(Exception error){failure=scenario+": "+error.GetType().Name+": "+error.Message;}
            finally { if(tx.GetDbTransaction().Connection!=null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            if(await db.Users.AnyAsync(ct)||await db.Branches.AnyAsync(ct)||await db.RoomInspections.AnyAsync(ct)||await db.UserSessions.AnyAsync(ct)||await db.AuditLogs.AnyAsync(ct))
                throw new Exception("Synthetic rollback cleanup failed");
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.triggers WHERE name='TR_DEV3_142A_AuditFault'").SingleAsync(ct)!=0)
                throw new Exception("Synthetic fault trigger survived rollback");
            checks.Add(new {scenario,success=failure==null,rollbackVerified=true});
            Console.WriteLine((failure==null?"PASS ":"FAIL ")+scenario);
            if(failure!=null)break;
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new {success=failure==null,checks,failure,
            scope="Real SQL credentials/session/permissions and internal approval domain participant; all writes rolled back; no public API or room/notification/replay acceptance"},new JsonSerializerOptions{WriteIndented=true}),ct);
        return failure==null?0:1;
    }
    private static async Task Deny(Func<Task<object>> run,string status)
    {
        try {await run();} catch(InvalidOperationException error) when(error.Message=="FE05_APPROVE_SOURCE_"+status){return;}
        throw new Exception("Expected source denial: "+status);
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;} }
}
