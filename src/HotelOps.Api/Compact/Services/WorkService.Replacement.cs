using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<bool> ReplacementManager(long branch,CancellationToken ct)=>
        await access.HasRole("OPERATION_MANAGER",branch,ct)&&await access.Allows("ASSET_MANAGE",branch,ct:ct);

    private async Task ReplacementRead(WorkItem work,CancellationToken ct)
    {
        Output.Require(work.Kind=="MAINTENANCE"&&work.AssetId!=null,"ASSET_MAINTENANCE_REQUIRED",400);
        if(await ReplacementManager(work.BranchId,ct))return;
        await Load(work.WorkId,"read",ct);
    }

    private Task<WorkEvent?> LastReplacement(long id,CancellationToken ct)=>db.WorkEvents.AsNoTracking()
        .Where(e=>e.WorkId==id&&e.EventType.StartsWith("REPLACEMENT_"))
        .OrderByDescending(e=>e.WorkEventId).FirstOrDefaultAsync(ct);

    public async Task<object> ReplacementQueue(long branch,CancellationToken ct)
    {
        await access.RequireRolePermission("OPERATION_MANAGER","ASSET_MANAGE",branch,ct);
        return await db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branch&&w.Kind=="MAINTENANCE"
            &&db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="REPLACEMENT_CONFIRMED"))
            .OrderByDescending(w=>w.WorkId).Select(w=>new{w.WorkId,w.Title,w.AssetId,w.RoomId,
                createdBy=db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&(e.EventType=="REPLACEMENT_PROPOSED"||e.EventType=="REPLACEMENT_CONFIRMED")).OrderBy(e=>e.WorkEventId).Select(e=>(long?)e.ActorUserId).FirstOrDefault(),
                updatedAt=db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&e.EventType.StartsWith("REPLACEMENT_")).OrderByDescending(e=>e.WorkEventId).Select(e=>(DateTime?)e.OccurredAt).FirstOrDefault(),
                canDecide=(!WorkRules.Terminal.Contains(w.Status)||(w.Status=="COMPLETED"&&w.Result=="FAIL"))
                    &&db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&e.EventType.StartsWith("REPLACEMENT_")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType=="REPLACEMENT_CONFIRMED"&&e.ActorUserId!=actor.Id).FirstOrDefault()
                    &&db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&(e.EventType=="REPLACEMENT_PROPOSED"||e.EventType=="REPLACEMENT_CONFIRMED")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.ActorUserId!=actor.Id).FirstOrDefault(),
                status=db.WorkEvents.Where(e=>e.WorkId==w.WorkId&&e.EventType.StartsWith("REPLACEMENT_")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType).FirstOrDefault()}).ToArrayAsync(ct);
    }

    public async Task<object> ReplacementDetail(long id,CancellationToken ct)
    {
        var work=await db.WorkItems.SingleOrDefaultAsync(w=>w.WorkId==id,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        await ReplacementRead(work,ct);
        var last=await LastReplacement(id,ct);var state=last?.EventType??"NONE";
        var assigned=await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE"&&a.AssigneeUserId==actor.Id,ct);
        var supervisor=await access.HasRole("MAINTENANCE_SUPERVISOR",work.BranchId,ct)&&await access.Allows("MAINTENANCE_REVIEW",work.BranchId,ct:ct);
        var executing=work.Status is "IN_PROGRESS" or "ON_HOLD";
        var terminal=WorkRules.Terminal.Contains(work.Status);
        var unrepairable=await IsUnrepairable(id,ct);
        var proposer=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==id&&e.EventType=="REPLACEMENT_PROPOSED")
            .OrderByDescending(e=>e.WorkEventId).Select(e=>(long?)e.ActorUserId).FirstOrDefaultAsync(ct);
        var latest=last==null?null:JsonSerializer.Deserialize<ReplacementSnapshot>(last.SnapshotJson!);
        var targetRoom=latest?.RoomId??work.RoomId;var targetLocation=latest?.LocationId??work.LocationId;
        return new{workId=id,work.Title,rowVersion=Convert.ToBase64String(work.RowVersion),state,
            history=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==id&&e.EventType.StartsWith("REPLACEMENT_"))
                .OrderBy(e=>e.WorkEventId).Select(e=>new{e.EventType,e.Reason,e.SnapshotJson,e.OccurredAt}).ToArrayAsync(ct),
            canPropose=supervisor&&unrepairable&&(state is "NONE" or "REPLACEMENT_REJECTED"),
            canConfirm=!terminal&&supervisor&&state=="REPLACEMENT_PROPOSED"&&last!.ActorUserId!=actor.Id,
            canDecide=(!terminal||unrepairable)&&await ReplacementManager(work.BranchId,ct)&&state=="REPLACEMENT_CONFIRMED"&&last!.ActorUserId!=actor.Id&&proposer!=actor.Id,
            canInstall=assigned&&executing&&state=="REPLACEMENT_APPROVED",
            replacementAssets=await db.Assets.AsNoTracking().Where(a=>(assigned||supervisor)&&state=="REPLACEMENT_APPROVED"&&a.BranchId==work.BranchId
                &&(targetRoom!=null?a.RoomId==targetRoom:targetLocation!=null&&a.LocationId==targetLocation)
                &&a.AssetId!=work.AssetId&&a.IsActive&&(a.ConditionCode=="GOOD"||a.ConditionCode=="FAIR"))
                .Select(a=>new{a.AssetId,a.AssetCode,a.Name}).ToArrayAsync(ct)};
    }

    public Task<object> ReplaceAsset(long id,string command,ReplacementRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        var work=await db.WorkItems.SingleOrDefaultAsync(w=>w.WorkId==id,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        await ReplacementRead(work,ct);Output.Version(work.RowVersion,request.RowVersion);Output.Key(request.OperationKey);
        Output.Require(!string.IsNullOrWhiteSpace(request.Reason)&&request.Reason.Trim().Length<=2000,"REPLACEMENT_REASON_REQUIRED",400);
        var unrepairable=await IsUnrepairable(id,ct);
        Output.Require(!WorkRules.Terminal.Contains(work.Status)||unrepairable,"WORK_ALREADY_TERMINAL");
        Output.Require(!await db.WorkEvents.AnyAsync(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var last=await LastReplacement(id,ct);var state=last?.EventType??"NONE";
        var snapshot=last==null?null:JsonSerializer.Deserialize<ReplacementSnapshot>(last.SnapshotJson!);
        var assigned=await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE"&&a.AssigneeUserId==actor.Id,ct);
        var next="";var recipient="";
        if(command=="propose"){
            await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_REVIEW",work.BranchId,ct);
            await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"MAINTENANCE_SUPERVISOR",ct);
            Output.Require(unrepairable,"UNREPAIRABLE_RESULT_REQUIRED");
            Output.Require(state is "NONE" or "REPLACEMENT_REJECTED","REPLACEMENT_ALREADY_OPEN");
            Output.Require(!string.IsNullOrWhiteSpace(request.Plan)&&request.Plan.Trim().Length<=2000,"REPLACEMENT_PLAN_REQUIRED",400);
            var target=await db.Assets.AsNoTracking().SingleAsync(a=>a.AssetId==work.AssetId&&a.BranchId==work.BranchId,ct);
            Output.Require(target.RoomId!=null||target.LocationId!=null,"ASSET_LOCATION_REQUIRED");
            Output.Require(work.RoomId==null||target.RoomId==work.RoomId,"ASSET_LOCATION_CHANGED");
            snapshot=new(work.AssetId!.Value,request.Reason.Trim(),request.Plan!.Trim(),RoomId:target.RoomId,LocationId:target.LocationId);next="CONFIRMED";recipient="OPERATION_MANAGER";
        }else if(command=="confirm"||command=="reject"&&state=="REPLACEMENT_PROPOSED"){
            await access.RequireRolePermission("MAINTENANCE_SUPERVISOR","MAINTENANCE_REVIEW",work.BranchId,ct);
            await WorkdayAuthority.Require(db,access,actor,clock,work.BranchId,"MAINTENANCE_SUPERVISOR",ct);
            Output.Require(state=="REPLACEMENT_PROPOSED","REPLACEMENT_WRONG_STAGE");
            Output.Require(last!.ActorUserId!=actor.Id,"INDEPENDENT_VERIFIER_REQUIRED",403);
            next=command=="confirm"?"CONFIRMED":"REJECTED";recipient=command=="confirm"?"OPERATION_MANAGER":"MAINTENANCE_SUPERVISOR";
        }else if(command=="approve"||command=="reject"&&state=="REPLACEMENT_CONFIRMED"){
            await access.RequireRolePermission("OPERATION_MANAGER","ASSET_MANAGE",work.BranchId,ct);
            Output.Require(state=="REPLACEMENT_CONFIRMED","REPLACEMENT_WRONG_STAGE");
            var proposer=await db.WorkEvents.Where(e=>e.WorkId==id&&e.WorkEventId<=last!.WorkEventId&&(e.EventType=="REPLACEMENT_PROPOSED"||e.EventType=="REPLACEMENT_CONFIRMED")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.ActorUserId).FirstAsync(ct);
            Output.Require(proposer!=actor.Id
                &&last!.ActorUserId!=actor.Id,"INDEPENDENT_APPROVER_REQUIRED",403);
            next=command=="approve"?"APPROVED":"REJECTED";recipient="MAINTENANCE_SUPERVISOR";
        }else if(command=="install"){
            await Load(id,"execute",ct);Output.Require(assigned,"CURRENT_ASSIGNEE_REQUIRED",403);
            Output.Require(work.Status is "IN_PROGRESS" or "ON_HOLD"&&state=="REPLACEMENT_APPROVED","REPLACEMENT_WRONG_STAGE");
            Output.Require(request.ReplacementAssetId!=null&&request.ReplacementAssetId!=snapshot!.OriginalAssetId,"REPLACEMENT_ASSET_REQUIRED",400);
            var original=await db.Assets.SingleAsync(a=>a.AssetId==snapshot!.OriginalAssetId&&a.BranchId==work.BranchId,ct);
            var replacement=await db.Assets.SingleOrDefaultAsync(a=>a.AssetId==request.ReplacementAssetId&&a.BranchId==work.BranchId&&a.IsActive,ct);
            var targetRoom=snapshot!.RoomId??work.RoomId;var targetLocation=snapshot.LocationId??work.LocationId;
            Output.Require(replacement!=null&&(targetRoom!=null?replacement.RoomId==targetRoom:targetLocation!=null&&replacement.LocationId==targetLocation)&&(replacement.ConditionCode is "GOOD" or "FAIR"),"REPLACEMENT_ASSET_NOT_INSTALLED");
            Output.Require(targetRoom!=null?original.RoomId!=targetRoom:original.LocationId!=targetLocation,"ORIGINAL_ASSET_LOCATION_RECONCILIATION_REQUIRED");
            Output.Require(original.AssetTypeId==null||replacement!.AssetTypeId==original.AssetTypeId,"REPLACEMENT_ASSET_TYPE_MISMATCH");
            snapshot=snapshot! with{ReplacementAssetId=replacement!.AssetId};
            db.AssetEvents.AddRange(
                new AssetEvent{BranchId=work.BranchId,AssetId=original.AssetId,EventType="REPLACEMENT_OUT",SourceWorkId=id,ActorUserId=actor.Id,OperationKey=request.OperationKey,OccurredAt=Now,Note=$"{request.Reason.Trim()} · Tài sản thay: {replacement.AssetCode}"},
                new AssetEvent{BranchId=work.BranchId,AssetId=replacement.AssetId,EventType="REPLACEMENT_IN",SourceWorkId=id,ActorUserId=actor.Id,OperationKey=request.OperationKey,OccurredAt=Now,Note=$"{request.Reason.Trim()} · Thay tài sản: {original.AssetCode}"});
            // Keep the incident's original identity and the asset ledger. The maintenance order now checks the installed asset.
            work.AssetId=replacement.AssetId;next="INSTALLED";recipient="MAINTENANCE_SUPERVISOR";
            foreach(var item in await db.WorkChecklistItems.Where(i=>i.WorkId==id).ToArrayAsync(ct)){
                audit.Add("ChecklistResult",item.WorkChecklistItemId,"REPLACEMENT_RESET",work.BranchId,before:new{item.Outcome,item.Note,item.CheckedAt,item.CheckedBy},key:request.OperationKey);
                item.Outcome=null;item.Note=null;item.CheckedAt=null;item.CheckedBy=null;
            }
        }else throw new ApiException(400,"UNKNOWN_ACTION");
        Event(work,"REPLACEMENT_"+next,request.OperationKey,request.Reason.Trim(),work.Status);
        db.WorkEvents.Local.Single(e=>e.WorkId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey&&e.EventType=="REPLACEMENT_"+next).SnapshotJson=JsonSerializer.Serialize(snapshot);
        db.Entry(work).Property(w=>w.Title).IsModified=true;
        audit.Add("WorkItem",id,"REPLACEMENT_"+next,work.BranchId,after:snapshot,key:request.OperationKey);
        long? followupWorkId=null;
        if(next=="APPROVED"&&unrepairable)
            followupWorkId=await CreateReplacementFollowup(work,snapshot!,request,ct);
        await NotifyReplacement(work,recipient,request.OperationKey,ct);
        foreach(var user in await db.WorkAssignments.Where(a=>a.WorkId==id&&a.AssignmentStatus=="ACTIVE").Select(a=>a.AssigneeUserId).Distinct().ToArrayAsync(ct))
            notifications.Add(work.BranchId,user,"REPLACEMENT_"+next,work.Title,id,null,request.OperationKey);
        await db.SaveChangesAsync(ct);return (object)new{workId=id,followupWorkId,state="REPLACEMENT_"+next,rowVersion=Convert.ToBase64String(work.RowVersion)};
    },ct);

    private async Task NotifyReplacement(WorkItem work,string roleCode,Guid key,CancellationToken ct)
    {
        var permission=roleCode=="OPERATION_MANAGER"?"ASSET_MANAGE":"MAINTENANCE_REVIEW";
        var users=await(from ur in db.UserRoles.AsNoTracking() join r in db.Roles on ur.RoleId equals r.RoleId
            join g in db.RolePermissions on r.RoleId equals g.RoleId join p in db.Permissions on g.PermissionId equals p.PermissionId
            join u in db.Users on ur.UserId equals u.UserId
            where ur.BranchId==work.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=Now&&(ur.ExpiresAt==null||ur.ExpiresAt>Now)&&r.IsActive&&r.Code==roleCode&&p.IsActive&&p.Code==permission&&(g.DataScope=="BRANCH"||g.DataScope=="ALL")
                &&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==work.BranchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=Now&&(m.ValidTo==null||m.ValidTo>Now))
            select ur.UserId).Distinct().ToArrayAsync(ct);
        foreach(var user in users){
            await access.Eligible(user,work.BranchId,ct);
            notifications.Add(work.BranchId,user,"REPLACEMENT_UPDATED",work.Title,work.WorkId,null,key);
        }
    }

    private async Task RequireReplacementSettled(long id,CancellationToken ct)
    {
        var state=(await LastReplacement(id,ct))?.EventType;
        Output.Require(state is null or "REPLACEMENT_REJECTED" or "REPLACEMENT_INSTALLED","REPLACEMENT_NOT_INSTALLED");
    }
}
