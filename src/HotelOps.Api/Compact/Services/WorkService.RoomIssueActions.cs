using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    public Task<object> ActRoomIssue(long id,string command,RoomIssueActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var work=await LoadRoomIssue(id,ct);
        Output.Require(!await access.HasRole("OPERATION_MANAGER",work.BranchId,ct)&&!await access.HasRole("OWNER",work.BranchId,ct),"REQUEST_READ_ONLY",403);
        Output.Key(request.OperationKey);
        var payloadHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new{command,request}))));
        var prior=await db.WorkEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(prior!=null){
            using var snapshot=System.Text.Json.JsonDocument.Parse(prior.SnapshotJson??"{}");
            Output.Require(snapshot.RootElement.TryGetProperty("PayloadHash",out var hash)&&hash.GetString()==payloadHash,"IDEMPOTENCY_KEY_REUSED");
            return (object)Output.Record(work);
        }
        Output.Version(work.RowVersion,request.RowVersion);Output.Key(request.OperationKey);
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var stage=await IssueStage(id,ct);var info=await IssueInfo(id,ct);var before=work.Status;
        var expectedRole=command is "create-repair" or "start-repair"||command=="report-remedy"&&info.Kind=="BROKEN"?"MAINTENANCE_SUPERVISOR":command=="report-remedy"?"STOREKEEPER":"HOUSEKEEPING_SUPERVISOR";
        // Older fully posted deliveries did not record the warehouse handoff. An
        // on-duty supervisor can receive these for inspection from delivery evidence.
        var deliveredLegacy=command=="request-inspection"&&info.Kind=="MISSING"&&stage=="ROOM_ISSUE_MATERIAL_REQUESTED";
        if(deliveredLegacy){
            await StaffSupplyService.Lock(db,ct);
            await RequireRoomIssueSupervisor(work.BranchId,ct);
            await access.Require("INSPECTION_PERFORM",work.BranchId,ct:ct);
            await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
            Output.Require(await MaterialDelivered(await CurrentIssueMaterial(id,ct),ct),"ROOM_ISSUE_MATERIAL_NOT_FULFILLED");
        }else await requests.RequireDomainHandler(id,expectedRole,new[]{"PENDING_SUPERVISOR","PENDING_STOREKEEPER","PENDING_MAINTENANCE_SUPERVISOR","ACCEPTED","IN_PROGRESS","PENDING_REINSPECTION","REWORK_REQUIRED"},ct);
        string next;string recipient;long? materialId=null;var finished=false;
        switch(command){
            case "approve":
                await RequireRoomIssueSupervisor(work.BranchId,ct);
                if(info.CleaningWorkId!=null){
                    var cleaning=await db.WorkItems.SingleAsync(w=>w.WorkId==info.CleaningWorkId&&w.BranchId==work.BranchId,ct);
                    Output.Require(cleaning.Kind=="INSPECTION"?cleaning.Status=="INSPECTING":cleaning.Status is "PENDING_INSPECTION" or "PENDING_REINSPECTION" or "REWORK_REQUIRED","CLEANING_NOT_READY_FOR_REVIEW");
                    var cleaningRoot=cleaning.Kind=="CLEANING"?cleaning.WorkId:cleaning.ParentWorkId;
                    Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==actor.Id&&(a.WorkId==cleaningRoot||db.WorkItems.Any(w=>w.WorkId==a.WorkId&&w.ParentWorkId==cleaningRoot&&w.Kind=="REWORK")),ct),"INSPECTOR_MUST_BE_INDEPENDENT",403);
                }
                Output.Require(stage is "ROOM_ISSUE_REPORTED" or "ROOM_ISSUE_INSPECTION_FAILED","ROOM_ISSUE_WRONG_STAGE");
                Output.Reason(request.Note);
                work.Status="IN_PROGRESS";
                next=info.Kind=="BROKEN"?"REPAIR_REQUESTED":"APPROVED";
                recipient=info.Kind=="BROKEN"?"MAINTENANCE_SUPERVISOR":"HOUSEKEEPING_SUPERVISOR";break;
            case "reject":
                await RequireRoomIssueSupervisor(work.BranchId,ct);Output.Reason(request.Note);
                Output.Require(stage=="ROOM_ISSUE_REPORTED","ROOM_ISSUE_WRONG_STAGE");
                work.Status="CANCELLED";work.ClosedAt=Now;next="REJECTED";recipient="HOUSEKEEPING_SUPERVISOR";finished=true;break;
            case "send-repair": // Compatibility for a report approved before P0 cutover.
                await RequireRoomIssueSupervisor(work.BranchId,ct);
                Output.Require(stage=="ROOM_ISSUE_APPROVED"&&info.Kind=="BROKEN","ROOM_ISSUE_WRONG_STAGE");
                next="REPAIR_REQUESTED";recipient="MAINTENANCE_SUPERVISOR";break;
            case "create-repair":
                await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_EXECUTE",work.BranchId,ct);
                Output.Require(stage=="ROOM_ISSUE_REPAIR_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
                Output.Require(!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE"&&w.Status!="CLOSED"&&w.Status!="CANCELLED",ct),"MAINTENANCE_ALREADY_ACTIVE");
                var title=$"Khắc phục · {work.Title}";
                var repair=new WorkItem{BranchId=work.BranchId,Kind="MAINTENANCE",Status="PENDING",WorkNo=Guid.NewGuid().ToString("N"),
                    Title=title[..Math.Min(250,title.Length)],RoomId=work.RoomId,TurnoverNo=work.TurnoverNo,AssetId=work.AssetId,ParentWorkId=id,CreatedBy=actor.Id,CreatedAt=Now};
                db.WorkItems.Add(repair);await db.SaveChangesAsync(ct);
                db.WorkAssignments.Add(new WorkAssignment{WorkId=repair.WorkId,BranchId=work.BranchId,AssigneeUserId=actor.Id,AssignedBy=actor.Id,AssignedAt=Now,AcceptedAt=Now,AssignmentStatus="ACTIVE"});
                Event(repair,"EXTERNAL_REPAIR_ACCEPTED",request.OperationKey,request.Note,null);
                await db.SaveChangesAsync(ct);
                await ActCore(repair.WorkId,"start",new ActionRequest(Convert.ToBase64String(repair.RowVersion),Guid.NewGuid(),null),ct,roomWorkflow:true);
                next="REPAIR_CREATED";recipient="MAINTENANCE_SUPERVISOR";break;
            case "start-repair":
                await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_EXECUTE",work.BranchId,ct);
                Output.Require(stage=="ROOM_ISSUE_REPAIR_CREATED","ROOM_ISSUE_WRONG_STAGE");
                var starting=await CurrentRepair(work,ct);
                await ActCore(starting.WorkId,"start",new ActionRequest(Convert.ToBase64String(starting.RowVersion),request.OperationKey,request.Note),ct,roomWorkflow:true);
                next="REPAIR_CREATED";recipient="MAINTENANCE_SUPERVISOR";break;
            case "request-material":
                await RequireRoomIssueSupervisor(work.BranchId,ct);
                Output.Require(info.Kind=="MISSING"&&(stage is "ROOM_ISSUE_APPROVED" or "ROOM_ISSUE_INSPECTION_FAILED"
                    ||stage=="ROOM_ISSUE_MATERIAL_REQUESTED"&&await CanReplaceMaterial(id,ct)),"ROOM_ISSUE_WRONG_STAGE");
                Output.Require(request.ItemId!=null&&request.UnitId!=null&&request.Quantity>0&&request.WarehouseId!=null,"ROOM_ISSUE_MATERIAL_REQUIRED",400);
                Output.Require(info.ItemId==request.ItemId,"ROOM_ISSUE_OBJECT_NOT_IN_ROOM");
                // Each failed inspection requires an explicit new outstanding quantity, never an automatic full reissue.
                materialId=await documents.CreateRoomRemediation(work,request,ct);
                next="MATERIAL_REQUESTED";recipient="STOREKEEPER";break;
            case "report-remedy":
                Output.Reason(request.Note);
                if(info.Kind=="BROKEN"){
                    await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_EXECUTE",work.BranchId,ct);
                    Output.Require(stage=="ROOM_ISSUE_REPAIR_CREATED","ROOM_ISSUE_WRONG_STAGE");
                    var completed=await CurrentRepair(work,ct);
                    if(completed.Status=="IN_PROGRESS")await ActCore(completed.WorkId,"complete",new ActionRequest(Convert.ToBase64String(completed.RowVersion),request.OperationKey,request.Note),ct,roomWorkflow:true);
                    Output.Require(completed.Status=="COMPLETED"&&await db.WorkAssignments.AnyAsync(a=>a.WorkId==completed.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct),"REPAIR_COMPLETION_REQUIRED",403);
                }else{
                    await access.RequireRolePermission("STOREKEEPER","MR_FULFILL",work.BranchId,ct);
                    Output.Require(stage=="ROOM_ISSUE_MATERIAL_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
                    var latestMaterial=await CurrentIssueMaterial(id,ct);
                    Output.Require(request.DocumentId==latestMaterial&&await MaterialDelivered(latestMaterial,ct),"ROOM_ISSUE_MATERIAL_NOT_FULFILLED");
                }
                work.Status="RESOLVED";next="INSPECTION_REQUESTED";recipient="HOUSEKEEPING_SUPERVISOR";break;
            case "request-inspection": // Legacy reports awaiting the former head's relay.
                await RequireRoomIssueSupervisor(work.BranchId,ct);
                Output.Require(stage=="ROOM_ISSUE_REMEDY_REPORTED"||deliveredLegacy,"ROOM_ISSUE_WRONG_STAGE");
                if(deliveredLegacy)work.Status="RESOLVED";
                next="INSPECTION_REQUESTED";recipient="HOUSEKEEPING_SUPERVISOR";break;
            case "inspection-fail":
            case "inspection-pass":
                await RequireRoomIssueSupervisor(work.BranchId,ct);await access.Require("INSPECTION_PERFORM",work.BranchId,ct:ct);
                Output.Require(stage=="ROOM_ISSUE_INSPECTION_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
                Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==actor.Id&&db.WorkItems.Any(w=>w.WorkId==a.WorkId&&w.ParentWorkId==id&&w.Kind=="MAINTENANCE"),ct),"INDEPENDENT_VERIFIER_REQUIRED",403);
                if(command=="inspection-fail"){
                    RequireInspectionNote(request.Note);work.Status="IN_PROGRESS";
                    if(info.Kind=="BROKEN"){
                        var rework=await CurrentRepair(work,ct);Output.Require(rework.Status=="COMPLETED","REPAIR_COMPLETION_REQUIRED");
                        Event(rework,"REPAIR_REINSPECTION_FAILED",request.OperationKey,request.Note,rework.Status);
                        db.WorkEvents.Local.Last(e=>e.WorkId==rework.WorkId).SnapshotJson=System.Text.Json.JsonSerializer.Serialize(new{rework.CompletedAt,rework.CompletionNote,rework.ProgressPercent});
                        rework.Status="PENDING";rework.CompletedAt=null;rework.ProgressPercent=0;
                        next="REPAIR_CREATED";recipient="MAINTENANCE_SUPERVISOR";
                    }else{next="INSPECTION_FAILED";recipient="HOUSEKEEPING_SUPERVISOR";}
                }else{
                    Output.Require(request.RoomRowVersion!=null,"ROOM_VERSION_REQUIRED",400);
                    var room=await db.Rooms.SingleAsync(r=>r.RoomId==work.RoomId,ct);Output.Version(room.RowVersion,request.RoomRowVersion!);
                    if(info.Kind=="MISSING")Output.Require(await MaterialDelivered(await CurrentIssueMaterial(id,ct),ct),"ROOM_ISSUE_MATERIAL_NOT_FULFILLED");
                    else{
                        var verified=await CurrentRepair(work,ct);Output.Require(verified.Status=="COMPLETED","REPAIR_COMPLETION_REQUIRED");
                        await EndAssignments(verified,Now,ct);verified.Status="CLOSED";verified.VerifiedBy=actor.Id;verified.VerifiedAt=Now;verified.ClosedAt=Now;
                        Event(verified,"VERIFY",request.OperationKey,request.Note,"COMPLETED");
                    }
                    work.Status="CLOSED";work.VerifiedBy=actor.Id;work.VerifiedAt=Now;work.ClosedAt=Now;
                    next="INSPECTION_PASSED";recipient="HOUSEKEEPING_SUPERVISOR";finished=true;
                }break;
            default:throw new ApiException(400,"UNKNOWN_ACTION");
        }
        Event(work,"ROOM_ISSUE_"+next,request.OperationKey,request.Note,before);
        db.ChangeTracker.Entries<WorkEvent>().Single(e=>e.State==EntityState.Added&&e.Entity.WorkId==id&&e.Entity.EventType=="ROOM_ISSUE_"+next).Entity.SnapshotJson=System.Text.Json.JsonSerializer.Serialize(new{DocumentId=materialId,PayloadHash=payloadHash});
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        await NotifyIssueRole(work,recipient,"ROOM_ISSUE_"+next,request.OperationKey,ct);
        notifications.Add(work.BranchId,work.CreatedBy,"ROOM_ISSUE_"+next,work.Title,id,null,request.OperationKey);
        audit.Add("WorkItem",id,"ROOM_ISSUE_"+next,work.BranchId,new{status=before,stage},new{work.Status,stage=next},request.OperationKey);
        await db.SaveChangesAsync(ct);
        var flowStatus=next switch{"REJECTED"=>"REJECTED","INSPECTION_PASSED"=>"COMPLETED","INSPECTION_REQUESTED"=>"PENDING_REINSPECTION","MATERIAL_REQUESTED"=>"PENDING_STOREKEEPER","REPAIR_REQUESTED"=>"PENDING_MAINTENANCE_SUPERVISOR","REPAIR_CREATED"=>command is "create-repair" or "start-repair"?"IN_PROGRESS":"ACCEPTED","INSPECTION_FAILED"=>"REWORK_REQUIRED",_=>"PENDING_SUPERVISOR"};
        var actingRole=command is "create-repair" or "start-repair"||command=="report-remedy"&&info.Kind=="BROKEN"?"MAINTENANCE_SUPERVISOR":command=="report-remedy"?"STOREKEEPER":"HOUSEKEEPING_SUPERVISOR";
        await requests.RecordDomainTransition(work,info.Kind=="BROKEN"?"ROOM_EQUIPMENT_DAMAGE":"ROOM_DURABLE_ITEM_MISSING",flowStatus,finished?null:recipient,command,actingRole,request.OperationKey,request.Note,ct,documentId:materialId);
        await RecordRoomIssueChildren(work,info.Kind,command,actingRole,request,materialId,ct);
        if(finished){
            // Safety blocks are explicit operational holds; a repair result alone cannot release them.
            if(info.Impact!="SAFETY"||command=="reject"){
                foreach(var block in await db.RoomBlocks.Where(b=>b.SourceWorkId==id&&b.ReleasedAt==null).ToArrayAsync(ct)){block.ReleasedAt=Now;block.ReleasedBy=actor.Id;}
            }
            await db.SaveChangesAsync(ct);
            if(command=="inspection-pass")await CompleteCleaningAfterRemediation(work,info.CleaningWorkId,request,ct);
            await readiness.Reevaluate(work.RoomId!.Value,id,request.OperationKey,"REMEDIATION_REVIEWED",ct);
        }
        return (object)Output.Record(work);
    },ct);

    private async Task<WorkItem> CurrentRepair(WorkItem work,CancellationToken ct)=>
        await db.WorkItems.Where(w=>w.ParentWorkId==work.WorkId&&w.BranchId==work.BranchId&&w.Kind=="MAINTENANCE"&&w.Status!="CANCELLED")
            .OrderByDescending(w=>w.WorkId).FirstOrDefaultAsync(ct)??throw new ApiException(409,"MAINTENANCE_REQUIRED");
}
