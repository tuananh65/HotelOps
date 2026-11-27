using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private sealed record RoomIssueBatchResult(long[] WorkIds,string RoomRowVersion);
    private sealed record RoomIssueBatchReceipt(string PayloadHash,RoomIssueBatchResult Result);

    public Task<object> CreateRoomIssueBatch(RoomIssueBatchRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("IR_CREATE",request.BranchId,true,ct);
        Output.Key(request.OperationKey);await StaffSupplyService.Lock(db,ct);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        var previous=await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a=>a.ActorUserId==actor.Id&&a.OperationKey==request.OperationKey&&a.Action=="CREATE_ROOM_ISSUE_BATCH",ct);
        if(previous!=null){
            var receipt=JsonSerializer.Deserialize<RoomIssueBatchReceipt>(previous.AfterJson!)!;
            Output.Require(receipt.PayloadHash==hash,"IDEMPOTENCY_KEY_REUSED");return (object)receipt.Result;
        }
        Output.Require(request.Lines is {Length:>=1 and <=50},"ROOM_ISSUE_BATCH_SIZE_INVALID",400);
        Output.Require(request.Lines.All(l=>l!=null&&(l.ItemId!=null)!=(l.AssetId!=null)&&l.Quantity>0&&SupplyMath.ValidQuantity(l.Quantity)),"ROOM_ISSUE_OBJECT_REQUIRED",400);
        Output.Require(request.Lines.Select(l=>(l.ItemId,l.AssetId)).Distinct().Count()==request.Lines.Length,"ROOM_ISSUE_DUPLICATE_OBJECT",400);
        var room=await db.Rooms.SingleOrDefaultAsync(r=>r.RoomId==request.RoomId&&r.BranchId==request.BranchId&&r.IsActive,ct)??throw new ApiException(404,"ROOM_NOT_FOUND");
        Output.Version(room.RowVersion,request.RoomRowVersion);
        var ids=new List<long>();
        for(var index=0;index<request.Lines.Length;index++){
            var line=request.Lines[index];
            var childKey=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"room-issue-batch:{request.OperationKey:D}:{index}")).AsSpan(0,16));
            await CreateRoomIssueCore(new(request.BranchId,request.RoomId,request.TypeId,line.AssetId,line.ItemId,
                request.RoomRowVersion,childKey,request.Note,line.Quantity,request.Impact,request.CleaningWorkId,request.DetectedByUserId),ct,checkRoomVersion:false);
            ids.Add(await db.WorkEvents.Where(e=>e.ActorUserId==actor.Id&&e.OperationKey==childKey&&e.EventType=="ROOM_ISSUE_REPORTED").Select(e=>e.WorkId).SingleAsync(ct));
        }
        var result=new RoomIssueBatchResult(ids.ToArray(),Convert.ToBase64String(room.RowVersion));
        audit.Add("Room",room.RoomId,"CREATE_ROOM_ISSUE_BATCH",room.BranchId,after:new RoomIssueBatchReceipt(hash,result),key:request.OperationKey);
        await db.SaveChangesAsync(ct);
        return (object)result;
    },ct);

    private async Task<long> MissingSourceCleaning(long branch,long room,long? context,CancellationToken ct)
    {
        var work=await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w=>w.WorkId==context&&w.BranchId==branch&&w.RoomId==room,ct);
        Output.Require(work!=null,"MISSING_SOURCE_CLEANING_REQUIRED",409);
        var root=work!.Kind=="CLEANING"?work.WorkId:work.ParentWorkId;
        Output.Require(root!=null&&await db.WorkItems.AnyAsync(w=>w.WorkId==root&&w.BranchId==branch&&w.RoomId==room&&w.Kind=="CLEANING"&&w.TurnoverNo==work.TurnoverNo,ct),"MISSING_SOURCE_CLEANING_REQUIRED",409);
        return root!.Value;
    }

    private async Task RequireMissingVerifier(WorkItem work,IssueData info,CancellationToken ct)
    {
        await RequireRoomIssueSupervisor(work.BranchId,ct);
        await access.Require("INSPECTION_PERFORM",work.BranchId,ct:ct);
        await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
        Output.Require(work.CreatedBy!=actor.Id&&info.DetectedByUserId!=actor.Id,"INDEPENDENT_VERIFIER_REQUIRED",403);
        if(info.CleaningWorkId!=null){
            var root=await MissingSourceCleaning(work.BranchId,work.RoomId!.Value,info.CleaningWorkId,ct);
            Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==actor.Id&&(a.WorkId==root||db.WorkItems.Any(w=>w.WorkId==a.WorkId&&w.ParentWorkId==root&&w.Kind=="REWORK")),ct),"INSPECTOR_MUST_BE_INDEPENDENT",403);
        }
        await requests.RequireDomainHandler(work.WorkId,"HOUSEKEEPING_SUPERVISOR",["PENDING_SUPERVISOR","ACCEPTED","REWORK_REQUIRED"],ct);
    }

    private async Task<object> ActVerifiedMissing(WorkItem work,IssueData info,string? stage,string command,RoomIssueActionRequest request,string hash,CancellationToken ct)
    {
        if(work.AssetId!=null&&command is "record-delivery" or "inspection-pass" or "inspection-fail")
            return await ActMissingAssetDelivery(work,info,stage,command,request,hash,ct);
        var purchase=command is "approve-purchase" or "reject-purchase";
        Output.Reason(request.Note);
        var before=work.Status;string next;var finished=false;
        if(purchase){
            Output.Require(work.AssetId!=null&&info.Kind=="MISSING"&&stage=="ROOM_ISSUE_ASSET_PURCHASE_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
            await access.RequireRolePermission("OPERATION_MANAGER","ASSET_MANAGE",work.BranchId,ct);
            var verifier=await db.WorkEvents.Where(e=>e.WorkId==work.WorkId&&e.EventType=="ROOM_ISSUE_ASSET_PURCHASE_REQUESTED").OrderByDescending(e=>e.WorkEventId).Select(e=>e.ActorUserId).FirstAsync(ct);
            Output.Require(work.CreatedBy!=actor.Id&&verifier!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);
            next=command=="approve-purchase"?"ASSET_PURCHASE_APPROVED":"ASSET_PURCHASE_REJECTED";
        }else{
            Output.Require(command is "approve" or "reject","UNKNOWN_ACTION",400);
            Output.Require(stage=="ROOM_ISSUE_REPORTED"||work.AssetId!=null&&stage=="ROOM_ISSUE_ASSET_PURCHASE_REJECTED","ROOM_ISSUE_WRONG_STAGE");
            await RequireMissingVerifier(work,info,ct);
            if(command=="reject"){
                work.Status="CANCELLED";work.ClosedAt=Now;next="REJECTED";finished=true;
            }else{
                Output.Require(request.RoomRowVersion!=null,"ROOM_VERSION_REQUIRED",400);
                var room=await db.Rooms.SingleAsync(r=>r.RoomId==work.RoomId&&r.BranchId==work.BranchId,ct);
                Output.Version(room.RowVersion,request.RoomRowVersion!);
                if(info.DirtySources!=null){
                    var accounting=new ReusableAccounting(db,clock);
                    foreach(var group in info.DirtySources.GroupBy(s=>s.PositionId)){
                        var source=await db.ReusablePositions.SingleAsync(p=>p.PositionId==group.Key&&p.BranchId==work.BranchId&&p.ItemId==info.ItemId,ct);
                        Output.Require(source.Status=="DIRTY"&&source.Quantity>=group.Sum(s=>s.Quantity),"MISSING_USED_CUSTODY_NOT_AVAILABLE",409);
                        var target=await accounting.Position(work.BranchId,source.ItemId,"WRITTEN_OFF",source.WarehouseId,source.UserId,source.RoomId,ct);
                        await accounting.Move(source,target,group.Sum(s=>s.Quantity),actor.Id,Guid.NewGuid(),"WRITE_OFF",request.Note!,ct,work:work.WorkId);
                    }
                    work.Status="CLOSED";work.ClosedAt=Now;next="MISSING_USED_VERIFIED";finished=true;
                }else{
                    Output.Require(work.AssetId!=null,"MISSING_ASSET_REQUIRED");
                    work.Status="IN_PROGRESS";next="ASSET_PURCHASE_REQUESTED";
                }
                work.VerifiedBy=actor.Id;work.VerifiedAt=Now;
            }
        }
        Event(work,"ROOM_ISSUE_"+next,request.OperationKey,request.Note,before);
        db.WorkEvents.Local.Single(e=>e.WorkId==work.WorkId&&e.OperationKey==request.OperationKey&&e.EventType=="ROOM_ISSUE_"+next).SnapshotJson=
            JsonSerializer.Serialize(new{PayloadHash=hash,info.CleaningWorkId,info.DirtySources,work.AssetId,info.Quantity});
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        audit.Add("WorkItem",work.WorkId,"ROOM_ISSUE_"+next,work.BranchId,new{Status=before},new{work.Status,info.DirtySources,work.AssetId},request.OperationKey);
        var recipient=next=="ASSET_PURCHASE_REQUESTED"?"OPERATION_MANAGER":"HOUSEKEEPING_SUPERVISOR";
        if(!finished)await NotifyIssueRole(work,recipient,"ROOM_ISSUE_"+next,request.OperationKey,ct);
        notifications.Add(work.BranchId,work.CreatedBy,"ROOM_ISSUE_"+next,work.Title,work.WorkId,null,request.OperationKey);
        await db.SaveChangesAsync(ct);
        // Asset purchase uses a dedicated event-backed OM inbox. The existing SQL constraint
        // deliberately has no OM state for room requests; do not fabricate maintenance work.
        if(!purchase)await requests.RecordDomainTransition(work,"ROOM_DURABLE_ITEM_MISSING",finished?(command=="reject"?"REJECTED":"COMPLETED"):"ACCEPTED",
            finished?null:"HOUSEKEEPING_SUPERVISOR",command,"HOUSEKEEPING_SUPERVISOR",request.OperationKey,request.Note,ct);
        if(finished){
            if(info.Impact!="SAFETY"||command=="reject")
                foreach(var block in await db.RoomBlocks.Where(b=>b.SourceWorkId==work.WorkId&&b.ReleasedAt==null).ToArrayAsync(ct)){block.ReleasedAt=Now;block.ReleasedBy=actor.Id;}
            await db.SaveChangesAsync(ct);
            await ReturnReviewedDirty(info,work.BranchId,ct);
            await readiness.Reevaluate(work.RoomId!.Value,work.WorkId,request.OperationKey,"REMEDIATION_REVIEWED",ct);
        }
        return Output.Record(work);
    }

    private sealed record MissingAssetDelivery(long ReplacementAssetId);

    private async Task<long?> DeliveredMissingAsset(long id,CancellationToken ct)
    {
        var json=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==id&&e.EventType=="ROOM_ISSUE_ASSET_DELIVERED")
            .OrderByDescending(e=>e.WorkEventId).Select(e=>e.SnapshotJson).FirstOrDefaultAsync(ct);
        return json==null?null:JsonSerializer.Deserialize<MissingAssetDelivery>(json)?.ReplacementAssetId;
    }

    private async Task<long[]> ClaimedMissingReplacements(long exceptWork,CancellationToken ct)
    {
        var events=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId!=exceptWork&&e.EventType=="ROOM_ISSUE_ASSET_DELIVERED"
            &&db.WorkItems.Any(w=>w.WorkId==e.WorkId&&w.Status!="CANCELLED"))
            .Select(e=>new{e.WorkId,e.WorkEventId,e.SnapshotJson}).ToArrayAsync(ct);
        return events.GroupBy(e=>e.WorkId).Select(g=>JsonSerializer.Deserialize<MissingAssetDelivery>(g.MaxBy(e=>e.WorkEventId)!.SnapshotJson!)!.ReplacementAssetId).Distinct().ToArray();
    }

    private async Task ValidateMissingReplacement(WorkItem work,long replacementId,CancellationToken ct)
    {
        var missing=await db.Assets.AsNoTracking().SingleAsync(a=>a.AssetId==work.AssetId&&a.BranchId==work.BranchId,ct);
        Output.Require(missing.RoomId!=work.RoomId,"MISSING_ASSET_STILL_ASSIGNED_TO_ROOM");
        Output.Require(missing.AssetTypeId!=null&&await db.Assets.AnyAsync(a=>a.AssetId==replacementId&&a.AssetId!=missing.AssetId
            &&a.BranchId==work.BranchId&&a.RoomId==work.RoomId&&a.AssetTypeId==missing.AssetTypeId&&a.IsActive&&a.RetiredAt==null
            &&(a.ConditionCode=="GOOD"||a.ConditionCode=="FAIR")),"INVALID_REPLACEMENT_ASSET");
        Output.Require(!(await ClaimedMissingReplacements(work.WorkId,ct)).Contains(replacementId),"REPLACEMENT_ASSET_ALREADY_USED");
        Output.Require(!await db.WorkItems.AnyAsync(w=>w.AssetId==replacementId&&w.Kind=="INCIDENT"&&w.Status!="CANCELLED"&&w.Status!="CLOSED"),"REPLACEMENT_ASSET_HAS_OPEN_INCIDENT");
    }

    private async Task<object> ActMissingAssetDelivery(WorkItem work,IssueData info,string? stage,string command,RoomIssueActionRequest request,string hash,CancellationToken ct)
    {
        Output.Reason(request.Note);
        var delivery=command=="record-delivery";
        long replacementId;
        if(delivery){
            await access.RequireRolePermission("OPERATION_MANAGER","ASSET_MANAGE",work.BranchId,ct);
            Output.Require(stage is "ROOM_ISSUE_ASSET_PURCHASE_APPROVED" or "ROOM_ISSUE_ASSET_DELIVERY_REJECTED","ROOM_ISSUE_WRONG_STAGE");
            Output.Require(request.ReplacementAssetId!=null,"REPLACEMENT_ASSET_REQUIRED",400);
            replacementId=request.ReplacementAssetId!.Value;
        }else{
            await RequireMissingVerifier(work,info,ct);
            Output.Require(stage=="ROOM_ISSUE_ASSET_DELIVERED","ROOM_ISSUE_WRONG_STAGE");
            var deliverer=await db.WorkEvents.Where(e=>e.WorkId==work.WorkId&&e.EventType=="ROOM_ISSUE_ASSET_DELIVERED")
                .OrderByDescending(e=>e.WorkEventId).Select(e=>e.ActorUserId).FirstAsync(ct);
            Output.Require(deliverer!=actor.Id,"INDEPENDENT_VERIFIER_REQUIRED",403);
            replacementId=(await DeliveredMissingAsset(work.WorkId,ct))??throw new ApiException(409,"REPLACEMENT_ASSET_REQUIRED");
        }
        Output.Require(request.RoomRowVersion!=null,"ROOM_VERSION_REQUIRED",400);
        var room=await db.Rooms.SingleAsync(r=>r.RoomId==work.RoomId&&r.BranchId==work.BranchId,ct);
        Output.Version(room.RowVersion,request.RoomRowVersion!);
        if(command!="inspection-fail")await ValidateMissingReplacement(work,replacementId,ct);
        var before=work.Status;var passed=command=="inspection-pass";
        var next=delivery?"ASSET_DELIVERED":passed?"INSPECTION_PASSED":"ASSET_DELIVERY_REJECTED";
        if(passed){work.Status="CLOSED";work.VerifiedBy=actor.Id;work.VerifiedAt=Now;work.ClosedAt=Now;}
        Event(work,"ROOM_ISSUE_"+next,request.OperationKey,request.Note,before);
        db.WorkEvents.Local.Single(e=>e.WorkId==work.WorkId&&e.OperationKey==request.OperationKey).SnapshotJson=
            JsonSerializer.Serialize(new{PayloadHash=hash,ReplacementAssetId=replacementId});
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        audit.Add("WorkItem",work.WorkId,"ROOM_ISSUE_"+next,work.BranchId,new{Status=before,stage},new{work.Status,ReplacementAssetId=replacementId},request.OperationKey);
        await NotifyIssueRole(work,command=="inspection-fail"?"OPERATION_MANAGER":"HOUSEKEEPING_SUPERVISOR","ROOM_ISSUE_"+next,request.OperationKey,ct);
        notifications.Add(work.BranchId,work.CreatedBy,"ROOM_ISSUE_"+next,work.Title,work.WorkId,null,request.OperationKey);
        await db.SaveChangesAsync(ct);
        if(!delivery)await requests.RecordDomainTransition(work,"ROOM_DURABLE_ITEM_MISSING",passed?"COMPLETED":"ACCEPTED",
            passed?null:"HOUSEKEEPING_SUPERVISOR",command,"HOUSEKEEPING_SUPERVISOR",request.OperationKey,request.Note,ct);
        if(passed){
            if(info.Impact!="SAFETY")
                foreach(var block in await db.RoomBlocks.Where(b=>b.SourceWorkId==work.WorkId&&b.ReleasedAt==null).ToArrayAsync(ct)){block.ReleasedAt=Now;block.ReleasedBy=actor.Id;}
            await db.SaveChangesAsync(ct);
            await CompleteCleaningAfterRemediation(work,info.CleaningWorkId,request,ct);
            await readiness.Reevaluate(work.RoomId!.Value,work.WorkId,request.OperationKey,"REMEDIATION_REVIEWED",ct);
        }
        return Output.Record(work);
    }

    private async Task ReturnReviewedDirty(IssueData info,long branch,CancellationToken ct)
    {
        foreach(var id in (info.DirtySources??[]).Select(s=>s.PositionId).Distinct()){
            var source=await db.ReusablePositions.SingleAsync(p=>p.PositionId==id,ct);
            if(source.Quantity<=0||source.UserId==null||await ReusableMissingCustody.HasPending(db,id,ct))continue;
            var latest=await db.StaffSupplyAllocations.Where(a=>a.BranchId==branch&&a.UserId==source.UserId).OrderByDescending(a=>a.AllocationId).FirstOrDefaultAsync(ct);
            if(latest?.Status!="CLOSED")continue;
            var warehouses=await db.Warehouses.Where(w=>w.BranchId==branch&&w.IsActive).Select(w=>w.WarehouseId).ToArrayAsync(ct);
            Output.Require(warehouses.Length==1,"BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE");
            var accounting=new ReusableAccounting(db,clock);
            var destination=await accounting.Position(branch,source.ItemId,"DIRTY",warehouses[0],null,null,ct);
            await accounting.Move(source,destination,source.Quantity,actor.Id,Guid.NewGuid(),"HANDOVER","Hoàn trả đồ đã dùng sau xác minh báo thiếu",ct);
        }
    }

    public async Task<object> MissingAssetPurchases(long branch,CancellationToken ct)
    {
        await access.RequireRolePermission("OPERATION_MANAGER","ASSET_MANAGE",branch,ct);
        return await db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branch&&w.Kind=="INCIDENT"&&w.AssetId!=null
            &&db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="ROOM_ISSUE_ASSET_PURCHASE_REQUESTED"))
            .OrderByDescending(w=>w.WorkId).Select(w=>new{w.WorkId,w.Title,w.AssetId,w.RoomId,w.CreatedBy,
                updatedAt=db.WorkEvents.Where(e=>e.WorkId==w.WorkId).Max(e=>(DateTime?)e.OccurredAt)??w.CreatedAt,
                status=db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&e.EventType.StartsWith("ROOM_ISSUE_")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType).FirstOrDefault(),
                rowVersion=Convert.ToBase64String(w.RowVersion)}).ToArrayAsync(ct);
    }
}
