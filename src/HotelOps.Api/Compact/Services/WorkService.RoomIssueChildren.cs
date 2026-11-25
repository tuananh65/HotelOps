using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // Children are coordination records, not independently executable generic requests.
    // The root's version/handler/stage checks and Atomic transaction govern every transition.
    private async Task RecordRoomIssueChildren(WorkItem root,string kind,string command,string actingRole,
        RoomIssueActionRequest input,long? documentId,CancellationToken ct)
    {
        if(kind=="BROKEN"&&command is "create-repair" or "start-repair" or "report-remedy" or "inspection-fail" or "inspection-pass" or "reopen-technical-review"){
            var repair=await CurrentRepair(root,ct);
            var status=command switch{
                "create-repair" or "start-repair"=>"IN_PROGRESS",
                "report-remedy"=>"PENDING_REINSPECTION","inspection-fail" or "reopen-technical-review"=>"REWORK_REQUIRED",_=>"COMPLETED"};
            var queue=status=="COMPLETED"?null:status=="PENDING_REINSPECTION"?"HOUSEKEEPING_SUPERVISOR":"MAINTENANCE_SUPERVISOR";
            await ChildTransition(root,repair,"MAINTENANCE_REMEDIATION",status,queue,command,actingRole,input,ct);
        }

        if(command=="reopen-technical-review"){
            foreach(var flow in await db.RequestWorkflows.Where(r=>r.ParentRequestWorkId==root.WorkId&&r.RequestType=="ROOM_REINSPECTION"&&r.Status=="PENDING_REINSPECTION").ToArrayAsync(ct)){
                var inspection=await db.WorkItems.SingleAsync(w=>w.WorkId==flow.WorkId,ct);
                await ChildTransition(root,inspection,"ROOM_REINSPECTION","CANCELLED",null,command,actingRole,input,ct);
            }
        }

        if(command=="request-material"){
            Output.Require(documentId!=null,"ROOM_ISSUE_MATERIAL_REQUIRED");
            // Superseded/rejected deliveries retain their own header and document reference.
            foreach(var old in await db.RequestWorkflows.Where(r=>r.ParentRequestWorkId==root.WorkId&&r.RequestType=="DURABLE_ITEM_REMEDIATION"
                &&r.Status!="COMPLETED"&&r.Status!="FULFILLED"&&r.Status!="REJECTED"&&r.Status!="CANCELLED").ToArrayAsync(ct)){
                var previous=await db.WorkItems.SingleAsync(w=>w.WorkId==old.WorkId,ct);
                await ChildTransition(root,previous,old.RequestType,"CANCELLED",null,"superseded",actingRole,input,ct,old.DocumentId);
            }
            var child=await NewIssueChild(root,"Bổ sung đồ vật",ct);
            await ChildTransition(root,child,"DURABLE_ITEM_REMEDIATION","PENDING_STOREKEEPER","STOREKEEPER",command,actingRole,input,ct,documentId);
        }

        if(kind=="MISSING"&&command is "report-remedy" or "request-inspection" or "inspection-fail" or "inspection-pass"){
            var material=await CurrentIssueMaterial(root.WorkId,ct);
            var flow=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.ParentRequestWorkId==root.WorkId&&r.RequestType=="DURABLE_ITEM_REMEDIATION"&&r.DocumentId==material,ct);
            // A pre-cutover issue may already have a document without structured children.
            var child=flow==null?await NewIssueChild(root,"Bổ sung đồ vật",ct):await db.WorkItems.SingleAsync(w=>w.WorkId==flow.WorkId,ct);
            var status=command is "report-remedy" or "request-inspection"?"PENDING_REINSPECTION":command=="inspection-pass"?"COMPLETED":"REJECTED";
            await ChildTransition(root,child,"DURABLE_ITEM_REMEDIATION",status,status=="PENDING_REINSPECTION"?"HOUSEKEEPING_SUPERVISOR":null,command,actingRole,input,ct,material);
        }

        if(command is "report-remedy" or "request-inspection"){
            Output.Require(!await db.RequestWorkflows.AnyAsync(r=>r.ParentRequestWorkId==root.WorkId&&r.RequestType=="ROOM_REINSPECTION"&&r.Status=="PENDING_REINSPECTION",ct),"ROOM_REINSPECTION_ALREADY_ACTIVE");
            var inspection=await NewIssueChild(root,"Kiểm tra lại phòng",ct);
            await ChildTransition(root,inspection,"ROOM_REINSPECTION","PENDING_REINSPECTION","HOUSEKEEPING_SUPERVISOR",command,actingRole,input,ct);
        }
        if(command is "inspection-pass" or "inspection-fail"){
            var flow=await db.RequestWorkflows.SingleOrDefaultAsync(r=>r.ParentRequestWorkId==root.WorkId&&r.RequestType=="ROOM_REINSPECTION"&&r.Status=="PENDING_REINSPECTION",ct);
            var inspection=flow==null?await NewIssueChild(root,"Kiểm tra lại phòng",ct):await db.WorkItems.SingleAsync(w=>w.WorkId==flow.WorkId,ct);
            await ChildTransition(root,inspection,"ROOM_REINSPECTION",command=="inspection-pass"?"COMPLETED":"REJECTED",null,command,actingRole,input,ct);
        }
    }

    private async Task<WorkItem> NewIssueChild(WorkItem root,string label,CancellationToken ct)
    {
        var title=$"{label} · {root.Title}";
        var child=new WorkItem{BranchId=root.BranchId,WorkNo=Guid.NewGuid().ToString("N"),Kind="INTERNAL_REQUEST",Status="SUBMITTED",
            // INTERNAL_REQUEST ancestry belongs to request_workflows; the work-item
            // parent constraint is reserved for operational repair/inspection kinds.
            Title=title[..Math.Min(250,title.Length)],RoomId=root.RoomId,TurnoverNo=root.TurnoverNo,
            AssetId=root.AssetId,CreatedBy=actor.Id,CreatedAt=Now,CategoryKind="WORK_CATEGORY",PriorityKind="PRIORITY",SeverityKind="SEVERITY"};
        db.WorkItems.Add(child);await db.SaveChangesAsync(ct);return child;
    }

    private async Task ChildTransition(WorkItem root,WorkItem child,string type,string status,string? queue,string command,string actingRole,
        RoomIssueActionRequest input,CancellationToken ct,long? documentId=null)
    {
        // Workflow operation keys are unique per actor across all headers, so derive a stable
        // child key rather than reusing the root key. Root retry returns before creating children.
        var digest=SHA256.HashData(Encoding.UTF8.GetBytes($"room-child:{input.OperationKey:D}:{child.WorkId}:{command}"));
        var key=new Guid(digest.AsSpan(0,16));
        if(child.Kind=="INTERNAL_REQUEST"){
            var before=child.Status;
            child.Status=status switch{"COMPLETED"=>"COMPLETED","REJECTED"=>"REJECTED","CANCELLED"=>"CANCELLED",_=>"SUBMITTED"};
            if(status is "COMPLETED" or "REJECTED" or "CANCELLED"){
                child.ClosedAt=Now;child.CompletionNote=input.Note;
                if(status=="COMPLETED")child.CompletedAt=Now;
            }
            Event(child,"ROOM_ISSUE_CHILD_"+command.ToUpperInvariant().Replace('-','_'),key,input.Note,before);
            await db.SaveChangesAsync(ct);
        }
        await requests.RecordDomainTransition(child,type,status,queue,command,actingRole,key,input.Note,ct,parentRequestId:root.WorkId,documentId:documentId);
        if(documentId!=null){
            var flow=await db.RequestWorkflows.SingleAsync(r=>r.WorkId==child.WorkId,ct);
            Output.Require(flow.DocumentId==null||flow.DocumentId==documentId,"REQUEST_ORIGIN_IMMUTABLE");
            flow.DocumentId=documentId;await db.SaveChangesAsync(ct);
        }
    }
}
