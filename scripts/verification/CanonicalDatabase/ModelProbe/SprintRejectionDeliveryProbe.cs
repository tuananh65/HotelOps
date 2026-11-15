using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class SprintRejectionDeliveryProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,long cleaner,long branch,long inspection,CancellationToken ct)
    {
        var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString(),ct);
        var type=typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
        async Task<int> Pending()
        {
            var service=Activator.CreateInstance(type,db,TimeProvider.System)!;
            var pending=(Task)type.GetMethod("ReadPendingAsync")!.Invoke(service,[100,0L,ct])!;
            await pending;
            return ((Array)pending.GetType().GetProperty("Result")!.GetValue(pending)!).Cast<object>().Count(h=>(long)h.GetType().GetProperty("RecipientUserId")!.GetValue(h)! == 0);
        }
        if(await Pending()!=1)throw new Exception("Failed event missing from durable discovery");
        async Task Send()
        {
            await using var fresh=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            var service=Activator.CreateInstance(type,fresh,TimeProvider.System)!;
            await (Task<string>)type.GetMethod("DeliverAsync")!.Invoke(service,[audit.AuditId,0L,ct])!;
        }
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients ADD CONSTRAINT CK_DEV3_142A_NotificationFault CHECK(user_id<0)",ct);
        try {await Send();throw new Exception("Delivery fault ignored");}
        catch(DbUpdateException ex) when(ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number==547) { }
        if(await db.Notifications.AnyAsync(ct)||await db.NotificationRecipients.AnyAsync(ct)) throw new Exception("Failed fanout committed partial delivery");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_142A_NotificationFault",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}",ct);
        await Send();
        if(await db.NotificationRecipients.AnyAsync(r=>r.UserId==actor,ct)) throw new Exception("Inactive supervisor role delivered");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={actor}",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}",ct);
        await Send();
        if(await db.NotificationRecipients.AnyAsync(r=>r.UserId==actor,ct)||await db.NotificationRecipients.CountAsync(r=>r.UserId==cleaner,ct)!=1)
            throw new Exception("Revoked supervisor delivered or cleaner/supervisor dedup failed");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={actor}",ct);
        await Send();
        if(await db.NotificationRecipients.AnyAsync(r=>r.UserId==actor,ct)) throw new Exception("Inactive supervisor account delivered");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='ACTIVE' WHERE user_id={actor}",ct);
        await Task.WhenAll(Send(),Send());
        await Send();
        if(await db.Notifications.CountAsync(ct)!=1||await db.NotificationRecipients.CountAsync(ct)!=2
            ||await db.NotificationRecipients.CountAsync(r=>r.UserId==actor&&r.DeliveryStatus=="DELIVERED",ct)!=1
            ||await db.NotificationRecipients.CountAsync(r=>r.UserId==cleaner&&r.DeliveryStatus=="DELIVERED",ct)!=1)
            throw new Exception("Fanout retry/concurrency duplicate or missing recipient");
        if(await Pending()!=0)throw new Exception("Completed failed event still pending");
    }
}
