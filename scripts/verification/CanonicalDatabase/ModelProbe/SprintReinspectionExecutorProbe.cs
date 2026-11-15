using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintReinspectionExecutorProbe
{
    // Parent has verified the exact disposable identity and owns all source/evidence cleanup.
    internal static async Task VerifyAsync(HotelOpsDbContext db,SessionReference ticket,long sid,long branch,long rework,CancellationToken ct,bool browser=false,long? nextRework=null)
    {
        var inspection=await db.Database.SqlQuery<long>($"SELECT inspection_id AS Value FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id={rework} AND submission_actor={ticket.UserId}").SingleAsync(ct);
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="REINSPECTION"&&a.EntityId==inspection.ToString()&&a.Action=="UPDATE",ct);
        async Task<RoomTypeBatchResult> Read(long? file=null,long? alternateBranch=null)
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            await context.Database.OpenConnectionAsync(ct);await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_Reinspector'",ct);
            try
            {
                var identity=new Identity(new(ticket.UserId,sid,false));
                var service=new RoomTypeCreationService(context,identity,new PermissionService(identity,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                    new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
                return await service.ReadAssignedReworkAsync(ticket,sid,alternateBranch??branch,rework,SprintReworkFixture.EvidenceRoot,ct,file);
            }
            finally{await context.Database.ExecuteSqlRawAsync("REVERT",CancellationToken.None);}
        }
        async Task<string> Send()
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var type=typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
            return await (Task<string>)type.GetMethod("DeliverAsync")!.Invoke(Activator.CreateInstance(type,context,TimeProvider.System),[audit.AuditId,-3L,ct])!;
        }
        async Task<NotificationSource> Open(long notice)
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            return await new NotificationCenterService(context,new Identity(new(ticket.UserId,sid,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions())).ResolveSourceAsync(notice,ct);
        }
        async Task<bool> Pending()
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var type=typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
            var task=(Task)type.GetMethod("ReadPendingAsync")!.Invoke(Activator.CreateInstance(type,context,TimeProvider.System),[100,0L,ct])!;
            await task;
            var rows=(System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
            return rows.Cast<object>().Any(row=>(long)row.GetType().GetProperty("AuditId")!.GetValue(row)! == audit.AuditId
                &&(long)row.GetType().GetProperty("RecipientUserId")!.GetValue(row)! == -3L);
        }
        var response=await Read();
        if(response.Value is not ReworkExecutionDto {Capability:"READ_ONLY",Conclusion:not null} ended
            ||ended.Conclusion.ReinspectionId!=inspection||ended.State is not("COMPLETED" or "FAILED"))
            throw new Exception("Executor terminal acknowledgement denied: "+response.Status);
        var photos=ended.Defects.SelectMany(d=>d.InspectionAttachmentIds.Concat(d.RemediationAttachmentIds)).Distinct().ToArray();
        if(photos.Length==0)throw new Exception("Executor terminal evidence missing");
        foreach(var file in photos)if((await Read(file)).Value is not HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto)throw new Exception("Executor terminal evidence denied");
        if((await Read(long.MaxValue)).Status==RoomTypeBatchStatus.Success||(await Read(alternateBranch:branch+100000)).Status==RoomTypeBatchStatus.Success)
            throw new Exception("Executor terminal foreign source admitted");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values=N'{{}}' WHERE audit_id={audit.AuditId}",ct);
        try{if((await Read()).Status==RoomTypeBatchStatus.Success||await Send()!="SOURCE_DENIED")throw new Exception("Tampered terminal audit admitted");}
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}",CancellationToken.None);}
        long? notice=null;
        if(ended.State=="COMPLETED")
        {
            if(!await Pending())throw new Exception("Passed conclusion missing from durable discovery");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients WITH NOCHECK ADD CONSTRAINT CK_DEV3_ReinspectionDeliveryFault CHECK(user_id<0)",ct);
            try{await Send();throw new Exception("Reinspection delivery fault did not fire");}
            catch(DbUpdateException e) when(e.InnerException is Microsoft.Data.SqlClient.SqlException {Number:547}){}
            finally{await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_ReinspectionDeliveryFault",CancellationToken.None);}
            if(await db.Notifications.AnyAsync(n=>n.NotificationType=="REINSPECTION_PASSED"&&n.SourceId==rework,ct))throw new Exception("Delivery rollback leaked notice");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={ticket.UserId} AND branch_id={branch}",ct);
        try
        {
            if((await Read()).Status==RoomTypeBatchStatus.Success||(await Read(photos[0])).Status==RoomTypeBatchStatus.Success)throw new Exception("Revoked executor read terminal source");
            if(await Send()!=(ended.State=="COMPLETED"?"RECIPIENT_READ_DENIED":"SOURCE_DENIED"))throw new Exception("Revoked/Failed event delivered");
        }
        finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={ticket.UserId} AND branch_id={branch}",CancellationToken.None);}
        if(ended.State=="COMPLETED")
        {
            var sends=await Task.WhenAll(Send(),Send());
            if(sends.Any(s=>s!="DELIVERED")||await Send()!="DELIVERED")throw new Exception("Concurrent delivery retry failed");
            var notices=await db.Notifications.AsNoTracking().Where(n=>n.NotificationType=="REINSPECTION_PASSED"&&n.SourceType=="CLEANING_REWORK"&&n.SourceId==rework).ToArrayAsync(ct);
            if(notices.Length!=1)throw new Exception("Duplicate conclusion notice");notice=notices[0].NotificationId;
            var recipients=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==notice).ToArrayAsync(ct);
            if(recipients.Length!=1||recipients[0].UserId!=ticket.UserId)throw new Exception("Wrong conclusion recipients");
            if(await Pending())throw new Exception("Delivered conclusion still discovered as pending");
            if((await Open(notice.Value)).Path!=$"/cleaning/reworks/{rework}?branchId={branch}")throw new Exception("Wrong conclusion source");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={ticket.UserId} AND branch_id={branch}",ct);
            try{await Open(notice.Value);throw new Exception("Revoked executor opened source");}
            catch(InsightException e) when(e.Status==404){}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={ticket.UserId} AND branch_id={branch}",CancellationToken.None);}
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values=N'{{}}' WHERE audit_id={audit.AuditId}",ct);
            try{await Open(notice.Value);throw new Exception("Tampered source opened");}
            catch(InsightException e) when(e.Status==404){}
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}",CancellationToken.None);}
        }
        else if(await Send()!="SOURCE_DENIED")throw new Exception("Failed incorrectly delivered as Passed");
        if(browser)
        {
            var nextNotice=nextRework is {} next?await db.Notifications.AsNoTracking().Where(n=>n.NotificationType=="REWORK_ASSIGNED"&&n.SourceType=="CLEANING_REWORK"&&n.SourceId==next)
                .Select(n=>(long?)n.NotificationId).SingleAsync(ct):null;
            var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==ticket.UserId,ct);
            var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(32))+"Aa1!";
            var hash=new PasswordHasher<User>().HashPassword(user,password);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={user.UserId}",ct);
            try
            {
                var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                start.ArgumentList.Add("frontend/scripts/sprintReinspectionExecutorBrowser.mjs");
                using var process=Process.Start(start)??throw new Exception("Executor browser unavailable");
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username=user.Username,password,branch,rework,inspection,notice,nextRework,nextNotice,state=ended.State,readiness=ended.Conclusion.EffectiveReadiness}).AsMemory(),ct);
                    process.StandardInput.Close();var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);await errors;
                    if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Executor browser: "+result.GetRawText());
                }
                finally{if(!process.HasExited)process.Kill(true);}
            }
            finally{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={user.PasswordHash} WHERE user_id={user.UserId}",CancellationToken.None);}
        }
        if((await Read()).ETag!=response.ETag)throw new Exception("Terminal acknowledgement changed rework");
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
