using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class NotificationService(HotelOpsCompactDbContext db,Actor actor,Access access,Atomic atomic,TimeProvider clock)
{
    public void Add(long branch,long recipient,string type,string title,long? work,long? document,Guid key,bool ack=false)
    {
        if(db.Notifications.Local.Any(n=>n.RecipientUserId==recipient&&n.EventKey==key))return;
        db.Notifications.Add(new Notification{BranchId=branch,RecipientUserId=recipient,NotificationType=type,Title=title,Message=title,WorkId=work,DocumentId=document,EventKey=key,RequiresAcknowledgment=ack,AvailableAt=clock.GetUtcNow().UtcDateTime});
    }
    public async Task ForPermission(long branch,string code,string type,string title,long? work,long? document,Guid key,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var recipients=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.PermissionId join u in db.Users on ur.UserId equals u.UserId
            where role.IsActive&&p.IsActive&&p.Code==code&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
                &&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&((ur.BranchId==null&&rp.DataScope=="ALL")||(ur.BranchId==branch&&(rp.DataScope=="ALL"||rp.DataScope=="BRANCH")
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==branch&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))))
            select u.UserId).Distinct().ToArrayAsync(ct);
        foreach(var user in recipients)Add(branch,user,type,title,work,document,key);
    }
    public async Task<int> UnreadCount(long branchId,CancellationToken ct){
        var now=clock.GetUtcNow().UtcDateTime;
        var rows=await db.Notifications.AsNoTracking().Where(n=>n.BranchId==branchId&&n.RecipientUserId==actor.Id&&n.ReadAt==null&&n.AvailableAt<=now&&(n.ExpiresAt==null||n.ExpiresAt>now)).ToArrayAsync(ct);
        var count=0;foreach(var n in rows)if(await Visible(n,ct))count++;return count;
    }
    public async Task<object> Inbox(int page,int pageSize,CancellationToken ct,long? branchId=null){
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var now=clock.GetUtcNow().UtcDateTime;
        var candidates=await db.Notifications.AsNoTracking().Where(n=>n.RecipientUserId==actor.Id&&(branchId==null||n.BranchId==branchId)&&n.AvailableAt<=now&&(n.ExpiresAt==null||n.ExpiresAt>now)).OrderByDescending(n=>n.NotificationId).Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        var visible=new List<Notification>();foreach(var n in candidates)if(await Visible(n,ct))visible.Add(n);
        // Concurrent inbox reads must not promote Serializable shared locks to writes.
        // Delivery is idempotent; update only this recipient's authorized, undelivered rows.
        var ids=visible.Where(n=>n.DeliveredAt==null).Select(n=>n.NotificationId).ToArray();
        if(ids.Length>0)await db.Notifications.Where(n=>ids.Contains(n.NotificationId)&&n.RecipientUserId==actor.Id&&n.DeliveredAt==null)
            .ExecuteUpdateAsync(set=>set.SetProperty(n=>n.DeliveredAt,now),ct);
        var visibleIds=visible.Select(n=>n.NotificationId).ToArray();
        return new{items=(await db.Notifications.AsNoTracking().Where(n=>visibleIds.Contains(n.NotificationId)&&n.RecipientUserId==actor.Id).OrderByDescending(n=>n.NotificationId).ToArrayAsync(ct)).Select(Output.Record),page,pageSize,hasMore=candidates.Length==pageSize};
    }
    private async Task<bool> Visible(Notification n,CancellationToken ct)
    {
        if(n.WorkId!=null){var w=await db.WorkItems.AsNoTracking().SingleAsync(w=>w.WorkId==n.WorkId,ct);
            var owns=w.CreatedBy==actor.Id||await db.WorkAssignments.AnyAsync(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct);
            if(!owns)owns=await db.WorkEvents.AnyAsync(e=>e.WorkId==w.WorkId&&e.EventType=="HANDOVER_OFFERED"&&e.RecipientUserId==actor.Id&&!db.WorkEvents.Any(d=>d.RelatedEventId==e.WorkEventId&&(d.EventType=="HANDOVER_ACCEPTED"||d.EventType=="HANDOVER_REJECTED")),ct);
            return await access.Allows(WorkRules.Permission(w.Kind,"read"),n.BranchId,owns,ct:ct);}
        if(n.DocumentId!=null){var d=await db.InventoryDocuments.AsNoTracking().SingleAsync(d=>d.DocumentId==n.DocumentId,ct);return await access.Allows(d.Kind=="MATERIAL_REQUEST"?"MR_VIEW":"INVENTORY_VIEW",n.BranchId,d.CreatedBy==actor.Id,ct:ct);}
        return await access.Allows(n.NotificationType=="LOW_STOCK"?"INVENTORY_VIEW":"ROOM_VIEW",n.BranchId,ct:ct);
    }
    public Task<object> Read(long id,bool acknowledge,CancellationToken ct)=>atomic.Run(async()=>{
        var n=await db.Notifications.SingleOrDefaultAsync(n=>n.NotificationId==id&&n.RecipientUserId==actor.Id,ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Require(await Visible(n,ct),"PERMISSION_DENIED",403);var now=clock.GetUtcNow().UtcDateTime;n.DeliveredAt??=now;n.ReadAt??=now;
        if(acknowledge){Output.Require(n.RequiresAcknowledgment,"ACKNOWLEDGMENT_NOT_REQUIRED",400);n.AcknowledgedAt??=now;}
        await db.SaveChangesAsync(ct);return (object)Output.Record(n);
    },ct);
}
