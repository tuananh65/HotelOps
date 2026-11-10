using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

// Direct candidate controller -> real service -> fresh owned SQL. No HTTP interception,
// no synthetic authorization. Synthetic source fixtures do not prove lifecycle acceptance.
internal static class ReinspectionWorkflowSqlChecks
{
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor {public bool TryGetCurrentUser(out CurrentUserInfo current){current=value;return true;}}
    internal static async Task RunAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard)
    {
        using var runtime=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!,"runtime.private.json")));
        var password=runtime.RootElement.GetProperty("password").GetString()!;
        var actor=await db.Users.Where(u=>u.Username=="int.hk").Select(u=>u.UserId).SingleAsync();
        var other=await db.Users.Where(u=>u.Username=="int.denied").Select(u=>u.UserId).SingleAsync();
        var root=await db.RoomInspections.AsNoTracking().Where(i=>i.Result==null).OrderBy(i=>i.InspectionId).FirstAsync();
        var branch=await db.Rooms.Where(r=>r.RoomId==root.RoomId).Select(r=>r.BranchId).SingleAsync();
        var foreign=await db.Branches.Where(b=>b.BranchCode=="BR002").Select(b=>b.BranchId).SingleAsync();
        var cases=new List<object>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);cases.Add(new{name,result="PASS"});Console.WriteLine("PASS "+name);}
        var sqlSessions=new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System);
        async Task<CreatedSqlSession> Login(long user){
            await guard();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var credentials=new CredentialLockoutService(db,new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount})),TimeProvider.System);
            var verified=await credentials.VerifyAsync(user,password,(u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"));
            var session=await sqlSessions.CreateSessionAsync(verified);await guard();await tx.CommitAsync();db.ChangeTracker.Clear();return session;
        }
        var login=await Login(actor);var denied=await Login(other);
        long inspection,rework;
        await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',inspected_at=SYSUTCDATETIME() WHERE inspection_id={root.InspectionId}; UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={root.CleaningTaskId}");
            rework=(await db.Database.SqlQuery<long>($"INSERT dbo.cleaning_reworks(inspection_id,cleaning_task_id,assignee_user_id,reason,status,workflow_status,requested_at) VALUES({root.InspectionId},{root.CleaningTaskId},{other},'FAST015 synthetic candidate source','IN_PROGRESS','PENDING_REINSPECTION',SYSUTCDATETIME()); SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value").ToArrayAsync()).Single();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_rework_defects(rework_id,defect_id,remediation_note,remediated_by,remediated_at) SELECT {rework},defect_id,'Synthetic remediation',{other},SYSUTCDATETIME() FROM dbo.inspection_defects WHERE inspection_id={root.InspectionId}");
            var number="F015-"+Guid.NewGuid().ToString("N");
            inspection=(await db.Database.SqlQuery<long>($"INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at) VALUES({number},{root.RoomId},{root.CleaningTaskId},{root.VersionId},{actor},'PENDING','ASSIGNED',SYSUTCDATETIME()); SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value").ToArrayAsync()).Single();
            await guard();await tx.CommitAsync();db.ChangeTracker.Clear();
        }
        string Tag(byte[] value)=>'"'+Convert.ToHexString(value)+'"';
        RoomTypeCreationService Service(HotelOpsDbContext context,CreatedSqlSession session){var current=new Identity(new(session.Session.UserId,session.Session.SessionId,false));return new(context,current,new PermissionService(current,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);}
        ReinspectionWorkflowCandidateController Controller(HotelOpsDbContext context,CreatedSqlSession? session){var http=new DefaultHttpContext();http.Request.Scheme="https";if(session!=null)http.User=HotelOpsCookieTicket.Create(session,TimeProvider.System).Principal;return new(Service(context,session??login)){ControllerContext=new(){HttpContext=http}};}
        int Status(IActionResult r)=>((ObjectResult)r).StatusCode??200;
        ReinspectionWorkflowDetails Details(IActionResult r)=>r is OkObjectResult{Value:ReinspectionWorkflowDetails d}?d:throw new Exception("Expected candidate details; status="+Status(r));
        var controller=Controller(db,login);
        var ir=Tag(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync());
        var wr=Tag(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync());
        var audits=await db.AuditLogs.CountAsync();
        var bound=Details(await controller.Bind(branch,inspection,new(rework,ir,wr),default));
        Check("Authorized candidate bind commits exact source and audit",bound.Criteria.Count>0&&bound.Defects.All(d=>d.Fixed==null&&!string.IsNullOrEmpty(d.Description))&&await db.AuditLogs.CountAsync()==audits+1&&bound.OriginalChecklistVersionId==root.VersionId&&bound.AssignedChecklistVersionId==root.VersionId);
        Check("Duplicate bind returns 409 without duplicate audit",Status(await controller.Bind(branch,inspection,new(rework,bound.InspectionRevision,bound.ReworkRevision),default))==409&&await db.AuditLogs.CountAsync()==audits+1);
        Check("Invalid branch returns 400",Status(await controller.List(0,default))==400);
        Check("Assigned list loads persisted exact binding",await controller.List(branch,default) is OkObjectResult{Value:ReinspectionWorkflowItem[] items}&&items.Any(i=>i.InspectionId==inspection&&i.ReworkId==rework));
        async Task<IActionResult> Save(ReinspectionWorkflowCandidateController c,ReinspectionWorkflowDetails d,bool value,string? tag=null){c.Request.Headers.IfMatch=tag??d.InspectionRevision;return await c.Save(branch,inspection,new(rework,d.ReworkRevision,d.BindingRevision,d.Defects.Select(x=>new ReinspectionDecisionDto(x.DefectId,value)).ToArray()),default);}
        var saved=Details(await Save(controller,bound,false));
        Check("Not Fixed service save commits actor, timestamp and aggregate revisions",saved.Defects.All(d=>d.Fixed==false&&d.RecordedBy==actor&&d.RecordedAt!=null)&&saved.InspectionRevision!=bound.InspectionRevision&&saved.BindingRevision!=bound.BindingRevision&&await db.AuditLogs.CountAsync()==audits+2);
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
        async Task<HotelOpsDbContext> Open(){var next=new HotelOpsDbContext(options);await next.Database.OpenConnectionAsync();using var expected=JsonDocument.Parse(await File.ReadAllTextAsync(environment));await using var q=next.Database.GetDbConnection().CreateCommand();q.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')";await using var r=await q.ExecuteReaderAsync();var e=expected.RootElement;if(!await r.ReadAsync()||r.GetString(0)!=e.GetProperty("server").GetString()||r.GetString(1)!=e.GetProperty("database").GetString()||r.GetString(2)!=e.GetProperty("owner").GetString())throw new Exception("Fresh connection ownership rejected");return next;}
        await using(var fresh=await Open()){var loaded=Details(await Controller(fresh,login).Read(branch,inspection,rework,default));Check("Different connection reload sees committed Not Fixed and pinned versions",loaded.Defects.All(d=>d.Fixed==false&&d.RecordedBy==actor)&&loaded.InspectionRevision==saved.InspectionRevision&&loaded.AssignedChecklistVersionId==root.VersionId);}
        Check("Stale root returns 412 without another audit",Status(await Save(controller,bound,true))==412&&await db.AuditLogs.CountAsync()==audits+2);
        Check("Stale binding returns 412",Status(await Save(controller,saved with{BindingRevision=bound.BindingRevision},true))==412);
        controller.Request.Headers.Remove("If-Match");Check("Missing If-Match returns 428",Status(await controller.Save(branch,inspection,new(rework,saved.ReworkRevision,saved.BindingRevision,[]),default))==428);
        controller.Request.Headers.IfMatch=saved.InspectionRevision;Check("Incomplete decisions return 400",Status(await controller.Save(branch,inspection,new(rework,saved.ReworkRevision,saved.BindingRevision,[]),default))==400);
        Check("Wrong branch returns 403",Status(await controller.Read(foreign,inspection,rework,default))==403);
        Check("Missing session returns 401",Status(await Controller(db,null).Read(branch,inspection,rework,default))==401);
        Check("Existing account without grant returns 403",Status(await Controller(db,denied).Read(branch,inspection,rework,default))==403);
        var allFixed=Details(await Save(controller,saved,true));Check("All Fixed persists without concluding lifecycle",allFixed.Defects.All(d=>d.Fixed==true)&&!allFixed.LifecycleConclusionAvailable);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IActionResult> Concurrent(bool value){await using var context=await Open();await ready.Task;return await Save(Controller(context,login),allFixed,value);}
        var one=Concurrent(true);var two=Concurrent(false);ready.SetResult();var contenders=await Task.WhenAll(one,two);
        Check("Concurrent saves: one commit and one stale rejection",contenders.Count(r=>Status(r)==200)==1&&contenders.Count(r=>Status(r)==412)==1);
        var current=Details(await controller.Read(branch,inspection,rework,default));var beforeRollback=await db.AuditLogs.CountAsync();
        await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
            await Service(db,login).SaveReinspectionComponentInCallerTransactionAsync(login.Ticket,login.Session.SessionId,branch,inspection,rework,current.InspectionRevision,current.ReworkRevision,current.BindingRevision,current.Defects.Select(d=>new ReinspectionDefectDecision(d.DefectId,!d.Fixed!.Value)).ToArray(),default);
            await tx.RollbackAsync();db.ChangeTracker.Clear();
        }
        var rolledBack=Details(await controller.Read(branch,inspection,rework,default));Check("Explicit rollback restores results, root/binding revision and audit",current.InspectionRevision==rolledBack.InspectionRevision&&current.BindingRevision==rolledBack.BindingRevision&&current.Defects.Select(d=>d.Fixed).SequenceEqual(rolledBack.Defects.Select(d=>d.Fixed))&&await db.AuditLogs.CountAsync()==beforeRollback);
        Check("Conclude stays 503 with no lifecycle mutation",Status(await controller.Conclude(branch,inspection,rework,default))==503&&await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync()=="PENDING_REINSPECTION"&&await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync()=="ASSIGNED");
        await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={other} WHERE inspection_id={inspection}");
        Check("Wrong persisted reinspector returns 403",Status(await controller.Read(branch,inspection,rework,default))==403);
        await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={actor} WHERE inspection_id={inspection}; DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");
        Check("Missing current cycle fails closed",Status(await controller.Read(branch,inspection,rework,default)) is 404 or 409);
        Check("Production MVC dispatch remains OFF",typeof(ReinspectionWorkflowCandidateController).IsDefined(typeof(NonControllerAttribute),false));
        await File.WriteAllTextAsync(Path.Combine(output,"workflow-results.json"),JsonSerializer.Serialize(new{success=true,candidateStorageCommitted=true,authoritativeLifecycleCommit=false,httpMiddlewareVerified=false,syntheticSourceFixtures=true,cases},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{cases.Count}/{cases.Count} PASS candidate workflow only; no authoritative handoff");
    }
}
