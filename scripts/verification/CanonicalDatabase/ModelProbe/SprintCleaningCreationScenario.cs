using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintCleaningCreationScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long request, long roomType, long floor, long cleaningType, string username, string password, List<object> checks, CancellationToken ct)
    {
        void Check(bool ok,string label) { if(!ok)throw new Exception(label);checks.Add(new{cleaningCreationCheck=label}); }
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);var branch=source.BranchId;
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="CLEANING_ASSIGN",ct);
        var grant=await db.RolePermissions.SingleOrDefaultAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct);
        if(grant==null){grant=new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow};db.RolePermissions.Add(grant);}else grant.DataScope="BRANCH";
        var room=new Room{BranchId=branch,FloorId=floor,RoomTypeId=roomType,RoomNumber="CREATE-LINK",OperationalStatus="DIRTY",IsActive=true};
        db.Rooms.Add(room);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order) VALUES({roomType},'Synthetic cleaning criterion',1,0)",ct);
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoomId,room.RoomId),ct);
        var root=$"/api/branches/{branch}/cleaning-tasks";var contextPath=$"/api/branches/{branch}/cleaning-request-context/{request}";
        async Task<JsonElement> Context()=>await http.GetFromJsonAsync<JsonElement>(contextPath,ct);
        async Task<int> Create(string version,bool csrf=true,bool match=true)
        {
            using var message=new HttpRequestMessage(HttpMethod.Post,root){Content=JsonContent.Create(new{roomId=room.RoomId,cleaningTypeId=cleaningType,priorityId=source.PriorityId,dueAt=DateTime.UtcNow.AddDays(2),instruction="Synthetic cleaning creation",internalRequestId=request,requestLink=new{version,mandatory=true}})};
            if(match)message.Headers.TryAddWithoutValidation("If-Match",'"'+version+'"');
            if(csrf){var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);message.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(message,ct);return (int)response.StatusCode;
        }
        var context=await Context();var version=context.GetProperty("version").GetString()!;
        Check(context.GetProperty("roomId").GetInt64()==room.RoomId&&context.GetProperty("instruction").GetString()==source.Description,"authorized cleaning prefill uses current source");
        Check(await Create(version,false)==400&&await Create(version,true,false)==428,"linked cleaning creation requires CSRF and source If-Match");
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteUpdateAsync(s=>s.SetProperty(g=>g.DataScope,"OWN"),ct);
        Check(await Create(version)==403,"IR_PROCESS cannot bypass target CLEANING_ASSIGN");
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteUpdateAsync(s=>s.SetProperty(g=>g.DataScope,"BRANCH"),ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND department_id={source.ReceivingDepartmentId}",ct);
        Check(await Create(version)==403,"target cleaning authority cannot bypass source department mapping");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND department_id={source.ReceivingDepartmentId}",ct);
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.UpdatedAt,DateTime.UtcNow),ct);
        Check(await Create(version)==412,"stale source creates no cleaning task");version=(await Context()).GetProperty("version").GetString()!;
        var audits=await db.AuditLogs.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER dbo.TR_DEV3_CleaningCreateFault ON dbo.internal_request_cleaning_links AFTER INSERT AS THROW 51902,'Synthetic cleaning create fault',1;",ct);
        try{Check(await Create(version)>=500,"injected link failure rejects cleaning creation");}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_CleaningCreateFault",CancellationToken.None);}
        Check(!await db.CleaningTasks.AnyAsync(t=>t.RoomId==room.RoomId,ct)&&await db.AuditLogs.CountAsync(ct)==audits
            &&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={room.RoomId}").SingleAsync(ct)==0
            &&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.room_readiness_events WHERE room_id={room.RoomId}").SingleAsync(ct)==0
            &&(await Context()).GetProperty("version").GetString()==version,"task cycle readiness link audit and source version roll back together");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintCleaningCreateFromRequestBrowser.mjs");
        using var browser=Process.Start(start)??throw new Exception("Cleaning create browser unavailable");var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
        try{
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request,branch,cleaningType,instruction=source.Description}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
            var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Cleaning create browser failed at "+proof.GetProperty("stage").GetString());checks.Add(new{cleaningCreationBrowser=proof});
        }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        var created=await db.CleaningTasks.AsNoTracking().SingleAsync(t=>t.RoomId==room.RoomId,ct);
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_cleaning_links l JOIN dbo.audit_logs a ON a.audit_id=l.audit_id JOIN dbo.room_cleaning_cycle_state c ON c.cleaning_task_id=l.cleaning_task_id WHERE l.internal_request_id={request} AND l.cleaning_task_id={created.CleaningTaskId} AND l.is_mandatory=1 AND a.actor_user_id={actor}").SingleAsync(ct)==1,"browser created cleaning current cycle and mandatory link persist with audit");
        Check(await Create(version)==412&&await db.CleaningTasks.CountAsync(t=>t.RoomId==room.RoomId,ct)==1,"retry with old source version cannot create a second cleaning cycle");
        Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="IN_PROGRESS"&&r.ClosedAt==null,ct),"linked cleaning creation never closes request");
    }
}
