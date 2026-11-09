using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dependencies;

// Public query builders allow consumers/tests to verify the exact SQL predicates.
// These are facts, not final deactivation policy or an authorization boundary.
public static class Dev2DependencyQueries
{
    public static IQueryable<MaintenanceWork> AssignedWorks(IQueryable<MaintenanceWork> rows,long user)=>rows.Where(w=>w.AssigneeId==user);
    public static IQueryable<Asset> UserAssets(IQueryable<Asset> rows,long user)=>rows.Where(a=>a.CustodianUserId==user);
    public static IQueryable<Warehouse> UserWarehouses(IQueryable<Warehouse> rows,long user)=>rows.Where(w=>w.ManagerUserId==user);
    public static IQueryable<AssetHandover> PendingUserHandovers(IQueryable<AssetHandover> rows,long user)=>rows.Where(h=>h.ToUserId==user);
    public static IQueryable<MaterialRequest> UserRequests(IQueryable<MaterialRequest> rows,long user)=>rows.Where(r=>r.RequesterId==user);
    public static IQueryable<MaintenanceWork> Maintenance(IQueryable<MaintenanceWork> rows,long branch) =>
        rows.Where(w => (w.Incident != null && w.Incident.BranchId == branch || w.Department != null && w.Department.BranchId == branch)
            && (w.Incident == null || w.Department == null || w.Incident.BranchId == w.Department.BranchId));
    public static IQueryable<MaterialRequestApprovalCycle> PendingApprovals(IQueryable<MaterialRequestApprovalCycle> rows,long user,long branch) =>
        rows.Where(c=>c.AssignedApproverId==user && c.Request.BranchId==branch && c.Status=="PENDING" && c.Request.Status=="PENDING_APPROVAL"
            && !rows.Any(n=>n.RequestId==c.RequestId && n.RevisionNo>c.RevisionNo));
    public static IQueryable<PreventiveMaintenanceSchedule> Schedules(IQueryable<PreventiveMaintenanceSchedule> rows,long branch) =>
        rows.Where(s=>(s.AssetId!=null?1:0)+(s.RoomId!=null?1:0)+(s.AreaId!=null?1:0)==1 &&
            (s.AssetId!=null && s.Asset!.BranchId==branch || s.RoomId!=null && s.Room!.BranchId==branch || s.AreaId!=null && s.Area!.BranchId==branch));
    public static IQueryable<Incident> RoomIncidents(HotelOpsDbContext db,long branch,long room) =>
        db.Incidents.AsNoTracking().Where(i=>i.BranchId==branch && (i.RoomId==room || i.Asset!=null && i.Asset.BranchId==branch && i.Asset.CurrentRoomId==room));
    public static DependencyClassification Classify(string category,string? state,bool approvedUserObligation=false)
    {
        // Only the explicit existing Task46/47 DEV2 user predicates confer BLOCKING.
        if(approvedUserObligation) return DependencyClassification.BLOCKING;
        return category switch
        {
            "INCIDENT" or "MAINTENANCE" => state is "CLOSED" or "CANCELLED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "ASSET" => state=="RETIRED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "ASSET_HANDOVER" => state is "ACCEPTED" or "REJECTED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "MATERIAL_REQUEST" => state is "CANCELLED" or "REJECTED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "REPLENISHMENT" => state=="CLOSED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "WAREHOUSE" or "PM_SCHEDULE" => state=="INACTIVE" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "PM_RUN" => state=="PERFORMED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "HISTORICAL_ACTOR" => DependencyClassification.HISTORICAL,
            "NOTIFICATION_INTENT" => state=="DELIVERED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            "MATERIAL_FULFILLMENT" => state=="RECEIVED" ? DependencyClassification.HISTORICAL : DependencyClassification.UNKNOWN_POLICY,
            _ => DependencyClassification.UNKNOWN_POLICY
        };
    }
}
