using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkDeliveryProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,long cleaner,long branch,long rework,CancellationToken ct)
    {
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="cleaning_reworks"&&a.EntityId==rework.ToString(),ct);
        var type=typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
        async Task<string> Send()
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            return await (Task<string>)type.GetMethod("DeliverAsync")!.Invoke(Activator.CreateInstance(type,fresh,TimeProvider.System),[audit.AuditId,-1L,ct])!;
        }
        async Task<int> Pending()
        {
            var task=(Task)type.GetMethod("ReadPendingAsync")!.Invoke(Activator.CreateInstance(type,db,TimeProvider.System),[100,0L,ct])!;await task;
            return ((Array)task.GetType().GetProperty("Result")!.GetValue(task)!).Cast<object>().Count(h=>(long)h.GetType().GetProperty("RecipientUserId")!.GetValue(h)! == -1);
        }
        if(await Pending()!=1)throw new Exception("Rework durable event discovery missing");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients WITH NOCHECK ADD CONSTRAINT CK_DEV3_ReworkFault CHECK(user_id<0)",ct);
        try {await Send();throw new Exception("Fault did not reject recipient");}
        catch(DbUpdateException e) when(e.InnerException is Microsoft.Data.SqlClient.SqlException sql&&sql.Number==547){}
        finally {await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_ReworkFault",ct);}
        if(await db.Notifications.AnyAsync(n=>n.SourceType=="CLEANING_REWORK",ct))throw new Exception("Partial rework delivery committed");
        foreach(var denied in new[]{"branch","account","role"})
        {
            if(denied=="branch")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={cleaner}",ct);
            if(denied=="account")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={cleaner}",ct);
            if(denied=="role")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={cleaner}",ct);
            try {if(await Send()!="RECIPIENT_READ_DENIED")throw new Exception("Revoked rework reader received: "+denied);}
            finally
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={cleaner}; UPDATE dbo.users SET account_status='ACTIVE' WHERE user_id={cleaner}; UPDATE dbo.user_roles SET is_active=1 WHERE user_id={cleaner}",ct);
            }
        }
        if(await Pending()!=1)throw new Exception("Denied event lost retry");
        await Task.WhenAll(Send(),Send());await Send();
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="CLEANING_REWORK"&&n.SourceId==rework,ct);
        if(await db.NotificationRecipients.CountAsync(r=>r.NotificationId==notice.NotificationId,ct)!=1
            ||!await db.NotificationRecipients.AnyAsync(r=>r.NotificationId==notice.NotificationId&&r.UserId==cleaner&&r.DeliveryStatus=="DELIVERED",ct)
            ||await Pending()!=0)throw new Exception("Rework delivery duplicate or missing receipt");
    }
}
