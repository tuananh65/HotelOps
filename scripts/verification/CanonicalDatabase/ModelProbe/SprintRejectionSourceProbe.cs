using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintRejectionSourceProbe
{
    internal static async Task VerifyAfterLifecycleAsync(HotelOpsDbContext db,long branch,long rework,long cleaner,CancellationToken ct)
    {
        var source=await db.CleaningReworks.AsNoTracking().Where(w=>w.ReworkId==rework)
            .Select(w=>new{w.InspectionId,w.Inspection.InspectorUserId}).SingleAsync(ct);
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
            JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.workflow_status IN('PASSED','FAILED')
            JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.workflow_status IN('COMPLETED','FAILED')
            WHERE b.rework_id={rework} AND b.branch_id={branch}
            """).SingleAsync(ct)!=1)throw new Exception("Terminal cycle required before historical source check");
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.NotificationType=="INSPECTION_FAILED"&&n.SourceId==source.InspectionId,ct);
        foreach(var user in new[]{source.InspectorUserId,cleaner})
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var sid=await fresh.UserSessions.AsNoTracking().Where(s=>s.UserId==user&&s.RevokedAt==null).MaxAsync(s=>s.SessionId,ct);
            var resolved=await new NotificationCenterService(fresh,new Identity(new(user,sid,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions())).ResolveSourceAsync(notice.NotificationId,ct);
            if(resolved.Path!=$"/inspection-history/{source.InspectionId}?branchId={branch}")throw new Exception("Later lifecycle broke original Reject source");
        }
    }
    // Called only inside the GUID/run-verified disposable rework fixture, after real Reject/delivery.
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,long cleaner,long branch,long inspection,string password,CancellationToken ct)
    {
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.NotificationType=="INSPECTION_FAILED"&&n.SourceId==inspection,ct);
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString()&&a.Action=="UPDATE",ct);
        var housekeeper=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==cleaner,ct);
        var hash=new PasswordHasher<User>().HashPassword(housekeeper,password);
        async Task Browser(long user,bool denied=false)
        {
            var username=await db.Users.Where(u=>u.UserId==user).Select(u=>u.Username).SingleAsync(ct);
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintRejectionSourceBrowser.mjs");
            using var process=Process.Start(start)??throw new Exception("Source browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,inspection,notice=notice.NotificationId,denied}).AsMemory(),ct);
                process.StandardInput.Close();
                var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);await errors;
                if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Rejected source browser: "+result.GetRawText());
            }
            finally{if(!process.HasExited)process.Kill(true);}
        }
        async Task Open(long user,bool denied=false)
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var sid=await fresh.UserSessions.AsNoTracking().Where(s=>s.UserId==user&&s.RevokedAt==null).MaxAsync(s=>s.SessionId,ct);
            try
            {
                var result=await new NotificationCenterService(fresh,new Identity(new(user,sid,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions())).ResolveSourceAsync(notice.NotificationId,ct);
                if(denied||result.Path!=$"/inspection-history/{inspection}?branchId={branch}")throw new Exception("Source identity/denial mismatch");
            }
            catch(InsightException error) when(denied&&error.Status==404) { }
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={cleaner}",ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET action_url='https://untrusted.invalid/' WHERE notification_id={notice.NotificationId}",ct);
            try{await Browser(actor);await Browser(cleaner);await Open(actor);await Open(cleaner);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET action_url={notice.ActionUrl} WHERE notification_id={notice.NotificationId}",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}",ct);
            try{await Open(actor,true);await Browser(actor,true);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}",ct);
            try{await Open(actor,true);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={actor}",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET delivery_status='PENDING' WHERE notification_id={notice.NotificationId} AND user_id={cleaner}",ct);
            try{await Open(cleaner,true);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET delivery_status='DELIVERED' WHERE notification_id={notice.NotificationId} AND user_id={cleaner}",CancellationToken.None);}
            foreach(var invalid in new[]{"[]","{\"notification\":null}",audit.NewValues!.Replace("\"kind\":\"INSPECTION_FAILED\"","\"kind\":\"OTHER\"")})
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={invalid} WHERE audit_id={audit.AuditId}",ct);
                try{await Open(actor,true);await Open(cleaner,true);}
                finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}",CancellationToken.None);}
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET created_by={cleaner} WHERE notification_id={notice.NotificationId}",ct);
            try{await Open(actor,true);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET created_by={notice.CreatedBy} WHERE notification_id={notice.NotificationId}",CancellationToken.None);}
            await Open(actor);await Open(cleaner);
            // Both operations keep their source snapshot, without a read upgrading the
            // inspection lock after it already holds the delivered notification row.
            async Task RetryDelivery()
            {
                await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                var type=typeof(NotificationCenterService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
                var result=await (Task<string>)type.GetMethod("DeliverAsync")!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[audit.AuditId,0L,ct])!;
                if(result!="DELIVERED")throw new Exception("Source read raced delivery incorrectly");
            }
            for(var attempt=0;attempt<3;attempt++)await Task.WhenAll(Open(actor),Open(cleaner),RetryDelivery());
        }
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={housekeeper.PasswordHash} WHERE user_id={cleaner}",CancellationToken.None);}
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
