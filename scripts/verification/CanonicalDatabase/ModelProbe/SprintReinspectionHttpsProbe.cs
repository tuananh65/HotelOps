using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionHttpsProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db, long actor, long branch, long rework, CancellationToken ct)
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
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username = user.Username, password, actor, branch, rework }).AsMemory(), ct);
                process.StandardInput.Close();
                var browserOutput = process.StandardOutput.ReadToEndAsync(ct); var browserErrors = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct); var result = JsonSerializer.Deserialize<JsonElement>(await browserOutput); await browserErrors;
                if (process.ExitCode != 0 || !result.GetProperty("success").GetBoolean()) throw new Exception("Reinspection browser: " + result.GetRawText());
                var inspection = result.GetProperty("inspection").GetInt64();
                if (await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
                    JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id={actor} AND i.workflow_status='ASSIGNED'
                    WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.branch_id={branch}
                      AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='REINSPECTION' AND entity_id=CONVERT(nvarchar(50),b.inspection_id) AND action='CREATE')=1
                    """).SingleAsync(ct) != 1) throw new Exception("Browser assignment persistence/audit mismatch");
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
                DROP APPLICATION ROLE DEV3_142A_ReinspectionHttps;
                """, CancellationToken.None);
        }
    }
}
