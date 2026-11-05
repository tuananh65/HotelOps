using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record BranchDependencyState(string Kind,string? Status,long Count,string Classification);
public sealed record BranchDependencySnapshot(long BranchId,long ActiveRooms,long WarehousesWithNonzeroStock,
    IReadOnlyList<BranchDependencyState> States,bool HasConfirmedBlockers,bool HasUnresolvedStates,
    bool CanDeactivate=false,string Coordination="DEACTIVATION_CONCURRENCY_BLOCKED",bool ScopeResolutionComplete=false);

/// <summary>Read facts only; an empty snapshot never authorizes a deactivation.</summary>
public sealed class BranchDeactivationDependencyReader(HotelOpsDbContext db)
{
    public async Task<BranchDependencySnapshot> ReadAsync(long branch,CancellationToken ct)
    {
        if(branch<=0)throw new ArgumentOutOfRangeException(nameof(branch));
        if(db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Retained Serializable caller transaction required.");
        var rooms=await db.Rooms.AsNoTracking().LongCountAsync(x=>x.BranchId==branch && x.IsActive,ct);
        // Ledger net by warehouse AND item, matching InventoryService.Balance. Never net unlike items.
        // Nonzero negative balances also fail closed; do not skip inactive warehouses holding stock.
        var stocked=await db.StockMovements.AsNoTracking().Where(x=>x.Warehouse.BranchId==branch)
            .GroupBy(x=>new{x.WarehouseId,x.ItemId}).Where(g=>g.Sum(x=>x.SignedBaseQty)!=0)
            .Select(g=>g.Key.WarehouseId).Distinct().LongCountAsync(ct);
        var facts=new List<BranchDependencyState>();
        await Add("operational_tasks",db.OperationalTasks.AsNoTracking().Where(x=>x.BranchId==branch).Select(x=>x.Status));
        await Add("cleaning_tasks",db.CleaningTasks.AsNoTracking().Where(x=>x.Room.BranchId==branch).Select(x=>x.Status));
        await Add("cleaning_reworks",db.CleaningReworks.AsNoTracking().Where(x=>x.CleaningTask.Room.BranchId==branch).Select(x=>x.Status));
        await Add("room_inspections",db.RoomInspections.AsNoTracking().Where(x=>x.Room.BranchId==branch
            && (x.CleaningTask==null || x.CleaningTask.Room.BranchId==branch)).Select(x=>x.Status));
        await Add("inspection_scope_mismatch",db.RoomInspections.AsNoTracking().Where(x=>x.CleaningTask!=null
            && x.CleaningTask.Room.BranchId!=x.Room.BranchId
            && (x.Room.BranchId==branch || x.CleaningTask.Room.BranchId==branch)).Select(x=>x.Status));
        await Add("maintenance_works",db.MaintenanceWorks.AsNoTracking().Where(x=>
            (x.Incident!=null && x.Incident.BranchId==branch || x.Department!=null && x.Department.BranchId==branch)
            && (x.Incident==null || x.Department==null || x.Incident.BranchId==x.Department.BranchId)).Select(x=>x.Status));
        await Add("maintenance_scope_mismatch",db.MaintenanceWorks.AsNoTracking().Where(x=>x.Incident!=null && x.Department!=null
            && x.Incident.BranchId!=x.Department.BranchId && (x.Incident.BranchId==branch || x.Department.BranchId==branch)).Select(x=>x.Status));
        await Add("incidents",db.Incidents.AsNoTracking().Where(x=>x.BranchId==branch).Select(x=>x.Status));
        await Add("internal_requests",db.InternalRequests.AsNoTracking().Where(x=>x.BranchId==branch).Select(x=>x.Status));
        await Add("material_requests",db.MaterialRequests.AsNoTracking().Where(x=>x.BranchId==branch).Select(x=>x.Status));
        await Add("replenishments",db.MaterialReplenishments.AsNoTracking().Where(x=>x.Warehouse.BranchId==branch).Select(x=>x.Status));
        // PM/orphan-work ownership and ambiguous terminal-state binding still need joint review.
        // ScopeResolutionComplete/CanDeactivate remain false even when every observed count is zero.
        return new(branch,rooms,stocked,facts,rooms>0||stocked>0||facts.Any(x=>x.Classification=="OPEN"),true);

        async Task Add(string kind,IQueryable<string> source)
        {
            var rows=await source.GroupBy(x=>x).Select(g=>new{Status=g.Key,Count=g.LongCount()}).ToArrayAsync(ct);
            foreach(var row in rows)facts.Add(new(kind,row.Status,row.Count,Classify(kind,row.Status)));
        }
    }

    // Unknown/ambiguous states remain unresolved; never turn missing policy into zero blockers.
    public static string Classify(string kind,string? status)=>kind switch
    {
        "operational_tasks"=>status switch{"COMPLETED" or "CANCELLED"=>"CLOSED","TODO" or "IN_PROGRESS" or "BLOCKED"=>"OPEN",_=>"UNRESOLVED"},
        "cleaning_tasks" or "cleaning_reworks" or "room_inspections"=>status switch{"COMPLETED" or "CANCELLED"=>"CLOSED","PENDING" or "IN_PROGRESS"=>"OPEN",_=>"UNRESOLVED"},
        "incidents"=>status switch{"CLOSED" or "CANCELLED"=>"CLOSED","OPEN" or "IN_PROGRESS"=>"OPEN",_=>"UNRESOLVED"},
        "maintenance_works"=>status switch{"CLOSED" or "CANCELLED"=>"CLOSED","PENDING" or "IN_PROGRESS" or "ON_HOLD"=>"OPEN",_=>"UNRESOLVED"},
        "internal_requests"=>status switch{"CLOSED" or "CANCELLED" or "REJECTED"=>"CLOSED","SUBMITTED" or "IN_PROGRESS"=>"OPEN",_=>"UNRESOLVED"},
        "material_requests"=>status switch{"CANCELLED" or "REJECTED"=>"CLOSED","SUBMITTED" or "PENDING_APPROVAL" or "NEEDS_INFORMATION" or "APPROVED" or "PARTIALLY_ISSUED"=>"OPEN",_=>"UNRESOLVED"},
        "replenishments"=>status switch{"CLOSED"=>"CLOSED","REQUESTED" or "PARTIALLY_RECEIVED"=>"OPEN",_=>"UNRESOLVED"},
        _=>"UNRESOLVED"
    };
}
