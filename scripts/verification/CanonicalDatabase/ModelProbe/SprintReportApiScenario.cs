using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintReportApiScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,List<object> checks,CancellationToken ct,string username,string password)
    {
        var prefix="D3RAPI"+Guid.NewGuid().ToString("N")[..8];
        var branch=new Branch{BranchCode=prefix,BranchName="Synthetic report API",IsActive=true};
        long department=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{reportCheck=label});}
        async Task<HttpResponseMessage> Post(string path,object body,bool csrf=true)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
            if(csrf){var value=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);request.Headers.Add("X-CSRF-TOKEN",value.GetProperty("csrfToken").GetString());}
            return await http.SendAsync(request,ct);
        }
        try
        {
            db.Branches.Add(branch);await db.SaveChangesAsync(ct);
            var dep=new Department{BranchId=branch.BranchId,DepartmentCode=prefix,DepartmentName="Synthetic",IsActive=true};
            db.Departments.Add(dep);await db.SaveChangesAsync(ct);department=dep.DepartmentId;
            db.UserBranchAccesses.Add(new(){UserId=actor,BranchId=branch.BranchId,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            var deptRole=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER").Select(r=>r.RoleId).SingleAsync(ct);
            var ownerRole=await db.Roles.Where(r=>r.RoleCode=="OWNER").Select(r=>r.RoleId).SingleAsync(ct);
            Check(await db.RolePermissions.AnyAsync(p=>p.RoleId==deptRole&&p.Permission.PermissionCode=="REPORT_VIEW",ct),"catalog DEPT_MANAGER actually has REPORT_VIEW");
            db.UserRoles.Add(new(){UserId=actor,RoleId=deptRole,BranchId=branch.BranchId,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
            db.OperationalTasks.Add(new(){TaskCode=prefix,Title="Synthetic frozen report row",BranchId=branch.BranchId,DepartmentId=department,
                Status="TODO",ProgressPercent=0,CreatedBy=actor,CreatedAt=DateTime.UtcNow});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            using(var denied=await http.GetAsync("/api/reports/options",ct))Check((int)denied.StatusCode==403,"DEPT_MANAGER options denied despite REPORT_VIEW");
            using(var denied=await Post("/api/reports/tasks/preview",new{branchId=branch.BranchId}))Check((int)denied.StatusCode==403,"DEPT_MANAGER preview denied");
            using(var denied=await Post("/api/reports/snapshots/unknown/export",new{format="csv"}))Check((int)denied.StatusCode==403,"DEPT_MANAGER export denied before snapshot lookup");
            using(var denied=await Post("/api/reports/snapshots/unknown/export",new{format="xlsx"}))Check((int)denied.StatusCode==403,"DEPT_MANAGER XLSX denied despite REPORT_VIEW");
            checks.Add(new { reportBrowser = await SprintReportsBrowser.RunAsync(username,password,branch.BranchId,department,"denied",ct) });
            db.UserRoles.Add(new(){UserId=actor,RoleId=ownerRole,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            using(var allowed=await http.GetAsync("/api/reports/options",ct))Check((int)allowed.StatusCode==200,"OWNER REPORT_VIEW options accepted alongside denied role");
            using(var denied=await Post("/api/reports/tasks/preview",new{branchId=branch.BranchId},false))Check((int)denied.StatusCode==400,"report preview requires CSRF");
            using var preview=await Post("/api/reports/tasks/preview",new{branchId=branch.BranchId});preview.EnsureSuccessStatusCode();
            var report=await preview.Content.ReadFromJsonAsync<JsonElement>(ct);var id=report.GetProperty("snapshotId").GetString()!;
            Check(report.GetProperty("totalCount").GetInt32()==1,"authorized preview materializes scoped task");
            var expectedCsv=HotelOps.Api.Services.Insights.ReportCsvRenderer.Render(
                JsonSerializer.Deserialize<HotelOps.Api.DTOs.Insights.ReportResult>(report.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Content;
            await db.OperationalTasks.Where(t=>t.TaskCode==prefix).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.ProgressPercent,(byte)73),ct);
            using(var export=await Post($"/api/reports/snapshots/{id}/export",new{format="csv"}))
            {
                var csv=await export.Content.ReadAsByteArrayAsync(ct);
                checks.Add(new{exportStatus=(int)export.StatusCode});
                Check((int)export.StatusCode==200&&csv.SequenceEqual(expectedCsv),"CSV exports exact frozen preview after source change");
            }
            Check(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="REPORT_EXPORT"&&a.EntityId==id&&a.Action=="CREATE",ct)==1,"CSV export audit committed before delivery");
            using(var denied=await Post($"/api/reports/snapshots/{id}/export",new{format="xlsx"},false))Check((int)denied.StatusCode==400,"XLSX requires CSRF");
            var expectedXlsx=HotelOps.Api.Services.Insights.ReportXlsxRenderer.Render(
                JsonSerializer.Deserialize<HotelOps.Api.DTOs.Insights.ReportResult>(report.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Content;
            using(var export=await Post($"/api/reports/snapshots/{id}/export",new{format="xlsx"}))
                Check((int)export.StatusCode==200 && export.Content.Headers.ContentType?.MediaType==HotelOps.Api.Services.Insights.ReportXlsxRenderer.ContentType
                    && (await export.Content.ReadAsByteArrayAsync(ct)).SequenceEqual(expectedXlsx),"XLSX exports frozen snapshot after source changed");
            Check(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="REPORT_EXPORT"&&a.EntityId==id&&a.NewValues!.Contains("\"format\":\"xlsx\""),ct)==1,"XLSX audit records actual format");
            await db.UserRoles.Where(r=>r.UserId==actor&&r.RoleId==ownerRole).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            using(var denied=await Post($"/api/reports/snapshots/{id}/export",new{format="csv"}))Check((int)denied.StatusCode==403,"revoked allowed role prevents export of owned cached snapshot");
            using(var denied=await Post($"/api/reports/snapshots/{id}/export",new{format="xlsx"}))Check((int)denied.StatusCode==403,"revoked role prevents XLSX export");
            Check(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="REPORT_EXPORT",ct)==2,"denied export creates no success audit");
            for(var index=1;index<=25;index++)
                db.OperationalTasks.Add(new(){TaskCode=prefix+"P"+index,Title="Synthetic report pagination",BranchId=branch.BranchId,DepartmentId=department,
                    Status="TODO",ProgressPercent=0,CreatedBy=actor,CreatedAt=DateTime.UtcNow});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.UserRoles.Where(r=>r.UserId==actor&&r.RoleId==ownerRole).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
            async Task Browser(string mode)
            {
                var result=await SprintReportsBrowser.RunAsync(username,password,branch.BranchId,department,mode,ct);
                checks.Add(new { reportBrowser=result });
                foreach(var snapshot in result.GetProperty("exports").EnumerateArray())
                {
                    var snapshotId=snapshot.GetString();
                    Check(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="REPORT_EXPORT"&&a.EntityId==snapshotId&&a.Action=="CREATE",ct)==2,"browser CSV and XLSX export each have a persisted audit: "+mode);
                }
                Check(await db.UserSessions.CountAsync(s=>s.UserId==actor&&s.RevokedAt==null,ct)==1,"browser session revoked on logout: "+mode);
            }
            await Browser("owner");
            await db.UserRoles.Where(r=>r.UserId==actor&&r.RoleId==ownerRole).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            var operationsRole=await db.Roles.Where(r=>r.RoleCode=="OPERATIONS_MANAGER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            db.UserRoles.Add(new(){UserId=actor,RoleId=operationsRole,BranchId=branch.BranchId,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await Browser("operations-manager");
            Check(await db.OperationalTasks.CountAsync(t=>t.BranchId==branch.BranchId,ct)==26
                && await db.OperationalTasks.Where(t=>t.TaskCode==prefix).Select(t=>t.ProgressPercent).SingleAsync(ct)==73
                && await db.OperationalTasks.Where(t=>t.BranchId==branch.BranchId&&t.TaskCode!=prefix).AllAsync(t=>t.ProgressPercent==0&&t.Status=="TODO",ct),
                "browser previews and CSV leave synthetic business rows unchanged");
        }
        finally
        {
            db.ChangeTracker.Clear();await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.ActorUserId==actor&&a.EntityType=="REPORT_EXPORT").ExecuteDeleteAsync(CancellationToken.None);
            await db.OperationalTasks.Where(t=>t.BranchId==branch.BranchId&&t.TaskCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==branch.BranchId).ExecuteDeleteAsync(CancellationToken.None);
            if(department>0)await db.Departments.Where(d=>d.DepartmentId==department).ExecuteDeleteAsync(CancellationToken.None);
            await db.Branches.Where(b=>b.BranchId==branch.BranchId).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None);checks.Add(new{reportFixtureCleanup=true});
        }
    }
}
