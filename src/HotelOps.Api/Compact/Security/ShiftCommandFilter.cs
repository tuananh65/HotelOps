using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using HotelOps.Api.Compact.Contracts;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Security;

// Resolve the persisted resource branch, never a caller-selected replacement branch.
public sealed class ShiftCommandFilter(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock,ShiftExpiryService shifts):IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var request=context.HttpContext.Request;
        // Refresh both read capabilities/candidates and writes before their transactions.
        // Anonymous authentication endpoints must not trigger operational mutations.
        if(context.HttpContext.User.Identity?.IsAuthenticated==true)
            await shifts.Synchronize(context.HttpContext.RequestAborted);
        if(!ShiftCommandAccess.IsBusinessCommand(request.Method,request.Path.Value??"")){await next();return;}
        var ct=context.HttpContext.RequestAborted;
        var parts=(request.Path.Value??"").Split('/',StringSplitOptions.RemoveEmptyEntries);
        var resource=parts.Length>2?parts[2].ToLowerInvariant():"";
        long? branch=null;
        var id=context.ActionArguments.TryGetValue("workId",out var workId)?workId:
            context.ActionArguments.TryGetValue("id",out var resourceId)?resourceId:null;
        if(id is long key)
        {
            branch=resource switch
            {
                "work" or "requests" or "supplies"=>await db.WorkItems.Where(x=>x.WorkId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "rooms"=>await db.Rooms.Where(x=>x.RoomId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "documents"=>await db.InventoryDocuments.Where(x=>x.DocumentId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "assets"=>await db.Assets.Where(x=>x.AssetId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "evidence"=>await db.Attachments.Where(x=>x.AttachmentId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "inventory-lifecycle"=>await db.StockBatches.Where(x=>x.BatchId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "reusable-inventory" when parts.Contains("positions",StringComparer.OrdinalIgnoreCase)=>await db.ReusablePositions.Where(x=>x.PositionId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                "reusable-inventory"=>await db.ReusableWriteOffs.Where(x=>x.WriteOffId==key).Select(x=>(long?)x.BranchId).SingleOrDefaultAsync(ct),
                _=>null
            };
        }
        if(resource=="evidence"&&context.ActionArguments.TryGetValue("workId",out var evidenceWorkId)&&evidenceWorkId is long ownerId)
            branch=await db.WorkItems.AsNoTracking().Where(w=>w.WorkId==ownerId).Select(w=>(long?)w.BranchId).SingleOrDefaultAsync(ct);
        branch??=context.ActionArguments.TryGetValue("branchId",out var branchArgument)?branchArgument as long?:null;
        if(branch==null)
            foreach(var value in context.ActionArguments.Values)
                if(value?.GetType().GetProperty("BranchId")?.GetValue(value) is long requestBranch){branch=requestBranch;break;}
        // Transfer requests belong to the supplying branch, but the requesting OM
        // acts for the receiving branch. Services still enforce ownership/approval.
        if(resource=="documents")
        {
            var create=context.ActionArguments.Values.OfType<CreateDocumentRequest>().SingleOrDefault();
            if(create?.Kind=="TRANSFER_REQUEST")branch=create.DestinationBranchId??create.BranchId;
            else if(id is long documentId)
            {
                var transfer=await db.InventoryDocuments.AsNoTracking().Where(d=>d.DocumentId==documentId&&d.Kind=="TRANSFER_REQUEST")
                    .Select(d=>new{d.BranchId,d.DestinationBranchId}).SingleOrDefaultAsync(ct);
                if(transfer!=null)branch=parts.Any(p=>p is "approve" or "reject")?transfer.BranchId:transfer.DestinationBranchId??transfer.BranchId;
            }
        }
        long? continuationWork=null;
        if(HttpMethods.IsPost(request.Method))
        {
            if(request.Path.Value?.TrimEnd('/').Equals("/api/v2/work/room-issues",StringComparison.OrdinalIgnoreCase)==true)
                continuationWork=context.ActionArguments.Values.OfType<RoomIssueCreateRequest>().SingleOrDefault()?.CleaningWorkId;
            else if(request.Path.Value?.TrimEnd('/').Equals("/api/v2/work/room-issues/batch",StringComparison.OrdinalIgnoreCase)==true)
                continuationWork=context.ActionArguments.Values.OfType<RoomIssueBatchRequest>().SingleOrDefault()?.CleaningWorkId;
            else if(resource=="reusable-inventory"&&parts.Contains("positions")&&parts.LastOrDefault()=="move")
                continuationWork=context.ActionArguments.Values.OfType<ReusableMoveRequest>().SingleOrDefault()?.WorkId;
        }
        if(branch is long continuationBranch&&continuationWork is long cleaningWork
            &&await CleaningContinuationAccess.CanFinishExisting(db,actor,clock,cleaningWork,continuationBranch,ct))
            context.HttpContext.Items[CleaningContinuationAccess.BoundWorkKey]=cleaningWork;
        if(branch is long scope&&!await CleaningContinuationAccess.AllowsRequest(db,actor,clock,scope,ct))await ShiftCommandAccess.Require(db,actor.Id,scope,clock.GetUtcNow().UtcDateTime,ct);
        // Resource-specific services also call Access.Require inside their transaction.
        await next();
    }
}
