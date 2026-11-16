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
        var historyPermission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="INSPECTION_VIEW",ct);
        if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==historyPermission.PermissionId,ct))
            db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=historyPermission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
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
        Check((await Send(HttpMethod.Post,root,new{input.code,input.name,input.roomTypeIds,criteria=Array.Empty<object>()})).Status==400,"create rejects missing criteria");
        Check((await Send(HttpMethod.Post,root,new{input.code,input.name,input.roomTypeIds,criteria=new[]{new{text="   ",mandatory=true}}})).Status==400,"create rejects whitespace criterion");
        var foreignType=await db.BranchRoomTypes.AsNoTracking().Where(t=>t.BranchId!=branch).Select(t=>t.RoomTypeId).FirstAsync(ct);
        Check((await Send(HttpMethod.Post,root,new{input.code,input.name,roomTypeIds=new[]{foreignType},input.criteria})).Status==400,"create rejects room type outside current branch");
        var checklistCount=await db.InspectionChecklists.CountAsync(ct);
        var versionCount=await db.InspectionChecklistVersions.CountAsync(ct);
        var itemCount=await db.InspectionChecklistItems.CountAsync(ct);
        var applicabilityCount=await db.ChecklistRoomTypes.CountAsync(ct);
        var auditCount=await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists",ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_ChecklistCreateFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='inspection_checklists') THROW 51891,'Synthetic checklist creation audit failure',1; END",ct);
            Check((await Send(HttpMethod.Post,root,input)).Status>=500,"creation audit failure rejects aggregate");
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_ChecklistCreateFault",CancellationToken.None);}
        Check(await db.InspectionChecklists.CountAsync(ct)==checklistCount&&await db.InspectionChecklistVersions.CountAsync(ct)==versionCount
            &&await db.InspectionChecklistItems.CountAsync(ct)==itemCount&&await db.ChecklistRoomTypes.CountAsync(ct)==applicabilityCount
            &&await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists",ct)==auditCount,"create audit rollback leaves no checklist version criteria applicability or audit orphan");
        var created=await Send(HttpMethod.Post,root,input);Check(created.Status==201,"public checklist creates Draft");
        Check(created.Body.GetProperty("versions").GetArrayLength()==1&&created.Body.GetProperty("versions")[0].GetProperty("number").GetInt32()==1
            &&created.Body.GetProperty("versions")[0].GetProperty("status").GetString()=="DRAFT"
            &&created.Body.GetProperty("versions")[0].GetProperty("criteria")[0].GetProperty("passCondition").GetString()=="Clean surfaces",
            "create persists version1 Draft with pass condition and original criteria");
        Check((await Send(HttpMethod.Post,root,input)).Status==409,"duplicate checklist code rejected without second aggregate");
        var id=created.Body.GetProperty("id").GetInt64();var path=root+"/"+id;
        async Task Fault(HttpMethod method,string target,object body,string? tag)
        {
            var before=await Send(HttpMethod.Get,path);
            var audits=await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists"&&a.EntityId==id.ToString(),ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_ChecklistAuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='inspection_checklists') THROW 51890,'Synthetic checklist audit failure',1; END",ct);
                Check((await Send(method,target,body,tag)).Status>=500,"audit failure rejects checklist mutation "+method+" "+target);
            }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_ChecklistAuditFault",CancellationToken.None); }
            var after=await Send(HttpMethod.Get,path);
            Check(after.Status==200&&after.Tag==before.Tag&&after.Body.GetRawText()==before.Body.GetRawText()
                &&await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists"&&a.EntityId==id.ToString(),ct)==audits,"audit failure rolls back versions criteria metadata and ETag");
        }
        var transition=new{reason="Reviewed standard for inspection",confirmation=true};
        Check((await Send(HttpMethod.Post,path+"/publish",transition)).Status==428,"publication requires If-Match");
        var published=await Send(HttpMethod.Post,path+"/publish",transition,created.Tag);Check(published.Status==200,"public checklist publishes Draft");
        Check((await Send(HttpMethod.Post,path+"/retire",transition,created.Tag)).Status==412,"stale checklist transition rejected");
        var version1=published.Body.GetProperty("versions")[0].GetProperty("id").GetInt64();
        var floor=await db.Floors.AsNoTracking().Where(f=>f.BranchId==branch&&f.IsActive).Select(f=>f.FloorId).FirstAsync(ct);
        var room=new Room{BranchId=branch,FloorId=floor,RoomTypeId=type,RoomNumber="D3CHK"+Guid.NewGuid().ToString("N")[..6],OperationalStatus="AVAILABLE",IsActive=true};
        db.Rooms.Add(room);await db.SaveChangesAsync(ct);
        var inspection=new RoomInspection{InspectionNo="D3CHK"+Guid.NewGuid().ToString("N"),RoomId=room.RoomId,VersionId=version1,InspectorUserId=actor,Status="PENDING",CreatedAt=DateTime.UtcNow};
        db.RoomInspections.Add(inspection);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        published=await Send(HttpMethod.Get,path);
        Check(published.Body.GetProperty("versions")[0].GetProperty("usedByInspection").GetBoolean(),"checklist read reports actual existing inspection use");
        var update=new{criteria=new[]{new{text="No dust or stains",group="Bedroom",mandatory=true}},reason="Improve inspection standard"};
        Check((await Send(HttpMethod.Put,path,new{criteria=Array.Empty<object>(),reason="Invalid empty criteria"},published.Tag)).Status==400,"empty criteria cannot replace an inspection standard");
        var foreign=await db.Branches.Where(b=>b.BranchId!=branch).Select(b=>b.BranchId).FirstAsync(ct);
        Check((await Send(HttpMethod.Put,$"/api/branches/{foreign}/inspection-checklists/{id}",update,published.Tag)).Status==403,"foreign branch checklist update denied");
        await Fault(HttpMethod.Put,path,update,published.Tag);
        var edits=await Task.WhenAll(Send(HttpMethod.Put,path,update,published.Tag),Send(HttpMethod.Put,path,update,published.Tag));
        Check(edits.Count(r=>r.Status==200)==1&&edits.Count(r=>r.Status==412)==1,"concurrent edits produce one new Draft and reject stale writer");
        var draft=edits.Single(r=>r.Status==200);Check(draft.Status==200,"published update creates next Draft");
        Check(draft.Body.GetProperty("versions").GetArrayLength()==2&&draft.Body.GetProperty("versions")[0].GetProperty("criteria")[0].GetProperty("text").GetString()=="No dust","previous published content remains unchanged");
        var draftId=draft.Body.GetProperty("versions")[1].GetProperty("id").GetInt64();
        draft=await Send(HttpMethod.Put,path,update,draft.Tag);
        Check(draft.Status==200&&draft.Body.GetProperty("versions").GetArrayLength()==2&&draft.Body.GetProperty("versions")[1].GetProperty("id").GetInt64()==draftId,"editing existing Draft preserves its version identity");
        await Fault(HttpMethod.Post,path+"/publish",transition,draft.Tag);
        var publications=await Task.WhenAll(Send(HttpMethod.Post,path+"/publish",transition,draft.Tag),Send(HttpMethod.Post,path+"/publish",transition,draft.Tag));
        Check(publications.Count(r=>r.Status==200)==1&&publications.Count(r=>r.Status==412)==1,"concurrent Publish commits one lifecycle transition");
        var next=publications.Single(r=>r.Status==200);Check(next.Status==200,"next version published atomically");
        Check(next.Body.GetProperty("versions")[0].GetProperty("status").GetString()=="RETIRED"&&next.Body.GetProperty("versions")[1].GetProperty("status").GetString()=="PUBLISHED","replacement publication retires only prior lifecycle");
        Check(await db.RoomInspections.AsNoTracking().AnyAsync(i=>i.InspectionId==inspection.InspectionId&&i.VersionId==version1&&i.Status=="PENDING",ct)
            &&await db.InspectionChecklistItems.AnyAsync(i=>i.VersionId==version1&&i.Criterion=="No dust",ct),"assigned inspection retains exact retired version and original criterion after publication");
        var newerInspection=new RoomInspection{InspectionNo="D3NEW"+Guid.NewGuid().ToString("N"),RoomId=room.RoomId,VersionId=draftId,InspectorUserId=actor,Status="PENDING",CreatedAt=DateTime.UtcNow};
        db.RoomInspections.Add(newerInspection);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        Check(await db.RoomInspections.AnyAsync(i=>i.InspectionId==newerInspection.InspectionId&&i.VersionId==draftId,ct),"new inspection can select the latest effective Published version");
        var oldHistory=await Send(HttpMethod.Get,$"/api/inspection-history/{inspection.InspectionId}?branchId={branch}");
        var newHistory=await Send(HttpMethod.Get,$"/api/inspection-history/{newerInspection.InspectionId}?branchId={branch}");
        Check(oldHistory.Status==200&&newHistory.Status==200
            &&oldHistory.Body.GetProperty("inspection").GetProperty("versionId").GetInt64()==version1
            &&oldHistory.Body.GetProperty("criteria")[0].GetProperty("criterion").GetString()=="No dust"
            &&newHistory.Body.GetProperty("inspection").GetProperty("versionId").GetInt64()==draftId
            &&newHistory.Body.GetProperty("criteria")[0].GetProperty("criterion").GetString()=="No dust or stains",
            "public HTTPS history returns the exact old and new assigned checklist criteria");
        next=await Send(HttpMethod.Get,path);
        await Fault(HttpMethod.Post,path+"/retire",transition,next.Tag);
        Check((await Send(HttpMethod.Post,path+"/retire",new{reason=transition.reason,confirmation=false},next.Tag)).Status==400,"retirement requires explicit confirmation");
        var retirements=await Task.WhenAll(Send(HttpMethod.Post,path+"/retire",transition,next.Tag),Send(HttpMethod.Post,path+"/retire",transition,next.Tag));
        Check(retirements.Count(r=>r.Status==200)==1&&retirements.Count(r=>r.Status==412)==1,"concurrent Retire commits once and rejects stale writer");
        var rejected=new RoomInspection{InspectionNo="D3RET"+Guid.NewGuid().ToString("N"),RoomId=room.RoomId,VersionId=draftId,InspectorUserId=actor,Status="PENDING",CreatedAt=DateTime.UtcNow};
        db.RoomInspections.Add(rejected);
        var refused=false;
        try{await db.SaveChangesAsync(ct);}
        catch(DbUpdateException error) when(error.InnerException is Microsoft.Data.SqlClient.SqlException sql)
        {
            refused=sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(e=>e.Number==51721&&e.Message.Contains("Published checklist",StringComparison.Ordinal));
            if(!refused)throw new Exception("Unexpected retired-selection SQL errors: "+string.Join("; ",sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Select(e=>$"{e.Number}: {e.Message}")));
        }
        finally{db.ChangeTracker.Clear();}
        Check(refused&&!await db.RoomInspections.AnyAsync(i=>i.InspectionNo==rejected.InspectionNo,ct),"retired checklist is rejected for new inspection by intact SQL selection guard");
        Check(await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists"&&a.EntityId==id.ToString(),ct)==6,"six committed checklist operations have six audits despite faults and races");
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
