using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    public Task<object> ActRoomIssue(long id,string command,RoomIssueActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await StaffSupplyService.Lock(db,ct);
        var work=await LoadRoomIssue(id,ct);
        var purchaseCommand=command is "approve-purchase" or "reject-purchase" or "record-delivery";
        Output.Require((purchaseCommand||!await access.HasRole("OPERATION_MANAGER",work.BranchId,ct))&&!await access.HasRole("OWNER",work.BranchId,ct),"REQUEST_READ_ONLY",403);
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
        if(info.Kind=="MISSING"&&(info.DirtySources!=null||work.AssetId!=null))
            return await ActVerifiedMissing(work,info,stage,command,request,payloadHash,ct);
        if(info.Reusable)return await ActReusableIncident(work,info,stage,command,request,payloadHash,ct);
        var expectedRole=command is "create-repair" or "start-repair"||command=="report-remedy"&&info.Kind=="BROKEN"?"MAINTENANCE_SUPERVISOR":command=="report-remedy"?"STOREKEEPER":"HOUSEKEEPING_SUPERVISOR";
        // Older fully posted deliveries did not record the warehouse handoff. An
        // on-duty supervisor can receive these for inspection from delivery evidence.
        var deliveredLegacy=command=="request-inspection"&&info.Kind=="MISSING"&&stage=="ROOM_ISSUE_MATERIAL_REQUESTED";
        if(command=="request-material"&&info.Kind=="MISSING"&&stage=="ROOM_ISSUE_MATERIAL_REQUESTED")
            await ReclaimRoomMaterialRequest(work,ct);
        if(command=="reopen-technical-review"){
            await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_REVIEW",work.BranchId,ct);
            await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"MAINTENANCE_SUPERVISOR",ct);
        }else if(deliveredLegacy){
            await StaffSupplyService.Lock(db,ct);
            await RequireRoomIssueSupervisor(work.BranchId,ct);
            await access.Require("INSPECTION_PERFORM",work.BranchId,ct:ct);
            await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"HOUSEKEEPING_SUPERVISOR",ct);
            Output.Require(await MaterialDelivered(await CurrentIssueMaterial(id,ct),ct),"ROOM_ISSUE_MATERIAL_NOT_FULFILLED");
        }else await requests.RequireDomainHandler(id,expectedRole,new[]{"PENDING_SUPERVISOR","PENDING_STOREKEEPER","PENDING_MAINTENANCE_SUPERVISOR","ACCEPTED","IN_PROGRESS","PENDING_REINSPECTION","REWORK_REQUIRED"},ct);
        string next;string recipient;long? materialId=null;var finished=false;
        switch(command){
            case "reopen-technical-review":
                Output.Require(info.Kind=="BROKEN"&&stage=="ROOM_ISSUE_INSPECTION_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
                Output.Reason(request.Note);
                var legacy=await CurrentRepair(work,ct);
                Output.Require(legacy.Status=="COMPLETED"&&!await db.WorkEvents.AnyAsync(e=>e.WorkId==legacy.WorkId&&e.EventType=="TECHNICAL_REVIEW_PASSED"&&e.OccurredAt>=legacy.CompletedAt,ct),"TECHNICAL_REVIEW_ALREADY_RECORDED");
                Event(legacy,"LEGACY_TECHNICAL_REVIEW_REOPENED",request.OperationKey,request.Note,legacy.Status);
                db.WorkEvents.Local.Single(e=>e.WorkId==legacy.WorkId&&e.OperationKey==request.OperationKey&&e.EventType=="LEGACY_TECHNICAL_REVIEW_REOPENED").SnapshotJson=System.Text.Json.JsonSerializer.Serialize(new{legacy.CompletedAt,legacy.CompletionNote});
                legacy.Status="PENDING";legacy.CompletedAt=null;legacy.ProgressPercent=0;
                foreach(var assignee in await db.WorkAssignments.Where(a=>a.WorkId==legacy.WorkId&&a.AssignmentStatus=="ACTIVE").Select(a=>a.AssigneeUserId).Distinct().ToArrayAsync(ct))
                    notifications.Add(work.BranchId,assignee,"REPAIR_REWORK_REQUIRED",legacy.Title,legacy.WorkId,null,request.OperationKey,true);
                foreach(var item in await db.WorkChecklistItems.Where(i=>i.WorkId==legacy.WorkId).ToArrayAsync(ct)){
                    audit.Add("ChecklistResult",item.WorkChecklistItemId,"TECHNICAL_REVIEW_RESET",legacy.BranchId,before:new{item.Outcome,item.Note,item.CheckedAt,item.CheckedBy},key:request.OperationKey);
                    item.Outcome=null;item.Note=null;item.CheckedAt=null;item.CheckedBy=null;
                }
                work.Status="IN_PROGRESS";next="REPAIR_CREATED";recipient="MAINTENANCE_SUPERVISOR";
                break;
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
                await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_ASSIGN",work.BranchId,ct);
                Output.Require(!request.ExternalRepair,"MAINTENANCE_STAFF_EXECUTION_REQUIRED",400);
                Output.Require(stage=="ROOM_ISSUE_REPAIR_REQUESTED","ROOM_ISSUE_WRONG_STAGE");
                Output.Require(!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE"&&w.Status!="CLOSED"&&w.Status!="CANCELLED",ct),"MAINTENANCE_ALREADY_ACTIVE");
                var title=$"Khắc phục · {work.Title}";
                var repair=new WorkItem{BranchId=work.BranchId,Kind="MAINTENANCE",Status="PENDING",WorkNo=Guid.NewGuid().ToString("N"),
                    Title=title[..Math.Min(250,title.Length)],RoomId=work.RoomId,TurnoverNo=work.TurnoverNo,AssetId=work.AssetId,ParentWorkId=id,CreatedBy=actor.Id,CreatedAt=Now};
                db.WorkItems.Add(repair);await db.SaveChangesAsync(ct);
                var technician=request.ExternalRepair?actor.Id:request.UserId;
                Output.Require(technician!=null&&await EligibleMaintenanceAssignee(technician.Value,work.BranchId,ct),"MAINTENANCE_ASSIGNEE_REQUIRED",400);
                Output.Require(request.ExternalRepair||technician!=actor.Id,"INTERNAL_REPAIR_REQUIRES_STAFF",400);
                await access.EligibleFor(technician!.Value,work.BranchId,"MAINTENANCE_EXECUTE",ct);
                db.WorkAssignments.Add(new WorkAssignment{WorkId=repair.WorkId,BranchId=work.BranchId,AssigneeUserId=technician.Value,AssignedBy=actor.Id,AssignedAt=Now,AcceptedAt=request.ExternalRepair?Now:null,AssignmentStatus="ACTIVE"});
                if(request.ChecklistTemplateId!=null)await SnapshotExecutionChecklist(repair,request.ChecklistTemplateId.Value,ct);
                Event(repair,request.ExternalRepair?"EXTERNAL_REPAIR_ACCEPTED":"INTERNAL_REPAIR_ASSIGNED",request.OperationKey,request.Note,null);
                notifications.Add(work.BranchId,technician.Value,"WORK_ASSIGNED",repair.Title,repair.WorkId,null,request.OperationKey,true);
                await db.SaveChangesAsync(ct);
                if(request.ExternalRepair)await ActCore(repair.WorkId,"start",new ActionRequest(Convert.ToBase64String(repair.RowVersion),Guid.NewGuid(),null),ct,roomWorkflow:true);
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
                Output.Require(info.Kind!="BROKEN","MAINTENANCE_STAFF_RESULT_REQUIRED",403);
                if(info.Kind=="BROKEN"){
                    await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_EXECUTE",work.BranchId,ct);
                    Output.Require(stage=="ROOM_ISSUE_REPAIR_CREATED","ROOM_ISSUE_WRONG_STAGE");
                    var completed=await CurrentRepair(work,ct);
                    await RequireReplacementSettled(completed.WorkId,ct);
                    var external=await db.WorkEvents.AnyAsync(e=>e.WorkId==completed.WorkId&&e.EventType=="EXTERNAL_REPAIR_ACCEPTED",ct);
                    if(external&&completed.Status=="IN_PROGRESS")await ActCore(completed.WorkId,"complete",new ActionRequest(Convert.ToBase64String(completed.RowVersion),request.OperationKey,request.Note),ct,roomWorkflow:true);
                    Output.Require(completed.Status=="COMPLETED","REPAIR_COMPLETION_REQUIRED");
                    if(!external)Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.WorkId==completed.WorkId&&a.AssigneeUserId==actor.Id,ct),"INDEPENDENT_VERIFIER_REQUIRED",403);
                    Output.Require(await db.WorkChecklistItems.AnyAsync(i=>i.WorkId==completed.WorkId&&i.RequiredSnapshot,ct)
                        &&!await db.WorkChecklistItems.AnyAsync(i=>i.WorkId==completed.WorkId&&i.RequiredSnapshot&&i.Outcome!="PASS",ct),"CHECKLIST_INCOMPLETE");
                    Event(completed,"TECHNICAL_REVIEW_PASSED",Guid.NewGuid(),request.Note,completed.Status);
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
                        db.WorkEvents.Local.Single(e=>e.WorkId==rework.WorkId&&e.OperationKey==request.OperationKey&&e.EventType=="REPAIR_REINSPECTION_FAILED").SnapshotJson=System.Text.Json.JsonSerializer.Serialize(new{rework.CompletedAt,rework.CompletionNote,rework.ProgressPercent});
                        rework.Status="PENDING";rework.CompletedAt=null;rework.ProgressPercent=0;
                        foreach(var assignee in await db.WorkAssignments.Where(a=>a.WorkId==rework.WorkId&&a.AssignmentStatus=="ACTIVE").Select(a=>a.AssigneeUserId).Distinct().ToArrayAsync(ct))
                            notifications.Add(work.BranchId,assignee,"REPAIR_REWORK_REQUIRED",rework.Title,rework.WorkId,null,request.OperationKey,true);
                        foreach(var item in await db.WorkChecklistItems.Where(i=>i.WorkId==rework.WorkId).ToArrayAsync(ct)){
                            audit.Add("ChecklistResult",item.WorkChecklistItemId,"REWORK_RESET",rework.BranchId,before:new{item.Outcome,item.Note,item.CheckedAt,item.CheckedBy},key:request.OperationKey);
                            item.Outcome=null;item.Note=null;item.CheckedAt=null;item.CheckedBy=null;
                        }
                        next="REPAIR_CREATED";recipient="MAINTENANCE_SUPERVISOR";
                    }else{next="INSPECTION_FAILED";recipient="HOUSEKEEPING_SUPERVISOR";}
                }else{
                    Output.Require(request.RoomRowVersion!=null,"ROOM_VERSION_REQUIRED",400);
                    var room=await db.Rooms.SingleAsync(r=>r.RoomId==work.RoomId,ct);Output.Version(room.RowVersion,request.RoomRowVersion!);
                    if(info.Kind=="MISSING")Output.Require(await MaterialDelivered(await CurrentIssueMaterial(id,ct),ct),"ROOM_ISSUE_MATERIAL_NOT_FULFILLED");
                    else{
                        var verified=await CurrentRepair(work,ct);Output.Require(verified.Status=="COMPLETED","REPAIR_COMPLETION_REQUIRED");
                        Output.Require(await db.WorkEvents.AnyAsync(e=>e.WorkId==verified.WorkId&&e.EventType=="TECHNICAL_REVIEW_PASSED"&&e.OccurredAt>=verified.CompletedAt,ct),"TECHNICAL_REVIEW_REQUIRED");
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
        var actingRole=command is "create-repair" or "start-repair" or "reopen-technical-review"||command=="report-remedy"&&info.Kind=="BROKEN"?"MAINTENANCE_SUPERVISOR":command=="report-remedy"?"STOREKEEPER":"HOUSEKEEPING_SUPERVISOR";
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
