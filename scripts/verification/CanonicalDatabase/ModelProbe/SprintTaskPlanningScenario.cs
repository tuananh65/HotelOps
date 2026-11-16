using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskPlanningScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient executor,HttpClient managerHttp,long actor,User manager,User verifier,
        long branch,long foreign,long department,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{planningCheck=name});}
        var root=$"/api/branches/{branch}/operational-tasks";
        async Task<(int Status,JsonElement Body,string? Tag)> Send(HttpClient client,HttpMethod method,string path,object? body=null,string? tag=null,bool csrf=true)
        {
            using var request=new HttpRequestMessage(method,path);if(body!=null)request.Content=JsonContent.Create(body);if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf&&method!=HttpMethod.Get){var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await client.SendAsync(request,ct);var raw=await response.Content.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(string.IsNullOrWhiteSpace(raw)?"{}":raw);
            return((int)response.StatusCode,json.RootElement.Clone(),response.Headers.ETag?.ToString());
        }
        var category=new WorkCategory{CategoryCode="D3-PLAN",CategoryName="Synthetic operational review",DepartmentId=department,IsActive=true};
        db.WorkCategories.Add(category);
        var priorities=new[]{"LOW","NORMAL","HIGH","CRITICAL"}.Select((code,i)=>new ReferenceValue{GroupCode="PRIORITY",ValueCode=code,DisplayName=code,IsActive=true,SortOrder=i}).ToArray();
        if(await db.ReferenceValues.AnyAsync(r=>r.GroupCode=="PRIORITY",ct))throw new Exception("Empty synthetic priority catalog required");
        db.ReferenceValues.AddRange(priorities);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_category_processes(category_id,process_code) VALUES({category.CategoryId},'OPERATIONAL')",ct);
        CreateOperationalTaskRequest Input()=>new(Guid.NewGuid(),department,category.CategoryId,"Public operational task","Synthetic public creation",
            "Operational result reviewed",[new ProgressDefinition("Result reviewed",true)],priorities[1].ReferenceId,
            DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2),60,null,null,null);
        async Task<string> Fingerprint()=>JsonSerializer.Serialize(new{
            tasks=await db.OperationalTasks.AsNoTracking().OrderBy(t=>t.TaskId).ToArrayAsync(ct),
            definitions=await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT * FROM dbo.operational_task_definitions ORDER BY task_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            criteria=await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT * FROM dbo.task_completion_criteria ORDER BY criterion_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            versions=await db.Database.SqlQuery<string>($"SELECT COALESCE((SELECT task_id,workflow_status,CONVERT(varchar(16),CONVERT(binary(8),row_version),2) AS revision FROM dbo.task_progress_versions ORDER BY task_id FOR JSON PATH),'[]') AS Value").SingleAsync(ct),
            assignments=await db.OperationalTaskAssignments.AsNoTracking().OrderBy(a=>a.AssignmentId).ToArrayAsync(ct),
            intervals=await db.WorkIntervals.AsNoTracking().OrderBy(i=>i.IntervalId).ToArrayAsync(ct),audit=await db.AuditLogs.CountAsync(ct),progress=await db.OperationalTaskProgresses.CountAsync(ct)});
        var options=await Send(managerHttp,HttpMethod.Get,root+$"/creation-options?department={department}");
        Check(options.Status==200&&options.Body.GetProperty("categories").GetArrayLength()==1&&options.Body.GetProperty("priorities").GetArrayLength()==4,"manager loads scoped approved categories and priority catalog");
        var before=await Fingerprint();var input=Input();
        Check((await Send(executor,HttpMethod.Get,root+"/creation-options")).Status==403&&(await Send(executor,HttpMethod.Post,root,input)).Status==403,"executor cannot create or inspect manager creation options");
        Check((await Send(managerHttp,HttpMethod.Post,root,input,csrf:false)).Status==400,"Create requires CSRF");
        using(var anonymous=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer()}){BaseAddress=executor.BaseAddress})
            Check((await Send(anonymous,HttpMethod.Post,root,input)).Status==401,"anonymous Create denied");
        Check((await Send(managerHttp,HttpMethod.Post,$"/api/branches/{foreign}/operational-tasks",input)).Status==403,"Create respects current branch authority");
        foreach(var invalid in new[]{input with{Title=" "},input with{RequestedResult=" "},input with{Criteria=[]},input with{Criteria=[new("Same",true),new(" same ",false)]},input with{DueAt=input.PlannedStartAt!.Value.AddMinutes(-1)},input with{SlaMinutes=0},input with{RoomId=1,AreaId=1},input with{DepartmentId=long.MaxValue},input with{CategoryId=long.MaxValue},input with{PriorityId=long.MaxValue}})
            Check((await Send(managerHttp,HttpMethod.Post,root,invalid)).Status==400,"invalid task definition or planning rejected");
        Check((await Send(managerHttp,HttpMethod.Post,root,input with{InternalRequestId=long.MaxValue})).Status==428,"linked request without source If-Match is rejected before mutation");
        foreach(var process in new[]{"CLEANING","INCIDENT","MATERIAL"}){
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_category_processes SET process_code={process} WHERE category_id={category.CategoryId}",ct);
            Check((await Send(managerHttp,HttpMethod.Post,root,input)).Status==400,"specialized category cannot bypass its workflow: "+process);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.task_category_processes WHERE category_id={category.CategoryId}",ct);
        Check((await Send(managerHttp,HttpMethod.Post,root,input)).Status==400,"unclassified category fails closed");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_category_processes(category_id,process_code) VALUES({category.CategoryId},'OPERATIONAL')",ct);
        await db.WorkCategories.Where(c=>c.CategoryId==category.CategoryId).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.IsActive,false),ct);
        Check((await Send(managerHttp,HttpMethod.Post,root,input)).Status==400,"inactive category cannot create");
        await db.WorkCategories.Where(c=>c.CategoryId==category.CategoryId).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.IsActive,true),ct);
        Check(await Fingerprint()==before,"Create validation and authority failures preserve task, criteria, audit and time");
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskPlanFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51870,'Synthetic creation audit failure',1; END",ct);
            Check((await Send(managerHttp,HttpMethod.Post,root,input)).Status>=500,"Create rejects audit persistence failure");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskPlanFault",CancellationToken.None);}
        Check(await Fingerprint()==before,"Create audit failure rolls back definition, criteria and sole workflow state");
        var creates=await Task.WhenAll(Send(managerHttp,HttpMethod.Post,root,input),Send(managerHttp,HttpMethod.Post,root,input));
        Check(creates.All(r=>r.Status==201)&&creates.Select(r=>r.Body.GetProperty("taskId").GetInt64()).Distinct().Count()==1,"concurrent same-key Create converges to one task");
        var task=creates[0].Body.GetProperty("taskId").GetInt64();var path=root+$"/{task}";var tag=creates[0].Tag!;
        Check(await db.AuditLogs.CountAsync(a=>a.EntityType=="operational_tasks"&&a.EntityId==task.ToString()&&a.Action=="CREATE",ct)==1,"idempotent Create records one audit");
        Check((await Send(managerHttp,HttpMethod.Post,root,input with{Title="Changed payload"})).Status==409,"same key different definition cannot overwrite created task");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_completion_criteria WHERE task_id={task} AND is_required=1 AND outcome IS NULL").SingleAsync(ct)==1,"public Create persists immutable criteria without outcomes");
        var read=await Send(managerHttp,HttpMethod.Get,path+"/plan");
        Check(read.Status==200&&read.Body.GetProperty("requestedResult").GetString()==input.RequestedResult&&read.Body.GetProperty("canCancel").GetBoolean(),"new Pending Assignment plan exposes requested outcome and safe cancellation");
        var plan=new PlanOperationalTaskRequest(priorities[2].ReferenceId,input.PlannedStartAt,input.DueAt!.Value.AddHours(1),90,null);
        Check((await Send(managerHttp,HttpMethod.Put,path+"/plan",plan)).Status==428&&(await Send(managerHttp,HttpMethod.Put,path+"/plan",plan,read.Tag,false)).Status==400,"Plan requires strong ETag and CSRF");
        var updated=await Send(managerHttp,HttpMethod.Put,path+"/plan",plan,read.Tag);
        Check(updated.Status==200&&updated.Tag!=read.Tag,"pending plan update commits new concurrency version");
        Check((await Send(managerHttp,HttpMethod.Put,path+"/plan",plan,read.Tag)).Status==412,"stale planning rejected");
        Check((await Send(managerHttp,HttpMethod.Post,path+"/assignment",new{assigneeId=actor},updated.Tag)).Status==200,"public created task enters verified assignment workflow");
        read=await Send(managerHttp,HttpMethod.Get,path+"/plan");
        Check((await Send(managerHttp,HttpMethod.Put,path+"/plan",plan,read.Tag)).Status==400,"assigned plan change requires reason");
        before=await Fingerprint();
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskPlanFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51870,'Synthetic plan audit failure',1; END",ct);
            Check((await Send(managerHttp,HttpMethod.Put,path+"/plan",plan with{Reason="Deadline updated",SlaMinutes=100},read.Tag)).Status>=500,"assigned plan audit failure rejected");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskPlanFault",CancellationToken.None);}
        Check(await Fingerprint()==before,"Plan audit failure rolls back priority, deadline, duration, progress and version");
        var race=await Task.WhenAll(Send(managerHttp,HttpMethod.Put,path+"/plan",plan with{Reason="Deadline updated",SlaMinutes=100},read.Tag),Send(managerHttp,HttpMethod.Put,path+"/plan",plan with{Reason="Deadline updated",SlaMinutes=100},read.Tag));
        Check(race.Count(r=>r.Status==200)==1&&race.Count(r=>r.Status==412)==1,"concurrent Plan commits exactly one new history event");
        read=await Send(managerHttp,HttpMethod.Get,path+"/plan");
        Check(read.Body.GetProperty("historyTotalCount").GetInt32()==2&&read.Body.GetProperty("history")[1].GetProperty("previousDueAt").GetDateTimeOffset()==input.DueAt,"old deadline retained in ordered plan history");
        var executorRead=await Send(executor,HttpMethod.Get,path+"/plan");
        Check(executorRead.Status==200&&!executorRead.Body.GetProperty("canManage").GetBoolean()&&(await Send(executor,HttpMethod.Put,path+"/plan",plan,read.Tag)).Status==403,"current executor can review plan without gaining manager writes");
        var audit=await db.AuditLogs.Where(a=>a.EntityType=="operational_tasks"&&a.EntityId==task.ToString()&&a.Action=="UPDATE").OrderByDescending(a=>a.AuditId).FirstAsync(ct);
        async Task<string> Deliver(long id){await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);var type=typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskPlanNotificationDelivery",true)!;return await (Task<string>)type.GetMethod("DeliverAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[id,ct])!;}
        await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        try{Check(await Deliver(audit.AuditId)=="RECIPIENT_READ_DENIED","Plan delivery rechecks recipient authorization");}
        finally{await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);}
        Check((await Task.WhenAll(Deliver(audit.AuditId),Deliver(audit.AuditId))).All(r=>r=="DELIVERED"),"Plan notification concurrent retry is idempotent");
        var notice=await db.Notifications.SingleAsync(n=>n.SourceType=="TASK_PLAN"&&n.SourceId==audit.AuditId,ct);
        Check(await db.NotificationRecipients.CountAsync(r=>r.NotificationId==notice.NotificationId,ct)==1&&(await Send(executor,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==200,"Plan notification has one recipient and authorized source");
        Check((await Send(managerHttp,HttpMethod.Post,path+"/cancel",new{reason="short"},read.Tag)).Status==400,"Cancel requires GBR012 reason");
        Check((await Send(executor,HttpMethod.Post,path+"/accept",tag:read.Tag)).Status==200,"assigned public task accepted before cancellation");
        read=await Send(managerHttp,HttpMethod.Get,path+"/plan");before=await Fingerprint();
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskPlanFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51870,'Synthetic cancellation audit failure',1; END",ct);
            Check((await Send(managerHttp,HttpMethod.Post,path+"/cancel",new{reason="Operational request withdrawn"},read.Tag)).Status>=500,"Cancel audit failure rejected");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskPlanFault",CancellationToken.None);}
        Check(await Fingerprint()==before,"Cancel audit failure restores active responsibility and lifecycle");
        race=await Task.WhenAll(Send(managerHttp,HttpMethod.Post,path+"/cancel",new{reason="Operational request withdrawn"},read.Tag),Send(managerHttp,HttpMethod.Post,path+"/cancel",new{reason="Operational request withdrawn"},read.Tag));
        Check(race.Count(r=>r.Status==200)==1&&race.Count(r=>r.Status==412)==1,"concurrent Cancel of Accepted task commits exactly once");
        Check(await db.OperationalTasks.AnyAsync(t=>t.TaskId==task&&t.Status=="CANCELLED"&&t.ActualStartAt==null,ct)&&!await db.OperationalTaskAssignments.AnyAsync(a=>a.TaskId==task&&a.AssignmentStatus=="ACTIVE",ct),"Cancel persists physical and sole lifecycle with ended responsibility and no work time");
        audit=await db.AuditLogs.Where(a=>a.EntityType=="operational_tasks"&&a.EntityId==task.ToString()&&a.Action=="UPDATE").OrderByDescending(a=>a.AuditId).FirstAsync(ct);
        Check(await Deliver(audit.AuditId)=="DELIVERED"&&await Deliver(audit.AuditId)=="DELIVERED","Cancel notification retries converge");
        notice=await db.Notifications.SingleAsync(n=>n.SourceType=="TASK_PLAN"&&n.SourceId==audit.AuditId,ct);
        Check((await Send(executor,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==404,"generic cancellation notification does not restore lost source access");
        read=await Send(managerHttp,HttpMethod.Get,path+"/plan");
        Check(read.Status==200&&!read.Body.GetProperty("canManage").GetBoolean()&&read.Body.GetProperty("historyTotalCount").GetInt32()==3,"Cancelled plan remains historical and immutable");
        Check((await Send(managerHttp,HttpMethod.Put,path+"/plan",plan,read.Tag)).Status==409,"Cancelled task cannot be replanned");
        var worked=await db.OperationalTasks.Where(t=>t.BranchId==branch&&t.ActualStartAt!=null&&t.Status=="TODO").OrderBy(t=>t.TaskId).Select(t=>t.TaskId).FirstAsync(ct);
        var workedPlan=await Send(managerHttp,HttpMethod.Get,root+$"/{worked}/plan");
        Check(workedPlan.Status==200&&!workedPlan.Body.GetProperty("canCancel").GetBoolean()
            &&(await Send(managerHttp,HttpMethod.Post,root+$"/{worked}/cancel",new{reason="Cannot erase prior work"},workedPlan.Tag)).Status==409,"reassigned task with historical work cannot be cancelled despite prestart current state");
        var managerRole=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER").Select(r=>r.RoleId).SingleAsync(ct);
        db.UserRoles.Add(new(){UserId=verifier.UserId,RoleId=managerRole,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintTaskPlanningBrowser.mjs");
        using(var process=Process.Start(start)!){try{
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{manager=manager.Username,executor=username,verifier=verifier.Username,password,branch,department,category=category.CategoryId,priority=priorities[1].ReferenceId,highPriority=priorities[2].ReferenceId,assignee=actor}));process.StandardInput.Close();
            var output=process.StandardOutput.ReadToEndAsync(ct);var error=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await output);_=await error;checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"public Create through Closed and prestart cancellation canonical browser workflows");
            var created=json.RootElement.GetProperty("taskId").GetInt64();
            Check(await db.OperationalTasks.AnyAsync(t=>t.TaskId==created&&t.Status=="COMPLETED"&&t.CompletedAt!=null,ct),"public browser task Closed persists SQL completion");
            Check(await db.WorkIntervals.CountAsync(i=>i.WorkItemKind=="TASK"&&i.WorkItemId==created&&i.ClosedAt!=null,ct)==2&&!await db.WorkIntervals.AnyAsync(i=>i.WorkItemKind=="TASK"&&i.WorkItemId==created&&i.ClosedAt==null,ct),"browser Pause Resume Submit preserve two closed intervals excluding paused time");
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskReopen.V1'").SingleAsync(ct)==1)
            {
                await SprintTaskReopenScenario.RunAsync(db,executor,managerHttp,actor,manager,verifier,branch,foreign,created,checks,ct,username,password);
                // Add a second task through the same public producer, after assertions
                // whose fixture intentionally contains only one Closed task.
                using var second = Process.Start(start)!;
                try
                {
                    var secondOutput = second.StandardOutput.ReadToEndAsync(ct); var secondError = second.StandardError.ReadToEndAsync(ct);
                    await second.StandardInput.WriteAsync(JsonSerializer.Serialize(new{manager=manager.Username,executor=username,verifier=verifier.Username,password,branch,department,category=category.CategoryId,priority=priorities[1].ReferenceId,highPriority=priorities[2].ReferenceId,assignee=actor}));
                    second.StandardInput.Close(); await second.WaitForExitAsync(ct);
                    using var result = JsonDocument.Parse(await secondOutput); _ = await secondError; checks.Add(result.RootElement.Clone());
                    Check(second.ExitCode == 0 && result.RootElement.GetProperty("success").GetBoolean(), "second independent public task completes browser lifecycle for KPI denominator");
                    var secondTask = result.RootElement.GetProperty("taskId").GetInt64();
                    Check(secondTask != created && await db.OperationalTasks.AnyAsync(t=>t.TaskId==secondTask&&t.Status=="COMPLETED"&&t.CompletedAt!=null,ct), "two distinct public tasks persist Closed");
                    Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_verification_events v JOIN dbo.task_submission_events s ON s.progress_id=v.submission_id WHERE s.task_id={secondTask} AND v.decision='ACCEPT'").SingleAsync(ct)==1, "second Closed task contributes one cycle alongside first task's four cycles including workload regression");
                    await SprintTaskCompletionDashboardScenario.RunAsync(db,managerHttp,manager.UserId,branch,created,4,checks,ct,manager.Username,password,expectedTasks:2);
                }
                finally { if(!second.HasExited) second.Kill(entireProcessTree:true); }
            }
        }finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}}
        if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.V1'").SingleAsync(ct) == 1)
            await SprintTaskSeriesScenario.RunAsync(db, executor, managerHttp, actor, manager, branch, foreign, Input(), checks, ct, username, password);
        if (Environment.GetEnvironmentVariable("DEV3_VERIFY_TASK_NOTIFICATION_RETENTION") == "1")
            await SprintTaskNotificationRetentionScenario.RunAsync(db, checks, ct);
    }
}
