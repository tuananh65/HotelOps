using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class InspectionAssignmentSqlChecks
{
 private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo current){current=user;return true;}}
 internal static async Task RunAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard)
 {
  using var secret=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!,"runtime.private.json")));
  var actor=await db.Users.Where(u=>u.Username=="int.hk").Select(u=>u.UserId).SingleAsync();var other=await db.Users.Where(u=>u.Username=="int.denied").Select(u=>u.UserId).SingleAsync();
  var roots=await db.RoomInspections.AsNoTracking().OrderBy(i=>i.InspectionId).ToArrayAsync();var task=roots[0].CleaningTaskId!.Value;var second=roots[1].CleaningTaskId!.Value;
  var branch=await db.Rooms.Where(r=>r.RoomId==roots[0].RoomId).Select(r=>r.BranchId).SingleAsync();var foreign=await db.Branches.Where(b=>b.BranchCode=="BR002").Select(b=>b.BranchId).SingleAsync();
  await guard();await db.Database.ExecuteSqlRawAsync("IF OBJECT_ID('dbo.TR_fe05_test_assignment_audit','TR') IS NOT NULL DROP TRIGGER dbo.TR_fe05_test_assignment_audit;");
  CreatedSqlSession login;
  await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
   var pw=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
   var verified=await new CredentialLockoutService(db,pw,TimeProvider.System).VerifyAsync(actor,secret.RootElement.GetProperty("password").GetString()!,(u,_)=>Task.FromResult(u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"));
   login=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(verified);
   await guard();await db.Database.ExecuteSqlRawAsync("UPDATE dbo.room_inspections SET status='CANCELLED',workflow_status='CANCELLED';");await tx.CommitAsync();db.ChangeTracker.Clear();
  }
  RoomTypeCreationService Service(HotelOpsDbContext context){var identity=new Identity(new(actor,login.Session.SessionId,false));return new(context,identity,new PermissionService(identity,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);}
  var cases=new List<object>();void Check(string name,bool ok){if(!ok)throw new Exception(name);cases.Add(new{name,result="PASS"});Console.WriteLine("PASS "+name);}
  var options=(InspectionAssignmentOptions)(await Service(db).ReadInspectionAssignmentOptionsAsync(login.Ticket,login.Session.SessionId,branch,task,default)).Value!;
  InspectionAssignmentExpectation Expect(InspectionAssignmentOptions s)=>new(s.TaskRevision,s.CycleRevision,s.ChecklistVersionId);
  AssignInspectionRequest Command(long inspector=0)=>new(inspector==0?actor:inspector,DateTime.UtcNow.AddHours(2),"Explicit synthetic assignment");
  async Task<SupervisorBindingUsers> Admit(HotelOpsDbContext context,long inspector){var actual=await new UserEligibilityLock(context).LockAsync(new[]{actor,inspector}.Distinct(),default);var retained=SupervisorBindingUsers.Retain(context,actual,actor,inspector,actor);await context.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction'; SELECT @r AS Value").ToArrayAsync();return retained;}
  async Task<string> Snapshot()=>await db.Database.SqlQuery<string>($"SELECT (SELECT (SELECT COUNT(*) FROM dbo.room_inspections) AS roots,(SELECT COUNT(*) FROM dbo.inspection_supervisor_bindings) AS bindings,(SELECT COUNT(*) FROM dbo.audit_logs) AS audits FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS Value").SingleAsync();
  async Task Negative(string name,RoomTypeBatchStatus status,Func<Task>? mutate=null,long target=0,long? targetBranch=null,InspectionAssignmentExpectation? expected=null){
   var before=await Snapshot();await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
    var inspector=target==0?actor:target;var retained=await Admit(db,inspector);if(mutate!=null){await guard();await mutate();}
    var result=await Service(db).AssignInspectionComponentInCallerAsync(login.Ticket,login.Session.SessionId,targetBranch??branch,task,Command(inspector),actor,retained,expected??Expect(options),default);
    Check(name,result.Status==status);await tx.RollbackAsync();db.ChangeTracker.Clear();
   }if(await Snapshot()!=before)throw new Exception("Negative case persisted residue");
  }
  Check("Existing eligibility supplies explicit self inspector/supervisor without inference",options.Inspectors.Any(p=>p.UserId==actor)&&options.ResponsibleSupervisors.Any(p=>p.UserId==actor)&&!options.Inspectors.Any(p=>p.UserId==other)&&!options.ProductionMutationAvailable);
  var queue=(InspectionAssignmentQueue)(await Service(db).ReadInspectionAssignmentQueueAsync(login.Ticket,login.Session.SessionId,branch,default)).Value!;
  Check("Branch queue contains pending current-cycle tasks",queue.Pending.Any(p=>p.TaskId==task)&&queue.Pending.Any(p=>p.TaskId==second));
  await Negative("Ineligible inspector is rejected",RoomTypeBatchStatus.InvalidReference,target:other);
  await Negative("Wrong branch has no assignment authority",RoomTypeBatchStatus.Forbidden,targetBranch:foreign);
  await Negative("Stale task revision is rejected",RoomTypeBatchStatus.Stale,expected:Expect(options) with{TaskRevision="\"0000000000000001\""});
  await Negative("Wrong current-cycle revision is rejected",RoomTypeBatchStatus.Stale,expected:Expect(options) with{CycleRevision="\"0000000000000001\""});
  await Negative("Changed checklist version is rejected",RoomTypeBatchStatus.Stale,expected:Expect(options) with{ChecklistVersionId=long.MaxValue});
  await Negative("Inspector who cleaned this cycle is rejected",RoomTypeBatchStatus.InvalidReference,async()=>{db.Add(new HotelOps.Api.Entities.CleaningProgressLog{CleaningTaskId=task,LoggedBy=actor,LoggedAt=DateTime.UtcNow,ProgressPercent=100});await db.SaveChangesAsync();db.ChangeTracker.Clear();});
  await Negative("Missing exact current cycle fails closed",RoomTypeBatchStatus.NotFound,async()=>{await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={roots[0].RoomId}");});
  await Negative("Missing Published checklist fails closed",RoomTypeBatchStatus.InvalidReference,async()=>{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={options.ChecklistVersionId}");});
  await Negative("Revoked existing permission grant fails closed",RoomTypeBatchStatus.Forbidden,async()=>{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}");});
  await Negative("Revoked real SQL session fails closed",RoomTypeBatchStatus.SessionInvalid,async()=>{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_sessions SET revoked_at=SYSUTCDATETIME() WHERE session_id={login.Session.SessionId}");});
  var beforeFault=await Snapshot();var auditBeforeFault=await db.AuditLogs.CountAsync();bool failed=false,domainWritten=false;
  await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
   try{var retained=await Admit(db,actor);var written=await Service(db).AssignInspectionComponentInCallerAsync(login.Ticket,login.Session.SessionId,branch,task,Command(),actor,retained,Expect(options),default);
       domainWritten=written.Status==RoomTypeBatchStatus.Success&&await db.AuditLogs.CountAsync()==auditBeforeFault+2;
       await guard();await db.Database.ExecuteSqlRawAsync("THROW 51996,'Owned caller participant fault after domain and audit',1;");}
   catch(SqlException e)when(e.Number==51996){failed=true;}finally{await tx.RollbackAsync();db.ChangeTracker.Clear();}
  }
  Check("Actual SQL participant fault after domain/audit rolls back root, binding and both audits",failed&&domainWritten&&await Snapshot()==beforeFault);
  InspectionAssignmentComponentResult committed;await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
   var retained=await Admit(db,actor);var physical=tx.GetDbTransaction();var result=await Service(db).AssignInspectionComponentInCallerAsync(login.Ticket,login.Session.SessionId,branch,task,Command(),actor,retained,Expect(options),default);
   if(result.Status!=RoomTypeBatchStatus.Success)throw new Exception("Positive component failed "+result.Status);committed=(InspectionAssignmentComponentResult)result.Value!;
   Check("Domain participant keeps caller physical SERIALIZABLE transaction",ReferenceEquals(physical,db.Database.CurrentTransaction!.GetDbTransaction()));
   await guard();await tx.CommitAsync();db.ChangeTracker.Clear();
  }
  Check("Assignment persists exact cycle/version/explicit supervisor, without notification acceptance",committed.TaskId==task&&committed.InspectorId==actor&&committed.ResponsibleSupervisorId==actor&&committed.ChecklistVersionId==options.ChecklistVersionId&&committed.CycleRevision==options.CycleRevision&&!committed.NotificationAccepted&&!committed.AuthoritativeCommitAccepted);
  var dbOptions=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
  async Task<HotelOpsDbContext> Open(){var context=new HotelOpsDbContext(dbOptions);await context.Database.OpenConnectionAsync();using var env=JsonDocument.Parse(await File.ReadAllTextAsync(environment));await using var q=context.Database.GetDbConnection().CreateCommand();q.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')";await using var r=await q.ExecuteReaderAsync();if(!await r.ReadAsync()||r.GetString(0)!=env.RootElement.GetProperty("database").GetString()||r.GetString(1)!=env.RootElement.GetProperty("server").GetString()||r.GetString(2)!=env.RootElement.GetProperty("owner").GetString())throw new Exception("Connection identity rejected");return context;}
  await using(var fresh=await Open()){
   var item=await fresh.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==committed.InspectionId);var binding=await fresh.Database.SqlQuery<long>($"SELECT responsible_supervisor_user_id AS Value FROM dbo.inspection_supervisor_bindings WHERE inspection_id={committed.InspectionId}").SingleAsync();
   Check("Separate connection reload sees committed assigned Inspection and responsibility",item.Status=="PENDING"&&item.VersionId==options.ChecklistVersionId&&item.CleaningTaskId==task&&binding==actor);
  }
  await Negative("Open Inspection rejects duplicate/reassignment before overwrite",RoomTypeBatchStatus.Duplicate);
  var beforePublic=await Snapshot();var closed=await Service(db).AssignInspectionAsync(login.Ticket,login.Session.SessionId,branch,second,Command(),default);
  Check("Production service remains unavailable and does not mutate",closed.Status==RoomTypeBatchStatus.Unavailable&&beforePublic==await Snapshot());
  var internalClosed=await Service(db).AssignInspectionWithSupervisorCandidateAsync(login.Ticket,login.Session.SessionId,branch,second,Command(),actor,default);
  Check("Existing explicit candidate still rolls back without mandatory notification",internalClosed.Status==RoomTypeBatchStatus.Unavailable&&beforePublic==await Snapshot());
  var secondOptions=(InspectionAssignmentOptions)(await Service(db).ReadInspectionAssignmentOptionsAsync(login.Ticket,login.Session.SessionId,branch,second,default)).Value!;
  var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  async Task<RoomTypeBatchStatus> Concurrent(){await using var next=await Open();await gate.Task;await using var tx=await next.Database.BeginTransactionAsync(IsolationLevel.Serializable);var retained=await Admit(next,actor);var result=await Service(next).AssignInspectionComponentInCallerAsync(login.Ticket,login.Session.SessionId,branch,second,Command(),actor,retained,Expect(secondOptions),default);if(result.Status==RoomTypeBatchStatus.Success)await tx.CommitAsync();else await tx.RollbackAsync();return result.Status;}
  var a=Concurrent();var b=Concurrent();gate.SetResult();var results=await Task.WhenAll(a,b);Check("Concurrent assignment gives one isolated component commit and one conflict",results.Count(s=>s==RoomTypeBatchStatus.Success)==1&&results.Count(s=>s==RoomTypeBatchStatus.Duplicate)==1);
  queue=(InspectionAssignmentQueue)(await Service(db).ReadInspectionAssignmentQueueAsync(login.Ticket,login.Session.SessionId,branch,default)).Value!;
  Check("Assigned-to-me navigation uses persisted Inspection identity",queue.Assigned.Any(i=>i.InspectionId==committed.InspectionId)&&queue.Pending.All(t=>t.TaskId!=task&&t.TaskId!=second));
  var constraints=await db.Database.SqlQuery<string>($"SELECT definition AS Value FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks')").ToArrayAsync();
  Check("UC047 production Rework constraint still has no invented Failed state",constraints.Any(s=>s.Contains("PENDING_REINSPECTION"))&&!constraints.Any(s=>s.Contains("'FAILED'")));
  await File.WriteAllTextAsync(Path.Combine(output,"assignment-results.json"),JsonSerializer.Serialize(new{success=true,componentCommitted=true,notificationAccepted=false,authoritativeAssignment=false,httpMiddlewareVerified=false,cases},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"{cases.Count}/{cases.Count} PASS isolated UC041 domain only; authoritative assignment NOT_RUN");
 }
}
