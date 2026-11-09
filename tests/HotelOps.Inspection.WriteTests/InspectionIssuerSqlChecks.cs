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
internal static class InspectionIssuerSqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db, string environment, string output, Func<Task> guard, string? only = null, HotelOps.Api.Services.Notifications.NotificationCommitGuard? commitGuard = null)
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
            if (only != null && name != only) return;
            if (name.StartsWith("Real N01") && commitGuard == null) return;
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
        var script=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"../../../../../scripts/database/fe05-fast007/01_TrustedInspectionIssuer.sql"));
        var batches=System.Text.RegularExpressions.Regex.Split(script,@"(?im)^\s*GO\s*\r?$");
        var existing=await db.Database.SqlQuery<string>($"SELECT definition AS Value FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe05_inspection_issuer_v1')").SingleOrDefaultAsync();
        if(existing==null){foreach(var batch in batches)if(!string.IsNullOrWhiteSpace(batch)){await guard();await db.Database.ExecuteSqlRawAsync(batch);}}
        else if(existing.Trim()!=batches[1].Trim())throw new Exception("Installed candidate definition changed; refusing overwrite");
        if(await db.Database.SqlQuery<int>($"SELECT execute_as_principal_id AS Value FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe05_inspection_issuer_v1')").SingleAsync()!=-2)throw new Exception("Module not EXECUTE AS OWNER");
        Console.WriteLine("PASS Candidate module created WITH EXECUTE AS OWNER; unregistered and ungranted");
        var operation=Guid.NewGuid();
        async Task LockScope(RoomTypeCreationService s,SessionReference t,long sid)
        {var load=await s.PrepareRejectInCallerTransactionAsync(t,sid,new(branch,root.InspectionId,new("SQL issuer fixture",true,await Tag())),default);Status(load,RoomTypeBatchStatus.Success);}
        Task Raw(SessionReference t,long sid,long scope,long id,string tag,bool? confirmed=true,string action="VALIDATE_REJECT",bool tamper=false)
        {
            var identity=InspectionIssuerOperation.Reject(operation,t.UserId,scope,id,tag,new("SQL issuer fixture",confirmed,tag));
            var canonical=identity.Canonical; var digest=identity.Fingerprint;
            if(tamper)canonical=canonical.Replace("|UC045|","|UC046|");
            var bit=new Microsoft.Data.SqlClient.SqlParameter("@confirmation",System.Data.SqlDbType.Bit){Value=(object?)confirmed??DBNull.Value};
            return db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe05_inspection_issuer_v1 @action={action},@actor={t.UserId},@session={sid},@session_hash={t.KeyHash},@session_created={t.CreatedAtUtc},@branch={scope},@inspection={id},@expected={Convert.FromHexString(tag.Trim('"'))},@operation={operation},@fingerprint={digest},@canonical={canonical},@reason={"SQL issuer fixture"},@confirmed={bit}");
        }
        async Task RejectSql(string message,Func<Task> run){try{await run();}catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==51996 && e.Message.Contains(message,StringComparison.Ordinal)){return;}throw new Exception("Expected SQL rejection: "+message);}
        try
        {
            await Check("SQL execute-only caller can validate source without underlying table authority",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await guard();
                await db.Database.ExecuteSqlRawAsync("IF DATABASE_PRINCIPAL_ID('fe05_fast007_execute_only') IS NOT NULL THROW 51996,'Refusing preexisting test principal',1; CREATE USER fe05_fast007_execute_only WITHOUT LOGIN; GRANT EXECUTE ON OBJECT::dbo.fe05_inspection_issuer_v1 TO fe05_fast007_execute_only;");
                await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='fe05_fast007_execute_only'");
                try {
                    var permissions=await db.Database.SqlQuery<int>($"SELECT CASE WHEN HAS_PERMS_BY_NAME('dbo.fe05_inspection_issuer_v1','OBJECT','EXECUTE')=1 AND ISNULL(HAS_PERMS_BY_NAME('dbo.room_inspections','OBJECT','UPDATE'),0)=0 AND ISNULL(HAS_PERMS_BY_NAME('dbo.room_inspections','OBJECT','SELECT'),0)=0 THEN 1 ELSE 0 END AS Value").SingleAsync();
                    if(permissions!=1)throw new Exception("Execute-only isolation failed");
                    await Raw(t,sid,branch,root.InspectionId,tag);
                } finally { await db.Database.ExecuteSqlRawAsync("REVERT"); }
            });
            await Check("Real N01 reservation failure poisons guarded EF commit and rolls back SQL",false,async(s,t,sid)=>{
                if(commitGuard==null)return; // This case runs only through the dedicated guarded SQL mode.
                await LockScope(s,t,sid);var tag=await Tag();
                var identity=new Identity(new(t.UserId,sid,false));
                var staging=new HotelOps.Api.Services.Notifications.Fe05OperationStaging(db,commitGuard,identity,
                    new HotelOps.Api.Services.Notifications.UnacceptedFe05Coordination(),
                    new HotelOps.Api.Services.Notifications.Fe05NotificationStaging(db,commitGuard,identity,
                        new HotelOps.Api.Services.Notifications.UnacceptedFe05Source(),new HotelOps.Api.Services.Notifications.UnacceptedFe05Recipients()),
                    new HotelOps.Api.Services.Notifications.Fe05Fs013CandidateStore(db,new InspectionClosedTrialExecution()));
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name='rollback-after-n01-boundary.pdf' WHERE entity_type='INSPECTION' AND entity_id={root.InspectionId}");
                var op=InspectionIssuerOperation.Reject(operation,t.UserId,branch,root.InspectionId,tag,new("SQL issuer fixture",true,tag));
                try { await s.ComposeRejectInspectionAsync(t,sid,branch,root.InspectionId,tag,operation,new("SQL issuer fixture",true),staging,commitGuard,default); throw new Exception("Unaccepted coordination bypassed"); }
                catch(HotelOps.Api.Services.Notifications.Fe05NotificationUnavailableException){}
                try { await commitGuard.VerifyBeforeCommitAsync(db,default); throw new Exception("Poisoned final gate accepted"); }
                catch(InvalidOperationException e)when(e.Message.Contains("roll back")){}
                try { await db.Database.CurrentTransaction!.CommitAsync(); throw new Exception("Poisoned EF commit reached provider"); }
                catch(InvalidOperationException e)when(e.Message.Contains("roll back")){}
            });
            await Check("SQL owner module validates real assigned Reject source",false,async(s,t,sid)=>{
                var input=new RejectInspectionInput("SQL issuer fixture",true,await Tag());var op=InspectionIssuerOperation.Reject(operation,actor,branch,root.InspectionId,input.ExpectedETag!,input);
                var result=await s.ExecuteTrustedRejectDomainInCallerTransactionAsync(t,sid,new(branch,root.InspectionId,input),op,default,validateOnly:true);
                if(result.Revision==null || '"'+Convert.ToHexString(result.Revision)+'"'!=input.ExpectedETag)throw new Exception("Trusted source mismatch");
            });
            await Check("SQL rejects changed canonical bytes before source mutation",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await RejectSql("FE05_CANONICAL_FINGERPRINT_INVALID",()=>Raw(t,sid,branch,root.InspectionId,tag,tamper:true));
            });
            await Check("SQL hashes non-ASCII reason identically to CSharp",false,async(s,t,sid)=>{
                var tag=await Tag();var text="Kiểm tra phòng 🔍";var op=InspectionIssuerOperation.Reject(operation,actor,branch,root.InspectionId,tag,new(text,true,tag));
                var result=await s.ExecuteTrustedRejectDomainInCallerTransactionAsync(t,sid,new(branch,root.InspectionId,new(text,true,tag)),op,default,validateOnly:true);
                if(result.Revision==null)throw new Exception("Unicode canonical source mismatch");
            });
            await Check("SQL canonical UC046 fingerprint and UTC ticks match CSharp",false,async(s,t,sid)=>{
                var tag=await Tag();var due=new DateTime(2027,1,2,3,4,5,DateTimeKind.Utc).AddTicks(1234567);
                var op=InspectionIssuerOperation.Rework(operation,actor,branch,root.InspectionId,tag,new(5,due,[9,8],"Reason"));
                var hash=await db.Database.SqlQuery<byte[]>($"SELECT HASHBYTES('SHA2_256',CONVERT(varchar(max),{op.Canonical})) AS Value").SingleAsync();
                var ticks=await db.Database.SqlQuery<long>($"SELECT DATEDIFF_BIG(day,CONVERT(datetime2(7),'00010101',112),CONVERT(date,{due}))*CONVERT(bigint,864000000000)+DATEDIFF_BIG(nanosecond,CONVERT(datetime2(7),CONVERT(date,{due})),CONVERT(datetime2(7),{due}))/100 AS Value").SingleAsync();
                if(!hash.SequenceEqual(op.Fingerprint)||ticks!=due.Ticks)throw new Exception("SQL/CSharp UC046 canonical mismatch");
            });
            await Check("Missing canonical N01 reservation blocks mutation without partial state",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await RejectSql("FE05_N01_RESERVED_OPERATION_REQUIRED",()=>Raw(t,sid,branch,root.InspectionId,tag,action:"REJECT"));
            });
            await Check("SQL module rejects revoked session",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE session_id={sid}");
                await RejectSql("FE05_SESSION_INVALID",()=>Raw(t,sid,branch,root.InspectionId,tag));
            });
            await Check("SQL branch permission isolation",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await RejectSql("FE05_PERMISSION_FORBIDDEN",()=>Raw(t,sid,foreign,root.InspectionId,tag));
            });
            await Check("SQL assigned inspector required",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={other} WHERE inspection_id={root.InspectionId}");
                var tag=await Tag();await RejectSql("FE05_ASSIGNED_INSPECTOR_REQUIRED",()=>Raw(t,sid,branch,root.InspectionId,tag));
            });
            await Check("SQL D4 stale evidence revision rejected",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name='issuer-change.pdf' WHERE entity_type='INSPECTION' AND entity_id={root.InspectionId}");
                await RejectSql("FE05_REVISION_STALE",()=>Raw(t,sid,branch,root.InspectionId,tag));
            });
            await Check("SQL current cycle binding rejected when missing",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);var tag=await Tag();await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");
                await RejectSql("FE05_CURRENT_CYCLE_REQUIRED",()=>Raw(t,sid,branch,root.InspectionId,tag));
            });
            await Check("SQL false Reject confirmation rejected",false,async(s,t,sid)=>{await LockScope(s,t,sid);var tag=await Tag();await RejectSql("FE05_REJECT_INPUT_INVALID",()=>Raw(t,sid,branch,root.InspectionId,tag,false));});
            await Check("SQL NULL Reject confirmation rejected",false,async(s,t,sid)=>{await LockScope(s,t,sid);var tag=await Tag();await RejectSql("FE05_REJECT_INPUT_INVALID",()=>Raw(t,sid,branch,root.InspectionId,tag,null));});
            await Check("SQL missing source rejected",false,async(s,t,sid)=>{await LockScope(s,t,sid);var tag=await Tag();await RejectSql("FE05_SOURCE_UNAVAILABLE",()=>Raw(t,sid,branch,long.MaxValue,tag));});
            await Check("SQL missing FAIL defect rejected",false,async(s,t,sid)=>{
                await LockScope(s,t,sid);await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.inspection_defects WHERE inspection_id={root.InspectionId}");
                var tag=await Tag();await RejectSql("FE05_CONCLUSION_RESULTS_INVALID",()=>Raw(t,sid,branch,root.InspectionId,tag));
            });
            await Check("SQL owner catalog lock required",false,async(s,t,sid)=>{var tag=await Tag();await RejectSql("FE05_OWNER_CATALOG_LOCK_REQUIRED",()=>Raw(t,sid,branch,root.InspectionId,tag));});
            await File.WriteAllTextAsync(Path.Combine(output,"issuer-results.json"),JsonSerializer.Serialize(new{success=true,moduleCreated=true,executeAsOwner=true,registeredIssuer=false,publicMutation=false,successfulDomainMutation=false,allCasesRollbackVerified=true,cases},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"issuer-results.json"),JsonSerializer.Serialize(new{success=false,cases,error=e.GetType().Name+": "+e.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo current){current=user;return true;}}
}
