using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestHandoverScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long request,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{handoverCheck=label});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);var branch=source.BranchId;var department=source.ReceivingDepartmentId!.Value;
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View requests",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var name="D3HAND"+Guid.NewGuid().ToString("N")[..8];
        var receiver=new User{Username=name,EmployeeCode=name,FullName="Synthetic handover receiver",Email=name+"@example.invalid",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",DepartmentId=department,CreatedAt=DateTime.UtcNow};
        receiver.PasswordHash=new PasswordHasher<User>().HashPassword(receiver,password);db.Users.Add(receiver);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        db.UserRoles.Add(new(){UserId=receiver.UserId,RoleId=role.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
        db.UserBranchAccesses.Add(new(){UserId=receiver.UserId,BranchId=branch,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
        var assignment=new InternalRequestAssignment{InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow.AddMinutes(-1),Status="ACTIVE"};
        db.InternalRequestAssignments.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,department),ct);
        using var cookies=new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false};using var receiverHttp=new HttpClient(cookies){BaseAddress=http.BaseAddress};
        var loginToken=await receiverHttp.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);
        using(var login=new HttpRequestMessage(HttpMethod.Post,"/api/auth/login"){Content=JsonContent.Create(new{username=name,password})}){
            login.Headers.Add("X-CSRF-TOKEN",loginToken.GetProperty("csrfToken").GetString());using var response=await receiverHttp.SendAsync(login,ct);Check((int)response.StatusCode==200,"receiver real session authenticated");
        }
        var path=$"/api/internal-requests/{request}/handover";
        async Task<JsonElement> Read(HttpClient client){using var response=await client.GetAsync(path,ct);Check((int)response.StatusCode==200,"authorized handover read");return await response.Content.ReadFromJsonAsync<JsonElement>(ct);}
        async Task<(int Status,JsonElement Body)> Post(HttpClient client,string suffix,object body,string? version,bool csrf=true){
            using var message=new HttpRequestMessage(HttpMethod.Post,path+suffix){Content=JsonContent.Create(body)};
            if(version!=null)message.Headers.Add("If-Match",'"'+version+'"');
            if(csrf){var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);message.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await client.SendAsync(message,ct);return((int)response.StatusCode,response.Content.Headers.ContentType?.MediaType?.Contains("json")==true?await response.Content.ReadFromJsonAsync<JsonElement>(ct):default);
        }
        var input=new{receiverId=receiver.UserId,summary="Synthetic handover summary",note="Synthetic note"};
        var initial=await Read(http);var version=initial.GetProperty("version").GetString()!;
        Check(initial.GetProperty("canSubmit").GetBoolean()&&!(await Read(receiverHttp)).GetProperty("canSubmit").GetBoolean(),"only current handler can submit despite manager read/process role");
        Check((await Post(http,"",input,version,false)).Status==400&&(await Post(http,"",input,null)).Status==428,"CSRF and request ETag mandatory");
        Check((await Post(receiverHttp,"",new{receiverId=actor,summary="Unauthorized sender",note=(string?)null},version)).Status==403,"noncurrent handler cannot initiate handover");
        Check((await Post(http,"",new{receiverId=receiver.UserId,summary=" ",note=(string?)null},version)).Status==400,"summary mandatory");
        await db.Users.Where(u=>u.UserId==receiver.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,(long?)null),ct);
        Check((await Post(http,"",input,version)).Status==403,"receiver outside receiving department denied");
        await db.Users.Where(u=>u.UserId==receiver.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,department),ct);
        await db.UserBranchAccesses.Where(a=>a.UserId==receiver.UserId&&a.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
        Check((await Post(http,"",input,version)).Status==403,"receiver without current branch access denied");
        await db.UserBranchAccesses.Where(a=>a.UserId==receiver.UserId&&a.BranchId==branch).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,(DateTime?)null),ct);
        await db.UserRoles.Where(r=>r.UserId==receiver.UserId&&r.RoleId==role.RoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check((await Post(http,"",input,version)).Status==403,"receiver revoked role denied");
        await db.UserRoles.Where(r=>r.UserId==receiver.UserId&&r.RoleId==role.RoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        var audit=await db.AuditLogs.CountAsync(ct);var events=await db.InternalRequestEvents.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER dbo.TR_DEV3_HandoverFault ON dbo.internal_request_handover_outbox AFTER INSERT AS THROW 51903,'Synthetic handover fault',1;",ct);
        try{Check((await Post(http,"",input,version)).Status>=500,"outbox failure rejects submit");}finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_HandoverFault",CancellationToken.None);}
        Check((await Read(http)).GetProperty("version").GetString()==version&&await db.AuditLogs.CountAsync(ct)==audit&&await db.InternalRequestEvents.CountAsync(ct)==events,"submit fault rolls back event audit and request version");
        var submitted=await Post(http,"",input,version);Check(submitted.Status==200,"valid submit creates pending handover");var handover=submitted.Body.GetProperty("handoverId").GetInt64();version=submitted.Body.GetProperty("version").GetString()!;
        Check(await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==request&&a.Status=="ACTIVE"&&a.AssigneeId==actor,ct)==1,"pending handover preserves sender responsibility");
        Check((await Post(http,"",input,version)).Status==409,"second pending handover rejected");
        using(var reassign=new HttpRequestMessage(HttpMethod.Post,$"/api/internal-requests/{request}/assignment"){Content=JsonContent.Create(new{handlerId=receiver.UserId,note="Synthetic bypass attempt"})}){
            var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);reassign.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());reassign.Headers.Add("If-Match",'"'+version+'"');
            using var denied=await http.SendAsync(reassign,ct);Check((int)denied.StatusCode==409,"manager reassignment cannot bypass pending acknowledgment");
        }
        var alternate=await db.Departments.Where(d=>d.BranchId==branch&&d.DepartmentId!=department).Select(d=>d.DepartmentId).SingleAsync(ct);
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,alternate),ct);
        var routed=await Read(receiverHttp);
        Check(!routed.GetProperty("canDecide").GetBoolean()&&(await Post(receiverHttp,"/decision",new{handoverId=handover,acknowledge=true,reason=(string?)null},routed.GetProperty("version").GetString())).Status==409,"receiving department change invalidates pending decision");
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,department),ct);
        version=(await Read(http)).GetProperty("version").GetString()!;
        audit=await db.AuditLogs.CountAsync(ct);events=await db.InternalRequestEvents.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER dbo.TR_DEV3_HandoverFault ON dbo.internal_request_handover_outbox AFTER INSERT AS THROW 51903,'Synthetic handover acknowledgment fault',1;",ct);
        try{Check((await Post(receiverHttp,"/decision",new{handoverId=handover,acknowledge=true,reason=(string?)null},version)).Status>=500,"acknowledgment outbox fault rejected");}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_HandoverFault",CancellationToken.None);}
        Check((await Read(http)).GetProperty("version").GetString()==version&&await db.AuditLogs.CountAsync(ct)==audit&&await db.InternalRequestEvents.CountAsync(ct)==events
            &&await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==request,ct)==1&&await db.InternalRequestAssignments.AnyAsync(a=>a.AssignmentId==assignment.AssignmentId&&a.Status=="ACTIVE"&&a.EndedAt==null,ct),"acknowledgment rollback restores original assignment handover event audit and version");
        var reject=new{handoverId=handover,acknowledge=false,reason="Synthetic direct rejection"};
        Check((await Post(http,"/decision",reject,version)).Status==403,"only designated receiver may decide");
        Check((await Post(receiverHttp,"/decision",new{handoverId=handover,acknowledge=false,reason=" "},version)).Status==400,"reject requires reason");
        Check((await Post(receiverHttp,"/decision",reject,version)).Status==200&&await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==request&&a.Status=="ACTIVE"&&a.AssigneeId==actor,ct)==1,"reject leaves sender sole active handler");
        Check((await Post(receiverHttp,"/decision",reject,version)).Status==412,"stale decision cannot replay");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintInternalRequestHandoverBrowser.mjs");
        using(var browser=Process.Start(start)??throw new Exception("Handover browser unavailable")){
            var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
            try{await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request,receiver=receiver.UserId,receiverName=name}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
                var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;checks.Add(new{handoverBrowser=proof});if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Handover browser failed at "+proof.GetProperty("stage").GetString());
            }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        }
        Check(await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==request,ct)==2
            &&await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==request&&a.Status=="ACTIVE"&&a.AssigneeId==receiver.UserId&&a.EndedAt==null,ct)==1
            &&await db.InternalRequestAssignments.AnyAsync(a=>a.AssignmentId==assignment.AssignmentId&&a.Status=="REASSIGNED"&&a.EndedAt!=null,ct),"browser acknowledgment atomically replaces sole handler; rejection created no assignment");
        Check(await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request&&e.EventType=="HANDOVER_ACKNOWLEDGE",ct)==1
            &&await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request&&e.EventType=="HANDOVER_REJECT",ct)==2,"acknowledgment and both rejections persist event history");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_handover_outbox WHERE recipient_kind='SENDER'").SingleAsync(ct)==1,"acknowledgment persists deduplicated sender notification intent");
        version=(await Read(receiverHttp)).GetProperty("version").GetString()!;
        var reverse=await Post(receiverHttp,"",new{receiverId=actor,summary="Synthetic hand back",note=(string?)null},version);
        Check(reverse.Status==200,"new current handler can initiate subsequent handover");
        var decision=new{handoverId=reverse.Body.GetProperty("handoverId").GetInt64(),acknowledge=true,reason=(string?)null};version=reverse.Body.GetProperty("version").GetString()!;
        var race=await Task.WhenAll(Post(http,"/decision",decision,version),Post(http,"/decision",decision,version));
        Check(race.Select(r=>r.Status).Order().SequenceEqual(new[]{200,412})&&await db.InternalRequestAssignments.CountAsync(a=>a.InternalRequestId==request&&a.Status=="ACTIVE"&&a.AssigneeId==actor,ct)==1,"concurrent acknowledgments commit once and retain exactly one handler");
        if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_HANDOVER_DELIVERY")!="1")
        {
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
        InternalRequestHandoverDelivery Delivery(HotelOpsDbContext context)=>new(context,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
        var lastEvent=await db.InternalRequestEvents.AsNoTracking().Where(e=>e.InternalRequestId==request&&e.EventType=="HANDOVER_ACKNOWLEDGE").MaxAsync(e=>e.EventId,ct);
        db.ChangeTracker.Clear();
        var noticesBefore=await db.Notifications.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_HandoverDeliveryFault ON dbo.internal_request_handover_outbox AFTER UPDATE AS THROW 51914,'Synthetic delivery fault',1;",ct);
        var failed=false;
        try{await Delivery(db).DeliverAsync(lastEvent,receiver.UserId,ct);}catch(Microsoft.Data.SqlClient.SqlException){failed=true;}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_HandoverDeliveryFault",CancellationToken.None);}
        Check(failed&&await db.Notifications.CountAsync(ct)==noticesBefore
            &&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_handover_outbox WHERE event_id={lastEvent} AND recipient_id={receiver.UserId} AND notification_id IS NULL").SingleAsync(ct)==1,
            "delivery fault rolls back notification recipient and outbox receipt");
        async Task<string> Deliver(){await using var context=new HotelOpsDbContext(options);return await Delivery(context).DeliverAsync(lastEvent,receiver.UserId,ct);}
        var deliveries=await Task.WhenAll(Deliver(),Deliver());
        Check(deliveries.Order().SequenceEqual(new[]{"ALREADY_DELIVERED","DELIVERED"}),"concurrent delivery creates one notification per recipient event");
        var notification=await db.Database.SqlQuery<long>($"SELECT notification_id AS Value FROM dbo.internal_request_handover_outbox WHERE event_id={lastEvent} AND recipient_id={receiver.UserId}").SingleAsync(ct);
        using(var opened=await receiverHttp.GetAsync($"/api/notifications/{notification}/source",ct))Check((int)opened.StatusCode==200,"previous handler with independent read scope opens notification source");
        var grant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
        Check(await Delivery(db).DeliverAsync(lastEvent,receiver.UserId,ct)=="RECIPIENT_READ_DENIED","delivery rechecks revoked IR_VIEW despite historical responsibility");
        using(var denied=await receiverHttp.GetAsync($"/api/notifications/{notification}/source",ct))Check((int)denied.StatusCode==404,"notification source rejects revoked IR_VIEW");
        db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope=grant.DataScope,GrantedAt=grant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={department}",ct);
        Check(await Delivery(db).DeliverAsync(lastEvent,actor,ct)=="RECIPIENT_READ_DENIED","manager notice requires current exact department mapping");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch} AND department_id={department}",ct);
        Check(await Delivery(db).DeliverAsync(lastEvent,actor,ct)=="DELIVERED","eligible mapped manager receives acknowledgment notice");
        foreach(var pending in await Delivery(db).PendingAsync(0,ct))await Delivery(db).DeliverEventAsync(pending,ct);
        Check((await Delivery(db).PendingAsync(0,ct)).Length==0,"eligible receiver sender and manager intents drained without duplicate recipients");
        }
        else
        {
            var notify=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};notify.ArgumentList.Add("frontend/scripts/sprintInternalRequestHandoverNotificationBrowser.mjs");
            using var browser=Process.Start(notify)??throw new Exception("Notification browser unavailable");
            var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
            try{await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
                var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;checks.Add(new{handoverNotificationBrowser=proof});if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Handover notification browser failed at "+proof.GetProperty("stage").GetString());
            }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_handover_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request} AND o.notification_id IS NOT NULL").SingleAsync(ct)>0,"hosted worker persists handover delivery receipts");
        }
        checks.Add(new{handoverLimit="Limited receipt read contract still pending; existing IR_VIEW enforced. Delivery service and HTTPS source verified; hosted delivery browser sequence verified separately when enabled."});
    }
}
