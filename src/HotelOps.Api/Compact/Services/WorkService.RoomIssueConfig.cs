using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private static string RoomIssueCode(long branch,string kind)=>$"ROOM_ISSUE_{branch}_{kind}";
    private async Task RequireRoomIssueSupervisor(long branch,CancellationToken ct)
    {
        await access.RequireRolePermission("HOUSEKEEPING_SUPERVISOR","IR_PROCESS",branch,ct);
        Output.Require(!await access.HasRole("OPERATION_MANAGER",branch,ct),"OM_REQUEST_READ_ONLY",403);
    }
    public async Task<object> RoomIssueTypes(long branch,CancellationToken ct)
    {
        if(!await access.Allows("IR_CREATE",branch,true,ct:ct))await access.Require("INCIDENT_VIEW",branch,ct:ct);
        var codes=new[]{RoomIssueCode(branch,"BROKEN"),RoomIssueCode(branch,"MISSING")};
        return (await db.ReferenceValues.AsNoTracking().Where(r=>r.Kind=="WORK_CATEGORY"&&codes.Contains(r.Code)).OrderBy(r=>r.SortOrder).ToArrayAsync(ct))
            .Select(r=>new{r.ReferenceId,r.Name,r.IsActive,description=RoomIssueDescription(r.SettingsJson),kind=r.Code.EndsWith("_BROKEN")?"BROKEN":"MISSING",rowVersion=Convert.ToBase64String(r.RowVersion)});
    }
    private static string? RoomIssueDescription(string? json){if(string.IsNullOrEmpty(json))return null;using var doc=JsonDocument.Parse(json);return doc.RootElement.TryGetProperty("description",out var value)?value.GetString():null;}
    public Task<object> SaveRoomIssueType(long branch,RoomIssueTypeRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.RequireRolePermission("OPERATION_MANAGER","ROOM_SUPPLY_CONFIG",branch,ct);
        Output.Require(request.Kind is "BROKEN" or "MISSING","ROOM_ISSUE_TYPE_INVALID",400);
        var code=RoomIssueCode(branch,request.Kind);
        var row=await db.ReferenceValues.SingleOrDefaultAsync(r=>r.Kind=="WORK_CATEGORY"&&r.Code==code,ct);
        if(row==null){Output.Require(request.RowVersion==null,"STALE_ROW_VERSION");row=new ReferenceValue{Kind="WORK_CATEGORY",Code=code,SortOrder=request.Kind=="BROKEN"?0:1};db.ReferenceValues.Add(row);}
        else {Output.Require(request.RowVersion!=null,"STALE_ROW_VERSION");Output.Version(row.RowVersion,request.RowVersion!);}
        var name=request.Name.Trim();Output.Require(name.Length>0,"NAME_REQUIRED",400);
        var other=RoomIssueCode(branch,request.Kind=="BROKEN"?"MISSING":"BROKEN");
        Output.Require(!await db.ReferenceValues.AnyAsync(r=>r.Kind=="WORK_CATEGORY"&&r.Code==other&&r.Name==name,ct),"DUPLICATE_NAME",409);
        row.Name=name;row.IsActive=request.IsActive;row.SettingsJson=JsonSerializer.Serialize(new{branchId=branch,roomIssueKind=request.Kind,description=request.Description?.Trim()});
        await db.SaveChangesAsync(ct);audit.Add("RoomIssueType",row.ReferenceId,"CONFIGURE",branch,after:new{request.Kind,row.Name,row.IsActive,request.Description});
        return (object)new{row.ReferenceId,row.Name,row.IsActive,rowVersion=Convert.ToBase64String(row.RowVersion)};
    },ct);
    public async Task<object> RoomIssueObjects(long branch,long roomId,CancellationToken ct)
    {
        await access.Require("IR_CREATE",branch,true,ct);
        Output.Require(!await access.HasRole("OPERATION_MANAGER",branch,ct),"OM_REQUEST_READ_ONLY",403);
        var room=await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r=>r.RoomId==roomId&&r.BranchId==branch&&r.IsActive,ct)??throw new ApiException(404,"ROOM_NOT_FOUND");
        var assets=await db.Assets.AsNoTracking().Where(a=>a.RoomId==roomId&&a.BranchId==branch&&a.IsActive&&a.RetiredAt==null
                &&db.SupplyConfigurations.Any(c=>c.BranchId==branch&&c.AssetId==a.AssetId&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="C"))
            .OrderBy(a=>a.Name).Select(a=>new{a.AssetId,a.Name,a.AssetCode}).ToArrayAsync(ct);
        var assignedItems=await RoomIssuedItems(branch,roomId,ct);
        var durableIds=await db.SupplyConfigurations.AsNoTracking().Where(c=>c.BranchId==branch&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="B"&&c.ItemId!=null).Select(c=>c.ItemId!.Value).ToArrayAsync(ct);
        var items=assignedItems.Where(i=>durableIds.Contains(i.ItemId)).ToArray();
        return new{room.RoomId,room.Code,rowVersion=Convert.ToBase64String(room.RowVersion),assets,items};
    }
    // Only posted room deliveries, minus their posted returns/reversals. Warehouse balance is not room possession.
    private sealed record RoomIssuedItem(long ItemId,string Name);
    private async Task<RoomIssuedItem[]> RoomIssuedItems(long branch,long roomId,CancellationToken ct)
    {
        var issued=await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where d.BranchId==branch&&d.Kind=="ISSUE"&&d.PostedAt!=null&&d.ReceivingRoomId==roomId&&m.SignedBaseQty<0
                &&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select new{l.LineId,l.ItemId,Qty=-m.SignedBaseQty}).ToArrayAsync(ct);
        var lines=issued.Select(x=>x.LineId).ToArray();
        var returned=await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where d.BranchId==branch&&d.Kind=="RETURN"&&d.PostedAt!=null&&l.SourceLineId!=null&&lines.Contains(l.SourceLineId.Value)
                &&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select new{l.ItemId,m.SignedBaseQty}).ToArrayAsync(ct);
        var ids=issued.GroupBy(i=>i.ItemId).Where(g=>g.Sum(x=>x.Qty)-returned.Where(x=>x.ItemId==g.Key).Sum(x=>x.SignedBaseQty)>0).Select(g=>g.Key).ToArray();
        return await db.InventoryItems.AsNoTracking().Where(i=>ids.Contains(i.ItemId)&&i.IsActive).OrderBy(i=>i.Name).Select(i=>new RoomIssuedItem(i.ItemId,i.Name)).ToArrayAsync(ct);
    }
}
