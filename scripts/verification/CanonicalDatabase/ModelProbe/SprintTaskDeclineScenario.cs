using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskDeclineScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long branch,long foreign,long department,
        List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{declineCheck=name});}
        async Task<(int Status,JsonElement Body,string? Tag)> Send(HttpClient client,HttpMethod method,string path,object? body=null,string? tag=null,bool csrf=true)
        {
            using var request=new HttpRequestMessage(method,path);if(body!=null)request.Content=JsonContent.Create(body);if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf&&method!=HttpMethod.Get){var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await client.SendAsync(request,ct);var raw=await response.Content.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(string.IsNullOrWhiteSpace(raw)?"{}":raw);
            return((int)response.StatusCode,json.RootElement.Clone(),response.Headers.ETag?.ToString());
        }
        var managerRole=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
        async Task<User> Manager(long target)
        {
            var suffix=Guid.NewGuid().ToString("N")[..8];var u=new User{EmployeeCode="D3-DECL-"+suffix,Username="decline_mgr_"+suffix,Email=suffix+"@example.invalid",
                FullName="Synthetic decline manager",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
            u.PasswordHash=new PasswordHasher<User>().HashPassword(u,password);db.Users.Add(u);await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new(){UserId=u.UserId,RoleId=managerRole,BranchId=target,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)});
            db.UserBranchAccesses.Add(new(){UserId=u.UserId,BranchId=target,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddMinutes(-2)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();return u;
        }
        var manager=await Manager(branch);var foreignManager=await Manager(foreign);
        using var managerHttp=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=http.BaseAddress};
        Check((await Send(managerHttp,HttpMethod.Post,"/api/auth/login",new{username=manager.Username,password})).Status==200,"Department Head session established");
        var hkRole=await db.Roles.Where(r=>r.RoleCode=="HK_STAFF"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
        async Task<OperationalTask> Assigned()
        {
            var priority=await db.ReferenceValues.Where(r=>r.GroupCode=="PRIORITY"&&r.IsActive).OrderBy(r=>r.ReferenceId).Select(r=>(long?)r.ReferenceId).FirstOrDefaultAsync(ct);
            var t=new OperationalTask{TaskCode="D3-DECL-"+Guid.NewGuid().ToString("N")[..8],BranchId=branch,DepartmentId=department,Title="Synthetic decline task",
                Description="Review assignment before responding",Status="TODO",PriorityId=priority,DueAt=DateTime.UtcNow.AddHours(2),CreatedBy=manager.UserId,CreatedAt=DateTime.UtcNow};
            db.OperationalTasks.Add(t);await db.SaveChangesAsync(ct);
            db.OperationalTaskAssignments.Add(new(){TaskId=t.TaskId,AssigneeId=actor,AssignedBy=manager.UserId,AssignedAt=DateTime.UtcNow.AddMinutes(-1),AssignmentStatus="ACTIVE",Note="Original assignment note"});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({t.TaskId},'ASSIGNED')",ct);return t;
        }
        async Task<string> Deliver(long audit)
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type=typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskDeclineNotificationDelivery",true)!;
            return await (Task<string>)type.GetMethod("DeliverAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[audit,ct])!;
        }
        var task=await Assigned();var root=$"/api/branches/{branch}/operational-tasks/{task.TaskId}";
        var tag=(await Send(http,HttpMethod.Get,root+"/progress")).Tag!;
        async Task<string> Fingerprint()=>JsonSerializer.Serialize(new{
            task=await db.OperationalTasks.AsNoTracking().Where(t=>t.TaskId==task.TaskId).Select(t=>new{t.Status,t.ActualStartAt,t.CompletedAt,t.UpdatedAt}).SingleAsync(ct),
            assignments=await db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.TaskId==task.TaskId).Select(a=>new{a.AssignmentStatus,a.EndedAt,a.Note}).ToArrayAsync(ct),
            version=(await Send(http,HttpMethod.Get,root+"/progress")).Tag,history=await db.OperationalTaskProgresses.CountAsync(ct),audit=await db.AuditLogs.CountAsync(ct),intervals=await db.WorkIntervals.CountAsync(ct)});
        var before=await Fingerprint();
        using(var anonymous=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer()}){BaseAddress=http.BaseAddress})
            Check((await Send(anonymous,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status==401,"anonymous Decline with CSRF denied");
        Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"})).Status==428,"Decline requires strong ETag");
        Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag,false)).Status==400,"Decline requires CSRF");
        Check((await Send(managerHttp,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status==403,"manager cannot decline another assignee's task");
        Check((await Send(http,HttpMethod.Post,$"/api/branches/{foreign}/operational-tasks/{task.TaskId}/decline",new{reason="Unavailable"},tag)).Status==403,"foreign branch Decline denied");
        foreach(var reason in new[]{"", " \r\n ",new string('x',4001),"bad\u0001reason"})
            Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason},tag)).Status==400,"invalid decline reason rejected: length "+reason.Length);
        Check(await Fingerprint()==before,"all input and authority denials leave state, assignment, audit, progress and time unchanged");
        try{
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskDecline.V1'",ct);
            Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status==503,"missing decline capability fails closed");
            Check((await Send(managerHttp,HttpMethod.Get,$"/api/branches/{branch}/operational-tasks?workflowStatus=PENDING_ASSIGNMENT")).Status==503,"missing workflow filter capability fails closed");
        }finally{await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskDecline.V1',@value=N'1'",CancellationToken.None);}
        await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        try{Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status==403,"revoked executor permission denies Decline");}
        finally{await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET updated_at=SYSUTCDATETIME() WHERE task_id={task.TaskId}",ct);
        Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status==412,"stale Decline refused");
        tag=(await Send(http,HttpMethod.Get,root+"/progress")).Tag!;before=await Fingerprint();
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskDeclineFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='operational_tasks' AND new_values LIKE '%DECLINE%') THROW 51867,'Synthetic decline failure',1; END",ct);
            Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status>=500&&await Fingerprint()==before,"audit failure rolls back Decline and cannot publish notification intent");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskDeclineFault",CancellationToken.None);}
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskDeclineFault ON dbo.operational_task_assignments AFTER UPDATE AS BEGIN UPDATE ur SET is_active=0 FROM dbo.user_roles ur JOIN inserted i ON i.assignee_id=ur.user_id WHERE i.assignment_status='CANCELLED' AND i.ended_at IS NOT NULL; END",ct);
            Check((await Send(http,HttpMethod.Post,root+"/decline",new{reason="Unavailable"},tag)).Status==403&&await Fingerprint()==before,"final permission gate revalidates after assignment removal and rolls back revocation and decline");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskDeclineFault",CancellationToken.None);}
        var race=await Task.WhenAll(Send(http,HttpMethod.Post,root+"/decline",new{reason="No"},tag),Send(http,HttpMethod.Post,root+"/decline",new{reason="No"},tag));
        Check(race.Count(r=>r.Status==200)==1&&race.Count(r=>r.Status==403)==1,"concurrent Decline commits once; loser has lost assignment authority");
        var success=race.Single(r=>r.Status==200);Check(success.Body.GetProperty("messageCode").GetString()=="MSG19"&&success.Tag!=tag,"Decline confirms MSG19 and fresh version; short nonblank reason accepted");
        var ended=await db.OperationalTaskAssignments.AsNoTracking().SingleAsync(a=>a.TaskId==task.TaskId,ct);
        Check(ended.AssignmentStatus=="CANCELLED"&&ended.EndedAt!=null&&ended.Note=="Original assignment note","assignee removed without erasing assignment history or original note");
        var detail=await Send(managerHttp,HttpMethod.Get,root);Check(detail.Status==200&&detail.Body.GetProperty("task").GetProperty("workflowStatus").GetString()=="PENDING_ASSIGNMENT"
            &&detail.Body.GetProperty("task").GetProperty("status").GetString()=="TODO","manager reads canonical Pending Assignment while legacy status remains valid");
        Check((await Send(http,HttpMethod.Get,root)).Status==404&&(await Send(http,HttpMethod.Get,root+"/progress")).Status==403,"former assignee cannot read task detail or progress");
        Check((await Send(http,HttpMethod.Get,$"/api/branches/{branch}/operational-tasks?workflowStatus=PENDING_ASSIGNMENT")).Body.GetProperty("totalCount").GetInt32()==0,"OWN list excludes declined tasks");
        var page=await Send(managerHttp,HttpMethod.Get,$"/api/branches/{branch}/operational-tasks?workflowStatus=PENDING_ASSIGNMENT&pageSize=1");
        Check(page.Status==200&&page.Body.GetProperty("totalCount").GetInt32()==1&&page.Body.GetProperty("items")[0].GetProperty("id").GetInt64()==task.TaskId,"workflow filter applies before pagination/count");
        Check((await Send(managerHttp,HttpMethod.Get,$"/api/branches/{branch}/operational-tasks?workflowStatus=TODO")).Status==400,"legacy status is not accepted as an invented workflow");
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="operational_tasks"&&a.EntityId==task.TaskId.ToString(),ct);
        using(var json=JsonDocument.Parse(audit.NewValues!))Check(json.RootElement.GetProperty("recipients").EnumerateArray().Select(x=>x.GetInt64()).SequenceEqual(new[]{manager.UserId}),"intent includes only eligible Department Head in branch");
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check(await Deliver(audit.AuditId)=="RECIPIENT_READ_DENIED"&&!await db.Notifications.AnyAsync(n=>n.SourceType=="TASK_DECLINE",ct),"revoked manager denied delivery");
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskDeliveryFault ON dbo.notification_recipients AFTER INSERT AS BEGIN THROW 51867,'Synthetic decline delivery failure',1; END",ct);
            var failed=false;try{await Deliver(audit.AuditId);}catch{failed=true;}
            Check(failed&&!await db.Notifications.AnyAsync(n=>n.SourceType=="TASK_DECLINE",ct),"delivery failure rolls back notification but preserves durable intent");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskDeliveryFault",CancellationToken.None);}
        Check((await Task.WhenAll(Deliver(audit.AuditId),Deliver(audit.AuditId))).All(x=>x=="DELIVERED"),"concurrent delivery retries converge");
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="TASK_DECLINE"&&n.SourceId==audit.AuditId,ct);
        Check(await db.NotificationRecipients.CountAsync(r=>r.NotificationId==notice.NotificationId,ct)==1,"exactly one manager delivery receipt");
        Check((await Send(managerHttp,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==200,"eligible recipient opens task source");
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check((await Send(managerHttp,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==404,"source opening revalidates manager authority");
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        foreach(var roleCode in new[]{"MAINTENANCE_STAFF","STOREKEEPER"})
        {
            var t=await Assigned();var path=$"/api/branches/{branch}/operational-tasks/{t.TaskId}";
            var role=await db.Roles.Where(r=>r.RoleCode==roleCode&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,role),ct);
            try{var current=await Send(http,HttpMethod.Get,path+"/progress");Check((await Send(http,HttpMethod.Post,path+"/decline",new{reason="Unavailable"},current.Tag)).Status==200,roleCode+" declines with real scoped permission");}
            finally{await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,hkRole),CancellationToken.None);}
        }
        var acceptedTask=await Assigned();var acceptedRoot=$"/api/branches/{branch}/operational-tasks/{acceptedTask.TaskId}";
        var firstStart=DateTime.UtcNow.AddHours(-2);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET actual_start_at={firstStart} WHERE task_id={acceptedTask.TaskId}",ct);
        var acceptedTag=(await Send(http,HttpMethod.Get,acceptedRoot+"/progress")).Tag;
        Check((await Send(http,HttpMethod.Post,acceptedRoot+"/accept",tag:acceptedTag)).Status==200
            &&(await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==acceptedTask.TaskId,ct)).ActualStartAt==firstStart,"Assigned task with historical first start can accept without losing time history");
        Check((await Send(http,HttpMethod.Post,acceptedRoot+"/decline",new{reason="Unavailable"},(await Send(http,HttpMethod.Get,acceptedRoot+"/progress")).Tag)).Status==409,"Accepted task cannot Decline");
        var raceTask=await Assigned();var raceRoot=$"/api/branches/{branch}/operational-tasks/{raceTask.TaskId}";var raceTag=(await Send(http,HttpMethod.Get,raceRoot+"/progress")).Tag;
        var responses=await Task.WhenAll(Send(http,HttpMethod.Post,raceRoot+"/accept",tag:raceTag),Send(http,HttpMethod.Post,raceRoot+"/decline",new{reason="Unavailable"},raceTag));
        Check(responses.Count(r=>r.Status==200)==1&&responses.Count(r=>r.Status is 403 or 412)==1,"Accept versus Decline commits exactly one assignment response");
        var browserTask=await Assigned();
        async Task Browser(object fixture)
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintTaskDeclineBrowser.mjs");
            using var process=Process.Start(start)!;try{await process.StandardInput.WriteAsync(JsonSerializer.Serialize(fixture));process.StandardInput.Close();var output=process.StandardOutput.ReadToEndAsync(ct);var error=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await output);_=await error;checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"canonical decline browser "+fixture.GetType().Name);}
            finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
        }
        await Browser(new{username,password,branch,task=browserTask.TaskId,code=browserTask.TaskCode,mode="decline"});
        var browserAudit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="operational_tasks"&&a.EntityId==browserTask.TaskId.ToString(),ct);
        Check(await Deliver(browserAudit.AuditId)=="DELIVERED","browser committed decline delivered to manager");
        await Browser(new{username=manager.Username,password,branch,task=browserTask.TaskId,code=browserTask.TaskCode,mode="manager"});
        Check(await db.WorkIntervals.CountAsync(i=>i.UserId==actor,ct)==2,"Decline never opens or closes unrelated existing work intervals");
    }
}
