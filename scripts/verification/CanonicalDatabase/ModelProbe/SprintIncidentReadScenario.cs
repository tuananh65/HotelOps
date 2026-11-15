using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintIncidentReadScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long branch, long department,
        List<object> checks, CancellationToken ct, string username, string password)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{incidentReadCheck=label});}
        var prefix="D3INC"+Guid.NewGuid().ToString("N")[..8];
        var other=new User{EmployeeCode=prefix,Username=prefix,Email=prefix+"@example.invalid",FullName="Synthetic reporter",
            PasswordHash="Synthetic account cannot log in",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        db.Users.Add(other);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var foreign=await db.Branches.Where(b=>b.BranchId!=branch).Select(b=>b.BranchId).SingleAsync(ct);
        Incident Row(string suffix,long reporter,long b)=>new(){IncidentNo=prefix+suffix,BranchId=b,IncidentTitle="Synthetic incident "+suffix,
            Description="Private incident "+suffix,Status="OPEN",ReportedBy=reporter,DiscoveredAt=DateTime.UtcNow,ReportedAt=DateTime.UtcNow};
        var own=Row("OWN",actor,branch);var assigned=Row("ASSIGNED",other.UserId,branch);var unrelated=Row("UNRELATED",other.UserId,branch);var outside=Row("FOREIGN",actor,foreign);
        db.Incidents.AddRange(own,assigned,unrelated,outside);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        db.MaintenanceWorks.Add(new(){WorkNo=prefix+"WORK",IncidentId=assigned.IncidentId,AssigneeId=actor,DepartmentId=department,
            Title="Synthetic assigned work",Status="IN_PROGRESS",CreatedBy=actor,CreatedAt=DateTime.UtcNow});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        async Task<(int Status,JsonElement Body)> Get(string path){using var response=await http.GetAsync(path,ct);return((int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType?.Contains("json")==true?await response.Content.ReadFromJsonAsync<JsonElement>(ct):default);}
        var list=$"/api/incidents?branchId={branch}";
        Check((await Get(list)).Status==403,"IR_PROCESS department manager does not gain incident read");
        var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="INCIDENT_VIEW",ct);
        var lead=await db.Roles.SingleAsync(r=>r.RoleCode=="MAINTENANCE_LEAD",ct);var staff=await db.Roles.SingleAsync(r=>r.RoleCode=="MAINTENANCE_STAFF",ct);
        foreach(var role in new[]{lead,staff}){
            var grant=await db.RolePermissions.SingleOrDefaultAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct);
            if(grant==null)db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
            else Check(grant.DataScope is "BRANCH" or "ALL","existing incident grant supports branch read");
        }
        var assignment=new UserRole{UserId=actor,RoleId=lead.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true};
        db.UserRoles.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var page=await Get(list+"&pageSize=2");Check(page.Status==200&&page.Body.GetProperty("totalCount").GetInt32()==3&&page.Body.GetProperty("items").GetArrayLength()==2,"lead branch count and pagination exclude foreign branch");
        Check((await Get("/api/incidents")).Status==403&&(await Get($"/api/incidents?branchId={foreign}")).Status==403
            &&(await Get($"/api/incidents/{outside.IncidentId}")).Status==403,"explicit branch and foreign branch detail guards");
        Check((await Get(list+"&page=0")).Status==400&&(await Get(list+"&status=CLOSED")).Body.GetProperty("totalCount").GetInt32()==0,"invalid pagination rejected and empty filtered list preserved");
        await db.UserRoles.Where(r=>r.UserRoleId==assignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,staff.RoleId),ct);
        page=await Get(list);Check(page.Status==200&&page.Body.GetProperty("totalCount").GetInt32()==2,"staff BRANCH catalog grant is narrowed to reported or assigned before count");
        Check((await Get($"/api/incidents/{unrelated.IncidentId}")).Status==404&&(await Get($"/api/incidents/{assigned.IncidentId}")).Status==200,"staff detail independently enforces ownership");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintIncidentReadBrowser.mjs");
        using var browser=Process.Start(start)??throw new Exception("Incident browser unavailable");var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
        try{await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,incident=assigned.IncidentId,title=assigned.IncidentTitle,hidden=unrelated.IncidentTitle}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
            var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Incident browser failed at "+proof.GetProperty("stage").GetString());checks.Add(new{incidentReadBrowser=proof});}
        finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        await db.MaintenanceWorks.Where(w=>w.IncidentId==assigned.IncidentId).ExecuteUpdateAsync(s=>s.SetProperty(w=>w.AssigneeId,other.UserId),ct);
        Check((await Get($"/api/incidents/{assigned.IncidentId}")).Status==404&&(await Get(list)).Body.GetProperty("totalCount").GetInt32()==1,"reassignment immediately removes former staff read");
        await db.UserRoles.Where(r=>r.UserRoleId==assignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check((await Get(list)).Status==403,"revoked role denies even incident reporter");
        await db.UserRoles.Where(r=>r.UserRoleId==assignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);using var mutation=new HttpRequestMessage(HttpMethod.Patch,$"/api/incidents/{own.IncidentId}/status"){Content=JsonContent.Create(new{actorUserId=actor,status="IN_PROGRESS"})};mutation.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());
        using var denied=await http.SendAsync(mutation,ct);Check((int)denied.StatusCode==403,"read contract does not admit incident mutations");
    }
}
