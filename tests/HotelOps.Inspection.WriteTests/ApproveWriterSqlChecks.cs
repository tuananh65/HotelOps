using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Security.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

// Real SQL preflight integration. Every case owns and explicitly rolls back its
// SERIALIZABLE transaction, including credential/session writes and fixtures.
internal static class ApproveWriterSqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db, string environment, string output, Func<Task> guard, bool reinspectionOnly = false)
    {
        using var runtime = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!, "runtime.private.json")));
        var password = runtime.RootElement.GetProperty("password").GetString()!;
        var actor = await db.Users.Where(u => u.Username == "int.hk").Select(u => u.UserId).SingleAsync();
        var other = await db.Users.Where(u => u.Username == "int.denied").Select(u => u.UserId).SingleAsync();
        var root = await db.RoomInspections.AsNoTracking().OrderBy(i => i.InspectionId).FirstAsync();
        var branch = await db.Rooms.Where(r => r.RoomId == root.RoomId).Select(r => r.BranchId).SingleAsync();
        var foreign = await db.Branches.Where(b => b.BranchCode == "BR002").Select(b => b.BranchId).SingleAsync();
        var cases = new List<object>();
        async Task<string> Tag() => '"' + Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={root.InspectionId}").SingleAsync()) + '"';
        async Task<string> Snapshot() => await db.Database.SqlQuery<string>($"SELECT (SELECT i.*,t.workflow_status AS task_phase,c.cleaning_task_id AS cycle FROM dbo.room_inspections i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id WHERE i.inspection_id={root.InspectionId} FOR JSON PATH) AS Value").SingleAsync();
        var original = await Snapshot();
        var sessionsBefore = await db.UserSessions.CountAsync();
        var inspectionsBefore = await db.RoomInspections.CountAsync();
        var auditsBefore = await db.AuditLogs.CountAsync();
        var resultsBefore = await db.InspectionResults.AsNoTracking().Where(r => r.InspectionId == root.InspectionId).Select(r => r.Outcome).ToArrayAsync();
        var defectsBefore = await db.InspectionDefects.CountAsync(d => d.InspectionId == root.InspectionId);
        var attachmentsBefore = await db.Attachments.AsNoTracking().Where(a => a.EntityType == "INSPECTION" && a.EntityId == root.InspectionId).Select(a => a.OriginalFileName).ToArrayAsync();
        async Task Check(string name, bool passing, Func<RoomTypeCreationService, SessionReference, long, Task> run, bool denied = false)
        {
            await guard();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await guard();
                var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
                var credentials = new CredentialLockoutService(db, passwords, TimeProvider.System);
                var sqlSessions = new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
                var verified = await credentials.VerifyAsync(denied ? other : actor, password,
                    (u, _) => Task.FromResult(u.EmploymentStatus == "ACTIVE" && u.AccountStatus == "ACTIVE"));
                var login = await sqlSessions.CreateSessionAsync(verified);
                var current = new Identity(new(login.Session.UserId, login.Session.SessionId, false));
                var permission = new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
                var service = new RoomTypeCreationService(db, current, permission, sqlSessions, TimeProvider.System);
                if (passing)
                {
                    await guard();
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={root.InspectionId}; DELETE dbo.inspection_defects WHERE inspection_id={root.InspectionId}; UPDATE dbo.inspection_results SET outcome='PASS' WHERE inspection_id={root.InspectionId}; UPDATE dbo.room_inspections SET workflow_status='PENDING_CONCLUSION' WHERE inspection_id={root.InspectionId}");
                }
                await run(service, login.Ticket, login.Session.SessionId);
                if (!ReferenceEquals(db.Database.CurrentTransaction?.GetDbTransaction(), tx.GetDbTransaction())) throw new Exception("Caller transaction replaced");
            }
            finally
            {
                if (tx.GetDbTransaction().Connection != null) await tx.RollbackAsync();
                db.ChangeTracker.Clear();
            }
            if (await db.RoomInspections.CountAsync() != inspectionsBefore || await Snapshot() != original || await db.UserSessions.CountAsync() != sessionsBefore || await db.AuditLogs.CountAsync() != auditsBefore
                || await db.CleaningReworks.AnyAsync() || await db.InspectionDefects.CountAsync(d => d.InspectionId == root.InspectionId) != defectsBefore
                || !(await db.InspectionResults.AsNoTracking().Where(r => r.InspectionId == root.InspectionId).Select(r => r.Outcome).ToArrayAsync()).SequenceEqual(resultsBefore)
                || !(await db.Attachments.AsNoTracking().Where(a => a.EntityType == "INSPECTION" && a.EntityId == root.InspectionId).Select(a => a.OriginalFileName).ToArrayAsync()).SequenceEqual(attachmentsBefore))
                throw new Exception("Rollback persistence mismatch");
            cases.Add(new { name, result = "PASS", rollbackVerified = true }); Console.WriteLine("PASS " + name);
        }
        static async Task Deny(Func<Task> run, string expected) {
            try { await run(); } catch(InvalidOperationException e) when(e.Message==expected) { return; }
            throw new Exception("Expected rejection: "+expected);
        }
        try
        {
            if (!reinspectionOnly) {
            await Check("Approve PASSED/task COMPLETED/audit stays in caller transaction; rollback restores all",true,async(s,t,sid)=>{
                var audit=await db.AuditLogs.CountAsync();
                var result=await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);
                var row=await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==root.InspectionId);
                var task=await db.CleaningTasks.AsNoTracking().SingleAsync(i=>i.CleaningTaskId==root.CleaningTaskId);
                if(row.Result!="PASS"||row.Status!="COMPLETED"||task.Status!="COMPLETED"||audit+1!=await db.AuditLogs.CountAsync()
                    ||result.AuthoritativeCommitAccepted||result.Revision!=await Tag())throw new Exception("Approve atomic component result mismatch");
            });
            await Check("Second Approve with original revision rejected before another audit",true,async(s,t,sid)=>{
                var tag=await Tag();await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,tag,default);
                var audits=await db.AuditLogs.CountAsync();
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,tag,default);},"FE05_APPROVE_SOURCE_Stale");
                if(audits!=await db.AuditLogs.CountAsync())throw new Exception("Duplicate audit");
            });
            await Check("Mandatory FAIL rejected without domain writes",false,async(s,t,sid)=>
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);},"FE05_APPROVE_SOURCE_InvalidReference"));
            await Check("Stale revision rejected",true,async(s,t,sid)=>
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,"\"0000000000000001\"",default);},"FE05_APPROVE_SOURCE_Stale"));
            await Check("Wrong branch rejected",true,async(s,t,sid)=>
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,foreign,root.InspectionId,await Tag(),default);},"FE05_APPROVE_SOURCE_Forbidden"));
            await Check("Unauthorized grant rejected",true,async(s,t,sid)=>
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);},"FE05_APPROVE_SOURCE_Forbidden"),true);
            await Check("Retired assigned checklist retained by writer",true,async(s,t,sid)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={root.VersionId}");
                await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);
                if((await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==root.InspectionId)).VersionId!=root.VersionId)throw new Exception("Pinned version changed");
            });
            await Check("Changed current cycle rejected",true,async(s,t,sid)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);},"FE05_APPROVE_SOURCE_Inactive");
            });
            await Check("Revoked actual session rejected",true,async(s,t,sid)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE session_id={sid}");
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);},"FE05_APPROVE_SOURCE_SessionInvalid");
            });
            await Check("Cleaner inspector SOD rejected",true,async(s,t,sid)=>{
                await guard();db.Add(new CleaningProgressLog{CleaningTaskId=root.CleaningTaskId!.Value,ProgressPercent=100,LoggedBy=actor,LoggedAt=DateTime.UtcNow});await db.SaveChangesAsync();db.ChangeTracker.Clear();
                await Deny(async()=>{await s.ApproveDomainInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);},"FE05_APPROVE_SOURCE_Forbidden");
            });
            }
            if(reinspectionOnly && await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NOT NULL AND COL_LENGTH('dbo.cleaning_rework_defects','remediated_by') IS NOT NULL AND COL_LENGTH('dbo.cleaning_rework_defects','remediated_at') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync()!=1) {
                await Check("UC047 missing remediation schema fails closed before source loading or fixture mutation",false,async(s,t,sid)=>
                    await Deny(async()=>{await s.ReadReinspectionCandidateInCallerTransactionAsync(t,sid,branch,root.InspectionId,1,await Tag(),"\"0000000000000001\"",default);},"REINSPECTION_REMEDIATION_SCHEMA_REQUIRED"));
                await File.WriteAllTextAsync(Path.Combine(output,"reinspection-reader-results.json"),JsonSerializer.Serialize(new{success=true,componentOnly=true,positiveCases="NOT_RUN",blockedBy="REINSPECTION_REMEDIATION_SCHEMA_REQUIRED",allCasesExplicitRollback=true,cases},new JsonSerializerOptions{WriteIndented=true}));
                return;
            }
            if(reinspectionOnly) {
                async Task<(long Inspection,long Rework,string InspectionTag,string ReworkTag)> Fixture() {
                    await guard();
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',inspected_at=SYSUTCDATETIME() WHERE inspection_id={root.InspectionId}; UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={root.CleaningTaskId}");
                    var rework=(await db.Database.SqlQuery<long>($"""
                        INSERT dbo.cleaning_reworks(inspection_id,cleaning_task_id,assignee_user_id,reason,status,workflow_status,requested_at)
                        VALUES({root.InspectionId},{root.CleaningTaskId},{other},'FAST012 synthetic reader component','IN_PROGRESS','PENDING_REINSPECTION',SYSUTCDATETIME());
                        SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
                        """).ToArrayAsync()).Single();
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_rework_defects(rework_id,defect_id,remediation_note,remediated_by,remediated_at) SELECT {rework},defect_id,'Synthetic remediation',{other},SYSUTCDATETIME() FROM dbo.inspection_defects WHERE inspection_id={root.InspectionId}");
                    var number="F012-"+Guid.NewGuid().ToString("N");
                    var inspection=(await db.Database.SqlQuery<long>($"""
                        INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at)
                        VALUES({number},{root.RoomId},{root.CleaningTaskId},{root.VersionId},{actor},'PENDING','ASSIGNED',SYSUTCDATETIME());
                        SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
                        """).ToArrayAsync()).Single();
                    var ir=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync();
                    var wr=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync();
                    return (inspection,rework,'"'+Convert.ToHexString(ir)+'"','"'+Convert.ToHexString(wr)+'"');
                }
                await Check("UC047 real assigned source reads exact remediation but never claims persisted assignment authority",false,async(s,t,sid)=>{
                    var f=await Fixture();
                    var candidate=await s.ReadReinspectionCandidateInCallerTransactionAsync(t,sid,branch,f.Inspection,f.Rework,f.InspectionTag,f.ReworkTag,default);
                    if(candidate.AuthoritativeBindingAccepted||candidate.Defects.Count==0||candidate.OriginalInspectionId!=root.InspectionId||candidate.AssignedChecklistVersionId!=root.VersionId)throw new Exception("Candidate authority or source mismatch");
                });
                await Check("UC047 stale Rework source rejected",false,async(s,t,sid)=>{
                    var f=await Fixture();
                    await Deny(async()=>{await s.ReadReinspectionCandidateInCallerTransactionAsync(t,sid,branch,f.Inspection,f.Rework,f.InspectionTag,"\"0000000000000001\"",default);},"REINSPECTION_REWORK_STALE");
                });
                await Check("UC047 wrong branch denied before source disclosure",false,async(s,t,sid)=>{
                    var f=await Fixture();
                    await Deny(async()=>{await s.ReadReinspectionCandidateInCallerTransactionAsync(t,sid,foreign,f.Inspection,f.Rework,f.InspectionTag,f.ReworkTag,default);},"REINSPECTION_SOURCE_FORBIDDEN");
                });
                await Check("UC047 wrong current cycle rejected",false,async(s,t,sid)=>{
                    var f=await Fixture();await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");
                    await Deny(async()=>{await s.ReadReinspectionCandidateInCallerTransactionAsync(t,sid,branch,f.Inspection,f.Rework,f.InspectionTag,f.ReworkTag,default);},"REINSPECTION_CURRENT_SOURCE_REQUIRED");
                });
                await Check("UC047 incomplete remediation rejected",false,async(s,t,sid)=>{
                    var f=await Fixture();await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_rework_defects SET remediated_by=NULL,remediated_at=NULL WHERE rework_id={f.Rework}");
                    var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={f.Rework}").SingleAsync();
                    await Deny(async()=>{await s.ReadReinspectionCandidateInCallerTransactionAsync(t,sid,branch,f.Inspection,f.Rework,f.InspectionTag,'"'+Convert.ToHexString(revision)+'"',default);},"REINSPECTION_REMEDIATION_REQUIRED");
                });
            }
            await File.WriteAllTextAsync(Path.Combine(output,reinspectionOnly?"reinspection-reader-results.json":"approve-writer-results.json"),JsonSerializer.Serialize(new{success=true,sqlServer=true,realSessionValidation=true,physicalCallerTransaction=true,allCasesExplicitRollback=true,productionAuthority=false,cases},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(output,reinspectionOnly?"reinspection-reader-results.json":"approve-writer-results.json"),JsonSerializer.Serialize(new{success=false,cases,error=error.GetType().Name+": "+error.Message},new JsonSerializerOptions{WriteIndented=true}));throw;
        }
    }
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor { public bool TryGetCurrentUser(out CurrentUserInfo current){current=user;return true;} }
}
