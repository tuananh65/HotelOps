using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Controllers;

[ApiController,Authorize,Route("api/v2/workspace")]
public sealed class WorkspaceController(WorkspaceService service):ControllerBase
{
    [HttpGet("options")]public Task<object> Options(long branchId,CancellationToken ct)=>service.Options(branchId,ct);
}
public sealed class WorkspaceService(HotelOpsCompactDbContext db,Access access)
{
    public async Task<object> Options(long branch,CancellationToken ct)
    {
        var master=await access.Allows("MASTER_DATA_MANAGE",branch,ct:ct);
        var work=master||await access.Allows("ASSET_MANAGE",branch,ct:ct);
        var stock=await access.Allows("ITEM_MASTER_MANAGE",branch,ct:ct);
        var rooms=master||await access.Allows("ROOM_VIEW",branch,ct:ct);
        var reportIncident=await access.Allows("INCIDENT_CREATE",branch,true,ct:ct);
        foreach(var permission in new[]{"CLEANING_VIEW","INSPECTION_VIEW","TASK_VIEW","INCIDENT_VIEW","MAINTENANCE_VIEW","IR_VIEW","SHIFT_MANAGE"})
            work|=await access.Allows(permission,branch,true,ct:ct);
        foreach(var permission in new[]{"INVENTORY_VIEW","MR_VIEW","MR_CREATE","REPLENISHMENT_VIEW"})
            stock|=await access.Allows(permission,branch,true,ct:ct);
        Output.Require(rooms||work||stock,"PERMISSION_DENIED",403);
        return new {
            departments=await db.Departments.AsNoTracking().Where(d=>d.BranchId==branch&&d.IsActive).Select(d=>new{d.DepartmentId,d.Name}).ToArrayAsync(ct),
            rooms=await db.Rooms.AsNoTracking().Where(r=>rooms&&r.BranchId==branch&&r.IsActive).Select(r=>new{r.RoomId,r.Code}).ToArrayAsync(ct),
            incidentAssets=await db.Assets.AsNoTracking().Where(a=>reportIncident&&a.BranchId==branch&&a.IsActive).Select(a=>new{a.AssetId,a.AssetCode,a.Name,a.RoomId}).ToArrayAsync(ct),
            references=await db.ReferenceValues.AsNoTracking().Where(r=>(work||stock)&&r.IsActive&&r.Kind!="BRANCH_CONTACT"&&!r.Code.StartsWith("IR_TYPE_")&&!r.Code.StartsWith("ROOM_ISSUE_")).OrderBy(r=>r.Kind).ThenBy(r=>r.SortOrder).ThenBy(r=>r.ReferenceId).Select(r=>new{r.ReferenceId,r.Kind,r.Code,r.Name,r.BaseUnitId}).ToArrayAsync(ct),
            warehouses=await db.Warehouses.AsNoTracking().Where(w=>stock&&w.BranchId==branch&&w.IsActive).Select(w=>new{w.WarehouseId,w.Name}).ToArrayAsync(ct),
            items=await db.InventoryItems.AsNoTracking().Where(i=>stock&&i.IsActive).Select(i=>new{i.ItemId,i.Name,i.Code,i.BaseUnitId}).ToArrayAsync(ct)
            ,checklists=await db.ChecklistTemplates.AsNoTracking().Where(t=>work&&t.BranchId==branch&&t.IsActive&&t.PublishedAt!=null).Select(t=>new{t.ChecklistTemplateId,t.Name,t.Kind,t.VersionNo}).ToArrayAsync(ct)
        };
    }
}
