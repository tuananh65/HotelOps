using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace HotelOps.Api.Services.Notifications;

internal static class ShiftAssignmentNoticeAccess
{
    internal const string Kind="SHIFT_ASSIGNMENT";
    internal static async Task<long?> BranchAsync(HotelOpsDbContext db,long assignment,long user,DateTime now,CancellationToken ct,bool includeCancelled=false)
    {
        var branch=await db.ShiftAssignments.AsNoTracking().Where(a=>a.ShiftAssignmentId==assignment&&a.UserId==user
            &&(a.AssignmentStatus=="ASSIGNED"||includeCancelled&&a.AssignmentStatus=="CANCELLED")&&a.AssignedAt<=now&&a.Shift.Status=="PLANNED"
            &&a.Shift.ScheduledStartAt<a.Shift.ScheduledEndAt&&a.Shift.Department.IsActive&&a.Shift.Department.BranchId==a.Shift.BranchId
            &&a.Shift.ShiftTemplate.IsActive&&a.Shift.ShiftTemplate.BranchId==a.Shift.BranchId)
            .Select(a=>(long?)a.Shift.BranchId).SingleOrDefaultAsync(ct);
        if(branch==null)return null;
        var data=new EfPermissionDataSource(db);
        if(!await data.IsActiveUserAsync(user,ct)||!await data.IsActiveBranchAsync(branch.Value,ct)
            ||!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=now,ct))return null;
        foreach(var grant in await data.ReadPathsAsync(user,"SHIFT_VIEW",now,ct))
            if(grant.PermissionCode=="SHIFT_VIEW"&&PermissionGrantRules.IsEligible(grant,now)
                &&(PermissionGrantRules.EffectiveScope(grant)=="GLOBAL"||grant.BranchId==branch&&PermissionGrantRules.EffectiveScope(grant) is "BRANCH" or "OWN"))return branch;
        return null;
    }
    internal static async Task<long?> TargetAsync(HotelOpsDbContext db,long assignment,long user,long? creator,DateTime created,CancellationToken ct)
    {
        if(!await db.ShiftAssignments.AsNoTracking().AnyAsync(a=>a.ShiftAssignmentId==assignment&&a.UserId==user&&a.AssignedBy==creator&&a.AssignedAt==created,ct))return null;
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return await BranchAsync(db,assignment,user,now,ct);
    }
}

public sealed class ShiftPlanNotificationParticipant:IShiftPlanNotificationParticipant
{
    public async Task<int> StageAsync(HotelOpsDbContext db,Guid transactionId,long actor,IReadOnlyList<ShiftAssignmentNotice> notices,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction?.TransactionId!=transactionId||transactionId==Guid.Empty||actor<=0
            ||notices.Count is <1 or >500||notices.Select(n=>n.AssignmentId).Distinct().Count()!=notices.Count)
            throw new InvalidOperationException("Owned shift transaction and unique assignments required.");
        foreach(var notice in notices.OrderBy(n=>n.AssignmentId))
        {
            var assignment=await db.ShiftAssignments.AsNoTracking().SingleOrDefaultAsync(a=>a.ShiftAssignmentId==notice.AssignmentId
                &&a.UserId==notice.EmployeeId&&a.ShiftId==notice.ShiftId&&a.AssignedBy==actor&&a.AssignmentStatus=="ASSIGNED",ct)
                ??throw new InvalidOperationException("Shift notice assignment mismatch.");
            // The caller owns user/shift locks. A repeated stage cannot create a second delivery intent.
            if(await db.Notifications.AnyAsync(n=>n.NotificationType==ShiftAssignmentNoticeAccess.Kind&&n.SourceType==ShiftAssignmentNoticeAccess.Kind&&n.SourceId==notice.AssignmentId,ct))
                throw new InvalidOperationException("Shift notice already staged.");
            var notification=new Notification{NotificationType=ShiftAssignmentNoticeAccess.Kind,SourceType=ShiftAssignmentNoticeAccess.Kind,SourceId=notice.AssignmentId,
                Title="Shift assigned",Message="A shift has been assigned to you. Open the assignment to review the current schedule.",Priority="NORMAL",CreatedBy=actor,CreatedAt=assignment.AssignedAt};
            notification.NotificationRecipients.Add(new NotificationRecipient{UserId=notice.EmployeeId,DeliveryChannel="IN_APP",DeliveryStatus="PENDING"});
            db.Notifications.Add(notification);
        }
        await db.SaveChangesAsync(ct);
        return notices.Count;
    }
}

internal sealed class ShiftAssignmentNotificationDelivery(HotelOpsDbContext db,TimeProvider clock,IOptions<NotificationCenterOptions> retention)
{
    internal async Task<long[]> PendingAsync(long after,CancellationToken ct)
    {
        var window=NotificationRetention.At(clock.GetUtcNow().UtcDateTime,retention.Value);
        return await db.NotificationRecipients.AsNoTracking().Where(r=>r.RecipientId>after&&r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="PENDING"
            &&((r.Notification.NotificationType==ShiftAssignmentNoticeAccess.Kind&&r.Notification.SourceType==ShiftAssignmentNoticeAccess.Kind)
                ||(r.Notification.NotificationType==ShiftCancellationSource.Kind&&r.Notification.SourceType==ShiftCancellationSource.Kind))
            &&r.Notification.CreatedAt>=window.Cutoff&&r.Notification.CreatedAt<=window.Now)
            .OrderBy(r=>r.RecipientId).Take(100).Select(r=>r.RecipientId).ToArrayAsync(ct);
    }
    internal async Task DeliverAsync(long id,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Independent delivery context required.");
        var user=await db.NotificationRecipients.AsNoTracking().Where(r=>r.RecipientId==id).Select(r=>(long?)r.UserId).SingleOrDefaultAsync(ct);
        if(user==null)return;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([user.Value],ct);
        var recipient=await db.NotificationRecipients.FromSqlInterpolated($"SELECT * FROM dbo.notification_recipients WITH(UPDLOCK,HOLDLOCK) WHERE recipient_id={id}").SingleOrDefaultAsync(ct);
        if(recipient==null||recipient.UserId!=user||recipient.DeliveryChannel!="IN_APP"||recipient.DeliveryStatus!="PENDING")return;
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.NotificationId==recipient.NotificationId,ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if(notice.SourceType!=notice.NotificationType||notice.NotificationType is not (ShiftAssignmentNoticeAccess.Kind or ShiftCancellationSource.Kind)||notice.SourceId is not >0
            ||!NotificationRetention.At(now,retention.Value).Contains(notice.CreatedAt)||(notice.ExpiresAt!=null&&notice.ExpiresAt<=now))return;
        if(!await EligibleAsync(notice,user.Value,ct))return;
        recipient.DeliveryStatus="DELIVERED";recipient.DeliveredAt=now;recipient.FailureReason=null;
        await db.SaveChangesAsync(ct);
        if(!await EligibleAsync(notice,user.Value,ct))return;
        await tx.CommitAsync(ct);
    }
    private async Task<bool> EligibleAsync(Notification notice,long user,CancellationToken ct)
        =>notice.NotificationType==ShiftCancellationSource.Kind
            ?await ShiftCancellationSource.TargetAsync(db,notice.SourceId!.Value,user,notice.CreatedBy,notice.CreatedAt,ct)!=null
            :await ShiftAssignmentNoticeAccess.TargetAsync(db,notice.SourceId!.Value,user,notice.CreatedBy,notice.CreatedAt,ct)!=null;

}
