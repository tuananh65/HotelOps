using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestEscalationUiScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long request,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(new{escalationUiCheck=name});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var manager=await db.Roles.SingleAsync(r=>r.RoleCode=="OPERATIONS_MANAGER",ct);var head=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View requests",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        var handle=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_OVERDUE_HANDLE",ct);
        foreach(var role in new[]{head,manager})foreach(var permission in new[]{view,handle})
            if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var priority=new ReferenceValue{GroupCode="PRIORITY",ValueCode="D3EUI"+Guid.NewGuid().ToString("N")[..8],DisplayName="Synthetic escalated priority",IsActive=true};
        db.ReferenceValues.Add(priority);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var now=DateTime.UtcNow;var created=now.AddHours(-10);var desired=new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,0,DateTimeKind.Utc).AddHours(4);
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.CreatedAt,created).SetProperty(r=>r.DueAt,now.AddHours(2)),ct);
        var path=$"/api/internal-requests/{request}/escalation";
        using(var denied=await http.GetAsync(path,ct))Check((int)denied.StatusCode==403,"DH with permission cannot read escalation capability");
        var assignment=new UserRole{UserId=actor,RoleId=manager.RoleId,BranchId=source.BranchId,AssignedAt=now.AddMinutes(-1),IsActive=true};
        db.UserRoles.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var loaded=await http.GetFromJsonAsync<JsonElement>(path,ct);var version=loaded.GetProperty("version").GetString();
        Check(loaded.GetProperty("canEscalate").GetBoolean()&&loaded.GetProperty("timingStatus").GetString()=="AT_RISK","HTTPS capability uses approved time window and current OM role");
        async Task<int> Post(bool csrf,string? tag,long? target=null)
        {
            using var message=new HttpRequestMessage(HttpMethod.Post,target==null?path:$"/api/internal-requests/{target}/escalation"){Content=JsonContent.Create(new{reason="Synthetic HTTPS intervention",priorityId=priority.ReferenceId,deadline=desired})};
            if(tag!=null)message.Headers.Add("If-Match",'"'+tag+'"');
            if(csrf){var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);message.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(message,ct);return(int)response.StatusCode;
        }
        Check(await Post(false,version)==400&&await Post(true,null)==428&&await Post(true,Convert.ToBase64String(new byte[8]))==412,"CSRF ETag and stale version enforced by HTTPS mutation");
        var foreign=await db.InternalRequests.Where(r=>r.BranchId!=source.BranchId).Select(r=>r.InternalRequestId).FirstAsync(ct);
        Check(await Post(true,version,foreign)==403,"cross-branch escalation denied");
        await db.UserRoles.Where(r=>r.UserRoleId==assignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check(await Post(true,version)==403,"revoked OM role denied at mutation");
        await db.UserRoles.Where(r=>r.UserRoleId==assignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        var start=new ProcessStartInfo("node","scripts/sprintInternalRequestEscalationBrowser.mjs"){WorkingDirectory="frontend",RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        using var process=Process.Start(start)??throw new Exception("Browser did not start");
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request,priority=priority.ReferenceId,deadline=desired}));process.StandardInput.Close();
        var output=process.StandardOutput.ReadToEndAsync(ct);var error=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);var result=await output;await error;
        Check(process.ExitCode==0,"real browser priority/deadline escalation notification acknowledgment and source workflow: "+result);
        var updated=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        Check(updated.PriorityId==priority.ReferenceId&&updated.DueAt==desired&&updated.CreatedAt==created&&updated.Status=="IN_PROGRESS","SQL confirms chosen intervention with unchanged creation and lifecycle");
        var entry=await db.InternalRequestEvents.AsNoTracking().SingleAsync(e=>e.InternalRequestId==request&&e.EventType=="ESCALATE",ct);
        Check(entry.EventNote=="Synthetic browser escalation intervention","SQL history preserves browser reason once");
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="INTERNAL_REQUEST_ESCALATION"&&n.SourceId==entry.EventId,ct);
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_recipients WHERE notification_id={notice.NotificationId} AND user_id={actor} AND read_at IS NOT NULL AND acknowledged_at IS NOT NULL").SingleAsync(ct)==1,"SQL confirms worker recipient read and acknowledgment");
        Check(await Post(true,version)==412,"stale public replay cannot duplicate escalation");
        loaded=await http.GetFromJsonAsync<JsonElement>(path,ct);
        Check(loaded.GetProperty("hasOpen").GetBoolean()&&!loaded.GetProperty("canEscalate").GetBoolean(),"fresh capability excludes second Open escalation");
    }
}
