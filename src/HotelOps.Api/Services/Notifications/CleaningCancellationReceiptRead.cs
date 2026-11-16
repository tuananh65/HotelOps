using HotelOps.Api.Services.Insights;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public sealed partial class NotificationCenterService
{
    public async Task<CleaningCancellationReceiptDto> ReadCleaningCancellationAsync(long id, CancellationToken ct)
    {
        if(id<=0)throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=clock.GetUtcNow().UtcDateTime;var user=await UserAsync(now,ct);
        var receipt=await CancellationReceiptAsync(id,user,now,ct);
        await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
        await tx.CommitAsync(ct);
        return new(receipt.Id,receipt.BranchId,receipt.AssignmentId!.Value,receipt.OccurredAtUtc,receipt.State);
    }

    private async Task<CleaningCancellationResult> CancellationReceiptAsync(long id,long user,DateTime now,CancellationToken ct)
    {
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
        var notice=await Inbox(user,now).AsNoTracking().Where(n=>n.NotificationId==id
            && n.Notification.NotificationType=="CLEANING_TASK_CANCELLED" && n.Notification.SourceType=="CLEANING_TASK")
            .Select(n=>new { n.Notification.SourceId,n.Notification.CreatedBy }).SingleOrDefaultAsync(ct);
        if(notice==null || await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.notification_delivery_intents','U') IS NOT NULL AND OBJECT_ID('dbo.notification_operation_reservations','U') IS NOT NULL AND COL_LENGTH('dbo.notification_publications','occurrence_key') IS NOT NULL THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct)!=1)
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        var publications=await db.Database.SqlQuery<Guid>($"SELECT publication_id AS [Value] FROM dbo.notification_delivery_intents WHERE inbox_notification_id={id} AND user_id={user} AND delivery_channel='IN_APP' AND status='DELIVERED'").ToArrayAsync(ct);
        var proof=publications.Length==1 ? await CleaningCancellationNotificationSource.LoadAsync(db,publications[0],user,ct) : null;
        if(proof==null || proof.Value.Actor!=notice.CreatedBy || proof.Value.Receipt.Id!=notice.SourceId
            || !await CleaningCancellationRecipientAccess.AllowsAsync(db,user,proof.Value.Receipt,now,ct))
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        return proof.Value.Receipt;
    }
}
