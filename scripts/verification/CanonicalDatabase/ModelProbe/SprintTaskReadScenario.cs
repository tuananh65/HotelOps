using System.Text.Json;
using System.Diagnostics;
using System.Net.Http.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskReadScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,List<object> checks,CancellationToken ct,string username,string password)
    {
        if(await db.Branches.AnyAsync(ct)||await db.OperationalTasks.AnyAsync(ct))throw new Exception("Empty task fixture required");
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
                Status=i is 0 or 3?"IN_PROGRESS":"TODO",Description="Synthetic description",CreatedBy=actor,CreatedAt=DateTime.UtcNow}).ToArray();
            db.OperationalTasks.AddRange(rows);await db.SaveChangesAsync(ct);tasks=rows.Select(t=>t.TaskId).ToArray();
            var a=new OperationalTaskAssignment{TaskId=tasks[0],AssigneeId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow.AddMinutes(-1),AssignmentStatus="ACTIVE"};
            db.OperationalTaskAssignments.Add(a);
            db.OperationalTaskProgresses.AddRange(new OperationalTaskProgress{TaskId=tasks[0],ReportedBy=actor,ReportedAt=DateTime.UtcNow.AddMinutes(-1),ProgressPercent=10,NewStatus="TODO",Description="Earlier synthetic update"},
                new OperationalTaskProgress{TaskId=tasks[0],ReportedBy=actor,ReportedAt=DateTime.UtcNow,ProgressPercent=20,NewStatus="TODO",Description="Latest synthetic update"});
            await db.SaveChangesAsync(ct);assignment=a.AssignmentId;db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({tasks[0]},'IN_PROGRESS'),({tasks[3]},'IN_PROGRESS')",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.operational_task_assignments(task_id,assignee_id,assigned_by,assigned_at,assignment_status) VALUES({tasks[3]},{actor},{actor},SYSUTCDATETIME(),'ACTIVE')",ct);
        }
        async Task Cleanup(CancellationToken token)
        {
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
            var page=await Get(root,200);if(page.GetProperty("totalCount").GetInt32()!=1||page.GetProperty("items")[0].GetProperty("id").GetInt64()!=tasks[0])throw new Exception("OWN list leaked tasks");
            var detail=await Get(root+"/"+tasks[0]+"?pageSize=1",200);
            if(detail.GetProperty("progressTotalCount").GetInt32()!=2||detail.GetProperty("progress")[0].GetProperty("description").GetString()!="Latest synthetic update")throw new Exception("History order/page mismatch");
            await Get(root+"/"+tasks[1],404);await Get(root+"/"+tasks[2],404);await Get($"/api/branches/{branches[1]}/operational-tasks",403);
            await Get(root+"?page=0",400);await Get(root+"?pageSize=101",400);await Get(root+"?page=2147483647",400);
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
            if(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)!=1)
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
                ||await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)!=2)
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
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch=branches[0],foreign=branches[1],code=prefix+"0"}).AsMemory(),ct);process.StandardInput.Close();
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
            if(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)!=3)
                throw new Exception("Browser progress audit missing");
            checks.Add(new{browserProgressPersisted=true,pausedProgressAccepted=true,threeSuccessfulWriteAudits=true});
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==branches[0]).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
            await Get(root,403);
        }
        finally
        {
            await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);await Cleanup(CancellationToken.None);await cleanup.CommitAsync(CancellationToken.None);
            if(await db.OperationalTasks.AnyAsync()||await db.Branches.AnyAsync())throw new Exception("Task fixture cleanup incomplete");
            checks.Add(new{taskFixtureCleanup=true});
        }
    }
}
