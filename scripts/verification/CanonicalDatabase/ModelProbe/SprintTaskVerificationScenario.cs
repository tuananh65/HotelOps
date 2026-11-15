using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskVerificationScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient executor,HttpClient manager,long actor,long verifier,long branch,long foreign,long department,long task,
        List<object> checks,CancellationToken ct,string executorName,string managerName,string password)
    {
        var root=$"/api/branches/{branch}/operational-tasks/{task}";
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{verificationCheck=name});}
        async Task<(int Status,JsonElement Body,string? Tag)> Send(HttpClient client,HttpMethod method,string path,object? body=null,string? tag=null,bool csrf=true)
        {
            using var request=new HttpRequestMessage(method,path);if(body!=null)request.Content=JsonContent.Create(body);if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf&&method!=HttpMethod.Get){var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await client.SendAsync(request,ct);var raw=await response.Content.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(string.IsNullOrWhiteSpace(raw)?"{}":raw);
            return((int)response.StatusCode,json.RootElement.Clone(),response.Headers.ETag?.ToString());
        }
        async Task<string> Fingerprint()=>JsonSerializer.Serialize(new{
            task=await db.OperationalTasks.AsNoTracking().Where(t=>t.TaskId==task).Select(t=>new{t.Status,t.CompletedAt,t.UpdatedAt,t.ActualStartAt}).SingleAsync(ct),
            assignments=await db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.TaskId==task).Select(a=>new{a.AssignmentId,a.AssignmentStatus,a.EndedAt}).ToArrayAsync(ct),
            review=(await Send(manager,HttpMethod.Get,root+"/verification")).Tag,
            decisions=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_verification_events").SingleAsync(ct),
            audits=await db.AuditLogs.CountAsync(ct),history=await db.OperationalTaskProgresses.CountAsync(ct),notices=await db.Notifications.CountAsync(ct)});
        var review=await Send(manager,HttpMethod.Get,root+"/verification");
        Check(review.Status==200&&review.Body.GetProperty("canDecide").GetBoolean(),"Department Head reads actionable pending verification");
        var submission=review.Body.GetProperty("submissionId").GetInt64();var tag=review.Tag!;
        object Decision(string code="RETURN",string note="Please correct the operational result",long? source=null)=>new{submissionId=source??submission,decision=code,note};
        var before=await Fingerprint();
        using(var anonymous=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer()}){BaseAddress=manager.BaseAddress})
            Check((await Send(anonymous,HttpMethod.Post,root+"/verification",Decision(),tag)).Status==401,"anonymous verification denied with valid CSRF");
        Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision())).Status==428,"verification requires ETag");
        Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(),tag,false)).Status==400,"verification requires CSRF");
        Check((await Send(executor,HttpMethod.Post,root+"/verification",Decision(),tag)).Status==403,"executor cannot impersonate verifier");
        Check((await Send(manager,HttpMethod.Post,$"/api/branches/{foreign}/operational-tasks/{task}/verification",Decision(),tag)).Status==403,"foreign branch verification denied");
        Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(note:"short"),tag)).Status==400
            &&(await Send(manager,HttpMethod.Post,root+"/verification",Decision("ACCEPT","  "),tag)).Status==400,"Return minimum reason and Accept verification note enforced");
        Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(source:long.MaxValue),tag)).Status==412,"another submission identity rejected");
        Check(await Fingerprint()==before,"validation and authorization denials leave all workflow records unchanged");
        var managerRole=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER").Select(r=>r.RoleId).SingleAsync(ct);
        var selfRole=new UserRole{UserId=actor,RoleId=managerRole,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)};
        db.UserRoles.Add(selfRole);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        try{var self=await Send(executor,HttpMethod.Post,root+"/verification",Decision("ACCEPT","Self verification forbidden"),tag);Check(self.Status==403&&self.Body.GetProperty("messageCode").GetString()=="MSG22","submitter with manager authority still cannot verify own cycle");}
        finally{await db.UserRoles.Where(r=>r.UserRoleId==selfRole.UserRoleId).ExecuteDeleteAsync(ct);}
        await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.CreatedBy,verifier),ct);
        try{Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(),(await Send(manager,HttpMethod.Get,root+"/verification")).Tag)).Status==403,"requester cannot verify even when distinct from submitter");}
        finally{await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.CreatedBy,actor),ct);}
        var managerDay=new Workday{UserId=verifier,BranchId=branch,DepartmentId=department,WorkDate=DateOnly.FromDateTime(DateTime.UtcNow),StartedAt=DateTime.UtcNow.AddMinutes(-4),Status="WORKING",IsUnscheduled=true,UnscheduledReason="Synthetic prior performer separation"};
        db.Workdays.Add(managerDay);await db.SaveChangesAsync(ct);
        var contribution=new WorkInterval{UserId=verifier,BranchId=branch,WorkdayId=managerDay.WorkdayId,WorkItemKind="TASK",WorkItemId=task,OpenedBy=verifier,ClosedBy=verifier,OpenedAt=DateTime.UtcNow.AddMinutes(-3),ClosedAt=DateTime.UtcNow.AddMinutes(-2),EffectiveStartedAt=DateTime.UtcNow.AddMinutes(-3),EffectiveEndedAt=DateTime.UtcNow.AddMinutes(-2)};
        db.WorkIntervals.Add(contribution);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        try{Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(),(await Send(manager,HttpMethod.Get,root+"/verification")).Tag)).Status==403,"earlier performer in same cycle cannot verify another submitter");}
        finally{await db.WorkIntervals.Where(i=>i.IntervalId==contribution.IntervalId).ExecuteDeleteAsync(ct);await db.Workdays.Where(d=>d.WorkdayId==managerDay.WorkdayId).ExecuteDeleteAsync(ct);}
        await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceId,long.MaxValue).SetProperty(t=>t.SourceType,"INTERNAL_REQUEST"),ct);
        try{Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(),(await Send(manager,HttpMethod.Get,root+"/verification")).Tag)).Status==503,"unknown linked requester provenance fails closed");}
        finally{await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceId,(long?)null).SetProperty(t=>t.SourceType,(string?)null),ct);}
        await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceType,"INTERNAL_REQUEST"),ct);
        try{Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(),(await Send(manager,HttpMethod.Get,root+"/verification")).Tag)).Status==503,"linked source without its identity also fails closed");}
        finally{await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceType,(string?)null),ct);}
        tag=(await Send(manager,HttpMethod.Get,root+"/verification")).Tag!;before=await Fingerprint();
        try{
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_TaskVerificationFault ON dbo.task_verification_events AFTER INSERT AS BEGIN THROW 51866,'Synthetic verification persistence failure',1; END",ct);
            Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision(),tag)).Status>=500&&await Fingerprint()==before,"verification failure rolls back audit, decision, workflow, assignment and notification intent");
        }finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_TaskVerificationFault",CancellationToken.None);}
        var firstStart=(await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct)).ActualStartAt;
        var races=await Task.WhenAll(Send(manager,HttpMethod.Post,root+"/verification",Decision(),tag),Send(manager,HttpMethod.Post,root+"/verification",Decision(),tag));
        Check(races.Select(r=>r.Status).Order().SequenceEqual(new[]{200,412}),"concurrent Return yields one committed decision and one stale response");
        Check((await Send(executor,HttpMethod.Get,root+"/progress")).Body.GetProperty("state").GetString()=="ACCEPTED"
            &&await db.OperationalTaskAssignments.CountAsync(a=>a.TaskId==task&&a.AssigneeId==actor&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null,ct)==1,"Return preserves the same assignment and requires Start again");
        async Task<string> Deliver(long id){await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);var type=typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Notifications.TaskVerificationNotificationDelivery",true)!;return await (Task<string>)type.GetMethod("DeliverAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Activator.CreateInstance(type,context,TimeProvider.System),[id,ct])!;}
        await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        try{Check(await Deliver(submission)=="RECIPIENT_READ_DENIED","verification delivery revalidates assignee access");}
        finally{await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);}
        Check((await Task.WhenAll(Deliver(submission),Deliver(submission))).All(r=>r=="DELIVERED")&&await db.Notifications.CountAsync(n=>n.SourceType=="TASK_VERIFICATION"&&n.SourceId==submission,ct)==1,"verification concurrent delivery is idempotent");
        async Task Browser(string mode,string name,string note)
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintTaskVerificationBrowser.mjs");using var process=Process.Start(start)!;
            try{await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=name,password,branch,task,mode,note}));process.StandardInput.Close();var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await stdout);_=await stderr;checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"canonical verification browser "+mode+" accepted");}finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
        }
        await Browser("resubmit",executorName,"First corrected task result");
        Check((await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct)).ActualStartAt==firstStart,"Start after Return preserves first actual start");
        var requesterSource=await SprintTaskRequesterScenario.RunAsync(db,manager,actor,verifier,branch,foreign,department,task,checks,ct);
        // Return does not start the new processing cycle described by UC106.
        // A performer before Return remains excluded after a new submission.
        managerDay.WorkdayId=0;db.Workdays.Add(managerDay);await db.SaveChangesAsync(ct);
        contribution.IntervalId=0;contribution.WorkdayId=managerDay.WorkdayId;db.WorkIntervals.Add(contribution);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        try{
            var later=await Send(manager,HttpMethod.Get,root+"/verification");
            Check(!later.Body.GetProperty("canDecide").GetBoolean()&&(await Send(manager,HttpMethod.Post,root+"/verification",Decision(source:later.Body.GetProperty("submissionId").GetInt64()),later.Tag)).Status==403,"prior performer remains excluded across Return and resubmission");
        }finally{await db.WorkIntervals.Where(i=>i.IntervalId==contribution.IntervalId).ExecuteDeleteAsync(ct);await db.Workdays.Where(d=>d.WorkdayId==managerDay.WorkdayId).ExecuteDeleteAsync(ct);}
        await Browser("RETURN",managerName,"Please fix the final operational issue");
        submission=(await Send(manager,HttpMethod.Get,root+"/verification")).Body.GetProperty("submissionId").GetInt64();await Deliver(submission);
        await Browser("resubmit",executorName,"Final corrected task result");
        await Browser("ACCEPT",managerName,"Verified all submitted operational results");
        var closed=await Send(manager,HttpMethod.Get,root+"/verification");submission=closed.Body.GetProperty("submissionId").GetInt64();
        Check(closed.Body.GetProperty("state").GetString()=="CLOSED"&&closed.Body.GetProperty("decisions").GetArrayLength()==3,"two Return cycles and final Accept persist distinct verifier history");
        var decisionRows=closed.Body.GetProperty("decisions").EnumerateArray().ToArray();
        Check(decisionRows[0].GetProperty("requestedBy").GetInt64()==actor&&decisionRows.Skip(1).All(d=>d.GetProperty("requestedBy").GetInt64()==requesterSource.RequesterId),
            "direct history retains original requester; linked Return and Accept persist official Internal Request requester");
        var linkedAudits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="operational_tasks"&&a.EntityId==task.ToString()&&a.NewValues!=null&&a.NewValues.Contains("requesterSource")).ToArrayAsync(ct);
        Check(linkedAudits.Count(a=>{using var j=JsonDocument.Parse(a.NewValues!);return j.RootElement.GetProperty("requesterSource").GetProperty("type").GetString()=="INTERNAL_REQUEST"
            &&j.RootElement.GetProperty("requesterSource").GetProperty("id").GetInt64()==requesterSource.RequestId&&j.RootElement.GetProperty("taskCreatedBy").GetInt64()==actor;})==2,
            "linked verification audit preserves exact source identity and separate task creator");
        Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==requesterSource.RequestId&&r.RequesterId==requesterSource.RequesterId&&r.Status=="IN_PROGRESS"&&r.UpdatedAt==null,ct),
            "operational task verification does not mutate the source request lifecycle");
        var seconds=await db.Database.SqlQuery<long>($"SELECT COALESCE(SUM(DATEDIFF_BIG(second,effective_started_at,effective_ended_at)),0) AS Value FROM dbo.work_intervals WHERE work_item_kind='TASK' AND work_item_id={task} AND closed_at IS NOT NULL").SingleAsync(ct);
        Check(closed.Body.GetProperty("actualEffortSeconds").GetInt64()==seconds&&!await db.WorkIntervals.AnyAsync(i=>i.WorkItemKind=="TASK"&&i.WorkItemId==task&&i.ClosedAt==null,ct),"reviewed actual effort equals closed effective intervals, with no time opened by Return or Verify");
        Check(await db.OperationalTaskAssignments.CountAsync(a=>a.TaskId==task&&a.AssignmentStatus=="ACTIVE",ct)==0&&await db.OperationalTasks.AnyAsync(t=>t.TaskId==task&&t.Status=="COMPLETED"&&t.CompletedAt!=null,ct),"Accept closes parent and current assignment atomically");
        Check((await Send(manager,HttpMethod.Post,root+"/verification",Decision("ACCEPT","Cannot verify closed task"),closed.Tag)).Status==409,"closed task refuses another decision");
        await Deliver(submission);await Browser("closed",executorName,"Verified all submitted operational results");
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="TASK_VERIFICATION"&&n.SourceId==submission,ct);
        await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        try{Check((await Send(executor,HttpMethod.Get,$"/api/notifications/{notice.NotificationId}/source")).Status==404&&(await Send(executor,HttpMethod.Get,root+"/submission")).Status==403,"closed-task source and private submission revalidate current reader authority");}
        finally{await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);}
    }
}
