using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private sealed record ReusableRemedyData(decimal IssuedQuantity,decimal ReturnedQuantity,bool LossAccountingReviewRequired);

    private async Task<ReusableRemedyData> ReusableRemedy(WorkItem work,IssueData info,CancellationToken ct)
    {
        var issued=await db.ReusableMovements.Where(m=>m.WorkId==work.WorkId&&m.ItemId==info.ItemId&&m.Kind=="REPLACEMENT")
            .SumAsync(m=>(decimal?)m.Quantity,ct)??0;
        var returned=await db.ReusableMovements.Where(m=>m.WorkId==work.WorkId&&m.ItemId==info.ItemId&&m.Kind=="DAMAGE")
            .SumAsync(m=>(decimal?)m.Quantity,ct)??0;
        return new(issued,returned,info.Kind=="MISSING");
    }

    private async Task RequireReusableReportQuantity(long branch,long room,long item,decimal quantity,CancellationToken ct)
    {
        var recorded=await db.ReusablePositions.Where(p=>p.BranchId==branch&&p.RoomId==room&&p.ItemId==item&&p.Status=="IN_USE")
            .SumAsync(p=>(decimal?)p.Quantity,ct)??0;
        var reports=await (from e in db.WorkEvents.AsNoTracking() join w in db.WorkItems on e.WorkId equals w.WorkId
            where w.BranchId==branch&&w.RoomId==room&&e.EventType=="ROOM_ISSUE_REPORTED"&&w.Status!="CANCELLED"
            select new{w.WorkId,w.Status,e.SnapshotJson}).ToArrayAsync(ct);
        decimal committed=0;
        foreach(var report in reports){
            var data=JsonSerializer.Deserialize<IssueData>(report.SnapshotJson??"{}");
            if(data is not{Reusable:true}||data.ItemId!=item||data.DirtySources!=null)continue;
            // Missing units remain a disclosed ledger discrepancy until an approved loss process exists.
            // Keep them reserved even after physical remediation, so they cannot be reported again.
            if(data.Kind=="MISSING")committed+=data.Quantity;
            else if(report.Status!="CLOSED"){
                var received=await db.ReusableMovements.Where(m=>m.WorkId==report.WorkId&&m.ItemId==item&&m.Kind=="DAMAGE")
                    .SumAsync(m=>(decimal?)m.Quantity,ct)??0;
                committed+=Math.Max(0,data.Quantity-received);
            }
        }
        RoomIssueQuantityRules.RequireWithin(quantity,recorded-committed,"ROOM_ISSUE_EXCEEDS_RECORDED_ROOM_QUANTITY");
    }

    private async Task<object> ActReusableIncident(WorkItem work,IssueData info,string? stage,string command,RoomIssueActionRequest request,string payloadHash,CancellationToken ct)
    {
        var store=command is "issue-replacement" or "reject-replacement" or "report-remedy";
        var role=store?"STOREKEEPER":"HOUSEKEEPING_SUPERVISOR";
        await access.RequireRolePermission(role,store?"MR_FULFILL":"IR_PROCESS",work.BranchId,ct);
        await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,role,ct);
        await requests.RequireDomainHandler(work.WorkId,role,["PENDING_STOREKEEPER","PENDING_SUPERVISOR","PENDING_REINSPECTION","REWORK_REQUIRED","ACCEPTED","IN_PROGRESS"],ct);
        await StaffSupplyService.Lock(db,ct);
        var before=work.Status;
        var remedy=await ReusableRemedy(work,info,ct);
        string next,flow,recipient;
        var finished=false;
        switch(command){
            case "issue-replacement":
            case "report-remedy":
                Output.Require(command=="issue-replacement"?stage is "ROOM_ISSUE_REPORTED" or "ROOM_ISSUE_REPLACEMENT_REOPENED":stage=="ROOM_ISSUE_INSPECTION_FAILED","ROOM_ISSUE_WRONG_STAGE");
                if(command=="issue-replacement"){
                    ReusableIncidentRules.RequireFullIssue(info.Quantity,request.Quantity,remedy.IssuedQuantity);
                    await access.Require("STOCK_ISSUE_POST",work.BranchId,ct:ct);
                }else Output.Require(remedy.IssuedQuantity==info.Quantity&&(request.Quantity==null||request.Quantity==0),"REUSABLE_REPLACEMENT_ALREADY_ISSUED");
                ReusableIncidentRules.RequireReturn(info.Kind,info.Quantity,remedy.ReturnedQuantity,request.ReturnedQuantity);
                if(command=="issue-replacement"||request.ReturnedQuantity>0){
                    Output.Require(request.WarehouseId!=null&&await db.Warehouses.AnyAsync(w=>w.WarehouseId==request.WarehouseId&&w.BranchId==work.BranchId&&w.IsActive,ct),"WAREHOUSE_NOT_FOUND",400);
                    var accounting=new ReusableAccounting(db,clock);
                    var room=await accounting.Position(work.BranchId,info.ItemId!.Value,"IN_USE",null,null,work.RoomId,ct);
                    // Receive only physically returned old units, before adding new units to room custody.
                    if(request.ReturnedQuantity>0){
                        var damaged=await accounting.Position(work.BranchId,info.ItemId.Value,"DAMAGED",request.WarehouseId,null,null,ct);
                        await accounting.Move(room,damaged,request.ReturnedQuantity,actor.Id,Guid.NewGuid(),"DAMAGE",request.Note??work.Title,ct,work:work.WorkId);
                    }
                    if(command=="issue-replacement"){
                        var ready=await accounting.Position(work.BranchId,info.ItemId.Value,"READY",request.WarehouseId,null,null,ct);
                        await accounting.Move(ready,room,info.Quantity,actor.Id,Guid.NewGuid(),"REPLACEMENT",request.Note??work.Title,ct,work:work.WorkId);
                    }
                }
                work.Status="RESOLVED";next="INSPECTION_REQUESTED";flow="PENDING_REINSPECTION";recipient="HOUSEKEEPING_SUPERVISOR";
                break;
            case "reject-replacement":
                Output.Require(stage is "ROOM_ISSUE_REPORTED" or "ROOM_ISSUE_REPLACEMENT_REOPENED","ROOM_ISSUE_WRONG_STAGE");Output.Reason(request.Note);
                next="REPLACEMENT_REJECTED";flow="PENDING_SUPERVISOR";recipient="HOUSEKEEPING_SUPERVISOR";break;
            case "approve":
                Output.Require(stage=="ROOM_ISSUE_REPLACEMENT_REJECTED","ROOM_ISSUE_WRONG_STAGE");Output.Reason(request.Note);
                next="REPLACEMENT_REOPENED";flow="PENDING_STOREKEEPER";recipient="STOREKEEPER";break;
            case "reject":
                Output.Require(stage=="ROOM_ISSUE_REPLACEMENT_REJECTED"&&remedy.IssuedQuantity==0,"ROOM_ISSUE_WRONG_STAGE");Output.Reason(request.Note);
                work.Status="CANCELLED";next="REJECTED";flow="REJECTED";recipient="HOUSEKEEPING_SUPERVISOR";finished=true;break;
            case "inspection-pass":
            case "inspection-fail":
                await access.Require("INSPECTION_PERFORM",work.BranchId,ct:ct);
                Output.Require(stage=="ROOM_ISSUE_INSPECTION_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
                Output.Require(work.CreatedBy!=actor.Id&&!await db.ReusableMovements.AnyAsync(m=>m.WorkId==work.WorkId&&m.ActorUserId==actor.Id,ct),"INDEPENDENT_VERIFIER_REQUIRED",403);
                if(command=="inspection-fail"){
                    RequireInspectionNote(request.Note);work.Status="IN_PROGRESS";next="INSPECTION_FAILED";flow="REWORK_REQUIRED";recipient="STOREKEEPER";
                }else{
                    Output.Require(request.RoomRowVersion!=null,"ROOM_VERSION_REQUIRED",400);
                    var room=await db.Rooms.SingleAsync(r=>r.RoomId==work.RoomId&&r.BranchId==work.BranchId,ct);Output.Version(room.RowVersion,request.RoomRowVersion!);
                    Output.Require(remedy.IssuedQuantity==info.Quantity&&(info.Kind=="MISSING"||remedy.ReturnedQuantity==info.Quantity),"REUSABLE_REMEDY_INCOMPLETE");
                    work.Status="CLOSED";work.VerifiedBy=actor.Id;work.VerifiedAt=Now;work.ClosedAt=Now;
                    next="INSPECTION_PASSED";flow="COMPLETED";recipient="HOUSEKEEPING_SUPERVISOR";finished=true;
                }
                break;
            default:throw new ApiException(400,"UNKNOWN_ACTION");
        }
        Event(work,"ROOM_ISSUE_"+next,request.OperationKey,request.Note,before);
        db.ChangeTracker.Entries<WorkEvent>().Single(e=>e.State==EntityState.Added&&e.Entity.WorkId==work.WorkId&&e.Entity.OperationKey==request.OperationKey)
            .Entity.SnapshotJson=JsonSerializer.Serialize(new{PayloadHash=payloadHash,request.Quantity,request.ReturnedQuantity,request.WarehouseId,LossAccountingReviewRequired=info.Kind=="MISSING"});
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        audit.Add("WorkItem",work.WorkId,"ROOM_ISSUE_"+next,work.BranchId,new{status=before,stage},new{work.Status,request.Quantity,request.ReturnedQuantity},request.OperationKey);
        if(!finished)await NotifyIssueRole(work,recipient,"ROOM_ISSUE_"+next,request.OperationKey,ct);
        notifications.Add(work.BranchId,work.CreatedBy,"ROOM_ISSUE_"+next,work.Title,work.WorkId,null,request.OperationKey);
        await db.SaveChangesAsync(ct);
        await requests.RecordDomainTransition(work,"ROOM_DURABLE_ITEM_MISSING",flow,finished?null:recipient,command,role,request.OperationKey,request.Note,ct);
        if(finished){
            if(info.Impact!="SAFETY"||command=="reject")
                foreach(var block in await db.RoomBlocks.Where(b=>b.SourceWorkId==work.WorkId&&b.ReleasedAt==null).ToArrayAsync(ct)){block.ReleasedAt=Now;block.ReleasedBy=actor.Id;}
            await db.SaveChangesAsync(ct);
            await readiness.Reevaluate(work.RoomId!.Value,work.WorkId,request.OperationKey,"REMEDIATION_REVIEWED",ct);
        }
        return Output.Record(work);
    }
}

public static class ReusableIncidentRules
{
    public static void RequireFullIssue(decimal reported,decimal? actual,decimal alreadyIssued)
        =>Output.Require(reported>0&&SupplyMath.ValidQuantity(reported)&&decimal.Truncate(reported)==reported&&actual==reported&&alreadyIssued==0,"REUSABLE_FULL_REPLACEMENT_REQUIRED");
    public static void RequireReturn(string kind,decimal reported,decimal alreadyReturned,decimal actual)
        =>Output.Require(actual>=0&&SupplyMath.ValidQuantity(actual)&&decimal.Truncate(actual)==actual&&alreadyReturned>=0&&alreadyReturned+actual<=reported&&(kind!="MISSING"||actual==0),"REUSABLE_RETURN_QUANTITY_INVALID",400);
}
