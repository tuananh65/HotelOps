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
        // Synthetic reset isolates Resume negatives; real Pause acceptance follows below.
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
        var reasons=await http.GetFromJsonAsync<JsonElement>(root+"/pause-reasons",ct);
        Check(reasons.GetArrayLength()==6,"six approved active reasons loaded from API");
        var otherReason=reasons.EnumerateArray().Single(r=>r.GetProperty("code").GetString()=="OTHER").GetProperty("id").GetInt64();
        var ordinary=reasons.EnumerateArray().Single(r=>r.GetProperty("code").GetString()=="WAITING_MATERIAL").GetProperty("id").GetInt64();
        async Task<int> Pause(string? etag,long reason,string? note=null,string? target=null,bool includeCsrf=true)
        {
            var csrf=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);
            using var request=new HttpRequestMessage(HttpMethod.Post,target??root+"/pause"){Content=JsonContent.Create(new{reasonId=reason,note})};
            if(includeCsrf)request.Headers.Add("X-CSRF-TOKEN",csrf.GetProperty("csrfToken").GetString());
            if(etag!=null)request.Headers.TryAddWithoutValidation("If-Match",etag);
            using var response=await http.SendAsync(request,ct);return (int)response.StatusCode;
        }
        tag=await Tag();
        Check(await Pause(null,ordinary)==428,"pause requires ETag");
        Check(await Pause(tag,ordinary,includeCsrf:false)==400,"pause requires CSRF");
        Check(await Pause(tag,otherReason," ")==400,"OTHER requires nonblank note");
        Check(await Pause(tag,ordinary,new string('x',1001))==400,"pause note persistence limit enforced");
        Check(await Pause(tag,long.MaxValue)==400,"unknown reason refused");
        Check(await Pause(tag,ordinary,null,$"/api/branches/{branch}/operational-tasks/{other}/pause")==403,"unassigned pause refused");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.reference_values SET is_active=0 WHERE reference_id={ordinary}",ct);
        try {Check(await Pause(tag,ordinary)==400,"inactive reason refused");}
        finally {await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.reference_values SET is_active=1 WHERE reference_id={ordinary}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.reference_values SET group_code='SYNTHETIC_WRONG_GROUP' WHERE reference_id={ordinary}",ct);
        try {Check(await Pause(tag,ordinary)==400,"reason from wrong group refused");}
        finally {await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.reference_values SET group_code='TASK_PAUSE_REASON' WHERE reference_id={ordinary}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.reference_values SET value_code='SYNTHETIC_UNAPPROVED' WHERE reference_id={ordinary}",ct);
        try {Check(await Pause(tag,ordinary)==400,"unapproved code in correct group refused");}
        finally {await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.reference_values SET value_code='WAITING_MATERIAL' WHERE reference_id={ordinary}",CancellationToken.None);}
        // Fail after interval close, task update and progress insert: all must roll back.
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_dev3_pause_fault ON dbo.task_pause_events AFTER INSERT AS THROW 51832,'Synthetic pause rollback trial',1;",ct);
        var beforeLogs=await db.OperationalTaskProgresses.CountAsync(p=>p.TaskId==task,ct);
        var beforeAudit=await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor,ct);
        try {Check(await Pause(tag,ordinary)>=500,"injected persistence failure rejected");}
        finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_dev3_pause_fault",CancellationToken.None);}
        Check(await Tag()==tag&&await db.WorkIntervals.CountAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct)==1
            &&await db.OperationalTaskProgresses.CountAsync(p=>p.TaskId==task,ct)==beforeLogs
            &&await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor,ct)==beforeAudit,"late failure rolls back interval, state, history and audits");
        Check(await Pause(tag,ordinary)==200,"approved reason pauses task without optional note");
        var ended=await db.WorkIntervals.AsNoTracking().Where(i=>i.UserId==actor).OrderByDescending(i=>i.IntervalId).FirstAsync(ct);
        Check(ended.ClosedAt!=null&&ended.EffectiveEndedAt==ended.ClosedAt&&ended.ClosedAt>=ended.OpenedAt,"pause closes interval at server time");
        Check(await Pause(tag,ordinary)==412,"duplicate pause stale ETag refused");
        Check(await Pause(await Tag(),ordinary)==409,"already paused task cannot pause again with fresh ETag");
        Check(await Post(await Tag())==200,"resume needs no new pause reason");
        var restarted=await db.WorkIntervals.AsNoTracking().SingleAsync(i=>i.UserId==actor&&i.ClosedAt==null,ct);
        Check(restarted.OpenedAt>ended.ClosedAt&&restarted.EffectiveStartedAt==restarted.OpenedAt,"paused gap is excluded from next work interval");
        tag=await Tag();var pauseRace=await Task.WhenAll(Pause(tag,otherReason,"Synthetic other note"),Pause(tag,otherReason,"Synthetic other note"));
        Check(pauseRace.Order().SequenceEqual(new[]{200,412}),"concurrent pause writes exactly once");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_pause_events e JOIN dbo.operational_task_progress p ON p.progress_id=e.progress_id WHERE p.task_id={task}").SingleAsync(ct)==2,"dedicated pause history persists both reasons");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_pause_events e JOIN dbo.operational_task_progress p ON p.progress_id=e.progress_id WHERE p.task_id={task} AND e.reason_code='OTHER' AND e.note='Synthetic other note' AND p.blocker_reason IS NULL").SingleAsync(ct)==1,"OTHER note snapshot stored without repurposing blocker reason");
        Check(await Post(await Tag())==200,"resume after OTHER accepted");
        var browserStart=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        browserStart.ArgumentList.Add("frontend/scripts/sprintTaskResumeBrowser.mjs");
        using(var process=Process.Start(browserStart)??throw new Exception("Browser unavailable"))
        {
            try {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,code=original.TaskCode,pause=true}));process.StandardInput.Close();
                var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);using var json=JsonDocument.Parse(await stdout);_ = await stderr;
                checks.Add(json.RootElement.Clone());Check(json.RootElement.GetProperty("success").GetBoolean(),"canonical pause-resume browser accepted");
            } finally {if(!process.HasExited)process.Kill(entireProcessTree:true);}
        }
        var allIntervals=await db.WorkIntervals.AsNoTracking().Where(i=>i.UserId==actor).OrderBy(i=>i.OpenedAt).ToArrayAsync(ct);
        var asOf=await db.Database.SqlQueryRaw<DateTime>("SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var effort=HotelOps.Api.Services.WorkTime.WorkIntervalMath.Summarize(allIntervals.Select(i=>new HotelOps.Api.Services.WorkTime.RecordedWorkRange(
            new DateTimeOffset(DateTime.SpecifyKind(i.EffectiveStartedAt,DateTimeKind.Utc)),i.EffectiveEndedAt is {} end?new DateTimeOffset(DateTime.SpecifyKind(end,DateTimeKind.Utc)):null)),
            new DateTimeOffset(DateTime.SpecifyKind(asOf,DateTimeKind.Utc)));
        long gaps=0;for(var index=1;index<allIntervals.Length;index++)gaps+=(allIntervals[index].EffectiveStartedAt-allIntervals[index-1].EffectiveEndedAt!.Value).Ticks;
        Check(gaps>0&&effort.Closed.Ticks+effort.ProvisionalOpen.Ticks+gaps==(asOf-allIntervals[0].EffectiveStartedAt).Ticks,"actual effort excludes every paused gap and separates provisional open work");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_pause_events e JOIN dbo.operational_task_progress p ON p.progress_id=e.progress_id WHERE p.task_id={task}").SingleAsync(ct)==6
            &&await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="operational_tasks",ct)==15
            &&await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="work_intervals",ct)==15,"all six pauses and nine resumes persist matching task and interval audits");
        checks.Add(new{closedEffortTicks=effort.Closed.Ticks,provisionalOpenTicks=effort.ProvisionalOpen.Ticks,excludedGapTicks=gaps});
    }
}
