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
internal static class InspectionAuthoritySqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db, string environment, string output, Func<Task> guard, string? only = null)
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
        var connection = db.Database.GetDbConnection();
        async Task<(long Rework, string Tag, long[] Defects)> Create(RoomTypeCreationService s, SessionReference ticket, long sid)
        {
            var input = new HotelOps.Api.Services.Housekeeping.RejectInspectionInput("FAST005 rollback-only", true, await Tag());
            var loaded = await s.PrepareRejectInCallerTransactionAsync(ticket, sid, new(branch, root.InspectionId, input), default);
            Status(loaded, RoomTypeBatchStatus.Success);
            await guard(); await InspectionConclusionSql.RejectAsync(db, loaded.Source!, input, default);
            var revision = await Tag(); var selected = loaded.Source!.Domain.Defects.Select(d => d.DefectId).ToArray();
            await guard(); var id = await InspectionConclusionSql.CreateReworkAsync(db, branch, root.InspectionId,
                Convert.FromHexString(revision.Trim('"')), other, DateTime.UtcNow.AddHours(2), "FAST005 core-only fixture", selected, default);
            return (id, revision, selected);
        }
        Task<PreparedInspectionReworkAuthority> Prepare(RoomTypeCreationService s, SessionReference ticket, long sid,
            (long Rework, string Tag, long[] Defects) created, Guid operation)
            => s.PrepareReworkAuthorityInCallerTransactionAsync(ticket,sid,connection,db.Database.CurrentTransaction!.GetDbTransaction(),
                branch,root.InspectionId,created.Rework,created.Tag,operation,created.Defects,output,default);
        async Task Fails(string expected, Func<Task> action)
        {
            try { await action(); } catch(InvalidOperationException e) when(e.Message==expected) { return; }
            throw new Exception("Expected fail-closed: "+expected);
        }
        try
        {
            await Check("Persisted Reject/Rework snapshot produces exact R01 receipt fields in caller transaction",false,async(s,t,sid)=>{
                var created=await Create(s,t,sid);var auditsInTx=await db.AuditLogs.CountAsync();var op=Guid.NewGuid();var prepared=await Prepare(s,t,sid,created,op);
                if(prepared.Receipt.ReworkId!=created.Rework || prepared.Receipt.InspectionETag!=created.Tag
                    || prepared.Receipt.Issuer!="FE05.UC045.UC046" || prepared.Receipt.IssuerVersion!=1
                    || prepared.Receipt.Origin!="FE05_COMMITTED_V1" || prepared.Receipt.OperationId!=op
                    || prepared.Receipt.ActorUserId!=actor || prepared.Receipt.WriterTransactionId!=await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS Value").SingleAsync())throw new Exception("Receipt mismatch");
                if(await db.AuditLogs.CountAsync()!=auditsInTx)throw new Exception("Unexpected audit issuance"); // login only; preparation never issues authority
            });
            await Check("R01 selected defect command must match persisted selection",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);
                await Fails("REWORK_AUTHORITY_SELECTION_MISMATCH",()=>Prepare(s,t,sid,(c.Rework,c.Tag,[long.MaxValue]),Guid.NewGuid()));
            });
            await Check("Foreign persisted selected defect is rejected",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);
                await Prepare(s,t,sid,c,Guid.NewGuid()); // positive control for the same caller
                var foreignDefect=await db.InspectionDefects.Where(d=>d.InspectionId!=root.InspectionId).Select(d=>d.DefectId).FirstAsync();
                await guard();var refused=false;
                try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_rework_defects SET defect_id={foreignDefect} WHERE rework_id={c.Rework}"); }
                catch(Microsoft.Data.SqlClient.SqlException error)when(error.Number==51731){refused=true;}
                if(!refused)throw new Exception("Existing cross-cycle ownership trigger did not reject foreign defect");
                // Do not disable the real trigger to fabricate a persisted cross-cycle graph.
            });
            await Check("D4 metadata change rejects stale authority command",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name='authority-change.pdf' WHERE entity_type='INSPECTION' AND entity_id={root.InspectionId}");
                await Fails("REWORK_AUTHORITY_SOURCE_Stale",()=>Prepare(s,t,sid,c,Guid.NewGuid()));
            });
            await Check("Wrong persisted cycle prevents authority preparation",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");
                await Fails("REWORK_AUTHORITY_SOURCE_Inactive",()=>Prepare(s,t,sid,c,Guid.NewGuid()));
            });
            await Check("Real revoked SQL session cannot prepare authority",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE session_id={sid}");
                await Fails("REWORK_AUTHORITY_SOURCE_SessionInvalid",()=>Prepare(s,t,sid,c,Guid.NewGuid()));
            });
            await Check("Real permission denied before authority facts",false,async(s,t,sid)=>{
                var tag=await Tag();await Fails("REWORK_AUTHORITY_SOURCE_Forbidden",()=>s.PrepareReworkAuthorityInCallerTransactionAsync(t,sid,connection,db.Database.CurrentTransaction!.GetDbTransaction(),branch,root.InspectionId,1,tag,Guid.NewGuid(),[1],output,default));
            },true);
            await Check("Caller rejects another physical connection",false,async(s,t,sid)=>{
                using var otherConnection = new Microsoft.Data.SqlClient.SqlConnection(connection.ConnectionString);
                await Fails("INSPECTION_AUTHORITY_WRONG_PHYSICAL_TRANSACTION",()=>s.PrepareReworkAuthorityInCallerTransactionAsync(t,sid,otherConnection,db.Database.CurrentTransaction!.GetDbTransaction(),branch,root.InspectionId,1,"\"0000000000000001\"",Guid.NewGuid(),[1],output,default));
            });
            await Check("Conclusion receipt and FE04 projection adapter remain fail-closed without schema/staging",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);var tx=db.Database.CurrentTransaction!.GetDbTransaction();
                var payload=await s.PrepareFailedConclusionReceiptInCallerTransactionAsync(t,sid,connection,tx,branch,root.InspectionId,c.Tag,default);
                if(payload.Contract!="FE05_INSPECTION_CONCLUSION_V1" || payload.Issuer!="FE05.UC044.UC045" || payload.Conclusion!="FAILED")throw new Exception("Projection receipt mismatch");
                await Fails("INSPECTION_PROJECTION_SCHEMA_REQUIRED",()=>s.ProjectFailedConclusionInCallerTransactionAsync(t,sid,connection,tx,branch,root.InspectionId,c.Tag,Guid.NewGuid(),default));
            });
            await Check("Selected linked evidence uses real file bytes; missing file rejected",false,async(s,t,sid)=>{
                var c=await Create(s,t,sid);var attachment=await db.Attachments.AsNoTracking().FirstAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==root.InspectionId);
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_defect_attachments(defect_id,attachment_id) VALUES({c.Defects[0]},{attachment.AttachmentId})");
                c=(c.Rework,await Tag(),c.Defects);
                await Fails("REWORK_AUTHORITY_EVIDENCE_UNAVAILABLE",()=>Prepare(s,t,sid,c,Guid.NewGuid()));
                var file=Path.Combine(output,"fixture.pdf");await File.WriteAllBytesAsync(file,"%PDF-1.4 Synthetic authority evidence"u8.ToArray());
                try {
                    var before=await Prepare(s,t,sid,c,Guid.NewGuid());
                    await File.WriteAllBytesAsync(file,"%PDF-1.4 Changed synthetic evidence"u8.ToArray());
                    var after=await Prepare(s,t,sid,c,Guid.NewGuid());
                    if(before.Receipt.EvidenceDigest==after.Receipt.EvidenceDigest || before.Receipt.SourceDigest==after.Receipt.SourceDigest)throw new Exception("File digest unchanged");
                } finally { File.Delete(file); } // only file created by this case in owned output
            });
            await Check("Concurrent Reject/Rework contends on source locks and leaves no partial records",false,async(s,t,sid)=>{
                var input=new RejectInspectionInput("Concurrent rollback-only",true,await Tag());
                var loaded=await s.PrepareRejectInCallerTransactionAsync(t,sid,new(branch,root.InspectionId,input),default);Status(loaded,RoomTypeBatchStatus.Success);
                await guard();await InspectionConclusionSql.RejectAsync(db,loaded.Source!,input,default);
                var revision=await Tag();
                async Task Contender(bool rework){
                    await using var second=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection.ConnectionString).Options);
                    using var expected=JsonDocument.Parse(await File.ReadAllTextAsync(environment));
                    var expectedIdentity=expected.RootElement;
                    var secondIdentity=await second.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),@@SERVERNAME)+N'|'+DB_NAME()+N'|'+(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner') AS Value").SingleAsync();
                    if(secondIdentity!=expectedIdentity.GetProperty("server").GetString()+"|"+expectedIdentity.GetProperty("database").GetString()+"|"+expectedIdentity.GetProperty("owner").GetString())throw new Exception("Contender isolation rejected");
                    await using var tx=await second.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    try{
                        await second.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 1000");
                        if(rework)await InspectionConclusionSql.CreateReworkAsync(second,branch,root.InspectionId,Convert.FromHexString(revision.Trim('"')),other,DateTime.UtcNow.AddHours(2),"Contender",loaded.Source!.Domain.Defects.Select(d=>d.DefectId).ToArray(),default);
                        else await InspectionConclusionSql.RejectAsync(second,loaded.Source!,input,default);
                        throw new Exception("Concurrent write bypassed lock");
                    }catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==1222){}
                    finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();}
                }
                await Contender(false);await Contender(true);
            });
            await File.WriteAllTextAsync(Path.Combine(output,only==null?"authority-results.json":only.StartsWith("Foreign")?"authority-selection-results.json":"authority-concurrency-results.json"),JsonSerializer.Serialize(new{success=true,sqlServer=true,realSessionValidation=true,allCasesExplicitRollback=true,realProofIssued=false,productionCommit=false,cases},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){await File.WriteAllTextAsync(Path.Combine(output,only==null?"authority-results.json":only.StartsWith("Foreign")?"authority-selection-results.json":"authority-concurrency-results.json"),JsonSerializer.Serialize(new{success=false,cases,error=error.GetType().Name+": "+error.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo current){current=user;return true;}}
}
