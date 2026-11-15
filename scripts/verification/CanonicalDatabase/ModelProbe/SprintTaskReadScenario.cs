using System.Text.Json;
using System.Diagnostics;
using System.Net.Http.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskReadScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,List<object> checks,CancellationToken ct,string username,string password,bool criteria=false,bool resume=false)
    {
        if(await db.Branches.AnyAsync(ct)||await db.OperationalTasks.AnyAsync(ct)||await db.Attachments.AnyAsync(ct))throw new Exception("Empty task/evidence fixture required");
        var evidenceRoot=Path.GetFullPath("TestResults/sprint25-evidence");
        if(Directory.Exists(evidenceRoot) && ((File.GetAttributes(evidenceRoot)&FileAttributes.ReparsePoint)!=0 || Directory.EnumerateFileSystemEntries(evidenceRoot).Any()))
            throw new Exception("Empty owned evidence directory required");
        var ownedFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string EvidencePath(string key)
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(key,"^[a-f0-9]{32}\\.(png|jpg|pdf)$"))throw new Exception("Unexpected evidence storage key");
            var full=Path.GetFullPath(Path.Combine(evidenceRoot,key));
            if(Path.GetDirectoryName(full)!=evidenceRoot)throw new Exception("Evidence path escaped owned root");
            return full;
        }
        var prefix="D3-TASK-"+Guid.NewGuid().ToString("N")[..8];
        long[] branches=[];long[] tasks=[];long department=0;long assignment=0;
        async Task Seed()
        {
            var b1=new Branch{BranchCode=prefix+"A",BranchName="Synthetic task branch",IsActive=true};
            var b2=new Branch{BranchCode=prefix+"B",BranchName="Synthetic foreign task branch",IsActive=true};
            db.Branches.AddRange(b1,b2);await db.SaveChangesAsync(ct);branches=[b1.BranchId,b2.BranchId];
            var d1=new Department{BranchId=b1.BranchId,DepartmentCode="TASK",DepartmentName="Synthetic tasks",IsActive=true};
            var d2=new Department{BranchId=b2.BranchId,DepartmentCode="TASK",DepartmentName="Foreign tasks",IsActive=true};
            db.Departments.AddRange(d1,d2);await db.SaveChangesAsync(ct);department=d1.DepartmentId;
            var rows=Enumerable.Range(0,4).Select(i=>new OperationalTask{TaskCode=prefix+i,Title="Synthetic task "+i,
                BranchId=i==2?b2.BranchId:b1.BranchId,DepartmentId=i>=2?d2.DepartmentId:d1.DepartmentId,
                Status=i is 0 or 3?"IN_PROGRESS":"TODO",ActualStartAt=i==0?DateTime.UtcNow.AddMinutes(-5):null,Description="Synthetic description",CreatedBy=actor,CreatedAt=DateTime.UtcNow}).ToArray();
            db.OperationalTasks.AddRange(rows);await db.SaveChangesAsync(ct);tasks=rows.Select(t=>t.TaskId).ToArray();
            var a=new OperationalTaskAssignment{TaskId=tasks[0],AssigneeId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow.AddMinutes(-1),AssignmentStatus="ACTIVE"};
            db.OperationalTaskAssignments.Add(a);
            db.OperationalTaskProgresses.AddRange(new OperationalTaskProgress{TaskId=tasks[0],ReportedBy=actor,ReportedAt=DateTime.UtcNow.AddMinutes(-1),ProgressPercent=10,NewStatus="TODO",Description="Earlier synthetic update"},
                new OperationalTaskProgress{TaskId=tasks[0],ReportedBy=actor,ReportedAt=DateTime.UtcNow,ProgressPercent=20,NewStatus="TODO",Description="Latest synthetic update"});
            await db.SaveChangesAsync(ct);assignment=a.AssignmentId;db.ChangeTracker.Clear();
            if(!criteria)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({tasks[0]},'IN_PROGRESS')",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({tasks[3]},'IN_PROGRESS')",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.operational_task_assignments(task_id,assignee_id,assigned_by,assigned_at,assignment_status) VALUES({tasks[3]},{actor},{actor},SYSUTCDATETIME(),'ACTIVE')",ct);
        }
        async Task Cleanup(CancellationToken token)
        {
            if (resume) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE e FROM dbo.task_pause_events e JOIN dbo.operational_task_progress p ON p.progress_id=e.progress_id WHERE p.reported_by={actor}", token);
            await db.WorkIntervals.Where(i=>i.UserId==actor && i.WorkItemKind=="TASK" && tasks.Contains(i.WorkItemId)).ExecuteDeleteAsync(token);
            await db.Workdays.Where(d=>d.UserId==actor && branches.Contains(d.BranchId)).ExecuteDeleteAsync(token);
            await db.AuditLogs.Where(a=>a.ActorUserId==actor && a.EntityType=="work_intervals").ExecuteDeleteAsync(token);
            var attachments=await db.Attachments.AsNoTracking().Where(a=>a.EntityType=="TASK"&&tasks.Contains(a.EntityId)&&a.UploadedBy==actor).ToArrayAsync(token);
            foreach(var attachment in attachments)
            {
                ownedFiles.Add(EvidencePath(attachment.StorageKey));
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.progress_evidence_links WHERE attachment_id={attachment.AttachmentId}",token);
            }
            await db.Attachments.Where(a=>a.EntityType=="TASK"&&tasks.Contains(a.EntityId)&&a.UploadedBy==actor).ExecuteDeleteAsync(token);
            foreach(var task in tasks) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.task_progress_versions WHERE task_id={task}",token);
            await db.AuditLogs.Where(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks").ExecuteDeleteAsync(token);
            await db.OperationalTaskProgresses.Where(p=>tasks.Contains(p.TaskId)).ExecuteDeleteAsync(token);
            await db.OperationalTaskAssignments.Where(a=>tasks.Contains(a.TaskId)).ExecuteDeleteAsync(token);
            await db.OperationalTasks.Where(t=>tasks.Contains(t.TaskId)&&t.CreatedBy==actor&&t.TaskCode.StartsWith(prefix)).ExecuteDeleteAsync(token);
            await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId.HasValue&&branches.Contains(r.BranchId.Value)).ExecuteDeleteAsync(token);
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&branches.Contains(a.BranchId)).ExecuteDeleteAsync(token);
            await db.Departments.Where(d=>branches.Contains(d.BranchId)).ExecuteDeleteAsync(token);
            await db.Branches.Where(b=>branches.Contains(b.BranchId)&&b.BranchCode.StartsWith(prefix)).ExecuteDeleteAsync(token);
            db.ChangeTracker.Clear();
        }
        await using(var rehearsal=await db.Database.BeginTransactionAsync(ct)){await Seed();await Cleanup(ct);await rehearsal.RollbackAsync(ct);}
        async Task<JsonElement> Get(string path,int status)
        {
            using var response=await http.GetAsync(path,ct);checks.Add(new{taskPath=path,expected=status,actual=(int)response.StatusCode});
            if((int)response.StatusCode!=status)throw new Exception("Task status mismatch: "+path+" actual="+(int)response.StatusCode);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return json.RootElement.Clone();
        }
        try
        {
            await using(var seed=await db.Database.BeginTransactionAsync(ct)){await Seed();await seed.CommitAsync(ct);}
            var root=$"/api/branches/{branches[0]}/operational-tasks";
            await Get(root,403);
            var role=await db.Roles.Where(r=>r.RoleCode=="HK_STAFF"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            var grant=new UserRole{UserId=actor,RoleId=role,BranchId=branches[0],IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)};
            db.UserRoles.Add(grant);db.UserBranchAccesses.Add(new(){UserId=actor,BranchId=branches[0],AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddMinutes(-2)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            if(resume) { await SprintTaskResumeScenario.RunAsync(db,http,actor,branches[0],department,tasks[0],tasks[1],checks,ct,username,password); return; }
            var page=await Get(root,200);if(page.GetProperty("totalCount").GetInt32()!=1||page.GetProperty("items")[0].GetProperty("id").GetInt64()!=tasks[0])throw new Exception("OWN list leaked tasks");
            var detail=await Get(root+"/"+tasks[0]+"?pageSize=1",200);
            if(detail.GetProperty("progressTotalCount").GetInt32()!=2||detail.GetProperty("progress")[0].GetProperty("description").GetString()!="Latest synthetic update")throw new Exception("History order/page mismatch");
            await Get(root+"/"+tasks[1],404);await Get(root+"/"+tasks[2],404);await Get($"/api/branches/{branches[1]}/operational-tasks",403);
            await Get(root+"?page=0",400);await Get(root+"?pageSize=101",400);await Get(root+"?page=2147483647",400);
            if(criteria)
            {
                if(!await ProgressSchema.InstalledAsync(db,ct))throw new Exception("Progress schema incompatible");
                using var setupCsrfResponse=await http.GetAsync("/api/auth/csrf",ct);setupCsrfResponse.EnsureSuccessStatusCode();
                using var setupCsrfJson=JsonDocument.Parse(await setupCsrfResponse.Content.ReadAsStringAsync(ct));
                var setupCsrf=setupCsrfJson.RootElement.GetProperty("csrfToken").GetString()!;
                async Task Initialize(long id,int expected,bool token=true)
                {
                    using var req=new HttpRequestMessage(HttpMethod.Post,root+"/"+id+"/progress-initialization"){
                        Content=JsonContent.Create(new{items=new[]{new{label="Synthetic duplicate attempt",required=true}},reason="Synthetic initialization authority check"})};
                    if(token)req.Headers.Add("X-CSRF-TOKEN",setupCsrf);
                    using var response=await http.SendAsync(req,ct);checks.Add(new{initializationExpected=expected,actual=(int)response.StatusCode});
                    if((int)response.StatusCode!=expected)throw new Exception("Initialization status mismatch: "+(int)response.StatusCode);
                }
                await Initialize(tasks[0],403);await Initialize(tasks[0],400,false);
                var managerRole=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
                var manager=new UserRole{UserId=actor,RoleId=managerRole,BranchId=branches[0],IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)};
                db.UserRoles.Add(manager);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                await Initialize(tasks[3],404);await Initialize(tasks[1],409);
                var setup=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                setup.ArgumentList.Add("frontend/scripts/sprintTaskInitializeBrowser.mjs");
                using(var process=Process.Start(setup)??throw new Exception("Setup browser unavailable"))
                {
                    try{
                        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch=branches[0],code=prefix+"0"}).AsMemory(),ct);process.StandardInput.Close();
                        var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);
                        using var json=JsonDocument.Parse(await output);await errors;checks.Add(new{taskInitializationBrowser=json.RootElement.Clone()});
                        if(!json.RootElement.GetProperty("success").GetBoolean())throw new Exception("Initialization browser failed at "+json.RootElement.GetProperty("stage").GetString());
                    }finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
                }
                await Initialize(tasks[0],409);
                await db.UserRoles.Where(r=>r.UserRoleId==manager.UserRoleId).ExecuteDeleteAsync(ct);
                checks.Add(new{criteriaInitializedThroughBrowserAndApi=true});
            }
            var progressPath=root+"/"+tasks[0]+"/progress";
            using var initial=await http.GetAsync(progressPath,ct);initial.EnsureSuccessStatusCode();
            var version=initial.Headers.ETag?.ToString()??throw new Exception("Missing progress ETag");
            using var csrfResponse=await http.GetAsync("/api/auth/csrf",ct);csrfResponse.EnsureSuccessStatusCode();
            using var csrfJson=JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync(ct));
            var csrf=csrfJson.RootElement.GetProperty("csrfToken").GetString()!;
            async Task Put(string path,int status,string? match,string? token,object body)
            {
                using var request=new HttpRequestMessage(HttpMethod.Put,path){Content=JsonContent.Create(body)};
                if(match!=null)request.Headers.TryAddWithoutValidation("If-Match",match);
                if(token!=null)request.Headers.Add("X-CSRF-TOKEN",token);
                using var response=await http.SendAsync(request,ct);
                checks.Add(new{taskProgressPath=path,expected=status,actual=(int)response.StatusCode});
                if((int)response.StatusCode!=status)throw new Exception("Task progress status mismatch: "+(int)response.StatusCode);
            }
            var update=new{percent=100,items=Array.Empty<object>(),note="HTTPS synthetic progress"};
            await Put(progressPath,400,version,null,update);
            await Put(progressPath,428,null,csrf,update);
            await Put(progressPath,400,version,csrf,new{percent=101,items=Array.Empty<object>(),note="invalid"});
            await Put(progressPath,400,version,csrf,new{percent=50,items=new[]{new{id=long.MaxValue,outcome="PASS"}},note="foreign criterion"});
            // First file is stored, then a bad second file must roll back SQL and remove the owned file.
            await Put(progressPath,400,version,csrf,new{percent=50,items=Array.Empty<object>(),note="invalid second evidence",
                photos=new[]{
                    new{name="synthetic-valid.png",mimeType="image/png",base64="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="},
                    new{name="synthetic-invalid.png",mimeType="image/png",base64="bm90IGFuIGltYWdl"}}});
            if(await db.Attachments.AnyAsync(ct)||await db.OperationalTaskProgresses.CountAsync(p=>p.TaskId==tasks[0],ct)!=2
                ||Directory.Exists(evidenceRoot)&&Directory.EnumerateFileSystemEntries(evidenceRoot).Any())
                throw new Exception("Invalid second evidence did not roll back progress/attachment/file");
            checks.Add(new{invalidSecondEvidenceRollsBackSqlAndPrivateFile=true});
            await Put(progressPath,200,version,csrf,update);
            await Put(progressPath,412,version,csrf,update);
            await Get(root+"/"+tasks[3]+"/progress",403);
            await Put(root+"/"+tasks[3]+"/progress",403,version,csrf,update);
            var persisted=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==tasks[0],ct);
            if(persisted.ProgressPercent!=100||persisted.Status!="IN_PROGRESS"||persisted.CompletedAt!=null
                ||await db.OperationalTaskProgresses.CountAsync(p=>p.TaskId==tasks[0],ct)!=3)
                throw new Exception("Progress persisted incorrectly or duplicate was applied");
            checks.Add(new{taskProgressPersisted=true,percent=100,status=persisted.Status,duplicateRejected=true});
            var taskKey=tasks[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)!=(criteria?2:1))
                throw new Exception("Progress audit missing or failed requests produced audit");
            using var raceRead=await http.GetAsync(progressPath,ct);raceRead.EnsureSuccessStatusCode();
            var raceVersion=raceRead.Headers.ETag!.ToString();
            async Task<int> Race(string note)
            {
                using var request=new HttpRequestMessage(HttpMethod.Put,progressPath){Content=JsonContent.Create(new{percent=90,items=Array.Empty<object>(),note})};
                request.Headers.Add("If-Match",raceVersion);request.Headers.Add("X-CSRF-TOKEN",csrf);
                using var response=await http.SendAsync(request,ct);return (int)response.StatusCode;
            }
            var race=await Task.WhenAll(Race("Concurrent synthetic A"),Race("Concurrent synthetic B"));
            if(!race.Order().SequenceEqual(new[]{200,412})||await db.OperationalTaskProgresses.CountAsync(p=>p.TaskId==tasks[0],ct)!=4
                ||await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)!=(criteria?3:2))
                throw new Exception("Concurrent progress lost update or duplicate audit");
            checks.Add(new{concurrentProgressStatuses=race,singleWinnerAndAudit=true});
            await db.OperationalTaskAssignments.Where(a=>a.AssignmentId==assignment).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.EndedAt,DateTime.UtcNow),ct);
            if((await Get(root,200)).GetProperty("totalCount").GetInt32()!=0)throw new Exception("Ended assignment retained OWN read");
            await Get(root+"/"+tasks[0],404);
            await Get(progressPath,403);
            await Put(progressPath,403,version,csrf,update);
            await db.UserRoles.Where(r=>r.UserRoleId==grant.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ExpiresAt,DateTime.UtcNow.AddMinutes(-1)),ct);
            await Get(root,403);
            var supervisor=await db.Roles.Where(r=>r.RoleCode=="HK_SUPERVISOR"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            await db.UserRoles.Where(r=>r.UserRoleId==grant.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,supervisor).SetProperty(r=>r.ExpiresAt,(DateTime?)null),ct);
            if((await Get(root,200)).GetProperty("totalCount").GetInt32()!=2)throw new Exception("Branch scope or department integrity failed");
            await Get(root+"/"+tasks[3],404);
            if((await Get(root+"?search="+prefix+"1",200)).GetProperty("totalCount").GetInt32()!=1)throw new Exception("Search mismatch");
            await Get(root+"/"+tasks[1],200);
            // Restore the synthetic executor and existing TASK_EXECUTE grant for the browser write.
            await db.OperationalTaskAssignments.Where(a=>a.AssignmentId==assignment).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.EndedAt,(DateTime?)null),ct);
            db.UserRoles.Add(new(){UserId=actor,RoleId=role,BranchId=branches[0],IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-2)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={tasks[0]}",ct);
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintTaskReadBrowser.mjs");
            using(var process=Process.Start(start)??throw new Exception("Browser unavailable"))
            {
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch=branches[0],foreign=branches[1],code=prefix+"0",criteria}).AsMemory(),ct);process.StandardInput.Close();
                    var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);var result=await output;await errors;
                    using var json=JsonDocument.Parse(result);checks.Add(new{taskBrowser=json.RootElement.Clone()});
                    if(!json.RootElement.GetProperty("success").GetBoolean())throw new Exception("Task browser failed at "+json.RootElement.GetProperty("stage").GetString());
                }
                finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
            }
            if(!await db.OperationalTaskProgresses.AnyAsync(p=>p.TaskId==tasks[0]&&p.Description=="Browser synthetic progress"&&p.ProgressPercent==75,ct)
                ||!await db.OperationalTasks.AnyAsync(t=>t.TaskId==tasks[0]&&t.ProgressPercent==75&&t.Status=="IN_PROGRESS"&&t.CompletedAt==null,ct))
                throw new Exception("Browser progress not persisted");
            if(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)!=(criteria?4:3))
                throw new Exception("Browser progress audit missing");
            checks.Add(new{browserProgressPersisted=true,pausedProgressAccepted=true,threeSuccessfulWriteAudits=true});
            if(criteria)
            {
                var results=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.task_completion_criteria WHERE task_id={tasks[0]} AND ((sort_order=0 AND outcome='PASS' AND note='Browser required checked') OR (sort_order=1 AND outcome='FAIL' AND note='Browser optional issue'))").SingleAsync(ct);
                if(results!=2)throw new Exception("Criterion outcomes/notes were not persisted");
                var audit=await db.AuditLogs.AsNoTracking().Where(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey).OrderByDescending(a=>a.AuditId).Select(a=>a.NewValues).FirstAsync(ct);
                if(audit==null||!audit.Contains("Browser required checked")||!audit.Contains("Browser optional issue"))throw new Exception("Criterion audit incomplete");
                checks.Add(new{criteriaPassFailAndNotesPersisted=true,criteriaAuditVerified=true});
            }
            var attachment=await db.Attachments.AsNoTracking().SingleAsync(a=>a.EntityType=="TASK"&&a.EntityId==tasks[0]&&a.UploadedBy==actor,ct);
            var stored=EvidencePath(attachment.StorageKey);ownedFiles.Add(stored);
            var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
            if(!File.Exists(stored)||!(await File.ReadAllBytesAsync(stored,ct)).SequenceEqual(bytes)
                ||attachment.OriginalFileName!="synthetic-task.png"||attachment.FileSizeBytes!=bytes.Length)
                throw new Exception("Evidence storage mismatch; API must use the dedicated trial evidence root");
            var evidencePath=progressPath+"/evidence/"+attachment.AttachmentId;
            using(var downloaded=await http.GetAsync(evidencePath,ct))
            {
                downloaded.EnsureSuccessStatusCode();
                if(!(await downloaded.Content.ReadAsByteArrayAsync(ct)).SequenceEqual(bytes)
                    ||downloaded.Content.Headers.ContentType?.MediaType!="image/png"
                    ||downloaded.Content.Headers.ContentDisposition?.DispositionType!="attachment")throw new Exception("Protected evidence response mismatch");
            }
            await Get(progressPath+"/evidence/"+long.MaxValue,404);
            var linked=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.progress_evidence_links e JOIN dbo.operational_task_progress p ON p.progress_id=e.task_progress_id WHERE e.attachment_id={attachment.AttachmentId} AND p.task_id={tasks[0]} AND p.description='Browser synthetic progress'").SingleAsync(ct);
            if(linked!=1)throw new Exception("Evidence is not linked to its committed progress update");
            checks.Add(new{taskEvidenceStoredAndDownloaded=true,progressEvidenceLinkVerified=true});
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==branches[0]).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
            await Get(root,403);
            await Get(evidencePath,403);
        }
        finally
        {
            if(criteria)
            {
                // Immutable criteria remain until the owner drops this entire clone.
                foreach(var key in await db.Attachments.AsNoTracking().Where(a=>a.EntityType=="TASK"&&tasks.Contains(a.EntityId)&&a.UploadedBy==actor).Select(a=>a.StorageKey).ToArrayAsync(CancellationToken.None))
                    ownedFiles.Add(EvidencePath(key));
                foreach(var file in ownedFiles)File.Delete(file);
                if(!await ProgressSchema.InstalledAsync(db,CancellationToken.None))throw new Exception("Progress schema changed during criteria trial");
                checks.Add(new{criteriaTriggerPreserved=true,wholeCloneDropRequired=true});
            }
            else
            {
            await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);await Cleanup(CancellationToken.None);await cleanup.CommitAsync(CancellationToken.None);
            foreach(var file in ownedFiles)File.Delete(file);
            if(Directory.Exists(evidenceRoot)&&Directory.EnumerateFileSystemEntries(evidenceRoot).Any())throw new Exception("Evidence cleanup incomplete");
            if(await db.Attachments.AnyAsync())throw new Exception("Attachment cleanup incomplete");
            if(await db.OperationalTasks.AnyAsync()||await db.Branches.AnyAsync())throw new Exception("Task fixture cleanup incomplete");
            checks.Add(new{taskFixtureCleanup=true});
            }
        }
    }
}
