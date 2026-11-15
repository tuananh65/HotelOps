using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintReinspectionNoticeProbe
{
    // Parent owns the identity-verified disposable fixture and protected binding cleanup.
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,long branch,long inspection,string password,CancellationToken ct,bool initial)
    {
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="REINSPECTION"&&a.EntityId==inspection.ToString()&&a.Action=="CREATE",ct);
        var type=typeof(NotificationCenterService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
        async Task<string> Send()
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            return await(Task<string>)type.GetMethod("DeliverAsync")!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[audit.AuditId,-4L,ct])!;
        }
        async Task<bool> Pending()
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var task=(Task)type.GetMethod("ReadPendingAsync")!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[100,0L,ct])!;
            await task;
            var rows=(System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
            return rows.Cast<object>().Any(r=>(long)r.GetType().GetProperty("AuditId")!.GetValue(r)! == audit.AuditId
                &&(long)r.GetType().GetProperty("RecipientUserId")!.GetValue(r)! == -4L);
        }
        async Task Open(long notice,bool denied=false)
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var sid=await fresh.UserSessions.AsNoTracking().Where(s=>s.UserId==actor&&s.RevokedAt==null).MaxAsync(s=>s.SessionId,ct);
            try
            {
                var source=await new NotificationCenterService(fresh,new Identity(new(actor,sid,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions())).ResolveSourceAsync(notice,ct);
                if(denied||source.Path!=$"/reinspections/{branch}/{inspection}/execution")throw new Exception("Assignment source authorization mismatch");
            }
            catch(InsightException e) when(denied&&e.Status==404) { }
        }
        if(initial)
        {
            if(!await Pending())throw new Exception("Committed assignment intent not discovered");
            foreach(var invalid in new[]{"[]",audit.NewValues!.Replace("REINSPECTION_ASSIGNED","OTHER")})
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={invalid} WHERE audit_id={audit.AuditId}",ct);
                try{if(await Send()!="SOURCE_DENIED")throw new Exception("Forged assignment source delivered");}
                finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}",CancellationToken.None);}
            }
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=0 WHERE issuer='FE05.UC041' AND version=1",ct);
            try{if(await Send()!="SOURCE_DENIED")throw new Exception("Disabled writer delivered");}
            finally{await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=1 WHERE issuer='FE05.UC041' AND version=1",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}",ct);
            try{if(await Send()!="RECIPIENT_READ_DENIED")throw new Exception("Revoked assignment recipient delivered");}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}",CancellationToken.None);}
            var maximum=await db.NotificationRecipients.MaxAsync(r=>(long?)r.RecipientId,ct)??0;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @maximum bigint={maximum};
                DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.notification_recipients ADD CONSTRAINT CK_DEV3_AssignmentNoticeFault CHECK(recipient_id<='+CONVERT(nvarchar(20),@maximum)+N')';
                EXEC sys.sp_executesql @ddl;
                """,ct);
            try
            {
                try{await Send();throw new Exception("Expected delivery SQL fault");}
                catch(DbUpdateException) { }
            }
            finally{await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_AssignmentNoticeFault",CancellationToken.None);}
            if(await db.Notifications.AnyAsync(n=>n.NotificationType=="REINSPECTION_ASSIGNED"&&n.SourceId==inspection,ct))throw new Exception("Delivery fault leaked notification");
            var results=await Task.WhenAll(Send(),Send());
            if(results.Any(r=>r!="DELIVERED")||await Send()!="DELIVERED")throw new Exception("Assignment delivery retry failed");
        }
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.NotificationType=="REINSPECTION_ASSIGNED"&&n.SourceType=="INSPECTION"&&n.SourceId==inspection,ct);
        var recipients=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice.NotificationId).ToArrayAsync(ct);
        if(recipients.Length!=1||recipients[0].UserId!=actor||recipients[0].DeliveryStatus!="DELIVERED"||await Pending())throw new Exception("Assignment receipt mismatch or rediscovered");
        await Open(notice.NotificationId);
        if(initial)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}",ct);
            try{await Open(notice.NotificationId,true);if(await Send()!="RECIPIENT_READ_DENIED")throw new Exception("Lost supervisor role retained delivery");}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={actor}",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET delivery_status='PENDING' WHERE notification_id={notice.NotificationId}",ct);
            try{await Open(notice.NotificationId,true);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET delivery_status='DELIVERED' WHERE notification_id={notice.NotificationId}",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values='[]' WHERE audit_id={audit.AuditId}",ct);
            try{await Open(notice.NotificationId,true);}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}",CancellationToken.None);}
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET action_url='https://untrusted.invalid/' WHERE notification_id={notice.NotificationId}",ct);
        try
        {
            await Open(notice.NotificationId);
            var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
            var state=await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct);
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintReinspectionNoticeBrowser.mjs");
            using var browser=Process.Start(start)??throw new Exception("Assignment notice browser unavailable");
            try
            {
                await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=user.Username,password,branch,inspection,state,notification=notice.NotificationId}).AsMemory(),ct);browser.StandardInput.Close();
                var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
                await browser.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);await errors;
                if(browser.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Assignment notice browser: "+result.GetRawText());
            }
            finally{if(!browser.HasExited)browser.Kill(true);}
        }
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET action_url={notice.ActionUrl} WHERE notification_id={notice.NotificationId}",CancellationToken.None);}
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
