using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using HotelOps.Api.Entities;

internal static class SprintReworkSubmissionDeliveryProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,long branch,long rework,CancellationToken ct)
    {
        var audit=await db.Database.SqlQuery<long>($"""
            SELECT audit_id AS Value FROM dbo.audit_logs WHERE actor_user_id={actor} AND entity_type='cleaning_reworks'
              AND entity_id=CONVERT(nvarchar(50),{rework}) AND JSON_VALUE(new_values,'$.contract')='FE04_REWORK_SUBMISSION_V1'
            """).SingleAsync(ct);
        var supervisors=await db.UserRoles.AsNoTracking().Where(r=>r.Role.RoleCode=="HK_SUPERVISOR"&&r.IsActive&&r.BranchId==branch)
            .Select(r=>r.UserId).Distinct().ToArrayAsync(ct);
        if(supervisors.Length==0)throw new Exception("Synthetic supervisor missing");
        var type=typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
        async Task<string> Send()
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            return await (Task<string>)type.GetMethod("DeliverAsync")!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[audit,-2L,ct])!;
        }
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients WITH NOCHECK ADD CONSTRAINT CK_DEV3_ReworkSubmitDeliveryFault CHECK(user_id<0)",ct);
        try{await Send();throw new Exception("Delivery fault did not fire");}
        catch(DbUpdateException e) when(e.InnerException is Microsoft.Data.SqlClient.SqlException {Number:547}){}
        finally{await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_ReworkSubmitDeliveryFault",ct);}
        if(await db.Notifications.AnyAsync(n=>n.NotificationType=="REWORK_SUBMITTED"&&n.SourceId==rework,ct))throw new Exception("Submission delivery rollback leaked notification");
        foreach(var user in supervisors)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={user} AND branch_id={branch}",ct);
        try{if(await Send()!="RECIPIENT_READ_DENIED")throw new Exception("Revoked supervisor received submission");}
        finally{foreach(var user in supervisors)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={user} AND branch_id={branch}",ct);}
        await Task.WhenAll(Send(),Send());await Send();
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.NotificationType=="REWORK_SUBMITTED"&&n.SourceId==rework,ct);
        var recipients=await db.NotificationRecipients.AsNoTracking().Where(n=>n.NotificationId==notice.NotificationId).Select(n=>n.UserId).ToArrayAsync(ct);
        if(!recipients.Order().SequenceEqual(supervisors.Order()))throw new Exception("Submission supervisor delivery missing/duplicated/unauthorized");
        var supervisor=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==supervisors[0],ct);
        var secret=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
        var hash=new PasswordHasher<User>().HashPassword(supervisor,secret);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={supervisor.UserId}",ct);
        try
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintReworkSubmittedInboxBrowser.mjs");
            using var process=Process.Start(start)??throw new Exception("Supervisor inbox browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=supervisor.Username,password=secret,notification=notice.NotificationId}).AsMemory(),ct);process.StandardInput.Close();
                var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);await errors;
                if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Submitted supervisor inbox: "+result.GetRawText());
            }
            finally{if(!process.HasExited)process.Kill(true);}
        }
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={supervisor.PasswordHash} WHERE user_id={supervisor.UserId}",CancellationToken.None);}
        // Restore only this synthetic occurrence before the independent SQL submission suite.
        await db.NotificationRecipients.Where(n=>n.NotificationId==notice.NotificationId).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n=>n.NotificationId==notice.NotificationId).ExecuteDeleteAsync(ct);
    }
}
