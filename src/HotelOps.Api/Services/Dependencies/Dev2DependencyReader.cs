using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dependencies;

// Internal backend integration service. No HTTP/DTO exposure or mutation approval.
public sealed class Dev2DependencyReader(HotelOpsDbContext db,ICurrentUserAccessor current,
    IPermissionService permissions,UserAccountStatusAccess userAccess)
{
    private (Guid Transaction,long Id,long Branch,string Kind)? callerLease;
    public Task<DependencySnapshot> ReadUserAsync(long user,long branch,CancellationToken ct)=>Read("USER",user,branch,ct);
    public Task<DependencySnapshot> ReadBranchAsync(long branch,CancellationToken ct)=>Read("BRANCH",branch,branch,ct);
    public Task<DependencySnapshot> ReadRoomAsync(long room,long branch,CancellationToken ct)=>Read("ROOM",room,branch,ct);
    public Task<DependencySnapshot> ReadDepartmentAsync(long department,long branch,CancellationToken ct)=>Read("DEPARTMENT",department,branch,ct);

    private async Task Authorize(long branch,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(branch<=0 || !current.TryGetCurrentUser(out var actor) || actor.UserId<=0 || actor.SessionId<=0 || actor.IsRestricted)
            throw new DependencyScopeException();
        // Same structure dependency summary authority as UC015/016. Does not grant
        // permission to edit business records or to expose this service via HTTP.
        if(!(await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE",branch,ct)).IsGranted)
            throw new DependencyScopeException();
    }

    public async Task<DependencySnapshot> ReadUserInCallerTransactionAsync(long user,long branch,CancellationToken ct)
    {
        await Authorize(branch,ct);
        if(!await userAccess.CanManageAsync(user,ct))throw new DependencyScopeException();
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Caller transaction and User locks required before responsibility reads.");
        // User locks FIRST; caller must invoke before any location/feature lock. No commit.
        await new UserEligibilityLock(db).LockAsync([current.TryGetCurrentUser(out var actor)?actor.UserId:0,user],ct);
        callerLease=(db.Database.CurrentTransaction.TransactionId,user,branch,"USER");
        return await Read("USER",user,branch,ct);
    }

    public async Task<DependencySnapshot> ReadStructureInCallerTransactionAsync(string kind,long id,long branch,CancellationToken ct)
    {
        await Authorize(branch,ct);
        if(kind is not ("BRANCH" or "ROOM" or "DEPARTMENT"))throw new ArgumentException("Unsupported structure dependency kind.");
        if(db.Database.CurrentTransaction is not { } tx || tx.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Caller Serializable transaction required before structure locks.");
        await new Dev2PlacementGuard(db).LockBranchAsync(branch,ct);
        callerLease=(tx.TransactionId,id,branch,kind);
        return await Read(kind,id,branch,ct);
    }

    public async Task<DependencySnapshot> RevalidateInCallerTransactionAsync(DependencySnapshot expected,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(db.Database.CurrentTransaction is not { } tx || callerLease is not { } lease || lease.Transaction!=tx.TransactionId ||
            lease.Id!=expected.ResourceId||lease.Branch!=expected.BranchId||lease.Kind!=expected.Resource)
            throw new InvalidOperationException("Matching retained caller transaction/resource lease required.");
        var fresh=await Read(expected.Resource,expected.ResourceId,expected.BranchId,ct);
        RequireUnchanged(expected,fresh);return fresh;
    }
    public static void RequireUnchanged(DependencySnapshot expected,DependencySnapshot fresh)
    {
        if(expected.Resource!=fresh.Resource||expected.ResourceId!=fresh.ResourceId||expected.BranchId!=fresh.BranchId||
            !StringComparer.Ordinal.Equals(expected.Fingerprint,fresh.Fingerprint))throw new DependencySnapshotConflictException();
    }

    private async Task<DependencySnapshot> Read(string kind,long id,long branch,CancellationToken ct)
    {
        await Authorize(branch,ct);
        if(id<=0)throw new DependencyScopeException();
        if(kind=="USER" && !await userAccess.CanManageAsync(id,ct))throw new DependencyScopeException();
        if(!await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==branch,ct) ||
            kind=="ROOM" && !await db.Rooms.AsNoTracking().AnyAsync(r=>r.RoomId==id&&r.BranchId==branch,ct) ||
            kind=="DEPARTMENT" && !await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==id&&d.BranchId==branch,ct))
            throw new DependencyScopeException();
        var facts=new List<DependencyFact>();
        var unresolved=new SortedSet<string>(StringComparer.Ordinal){"FINAL_DEACTIVATION_WRITER_COORDINATION_REQUIRED","NOT_A_CONSISTENT_RELEASE_DECISION"};
        var works=Dev2DependencyQueries.Maintenance(db.MaintenanceWorks.AsNoTracking(),branch);
        if(kind=="USER")works=Dev2DependencyQueries.AssignedWorks(works,id);
        if(kind=="ROOM") { var incidentSource=Dev2DependencyQueries.RoomIncidents(db,branch,id); works=works.Where(w=>w.IncidentId!=null && incidentSource.Any(i=>i.IncidentId==w.IncidentId)); }
        if(kind=="DEPARTMENT")works=works.Where(w=>w.DepartmentId==id);
        foreach(var w in await works.OrderBy(w=>w.MaintenanceWorkId).Select(w=>new{Id=w.MaintenanceWorkId,w.Status}).ToArrayAsync(ct))
            Add("MAINTENANCE",w.Id,w.Status,kind=="USER" && w.Status is "PENDING" or "IN_PROGRESS" or "ON_HOLD");
        if(kind=="USER")
        {
            // Existing approved predicates; historical reporter/reviewer is not assignee.
            foreach(var c in await Dev2DependencyQueries.PendingApprovals(db.MaterialRequestApprovalCycles.AsNoTracking(),id,branch)
                .OrderBy(c=>c.ApprovalCycleId).Select(c=>c.ApprovalCycleId).ToArrayAsync(ct))Add("MATERIAL_REQUEST_APPROVER",c,"PENDING",true);
            foreach(var i in await db.Incidents.AsNoTracking().Where(i=>i.BranchId==branch&&i.ReportedBy==id).Select(i=>i.IncidentId).ToArrayAsync(ct))Add("HISTORICAL_ACTOR",i,"INCIDENT_REPORTER");
            unresolved.Add("USER_OTHER_BRANCH_COVERAGE_REQUIRED");
            // Existing state validation includes unknown/legacy and current-cycle anomalies.
            if((await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE",ct)).IsGranted)
                foreach(var code in await new Dev2ResponsibilityStateQueries(db).ReadUnresolvedAsync(id,ct))unresolved.Add(code);
            else unresolved.Add("USER_GLOBAL_STATE_REVIEW_REQUIRED");
        }
        var assets=db.Assets.AsNoTracking().Where(a=>a.BranchId==branch);
        if(kind=="USER")assets=Dev2DependencyQueries.UserAssets(assets,id);
        if(kind=="ROOM")assets=assets.Where(a=>a.CurrentRoomId==id);
        if(kind=="DEPARTMENT")assets=assets.Where(a=>a.CurrentDepartmentId==id);
        foreach(var a in await assets.Select(a=>new{Id=a.AssetId,State=a.LifecycleStatus}).ToArrayAsync(ct))Add("ASSET",a.Id,a.State,kind=="USER"&&a.State is "IN_USE" or "UNDER_REPAIR");
        if(kind is "USER" or "DEPARTMENT" or "BRANCH")
        {
            var handovers=db.AssetHandovers.AsNoTracking().Where(h=>h.Asset.BranchId==branch);
            if(kind=="USER")handovers=Dev2DependencyQueries.PendingUserHandovers(handovers,id);
            if(kind=="DEPARTMENT")handovers=handovers.Where(h=>h.ToDepartmentId==id||h.FromDepartmentId==id);
            foreach(var h in await handovers.Select(h=>new{Id=h.HandoverId,h.HandoverStatus}).ToArrayAsync(ct))Add("ASSET_HANDOVER",h.Id,h.HandoverStatus,kind=="USER"&&h.HandoverStatus=="PENDING");
        }
        var requests=db.MaterialRequests.AsNoTracking().Where(r=>r.BranchId==branch);
        if(kind=="USER")requests=Dev2DependencyQueries.UserRequests(requests,id);
        if(kind=="ROOM")requests=requests.Where(r=>r.ReceivingRoomId==id);
        if(kind=="DEPARTMENT")requests=requests.Where(r=>r.DepartmentId==id);
        foreach(var r in await requests.Select(r=>new{Id=r.RequestId,r.Status}).ToArrayAsync(ct))Add("MATERIAL_REQUEST",r.Id,r.Status,kind=="USER"&&r.Status is "DRAFT" or "NEEDS_INFORMATION");
        var fulfillment=db.MaterialFulfillments.AsNoTracking().Where(f=>f.Request.BranchId==branch);
        if(kind=="USER")fulfillment=fulfillment.Where(f=>f.FulfilledBy==id||f.ReceivedBy==id);
        if(kind=="ROOM")fulfillment=fulfillment.Where(f=>f.Request.ReceivingRoomId==id);
        if(kind=="DEPARTMENT")fulfillment=fulfillment.Where(f=>f.Request.DepartmentId==id);
        foreach(var f in await fulfillment.Select(f=>new{Id=f.FulfillmentId,f.FulfilledBy,f.ReceivedBy,f.ReceivedAt,f.Status}).ToArrayAsync(ct))
        {
            if(kind=="USER"&&f.FulfilledBy==id)Add("HISTORICAL_ACTOR",f.Id,"MATERIAL_FULFILLMENT_AUTHOR");
            if(kind!="USER"||f.ReceivedBy==id)Add("MATERIAL_FULFILLMENT",f.Id,f.ReceivedAt==null?f.Status:"RECEIVED");
        }
        unresolved.Add("MATERIAL_FULFILLMENT_RECEIPT_POLICY_REQUIRED");
        if(kind=="USER")foreach(var r in await db.MaterialReplenishments.AsNoTracking().Where(r=>r.Warehouse.BranchId==branch&&r.RequestedBy==id)
            .Select(r=>r.ReplenishmentId).ToArrayAsync(ct))Add("HISTORICAL_ACTOR",r,"REPLENISHMENT_REQUESTER_NOT_ASSIGNEE");
        if(kind!="ROOM")
        {
            var warehouses=db.Warehouses.AsNoTracking().Where(w=>w.BranchId==branch);
            if(kind=="USER")warehouses=Dev2DependencyQueries.UserWarehouses(warehouses,id);
            if(kind=="DEPARTMENT")warehouses=warehouses.Where(w=>w.DepartmentId==id);
            foreach(var w in await warehouses.Select(w=>new{Id=w.WarehouseId,w.IsActive}).ToArrayAsync(ct))Add("WAREHOUSE",w.Id,w.IsActive?"ACTIVE":"INACTIVE",kind=="USER"&&w.IsActive);
            // Do not net unlike items, even when warehouse sum is zero.
            if(kind!="USER")foreach(var w in await db.StockMovements.AsNoTracking().Where(m=>warehouses.Any(w=>w.WarehouseId==m.WarehouseId))
                .GroupBy(m=>new{m.WarehouseId,m.ItemId}).Where(g=>g.Sum(m=>m.SignedBaseQty)!=0).Select(g=>g.Key.WarehouseId).Distinct().ToArrayAsync(ct))Add("NONZERO_STOCK",w,"PER_ITEM_NONZERO");
            if(kind!="USER")foreach(var r in await db.MaterialReplenishments.AsNoTracking().Where(r=>warehouses.Any(w=>w.WarehouseId==r.WarehouseId)).Select(r=>new{Id=r.ReplenishmentId,r.Status}).ToArrayAsync(ct))Add("REPLENISHMENT",r.Id,r.Status);
        }
        if(kind is "BRANCH" or "ROOM")foreach(var i in await (kind=="ROOM"?Dev2DependencyQueries.RoomIncidents(db,branch,id):db.Incidents.AsNoTracking().Where(i=>i.BranchId==branch))
            .Select(i=>new{Id=i.IncidentId,i.Status}).ToArrayAsync(ct))Add("INCIDENT",i.Id,i.Status);
        if(kind!="USER")
        {
            var schedules=Dev2DependencyQueries.Schedules(db.PreventiveMaintenanceSchedules.AsNoTracking(),branch);
            if(kind=="ROOM")schedules=schedules.Where(s=>s.RoomId==id||s.Asset!=null&&s.Asset.CurrentRoomId==id);
            if(kind=="DEPARTMENT")schedules=schedules.Where(s=>s.AssignedDepartmentId==id);
            foreach(var s in await schedules.Select(s=>new{Id=s.ScheduleId,s.IsActive}).ToArrayAsync(ct))Add("PM_SCHEDULE",s.Id,s.IsActive?"ACTIVE":"INACTIVE");
            foreach(var r in await db.PreventiveMaintenanceRuns.AsNoTracking().Where(r=>schedules.Any(s=>s.ScheduleId==r.ScheduleId))
                .Select(r=>new{Id=r.RunId,r.PerformedAt}).ToArrayAsync(ct))Add("PM_RUN",r.Id,r.PerformedAt==null?"UNPERFORMED":"PERFORMED");
        }
        unresolved.Add("PM_AMBIGUOUS_SOURCE_AND_ORPHAN_WORK_COVERAGE_REQUIRED");
        unresolved.Add("NOTIFICATION_ROUTING_DEACTIVATION_POLICY_REQUIRED");
        if(kind=="BRANCH")unresolved.Add("NOTIFICATION_FOUNDATION_NOT_PRESENT_IN_SELECTED_BASE");
        if(kind=="ROOM")unresolved.Add("DEV1_CURRENT_CYCLE_CLEANING_INSPECTION_ROOMBLOCK_REQUIRED");
        // Reauthorize after all reads. Never leak a partial snapshot on cancellation/failure.
        await Authorize(branch,ct);
        if(kind=="USER"&&!await userAccess.CanManageAsync(id,ct))throw new DependencyScopeException();
        return Canonical(kind,id,branch,facts,unresolved);
        void Add(string category,long record,string state,bool approved=false)=>facts.Add(new(category,record,branch,state,Dev2DependencyQueries.Classify(category,state,approved)));
    }
    public static DependencySnapshot Canonical(string kind,long id,long branch,IEnumerable<DependencyFact> facts,IEnumerable<string> unresolved)
    {
        var ordered=facts.Distinct().OrderBy(f=>f.Category,StringComparer.Ordinal).ThenBy(f=>f.RecordId).ThenBy(f=>f.State,StringComparer.Ordinal).ThenBy(f=>f.Classification).ToArray();
        if(ordered.Any(f=>f.BranchId!=branch))throw new DependencyScopeException();
        var gaps=unresolved.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{kind,id,branch,facts=ordered,gaps})));
        return new(kind,id,branch,Array.AsReadOnly(ordered),Array.AsReadOnly(gaps),hash);
    }
}
