using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

internal static class Program
{
 const string Target="HotelOps_Canonical_FE04_R05_20261110_Fresh";
 const string Connection="Data Source=tcp:DESKTOP-JI8U50T,1433;Initial Catalog="+Target+";Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=5;Pooling=False;Application Name=FE04-R05-controlled-fixture";
 static readonly CancellationTokenSource Budget=new(TimeSpan.FromMinutes(10));
 static CancellationToken Ct=>Budget.Token;
 static readonly List<object> Results=[];
 static int Failures;
 static readonly string SourceRoot=Environment.GetEnvironmentVariable("FE04_R05_SOURCE_ROOT")??Path.GetFullPath("obj/fe04-unblock-r01");
 static readonly string EvidenceRoot=Environment.GetEnvironmentVariable("FE04_R05_EVIDENCE_ROOT")??Path.GetFullPath("obj/fe04-r05");
 static readonly MethodInfo CaptureMethod=typeof(RoomTypeCreationService).GetMethod("CaptureCleanerAttribution",BindingFlags.NonPublic|BindingFlags.Instance)!;
 static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
 static void Save()=>File.WriteAllText(Path.Combine(EvidenceRoot,"sql-results.json"),JsonSerializer.Serialize(new{task="FE04-R05",target=Target,fixtureOnly=true,sharedWrites=0,results=Results},new JsonSerializerOptions{WriteIndented=true}));
 static async Task Test(string name,Func<Task> run){try{await run();Results.Add(new{name,status="PASS"});Console.WriteLine("PASS "+name);}catch(Exception e){Failures++;Results.Add(new{name,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}Save();}
 static RoomTypeCreationService Service(HotelOpsDbContext db)=>new(db,null!,null!,null!,TimeProvider.System);
 static async Task<HotelOpsDbContext> Db(bool limited=false,bool readOnly=false){var c=new SqlConnection(Connection);await c.OpenAsync(Ct);await Guard.Check(c,null,Ct);if(limited){await using var q=c.CreateCommand();q.CommandText=readOnly?"EXECUTE AS USER='fe04_r05_reader'":"EXECUTE AS USER='fe04_r05_completion'";await q.ExecuteNonQueryAsync(Ct);}return new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(c,contextOwnsConnection:true).AddInterceptors(new Guard()).Options);}
 static SqlParameter P(string n,object v)=>new(n,v);
 static Task Exec(HotelOpsDbContext d,string sql,params object[] p)=>d.Database.ExecuteSqlRawAsync(sql,p,Ct);
 static Task<long> Num(HotelOpsDbContext d,string sql)=>d.Database.SqlQueryRaw<long>(sql).SingleAsync(Ct);
 static async Task Capture(HotelOpsDbContext d,F f)=>await (Task)CaptureMethod.Invoke(Service(d),[f.TaskId,f.Assignment,f.Actor,f.Progress,Ct])!;
 static Task Direct(HotelOpsDbContext d,F f)=>Exec(d,"EXEC dbo.fe04_capture_cleaner_attribution @task,@assignment,@cleaner,@progress",P("@task",f.TaskId),P("@assignment",f.Assignment),P("@cleaner",f.Actor),P("@progress",f.Progress));
 static async Task<CleanerAttribution?> Read(F f,long? branch=null,long? inspection=null,bool limited=true){await using var d=await Db(limited,readOnly:limited);await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);return await Service(d).ReadInspectionCleanerAttributionAsync(d.Database.GetDbConnection(),tx.GetDbTransaction(),inspection??f.Inspection,branch??f.Branch,Ct);}
 static async Task<F> Fixture(){
  await using var d=await Db();var code="R05"+Guid.NewGuid().ToString("N")[..12];
  var c=(await d.Database.SqlQueryRaw<C>(await File.ReadAllTextAsync(Path.Combine(SourceRoot,"scripts/verification/FE04-R05/CleaningFixture.sql"),Ct),P("@fixtureCode",code)).ToArrayAsync(Ct)).Single();
  // Synthetic setup only. Completion itself is performed later in the tested caller transaction.
  await Exec(d,"UPDATE dbo.cleaning_tasks SET workflow_status='IN_PROGRESS' WHERE cleaning_task_id=@task",P("@task",c.TaskId));
  var room=await Num(d,$"SELECT room_id AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={c.TaskId}");
  var assignment=await Num(d,$"SELECT assignment_id AS Value FROM dbo.cleaning_assignments WHERE cleaning_task_id={c.TaskId}");
  return new(c.Actor,c.Branch,c.TaskId,room,assignment,0,0);
 }
 static async Task<F> Complete(HotelOpsDbContext d,F f){
  var p=(await d.Database.SqlQueryRaw<long>("""
   DECLARE @now datetime2(7)=SYSUTCDATETIME();
   UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_INSPECTION',actual_end_at=@now WHERE cleaning_task_id=@task;
   INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,logged_by,logged_at)
   OUTPUT INSERTED.progress_id AS Value VALUES(@task,100,'COMPLETED',@actor,@now);
   """,P("@task",f.TaskId),P("@actor",f.Actor)).ToArrayAsync(Ct)).Single();
  return f with{Progress=p};
 }
 static async Task<F> Inspection(F f){
  await using var d=await Db();
  var id=(await d.Database.SqlQueryRaw<long>("""
   DECLARE @checklist bigint=(SELECT rt.default_checklist_id FROM dbo.rooms r JOIN dbo.room_types rt ON rt.room_type_id=r.room_type_id WHERE r.room_id=@room),@v bigint;
   INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@checklist,1,'DRAFT'); SET @v=SCOPE_IDENTITY();
   INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,inspector_user_id,version_id,status,workflow_status,created_at)
   VALUES(CONCAT('R05-',NEWID()),@room,@task,@actor,@v,'IN_PROGRESS','INSPECTING',SYSUTCDATETIME());
   SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
   """,P("@room",f.Room),P("@task",f.TaskId),P("@actor",f.Actor)).ToArrayAsync(Ct)).Single();
  return f with{Inspection=id};
 }
 static async Task<F> Committed(bool limited=true){var f=await Fixture();await using(var d=await Db(limited)){await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);f=await Complete(d,f);await Capture(d,f);await tx.CommitAsync(Ct);}return await Inspection(f);}
 static async Task ExpectSql(Func<Task> run,params int[] errors){try{await run();throw new InvalidOperationException("Expected SQL rejection was absent");}catch(SqlException e){Check(errors.Contains(e.Number),"Unexpected SQL error "+e.Number+": "+e.Message);}}
 static async Task Main(){
  await Test("install exact R04 candidate and R03 substrate",async()=>{
   await using var d=await Db();
   Check(await Num(d,"SELECT COUNT_BIG(*) AS Value FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.R05.Source' AND CONVERT(nvarchar(40),value)='d37e99f42eaaf3089eb510c6298296a0f0743faa'")==1,"R03 pin missing");
   var candidate=await File.ReadAllTextAsync(Path.Combine(SourceRoot,"scripts/database/release/fe04-cleaner-r04/01_CleanerAttributionCandidate.sql"),Ct);
   if(await Num(d,"SELECT COUNT_BIG(*) AS Value FROM sys.tables WHERE object_id=OBJECT_ID('dbo.fe04_cleaner_attributions')")==0)
    await Exec(d,candidate);
   var expected=System.Text.RegularExpressions.Regex.Match(candidate,"EXEC\\(N'(CREATE PROCEDURE[\\s\\S]*?)'\\);").Groups[1].Value.Replace("''","'");
   var installed=await d.Database.SqlQueryRaw<string>("SELECT definition AS Value FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe04_capture_cleaner_attribution')").SingleAsync(Ct);
   Check(expected==installed,"Installed owner procedure differs from exact candidate");
   Check(await Num(d,"SELECT COUNT_BIG(*) AS Value FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.fe04_cleaner_attributions') AND is_disabled=0 AND is_not_trusted=0")==6,"Expected six trusted FKs");
   Check(await Num(d,"SELECT COUNT_BIG(*) AS Value FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe04_capture_cleaner_attribution') AND execute_as_principal_id=-2")==1,"Owner procedure missing");
   // Test-only database principal; no server login, db_owner, or schema ownership.
   await Exec(d,"IF USER_ID('fe04_r05_completion') IS NULL CREATE USER fe04_r05_completion WITHOUT LOGIN; IF USER_ID('fe04_r05_reader') IS NULL CREATE USER fe04_r05_reader WITHOUT LOGIN; REVOKE SELECT ON SCHEMA::dbo FROM fe04_r05_completion; GRANT UPDATE ON dbo.cleaning_tasks TO fe04_r05_completion; GRANT INSERT ON dbo.cleaning_progress_logs TO fe04_r05_completion; GRANT EXECUTE ON dbo.fe04_capture_cleaner_attribution TO fe04_r05_completion; GRANT VIEW DEFINITION ON OBJECT::dbo.fe04_capture_cleaner_attribution TO fe04_r05_reader;");
   foreach(var table in new[]{"fe04_cleaner_attributions","cleaning_tasks","cleaning_assignments","cleaning_progress_logs","rooms","room_cleaning_cycle_state","room_inspections","users","departments","user_branch_access","user_roles","roles","shift_assignments","work_shifts","shift_templates"})
    await Exec(d,$"GRANT SELECT ON dbo.{table} TO fe04_r05_completion; GRANT SELECT ON dbo.{table} TO fe04_r05_reader;");
  });
  await Test("least privileged direct DML denied and principal non-owner",async()=>{await using var d=await Db(true);Check(await Num(d,"SELECT CONVERT(bigint,IS_MEMBER('db_owner')) AS Value")==0,"db_owner");await ExpectSql(()=>Exec(d,"DELETE dbo.fe04_cleaner_attributions WHERE cleaning_task_id=-1"),229);await ExpectSql(()=>Exec(d,"UPDATE dbo.fe04_cleaner_attributions SET cleaner_user_id=1 WHERE cleaning_task_id=-1"),229);await ExpectSql(()=>Exec(d,"INSERT dbo.fe04_cleaner_attributions DEFAULT VALUES"),229);});
  await Test("FE05 reader principal cannot capture or mutate",async()=>{var f=await Fixture();await using var d=await Db(true,true);await ExpectSql(()=>Direct(d,f),229);await ExpectSql(()=>Exec(d,"UPDATE dbo.cleaning_tasks SET completion_note='forbidden' WHERE cleaning_task_id=-1"),229);});
  await Test("single cleaner actual capture participant and FE05-signature reader",async()=>{var f=await Committed();var a=await Read(f);Check(a?.CleanerUserId==f.Actor&&a.CleaningTaskId==f.TaskId&&a.AssignmentId==f.Assignment&&a.CompletionProgressId==f.Progress,"Exact binding missing");});
  foreach(var kind in new[]{"UPDATE","DELETE"})await Test("immutable trigger "+kind,async()=>{var f=await Committed();await using var d=await Db();await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);await ExpectSql(()=>Exec(d,kind=="UPDATE"?$"UPDATE dbo.fe04_cleaner_attributions SET cleaner_user_id={f.Actor} WHERE cleaning_task_id={f.TaskId}":$"DELETE dbo.fe04_cleaner_attributions WHERE cleaning_task_id={f.TaskId}"),51995);await tx.RollbackAsync(Ct);});
  await Test("FK rejects nonexistent source",async()=>{var f=await Fixture();await using var d=await Db();await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);await ExpectSql(()=>Exec(d,$"INSERT dbo.fe04_cleaner_attributions VALUES({f.TaskId},{f.Room},{f.Branch},{f.Assignment},{f.Actor},9223372036854775807,SYSUTCDATETIME(),0x0000000000000001,CURRENT_TRANSACTION_ID(),1)"),547);await tx.RollbackAsync(Ct);});
  await Test("reassignment retains original cleaner and completion revision",async()=>{var f=await Committed();var before=await Read(f);await using var d=await Db();await Exec(d,$"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='REASSIGNED' WHERE assignment_id={f.Assignment}; INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({f.TaskId},1,1,SYSUTCDATETIME(),'ACTIVE'); UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={f.TaskId}");var after=await Read(f);Check(after?.CleanerUserId==f.Actor&&after.AssignmentId==f.Assignment&&after.CompletionRevision.SequenceEqual(before!.CompletionRevision),"Original attribution replaced");});
  foreach(var history in new[]{"assignment","progress"})await Test("multiple "+history+" contributors fail closed",async()=>{var f=await Fixture();await using var d=await Db();await using(var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct)){f=await Complete(d,f);await Exec(d,history=="assignment"?$"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,ended_at,assignment_status) VALUES({f.TaskId},1,1,DATEADD(hour,-2,SYSUTCDATETIME()),DATEADD(hour,-1,SYSUTCDATETIME()),'REASSIGNED')":$"INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,logged_by,logged_at) VALUES({f.TaskId},50,'IN_PROGRESS',1,SYSUTCDATETIME())");await Capture(d,f);Check(await Num(d,$"SELECT COUNT_BIG(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={f.TaskId}")==0,"Ambiguous history was attributed");await tx.CommitAsync(Ct);}f=await Inspection(f);Check(await Read(f)==null,"Ambiguous reader accepted");});
  foreach(var mode in new[]{"branch","inspection","cycle","source","revoked","inactive"})await Test("reader rejects "+mode,async()=>{var f=await Committed();await using var d=await Db();if(mode=="cycle")await Exec(d,$"DELETE dbo.room_cleaning_cycle_state WHERE room_id={f.Room}");if(mode=="source")await Exec(d,$"UPDATE dbo.cleaning_progress_logs SET progress_percent=99 WHERE progress_id={f.Progress}");if(mode=="revoked")await Exec(d,$"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={f.Actor}");if(mode=="inactive")await Exec(d,$"UPDATE dbo.users SET deleted_at=SYSUTCDATETIME() WHERE user_id={f.Actor}");Check(await Read(f,mode=="branch"?long.MaxValue:null,mode=="inspection"?long.MaxValue:null)==null,"Invalid source accepted");});
  foreach(var fault in new[]{"audit","notification"})await Test("capture participant rollback on "+fault+" constraint fault",async()=>{var f=await Fixture();await using var d=await Db();await using(var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct)){f=await Complete(d,f);await Capture(d,f);Check(await Num(d,$"SELECT COUNT_BIG(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={f.TaskId}")==1,"Capture missing before fault");await ExpectSql(()=>Exec(d,fault=="audit"?"INSERT dbo.audit_logs DEFAULT VALUES":"INSERT dbo.notification_publications DEFAULT VALUES"),515,547);await tx.RollbackAsync(Ct);}Check(await Num(d,$"SELECT COUNT_BIG(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={f.TaskId}")==0,"Attribution survived rollback");Check(await Num(d,$"SELECT COUNT_BIG(*) AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={f.TaskId} AND actual_end_at IS NULL AND workflow_status='IN_PROGRESS'")==1,"Completion survived rollback");Check(!await d.CleaningProgressLogs.AnyAsync(p=>p.ProgressId==f.Progress,Ct),"Completion log survived rollback");});
  await Test("procedure requires caller transaction",async()=>{var f=await Fixture();await using var d=await Db(true);await ExpectSql(()=>Direct(d,f),51995);});
  await Test("capture refuses revoked cleaner eligibility",async()=>{var f=await Fixture();await using var d=await Db();await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);f=await Complete(d,f);await Exec(d,$"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={f.Actor}");try{await Capture(d,f);throw new InvalidOperationException("Revoked cleaner accepted");}catch(InvalidOperationException e) when(e.Message=="CLEANER_ATTRIBUTION_ELIGIBILITY_CHANGED"){}await tx.RollbackAsync(Ct);});
  await Test("missing completion progress denied by owner procedure",async()=>{var f=await Fixture();await using var d=await Db(true);await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);f=await Complete(d,f);await ExpectSql(()=>Direct(d,f with{Progress=long.MaxValue}),51995);await tx.RollbackAsync(Ct);});
  await Test("duplicate capture cannot overwrite committed original",async()=>{var f=await Committed();await using var d=await Db(true);await using var tx=await d.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);await ExpectSql(()=>Direct(d,f),51995);await tx.RollbackAsync(Ct);Check((await Read(f))?.CleanerUserId==f.Actor,"Original lost");});
  await Test("concurrent capture serializes and duplicate loses after commit",async()=>{var f=await Fixture();await using var a=await Db(true);await using var ta=await a.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);f=await Complete(a,f);await Direct(a,f);await using var b=await Db(true);await using var tb=await b.Database.BeginTransactionAsync(IsolationLevel.Serializable,Ct);var competing=Direct(b,f);await Task.Delay(200,Ct);Check(!competing.IsCompleted,"Concurrent capture was not serialized");await ta.CommitAsync(Ct);await ExpectSql(()=>competing,51995);await tb.RollbackAsync(Ct);});
  Results.Add(new{name="UC034 full public completion audit/notification/concurrent commands",status="NOT_RUN",reason="Exact FS015/FS016 database-name gates reject fresh R05. No gate relaxed; participant transaction tests are separate evidence."});
  Results.Add(new{name="actual FE05-owned authorization caller and HTTPS",status="NOT_RUN",reason="Real signature exercised by controlled backend caller; no FE05 source/authorization pipeline integration or HTTP acceptance claimed."});Save();
  Environment.ExitCode=Failures==0?0:1;
 }
 public sealed class C{public long Actor{get;set;}public long Branch{get;set;}public long TaskId{get;set;}public long ProgressId{get;set;}}
 public sealed record F(long Actor,long Branch,long TaskId,long Room,long Assignment,long Progress,long Inspection);
 sealed class Guard:DbCommandInterceptor{
  public static async Task Check(DbConnection c,DbTransaction? tx,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="""
   SELECT CASE WHEN DB_NAME()='HotelOps_Canonical_FE04_R05_20261110_Fresh'
    AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))='DESKTOP-JI8U50T'
    AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))='SQLEXPRESS'
    AND EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND owner_sid=0x01)
    AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.UnblockR01.Owner' AND CONVERT(nvarchar(128),value)='DEV1_DISPOSABLE')
    AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.R05.Source' AND CONVERT(nvarchar(40),value)='d37e99f42eaaf3089eb510c6298296a0f0743faa') THEN 1 ELSE 0 END
   """;if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("R05_TARGET_INSTANCE_OWNERSHIP_MISMATCH");}
  public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c,CommandEventData e,InterceptionResult<int> r,CancellationToken ct=default){await Check(c.Connection!,c.Transaction,ct);return r;}
  public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c,CommandEventData e,InterceptionResult<DbDataReader> r,CancellationToken ct=default){await Check(c.Connection!,c.Transaction,ct);return r;}
  public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c,CommandEventData e,InterceptionResult<object> r,CancellationToken ct=default){await Check(c.Connection!,c.Transaction,ct);return r;}
 }
}
