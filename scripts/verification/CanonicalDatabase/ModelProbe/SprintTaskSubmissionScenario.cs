using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskSubmissionScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long branch,long foreign,long department,long task,long other,
        List<object> checks,CancellationToken ct,string username,string password)
    {
        var root=$"/api/branches/{branch}/operational-tasks/{task}";
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{submissionCheck=name});}
        async Task<(int Status,JsonElement Body,string? Tag)> Send(HttpMethod method,string path,object? body=null,string? tag=null,bool csrf=true,HttpClient? client=null)
        {
            client??=http;using var request=new HttpRequestMessage(method,path);
            if(body!=null)request.Content=JsonContent.Create(body);
            if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf&&method!=HttpMethod.Get){var c=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",c.GetProperty("csrfToken").GetString());}
            using var response=await client.SendAsync(request,ct);var raw=await response.Content.ReadAsStringAsync(ct);
            using var json=JsonDocument.Parse(string.IsNullOrWhiteSpace(raw)?"{}":raw);
            return((int)response.StatusCode,json.RootElement.Clone(),response.Headers.ETag?.ToString());
        }
        async Task<string> Fingerprint()
        {
            var row=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct);
            var count=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_submission_events WHERE task_id={task}").SingleAsync(ct);
            return JsonSerializer.Serialize(new{row.Status,row.CompletedAt,row.UpdatedAt,tag=(await Send(HttpMethod.Get,root+"/progress")).Tag,
                history=await db.OperationalTaskProgresses.CountAsync(p=>p.TaskId==task,ct),events=count,
                open=await db.WorkIntervals.CountAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct),audit=await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor,ct),files=await db.Attachments.CountAsync(ct)});
        }
        var managerRole=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
        var setup=new UserRole{UserId=actor,RoleId=managerRole,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)};
        db.UserRoles.Add(setup);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        Check((await Send(HttpMethod.Post,root+"/progress-initialization",new{items=new[]{new{label="Required task result",required=true},new{label="Optional task result",required=false}},reason="Synthetic task submission acceptance criteria"})).Status==201,"criteria initialized by authorized API");
        await db.UserRoles.Where(r=>r.UserRoleId==setup.UserRoleId).ExecuteDeleteAsync(ct);
        var manager=new User{EmployeeCode="D3-MGR-"+Guid.NewGuid().ToString("N")[..8],Username="submit_mgr_"+Guid.NewGuid().ToString("N")[..8],Email="manager_"+Guid.NewGuid().ToString("N")[..8]+"@example.invalid",
            FullName="Synthetic task verifier",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        manager.PasswordHash=new PasswordHasher<User>().HashPassword(manager,password);db.Users.Add(manager);await db.SaveChangesAsync(ct);
        db.UserRoles.Add(new UserRole{UserId=manager.UserId,RoleId=managerRole,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)});
        db.UserBranchAccesses.Add(new(){UserId=manager.UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddMinutes(-2)});
        var day=new Workday{UserId=actor,BranchId=branch,DepartmentId=department,WorkDate=DateOnly.FromDateTime(DateTime.UtcNow),Status="WORKING",StartedAt=DateTime.UtcNow.AddMinutes(-10),IsUnscheduled=true,UnscheduledReason="Synthetic completion trial"};
        db.Workdays.Add(day);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        async Task Open(){db.WorkIntervals.Add(new WorkInterval{UserId=actor,BranchId=branch,WorkdayId=day.WorkdayId,WorkItemKind="TASK",WorkItemId=task,OpenedBy=actor,OpenedAt=DateTime.UtcNow.AddSeconds(-2),EffectiveStartedAt=DateTime.UtcNow.AddSeconds(-2)});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();}
        await Open();
        var snapshot=await Send(HttpMethod.Get,root+"/progress");var tag=snapshot.Tag!;
        var required=snapshot.Body.GetProperty("items")[0].GetProperty("id").GetInt64();var optional=snapshot.Body.GetProperty("items")[1].GetProperty("id").GetInt64();
        object Payload(bool complete=true,object[]? photos=null)=>new{items=complete?new[]{new{id=required,outcome="FAIL",note="Result recorded; verification decides acceptance"}}:new[]{new{id=optional,outcome="PASS",note="Must roll back when required result missing"}},resultNote="Synthetic result\nSecond line",handoverNote="Synthetic handover\nSecond line",photos=photos??[]};
        using(var anonymous=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=http.BaseAddress})
            Check((await Send(HttpMethod.Post,root+"/submit",Payload(),tag,client:anonymous)).Status==401,"anonymous submission with valid CSRF denied");
        try
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_dropextendedproperty @name=N'HotelOps.TaskSubmission.V1'",ct);
            Check((await Send(HttpMethod.Post,root+"/submit",Payload(),tag)).Status==503,"missing submission capability fails closed");
        }
        finally{await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskSubmission.V1',@value=N'1'",CancellationToken.None);}
        await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        try{Check((await Send(HttpMethod.Post,root+"/submit",Payload(),tag)).Status==403,"revoked executor permission denies submission");}
        finally{await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PENDING_VERIFICATION' WHERE task_id={task}",ct);
        Check((await Send(HttpMethod.Get,root+"/progress")).Status==409,"pending state without a committed submission fails closed");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={task}",ct);
        Check((await Send(HttpMethod.Post,root+"/submit",Payload(),(await Send(HttpMethod.Get,root+"/progress")).Tag)).Status==409,"Paused task cannot submit");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='IN_PROGRESS' WHERE task_id={task}",ct);
        await db.WorkIntervals.Where(i=>i.UserId==actor&&i.ClosedAt==null).ExecuteDeleteAsync(ct);
        Check((await Send(HttpMethod.Post,root+"/submit",Payload(),(await Send(HttpMethod.Get,root+"/progress")).Tag)).Status==409,"submission requires its current work interval");
        await Open();tag=(await Send(HttpMethod.Get,root+"/progress")).Tag!;
        var original=await Fingerprint();
        Check((await Send(HttpMethod.Post,root+"/submit",new{items=Array.Empty<object>(),resultNote=new string('x',4001),handoverNote=(string?)null},tag)).Status==400
            && await Fingerprint()==original,"persistence note length enforced without writes");
        Check((await Send(HttpMethod.Post,root+"/submit",Payload(),null)).Status==428,"submission requires ETag");
        Check((await Send(HttpMethod.Post,root+"/submit",Payload(),tag,false)).Status==400,"submission requires CSRF");
        Check((await Send(HttpMethod.Post,$"/api/branches/{branch}/operational-tasks/{other}/submit",Payload(),tag)).Status==403,"unassigned submission denied");
        Check((await Send(HttpMethod.Post,$"/api/branches/{foreign}/operational-tasks/{task}/submit",Payload(),tag)).Status==403,"foreign branch submission denied");
        var missing=await Send(HttpMethod.Post,root+"/submit",Payload(false),tag);
        Check(missing.Status==400&&missing.Body.GetProperty("messageCode").GetString()=="MSG03"&&missing.Body.GetProperty("missingCriteria")[0].GetString()=="Required task result","missing mandatory result returns MSG03 and exact criterion label");
        Check(await Fingerprint()==original&&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_completion_criteria WHERE task_id={task} AND outcome IS NOT NULL").SingleAsync(ct)==0,"failed criteria submission rolls back partial result edits and all business writes");
        var png=new{name="task-proof.png",mimeType="image/png",base64=Convert.ToBase64String(new byte[]{137,80,78,71,13,10,26,10,1,2,3})};
        var bad=new{name="bad.png",mimeType="image/png",base64=Convert.ToBase64String(new byte[]{1,2,3})};
        var invalid=await Send(HttpMethod.Post,root+"/submit",Payload(photos:[png,bad]),tag);
        Check(invalid.Status==400&&invalid.Body.GetProperty("messageCode").GetString()=="MSG23"&&await Fingerprint()==original,"invalid second evidence rolls back first file, criteria, interval and history");
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskSubmitFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='operational_tasks' AND new_values LIKE '%SUBMIT%') THROW 51865,'Synthetic submission audit failure',1; END",ct);
            Check((await Send(HttpMethod.Post,root+"/submit",Payload(photos:[png]),tag)).Status>=500&&await Fingerprint()==original,"submission audit failure rolls back interval, file, results, event and workflow");
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskSubmitFault",CancellationToken.None);}
        var races=await Task.WhenAll(Send(HttpMethod.Post,root+"/submit",Payload(photos:[png]),tag),Send(HttpMethod.Post,root+"/submit",Payload(photos:[png]),tag));
        Check(races.Select(r=>r.Status).Order().SequenceEqual(new[]{200,412}),"concurrent Submit yields one success and one stale response");
        var pending=await Send(HttpMethod.Get,root+"/progress");Check(pending.Body.GetProperty("state").GetString()=="PENDING_VERIFICATION","submission awaits verification and does not close task");
        Check((await Send(HttpMethod.Post,root+"/submit",Payload(),pending.Tag)).Status==409,"fresh repeated submission denied");
        Check((await Send(HttpMethod.Put,root+"/progress",new{percent=100,items=Array.Empty<object>(),note="Forbidden pending edit"},pending.Tag)).Status==409,"pending submission locks progress updates");
        var saved=await Send(HttpMethod.Get,root+"/submission");Check(saved.Status==200&&saved.Body.GetProperty("criteria")[0].GetProperty("outcome").GetString()=="FAIL","mandatory FAIL is a recorded result, not an automatic verification rejection");
        var eventId=saved.Body.GetProperty("id").GetInt64();var taskRow=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct);
        Check(taskRow.CompletedAt==null&&taskRow.Status=="IN_PROGRESS"&&!await db.WorkIntervals.AnyAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct),"actual work ends but legacy task completion remains unset");
        Check(saved.Body.GetProperty("resultNote").GetString()=="Synthetic result\nSecond line"&&saved.Body.GetProperty("handoverNote").GetString()=="Synthetic handover\nSecond line","result and handover persist separately with newlines");
        var evidencePath=saved.Body.GetProperty("evidence")[0].GetProperty("downloadPath").GetString()!;
        using(var file=await http.GetAsync(evidencePath,ct))Check(file.IsSuccessStatusCode&&file.Content.Headers.ContentType?.MediaType=="image/png","submitter downloads private submitted evidence");
        async Task<string> Deliver(long id)
        {
            await using var deliveryDb=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
            var type=typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskSubmissionNotificationDelivery",true)!;
            var instance=Activator.CreateInstance(type,deliveryDb,TimeProvider.System)!;
            return await (Task<string>)type.GetMethod("DeliverAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(instance,[id,ct])!;
        }
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check(await Deliver(eventId)=="RECIPIENT_READ_DENIED"&&!await db.Notifications.AnyAsync(ct),"revoked Department Head gets no delivery");
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskDeliveryFault ON dbo.notification_recipients AFTER INSERT AS BEGIN THROW 51865,'Synthetic delivery failure',1; END",ct);
            var failed=false;try{await Deliver(eventId);}catch{failed=true;}
            Check(failed&&!await db.Notifications.AnyAsync(ct),"delivery failure rolls back notice and recipient, preserving source intent");
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskDeliveryFault",CancellationToken.None);}
        var deliveries=await Task.WhenAll(Deliver(eventId),Deliver(eventId));Check(deliveries.All(x=>x=="DELIVERED"),"concurrent delivery retries both reconcile the same committed event");
        Check(await db.Notifications.CountAsync(ct)==1&&await db.NotificationRecipients.CountAsync(ct)==1,"delivery retry is idempotent with one recipient per event");
        // Separate browser submission fixture, retaining the first immutable event as historical evidence.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='IN_PROGRESS' WHERE task_id={task}; UPDATE dbo.task_completion_criteria SET outcome=NULL,note=NULL WHERE task_id={task}",ct);await Open();
        var code=taskRow.TaskCode;
        async Task Browser(object fixture)
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintTaskSubmissionBrowser.mjs");using var process=Process.Start(start)!;
            try{await process.StandardInput.WriteAsync(JsonSerializer.Serialize(fixture));process.StandardInput.Close();var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await stdout);_=await stderr;checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"canonical submission browser stage accepted");}
            finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
        }
        await Browser(new{username,password,branch,code,mode="submit"});
        var latest=await Send(HttpMethod.Get,root+"/submission");var latestId=latest.Body.GetProperty("id").GetInt64();
        Check(latestId!=eventId&&latest.Body.GetProperty("criteria")[0].GetProperty("outcome").GetString()=="PASS","browser persists a distinct submission snapshot");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_submission_events WHERE progress_id={eventId} AND JSON_VALUE(criteria_json,'$[0].outcome')='FAIL'").SingleAsync(ct)==1,"later results never rewrite earlier submission snapshot");
        await Deliver(latestId);await Browser(new{username=manager.Username,password,branch,code,task,mode="review"});
        using var managerHttp=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=http.BaseAddress};
        Check((await Send(HttpMethod.Post,"/api/auth/login",new{username=manager.Username,password},client:managerHttp)).Status==200,"manager HTTP login for source reauthorization");
        var notification=await db.Notifications.AsNoTracking().Where(n=>n.SourceType=="TASK_SUBMISSION"&&n.SourceId==latestId).SingleAsync(ct);
        Check((await Send(HttpMethod.Get,$"/api/notifications/{notification.NotificationId}/source",client:managerHttp)).Status==200,"manager opens committed task source");
        await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check((await Send(HttpMethod.Get,$"/api/notifications/{notification.NotificationId}/source",client:managerHttp)).Status==404
            &&(await Send(HttpMethod.Get,root+"/submission",client:managerHttp)).Status==403,"source opening and snapshot reread revalidate current manager authority");
        var hkRole=await db.Roles.Where(r=>r.RoleCode=="HK_STAFF"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
        foreach(var roleCode in new[]{"MAINTENANCE_STAFF","STOREKEEPER"})
        {
            // Independent owned fixture preconditions, not a production Return transition.
            var role=await db.Roles.Where(r=>r.RoleCode==roleCode&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='IN_PROGRESS' WHERE task_id={task}",ct);
            await Open();
            await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,role),ct);
            try
            {
                Check((await Send(HttpMethod.Post,root+"/submit",Payload(),(await Send(HttpMethod.Get,root+"/progress")).Tag)).Status==200
                    && !await db.WorkIntervals.AnyAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct),roleCode+" submits and closes its interval under existing scoped permission");
            }
            finally{await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,hkRole),CancellationToken.None);}
        }
    }
}
