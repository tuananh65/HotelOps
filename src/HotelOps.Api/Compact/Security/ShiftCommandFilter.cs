using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Security;

// Resolve the persisted resource branch, never a caller-selected replacement branch.
public sealed class ShiftCommandFilter(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock):IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var request=context.HttpContext.Request;
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
        branch??=context.ActionArguments.TryGetValue("branchId",out var branchArgument)?branchArgument as long?:null;
        if(branch==null)
            foreach(var value in context.ActionArguments.Values)
                if(value?.GetType().GetProperty("BranchId")?.GetValue(value) is long requestBranch){branch=requestBranch;break;}
        if(branch is long scope)await ShiftCommandAccess.Require(db,actor.Id,scope,clock.GetUtcNow().UtcDateTime,ct);
        // Resource-specific services also call Access.Require inside their transaction.
        await next();
    }
}
