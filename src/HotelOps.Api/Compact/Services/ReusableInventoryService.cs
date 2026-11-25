using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class ReusableInventoryService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,NotificationService notifications,LowStockService lowStock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private ReusableAccounting Accounting=>new(db,clock);
    private static string Payload(string action,object input)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{action,input}))));
    private async Task<bool> Already(Guid key,string hash,CancellationToken ct){
        Output.Key(key);var old=await db.AuditLogs.FirstOrDefaultAsync(a=>a.ActorUserId==actor.Id&&a.OperationKey==key&&a.EntityType=="ReusableCommand",ct);
        if(old==null)return false;Output.Require(old.AfterJson==JsonSerializer.Serialize(new{hash}),"IDEMPOTENCY_KEY_REUSED");return true;
    }
    private void Remember(long id,long branch,Guid key,string hash)=>audit.Add("ReusableCommand",id,"APPLIED",branch,after:new{hash},key:key);
    private async Task<bool> Store(long branch,CancellationToken ct)=>await access.HasRole("STOREKEEPER",branch,ct)&&await access.Allows("STOCK_ISSUE_POST",branch,ct:ct);
    public async Task<object> List(long branch,CancellationToken ct){
        var broad=await access.Allows("INVENTORY_VIEW",branch,ct:ct)||await access.Allows("ROOM_SUPPLY_CONFIG",branch,ct:ct);
        if(!broad)await access.Require("STAFF_SUPPLY_VIEW",branch,true,ct);
        var rows=await (from p in db.ReusablePositions.AsNoTracking() join i in db.InventoryItems on p.ItemId equals i.ItemId
            where p.BranchId==branch&&(broad||p.UserId==actor.Id||p.RoomId!=null&&db.WorkItems.Any(w=>w.RoomId==p.RoomId&&!WorkRules.Terminal.Contains(w.Status)&&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE")))
            orderby i.Name,p.LocationKey,p.Status select new{p.PositionId,p.BranchId,p.ItemId,i.Name,p.WarehouseId,p.UserId,p.RoomId,p.LocationKey,p.Status,p.Quantity,
                location=p.WarehouseId!=null?db.Warehouses.Where(w=>w.WarehouseId==p.WarehouseId).Select(w=>w.Name).FirstOrDefault():p.RoomId!=null?db.Rooms.Where(r=>r.RoomId==p.RoomId).Select(r=>r.Code).FirstOrDefault():db.Users.Where(u=>u.UserId==p.UserId).Select(u=>u.FullName).FirstOrDefault(),
                pendingWriteOff=db.ReusableWriteOffs.Where(w=>w.PositionId==p.PositionId&&w.Status=="PENDING").Sum(w=>(decimal?)w.Quantity)??0,rowVersion=Convert.ToBase64String(p.RowVersion)}).ToArrayAsync(ct);
        var deadlines=new Dictionary<long,DateOnly?>();var overdue=new HashSet<long>();
        foreach(var item in rows.Select(r=>r.ItemId).Distinct()){
            deadlines[item]=await ReusableShelfLifePolicy.Deadline(db,branch,item,ct);
            if(await ReusableShelfLifePolicy.Overdue(db,branch,item,Now,ct))overdue.Add(item);
        }
        return new{positions=rows,shelfPolicies=deadlines.Select(p=>new{itemId=p.Key,deadline=p.Value,overdue=overdue.Contains(p.Key)}),totals=rows.GroupBy(x=>new{x.ItemId,x.Name}).Select(g=>new{g.Key.ItemId,g.Key.Name,
            owned=g.Where(x=>x.Status!="WRITTEN_OFF"&&x.Status!="LEGACY_REVIEW").Sum(x=>x.Quantity),ready=overdue.Contains(g.Key.ItemId)?0:g.Where(x=>x.Status=="READY").Sum(x=>x.Quantity),
            unresolvedLegacy=g.Where(x=>x.Status=="LEGACY_REVIEW").Sum(x=>x.Quantity),writtenOff=g.Where(x=>x.Status=="WRITTEN_OFF").Sum(x=>x.Quantity)})};
    }
    public async Task<object> Destinations(long branch,CancellationToken ct){
        Output.Require(await Store(branch,ct),"STOREKEEPER_REQUIRED",403);
        var warehouses=await db.Warehouses.AsNoTracking().Where(w=>w.BranchId==branch&&w.IsActive).Select(w=>new{id=w.WarehouseId,name=w.Name}).ToArrayAsync(ct);
        var rooms=await db.Rooms.AsNoTracking().Where(r=>r.BranchId==branch&&r.IsActive).OrderBy(r=>r.Code).Select(r=>new{id=r.RoomId,name=r.Code}).ToArrayAsync(ct);
        var ids=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId
            where ur.BranchId==branch&&ur.RevokedAt==null&&ur.AssignedAt<=Now&&(ur.ExpiresAt==null||ur.ExpiresAt>Now)&&role.Code=="HK_STAFF"&&role.IsActive select ur.UserId).Distinct().ToArrayAsync(ct);
        var staff=await db.Users.AsNoTracking().Where(u=>ids.Contains(u.UserId)&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null).OrderBy(u=>u.FullName).Select(u=>new{id=u.UserId,name=u.FullName}).ToArrayAsync(ct);
        return new{warehouses,rooms,staff};
    }
    public async Task<object> History(long branch,CancellationToken ct){
        if(!await Store(branch,ct))await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",branch,ct);
        return await (from m in db.ReusableMovements.AsNoTracking() join i in db.InventoryItems on m.ItemId equals i.ItemId
            where m.BranchId==branch orderby m.ReusableMovementId descending select new{m.ReusableMovementId,i.Name,m.Kind,m.Quantity,m.SourcePositionId,m.DestinationPositionId,m.DocumentLineId,m.WorkId,m.Reason,m.ActorUserId,m.OccurredAt,
                source=db.ReusablePositions.Where(p=>p.PositionId==m.SourcePositionId).Select(p=>new{p.Status,p.LocationKey}).FirstOrDefault(),
                destination=db.ReusablePositions.Where(p=>p.PositionId==m.DestinationPositionId).Select(p=>new{p.Status,p.LocationKey}).FirstOrDefault(),
                documentId=db.InventoryDocumentLines.Where(l=>l.LineId==m.DocumentLineId).Select(l=>(long?)l.DocumentId).FirstOrDefault()}).ToArrayAsync(ct);
    }
    private async Task Destination(long branch,ReusableMoveRequest r,CancellationToken ct){
        Output.Require(new[]{r.WarehouseId,r.UserId,r.RoomId}.Count(x=>x!=null)==1,"CUSTODY_LOCATION_REQUIRED",400);
        if(r.WarehouseId!=null)Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==r.WarehouseId&&w.BranchId==branch&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
        if(r.RoomId!=null)Output.Require(await db.Rooms.AnyAsync(w=>w.RoomId==r.RoomId&&w.BranchId==branch&&w.IsActive,ct),"ROOM_UNAVAILABLE");
        if(r.UserId!=null)await access.EligibleFor(r.UserId.Value,branch,"CLEANING_EXECUTE",ct);
        Output.Require(r.Status switch{"READY" or "LAUNDRY"=>r.WarehouseId!=null,"STAFF_HOLDING"=>r.UserId!=null,"IN_USE"=>r.RoomId!=null,"DIRTY" or "DAMAGED"=>true,_=>false},"INVALID_CUSTODY_DESTINATION",400);
    }
    public Task<object> Move(long id,ReusableMoveRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        await StaffSupplyService.Lock(db,ct);var source=await db.ReusablePositions.SingleOrDefaultAsync(p=>p.PositionId==id,ct)??throw new ApiException(404,"CUSTODY_NOT_FOUND");
        var store=await Store(source.BranchId,ct);
        if(!store){
            await access.RequireRolePermission("HK_STAFF","CLEANING_EXECUTE",source.BranchId,ct);
            Output.Require(r.WorkId!=null&&await db.WorkItems.AnyAsync(w=>w.WorkId==r.WorkId&&w.BranchId==source.BranchId&&(w.Kind=="CLEANING"||w.Kind=="REWORK")&&w.Status=="IN_PROGRESS"
                &&(source.UserId==actor.Id||source.RoomId==w.RoomId)&&(r.RoomId==null||r.RoomId==w.RoomId)
                &&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"),ct),"ASSIGNED_ROOM_CUSTODY_REQUIRED",403);
            Output.Require(r.UserId==null||r.UserId==actor.Id,"CUSTODY_OWNER_REQUIRED",403);
            Output.Require(r.Status is "IN_USE" or "DIRTY" or "DAMAGED","STOREKEEPER_REQUIRED",403);
        }
        var hash=Payload($"move:{id}",r);if(await Already(r.OperationKey,hash,ct))return (object)new{alreadyApplied=true};
        Output.Version(source.RowVersion,r.RowVersion);Output.Reason(r.Reason);await Destination(source.BranchId,r,ct);
        Output.Require(source.Status!="WRITTEN_OFF"&&source.Status!="IN_TRANSIT","INVALID_CUSTODY_TRANSITION");
        if(source.Status=="LEGACY_REVIEW")Output.Require(store&&r.Status=="IN_USE"&&source.RoomId==r.RoomId,"PHYSICAL_RECONCILIATION_REQUIRED");
        else Output.Require((source.Status,r.Status) switch{
            ("READY","STAFF_HOLDING" or "IN_USE" or "DAMAGED")=>true,
            ("STAFF_HOLDING","IN_USE" or "READY" or "DAMAGED")=>true,
            ("IN_USE","DIRTY" or "DAMAGED")=>true,("DIRTY","LAUNDRY" or "DAMAGED")=>true,
            ("LAUNDRY","READY" or "DAMAGED")=>true,("DAMAGED","READY")=>store,_=>false},"INVALID_CUSTODY_TRANSITION");
        var target=await Accounting.Position(source.BranchId,source.ItemId,r.Status,r.WarehouseId,r.UserId,r.RoomId,ct);
        if(r.Replace){
            Output.Require(r.Status=="IN_USE"&&(source.Status=="READY"||source.Status=="STAFF_HOLDING"),"REPLACEMENT_SOURCE_REQUIRED");
            var dirty=await Accounting.Position(source.BranchId,source.ItemId,"DIRTY",null,null,r.RoomId,ct);
            await Accounting.Move(target,dirty,r.Quantity,actor.Id,r.OperationKey,"REPLACEMENT",r.Reason,ct,r.WorkId);
        }
        await Accounting.Move(source,target,r.Quantity,actor.Id,r.OperationKey,"MOVE",r.Reason,ct,r.WorkId);
        Remember(id,source.BranchId,r.OperationKey,hash);await db.SaveChangesAsync(ct);
        await lowStock.Reconcile(ct,source.BranchId);
        return (object)new{applied=true};
    },ct);
    public Task<object> Propose(long id,ReusableWriteOffRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        await StaffSupplyService.Lock(db,ct);var source=await db.ReusablePositions.SingleOrDefaultAsync(p=>p.PositionId==id,ct)??throw new ApiException(404,"CUSTODY_NOT_FOUND");
        Output.Require(await Store(source.BranchId,ct),"STOREKEEPER_REQUIRED",403);var hash=Payload($"write-off:{id}",r);
        if(await Already(r.OperationKey,hash,ct))return (object)new{alreadyApplied=true};
        Output.Version(source.RowVersion,r.RowVersion);Output.Reason(r.Reason);Output.Require(source.Status=="DAMAGED","DAMAGED_ITEMS_REQUIRED");
        var pending=await db.ReusableWriteOffs.Where(w=>w.PositionId==id&&w.Status=="PENDING").SumAsync(w=>(decimal?)w.Quantity,ct)??0;
        Output.Require(r.Quantity>0&&SupplyMath.ValidQuantity(r.Quantity)&&r.Quantity<=source.Quantity-pending,"WRITE_OFF_QUANTITY_EXCEEDED",400);
        var proposal=new ReusableWriteOff{BranchId=source.BranchId,PositionId=id,Quantity=r.Quantity,Reason=r.Reason,CreatedBy=actor.Id,CreatedAt=Now,OperationKey=r.OperationKey};
        db.ReusableWriteOffs.Add(proposal);await db.SaveChangesAsync(ct);
        foreach(var user in await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId where ur.BranchId==source.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=Now&&(ur.ExpiresAt==null||ur.ExpiresAt>Now)&&role.Code=="OPERATION_MANAGER"&&role.IsActive select ur.UserId).Distinct().ToArrayAsync(ct))
            notifications.Add(source.BranchId,user,"REUSABLE_WRITE_OFF_REQUESTED","Đề nghị ghi giảm vật tư hư hỏng",null,null,r.OperationKey);
        Remember(proposal.WriteOffId,source.BranchId,r.OperationKey,hash);await db.SaveChangesAsync(ct);return (object)new{proposal.WriteOffId,proposal.Status};
    },ct);
    public async Task<object> WriteOffs(long branch,CancellationToken ct){
        if(!await Store(branch,ct))await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",branch,ct);
        return await (from w in db.ReusableWriteOffs.AsNoTracking() join p in db.ReusablePositions on w.PositionId equals p.PositionId join i in db.InventoryItems on p.ItemId equals i.ItemId
            where w.BranchId==branch orderby w.WriteOffId descending select new{w.WriteOffId,w.PositionId,i.Name,w.Quantity,w.Status,w.Reason,w.CreatedAt,w.CreatedBy,w.DecidedAt,w.DecidedBy,w.DecisionReason,rowVersion=Convert.ToBase64String(w.RowVersion)}).ToArrayAsync(ct);
    }
    public Task<object> Decide(long id,DecideWriteOffRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        await StaffSupplyService.Lock(db,ct);var proposal=await db.ReusableWriteOffs.SingleOrDefaultAsync(w=>w.WriteOffId==id,ct)??throw new ApiException(404,"WRITE_OFF_NOT_FOUND");
        await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",proposal.BranchId,ct);
        var hash=Payload($"decide:{id}",r);if(await Already(r.OperationKey,hash,ct))return (object)new{proposal.WriteOffId,proposal.Status};
        Output.Version(proposal.RowVersion,r.RowVersion);Output.Require(proposal.Status=="PENDING","WRITE_OFF_ALREADY_DECIDED");Output.Require(proposal.CreatedBy!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);
        if(!r.Approve)Output.Reason(r.Reason);
        proposal.Status=r.Approve?"APPROVED":"REJECTED";proposal.DecidedBy=actor.Id;proposal.DecidedAt=Now;proposal.DecisionReason=r.Reason;await db.SaveChangesAsync(ct);
        if(r.Approve){var source=await db.ReusablePositions.SingleAsync(p=>p.PositionId==proposal.PositionId,ct);Output.Require(source.Status=="DAMAGED","DAMAGED_ITEMS_REQUIRED");
            var target=await Accounting.Position(source.BranchId,source.ItemId,"WRITTEN_OFF",source.WarehouseId,source.UserId,source.RoomId,ct);
            await Accounting.Move(source,target,proposal.Quantity,actor.Id,r.OperationKey,"WRITE_OFF",proposal.Reason,ct);}
        Remember(id,proposal.BranchId,r.OperationKey,hash);notifications.Add(proposal.BranchId,proposal.CreatedBy,"REUSABLE_WRITE_OFF_DECIDED",r.Approve?"Đề nghị ghi giảm đã được xác nhận":"Đề nghị ghi giảm bị từ chối",null,null,r.OperationKey);
        await db.SaveChangesAsync(ct);return (object)new{proposal.WriteOffId,proposal.Status};
    },ct);
}
