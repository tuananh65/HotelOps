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
            where role.IsActive&&p.IsActive&&p.Code==code&&ur.RevokedAt==null&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
                &&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&((ur.BranchId==null&&rp.DataScope=="ALL")||(ur.BranchId==branch&&(rp.DataScope=="ALL"||rp.DataScope=="BRANCH")
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==branch&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))))
            select u.UserId).Distinct().ToArrayAsync(ct);
        foreach(var user in recipients)Add(branch,user,type,title,work,document,key);
    }
    public async Task<int> UnreadCount(long branchId,CancellationToken ct){
        var now=clock.GetUtcNow().UtcDateTime;
        var rows=await InBranch(branchId).Where(n=>n.RecipientUserId==actor.Id&&n.ReadAt==null&&n.AvailableAt<=now&&(n.ExpiresAt==null||n.ExpiresAt>now)).ToArrayAsync(ct);
        var checks=new VisibilityChecks(db,access);var count=0;foreach(var n in rows)if(await Visible(n,ct,checks))count++;return count;
    }
    public async Task<NotificationPulse> RealtimeSnapshot(long? branchId,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var rows=await InBranch(branchId).Where(n=>n.RecipientUserId==actor.Id&&n.ReadAt==null
            &&n.AvailableAt<=now&&(n.ExpiresAt==null||n.ExpiresAt>now))
            .OrderBy(n=>n.NotificationId).ToArrayAsync(ct);
        var ids=new List<long>();
        var checks=new VisibilityChecks(db,access);
        foreach(var row in rows)if(await Visible(row,ct,checks))ids.Add(row.NotificationId);
        var bytes=System.Text.Encoding.UTF8.GetBytes(string.Join(',',ids));
        var eventId=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        return new NotificationPulse(eventId,ids.Count,ids.TakeLast(100).ToArray());
    }
    public async Task<object> Inbox(int page,int pageSize,CancellationToken ct,long? branchId=null){
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var now=clock.GetUtcNow().UtcDateTime;
        var candidates=await InBranch(branchId).Where(n=>n.RecipientUserId==actor.Id&&n.AvailableAt<=now&&(n.ExpiresAt==null||n.ExpiresAt>now)).OrderByDescending(n=>n.NotificationId).Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        var checks=new VisibilityChecks(db,access);var visible=new List<Notification>();foreach(var n in candidates)if(await Visible(n,ct,checks))visible.Add(n);
        // Concurrent inbox reads must not promote Serializable shared locks to writes.
        // Delivery is idempotent; update only this recipient's authorized, undelivered rows.
        var ids=visible.Where(n=>n.DeliveredAt==null).Select(n=>n.NotificationId).ToArray();
        if(ids.Length>0)await db.Notifications.Where(n=>ids.Contains(n.NotificationId)&&n.RecipientUserId==actor.Id&&n.DeliveredAt==null)
            .ExecuteUpdateAsync(set=>set.SetProperty(n=>n.DeliveredAt,now),ct);
        var visibleIds=visible.Select(n=>n.NotificationId).ToArray();
        return new{items=(await db.Notifications.AsNoTracking().Where(n=>visibleIds.Contains(n.NotificationId)&&n.RecipientUserId==actor.Id).OrderByDescending(n=>n.NotificationId).ToArrayAsync(ct)).Select(Output.Record),page,pageSize,hasMore=candidates.Length==pageSize};
    }
    // Initial shift kits are no longer issued. Retain their historical alerts in the database,
    // but exclude obsolete shortage requests from both inbox paging and realtime counts.
    private IQueryable<Notification> InBranch(long? branchId) => db.Notifications.AsNoTracking().Where(n=>n.NotificationType!="SUPPLY_INITIAL_SHORT").Where(n=>branchId==null||n.BranchId==branchId
        ||db.InventoryDocuments.Any(d=>d.DocumentId==n.DocumentId
            &&((d.Kind=="TRANSFER_REQUEST"||d.Kind=="TRANSFER")&&d.DestinationBranchId==branchId
                ||d.Kind=="TRANSFER_RECEIPT"&&db.InventoryDocuments.Any(source=>source.DocumentId==d.SourceDocumentId&&source.Kind=="TRANSFER"&&source.BranchId==branchId))));
    private async Task<bool> Visible(Notification n,CancellationToken ct,VisibilityChecks? snapshot=null)
    {
        var checks=snapshot??new VisibilityChecks(db,access);
        if(n.NotificationType.StartsWith("REPLACEMENT_")&&await checks.HasRole("OPERATION_MANAGER",n.BranchId,ct))
            return await checks.Allows("ASSET_MANAGE",n.BranchId,ct:ct);
        if(n.NotificationType is "LOW_STOCK" or "EXPIRY_WARNING")return await checks.HasRole("STOREKEEPER",n.BranchId,ct)&&await checks.Allows("INVENTORY_VIEW",n.BranchId,ct:ct);
        if(n.NotificationType=="SUPPLY_INITIAL_SHORT")return n.RecipientUserId==actor.Id&&await checks.Allows("STAFF_SUPPLY_VIEW",n.BranchId,true,ct:ct);
        if(n.WorkId!=null){var requestAccess=await RequestWorkflowVisibility.CanRead(db,access,actor.Id,n.WorkId.Value,clock.GetUtcNow().UtcDateTime,ct);
            if(requestAccess!=null)return requestAccess.Value;
            var w=await db.WorkItems.AsNoTracking().SingleAsync(w=>w.WorkId==n.WorkId,ct);
            if(n.NotificationType.StartsWith("ROOM_ISSUE_")&&w.CreatedBy==actor.Id&&await checks.Allows("IR_VIEW",n.BranchId,true,ct:ct))return true;
            if(n.NotificationType.StartsWith("ROOM_ISSUE_")&&await checks.HasRole("STOREKEEPER",n.BranchId,ct)
                &&await checks.Allows("MR_FULFILL",n.BranchId,ct:ct)
                &&await db.WorkEvents.AnyAsync(e=>e.WorkId==w.WorkId&&e.EventType=="ROOM_ISSUE_MATERIAL_REQUESTED",ct))return true;
            var owns=w.CreatedBy==actor.Id||await db.WorkAssignments.AnyAsync(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct);
            if(!owns)owns=await db.WorkEvents.AnyAsync(e=>e.WorkId==w.WorkId&&e.EventType=="HANDOVER_OFFERED"&&e.RecipientUserId==actor.Id&&!db.WorkEvents.Any(d=>d.RelatedEventId==e.WorkEventId&&(d.EventType=="HANDOVER_ACCEPTED"||d.EventType=="HANDOVER_REJECTED")),ct);
            return await checks.Allows(WorkRules.Permission(w.Kind,"read"),n.BranchId,owns,ct:ct);}
        if(n.DocumentId!=null){var d=await checks.Document(n.DocumentId.Value,ct)??throw new InvalidOperationException("Notification document does not exist.");
            if(d.Kind is "TRANSFER_REQUEST" or "TRANSFER" or "TRANSFER_RECEIPT")return await DocumentService.CanReadTransfer(db,access,d,ct,(code,branch,token)=>checks.Allows(code,branch,ct:token),checks.HasRole,checks.Document);
            return await checks.Allows(d.Kind=="MATERIAL_REQUEST"?"MR_VIEW":"INVENTORY_VIEW",n.BranchId,d.CreatedBy==actor.Id,ct:ct);}
        return await checks.Allows(n.NotificationType=="LOW_STOCK"?"INVENTORY_VIEW":"ROOM_VIEW",n.BranchId,ct:ct);
    }
    // Decisions live only for one snapshot. Every subsequent pulse rechecks session,
    // membership and role validity; no authorization result survives a stream iteration.
    private sealed class VisibilityChecks(HotelOpsCompactDbContext db,Access access)
    {
        private readonly Dictionary<long,Task<InventoryDocument?>> documents=[];
        private readonly Dictionary<(string Code,long? Branch,bool Owns),Task<bool>> permissions=[];
        private readonly Dictionary<(string Code,long Branch),Task<bool>> roles=[];
        public Task<InventoryDocument?> Document(long id,CancellationToken ct)
        {
            if(!documents.TryGetValue(id,out var result))documents[id]=result=db.InventoryDocuments.AsNoTracking().SingleOrDefaultAsync(d=>d.DocumentId==id,ct);
            return result;
        }
        public Task<bool> Allows(string code,long? branch,bool owns=false,CancellationToken ct=default)
        {
            var key=(code,branch,owns);
            if(!permissions.TryGetValue(key,out var result))permissions[key]=result=access.Allows(code,branch,owns,ct:ct);
            return result;
        }
        public Task<bool> HasRole(string code,long branch,CancellationToken ct)
        {
            var key=(code,branch);
            if(!roles.TryGetValue(key,out var result))roles[key]=result=access.HasRole(code,branch,ct);
            return result;
        }
    }
    public Task<object> Read(long id,bool acknowledge,CancellationToken ct)=>atomic.Run(async()=>{
        var n=await db.Notifications.SingleOrDefaultAsync(n=>n.NotificationId==id&&n.RecipientUserId==actor.Id,ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Require(await Visible(n,ct),"PERMISSION_DENIED",403);var now=clock.GetUtcNow().UtcDateTime;n.DeliveredAt??=now;n.ReadAt??=now;
        if(acknowledge){Output.Require(n.RequiresAcknowledgment,"ACKNOWLEDGMENT_NOT_REQUIRED",400);n.AcknowledgedAt??=now;}
        await db.SaveChangesAsync(ct);return (object)Output.Record(n);
    },ct);
}
