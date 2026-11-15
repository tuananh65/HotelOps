using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintInspectionAssignmentExceptionsProbe
{
    // The initial-assignment parent owns the exact verified disposable fixture and HTTPS7143 host.
    internal static async Task<object> VerifyAsync(HotelOpsDbContext db,long actor,long other,long branch,long task,string password,CancellationToken ct)
    {
        if(await db.RoomInspections.AnyAsync(i=>i.CleaningTaskId==task,ct)
            ||await db.CleaningAssignments.AnyAsync(a=>a.CleaningTaskId==task&&a.AssigneeUserId==other,ct))throw new Exception("Fresh owned assignment fixture required");
        var version=await db.InspectionChecklistVersions.Where(v=>v.Status=="PUBLISHED").Select(v=>v.VersionId).SingleAsync(ct);
        var checks=new List<JsonElement>();
        async Task Browser(string mode)
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintInspectionAssignmentExceptionsBrowser.mjs");
            using var process=Process.Start(start)??throw new Exception("Exception browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username="D3-EVIDENCE",password,actor,other,branch,task,version,mode}).AsMemory(),ct);
                process.StandardInput.Close();var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await stdout);await stderr;
                if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Assignment exception browser: "+result.GetRawText());
                if(await db.RoomInspections.AnyAsync(i=>i.CleaningTaskId==task,ct)
                    ||await db.AuditLogs.AnyAsync(a=>a.EntityType=="INSPECTION_ASSIGNMENT"&&a.ActorUserId==actor,ct))throw new Exception("Rejected assignment leaked persistence");
                checks.Add(result);
            }
            finally{if(!process.HasExited)process.Kill(true);}
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='RETIRED' WHERE version_id={version}",ct);
        try{await Browser("checklist");}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED' WHERE version_id={version}",CancellationToken.None);}
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({task},{other},{actor},SYSUTCDATETIME(),'COMPLETED')",ct);
        try{await Browser("cleaner");}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.cleaning_assignments WHERE cleaning_task_id={task} AND assignee_user_id={other} AND assignment_status='COMPLETED'",CancellationToken.None);}
        return new{assignmentExceptionBrowser=checks,sqlUnchanged=true};
    }
}
