using System.Text.Json;
using System.Diagnostics;
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
                Status="TODO",Description="Synthetic description",CreatedBy=actor,CreatedAt=DateTime.UtcNow}).ToArray();
            db.OperationalTasks.AddRange(rows);await db.SaveChangesAsync(ct);tasks=rows.Select(t=>t.TaskId).ToArray();
            var a=new OperationalTaskAssignment{TaskId=tasks[0],AssigneeId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow.AddMinutes(-1),AssignmentStatus="ACTIVE"};
            db.OperationalTaskAssignments.Add(a);
            db.OperationalTaskProgresses.AddRange(new OperationalTaskProgress{TaskId=tasks[0],ReportedBy=actor,ReportedAt=DateTime.UtcNow.AddMinutes(-1),ProgressPercent=10,NewStatus="TODO",Description="Earlier synthetic update"},
                new OperationalTaskProgress{TaskId=tasks[0],ReportedBy=actor,ReportedAt=DateTime.UtcNow,ProgressPercent=20,NewStatus="TODO",Description="Latest synthetic update"});
            await db.SaveChangesAsync(ct);assignment=a.AssignmentId;db.ChangeTracker.Clear();
        }
        async Task Cleanup(CancellationToken token)
        {
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
            await db.OperationalTaskAssignments.Where(a=>a.AssignmentId==assignment).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.EndedAt,DateTime.UtcNow),ct);
            if((await Get(root,200)).GetProperty("totalCount").GetInt32()!=0)throw new Exception("Ended assignment retained OWN read");
            await Get(root+"/"+tasks[0],404);
            await db.UserRoles.Where(r=>r.UserRoleId==grant.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ExpiresAt,DateTime.UtcNow.AddMinutes(-1)),ct);
            await Get(root,403);
            var supervisor=await db.Roles.Where(r=>r.RoleCode=="HK_SUPERVISOR"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            await db.UserRoles.Where(r=>r.UserRoleId==grant.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RoleId,supervisor).SetProperty(r=>r.ExpiresAt,(DateTime?)null),ct);
            if((await Get(root,200)).GetProperty("totalCount").GetInt32()!=2)throw new Exception("Branch scope or department integrity failed");
            await Get(root+"/"+tasks[3],404);
            if((await Get(root+"?search="+prefix+"1",200)).GetProperty("totalCount").GetInt32()!=1)throw new Exception("Search mismatch");
            await Get(root+"/"+tasks[1],200);
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
