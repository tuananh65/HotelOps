using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskResumeScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long branch,long department,long task,long other,
        List<object> checks,CancellationToken ct,string username,string password)
    {
        var root=$"/api/branches/{branch}/operational-tasks/{task}";
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{resumeCheck=name});}
        async Task<string> Tag(){using var r=await http.GetAsync(root+"/progress",ct);r.EnsureSuccessStatusCode();return r.Headers.ETag!.ToString();}
        async Task<int> Post(string? tag,bool csrf=true,string? path=null)
        {
            using var r=new HttpRequestMessage(HttpMethod.Post,path??root+"/resume");
            if(tag!=null)r.Headers.TryAddWithoutValidation("If-Match",tag);
            if(csrf){var c=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);r.Headers.Add("X-CSRF-TOKEN",c.GetProperty("csrfToken").GetString());}
            using var response=await http.SendAsync(r,ct);return (int)response.StatusCode;
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={task}",ct);
        var original=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct);
        var tag=await Tag();
        Check(await Post(null)==428,"missing ETag refused");
        Check(await Post(tag,false)==400,"missing CSRF refused");
        Check(await Post(tag,true,$"/api/branches/{branch}/operational-tasks/{other}/resume")==403,"unassigned task refused");
        Check(await Post(tag)==409,"working workday required");
        Check(!await db.WorkIntervals.AnyAsync(ct)&&await Tag()==tag,"failed resume leaves interval and version unchanged");
        var day=new Workday{UserId=actor,BranchId=branch,DepartmentId=department,WorkDate=DateOnly.FromDateTime(DateTime.UtcNow),IsUnscheduled=true,
            UnscheduledReason="Synthetic isolated resume trial",Status="WORKING",StartedAt=DateTime.UtcNow.AddMinutes(-5)};
        db.Workdays.Add(day);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        Check(await Post(tag)==200,"paused task resumes with working workday");
        var interval=await db.WorkIntervals.AsNoTracking().SingleAsync(i=>i.UserId==actor,ct);
        var updated=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==task,ct);
        Check(interval.WorkItemKind=="TASK"&&interval.WorkItemId==task&&interval.ClosedAt==null&&interval.OpenedAt==interval.EffectiveStartedAt,
            "one open interval starts at server time");
        Check(updated.ProgressPercent==original.ProgressPercent&&updated.ActualStartAt==original.ActualStartAt,"resume preserves progress and original actual start");
        Check(await Post(tag)==412,"duplicate stale resume rejected");
        Check(await Post(await Tag())==409,"already running task rejected");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={task}",ct);
        var busyTag=await Tag();
        Check(await Post(busyTag)==409&&await Tag()==busyTag,"existing open interval prevents resume without changing task version");
        async Task ResetPaused()
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.work_intervals SET closed_at=SYSUTCDATETIME(),effective_ended_at=SYSUTCDATETIME(),closed_by={actor} WHERE user_id={actor} AND work_item_kind='TASK' AND work_item_id={task} AND closed_at IS NULL; UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={task}",ct);
        }
        // Synthetic reset only; not evidence for the unimplemented Pause endpoint.
        await ResetPaused();tag=await Tag();
        var races=await Task.WhenAll(Post(tag),Post(tag));
        Check(races.Order().SequenceEqual(new[]{200,412}),"concurrent same-version resumes produce one success and one stale result");
        Check(await db.WorkIntervals.CountAsync(i=>i.UserId==actor,ct)==2&&await db.WorkIntervals.CountAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct)==1,
            "concurrency never creates duplicate open intervals");
        await ResetPaused();
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("frontend/scripts/sprintTaskResumeBrowser.mjs");
        using(var process=Process.Start(start)??throw new Exception("Browser unavailable"))
        {
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,code=original.TaskCode}));process.StandardInput.Close();
                var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await stdout);_ = await stderr;
                checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"canonical resume browser accepted");
            }finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
        }
        var taskKey=task.ToString();
        Check(await db.WorkIntervals.CountAsync(i=>i.UserId==actor,ct)==3
            &&await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks"&&a.EntityId==taskKey,ct)==3
            &&await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="work_intervals",ct)==3,
            "three committed resumes each persist task and interval audits");
    }
}
