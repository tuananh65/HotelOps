using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskAssignmentScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient executor,HttpClient managerHttp,long actor,User manager,
        long branch,long foreign,long department,long pending,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{assignmentCheck=name});}
        string Root(long task)=>$"/api/branches/{branch}/operational-tasks/{task}";
        async Task<(int Status,JsonElement Body,string? Tag)> Send(HttpClient client,HttpMethod method,string path,object? body=null,string? tag=null,bool csrf=true)
        {
            using var request=new HttpRequestMessage(method,path);if(body!=null)request.Content=JsonContent.Create(body);if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf&&method!=HttpMethod.Get){var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await client.SendAsync(request,ct);var raw=await response.Content.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(string.IsNullOrWhiteSpace(raw)?"{}":raw);
            return((int)response.StatusCode,json.RootElement.Clone(),response.Headers.ETag?.ToString());
        }
        async Task<string> Tag(long task){var result=await Send(managerHttp,HttpMethod.Get,Root(task)+"/assignment");Check(result.Status==200,"manager assignment snapshot available");return result.Tag!;}
        async Task<string> Fingerprint(long task)=>JsonSerializer.Serialize(new{
            parent=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct),
            assignments=await db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.TaskId==task).OrderBy(a=>a.AssignmentId).Select(a=>new{a.AssignmentId,a.AssigneeId,a.AssignmentStatus,a.AssignedAt,a.EndedAt,a.Note}).ToArrayAsync(ct),
            intervals=await db.WorkIntervals.AsNoTracking().OrderBy(i=>i.IntervalId).ToArrayAsync(ct),
            progress=await db.OperationalTaskProgresses.CountAsync(ct),audit=await db.AuditLogs.CountAsync(ct),
            version=await db.Database.SqlQuery<string>($"SELECT CONVERT(varchar(16),CONVERT(binary(8),row_version),2) AS Value FROM dbo.task_progress_versions WHERE task_id={task}").SingleAsync(ct)});
        await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,department),ct);
        var hk=await db.Roles.Where(r=>r.RoleCode=="HK_STAFF").Select(r=>r.RoleId).SingleAsync(ct);
        var suffix=Guid.NewGuid().ToString("N")[..8];var next=new User{EmployeeCode="D3-ASG-"+suffix,Username="assign_exec_"+suffix,Email=suffix+"@example.invalid",FullName="Synthetic next executor",
            AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",DepartmentId=department,CreatedAt=DateTime.UtcNow};
        next.PasswordHash=new PasswordHasher<User>().HashPassword(next,password);db.Users.Add(next);await db.SaveChangesAsync(ct);
        db.UserRoles.Add(new(){UserId=next.UserId,RoleId=hk,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)});
        db.UserBranchAccesses.Add(new(){UserId=next.UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddMinutes(-2)});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        using var nextHttp=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=executor.BaseAddress};
        Check((await Send(nextHttp,HttpMethod.Post,"/api/auth/login",new{username=next.Username,password})).Status==200,"new executor authenticated");
        var root=Root(pending);var tag=await Tag(pending);var before=await Fingerprint(pending);
        Check((await Send(executor,HttpMethod.Get,root+"/assignment")).Status==403,"executor cannot inspect other executors workload");
        Check((await Send(executor,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},tag)).Status==403,"executor cannot assign tasks");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId})).Status==428,"assignment requires ETag");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},tag,false)).Status==400,"assignment requires CSRF");
        Check((await Send(managerHttp,HttpMethod.Post,$"/api/branches/{foreign}/operational-tasks/{pending}/assignment",new{assigneeId=next.UserId},tag)).Status==403,"manager cannot assign outside branch");
        using(var anonymous=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer()}){BaseAddress=executor.BaseAddress})
            Check((await Send(anonymous,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},tag)).Status==401,"anonymous assignment denied");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},"\"0000000000000000\"")).Status==412,"stale assignment rejected");
        foreach(var invalid in new[]{"inactive","department","branch","role","expired"})
        {
            if(invalid=="inactive")await db.Users.Where(u=>u.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.EmploymentStatus,"INACTIVE"),ct);
            if(invalid=="department")await db.Users.Where(u=>u.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,(long?)null),ct);
            if(invalid=="branch")await db.UserBranchAccesses.Where(a=>a.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
            if(invalid=="role")await db.UserRoles.Where(r=>r.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            if(invalid=="expired")await db.UserRoles.Where(r=>r.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ExpiresAt,DateTime.UtcNow.AddMinutes(-1)),ct);
            try{Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},tag)).Status==400,"invalid executor rejected: "+invalid);}
            finally{
                await db.Users.Where(u=>u.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.EmploymentStatus,"ACTIVE").SetProperty(u=>u.DepartmentId,department),CancellationToken.None);
                await db.UserBranchAccesses.Where(a=>a.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,(DateTime?)null),CancellationToken.None);
                await db.UserRoles.Where(r=>r.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true).SetProperty(r=>r.ExpiresAt,(DateTime?)null),CancellationToken.None);
            }
        }
        Check(await Fingerprint(pending)==before,"authority, eligibility and stale denials preserve full task state and time");
        await SprintTaskAssignmentUserRace.RunAsync(db,managerHttp,next.UserId,root+"/assignment",tag,()=>Fingerprint(pending),checks,ct);
        Check(!await db.ShiftAssignments.AnyAsync(a=>a.UserId==next.UserId,ct),"new executor has no PLANNED shift");
        var race=await Task.WhenAll(Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},tag),Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId},tag));
        Check(race.Count(r=>r.Status==200)==1&&race.Count(r=>r.Status==412)==1,"assign after Decline without PLANNED shift commits once under concurrent retry");
        Check(await db.OperationalTaskAssignments.CountAsync(a=>a.TaskId==pending&&a.AssignmentStatus=="CANCELLED"&&a.EndedAt!=null,ct)==1,"assignment preserves original decline attribution");
        tag=await Tag(pending);
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId,reason="same"},tag)).Status==409,"same owner cannot reset lifecycle");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=actor,reason="   "},tag)).Status==400,"reassignment requires nonblank reason");
        Check((await Send(nextHttp,HttpMethod.Post,root+"/accept",tag:tag)).Status==200,"new assignee Accept after assignment");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=actor,reason="Swap"},await Tag(pending))).Status==200,"Accepted reassigns to Assigned with short nonblank reason");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=next.UserId,reason="Cover"},await Tag(pending))).Status==200,"Assigned reassigns to Assigned");
        var open=await db.WorkIntervals.AsNoTracking().SingleAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct);var working=open.WorkItemId;
        var firstStart=await db.OperationalTasks.Where(t=>t.TaskId==working).Select(t=>t.ActualStartAt).SingleAsync(ct);
        tag=await Tag(working);before=await Fingerprint(working);
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskAssignFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51869,'Synthetic assignment audit failure',1; END",ct);
            Check((await Send(managerHttp,HttpMethod.Post,Root(working)+"/assignment",new{assigneeId=next.UserId,reason="Cover work"},tag)).Status>=500,"audit failure rejects In Progress reassignment");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskAssignFault",CancellationToken.None);}
        Check(await Fingerprint(working)==before,"audit failure rolls back interval closure, old/new assignment, progress and workflow");
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskAssignFault ON dbo.operational_task_assignments AFTER INSERT AS BEGIN UPDATE r SET is_active=0 FROM dbo.user_roles r JOIN inserted i ON i.assignee_id=r.user_id; END",ct);
            Check((await Send(managerHttp,HttpMethod.Post,Root(working)+"/assignment",new{assigneeId=next.UserId,reason="Cover work"},tag)).Status==400,"final executor eligibility loss rejects reassignment");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskAssignFault",CancellationToken.None);}
        Check(await Fingerprint(working)==before&&await db.UserRoles.AnyAsync(r=>r.UserId==next.UserId&&r.IsActive,ct),"final eligibility failure rolls back closure and injected role change");
        Check((await Send(managerHttp,HttpMethod.Post,Root(working)+"/assignment",new{assigneeId=next.UserId,reason="Cover work"},tag)).Status==200,"In Progress reassign closes interval and commits new owner");
        var closed=await db.WorkIntervals.AsNoTracking().SingleAsync(i=>i.IntervalId==open.IntervalId,ct);
        Check(closed.ClosedAt!=null&&closed.ClosedBy==manager.UserId&&closed.EffectiveEndedAt==closed.ClosedAt&&closed.EffectiveStartedAt==open.EffectiveStartedAt,"old interval closed at server time by authorized manager");
        Check(await db.OperationalTasks.Where(t=>t.TaskId==working).Select(t=>t.ActualStartAt).SingleAsync(ct)==firstStart,"reassign preserves historical first start");
        Check((await Send(executor,HttpMethod.Get,Root(working)+"/progress")).Status==403&&(await Send(executor,HttpMethod.Get,Root(working))).Status==404,"previous owner loses execute and OWN read");
        var snap=await Send(managerHttp,HttpMethod.Get,Root(working)+"/assignment");
        Check(snap.Body.GetProperty("executors").EnumerateArray().Single(x=>x.GetProperty("userId").GetInt64()==actor).GetProperty("loggedSecondsToday").GetInt64()>0,"manager workload includes closed effective effort today");
        Check((await Send(nextHttp,HttpMethod.Post,Root(working)+"/start",tag:snap.Tag)).Status==409,"new executor cannot Start before Accept");
        Check((await Send(nextHttp,HttpMethod.Post,Root(working)+"/accept",tag:snap.Tag)).Status==200,"reassigned executor must Accept again");
        var day=new Workday{UserId=next.UserId,BranchId=branch,DepartmentId=department,WorkDate=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),IsUnscheduled=true,UnscheduledReason="Synthetic assignment trial",Status="WORKING",StartedAt=DateTime.UtcNow.AddMinutes(-2)};
        db.Workdays.Add(day);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await SprintTaskExecutionUserLock.RunAsync(db,nextHttp,next.UserId,working,Root(working)+"/start",await Tag(working),checks,ct);
        var pauseReason=await db.ReferenceValues.Where(r=>r.GroupCode=="TASK_PAUSE_REASON"&&r.ValueCode=="OTHER"&&r.IsActive).Select(r=>r.ReferenceId).SingleAsync(ct);
        Check((await Send(nextHttp,HttpMethod.Post,Root(working)+"/pause",new{reasonId=pauseReason,note="Synthetic pause before reassignment"},await Tag(working))).Status==200,"new executor pauses with approved reason");
        await SprintTaskExecutionUserLock.RunAsync(db,nextHttp,next.UserId,working,Root(working)+"/resume",await Tag(working),checks,ct);
        Check((await Send(nextHttp,HttpMethod.Post,Root(working)+"/pause",new{reasonId=pauseReason,note="Synthetic pause after concurrency verification"},await Tag(working))).Status==200,"executor pauses again before reassignment");
        Check((await Send(managerHttp,HttpMethod.Post,Root(working)+"/assignment",new{assigneeId=actor,reason="Resume by original executor"},await Tag(working))).Status==200,"Paused reassign returns Assigned without opening time");
        Check(!await db.WorkIntervals.AnyAsync(i=>i.WorkItemId==working&&i.WorkItemKind=="TASK"&&i.ClosedAt==null,ct),"paused reassignment opens no interval");
        var lastAudit=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="operational_tasks"&&a.EntityId==working.ToString()&&a.NewValues!.Contains("\"operation\":\"ASSIGN\"")).OrderByDescending(a=>a.AuditId).FirstAsync(ct);
        async Task<string> Deliver(){await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type=typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskAssignmentNotificationDelivery",true)!;
            return await (Task<string>)type.GetMethod("DeliverAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[lastAudit.AuditId,ct])!;}
        await db.UserRoles.Where(r=>r.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check(await Deliver()=="RECIPIENT_READ_DENIED","revoked previous executor is not notified");
        await db.UserRoles.Where(r=>r.UserId==next.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        Check((await Task.WhenAll(Deliver(),Deliver())).All(r=>r=="DELIVERED"),"assignment notification concurrent retries converge");
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="TASK_ASSIGNMENT"&&n.SourceId==lastAudit.AuditId,ct);
        Check(await db.NotificationRecipients.CountAsync(r=>r.NotificationId==notice.NotificationId,ct)==2,"one notice receipt each for previous and new owner");
        Check((await Send(nextHttp,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==404,"generic removal notice never grants previous owner source access");
        Check((await Send(executor,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==200,"new owner opens source with current authorization");
        foreach(var state in new[]{"PENDING_VERIFICATION","CLOSED"}){
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status={state} WHERE task_id={pending}",ct);
            var revision=await db.Database.SqlQuery<string>($"SELECT CONVERT(varchar(16),CONVERT(binary(8),row_version),2) AS Value FROM dbo.task_progress_versions WHERE task_id={pending}").SingleAsync(ct);
            Check((await Send(managerHttp,HttpMethod.Post,root+"/assignment",new{assigneeId=actor,reason="Too late"},"\""+revision+"\"")).Status==409,state+" cannot reassign even with fresh version");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='ASSIGNED' WHERE task_id={pending}",ct);
        var browserTask=new OperationalTask{TaskCode="D3-ASG-"+Guid.NewGuid().ToString("N")[..8],BranchId=branch,DepartmentId=department,Title="Synthetic assignment demo",Status="TODO",CreatedBy=manager.UserId,CreatedAt=DateTime.UtcNow};
        db.OperationalTasks.Add(browserTask);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({browserTask.TaskId},'PENDING_ASSIGNMENT')",ct);
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintTaskAssignmentBrowser.mjs");
        using(var process=Process.Start(start)!){try{await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{manager=manager.Username,executor=username,password,branch,task=browserTask.TaskId,assignee=actor,nextAssignee=next.UserId,nextUsername=next.Username}));process.StandardInput.Close();var output=process.StandardOutput.ReadToEndAsync(ct);var error=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await output);_=await error;checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"canonical manager Assign then executor Accept browser workflow");}finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}}
        Check((await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.task_progress_versions WHERE task_id={browserTask.TaskId}").SingleAsync(ct))=="ACCEPTED","browser assignment and acceptance persisted to SQL");
        if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1' AND CONVERT(nvarchar(20),value)='1'").SingleAsync(ct)==1)
            await SprintTaskPlanningScenario.RunAsync(db,executor,managerHttp,actor,manager,next,branch,foreign,department,checks,ct,username,password);
    }
}
