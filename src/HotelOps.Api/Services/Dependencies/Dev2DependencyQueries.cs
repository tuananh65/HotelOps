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
    public static IQueryable<MaterialRequest> RoomRequests(HotelOpsDbContext db,long branch,long room) =>
        db.MaterialRequests.AsNoTracking().Where(r=>r.BranchId==branch && (r.ReceivingRoomId==room ||
            r.IncidentId!=null && RoomIncidents(db,branch,room).Any(i=>i.IncidentId==r.IncidentId)));
    public static IQueryable<MaintenanceWork> RoomMaintenance(HotelOpsDbContext db,long branch,long room) =>
        Maintenance(db.MaintenanceWorks.AsNoTracking(),branch).Where(w=>
            w.IncidentId!=null && RoomIncidents(db,branch,room).Any(i=>i.IncidentId==w.IncidentId) ||
            w.PreventiveMaintenanceRuns.Any(r=>r.Schedule.RoomId==room && r.Schedule.Room!.BranchId==branch ||
                r.Schedule.Asset!=null && r.Schedule.Asset.BranchId==branch && r.Schedule.Asset.CurrentRoomId==room));
    public static IQueryable<MaintenanceWork> ScopeMismatches(IQueryable<MaintenanceWork> rows,long branch) =>
        rows.Where(w=>w.Incident!=null&&w.Department!=null&&w.Incident.BranchId!=w.Department.BranchId
            &&(w.Incident.BranchId==branch||w.Department.BranchId==branch));
    public static IQueryable<MaterialFulfillment> DepartmentFulfillments(IQueryable<MaterialFulfillment> rows,long branch,long department) =>
        rows.Where(f=>f.Request.BranchId==branch&&f.Request.DepartmentId==department ||
            f.Warehouse.BranchId==branch&&f.Warehouse.DepartmentId==department);
    public static IQueryable<AssetHandover> DepartmentHandovers(IQueryable<AssetHandover> rows,long department) =>
        rows.Where(h=>h.FromDepartmentId==department||h.ToDepartmentId==department);
    public static IQueryable<PreventiveMaintenanceSchedule> AmbiguousSchedules(IQueryable<PreventiveMaintenanceSchedule> rows,long branch) =>
        rows.Where(s=>(s.AssetId!=null?1:0)+(s.RoomId!=null?1:0)+(s.AreaId!=null?1:0)!=1 &&
            (s.Asset!=null&&s.Asset.BranchId==branch||s.Room!=null&&s.Room.BranchId==branch||s.Area!=null&&s.Area.BranchId==branch||
                s.AssignedDepartment!=null&&s.AssignedDepartment.BranchId==branch));
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
