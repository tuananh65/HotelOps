using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintInitialInspectionHttpsProbe
{
    internal static async Task<object> VerifyAsync(HotelOpsDbContext db,long actor,long branch,long task,string password,string rolePassword,CancellationToken ct)
    {
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(Path.GetFullPath("src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
        start.ArgumentList.Add("--urls");start.ArgumentList.Add("https://localhost:7143");
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
        start.Environment["ConnectionStrings__HotelOpsDb"]=SprintInitialInspectionProbe.Connection+";Pooling=false;Enlist=false";
        start.Environment["CanonicalFrontend__Root"]=Path.GetFullPath("frontend/dist");
        start.Environment["Evidence__PrivateRoot"]=SprintReworkFixture.EvidenceRoot;
        start.Environment["InspectionNotifications__Enabled"]="false";
        start.Environment["Logging__LogLevel__Default"]="Warning";
        start.Environment["IsolatedSqlApplicationRole__Name"]=SprintInitialInspectionProbe.Role;
        start.Environment["IsolatedSqlApplicationRole__Password"]=rolePassword;
        start.Environment["IsolatedSqlApplicationRole__Server"]="meita";
        start.Environment["IsolatedSqlApplicationRole__DatabaseGuid"]=(await db.Database.SqlQueryRaw<Guid>("SELECT database_guid AS Value FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync(ct)).ToString("D");
        start.Environment["IsolatedSqlApplicationRole__Ownership"]="cc78c13ffc124efcad67b3d804baa84c";
        using var host=Process.Start(start)??throw new Exception("Initial assignment HTTPS host unavailable");
        var output=host.StandardOutput.ReadToEndAsync(ct);var errors=host.StandardError.ReadToEndAsync(ct);
        try
        {
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};var ready=false;
            for(var attempt=0;attempt<60&&!host.HasExited;attempt++)
            {
                try{using var response=await http.GetAsync("https://localhost:7143/",ct);if(response.IsSuccessStatusCode){ready=true;break;}}
                catch(HttpRequestException){}catch(OperationCanceledException)when(!ct.IsCancellationRequested){}
                await Task.Delay(250,ct);
            }
            if(!ready)throw new Exception("Initial assignment HTTPS host not ready");
            async Task<JsonElement> Browser(object fixture,bool reassignment=false)
            {
                var browser=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                browser.ArgumentList.Add(reassignment ? "frontend/scripts/sprintInspectionReassignmentBrowser.mjs" : "frontend/scripts/sprintInitialInspectionBrowser.mjs");
                using var process=Process.Start(browser)??throw new Exception("Browser unavailable");
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(fixture).AsMemory(),ct);process.StandardInput.Close();
                    var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);var json=JsonSerializer.Deserialize<JsonElement>(await stdout);await stderr;
                    if(process.ExitCode!=0||!json.GetProperty("success").GetBoolean())throw new Exception("Initial assignment browser: "+json.GetRawText());
                    return json;
                }
                finally{if(!process.HasExited)process.Kill(true);}
            }
            var room=await db.CleaningTasks.Where(t=>t.CleaningTaskId==task).Select(t=>t.RoomId).SingleAsync(ct);
            var assigned=await Browser(new{username="D3-EVIDENCE",password,actor,branch,task,room});
            var inspection=assigned.GetProperty("inspection").GetInt64();
            var other=await db.Users.Where(u=>u.Username=="D3-INITIAL-OTHER").Select(u=>u.UserId).SingleAsync(ct);
            var assignedHandoff=await Browser(new{username="D3-EVIDENCE",password,actor,other,branch,task,inspection,state="ASSIGNED",returnToActor=true},true);
            var notice=await SprintInitialInspectionNoticeProbe.VerifyAsync(db,actor,branch,inspection,ct);
            var executed=await Browser(new{username="D3-EVIDENCE",password,actor,branch,task,inspection,notice});
            var pendingHandoff=await Browser(new{username="D3-EVIDENCE",password,actor,other,branch,task,inspection,state="PENDING_CONCLUSION",returnToActor=false},true);
            var recipientSession=await Browser(new{username="D3-INITIAL-OTHER",password,branch,task,inspection,recipientRead=true,state="PENDING_CONCLUSION"},true);
            var reassignedNotice=await SprintInspectionReassignmentNoticeProbe.VerifyAsync(db,other,branch,inspection,ct);
            var received=await Browser(new{username="D3-INITIAL-OTHER",password,branch,task,inspection,notice=reassignedNotice,state="PENDING_CONCLUSION"},true);
            if(await db.RoomInspections.CountAsync(i=>i.CleaningTaskId==task,ct)!=1
                ||await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct)!="PENDING_CONCLUSION"
                ||await db.InspectionResults.CountAsync(r=>r.InspectionId==inspection&&r.Outcome=="PASS",ct)!=1
                ||await db.AuditLogs.CountAsync(a=>a.EntityType=="room_inspections"&&a.EntityId==inspection.ToString(),ct)!=3)
                throw new Exception("Browser execution persistence mismatch");
            return new{assigned,assignedHandoff,notification=notice,executed,pendingHandoff,recipientSession,reassignedNotice,received,sqlAssignedThenPendingConclusion=true,assignmentAudit=1,executionAudits=3};
        }
        finally{if(!host.HasExited)host.Kill(true);await host.WaitForExitAsync(CancellationToken.None);await output;await errors;}
    }
}
