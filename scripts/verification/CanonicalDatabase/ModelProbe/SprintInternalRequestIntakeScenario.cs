using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintInternalRequestIntakeScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long sourceId,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{intakeCheck=label});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==sourceId,ct);
        var branch=source.BranchId;var department=source.ReceivingDepartmentId!.Value;
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View internal requests",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        foreach(var permission in new[]{create,view})
            if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var material=new InternalRequestType{TypeCode="D3MAT"+Guid.NewGuid().ToString("N")[..8],TypeName="Synthetic material need",IsActive=true};
        var unknown=new InternalRequestType{TypeCode="D3UNK"+Guid.NewGuid().ToString("N")[..8],TypeName="Synthetic unclassified",IsActive=true};
        db.InternalRequestTypes.AddRange(material,unknown);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_type_policy(request_type_id,is_material_need) VALUES({source.RequestTypeId},0),({material.RequestTypeId},1)",ct);
        var priorityRow=await db.ReferenceValues.SingleOrDefaultAsync(r=>r.GroupCode=="PRIORITY"&&r.ValueCode=="NORMAL",ct);
        if(priorityRow==null){priorityRow=new(){GroupCode="PRIORITY",ValueCode="NORMAL",DisplayName="Synthetic normal priority",IsActive=true,SortOrder=1};db.ReferenceValues.Add(priorityRow);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();}
        Check(priorityRow.IsActive,"synthetic priority is active");
        var priority=priorityRow.ReferenceId;
        var foreignDepartment=await db.Departments.Where(d=>d.BranchId!=branch&&d.IsActive).Select(d=>d.DepartmentId).FirstAsync(ct);
        var bytes=System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\nSynthetic evidence\n%%EOF");
        var upload=new EvidenceUpload("synthetic.pdf","application/pdf",Convert.ToBase64String(bytes));
        InternalRequestIntakeInput Input()=>new(Guid.NewGuid(),branch,source.RequestTypeId,department,"Synthetic HTTP intake","Synthetic service",priority,DateTimeOffset.UtcNow.AddDays(1),Files:[upload]);
        async Task<(int Status,JsonElement Body)> Post(InternalRequestIntakeInput value,bool csrf=true)
        {
            using var message=new HttpRequestMessage(HttpMethod.Post,"/api/internal-requests/intake"){Content=JsonContent.Create(value)};
            if(csrf){var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);message.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(message,ct);
            var body=response.Content.Headers.ContentType?.MediaType?.Contains("json")==true?await response.Content.ReadFromJsonAsync<JsonElement>(ct):default;
            return((int)response.StatusCode,body);
        }
        using(var options=await http.GetAsync($"/api/internal-requests/intake?branchId={branch}",ct)){
            Check((int)options.StatusCode==200,"authorized intake options available");
            var refs=await options.Content.ReadFromJsonAsync<JsonElement>(ct);
            Check(refs.GetProperty("branches").EnumerateArray().All(b=>b.GetProperty("id").GetInt64()==branch)
                &&refs.GetProperty("departments").EnumerateArray().All(d=>d.GetProperty("id").GetInt64()!=foreignDepartment),"options exclude other branches and departments");
        }
        Check((await Post(Input(),false)).Status==400,"CSRF mandatory for creation");
        Check((await Post(Input() with{DepartmentId=foreignDepartment})).Status==400,"foreign receiving department rejected");
        Check((await Post(Input() with{TypeId=unknown.RequestTypeId})).Status==400,"unclassified type fails closed");
        var materialResult=await Post(Input() with{TypeId=material.RequestTypeId});
        Check(materialResult.Status==400&&materialResult.Body.GetProperty("code").GetString()=="MATERIAL_REQUEST_REQUIRED","material need directed to UC091");
        await db.InternalRequestTypes.Where(t=>t.RequestTypeId==source.RequestTypeId).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.IsActive,false),ct);
        Check((await Post(Input())).Status==400,"inactive type rejected");
        await db.InternalRequestTypes.Where(t=>t.RequestTypeId==source.RequestTypeId).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.IsActive,true),ct);
        Check((await Post(Input() with{DesiredDeadline=DateTimeOffset.UtcNow.AddDays(-1)})).Status==400,"past desired deadline rejected");
        Check((await Post(Input() with{PriorityId=long.MaxValue})).Status==400,"invalid priority rejected");
        Check((await Post(Input() with{Files=[upload with{MimeType="image/png"}]})).Status==400,"attachment content and MIME mismatch rejected");
        await db.Departments.Where(d=>d.DepartmentId==department).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.IsActive,false),ct);
        Check((await Post(Input())).Status==400,"inactive receiving department rejected");
        await db.Departments.Where(d=>d.DepartmentId==department).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.IsActive,true),ct);
        await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
        Check((await Post(Input())).Status==403,"revoked branch access denies creation");
        await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,(DateTime?)null),ct);
        var grant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==create.PermissionId,ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
        Check((await Post(Input())).Status==403,"IR_VIEW and processing authority cannot replace IR_CREATE");
        db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=create.PermissionId,DataScope=grant.DataScope,GrantedAt=grant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var root=Environment.GetEnvironmentVariable("Evidence__PrivateRoot")??throw new Exception("Owned evidence root required");
        int Files()=>Directory.Exists(root)?Directory.GetFiles(root).Length:0;
        var before=new[]{await db.InternalRequests.CountAsync(ct),await db.InternalRequestEvents.CountAsync(ct),await db.AuditLogs.CountAsync(ct),await db.Attachments.CountAsync(ct),Files()};
        await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER dbo.TR_DEV3_IntakeFault ON dbo.internal_request_intake_outbox AFTER INSERT AS THROW 51903,'Synthetic intake fault',1;",ct);
        try{Check((await Post(Input())).Status>=500,"outbox fault rejects creation");}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_IntakeFault",CancellationToken.None);}
        var after=new[]{await db.InternalRequests.CountAsync(ct),await db.InternalRequestEvents.CountAsync(ct),await db.AuditLogs.CountAsync(ct),await db.Attachments.CountAsync(ct),Files()};
        Check(before.SequenceEqual(after),"request event audit attachment and private file roll back with outbox");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("frontend/scripts/sprintInternalRequestIntakeBrowser.mjs");
        JsonElement proof;
        using(var browser=Process.Start(start)??throw new Exception("Intake browser unavailable"))
        {
            var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
            try{
                await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,department,type=source.RequestTypeId,material=material.RequestTypeId,priority}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
                proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;
                if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Intake browser failed at "+proof.GetProperty("stage").GetString());
                checks.Add(new{intakeBrowser=proof});
            }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        }
        var request=proof.GetProperty("requestId").GetInt64();var eventId=proof.GetProperty("eventId").GetInt64();
        Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.RequesterId==actor&&r.ReceivingDepartmentId==department&&r.Status=="PENDING_ACCEPTANCE"&&r.RequiredAt!=null&&r.DueAt==null,ct)
            &&await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request&&e.EventType=="CREATE"&&e.ActorId==actor,ct)==1
            &&await db.AuditLogs.CountAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()&&a.Action=="CREATE"&&a.ActorUserId==actor,ct)==1,"browser creates one pending request with event and audit");
        var attachment=await db.Attachments.AsNoTracking().SingleAsync(a=>a.EntityType=="INTERNAL_REQUEST_INTAKE"&&a.EntityId==eventId,ct);
        using(var download=await http.GetAsync($"/api/internal-requests/{request}/progress/evidence/{attachment.AttachmentId}",ct))Check((int)download.StatusCode==200,"intake evidence download uses independent source read authorization");
        var readGrant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==readGrant.RolePermissionId).ExecuteDeleteAsync(ct);
        using(var download=await http.GetAsync($"/api/internal-requests/{request}/progress/evidence/{attachment.AttachmentId}",ct))Check((int)download.StatusCode==404,"IR_CREATE does not authorize evidence after IR_VIEW revocation");
        db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope=readGrant.DataScope,GrantedAt=readGrant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var delivery=new InternalRequestIntakeDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={department}",ct);
        Check(await delivery.DeliverAsync(eventId,actor,ct)=="RECIPIENT_READ_DENIED","revoked manager mapping denies delivery");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch} AND department_id={department}",ct);
        Check(await delivery.DeliverAsync(eventId,actor,ct)=="DELIVERED"&&await delivery.DeliverAsync(eventId,actor,ct)=="ALREADY_DELIVERED","notification delivered once after commit");
        var notice=await db.Notifications.SingleAsync(n=>n.SourceType=="INTERNAL_REQUEST_INTAKE"&&n.SourceId==eventId,ct);
        using(var resolved=await http.GetAsync($"/api/notifications/{notice.NotificationId}/source",ct))Check((int)resolved.StatusCode==200,"notification opens current authorized source");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={department}",ct);
        using(var denied=await http.GetAsync($"/api/notifications/{notice.NotificationId}/source",ct))Check((int)denied.StatusCode==404,"source rechecks manager mapping after delivery");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch} AND department_id={department}",ct);
        var race=Input();var responses=await Task.WhenAll(Post(race),Post(race));
        Check(responses.Select(r=>r.Status).Order().SequenceEqual(new[]{201,409})&&await db.InternalRequests.CountAsync(r=>r.RequestNo=="IR-"+race.SubmissionId.ToString("N"),ct)==1,"concurrent same submission commits once and duplicate returns409");
        var raceEvent=responses.Single(r=>r.Status==201).Body.GetProperty("eventId").GetInt64();
        var workerDelivered=false;
        for(var attempt=0;attempt<80;attempt++)
        {
            if(await db.NotificationRecipients.AnyAsync(r=>r.UserId==actor&&r.DeliveryStatus=="DELIVERED"&&r.Notification.SourceType=="INTERNAL_REQUEST_INTAKE"&&r.Notification.SourceId==raceEvent,ct)){workerDelivered=true;break;}
            await Task.Delay(1000,ct);
        }
        Check(workerDelivered,"hosted worker automatically delivers committed intake intent");
        start.ArgumentList.Clear();start.ArgumentList.Add("frontend/scripts/sprintInternalRequestIntakeNoticeBrowser.mjs");
        using(var browser=Process.Start(start)??throw new Exception("Notice browser unavailable"))
        {
            var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
            try{
                await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request,notice=notice.NotificationId}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
                var noticeProof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;
                if(browser.ExitCode!=0||!noticeProof.GetProperty("success").GetBoolean())throw new Exception("Intake notice browser failed at "+noticeProof.GetProperty("stage").GetString());
                checks.Add(new{intakeNoticeBrowser=noticeProof});
            }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        }
        Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="ACCEPTED"&&r.DueAt!=null,ct)
            &&await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request&&e.EventType=="ACCEPT",ct)==1,"created request moves through notification to accepted with persisted decision");
    }
}
