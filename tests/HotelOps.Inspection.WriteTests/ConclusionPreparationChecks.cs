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
internal static class ConclusionPreparationChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db, string environment, string output, Func<Task> guard)
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
            if (await Snapshot() != original || await db.UserSessions.CountAsync() != sessionsBefore || await db.AuditLogs.CountAsync() != auditsBefore
                || await db.CleaningReworks.AnyAsync() || await db.InspectionDefects.CountAsync(d => d.InspectionId == root.InspectionId) != defectsBefore
                || !(await db.InspectionResults.AsNoTracking().Where(r => r.InspectionId == root.InspectionId).Select(r => r.Outcome).ToArrayAsync()).SequenceEqual(resultsBefore)
                || !(await db.Attachments.AsNoTracking().Where(a => a.EntityType == "INSPECTION" && a.EntityId == root.InspectionId).Select(a => a.OriginalFileName).ToArrayAsync()).SequenceEqual(attachmentsBefore))
                throw new Exception("Rollback persistence mismatch");
            cases.Add(new { name, result = "PASS", rollbackVerified = true }); Console.WriteLine("PASS " + name);
        }
        static void Status(RejectInspectionLoadResult value, RoomTypeBatchStatus expected) { if (value.Status != expected) throw new Exception($"Expected {expected}, got {value.Status}: {value.Blocker} {string.Join(',', value.Errors ?? [])}"); }
        try
        {
            await Check("UC044 real persisted PASS preflight retains assigned retired version", true, async (s,t,sid) => {
                await guard(); await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={root.VersionId}");
                var value = await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);
                Status(value,RoomTypeBatchStatus.Success); if(value.Source!.Domain.Version.Status!="RETIRED")throw new Exception("Version substituted");
            });
            await Check("UC044 mandatory FAIL prevents approval", false, async(s,t,sid) => Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.InvalidReference));
            await Check("UC044 D4 evidence update invalidates supplied revision",true,async(s,t,sid)=>{
                var old = await Tag(); await guard(); await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name='changed.pdf' WHERE entity_type='INSPECTION' AND entity_id={root.InspectionId}");
                if(old==await Tag())throw new Exception("D4 did not invalidate");
                Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,old,default),RoomTypeBatchStatus.Stale);
            });
            await Check("UC044 persisted current-cycle mismatch rejected",true,async(s,t,sid)=>{

                // Remove this cycle row rather than violate the trusted task/room FK.
                await guard(); await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");
                Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.Inactive);
            });
            await Check("UC044 revoked real SQL session rejected",true,async(s,t,sid)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE session_id={sid}");
                Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.SessionInvalid);
            });
            await Check("UC044 wrong assigned inspector rejected",true,async(s,t,sid)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={other} WHERE inspection_id={root.InspectionId}");
                Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.Forbidden);
            });
            await Check("UC044 branch isolation",true,async(s,t,sid)=>Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,foreign,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.Forbidden));
            await Check("UC044 real permission denied",true,async(s,t,sid)=>Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.Forbidden),true);
            await Check("UC044 persisted cleaner inspector conflict rejected",true,async(s,t,sid)=>{
                await guard();db.Add(new CleaningProgressLog{CleaningTaskId=root.CleaningTaskId!.Value,ProgressPercent=100,LoggedBy=actor,LoggedAt=DateTime.UtcNow});await db.SaveChangesAsync();db.ChangeTracker.Clear();
                Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.Forbidden);
            });
            await Check("UC044 incompatible cleaning phase rejected",true,async(s,t,sid)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='ON_HOLD' WHERE cleaning_task_id={root.CleaningTaskId}");
                Status(await s.PrepareApproveInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default),RoomTypeBatchStatus.Inactive);
            });
            await Check("UC045 preparation and SQL core share caller transaction; UC046 never claims authority",false,async(s,t,sid)=>{
                var input = new RejectInspectionInput("FAST004 rollback-only",true,await Tag());
                var loaded = await s.PrepareRejectInCallerTransactionAsync(t,sid,new(branch,root.InspectionId,input),default);Status(loaded,RoomTypeBatchStatus.Success);
                await guard();await InspectionConclusionSql.RejectAsync(db,loaded.Source!,input,default);
                var rework = await s.PrepareReworkInCallerTransactionAsync(t,sid,branch,root.InspectionId,await Tag(),default);
                if(rework.Status!=RoomTypeBatchStatus.Success || rework.Value is not HotelOps.Api.DTOs.Housekeeping.ReworkSourcePreview { AuthorityEstablished:false })throw new Exception("Preview promoted to authority");
                if(RoomTypeCreationService.RejectWriteDependencies.Length==0 || RoomTypeCreationService.ReworkWriteDependencies.Length==0)throw new Exception("Production gate opened");
            });
            await File.WriteAllTextAsync(Path.Combine(output,"preparation-results.json"),JsonSerializer.Serialize(new{success=true,sqlServer=true,realSessionValidation=true,physicalCallerTransaction=true,allCasesExplicitRollback=true,productionAuthority=false,cases},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(output,"preparation-results.json"),JsonSerializer.Serialize(new{success=false,cases,error=error.GetType().Name+": "+error.Message},new JsonSerializerOptions{WriteIndented=true}));throw;
        }
    }
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor { public bool TryGetCurrentUser(out CurrentUserInfo current){current=user;return true;} }
}
