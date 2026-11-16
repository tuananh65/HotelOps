using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintChecklistScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long branch,string username,string password,List<object> checks,CancellationToken ct)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{checklistCheck=label});}
        Check(await ChecklistSchema.StateAsync(db,ct)==1,"verified clone has existing checklist capability");
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="HK_SUPERVISOR"&&r.IsActive,ct);
        var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="CHECKLIST_MANAGE",ct);
        var root=$"/api/branches/{branch}/inspection-checklists";
        using(var denied=await http.GetAsync(root,ct))Check((int)denied.StatusCode==403,"existing manager role does not infer checklist authority");
        var assignment=new UserRole{UserId=actor,RoleId=role.RoleId,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)};
        db.UserRoles.Add(assignment);
        if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct))
            db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var legacy=new InspectionChecklist{ChecklistCode="D3BOOT"+Guid.NewGuid().ToString("N")[..8],ChecklistName="Synthetic bootstrap template",IsActive=true};
        db.InspectionChecklists.Add(legacy);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var code="D3TYPE"+Guid.NewGuid().ToString("N")[..8];
        var type=(await db.Database.SqlQuery<long>($"INSERT dbo.room_types(type_code,type_name,is_active,standard_capacity,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES({code},'Synthetic checklist type',1,1,{branch},30,{legacy.ChecklistId}); SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;").ToArrayAsync(ct)).Single();
        async Task<(int Status,JsonElement Body,string? Tag)> Send(HttpMethod method,string path,object? body=null,string? tag=null,bool csrf=true)
        {
            using var request=new HttpRequestMessage(method,path);if(body!=null)request.Content=JsonContent.Create(body);
            if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf&&method!=HttpMethod.Get){var token=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(request,ct);var raw=await response.Content.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(string.IsNullOrEmpty(raw)?"{}":raw);
            return((int)response.StatusCode,json.RootElement.Clone(),response.Headers.ETag?.ToString());
        }
        var input=new{code="D3API"+Guid.NewGuid().ToString("N")[..8],name="Synthetic checklist",roomTypeIds=new[]{type},criteria=new[]{new{text="No dust",group="Bedroom",mandatory=true,passCondition="Clean surfaces",description="Review surfaces"}}};
        Check((await Send(HttpMethod.Post,root,input,csrf:false)).Status==400,"checklist creation requires CSRF");
        var created=await Send(HttpMethod.Post,root,input);Check(created.Status==201,"public checklist creates Draft");
        var id=created.Body.GetProperty("id").GetInt64();var path=root+"/"+id;
        var transition=new{reason="Reviewed standard for inspection",confirmation=true};
        Check((await Send(HttpMethod.Post,path+"/publish",transition)).Status==428,"publication requires If-Match");
        var published=await Send(HttpMethod.Post,path+"/publish",transition,created.Tag);Check(published.Status==200,"public checklist publishes Draft");
        Check((await Send(HttpMethod.Post,path+"/retire",transition,created.Tag)).Status==412,"stale checklist transition rejected");
        var update=new{criteria=new[]{new{text="No dust or stains",group="Bedroom",mandatory=true}},reason="Improve inspection standard"};
        var draft=await Send(HttpMethod.Put,path,update,published.Tag);Check(draft.Status==200,"published update creates next Draft");
        Check(draft.Body.GetProperty("versions").GetArrayLength()==2&&draft.Body.GetProperty("versions")[0].GetProperty("criteria")[0].GetProperty("text").GetString()=="No dust","previous published content remains unchanged");
        var next=await Send(HttpMethod.Post,path+"/publish",transition,draft.Tag);Check(next.Status==200,"next version published atomically");
        Check(next.Body.GetProperty("versions")[0].GetProperty("status").GetString()=="RETIRED"&&next.Body.GetProperty("versions")[1].GetProperty("status").GetString()=="PUBLISHED","replacement publication retires only prior lifecycle");
        Check((await Send(HttpMethod.Post,path+"/retire",transition,next.Tag)).Status==200,"public retirement preserves version history");
        Check(await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists"&&a.EntityId==id.ToString(),ct)==5,"create update publish and retire audit persisted");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintChecklistBrowser.mjs");
        using var browser=Process.Start(start)!;
        try{
            var output=browser.StandardOutput.ReadToEndAsync(ct);var error=browser.StandardError.ReadToEndAsync(ct);
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,type}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
            var result=JsonSerializer.Deserialize<JsonElement>(await output);_=await error;checks.Add(new{checklistBrowser=result});
            Check(browser.ExitCode==0&&result.GetProperty("success").GetBoolean(),"canonical browser checklist create publish update retire");
        }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
        await db.UserRoles.Where(r=>r.UserRoleId==assignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        Check((await Send(HttpMethod.Get,path)).Status==403,"revoked checklist role denies retained URL");
    }
}
