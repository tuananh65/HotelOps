using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintReportActorsScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long branch,
        long department, List<object> checks, CancellationToken ct, string username, string password)
    {
        // Entry follows SprintAuthProbe's exact clone GUID/run verification; immutable fixtures
        // are retained until the runner verifies and drops the entire owned clone.
        if (!db.Database.GetDbConnection().Database.StartsWith("HotelOps_DEV3_142A_CriteriaTrial_", StringComparison.Ordinal))
            throw new Exception("Verified whole clone required");
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { reportActorsCheck = label }); }
        async Task<HttpResponseMessage> Post(string path, object body)
        {
            var csrf = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("csrfToken").GetString());
            return await http.SendAsync(request, ct);
        }
        var prefix = "D3RA" + Guid.NewGuid().ToString("N")[..8];
        var role = await db.Roles.Where(r => r.RoleCode == "OPERATIONS_MANAGER" && r.IsActive).Select(r => r.RoleId).SingleAsync(ct);
        db.UserRoles.Add(new() { UserId = actor, RoleId = role, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync(ct);
        long cleaner;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.floors(branch_id,floor_code,floor_name) VALUES({branch},{prefix},'Synthetic report floor');
                DECLARE @floor bigint=SCOPE_IDENTITY();
                INSERT dbo.room_types(type_code,type_name,standard_capacity) VALUES({prefix},'Synthetic report type',1);
                DECLARE @type bigint=SCOPE_IDENTITY();
                INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status)
                  VALUES({branch},@floor,@type,'RPT-ACTORS','INSPECTION');
                DECLARE @room bigint=SCOPE_IDENTITY();
                INSERT dbo.reference_values(group_code,value_code,display_name) VALUES('CLEANING_TYPE',{prefix},'Synthetic');
                DECLARE @cleanType bigint=SCOPE_IDENTITY();
                INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at)
                  VALUES({prefix},@room,@cleanType,'IN_PROGRESS',{actor},'PENDING_INSPECTION',SYSUTCDATETIME());
                DECLARE @task bigint=SCOPE_IDENTITY();
                INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
                INSERT dbo.inspection_checklists(checklist_code,checklist_name,room_type_id) VALUES({prefix},'Synthetic',@type);
                DECLARE @list bigint=SCOPE_IDENTITY();
                INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@list,1,'DRAFT');
                DECLARE @version bigint=SCOPE_IDENTITY();
                INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order) VALUES(@version,'Synthetic',1,1);
                UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',published_by={actor},effective_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE version_id=@version;
                INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at,inspected_at)
                  VALUES({prefix},@room,@task,@version,{actor},'IN_PROGRESS','INSPECTING',SYSUTCDATETIME(),SYSUTCDATETIME());
                """, ct);
            var task = await db.CleaningTasks.Where(t => t.TaskNo == prefix).Select(t => t.CleaningTaskId).SingleAsync(ct);
            cleaner = await SprintApproveProbe.SeedCleanerAsync(db, actor, branch, task, "recipient-normal", ct);
            await tx.CommitAsync(ct);
        }
        var inspection = await db.RoomInspections.AsNoTracking().SingleAsync(i => i.InspectionNo == prefix, ct);
        async Task<JsonElement> Preview(long housekeeper, long? inspector = null)
        {
            using var response = await Post("/api/reports/inspections/preview", new { branchId = branch, housekeeperId = housekeeper, inspectorId = inspector });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        var initial = await Preview(cleaner, actor);
        Check(initial.GetProperty("rows").GetArrayLength() == 1 && initial.GetProperty("rows")[0].GetProperty("id").GetInt64() == inspection.InspectionId,
            "public combined cleaner/inspector filters select the officially attributed inspection");
        Check((await Preview(actor)).GetProperty("totalCount").GetInt32() == 0, "inspector and assigning actor are not inferred as cleaner");
        Check((await Preview(cleaner, cleaner)).GetProperty("totalCount").GetInt32() == 0, "inspector filter intersects cleaner filter");
        await db.Users.Where(u => u.UserId == cleaner).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
        Check((await Preview(cleaner, actor)).GetProperty("rows").GetArrayLength() == 1, "inactive cleaner retains immutable historical attribution");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL' WHERE inspection_id={inspection.InspectionId};
            UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={inspection.CleaningTaskId};
            INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,reason,requested_at,status,workflow_status,assignee_user_id)
              VALUES({inspection.CleaningTaskId},{inspection.InspectionId},'Synthetic report rework',SYSUTCDATETIME(),'PENDING','PENDING_ACCEPTANCE',{actor});
            """, ct);
        var frozenId = initial.GetProperty("snapshotId").GetString();
        using (var frozen = await Post($"/api/reports/snapshots/{frozenId}/export", new { format = "csv" }))
            Check((int)frozen.StatusCode == 200 && !(await frozen.Content.ReadAsStringAsync(ct)).Contains("PENDING"),
                "cached report does not acquire rework created after its snapshot");
        var browser = await SprintReportsBrowser.RunAsync(username, password, branch, department, "inspection-actors", ct, cleaner, actor);
        checks.Add(new { reportActorsBrowser = browser });
        db.OperationalTasks.Add(new() { TaskCode=prefix+"TASK",Title="Synthetic summary task",BranchId=branch,DepartmentId=department,
            Status="TODO",ProgressPercent=0,CreatedBy=actor,CreatedAt=DateTime.UtcNow });
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var taskBrowser=await SprintReportsBrowser.RunAsync(username,password,branch,department,"task-summary",ct);
        checks.Add(new { reportTaskSummaryBrowser=taskBrowser });
        foreach(var item in taskBrowser.GetProperty("exports").EnumerateArray())
        {
            var taskSnapshot=item.GetString();
            Check(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="REPORT_EXPORT"&&a.EntityId==taskSnapshot&&a.Action=="CREATE",ct)==2,
                "task summary browser CSV/XLSX each persist export audit");
        }
        foreach (var exported in browser.GetProperty("exports").EnumerateArray())
        {
            var id = exported.GetString();
            Check(await db.AuditLogs.CountAsync(a => a.ActorUserId == actor && a.EntityType == "REPORT_EXPORT" && a.EntityId == id, ct) == 2,
                "browser CSV and XLSX each persist an export audit for the actor-filtered snapshot");
        }
        var snapshot = initial.GetProperty("snapshotId").GetString();
        await db.UserRoles.Where(r => r.UserId == actor && r.RoleId == role).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        using (var denied = await Post($"/api/reports/snapshots/{snapshot}/export", new { format = "csv" }))
            Check((int)denied.StatusCode == 403, "revoked Operations Manager cannot export cached actor-filtered report despite DEPT_MANAGER");
        await db.UserRoles.Where(r => r.UserId == actor && r.RoleId == role).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status)
              SELECT branch_id,floor_id,room_type_id,'RPT-UNKNOWN','INSPECTION' FROM dbo.rooms WHERE room_id={inspection.RoomId};
            DECLARE @room bigint=SCOPE_IDENTITY();
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at)
              SELECT {prefix + "UNKNOWN"},@room,cleaning_type_id,'IN_PROGRESS',{actor},'PENDING_INSPECTION',SYSUTCDATETIME()
              FROM dbo.cleaning_tasks WHERE cleaning_task_id={inspection.CleaningTaskId};
            DECLARE @task bigint=SCOPE_IDENTITY();
            INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
            INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at,inspected_at)
              VALUES({prefix + "UNKNOWN"},@room,@task,{inspection.VersionId},{actor},'IN_PROGRESS','INSPECTING',SYSUTCDATETIME(),SYSUTCDATETIME());
            """, ct);
        using var incomplete = await Post("/api/reports/inspections/preview", new { branchId = branch, housekeeperId = cleaner });
        Check((int)incomplete.StatusCode == 503 && (await incomplete.Content.ReadAsStringAsync(ct)).Contains("HOUSEKEEPER_HISTORY_INCOMPLETE"),
            "unknown attribution fails closed in public preview instead of silently omitting history");
    }
}
