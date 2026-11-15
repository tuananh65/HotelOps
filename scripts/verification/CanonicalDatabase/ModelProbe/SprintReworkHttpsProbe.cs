using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkHttpsProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,long branch,long rework,string username,string password,string readiness,CancellationToken ct,bool submission=false)
    {
        if(await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
              AND DATABASE_PRINCIPAL_ID('DEV3_142A_ReworkHttps') IS NULL AND OBJECT_ID('dbo.fs016_rework_source_proofs') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)throw new Exception("Disposable HTTPS identity required");
        var rolePassword=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))+"aA1!";
        async Task Install()=>await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @password nvarchar(128)={rolePassword};
            DECLARE @ddl nvarchar(max)=N'CREATE APPLICATION ROLE DEV3_142A_ReworkHttps WITH PASSWORD='''+REPLACE(@password,'''','''''')+N''', DEFAULT_SCHEMA=dbo';
            EXEC sys.sp_executesql @ddl;
            GRANT SELECT ON SCHEMA::dbo TO DEV3_142A_ReworkHttps;
            GRANT VIEW DEFINITION TO DEV3_142A_ReworkHttps;
            GRANT UPDATE ON dbo.users TO DEV3_142A_ReworkHttps;
            GRANT INSERT,UPDATE ON dbo.user_sessions TO DEV3_142A_ReworkHttps;
            GRANT UPDATE ON dbo.cleaning_reworks TO DEV3_142A_ReworkHttps;
            GRANT INSERT ON dbo.audit_logs TO DEV3_142A_ReworkHttps;
            GRANT INSERT ON dbo.work_intervals TO DEV3_142A_ReworkHttps;
            DENY INSERT,UPDATE,DELETE ON dbo.fs016_rework_source_proofs TO DEV3_142A_ReworkHttps;
            """,ct);
        await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {await Install();await rehearsal.RollbackAsync(ct);}
        await Install();
        Process? host=null;
        try
        {
            if(submission)await db.Database.ExecuteSqlRawAsync("""
                ALTER ROLE hotelops_rework_submission_writer ADD MEMBER DEV3_142A_ReworkHttps;
                GRANT UPDATE ON dbo.work_intervals TO DEV3_142A_ReworkHttps;
                GRANT UPDATE ON dbo.cleaning_rework_defects TO DEV3_142A_ReworkHttps;
                GRANT INSERT ON dbo.attachments TO DEV3_142A_ReworkHttps;
                GRANT INSERT ON dbo.fs016_rework_evidence TO DEV3_142A_ReworkHttps;
                DENY INSERT,UPDATE,DELETE ON dbo.notification_operation_reservations TO DEV3_142A_ReworkHttps;
                """,ct);
            var department=await db.Users.Where(u=>u.UserId==actor).Select(u=>u.DepartmentId!.Value).SingleAsync(ct);
            db.Workdays.Add(new Workday{UserId=actor,BranchId=branch,DepartmentId=department,WorkDate=DateOnly.FromDateTime(DateTime.UtcNow),
                IsUnscheduled=true,UnscheduledReason="Synthetic HTTPS receive fixture",Status="WORKING",StartedAt=DateTime.UtcNow.AddMinutes(-1)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add(Path.GetFullPath("src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
            start.ArgumentList.Add("--urls");start.ArgumentList.Add("https://localhost:7143");
            start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
            start.Environment["ConnectionStrings__HotelOpsDb"]=db.Database.GetConnectionString()+";Pooling=false;Enlist=false";
            start.Environment["CanonicalFrontend__Root"]=Path.GetFullPath("frontend/dist");
            start.Environment["Evidence__PrivateRoot"]=SprintReworkFixture.EvidenceRoot;
            start.Environment["InspectionNotifications__Enabled"]="false";
            start.Environment["Logging__LogLevel__Default"]="Warning";
            start.Environment["IsolatedSqlApplicationRole__Name"]="DEV3_142A_ReworkHttps";
            start.Environment["IsolatedSqlApplicationRole__Password"]=rolePassword;
            start.Environment["IsolatedSqlApplicationRole__Server"]="meita";
            start.Environment["IsolatedSqlApplicationRole__DatabaseGuid"]=(await db.Database.SqlQueryRaw<Guid>("SELECT database_guid AS Value FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync(ct)).ToString("D");
            start.Environment["IsolatedSqlApplicationRole__Ownership"]="cc78c13ffc124efcad67b3d804baa84c";
            host=Process.Start(start)??throw new Exception("Restricted host unavailable");
            var output=host.StandardOutput.ReadToEndAsync(ct);var errors=host.StandardError.ReadToEndAsync(ct);
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};var ready=false;
            for(var attempt=0;attempt<60&&!host.HasExited;attempt++)
            {
                try{using var response=await http.GetAsync("https://localhost:7143/",ct);if(response.IsSuccessStatusCode){ready=true;break;}}
                catch(HttpRequestException){}
                catch(OperationCanceledException) when(!ct.IsCancellationRequested){}
                await Task.Delay(250,ct);
            }
            if(!ready)throw new Exception("Restricted HTTPS host not ready");
            var browser=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            browser.ArgumentList.Add("frontend/scripts/sprintReworkReceiveBrowser.mjs");
            using(var process=Process.Start(browser)??throw new Exception("Receive browser unavailable"))
            {
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,rework,readiness,submission}).AsMemory(),ct);process.StandardInput.Close();
                    var browserOutput=process.StandardOutput.ReadToEndAsync(ct);var browserErrors=process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await browserOutput);await browserErrors;
                    if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Restricted receive browser: "+result.GetRawText());
                }
                finally{if(!process.HasExited)process.Kill(true);}
            }
            if(await db.WorkIntervals.CountAsync(w=>w.UserId==actor&&w.WorkItemKind=="REWORK"&&w.WorkItemId==rework&&w.ClosedAt==null,ct)!=(submission?0:1)
                ||await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="cleaning_reworks"&&a.EntityId==rework.ToString(),ct)!=(submission?3:2)
                ||await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync(ct)!=(submission?"PENDING_REINSPECTION":"IN_PROGRESS"))
                throw new Exception("HTTPS receive did not persist one interval and two task audits");
            if(submission)await SprintReworkSubmissionDeliveryProbe.VerifyAsync(db,actor,branch,rework,ct);
            host.Kill(true);await host.WaitForExitAsync(ct);await output;await errors;
        }
        finally
        {
            if(host!=null){if(!host.HasExited){host.Kill(true);await host.WaitForExitAsync(CancellationToken.None);}host.Dispose();}
            if(submission)await db.Database.ExecuteSqlRawAsync("ALTER ROLE hotelops_rework_submission_writer DROP MEMBER DEV3_142A_ReworkHttps",CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE dbo.work_intervals WHERE user_id={actor} AND work_item_kind='REWORK' AND work_item_id={rework};
                DELETE dbo.workdays WHERE user_id={actor};
                DELETE dbo.audit_logs WHERE actor_user_id={actor} AND entity_type='cleaning_reworks' AND entity_id=CONVERT(nvarchar(50),{rework});
                UPDATE dbo.cleaning_reworks SET status='PENDING',workflow_status='PENDING_ACCEPTANCE',note=NULL WHERE rework_id={rework};
                DROP APPLICATION ROLE DEV3_142A_ReworkHttps;
                """,CancellationToken.None);
            db.ChangeTracker.Clear();
        }
    }
}
