using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintIncidentLinkScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long request,long incident,long hidden,long foreign,
        List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{incidentLinkCheck=label});}
        var row=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View internal request history",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        Check(view.ModuleCode==create.ModuleCode&&view.ResourceCode==create.ResourceCode&&view.ActionCode=="VIEW","approved IR_VIEW contract");
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var grant=await db.RolePermissions.SingleOrDefaultAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct);
        if(grant==null){db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});await db.SaveChangesAsync(ct);}
        else Check(grant.DataScope is "BRANCH" or "ALL","existing IR_VIEW scope supported");
        db.ChangeTracker.Clear();
        await SprintRequestDirectionScenario.RunAsync(db,http,actor,request,checks,ct);
        var path=$"/api/internal-requests/{request}/linked-incidents";
        async Task<(int Status,JsonElement Body)> Send(HttpMethod method,string url,object? body=null,string? version=null,bool csrf=true)
        {
            using var message=new HttpRequestMessage(method,url);
            if(body!=null)message.Content=JsonContent.Create(body);
            if(version!=null)message.Headers.TryAddWithoutValidation("If-Match",'"'+version+'"');
            if(method!=HttpMethod.Get&&csrf){var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);message.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(message,ct);
            return((int)response.StatusCode,response.Content.Headers.ContentType?.MediaType?.Contains("json")==true?await response.Content.ReadFromJsonAsync<JsonElement>(ct):default);
        }
        async Task<string> Version(){var v=await Send(HttpMethod.Get,path);Check(v.Status==200,"linked list readable");return v.Body.GetProperty("version").GetString()!;}
        foreach(var legacy in new[]{hidden,foreign,incident})
        {
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(u=>u.SetProperty(r=>r.RelatedIncidentId,(long?)legacy),ct);
            var history=await Send(HttpMethod.Get,path);
            Check(history.Status==200&&history.Body.GetProperty("total").GetInt32()==(legacy==incident?1:0),"legacy reference obeys independent incident read and branch scope");
            if(legacy==incident)Check(history.Body.GetProperty("items")[0].GetProperty("mandatory").ValueKind==JsonValueKind.Null,"legacy requirement remains unclassified");
        }
        var candidates=await Send(HttpMethod.Get,path+"?candidates=true");
        Check(candidates.Status==200&&candidates.Body.GetProperty("total").GetInt32()==1&&candidates.Body.GetProperty("items")[0].GetProperty("incidentId").GetInt64()==incident,"candidate intersection excludes unrelated and cross-branch incidents");
        var version=await Version();var input=new{incidentId=incident,mandatory=true};
        Check((await Send(HttpMethod.Post,path,input,version,false)).Status==400,"CSRF required");
        Check((await Send(HttpMethod.Post,path,input)).Status==428,"loaded If-Match required");
        Check((await Send(HttpMethod.Post,path,new{incidentId=incident},version)).Status==400,"mandatory flag must be explicit");
        Check((await Send(HttpMethod.Post,path,new{incidentId=hidden,mandatory=true},version)).Status==404,"unrelated incident denied despite IR_PROCESS");
        Check((await Send(HttpMethod.Post,path,new{incidentId=foreign,mandatory=true},version)).Status==404,"cross branch incident denied");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={row.BranchId} AND department_id={row.ReceivingDepartmentId}",ct);
        Check((await Send(HttpMethod.Post,path,input,version)).Status==403,"revoked department mapping denies mutation");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={row.BranchId} AND department_id={row.ReceivingDepartmentId}",ct);
        await db.Incidents.Where(i=>i.IncidentId==incident).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.Status,"CLOSED"),ct);
        Check((await Send(HttpMethod.Post,path,input,version)).Status==409,"closed incident cannot be linked");
        await db.Incidents.Where(i=>i.IncidentId==incident).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.Status,"OPEN"),ct);
        var auditBefore=await db.AuditLogs.CountAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString(),ct);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.D3IncidentLinkFault ON dbo.internal_request_incident_links AFTER INSERT AS THROW 51999,'Synthetic link rollback',1;",ct);
        try{Check((await Send(HttpMethod.Post,path,input,version)).Status>=500,"injected link failure rejected");}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.D3IncidentLinkFault",ct);}
        Check(await Version()==version&&await db.AuditLogs.CountAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString(),ct)==auditBefore,"failure rolls back audit and request version");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintIncidentLinkBrowser.mjs");
        using var browser=Process.Start(start)??throw new Exception("Browser unavailable");var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
        try{await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request,incident}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;checks.Add(new{incidentLinkBrowser=proof});Check(browser.ExitCode==0&&proof.GetProperty("success").GetBoolean(),"canonical browser links required incident and opens independently authorized detail");}
        finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        Check((await Send(HttpMethod.Post,path,input,version)).Status==412,"stale version rejected after browser mutation");
        version=await Version();Check((await Send(HttpMethod.Post,path,input,version)).Status==409,"duplicate link rejected");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_incident_links l JOIN dbo.audit_logs a ON l.audit_id=a.audit_id WHERE l.internal_request_id={request} AND l.incident_id={incident} AND l.is_mandatory=1 AND a.actor_user_id={actor}").SingleAsync(ct)==1,"required link and actor audit persisted exactly once");
        Check(await db.InternalRequests.Where(r=>r.InternalRequestId==request).Select(r=>r.Status).SingleAsync(ct)=="IN_PROGRESS","linking does not close request");
        var overlap=await Send(HttpMethod.Get,path);
        Check(overlap.Body.GetProperty("total").GetInt32()==1&&overlap.Body.GetProperty("items").GetArrayLength()==1
            &&overlap.Body.GetProperty("items")[0].GetProperty("mandatory").GetBoolean(),"explicit classification overrides legacy unknown without duplicate row");
        var race=new Incident{IncidentNo="D3RACE"+Guid.NewGuid().ToString("N")[..8],BranchId=row.BranchId,IncidentTitle="Synthetic race",Description="Synthetic",Status="OPEN",ReportedBy=actor,DiscoveredAt=DateTime.UtcNow,ReportedAt=DateTime.UtcNow};
        db.Incidents.Add(race);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        version=await Version();
        var racing=await Task.WhenAll(Send(HttpMethod.Post,path,new{incidentId=race.IncidentId,mandatory=false},version),Send(HttpMethod.Post,path,new{incidentId=race.IncidentId,mandatory=false},version));
        Check(racing.Select(r=>r.Status).Order().SequenceEqual(new[]{200,412}),"concurrent same-version links yield one200 and one412");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_incident_links WHERE internal_request_id={request} AND incident_id={race.IncidentId} AND is_mandatory=0").SingleAsync(ct)==1,"one optional link persisted under concurrency");
        await db.UserRoles.Where(r=>r.UserId==actor&&r.Role.RoleCode=="MAINTENANCE_STAFF").ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        var denied=await Send(HttpMethod.Get,path);Check(denied.Status==200&&denied.Body.GetProperty("total").GetInt32()==0,"target role revocation removes link disclosure even with IR_VIEW");
        await db.UserRoles.Where(r=>r.UserId==actor&&r.Role.RoleCode=="MAINTENANCE_STAFF").ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
    }
}
