using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class AssetService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock)
{
    public async Task<object> List(long branchId,CancellationToken ct)
    {
        await access.Require("ASSET_VIEW",branchId,ct:ct);
        return await db.Assets.AsNoTracking().Where(a=>a.BranchId==branchId)
            .OrderBy(a=>a.RoomId).ThenBy(a=>a.Name).ThenBy(a=>a.AssetId)
            .Select(a=>new {a.AssetId,a.BranchId,a.AssetCode,a.Name,a.SerialNumber,a.ConditionCode,a.IsActive,a.RoomId,a.LocationId,
                roomCode=db.Rooms.Where(r=>r.RoomId==a.RoomId&&r.BranchId==branchId).Select(r=>r.Code).FirstOrDefault(),
                locationName=db.Locations.Where(l=>l.LocationId==a.LocationId&&l.BranchId==branchId).Select(l=>l.Name).FirstOrDefault()})
            .ToArrayAsync(ct);
    }
    public async Task<object> Detail(long id,CancellationToken ct){var a=await db.Assets.SingleOrDefaultAsync(x=>x.AssetId==id,ct)??throw new ApiException(404,"ASSET_NOT_FOUND");await access.Require("ASSET_VIEW",a.BranchId,ct:ct);
        var maintenance=await access.Allows("MAINTENANCE_VIEW",a.BranchId,ct:ct);var incidents=await access.Allows("INCIDENT_VIEW",a.BranchId,ct:ct);var pm=await access.Allows("PM_MANAGE",a.BranchId,ct:ct);
        return new{asset=Output.Record(a),
            location=await db.Locations.AsNoTracking().Where(l=>l.LocationId==a.LocationId&&l.BranchId==a.BranchId).Select(l=>l.Name).SingleOrDefaultAsync(ct),
            room=await db.Rooms.AsNoTracking().Where(r=>r.RoomId==a.RoomId&&r.BranchId==a.BranchId).Select(r=>r.Code).SingleOrDefaultAsync(ct),
            work=await db.WorkItems.AsNoTracking().Where(w=>w.AssetId==id&&w.BranchId==a.BranchId&&((maintenance&&w.Kind=="MAINTENANCE")||(incidents&&w.Kind=="INCIDENT"))).OrderByDescending(w=>w.WorkId).Take(100).Select(w=>new{w.WorkId,w.Kind,w.Title,w.Status,w.CompletedAt,w.VerifiedAt}).ToArrayAsync(ct),
            plans=await db.WorkTemplates.AsNoTracking().Where(t=>pm&&t.AssetId==id&&t.BranchId==a.BranchId&&t.Kind=="MAINTENANCE").Select(t=>new{t.WorkTemplateId,t.Title,t.NextDueAt,t.IsActive}).ToArrayAsync(ct),
            history=(await db.AssetEvents.AsNoTracking().Where(x=>x.AssetId==id).OrderByDescending(x=>x.AssetEventId).Take(200).ToArrayAsync(ct)).Select(Output.Record)};}
    public Task<object> Act(long id,string action,AssetActionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var a=await db.Assets.SingleOrDefaultAsync(x=>x.AssetId==id,ct)??throw new ApiException(404,"ASSET_NOT_FOUND");
        await access.Require(action is "accept" or "reject"?"ASSET_VIEW":action=="retire"?"ASSET_RETIRE":action=="move"?"ASSET_TRANSFER":"ASSET_MANAGE",a.BranchId,ct:ct);
        if(action is "accept" or "reject"){
            // A global read grant is for monitoring. Operational recipients retain
            // their branch read path; asset managers retain their explicit write capability.
            var now=clock.GetUtcNow().UtcDateTime;
            var receiver=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId
                join grant in db.RolePermissions on role.RoleId equals grant.RoleId join p in db.Permissions on grant.PermissionId equals p.PermissionId
                where ur.UserId==actor.Id&&ur.BranchId==a.BranchId&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
                    &&role.IsActive&&role.Code!="OWNER"&&role.Code!="SYS_ADMIN"&&p.IsActive&&p.Code=="ASSET_VIEW"&&(grant.DataScope=="BRANCH"||grant.DataScope=="ALL"||grant.DataScope=="OWN") select ur.UserRoleId).AnyAsync(ct);
            Output.Require(receiver||await access.Allows("ASSET_MANAGE",a.BranchId,ct:ct)||await access.Allows("ASSET_TRANSFER",a.BranchId,ct:ct),"ASSET_RECEIPT_CAPABILITY_REQUIRED",403);
        }
        Output.Version(a.RowVersion,r.RowVersion);Output.Reason(r.Reason);Output.Key(r.OperationKey);Output.Require(a.IsActive,"ASSET_INACTIVE");
        Output.Require(!await db.AssetEvents.AnyAsync(x=>x.AssetId==id&&x.ActorUserId==actor.Id&&x.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var e=new AssetEvent{AssetId=id,BranchId=a.BranchId,EventType=action.ToUpperInvariant(),FromLocationId=a.LocationId,FromRoomId=a.RoomId,FromUserId=a.CustodianUserId,OldCondition=a.ConditionCode,ActorUserId=actor.Id,OperationKey=r.OperationKey,Note=r.Reason,OccurredAt=clock.GetUtcNow().UtcDateTime};
        if(action=="move"){
            Output.Require((r.RoomId!=null)^(r.LocationId!=null),"ONE_LOCATION_REQUIRED",400);
            if(r.RoomId!=null)Output.Require(await db.Rooms.AnyAsync(x=>x.RoomId==r.RoomId&&x.BranchId==a.BranchId&&x.IsActive,ct),"ROOM_UNAVAILABLE");
            if(r.LocationId!=null)Output.Require(await db.Locations.AnyAsync(x=>x.LocationId==r.LocationId&&x.BranchId==a.BranchId&&x.IsActive,ct),"LOCATION_UNAVAILABLE");
            a.RoomId=r.RoomId;a.LocationId=r.LocationId;
        }else if(action=="condition"){
            Output.Require(r.Condition is "GOOD" or "FAIR" or "POOR" or "FAULTY" or "UNDER_REPAIR" or "LOST","INVALID_CONDITION",400);
            if(a.ConditionCode is "FAULTY" or "UNDER_REPAIR" && r.Condition is "GOOD" or "FAIR" or "POOR")
                Output.Require(await db.WorkItems.AnyAsync(w=>w.AssetId==id&&w.Kind=="MAINTENANCE"&&w.Status=="CLOSED"&&w.VerifiedBy!=null,ct),"VERIFIED_REPAIR_REQUIRED");
            a.ConditionCode=r.Condition!;
        }else if(action=="handover"){
            Output.Require(r.UserId!=null&&r.UserId!=a.CustodianUserId,"NEW_CUSTODIAN_REQUIRED",400);await access.Eligible(r.UserId!.Value,a.BranchId,ct);
            await access.EligibleFor(r.UserId.Value,a.BranchId,"ASSET_VIEW",ct);
            var offeredAt=clock.GetUtcNow().UtcDateTime;
            Output.Require(await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId
                join grant in db.RolePermissions on role.RoleId equals grant.RoleId join permission in db.Permissions on grant.PermissionId equals permission.PermissionId
                where ur.UserId==r.UserId&&ur.BranchId==a.BranchId&&ur.AssignedAt<=offeredAt&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>offeredAt)
                    &&role.IsActive&&role.Code!="OWNER"&&role.Code!="SYS_ADMIN"&&permission.IsActive&&permission.Code=="ASSET_VIEW"
                    &&(grant.DataScope=="BRANCH"||grant.DataScope=="ALL"||grant.DataScope=="OWN") select ur.UserRoleId).AnyAsync(ct),"ASSET_RECEIPT_CAPABILITY_REQUIRED",403);
            var last=await db.AssetEvents.Where(x=>x.AssetId==id&&x.HandoverStatus!=null).OrderByDescending(x=>x.AssetEventId).FirstOrDefaultAsync(ct);
            Output.Require(last?.HandoverStatus!="PENDING","HANDOVER_ALREADY_PENDING");e.HandoverStatus="PENDING";e.ToUserId=r.UserId;
        }else if(action is "accept" or "reject"){
            var offer=await db.AssetEvents.Where(x=>x.AssetId==id&&x.HandoverStatus!=null).OrderByDescending(x=>x.AssetEventId).FirstOrDefaultAsync(ct);
            Output.Require(offer?.HandoverStatus=="PENDING"&&offer.ToUserId==actor.Id,"HANDOVER_RECIPIENT_REQUIRED",403);e.HandoverStatus=action=="accept"?"ACCEPTED":"REJECTED";e.ToUserId=actor.Id;
            if(action=="accept"){a.CustodianUserId=actor.Id;e.AcceptedAt=e.OccurredAt;}
        }else if(action=="retire"){
            Output.Require(!await db.WorkItems.AnyAsync(x=>x.AssetId==id&&!WorkRules.Terminal.Contains(x.Status),ct),"ASSET_HAS_OPEN_WORK");a.IsActive=false;a.RetiredAt=e.OccurredAt;a.RetirementReason=r.Reason;
        }else throw new ApiException(400,"UNKNOWN_ACTION");
        e.ToLocationId=a.LocationId;e.ToRoomId=a.RoomId;e.NewCondition=a.ConditionCode;db.AssetEvents.Add(e);db.Entry(a).Property(x=>x.Name).IsModified=true;
        audit.Add("Asset",id,e.EventType,a.BranchId,after:r,key:r.OperationKey);await db.SaveChangesAsync(ct);return (object)Output.Record(a);
    },ct);
}
