using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private async Task<long[]> CleaningAwaitingRemedy(long branch, CancellationToken ct)
    {
        var issues = await (from link in db.WorkLinks.AsNoTracking()
            join issue in db.WorkItems on link.SourceWorkId equals issue.WorkId
            join target in db.WorkItems on link.TargetWorkId equals target.WorkId
            where link.BranchId == branch && link.Relation == "SOURCE" && issue.Kind == "INCIDENT"
                && issue.Status != "CLOSED" && issue.Status != "CANCELLED"
                && (target.Kind == "CLEANING" || target.Kind == "REWORK")
                && db.WorkEvents.Any(e => e.WorkId == issue.WorkId && (e.EventType == "ROOM_ISSUE_REPORTED" || e.EventType == "LEGACY_ROOM_ISSUE_RECOVERED"))
            select new { issue.WorkId, Root = target.Kind == "REWORK" ? target.ParentWorkId!.Value : target.WorkId,
                Stage = db.WorkEvents.Where(e => e.WorkId == issue.WorkId && e.EventType.StartsWith("ROOM_ISSUE_"))
                    .OrderByDescending(e => e.WorkEventId).Select(e => e.EventType).First() }).ToArrayAsync(ct);
        string[] actionable = ["ROOM_ISSUE_REPORTED", "ROOM_ISSUE_INSPECTION_REQUESTED", "ROOM_ISSUE_INSPECTION_FAILED", "ROOM_ISSUE_REMEDY_REPORTED"];
        var delivered = new HashSet<long>();
        foreach(var issue in issues.Where(i=>i.Stage=="ROOM_ISSUE_MATERIAL_REQUESTED"))
            if(await MaterialDelivered(await CurrentIssueMaterial(issue.WorkId,ct),ct))delivered.Add(issue.WorkId);
        return issues.GroupBy(i => i.Root).Where(g => g.All(i => !actionable.Contains(i.Stage)&&!delivered.Contains(i.WorkId))).Select(g => g.Key).ToArray();
    }
    private IQueryable<WorkItem> CleaningIssues(long workId)=>db.WorkItems.Where(i=>i.Kind=="INCIDENT"
        &&i.RoomId!=null
        &&db.WorkLinks.Any(l=>l.SourceWorkId==i.WorkId&&l.BranchId==i.BranchId&&l.Relation=="SOURCE"
            &&(l.TargetWorkId==workId||db.WorkItems.Any(c=>c.WorkId==l.TargetWorkId&&c.ParentWorkId==workId&&c.Kind=="REWORK"))));
    private IQueryable<long> CleaningWithOpenIssues(long branch)=>db.WorkItems.Where(w=>w.BranchId==branch&&w.Kind=="CLEANING"
        &&db.WorkLinks.Any(l=>l.BranchId==branch&&l.Relation=="SOURCE"&&(l.TargetWorkId==w.WorkId||db.WorkItems.Any(c=>c.WorkId==l.TargetWorkId&&c.ParentWorkId==w.WorkId&&c.Kind=="REWORK"))
            &&db.WorkItems.Any(i=>i.WorkId==l.SourceWorkId&&i.Kind=="INCIDENT"&&i.Status!="CLOSED"&&i.Status!="CANCELLED"
                &&i.RoomId!=null))).Select(w=>w.WorkId);
    private async Task<object[]> CleaningIssueDetails(WorkItem work,CancellationToken ct)
    {
        if(work.Kind is not ("CLEANING" or "REWORK"))return [];
        var rows=await CleaningIssues(work.Kind=="REWORK"?work.ParentWorkId!.Value:work.WorkId).AsNoTracking().OrderBy(i=>i.WorkId).ToArrayAsync(ct);
        var result=new List<object>();
        foreach(var issue in rows){
            if(!await IsRoomIssue(issue.WorkId,ct)){
                result.Add(new{issue.WorkId,issue.Title,issue.Status,kind="LEGACY",legacy=true,stage=(string?)null,readyForInspection=false});
                continue;
            }
            var info=await IssueInfo(issue.WorkId,ct);var stage=await IssueStage(issue.WorkId,ct);
            var readyForInspection=stage=="ROOM_ISSUE_MATERIAL_REQUESTED"&&await MaterialDelivered(await CurrentIssueMaterial(issue.WorkId,ct),ct);
            result.Add(new{issue.WorkId,issue.Title,issue.Status,info.Kind,info.Quantity,info.ObjectName,stage,readyForInspection});}
        return result.ToArray();
    }
    public Task<object> ConfirmCleaningIssue(long id,ActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var work=await Load(id,"read",ct);
        await RequireRoomIssueSupervisor(work.BranchId,ct);await access.Require("INSPECTION_PERFORM",work.BranchId,ct:ct);
        Output.Key(request.OperationKey);Output.Reason(request.Reason);
        var prior=await db.WorkEvents.SingleOrDefaultAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        var payload=System.Text.Json.JsonSerializer.Serialize(new{IssueId=request.RelatedId});
        if(prior!=null){Output.Require(prior.EventType=="CLEANING_ISSUE_CONFIRMED"&&prior.Reason==request.Reason&&prior.SnapshotJson==payload,"IDEMPOTENCY_KEY_REUSED");return (object)Output.Record(work);}
        Output.Version(work.RowVersion,request.RowVersion);
        Output.Require(work.Kind=="CLEANING"&&work.Status is "PENDING_INSPECTION" or "REWORK_REQUIRED","CLEANING_NOT_READY_FOR_REVIEW");
        Output.Require(await CanReportDuringHousekeeping(work,ct),"ROOM_REPORT_EXECUTION_REQUIRED",403);
        Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==actor.Id&&(a.WorkId==id||db.WorkItems.Any(w=>w.WorkId==a.WorkId&&w.ParentWorkId==id&&w.Kind=="REWORK")),ct),"INSPECTOR_MUST_BE_INDEPENDENT",403);
        var issue=await CleaningIssues(id).SingleOrDefaultAsync(i=>i.WorkId==request.RelatedId,ct)??throw new ApiException(404,"ROOM_ISSUE_NOT_FOUND");
        Output.Require(await IssueStage(issue.WorkId,ct)=="ROOM_ISSUE_REPORTED","ROOM_ISSUE_WRONG_STAGE");
        await ActRoomIssue(issue.WorkId,"approve",new RoomIssueActionRequest(Convert.ToBase64String(issue.RowVersion),Guid.NewGuid(),request.Reason),ct);
        var info=await IssueInfo(issue.WorkId,ct);
        if(info.Kind=="MISSING"){
            var warehouses=await db.Warehouses.Where(w=>w.BranchId==work.BranchId&&w.IsActive).Select(w=>w.WarehouseId).ToArrayAsync(ct);
            Output.Require(warehouses.Length==1,"BRANCH_WAREHOUSE_REQUIRED");
            var item=await db.InventoryItems.SingleOrDefaultAsync(i=>i.ItemId==info.ItemId&&i.IsActive,ct)??throw new ApiException(409,"ITEM_UNAVAILABLE");
            await ActRoomIssue(issue.WorkId,"request-material",new RoomIssueActionRequest(Convert.ToBase64String(issue.RowVersion),Guid.NewGuid(),request.Reason,ItemId:item.ItemId,UnitId:item.BaseUnitId,Quantity:info.Quantity,WarehouseId:warehouses[0]),ct);
        }
        Event(work,"CLEANING_ISSUE_CONFIRMED",request.OperationKey,request.Reason,work.Status);
        db.WorkEvents.Local.Last(e=>e.WorkId==id&&e.OperationKey==request.OperationKey).SnapshotJson=payload;
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        await db.SaveChangesAsync(ct);return (object)Output.Record(work);
    },ct);
}
