using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

if(args.Length==2&&args[0]=="fast017-unit"){await InspectionAssignmentPlanChecks.RunAsync(args[1]);return;}
if(args.Length == 2 && args[0] == "fast014-unit") { await InspectionFast014Checks.RunAsync(args[1]); return; }
#if ACCEPT010_REFERENCE
if(args.Length == 3 && args[0] == "accept010-compat") { await Fe05Accept010CompatibilityChecks.RunAsync(args[1],args[2]); return; }
#endif
if(args.Length == 1 && args[0] == "fast013-unit") { await InspectionFast013Checks.RunAsync(); return; }
if(args.Length == 2 && args[0] == "owner-14") { var complete=await InspectionOwnerFourteenDriver.RunAsync(args[1],new HotelOps.Api.Services.HotelStructure.InspectionClosedTrialExecution(),null,default); Environment.ExitCode=complete?0:2; return; }
if(args.Length == 1 && args[0] == "approve-reinspect-unit") { await ApproveReinspectionChecks.RunAsync(); return; }
if(args.Length == 1 && args[0] == "closure-unit") { await InspectionAuthorityClosureChecks.RunAsync(); return; }
if(args.Length == 1 && args[0] == "r05-unit") { InspectionR05IntegrationChecks.Unit(); return; }
if(args.Length == 1 && args[0] == "responsibility-unit") { InspectionResponsibilityChecks.Unit(); return; }
if(args.Length == 1 && args[0] == "decision08-unit") { await InspectionDecision08UnitChecks.RunAsync(); return; }
if(args.Length == 1 && args[0] == "composition-unit") { await InspectionCompositionChecks.RunAsync(); return; }
if(args.Length == 1 && args[0] == "issuer-unit") { InspectionIssuerUnitChecks.Run(); return; }
if(args.Length == 1 && args[0] == "authority-unit") { await InspectionAuthorityUnitChecks.RunAsync(); return; }
if (args.Length != 3 || args[0] is not ("serve" or "verify" or "probe" or "resume" or "sql" or "invalidate" or "sql-rollback" or "seed" or "preparation" or "authority" or "authority-concurrency" or "authority-selection" or "issuer" or "issuer-leastpriv" or "composition-sql" or "decision08-sql" or "decision08-hook-sql" or "responsibility-sql" or "r05-sql" or "closure-sql" or "approve-sql" or "reinspect-sql" or "fast013-sql" or "fast014-persist-sql" or "fast015-workflow-sql" or "fast016-fixed-sql" or "fast016-notfixed-sql" or "fast016-schema-sql" or "fast017-assignment-sql")) throw new ArgumentException("mode environment-receipt output-directory");
using var env = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
var e = env.RootElement; var name = e.GetProperty("database").GetString()!; var owner = e.GetProperty("owner").GetString()!;
if (!e.GetProperty("success").GetBoolean() || !e.GetProperty("createdByThisRun").GetBoolean()
    || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || e.GetProperty("server").GetString() != @"DESKTOP-JI8U50T\SQLEXPRESS") throw new InvalidOperationException("Isolation receipt rejected");
var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
var backend = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource=@"lpc:.\SQLEXPRESS", InitialCatalog=name, IntegratedSecurity=true, TrustServerCertificate=true, ConnectTimeout=5, ApplicationName="HotelOps FE05 FAST003 owned synthetic trial" }.ConnectionString;
var n01Guard = new HotelOps.Api.Services.Notifications.NotificationCommitGuard();
var contextOptions = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection);
if(args[0] is "composition-sql" or "decision08-sql" or "responsibility-sql" or "r05-sql" or "closure-sql" or "fast013-sql")contextOptions.AddInterceptors(n01Guard);
await using var db = new HotelOpsDbContext(contextOptions.Options);
await db.Database.OpenConnectionAsync();
async Task Guard()
{
    await using var q = db.Database.GetDbConnection().CreateCommand();
    q.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
    q.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')";
    await using var r=await q.ExecuteReaderAsync();
    if(!await r.ReadAsync() || r.GetString(0)!=@"DESKTOP-JI8U50T\SQLEXPRESS" || r.GetString(1)!=name || r.IsDBNull(2) || r.GetString(2)!=owner) throw new InvalidOperationException("SQL identity/ownership rejected");
}
async Task Save() { await Guard(); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
async Task Write(string file, object value) => await File.WriteAllTextAsync(Path.Combine(output,file),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
await Guard();
if(args[0]=="fast017-assignment-sql") { await InspectionAssignmentSqlChecks.RunAsync(db,args[1],output,Guard); return; }
if(args[0]=="fast016-schema-sql") { await ReinspectionConclusionSqlChecks.VerifyStorageGuardsAsync(db,output,Guard); return; }
if(args[0] is "fast016-fixed-sql" or "fast016-notfixed-sql") { await ReinspectionWorkflowSqlChecks.RunAsync(db,args[1],output,Guard,args[0]); return; }
if(args[0]=="fast015-workflow-sql") { await ReinspectionWorkflowSqlChecks.RunAsync(db,args[1],output,Guard); return; }
if(args[0]=="fast014-persist-sql") { await ApproveWriterSqlChecks.RunAsync(db,args[1],output,Guard,false,false,null,true); return; }
if(args[0]=="fast013-sql") { await ApproveWriterSqlChecks.RunAsync(db,args[1],output,Guard,false,true,n01Guard); return; }
if(args[0]=="reinspect-sql") { await ApproveWriterSqlChecks.RunAsync(db,args[1],output,Guard,true); return; }
if(args[0]=="approve-sql") { await ApproveWriterSqlChecks.RunAsync(db,args[1],output,Guard); return; }
if(args[0]=="closure-sql") { await InspectionEvidenceClosureSqlChecks.RunAsync(db,args[1],output,Guard,n01Guard); return; }
if(args[0]=="r05-sql") { await InspectionR05IntegrationChecks.SqlAsync(db,args[1],output,Guard,n01Guard); return; }
if(args[0]=="responsibility-sql") { await InspectionResponsibilityChecks.SqlAsync(db,args[1],output,Guard,n01Guard); return; }
if(args[0]=="probe") { Console.WriteLine("PASS owned database identity"); return; }
if(args[0]=="decision08-hook-sql") { await InspectionSharedHookSqlChecks.RunAsync(db,args[1],output,Guard); return; }
if(args[0]=="decision08-sql") { await InspectionDecision08SqlChecks.RunAsync(db,output,Guard,n01Guard); return; }
if(args[0]=="issuer-leastpriv") { await InspectionIssuerSqlChecks.RunAsync(db,args[1],output,Guard,"SQL execute-only caller can validate source without underlying table authority"); return; }
if(args[0]=="composition-sql") { await InspectionIssuerSqlChecks.RunAsync(db,args[1],output,Guard,"Real N01 reservation failure poisons guarded EF commit and rolls back SQL",n01Guard); return; }
if(args[0]=="issuer") { await InspectionIssuerSqlChecks.RunAsync(db,args[1],output,Guard); return; }
if(args[0]=="authority-selection") { await InspectionAuthoritySqlChecks.RunAsync(db,args[1],output,Guard,"Foreign persisted selected defect is rejected"); return; }
if(args[0]=="authority-concurrency") { await InspectionAuthoritySqlChecks.RunAsync(db,args[1],output,Guard,"Concurrent Reject/Rework contends on source locks and leaves no partial records"); return; }
if(args[0]=="authority") { await InspectionAuthoritySqlChecks.RunAsync(db,args[1],output,Guard); return; }
if(args[0]=="preparation") { await ConclusionPreparationChecks.RunAsync(db,args[1],output,Guard); return; }
var branch = await db.Branches.Where(b=>b.BranchCode=="BR001").Select(b=>b.BranchId).SingleAsync();
var foreign = await db.Branches.Where(b=>b.BranchCode=="BR002").Select(b=>b.BranchId).SingleAsync();
if(args[0]=="sql-rollback"){
 await Guard();var id=await db.RoomInspections.OrderBy(i=>i.InspectionId).Select(i=>i.InspectionId).FirstAsync();
 var row=await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==id);
 var task=await db.CleaningTasks.AsNoTracking().SingleAsync(t=>t.CleaningTaskId==row.CleaningTaskId);
 var originalCycle=await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={row.RoomId}").SingleAsync();
 var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);bool conflict=false;
 try{
  await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id={task.CleaningTaskId} WHERE room_id={row.RoomId}; UPDATE dbo.cleaning_tasks SET workflow_status='ON_HOLD' WHERE cleaning_task_id={task.CleaningTaskId}");
  var version=await db.InspectionChecklistVersions.AsNoTracking().SingleAsync(v=>v.VersionId==row.VersionId);
  var criteria=await db.InspectionChecklistItems.AsNoTracking().Where(c=>c.VersionId==row.VersionId).ToArrayAsync();
  var results=await db.InspectionResults.AsNoTracking().Where(r=>r.InspectionId==id).ToArrayAsync();
  var defects=await db.InspectionDefects.AsNoTracking().Where(d=>d.InspectionId==id).ToArrayAsync();
  var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={id}").SingleAsync();
  var source=new HotelOps.Api.Services.Housekeeping.RejectInspectionLoadedSource(new(row,task,version,criteria,results,defects,"PENDING_CONCLUSION",revision),[],task.CleaningTaskId);
  await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,source,new("Core-only injected task phase conflict",true,'"'+Convert.ToHexString(revision)+'"'),default);
 }catch(Microsoft.Data.SqlClient.SqlException ex)when(ex.Number==51990&&ex.Message.Contains("CLEANING_PHASE_CONFLICT")){conflict=true;}
 finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();await tx.DisposeAsync();db.ChangeTracker.Clear();}
 var after=await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==id);
 var phase=await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={task.CleaningTaskId}").SingleAsync();
 var current=await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={row.RoomId}").SingleAsync();
 if(!conflict||after.Result!=null||after.Status!="IN_PROGRESS"||phase!="PENDING_INSPECTION"||current!=originalCycle)throw new Exception("Partial Reject transition escaped caller rollback");
 await Write("sql-rollback-results.json",new{success=true,coreOnly=true,authoritativeHandoff=false,cases=new[]{new{name="Failure after root UPDATE rolls back Inspection, task and cycle together",result="PASS"}}});
 Console.WriteLine("PASS failure after root UPDATE rolls back Inspection/task/cycle together");return;
}

if(args[0]=="sql")
{
 var receipt=new List<object>();
 var previousFile=Path.Combine(output,"sql-initial-failure.json");
 using var previous=File.Exists(previousFile)?JsonDocument.Parse(await File.ReadAllTextAsync(previousFile)):null;
 var alreadyPassed=previous?.RootElement.GetProperty("cases").EnumerateArray().Where(x=>x.GetProperty("result").GetString()=="PASS").Select(x=>x.GetProperty("name").GetString()!).ToHashSet()??[];
 async Task Check(string title,Func<Task> run){if(alreadyPassed.Contains(title)){receipt.Add(new{name=title,result="NOT_RERUN",priorReceipt="sql-initial-failure.json"});Console.WriteLine("NOT_RERUN "+title);return;}try{await run();receipt.Add(new{name=title,result="PASS"});Console.WriteLine("PASS "+title);}catch(Exception error){receipt.Add(new{name=title,result="FAIL",error=error.Message[..Math.Min(error.Message.Length,300)]});throw;}}
 var ids=await db.RoomInspections.OrderBy(x=>x.InspectionId).Select(x=>x.InspectionId).ToArrayAsync();
 async Task<HotelOps.Api.Services.Housekeeping.RejectInspectionLoadedSource> Load(long id){
  var row=await db.RoomInspections.AsNoTracking().SingleAsync(x=>x.InspectionId==id);
  var task=await db.CleaningTasks.AsNoTracking().SingleAsync(x=>x.CleaningTaskId==row.CleaningTaskId);
  var version=await db.InspectionChecklistVersions.AsNoTracking().SingleAsync(x=>x.VersionId==row.VersionId);
  var criteria=await db.InspectionChecklistItems.AsNoTracking().Where(x=>x.VersionId==row.VersionId).ToArrayAsync();
  var results=await db.InspectionResults.AsNoTracking().Where(x=>x.InspectionId==id).ToArrayAsync();
  var defects=await db.InspectionDefects.AsNoTracking().Where(x=>x.InspectionId==id).ToArrayAsync();
  var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={id}").SingleAsync();
  var state=await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.room_inspections WHERE inspection_id={id}").SingleAsync();
  var cycle=await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={row.RoomId}").SingleAsync();
  return new(new(row,task,version,criteria,results,defects,state,revision),[],cycle);
 }
 async Task InTransaction(Func<Task> run){await Guard();await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);try{await run();}finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();db.ChangeTracker.Clear();}}
 HotelOps.Api.Services.Housekeeping.RejectInspectionInput Input(HotelOps.Api.Services.Housekeeping.RejectInspectionLoadedSource source)=>new("Synthetic core rejection",true,'"'+Convert.ToHexString(source.Domain.Revision)+'"');
 async Task ExpectFailure(Func<Task> run){try{await run();}catch(Exception ex)when(ex is InvalidOperationException or Microsoft.Data.SqlClient.SqlException){return;}throw new Exception("Unexpected acceptance");}
 try{
 await Check("Caller transaction required",()=>ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,Load(ids[0]).GetAwaiter().GetResult(),new("Reason",true,"\"0000000000000001\""),default)));
 await Check("Valid FAILED and REWORK_REQUIRED transition; caller rollback restores both",()=>InTransaction(async()=>{var s=await Load(ids[0]);await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default);if((await db.RoomInspections.AsNoTracking().SingleAsync(x=>x.InspectionId==ids[0])).Result!="FAIL"||await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={s.CurrentCleaningTaskId}").SingleAsync()!="REWORK_REQUIRED")throw new Exception("Transition missing");}));
 await Check("Rollback restored original pending conclusion",async()=>{if((await Load(ids[0])).Domain.WorkflowState!="PENDING_CONCLUSION")throw new Exception("Rollback failed");});
 await Check("Missing confirmation rejected before writes",()=>InTransaction(async()=>{var s=await Load(ids[0]);await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s) with{Confirmed=false},default));}));
 await Check("Missing failed-criterion defect rejected",()=>InTransaction(async()=>{var s=await Load(ids[0]);await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s with{Domain=s.Domain with{Defects=[]}},Input(s),default));}));
 await Check("Wrong current cycle rejected",()=>InTransaction(async()=>{var s=await Load(ids[0]);await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s with{CurrentCleaningTaskId=999999},Input(s),default));}));
 await Check("Stale root revision rejected by conditional SQL",()=>InTransaction(async()=>{var s=await Load(ids[0]);await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note=summary_note WHERE inspection_id={ids[0]}");await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default));}));
 await Check("D4 evidence metadata invalidates pending conclusion revision",()=>InTransaction(async()=>{var s=await Load(ids[0]);await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name=original_file_name WHERE entity_type='INSPECTION' AND entity_id={ids[0]}");var after=await Load(ids[0]);if(s.Domain.Revision.SequenceEqual(after.Domain.Revision))throw new Exception("Evidence invalidation missing");await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default));}));
 await Check("Rework persisted identity and selected defect links inside caller transaction",()=>InTransaction(async()=>{var s=await Load(ids[0]);await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default);var failed=await Load(ids[0]);var rework=await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.CreateReworkAsync(db,branch,ids[0],failed.Domain.Revision,failed.Domain.Inspection.InspectorUserId,DateTime.UtcNow.AddHours(2),"Core-only rework fixture",failed.Domain.Defects.Select(x=>x.DefectId).ToArray(),default);var consumer=new HotelOps.Api.Services.HotelStructure.RoomTypeCreationService(db,null!,null!,null!,TimeProvider.System);if((await consumer.ReworkSourceDigestAsync(rework,default)).Length!=32)throw new Exception("FE04 digest read failed");if(rework<=0||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={rework}").SingleAsync()!=1)throw new Exception("Binding missing");await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.CreateReworkAsync(db,branch,ids[0],failed.Domain.Revision,failed.Domain.Inspection.InspectorUserId,DateTime.UtcNow.AddHours(2),"Duplicate",failed.Domain.Defects.Select(x=>x.DefectId).ToArray(),default));}));
 await Check("Rework rollback leaves no durable identity or links",async()=>{if(await db.CleaningReworks.AnyAsync()||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects").SingleAsync()!=0)throw new Exception("Rollback failed");});
 await Check("Cross-inspection selected defects rejected",()=>InTransaction(async()=>{var s=await Load(ids[0]);await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default);var f=await Load(ids[0]);var wrong=await Load(ids[1]);await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.CreateReworkAsync(db,branch,ids[0],f.Domain.Revision,f.Domain.Inspection.InspectorUserId,DateTime.UtcNow.AddHours(2),"Wrong defect",wrong.Domain.Defects.Select(x=>x.DefectId).ToArray(),default));}));
 await Check("Evidence change invalidates FAILED revision before rework",()=>InTransaction(async()=>{var s=await Load(ids[0]);await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default);var f=await Load(ids[0]);await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name=original_file_name WHERE entity_type='INSPECTION' AND entity_id={ids[0]}");await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.CreateReworkAsync(db,branch,ids[0],f.Domain.Revision,f.Domain.Inspection.InspectorUserId,DateTime.UtcNow.AddHours(2),"Stale",f.Domain.Defects.Select(x=>x.DefectId).ToArray(),default));}));
 await Check("Wrong branch cannot create core rework",()=>InTransaction(async()=>{var s=await Load(ids[0]);await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(db,s,Input(s),default);var f=await Load(ids[0]);await ExpectFailure(()=>HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.CreateReworkAsync(db,foreign,ids[0],f.Domain.Revision,f.Domain.Inspection.InspectorUserId,DateTime.UtcNow.AddHours(2),"Wrong branch",f.Domain.Defects.Select(x=>x.DefectId).ToArray(),default));}));
 await Check("Concurrent conditional Reject has one SQL core winner",async()=>{
  var snapshot=await Load(ids[1]);var command=Input(snapshot);var began=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  async Task<bool> Attempt(bool first){await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);await using var tx=await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);try{if(!first)await began.Task;await HotelOps.Api.Services.Housekeeping.InspectionConclusionSql.RejectAsync(context,snapshot,command,default);if(first){began.TrySetResult();await Task.Delay(150);}await tx.CommitAsync();return true;}catch(Microsoft.Data.SqlClient.SqlException ex)when(ex.Number==51990){await tx.RollbackAsync();return false;}finally{if(first)began.TrySetResult();}}
  var one=Attempt(true);var two=Attempt(false);var wins=await Task.WhenAll(one,two);if(wins.Count(x=>x)!=1)throw new Exception("Conditional write conflict not enforced");
 });
 await Check("Committed SQL-core test fixture has no production authority proof",async()=>{if(await db.CleaningReworks.AnyAsync())throw new Exception("Unexpected rework commit");if((await Load(ids[0])).Domain.WorkflowState!="PENDING_CONCLUSION"||(await Load(ids[1])).Domain.WorkflowState!="FAILED")throw new Exception("Unexpected fixture phases");});
 await Write("sql-results.json",new{success=true,coreOnly=true,authoritativeHandoff=false,database=name,owner,cases=receipt});
 }catch{await Write("sql-results.json",new{success=false,coreOnly=true,authoritativeHandoff=false,cases=receipt});throw;}
 return;
}
if(args[0]=="invalidate"){
 await Guard();var id=await db.RoomInspections.OrderBy(x=>x.InspectionId).Select(x=>x.InspectionId).FirstAsync();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name=original_file_name WHERE entity_type='INSPECTION' AND entity_id={id}");Console.WriteLine("PASS owned evidence invalidation fixture mutation");return;
}

if(args[0]=="verify"){
 await Guard();var states=await db.Database.SqlQueryRaw<string>("SELECT workflow_status AS Value FROM dbo.room_inspections ORDER BY inspection_id").ToArrayAsync();
 var reworks=await db.CleaningReworks.CountAsync();var audits=await db.AuditLogs.CountAsync(x=>x.EntityType=="room_inspections");
 if(states.Length!=2||states[0]!="PENDING_CONCLUSION"||states[1]!="FAILED"||reworks!=0||audits!=0)throw new InvalidOperationException("Public fail-closed behavior violated");
 await Write("persistence.json",new{success=true,database=name,owner,states,reworks,audits,secondInspectionIsCoreOnlyFixture=true,authoritativeHandoff=false});Console.WriteLine("PASS API mutations remain fail-closed; no rework or conclusion audit committed");return;
}string password; long typeId;
if(args[0]=="resume")
{
    using var prior=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"runtime.private.json")));
    password=prior.RootElement.GetProperty("password").GetString()!;
    typeId=prior.RootElement.GetProperty("typeId").GetInt64();
    if(await new StartupSchemaValidator(db).ValidateAsync(default)!=SchemaCompatibility.Compatible || await ChecklistSchema.StateAsync(db,default)!=1
        || !await db.Users.AnyAsync(u=>u.Username=="int.hk") || !await db.BranchRoomTypes.AnyAsync(t=>t.RoomTypeId==typeId && t.BranchId==branch)) throw new InvalidOperationException("Resume readiness rejected");
}
else
{
if(await db.Users.AnyAsync() || await db.RoomTypes.AnyAsync()) throw new InvalidOperationException("Synthetic fixture setup requires empty personnel/types; no reuse/reset");
if(await new StartupSchemaValidator(db).ValidateAsync(default)!=SchemaCompatibility.Compatible || await ChecklistSchema.StateAsync(db,default)!=1 || await RoomTypeSchema.ReadAsync(db,default)!=RoomTypeContract.BranchV1) throw new InvalidOperationException("Schema readiness failed");
password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"aA1-";
var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
var users=new List<User>();
foreach(var login in new[]{"int.hk","int.denied"})
{
    var user=new User{EmployeeCode=login,Username=login,Email=login+"@example.invalid",FullName="Synthetic "+login,PrimaryBranchId=branch,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
    user.PasswordHash=hasher.HashPassword(user,password);users.Add(user);db.Add(user);
}
await Save();
var role=await db.Roles.SingleAsync(r=>r.RoleCode=="HK_SUPERVISOR");
foreach(var code in new[]{"INSPECTION_PERFORM","INSPECTION_VIEW"})
 if(!await db.RolePermissions.AnyAsync(p=>p.RoleId==role.RoleId && p.Permission.PermissionCode==code))throw new InvalidOperationException("Missing existing role grant: "+code);db.Add(new UserRole{UserId=users[0].UserId,RoleId=role.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddHours(-1),IsActive=true});
db.Add(new UserBranchAccess{UserId=users[0].UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddHours(-1)});
await Save();
// Existing UC017 contract requires a published generic bootstrap checklist, never NULL.
var legacy=new InspectionChecklist{ChecklistCode="INT_LEGACY",ChecklistName="Synthetic generic bootstrap",IsActive=true};
var legacyVersion=new InspectionChecklistVersion{Checklist=legacy,VersionNo=1,Status="PUBLISHED"};
db.Add(new InspectionChecklistItem{Version=legacyVersion,Criterion="Synthetic bootstrap",IsRequired=true,IsActive=true});
await Save();await Guard();
await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(branch_id,type_code,type_name,standard_capacity,is_active,standard_cleaning_minutes,default_checklist_id) VALUES({branch},{"INT_TYPE"},{"Synthetic room type"},2,1,30,{legacy.ChecklistId})");
typeId=await db.BranchRoomTypes.Where(t=>t.BranchId==branch).Select(t=>t.RoomTypeId).SingleAsync();
var floor=new Floor{BranchId=branch,FloorCode="FAST",FloorName="Synthetic execution",IsActive=true};db.Add(floor);await Save();
var cleaningType=await db.ReferenceValues.Where(x=>x.GroupCode=="CLEANING_TYPE"&&x.IsActive).Select(x=>x.ReferenceId).FirstAsync();
foreach(var n in new[]{1,2}){
 var room=new Room{BranchId=branch,FloorId=floor.FloorId,RoomTypeId=typeId,RoomNumber="FAST"+n,OperationalStatus="AVAILABLE",IsActive=true};db.Add(room);await Save();
 var task=new CleaningTask{TaskNo="FAST_TASK_"+n,RoomId=room.RoomId,CleaningTypeId=cleaningType,Status="IN_PROGRESS",CreatedBy=users[0].UserId,CreatedAt=DateTime.UtcNow};db.Add(task);await Save();
 await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"PENDING_INSPECTION"} WHERE cleaning_task_id={task.CleaningTaskId}");
 var insp=new RoomInspection{InspectionNo="FAST_INSP_"+n,RoomId=room.RoomId,CleaningTaskId=task.CleaningTaskId,VersionId=legacyVersion.VersionId,InspectorUserId=users[0].UserId,Status="PENDING",CreatedAt=DateTime.UtcNow};db.Add(insp);await Save();
 await Guard(); await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"INSPECTING"},status={"IN_PROGRESS"} WHERE inspection_id={insp.InspectionId}");
 var itemId=await db.InspectionChecklistItems.Where(x=>x.VersionId==legacyVersion.VersionId).Select(x=>x.ItemId).SingleAsync();
 db.Add(new InspectionResult{InspectionId=insp.InspectionId,ChecklistItemId=itemId,Outcome="FAIL",InspectedAt=DateTime.UtcNow});await Save();
 var severity=await db.ReferenceValues.Where(x=>x.GroupCode=="SEVERITY"&&x.ValueCode=="MINOR").Select(x=>x.ReferenceId).SingleAsync();
 db.Add(new InspectionDefect{InspectionId=insp.InspectionId,ChecklistItemId=itemId,SeverityId=severity,Description="Synthetic failed criterion defect",Status="OPEN"});await Save();
 db.Add(new Attachment{EntityType="INSPECTION",EntityId=insp.InspectionId,FileName="fixture.pdf",OriginalFileName="fixture.pdf",MimeType="application/pdf",StorageKey="fixture.pdf",UploadedBy=users[0].UserId,UploadedAt=DateTime.UtcNow});await Save();
 await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"PENDING_CONCLUSION"} WHERE inspection_id={insp.InspectionId}");
 await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES({room.RoomId},{task.CleaningTaskId})");
}
}
if(args[0]=="seed") { await Write("runtime.private.json",new{password,typeId}); await Write("seed.json",new{success=true,syntheticOnly=true,database=name,owner}); Console.WriteLine("PASS fresh synthetic fixtures; no API launched"); return; }
var inspectionIds=await db.RoomInspections.OrderBy(x=>x.InspectionId).Select(x=>x.InspectionId).ToArrayAsync();
var severityId=await db.ReferenceValues.Where(x=>x.GroupCode=="SEVERITY"&&x.ValueCode=="MINOR"&&x.IsActive).Select(x=>x.ReferenceId).SingleAsync();
using var rsa=RSA.Create(2048);var certReq=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(System.Net.IPAddress.Loopback);certReq.CertificateExtensions.Add(san.Build());
using var cert=certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
var certPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));var pfx=Path.Combine(output,"runtime.private.pfx");
await File.WriteAllBytesAsync(pfx,cert.Export(X509ContentType.Pfx,certPassword));
await File.WriteAllTextAsync(Path.Combine(output,"runtime.private.cert.pem"),cert.ExportCertificatePem());
await File.WriteAllTextAsync(Path.Combine(output,"runtime.private.key.pem"),rsa.ExportPkcs8PrivateKeyPem());
await Write("runtime.private.json",new{branch,foreign,typeId,password,inspectionIds,severityId,api="https://127.0.0.1:7447",origin="https://127.0.0.1:7448"});
await Write("runtime.json",new{database=name,owner,branch,foreign,typeId,httpsApi="https://127.0.0.1:7447",httpsFrontend="https://127.0.0.1:7448",certificateThumbprint=cert.Thumbprint,syntheticGrant="Existing catalog INSPECTION_PERFORM/VIEW grants; no permission changes",masterDataGrantedToHK=false});
await Guard();
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(backend,"src/HotelOps.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(backend,"src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["DOTNET_ENVIRONMENT"]="Development";
start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:7447";start.Environment["ConnectionStrings__HotelOpsDb"]=connection;
start.Environment["Kestrel__Certificates__Default__Path"]=pfx;start.Environment["Kestrel__Certificates__Default__Password"]=certPassword;
start.Environment["Evidence__PrivateRoot"]=Path.Combine(output,"private-evidence");start.Environment["Logging__LogLevel__Default"]="None";
using var process=Process.Start(start)??throw new InvalidOperationException("API start failed");
var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
Console.WriteLine("READY: isolated fixtures + HTTPS API process started; no credentials printed");
try
{
    var deadline=DateTime.UtcNow.AddMinutes(15);
    while(!File.Exists(Path.Combine(output,"stop")) && DateTime.UtcNow<deadline && !process.HasExited) await Task.Delay(500);
    if(process.HasExited) { await Write("startup-failure.json",new{exitCode=process.ExitCode,log=await stdout+await stderr}); throw new InvalidOperationException("API exited; inspect local sanitized startup receipt"); }
}
finally{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();}
