using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<object> InstallReplacementByManager(WorkItem failed,ReplacementSnapshot? snapshot,string state,ReplacementRequest request,CancellationToken ct)
    {
        await access.RequireRolePermission("OPERATION_MANAGER","ASSET_MANAGE",failed.BranchId,ct);
        Output.Require(state=="REPLACEMENT_APPROVED"&&snapshot!=null&&await IsUnrepairable(failed.WorkId,ct),"REPLACEMENT_WRONG_STAGE");
        var original=await db.Assets.SingleAsync(a=>a.AssetId==snapshot!.OriginalAssetId&&a.BranchId==failed.BranchId,ct);
        var inplace=request.ReplacementAssetId==null;
        var replacement=inplace?original:await db.Assets.SingleOrDefaultAsync(a=>a.AssetId==request.ReplacementAssetId&&a.BranchId==failed.BranchId&&a.IsActive&&a.RetiredAt==null,ct);
        var room=snapshot!.RoomId??failed.RoomId;var location=snapshot.LocationId??failed.LocationId;
        Output.Require(room!=null||location!=null,"ASSET_LOCATION_REQUIRED");
        Output.Require(replacement!=null&&replacement.IsActive&&replacement.RetiredAt==null&&(inplace||replacement.ConditionCode is "GOOD" or "FAIR"),"REPLACEMENT_ASSET_NOT_INSTALLED");
        Output.Require(original.AssetTypeId==null||replacement!.AssetTypeId==original.AssetTypeId,"REPLACEMENT_ASSET_TYPE_MISMATCH");
        Output.Require((replacement!.RoomId==null&&replacement.LocationId==null)||(room!=null?replacement.RoomId==room:replacement.LocationId==location),"ASSET_LOCATION_CHANGED");
        Output.Require((original.RoomId==null&&original.LocationId==null)||(room!=null?original.RoomId==room:original.LocationId==location),"ASSET_LOCATION_CHANGED");
        Output.Require(!await db.WorkItems.AnyAsync(w=>w.AssetId==replacement.AssetId&&!WorkRules.Terminal.Contains(w.Status)
            &&(!inplace||w.WorkId!=failed.ParentWorkId&&w.ParentWorkId!=failed.ParentWorkId)),"ASSET_HAS_OPEN_WORK");
        var note="OM xác nhận đã thay mới thiết bị tại vị trí sử dụng.";
        var existing=await (from link in db.WorkLinks join w in db.WorkItems on link.SourceWorkId equals w.WorkId
            where link.TargetWorkId==failed.WorkId&&link.Relation=="SOURCE"&&w.Kind=="MAINTENANCE"&&w.Status!="CANCELLED"
            select w).SingleOrDefaultAsync(ct);
        Output.Require(existing==null||existing.Status=="PENDING"&&!await db.WorkAssignments.AnyAsync(a=>a.WorkId==existing.WorkId&&a.AssignmentStatus=="ACTIVE",ct),"MAINTENANCE_ALREADY_ACTIVE");
        var followupId=existing?.WorkId??await CreateReplacementFollowup(failed,snapshot,request with{Reason=note},ct);
        var installed=await db.WorkItems.SingleAsync(w=>w.WorkId==followupId,ct);
        if(!inplace)db.AssetEvents.AddRange(
            new AssetEvent{BranchId=failed.BranchId,AssetId=original.AssetId,EventType="REPLACEMENT_OUT",SourceWorkId=followupId,FromRoomId=original.RoomId,FromLocationId=original.LocationId,ActorUserId=actor.Id,OperationKey=request.OperationKey,OccurredAt=Now,Note=note},
            new AssetEvent{BranchId=failed.BranchId,AssetId=replacement.AssetId,EventType="REPLACEMENT_IN",SourceWorkId=followupId,FromRoomId=replacement.RoomId,FromLocationId=replacement.LocationId,ToRoomId=room,ToLocationId=room==null?location:null,ActorUserId=actor.Id,OperationKey=request.OperationKey,OccurredAt=Now,Note=note});
        if(!inplace){original.RoomId=null;original.LocationId=null;}
        else db.AssetEvents.Add(new AssetEvent{BranchId=failed.BranchId,AssetId=original.AssetId,EventType="REPLACEMENT_IN",SourceWorkId=followupId,
            FromRoomId=original.RoomId,ToRoomId=room,FromLocationId=original.LocationId,ToLocationId=room==null?location:null,
            OldCondition=original.ConditionCode,NewCondition="GOOD",ActorUserId=actor.Id,OperationKey=request.OperationKey,OccurredAt=Now,Note=note});
        replacement.RoomId=room;replacement.LocationId=room==null?location:null;
        snapshot=snapshot with{ReplacementAssetId=request.ReplacementAssetId};
        installed.AssetId=replacement.AssetId;installed.Status="COMPLETED";installed.Result="PASS";installed.CompletedAt=Now;installed.ProgressPercent=100;installed.CompletionNote=note;
        Event(failed,"REPLACEMENT_INSTALLED",request.OperationKey,note,failed.Status);
        db.WorkEvents.Local.Single(e=>e.WorkId==failed.WorkId&&e.OperationKey==request.OperationKey).SnapshotJson=JsonSerializer.Serialize(snapshot);
        db.Entry(failed).Property(w=>w.Title).IsModified=true;
        Event(installed,"REPLACEMENT_INSTALLED",Guid.NewGuid(),note,"PENDING");
        db.WorkEvents.Local.Last(e=>e.WorkId==followupId&&e.EventType=="REPLACEMENT_INSTALLED").SnapshotJson=JsonSerializer.Serialize(snapshot);
        audit.Add("WorkItem",failed.WorkId,"REPLACEMENT_INSTALLED",failed.BranchId,after:snapshot,key:request.OperationKey);
        await db.SaveChangesAsync(ct);
        await MaintenanceSucceeded(installed,new(Convert.ToBase64String(installed.RowVersion),Guid.NewGuid(),note),ct,"OPERATION_MANAGER");
        return new{workId=failed.WorkId,followupWorkId=followupId,state="REPLACEMENT_INSTALLED",rowVersion=Convert.ToBase64String(failed.RowVersion)};
    }
}
