using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionHttpsProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db, long actor, long branch, long rework,
        HotelOps.Api.Services.Authentication.SessionReference executorTicket,long executorSid,CancellationToken ct)
    {
        if (await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
              AND DATABASE_PRINCIPAL_ID('DEV3_142A_ReinspectionHttps') IS NULL THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new Exception("Disposable reinspection HTTPS identity required");
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == actor, ct);
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
        var rolePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
        async Task Install() => await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @password nvarchar(128)={rolePassword};
            DECLARE @ddl nvarchar(max)=N'CREATE APPLICATION ROLE DEV3_142A_ReinspectionHttps WITH PASSWORD='''+REPLACE(@password,'''','''''')+N''', DEFAULT_SCHEMA=dbo';
            EXEC sys.sp_executesql @ddl;
            GRANT SELECT ON SCHEMA::dbo TO DEV3_142A_ReinspectionHttps;
            GRANT VIEW DEFINITION TO DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.users TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT,UPDATE ON dbo.user_sessions TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT,UPDATE ON dbo.room_inspections TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.audit_logs TO DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.notification_recipients TO DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.fe05_reinspection_defect_results_v1 TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.fe05_reinspection_defect_attachments_v1 TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.inspection_results TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT,UPDATE ON dbo.inspection_defects TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.inspection_defect_attachments TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.attachments TO DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.cleaning_reworks TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.cleaning_reworks TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.cleaning_rework_defects TO DEV3_142A_ReinspectionHttps;
            ALTER ROLE hotelops_rework_writer ADD MEMBER DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.cleaning_tasks TO DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.rooms TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.room_status_history TO DEV3_142A_ReinspectionHttps;
            DENY INSERT,UPDATE,DELETE ON dbo.fs016_rework_source_proofs TO DEV3_142A_ReinspectionHttps;
            DENY INSERT,UPDATE,DELETE ON dbo.notification_operation_reservations TO DEV3_142A_ReinspectionHttps;
            DENY INSERT,UPDATE,DELETE ON dbo.fe05_reinspection_bindings_v1 TO DEV3_142A_ReinspectionHttps;
            ALTER ROLE hotelops_reinspection_writer ADD MEMBER DEV3_142A_ReinspectionHttps;
            """, ct);
        await using (var rehearsal = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        { await Install(); await rehearsal.RollbackAsync(ct); }
        await using (var setup = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        { await Install(); await setup.CommitAsync(ct); }
        Process? host = null;
        try
        {
            var hash = new PasswordHasher<User>().HashPassword(user, password);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}", ct);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.GetFullPath("src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
            start.ArgumentList.Add("--urls"); start.ArgumentList.Add("https://localhost:7143");
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["ConnectionStrings__HotelOpsDb"] = db.Database.GetConnectionString() + ";Pooling=false;Enlist=false";
            start.Environment["CanonicalFrontend__Root"] = Path.GetFullPath("frontend/dist");
            start.Environment["Evidence__PrivateRoot"] = SprintReworkFixture.EvidenceRoot;
            start.Environment["InspectionNotifications__Enabled"] = "false";
            start.Environment["Logging__LogLevel__Default"] = "Warning";
            start.Environment["IsolatedSqlApplicationRole__Name"] = "DEV3_142A_ReinspectionHttps";
            start.Environment["IsolatedSqlApplicationRole__Password"] = rolePassword;
            start.Environment["IsolatedSqlApplicationRole__Server"] = "meita";
            start.Environment["IsolatedSqlApplicationRole__DatabaseGuid"] = (await db.Database.SqlQueryRaw<Guid>("SELECT database_guid AS Value FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync(ct)).ToString("D");
            start.Environment["IsolatedSqlApplicationRole__Ownership"] = "cc78c13ffc124efcad67b3d804baa84c";
            host = Process.Start(start) ?? throw new Exception("Reinspection HTTPS host unavailable");
            var output = host.StandardOutput.ReadToEndAsync(ct); var errors = host.StandardError.ReadToEndAsync(ct);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) }; var ready = false;
            for (var attempt = 0; attempt < 60 && !host.HasExited; attempt++)
            {
                try { using var response = await http.GetAsync("https://localhost:7143/", ct); if (response.IsSuccessStatusCode) { ready = true; break; } }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                await Task.Delay(250, ct);
            }
            if (!ready) throw new Exception("Reinspection HTTPS host not ready");
            var browser = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            browser.ArgumentList.Add("frontend/scripts/sprintReinspectionAssignmentBrowser.mjs");
            using var process = Process.Start(browser) ?? throw new Exception("Reinspection browser unavailable");
            try
            {
                var conclude = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE") == "1";
                var outcome = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_OUTCOME") ?? "PASS";
                var nextRound=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_NEXT_ROUND")=="1";
                if (outcome is not ("PASS" or "FAIL")) throw new Exception("Explicit PASS/FAIL browser outcome required");
                if(nextRound&&(!conclude||outcome!="FAIL"))throw new Exception("Next round requires browser Failed conclusion");
                var distinctInspector = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_DISTINCT_INSPECTOR") == "1";
                var retireAfterAssignment = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_RETIRE_AFTER_ASSIGNMENT") == "1";
                var assignmentNotice = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_ASSIGNMENT_NOTICE") == "1";
                var completeNextRound = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND") == "1";
                if (completeNextRound && (!nextRound || !conclude || outcome != "FAIL" || retireAfterAssignment || distinctInspector))
                    throw new Exception("Second-round trial requires Failed/next-round mode with the original published source and inspector");
                if (assignmentNotice && !conclude) throw new Exception("Assignment notice trial requires terminal browser conclusion");
                if (retireAfterAssignment && !conclude) throw new Exception("Retirement trial requires the complete browser conclusion mode");
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username = user.Username, password, actor, branch, rework,
                    conclude = conclude && !retireAfterAssignment && !assignmentNotice, outcome, nextRound = nextRound && !retireAfterAssignment && !assignmentNotice, distinctInspector, executor=executorTicket.UserId }).AsMemory(), ct);
                process.StandardInput.Close();
                var browserOutput = process.StandardOutput.ReadToEndAsync(ct); var browserErrors = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct); var result = JsonSerializer.Deserialize<JsonElement>(await browserOutput); await browserErrors;
                if (process.ExitCode != 0 || !result.GetProperty("success").GetBoolean()) throw new Exception("Reinspection browser: " + result.GetRawText());
                var inspection = result.GetProperty("inspection").GetInt64();
                if (retireAfterAssignment || assignmentNotice)
                {
                    if (!result.GetProperty("canonicalAssignment").GetBoolean() || result.GetProperty("conclusion").ValueKind != JsonValueKind.Null)
                        throw new Exception("Expected real assigned source before retirement");
                    if (assignmentNotice) await SprintReinspectionNoticeProbe.VerifyAsync(db, actor, branch, inspection, password, ct, initial:true);
                    if (retireAfterAssignment) await SprintReinspectionRetirementProbe.RetireAfterAssignmentAsync(db, actor, branch, rework, inspection, ct);
                    using var resumed = Process.Start(browser) ?? throw new Exception("Retired-source browser unavailable");
                    try
                    {
                        await resumed.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username = user.Username, password, actor, branch, rework,
                            conclude, outcome, nextRound, distinctInspector, existingInspection = inspection, executor = executorTicket.UserId }).AsMemory(), ct);
                        resumed.StandardInput.Close();
                        var resumedOutput = resumed.StandardOutput.ReadToEndAsync(ct); var resumedErrors = resumed.StandardError.ReadToEndAsync(ct);
                        await resumed.WaitForExitAsync(ct); result = JsonSerializer.Deserialize<JsonElement>(await resumedOutput); await resumedErrors;
                        if (resumed.ExitCode != 0 || !result.GetProperty("success").GetBoolean()
                            || !result.GetProperty("retiredVersionAssignmentReload").GetBoolean() || result.GetProperty("inspection").GetInt64() != inspection)
                            throw new Exception("Retired-source browser: " + result.GetRawText());
                    }
                    finally { if (!resumed.HasExited) resumed.Kill(true); }
                    if (retireAfterAssignment && await db.Database.SqlQuery<int>($"""
                        SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
                        JOIN dbo.inspection_checklist_versions v ON v.version_id=b.version_id AND v.status='RETIRED'
                        JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.version_id=b.version_id
                        JOIN dbo.room_inspections original ON original.inspection_id=b.original_inspection_id AND original.version_id=b.version_id
                        WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.reinspector_user_id={actor}
                        """).SingleAsync(ct) != 1) throw new Exception("Retired version was changed or remapped");
                }
                if (await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
                    JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id={actor}
                      AND i.workflow_status={(conclude ? outcome == "PASS" ? "PASSED" : "FAILED" : "ASSIGNED")}
                    WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.branch_id={branch}
                      AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='REINSPECTION' AND entity_id=CONVERT(nvarchar(50),b.inspection_id) AND action='CREATE')=1
                    """).SingleAsync(ct) != 1) throw new Exception("Browser assignment persistence/audit mismatch");
                if (conclude && (!result.GetProperty("conclusion").GetProperty("success").GetBoolean()
                    || await db.Database.SqlQuery<int>($"""
                        SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
                        JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.result={outcome} AND i.status='COMPLETED'
                        JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.status='COMPLETED'
                          AND w.workflow_status={(outcome == "PASS" ? "COMPLETED" : "FAILED")} AND w.completed_at=i.inspected_at
                        JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=b.cleaning_task_id
                          AND t.workflow_status={(outcome == "PASS" ? "COMPLETED" : "REWORK_REQUIRED")}
                        WHERE b.inspection_id={inspection} AND b.rework_id={rework}
                          AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='REINSPECTION' AND entity_id=CONVERT(nvarchar(50),b.inspection_id) AND action='UPDATE')=1
                          AND EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_attachments_v1 WHERE inspection_id=b.inspection_id)
                          AND NOT EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=b.inspection_id AND (fixed IS NULL OR recorded_by<>{actor}))
                        """).SingleAsync(ct) != 1)) throw new Exception("Browser conclusion persistence/audit/evidence mismatch");
                long? nextRework=nextRound?result.GetProperty("nextRework").GetInt64():null;
                if(nextRework is {} next)
                {
                    if(next<=0||next==rework||await db.Database.SqlQuery<int>($"""
                        SELECT COUNT(*) AS Value FROM dbo.cleaning_reworks w JOIN dbo.fs016_rework_source_proofs p ON p.rework_id=w.rework_id
                        WHERE w.rework_id={next} AND w.inspection_id={inspection} AND w.assignee_user_id={executorTicket.UserId}
                          AND w.workflow_status='PENDING_ACCEPTANCE' AND w.status='PENDING' AND p.inspection_id={inspection} AND p.branch_id={branch}
                          AND(SELECT COUNT(*) FROM dbo.cleaning_rework_defects d WHERE d.rework_id=w.rework_id)>0
                          AND NOT EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects d JOIN dbo.inspection_defects defect ON defect.defect_id=d.defect_id
                            WHERE d.rework_id=w.rework_id AND defect.inspection_id<>{inspection})
                        """).SingleAsync(ct)!=1)throw new Exception("Next rework protected source/persistence mismatch");
                    var nextAudit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="cleaning_reworks"&&a.EntityId==next.ToString()&&a.Action=="CREATE",ct);
                    var delivery=typeof(HotelOps.Api.Services.HotelStructure.RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
                    for(var attempt=0;attempt<2;attempt++)
                    {
                        await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                        var delivered=await(Task<string>)delivery.GetMethod("DeliverAsync")!.Invoke(Activator.CreateInstance(delivery,fresh,TimeProvider.System),[nextAudit.AuditId,-1L,ct])!;
                        if(delivered!="DELIVERED")throw new Exception("Next rework notification denied: "+delivered);
                    }
                }
                if(conclude)await SprintReinspectionExecutorProbe.VerifyAsync(db,executorTicket,executorSid,branch,rework,ct,browser:true,nextRework:nextRework);
                if(assignmentNotice)await SprintReinspectionNoticeProbe.VerifyAsync(db,actor,branch,inspection,password,ct,initial:false);
                if(completeNextRound)await SprintSecondReworkRoundProbe.VerifyAsync(db,actor,branch,rework,nextRework!.Value,
                    executorTicket,executorSid,password,ct);
            }
            finally { if (!process.HasExited) process.Kill(true); }
            host.Kill(true); await host.WaitForExitAsync(ct); await output; await errors;
        }
        finally
        {
            if (host != null) { if (!host.HasExited) { host.Kill(true); await host.WaitForExitAsync(CancellationToken.None); } host.Dispose(); }
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.users SET password_hash={user.PasswordHash} WHERE user_id={actor};
                ALTER ROLE hotelops_reinspection_writer DROP MEMBER DEV3_142A_ReinspectionHttps;
                ALTER ROLE hotelops_rework_writer DROP MEMBER DEV3_142A_ReinspectionHttps;
                DROP APPLICATION ROLE DEV3_142A_ReinspectionHttps;
                """, CancellationToken.None);
        }
    }
}
