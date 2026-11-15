using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.Options;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestCompletionUiScenario
{
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo result){result=user;return true;}}
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long request,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(new{completionCheck=name});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View requests",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var credential=SessionCredential.Generate();var now=DateTime.UtcNow;
        var session=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(credential),CreatedAt=now,ExpiresAt=now.AddMinutes(20)};
        var assignment=new InternalRequestAssignment{InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=now,Status="ACTIVE"};
        var escalation=new InternalRequestEvent{InternalRequestId=request,EventType="ESCALATE",ActorId=actor,OccurredAt=now};
        db.AddRange(session,assignment,escalation);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalations(internal_request_id,opened_event_id,state,opened_at) VALUES({request},{escalation.EventId},'OPEN',{now})",ct);
        await db.UserSessions.Where(s=>s.SessionId==session.SessionId&&s.UserId==actor&&s.SessionKeyHash==session.SessionKeyHash).ExecuteDeleteAsync(ct);
        var path=$"/api/internal-requests/{request}/completion";
        using(var read=await http.GetAsync(path,ct))Check((int)read.StatusCode==200,"public completion read authorized");
        var loaded=await http.GetFromJsonAsync<JsonElement>(path,ct);var version=loaded.GetProperty("version").GetString();
        async Task<int> Post(string result,bool csrf,string? tag,long? target=null){using var message=new HttpRequestMessage(HttpMethod.Post,target==null?path:$"/api/internal-requests/{target}/completion"){Content=JsonContent.Create(new{result})};
            if(tag!=null)message.Headers.Add("If-Match",'"'+tag+'"');if(csrf){var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);message.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(message,ct);return(int)response.StatusCode;}
        Check(await Post("Synthetic",false,version)==400&&await Post("Synthetic",true,null)==428,"public closure CSRF and ETag mandatory");
        Check(await Post(" ",true,version)==400,"public closure result mandatory");
        var foreign=await db.InternalRequests.AsNoTracking().Where(r=>r.BranchId!=source.BranchId).Select(r=>r.InternalRequestId).FirstAsync(ct);
        using(var hidden=await http.GetAsync($"/api/internal-requests/{foreign}/completion",ct))Check((int)hidden.StatusCode==404,"cross-branch completion read denied");
        Check(await Post("Synthetic",true,version,foreign)==403,"cross-branch completion mutation denied");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={source.BranchId} AND department_id={source.ReceivingDepartmentId}",ct);
        Check(await Post("Synthetic",true,version)==403,"public closure denied after mapping revocation");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={source.BranchId} AND department_id={source.ReceivingDepartmentId}",ct);
        var readGrant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==readGrant.RolePermissionId).ExecuteDeleteAsync(ct);
        Check(await Post("Synthetic",true,version)==404,"IR_PROCESS cannot replace missing IR_VIEW for closure");
        db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope=readGrant.DataScope,GrantedAt=readGrant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var material=new MaterialRequest{RequestNo="D3CLOSE"+Guid.NewGuid().ToString("N")[..12],BranchId=source.BranchId,DepartmentId=source.ReceivingDepartmentId!.Value,
            RequesterId=actor,Purpose="Synthetic unresolved material completion",Status="COMPLETED",CompletedAt=DateTime.UtcNow,CreatedAt=DateTime.UtcNow};
        var materialAudit=new AuditLog{ActorUserId=actor,BranchId=source.BranchId,EntityType="internal_requests",EntityId=request.ToString(),Action="UPDATE",OccurredAt=DateTime.UtcNow};
        db.AddRange(material,materialAudit);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_material_links(internal_request_id,material_request_id,is_mandatory,audit_id) VALUES({request},{material.RequestId},1,{materialAudit.AuditId})",ct);
        loaded=await http.GetFromJsonAsync<JsonElement>(path,ct);
        Check(!loaded.GetProperty("canComplete").GetBoolean()&&loaded.GetProperty("blocker").GetString()=="IR_MATERIAL_COMPLETION_UNRESOLVED"&&await Post("Synthetic",true,version)==409,"unresolved mandatory material blocks both public read capability and closure");
        // Remove only this synthetic negative-case link; the following scenario intentionally has no material linkage.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.internal_request_material_links WHERE internal_request_id={request} AND material_request_id={material.RequestId} AND audit_id={materialAudit.AuditId}",ct);
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintInternalRequestCompletionBrowser.mjs");
        using(var browser=Process.Start(start)??throw new Exception("Completion browser unavailable")){
            var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
            try{await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
                var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;checks.Add(new{completionBrowser=proof});if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Completion browser failed at "+proof.GetProperty("stage").GetString());
            }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        }
        Check(await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request&&e.EventType=="COMPLETE",ct)==1
            &&await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="CLOSED"&&r.ClosedAt!=null,ct),"browser double submit persists one closure");
        var completion=await db.InternalRequestEvents.SingleAsync(e=>e.InternalRequestId==request&&e.EventType=="COMPLETE",ct);db.ChangeTracker.Clear();
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="INTERNAL_REQUEST_COMPLETION"&&n.SourceId==completion.EventId,ct);
        var delivery=new InternalRequestCompletionDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
        Check(await delivery.DeliverAsync(completion.EventId,ct)=="ALREADY_DELIVERED","hosted worker delivers once and replay is idempotent");
        var grant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
        using(var denied=await http.GetAsync($"/api/notifications/{notice.NotificationId}/source",ct))Check((int)denied.StatusCode==404,"notification source rechecks revoked IR_VIEW");
        using(var retained=await http.GetAsync("/api/notifications",ct))
        {
            Check((int)retained.StatusCode==200,"recipient retains notification center after source read grant revoked");
            var inbox=await retained.Content.ReadFromJsonAsync<JsonElement>(ct);
            Check(inbox.GetProperty("items").EnumerateArray().Any(n=>n.GetProperty("id").GetInt64()==notice.NotificationId
                &&n.GetProperty("title").GetString()==notice.Title),"GBR157 retains delivered notification text while denying source access");
        }
        Check(await delivery.DeliverAsync(completion.EventId,ct)=="RECIPIENT_READ_DENIED","delivery rechecks current requester read grant");
        db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope=grant.DataScope,GrantedAt=grant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        using(var allowed=await http.GetAsync($"/api/notifications/{notice.NotificationId}/source",ct))Check((int)allowed.StatusCode==200,"restored requester scope opens source");
        Check(await Post("Replay",true,version)==412,"stale public closure does not duplicate receipt");
        checks.Add(new{completionLimit="Material completion mapping remains unresolved; whole UC120 is still PARTIAL"});
    }
}
