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
    private (Guid Transaction,long User)? globalLease;
    private sealed record GlobalUserReviewAuthority(CurrentUserInfo Actor,long Target);

    private async Task<GlobalUserReviewAuthority> AuthorizeGlobalUserAsync(long user,CancellationToken ct,
        GlobalUserReviewAuthority? expected=null)
    {
        ct.ThrowIfCancellationRequested();
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId<=0||actor.SessionId<=0||actor.IsRestricted||user<=0||
            expected is not null&&(expected.Actor!=actor||expected.Target!=user)||
            !(await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE",ct)).IsGranted||
            !await userAccess.CanManageAsync(user,ct))throw new DependencyScopeException();
        if(!current.TryGetCurrentUser(out var observed)||observed!=actor)throw new DependencyScopeException();
        return new(actor,user);
    }

    // Global target review must not silently certify only one branch's responsibilities.
    public async Task<GlobalUserDependencySnapshot> ReadUserGlobalAsync(long user,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var authority=await AuthorizeGlobalUserAsync(user,ct);
        var branches=await db.Branches.AsNoTracking().OrderBy(b=>b.BranchId).Select(b=>b.BranchId).ToArrayAsync(ct);
        var result=new List<DependencySnapshot>();
        foreach(var branch in branches)result.Add(await Read("USER",user,branch,ct,authority));
        var unscoped=new List<UnscopedResponsibility>();
        foreach(var w in await Dev2DependencyQueries.AssignedWorks(db.MaintenanceWorks.AsNoTracking(),user)
            .Where(w=>w.Incident==null&&w.Department==null || w.Incident!=null&&w.Department!=null&&w.Incident.BranchId!=w.Department.BranchId)
            .OrderBy(w=>w.MaintenanceWorkId).Select(w=>new{w.MaintenanceWorkId,w.Status,w.IncidentId,w.DepartmentId,w.AssigneeId,w.ResolvedAt,w.ReviewedAt}).ToArrayAsync(ct))
            unscoped.Add(new("MAINTENANCE_UNRESOLVED_SCOPE",w.MaintenanceWorkId,w.Status,SourceFingerprint(w)));
        var coveredBranches=await db.Branches.AsNoTracking().OrderBy(b=>b.BranchId).Select(b=>b.BranchId).ToArrayAsync(ct);
        if(!branches.SequenceEqual(coveredBranches))
            throw new DependencySnapshotConflictException();
        await AuthorizeGlobalUserAsync(user,ct,authority);
        var digest=SourceFingerprint(new{user,branches=result.Select(s=>new{s.BranchId,s.Fingerprint}),unscoped});
        return new(user,result.AsReadOnly(),unscoped.AsReadOnly(),digest); // Never mutation approval.
    }
    public async Task<GlobalUserDependencySnapshot> ReadUserGlobalInCallerTransactionAsync(long user,CancellationToken ct)
    {
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction is not {} tx||System.Transactions.Transaction.Current!=null)
            throw new InvalidOperationException("Existing owner global-admin/User transaction required.");
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId<=0||actor.SessionId<=0||actor.IsRestricted||user<=0)throw new DependencyScopeException();
        await new UserEligibilityLock(db).LockAsync([actor.UserId,user],ct);
        var result=await ReadUserGlobalAsync(user,ct);globalLease=(tx.TransactionId,user);return result;
    }
    public async Task<GlobalUserDependencySnapshot> RevalidateUserGlobalInCallerTransactionAsync(long user,
        GlobalUserDependencySnapshot expected,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(db.Database.CurrentTransaction is not {} tx||globalLease!=(tx.TransactionId,user))
            throw new InvalidOperationException("Matching global responsibility transaction lease required.");
        var fresh=await ReadUserGlobalAsync(user,ct);
        if(expected.UserId!=fresh.UserId||expected.Fingerprint!=fresh.Fingerprint)throw new DependencySnapshotConflictException();
        return fresh;
    }

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
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction is null||System.Transactions.Transaction.Current!=null)
            throw new InvalidOperationException("Caller transaction and User locks required before responsibility reads.");
        // User locks FIRST; caller must invoke before any location/feature lock. No commit.
        await new UserEligibilityLock(db).LockAsync([current.TryGetCurrentUser(out var actor)?actor.UserId:0,user],ct);
        await Authorize(branch,ct);
        if(!await userAccess.CanManageAsync(user,ct))throw new DependencyScopeException();
        callerLease=(db.Database.CurrentTransaction.TransactionId,user,branch,"USER");
        return await Read("USER",user,branch,ct);
    }

    public async Task<DependencySnapshot> ReadStructureInCallerTransactionAsync(string kind,long id,long branch,CancellationToken ct)
    {
        if(kind is not ("BRANCH" or "ROOM" or "DEPARTMENT"))throw new ArgumentException("Unsupported structure dependency kind.");
        if(!db.Database.IsSqlServer()||System.Transactions.Transaction.Current!=null||db.Database.CurrentTransaction is not { } tx || tx.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Caller Serializable transaction required before structure locks.");
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId<=0||actor.IsRestricted)throw new DependencyScopeException();
        await new UserEligibilityLock(db).RequireEligibleAsync(actor.UserId,ct);
        await Authorize(branch,ct);
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

    private async Task AuthorizeReadAsync(string kind,long id,long branch,GlobalUserReviewAuthority? globalAuthority,CancellationToken ct)
    {
        if(globalAuthority is not null)
        {
            // Private target-bound context minted only after approved global checks.
            // Revalidate it at BOTH ends; a flag or branch grant cannot enter this path.
            if(kind!="USER"||id!=globalAuthority.Target||branch<=0)throw new DependencyScopeException();
            await AuthorizeGlobalUserAsync(id,ct,globalAuthority);
            return;
        }
        await Authorize(branch,ct);
        if(id<=0||kind=="BRANCH"&&id!=branch)throw new DependencyScopeException();
        if(kind=="USER" && !await userAccess.CanManageAsync(id,ct))throw new DependencyScopeException();
    }

    private async Task<DependencySnapshot> Read(string kind,long id,long branch,CancellationToken ct,GlobalUserReviewAuthority? globalAuthority=null)
    {
        await AuthorizeReadAsync(kind,id,branch,globalAuthority,ct);
        if(id<=0||kind=="BRANCH"&&id!=branch)throw new DependencyScopeException();
        if(!await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==branch,ct) ||
            kind=="ROOM" && !await db.Rooms.AsNoTracking().AnyAsync(r=>r.RoomId==id&&r.BranchId==branch,ct) ||
            kind=="DEPARTMENT" && !await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==id&&d.BranchId==branch,ct))
            throw new DependencyScopeException();
        var facts=new List<DependencyFact>();
        var targetBranch=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branch).Select(b=>new{b.BranchId,b.IsActive}).SingleAsync(ct);
        facts.Add(new("TARGET_BRANCH",branch,branch,targetBranch.IsActive?"ACTIVE":"INACTIVE",DependencyClassification.UNKNOWN_POLICY,SourceFingerprint(targetBranch)));
        if(kind=="ROOM"){
            var room=await db.Rooms.AsNoTracking().Where(r=>r.RoomId==id&&r.BranchId==branch)
                .Select(r=>new{r.RoomId,r.BranchId,r.FloorId,r.AreaId,r.IsActive,r.OperationalStatus,
                    FloorActive=r.Floor.IsActive,AreaActive=r.Area==null?(bool?)null:r.Area.IsActive}).SingleAsync(ct);
            facts.Add(new("TARGET_ROOM",id,branch,room.IsActive?"ACTIVE":"INACTIVE",DependencyClassification.UNKNOWN_POLICY,SourceFingerprint(room)));
        }
        if(kind=="DEPARTMENT"){
            var department=await db.Departments.AsNoTracking().Where(d=>d.DepartmentId==id&&d.BranchId==branch)
                .Select(d=>new{d.DepartmentId,d.BranchId,d.IsActive}).SingleAsync(ct);
            facts.Add(new("TARGET_DEPARTMENT",id,branch,department.IsActive?"ACTIVE":"INACTIVE",DependencyClassification.UNKNOWN_POLICY,SourceFingerprint(department)));
        }
        if(kind=="USER"){
            var user=await db.Users.AsNoTracking().Where(u=>u.UserId==id).Select(u=>new{u.UserId,u.RowVersion,u.DeletedAt,u.AccountStatus,u.EmploymentStatus}).SingleOrDefaultAsync(ct)
                ??throw new DependencyScopeException();
            facts.Add(new("TARGET_USER",id,branch,user.AccountStatus,DependencyClassification.UNKNOWN_POLICY,SourceFingerprint(user)));
        }
        var unresolved=new SortedSet<string>(StringComparer.Ordinal){"FINAL_DEACTIVATION_WRITER_COORDINATION_REQUIRED","NOT_A_CONSISTENT_RELEASE_DECISION"};
        var works=Dev2DependencyQueries.Maintenance(db.MaintenanceWorks.AsNoTracking(),branch);
        if(kind=="USER")works=Dev2DependencyQueries.AssignedWorks(works,id);
        if(kind=="ROOM") works=Dev2DependencyQueries.RoomMaintenance(db,branch,id);
        if(kind=="DEPARTMENT")works=works.Where(w=>w.DepartmentId==id);
        foreach(var w in await works.OrderBy(w=>w.MaintenanceWorkId).Select(w=>new{Id=w.MaintenanceWorkId,w.Status,w.AssigneeId,w.DepartmentId,w.IncidentId,w.ResolvedAt,w.ReviewedAt,w.ActualStartAt}).ToArrayAsync(ct))
            Add("MAINTENANCE",w.Id,w.Status,kind=="USER" && w.Status is "PENDING" or "IN_PROGRESS" or "ON_HOLD",w);
        if(kind is "BRANCH" or "DEPARTMENT")foreach(var w in await Dev2DependencyQueries.ScopeMismatches(db.MaintenanceWorks.AsNoTracking(),branch)
            .Where(w=>kind!="DEPARTMENT"||w.DepartmentId==id).Select(w=>new{Id=w.MaintenanceWorkId,w.AssigneeId,w.DepartmentId,w.IncidentId,w.Status}).ToArrayAsync(ct))
            Add("MAINTENANCE_SCOPE_MISMATCH",w.Id,w.Status,version:w);
        if(kind=="USER")
        {
            // Existing approved predicates; historical reporter/reviewer is not assignee.
            foreach(var c in await Dev2DependencyQueries.PendingApprovals(db.MaterialRequestApprovalCycles.AsNoTracking(),id,branch)
                .OrderBy(c=>c.ApprovalCycleId).Select(c=>c.ApprovalCycleId).ToArrayAsync(ct))Add("MATERIAL_REQUEST_APPROVER",c,"PENDING",true);
            foreach(var i in await db.Incidents.AsNoTracking().Where(i=>i.BranchId==branch&&i.ReportedBy==id).Select(i=>i.IncidentId).ToArrayAsync(ct))Add("HISTORICAL_ACTOR",i,"INCIDENT_REPORTER");
            if(globalAuthority is null)unresolved.Add("USER_OTHER_BRANCH_COVERAGE_REQUIRED");
            unresolved.Add("REPLENISHMENT_CURRENT_RESPONSIBILITY_POLICY_REQUIRED");
            // Existing state validation includes unknown/legacy and current-cycle anomalies.
            if((await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE",ct)).IsGranted)
                foreach(var code in await new Dev2ResponsibilityStateQueries(db).ReadUnresolvedAsync(id,ct))unresolved.Add(code);
            else unresolved.Add("USER_GLOBAL_STATE_REVIEW_REQUIRED");
        }
        var assets=db.Assets.AsNoTracking().Where(a=>a.BranchId==branch);
        if(kind=="USER")assets=Dev2DependencyQueries.UserAssets(assets,id);
        if(kind=="ROOM")assets=assets.Where(a=>a.CurrentRoomId==id);
        if(kind=="DEPARTMENT")assets=assets.Where(a=>a.CurrentDepartmentId==id);
        foreach(var a in await assets.Select(a=>new{Id=a.AssetId,State=a.LifecycleStatus,a.CustodianUserId,a.CurrentDepartmentId,a.CurrentRoomId,a.CurrentAreaId,a.UpdatedAt}).ToArrayAsync(ct))Add("ASSET",a.Id,a.State,kind=="USER"&&a.State is "IN_USE" or "UNDER_REPAIR",a);
        if(kind=="DEPARTMENT")foreach(var a in await db.Assets.AsNoTracking().Where(a=>a.CurrentDepartmentId==id&&a.BranchId!=branch)
            .Select(a=>new{Id=a.AssetId,a.BranchId,a.LifecycleStatus,a.CurrentDepartmentId,a.CustodianUserId,a.UpdatedAt}).ToArrayAsync(ct))
            Add("ASSET_SCOPE_MISMATCH",a.Id,a.LifecycleStatus,version:a);
        if(kind is "USER" or "DEPARTMENT" or "BRANCH")
        {
            var handovers=db.AssetHandovers.AsNoTracking().Where(h=>h.Asset.BranchId==branch);
            if(kind=="USER")handovers=Dev2DependencyQueries.PendingUserHandovers(handovers,id);
            if(kind=="DEPARTMENT")handovers=Dev2DependencyQueries.DepartmentHandovers(db.AssetHandovers.AsNoTracking(),id);
            foreach(var h in await handovers.Select(h=>new{Id=h.HandoverId,h.HandoverStatus,h.ToUserId,h.FromUserId,h.ToDepartmentId,h.FromDepartmentId,h.AssetId,h.AcceptedAt,AssetBranch=h.Asset.BranchId}).ToArrayAsync(ct))
                Add(h.AssetBranch==branch?"ASSET_HANDOVER":"ASSET_HANDOVER_SCOPE_MISMATCH",h.Id,h.HandoverStatus,kind=="USER"&&h.HandoverStatus=="PENDING",h);
        }
        var requests=db.MaterialRequests.AsNoTracking().Where(r=>r.BranchId==branch);
        if(kind=="USER")requests=Dev2DependencyQueries.UserRequests(requests,id);
        if(kind=="ROOM")requests=Dev2DependencyQueries.RoomRequests(db,branch,id);
        if(kind=="DEPARTMENT")requests=requests.Where(r=>r.DepartmentId==id);
        foreach(var r in await requests.Select(r=>new{Id=r.RequestId,r.Status,r.RequesterId,r.DepartmentId,r.SourceWarehouseId,r.ReceivingRoomId,r.ReceivingAreaId,r.IncidentId,r.RowVersion}).ToArrayAsync(ct))Add("MATERIAL_REQUEST",r.Id,r.Status,kind=="USER"&&r.Status is "DRAFT" or "NEEDS_INFORMATION",r);
        var fulfillment=db.MaterialFulfillments.AsNoTracking().Where(f=>f.Request.BranchId==branch);
        if(kind=="USER")fulfillment=fulfillment.Where(f=>f.FulfilledBy==id||f.ReceivedBy==id);
        if(kind=="ROOM")fulfillment=fulfillment.Where(f=>requests.Any(r=>r.RequestId==f.RequestId));
        if(kind=="DEPARTMENT")fulfillment=Dev2DependencyQueries.DepartmentFulfillments(db.MaterialFulfillments.AsNoTracking(),branch,id);
        foreach(var f in await fulfillment.Select(f=>new{Id=f.FulfillmentId,f.FulfilledBy,f.ReceivedBy,f.ReceivedAt,f.Status,f.RequestId,f.WarehouseId,f.StockDocumentId,RequestBranch=f.Request.BranchId,WarehouseBranch=f.Warehouse.BranchId}).ToArrayAsync(ct))
        {
            if(kind=="USER"&&f.FulfilledBy==id)Add("HISTORICAL_ACTOR",f.Id,"MATERIAL_FULFILLMENT_AUTHOR");
            if(kind!="USER"||f.ReceivedBy==id)Add(f.RequestBranch==branch&&f.WarehouseBranch==branch?"MATERIAL_FULFILLMENT":"FULFILLMENT_SCOPE_MISMATCH",f.Id,f.Status,version:f);
        }
        unresolved.Add("MATERIAL_FULFILLMENT_RECEIPT_POLICY_REQUIRED");
        if(kind=="USER")foreach(var r in await db.MaterialReplenishments.AsNoTracking().Where(r=>r.Warehouse.BranchId==branch&&r.RequestedBy==id)
            .Select(r=>r.ReplenishmentId).ToArrayAsync(ct))Add("HISTORICAL_ACTOR",r,"REPLENISHMENT_REQUESTER_NOT_ASSIGNEE");
        if(kind!="ROOM")
        {
            var warehouses=db.Warehouses.AsNoTracking().Where(w=>w.BranchId==branch);
            if(kind=="USER")warehouses=Dev2DependencyQueries.UserWarehouses(warehouses,id);
            if(kind=="DEPARTMENT")warehouses=warehouses.Where(w=>w.DepartmentId==id);
            foreach(var w in await warehouses.Select(w=>new{Id=w.WarehouseId,w.IsActive,w.ManagerUserId,w.DepartmentId,w.RowVersion}).ToArrayAsync(ct))Add("WAREHOUSE",w.Id,w.IsActive?"ACTIVE":"INACTIVE",kind=="USER"&&w.IsActive,w);
            // Do not net unlike items, even when warehouse sum is zero.
            if(kind!="USER")foreach(var w in await db.StockMovements.AsNoTracking().Where(m=>warehouses.Any(w=>w.WarehouseId==m.WarehouseId))
                .GroupBy(m=>new{m.WarehouseId,m.ItemId}).Where(g=>g.Sum(m=>m.SignedBaseQty)!=0).Select(g=>new{g.Key.WarehouseId,g.Key.ItemId,Quantity=g.Sum(m=>m.SignedBaseQty)}).ToArrayAsync(ct))Add("NONZERO_STOCK",w.WarehouseId,"PER_ITEM_NONZERO",version:w);
            if(kind!="USER")foreach(var r in await db.MaterialReplenishments.AsNoTracking().Where(r=>warehouses.Any(w=>w.WarehouseId==r.WarehouseId)).Select(r=>new{Id=r.ReplenishmentId,r.Status,r.WarehouseId,r.RowVersion}).ToArrayAsync(ct))Add("REPLENISHMENT",r.Id,r.Status,version:r);
        }
        if(kind is "BRANCH" or "ROOM")foreach(var i in await (kind=="ROOM"?Dev2DependencyQueries.RoomIncidents(db,branch,id):db.Incidents.AsNoTracking().Where(i=>i.BranchId==branch))
            .Select(i=>new{Id=i.IncidentId,i.Status,i.RoomId,i.AreaId,i.AssetId,i.ResolvedAt,i.ClosedAt}).ToArrayAsync(ct))Add("INCIDENT",i.Id,i.Status,version:i);
        if(kind!="USER")
        {
            var schedules=Dev2DependencyQueries.Schedules(db.PreventiveMaintenanceSchedules.AsNoTracking(),branch);
            if(kind=="ROOM")schedules=schedules.Where(s=>s.RoomId==id||s.Asset!=null&&s.Asset.CurrentRoomId==id);
            if(kind=="DEPARTMENT")schedules=schedules.Where(s=>s.AssignedDepartmentId==id);
            if(kind=="DEPARTMENT")foreach(var s in await db.PreventiveMaintenanceSchedules.AsNoTracking()
                .Where(s=>s.AssignedDepartmentId==id&&!schedules.Any(v=>v.ScheduleId==s.ScheduleId))
                .Select(s=>new{Id=s.ScheduleId,s.AssetId,s.RoomId,s.AreaId,s.AssignedDepartmentId,s.IsActive}).ToArrayAsync(ct))
                Add("PM_DEPARTMENT_SOURCE_UNRESOLVED",s.Id,s.IsActive?"ACTIVE":"INACTIVE",version:s);
            foreach(var s in await schedules.Select(s=>new{Id=s.ScheduleId,s.IsActive,s.AssignedDepartmentId,s.AssetId,s.RoomId,s.AreaId,s.NextDueAt}).ToArrayAsync(ct))Add("PM_SCHEDULE",s.Id,s.IsActive?"ACTIVE":"INACTIVE",version:s);
            foreach(var r in await db.PreventiveMaintenanceRuns.AsNoTracking().Where(r=>schedules.Any(s=>s.ScheduleId==r.ScheduleId))
                .Select(r=>new{Id=r.RunId,r.PerformedAt,r.ScheduleId,r.MaintenanceWorkId,r.PlannedAt,
                    WorkStatus=r.MaintenanceWork==null?null:r.MaintenanceWork.Status,
                    Assignee=r.MaintenanceWork==null?(long?)null:r.MaintenanceWork.AssigneeId}).ToArrayAsync(ct)){
                Add("PM_RUN",r.Id,r.PerformedAt==null?"UNPERFORMED":"PERFORMED",version:r);
                if(r.MaintenanceWorkId is long linked)Add("PM_LINKED_MAINTENANCE",linked,r.WorkStatus??"MISSING_SOURCE",version:r);
            }
            if(kind=="BRANCH")foreach(var s in await Dev2DependencyQueries.AmbiguousSchedules(db.PreventiveMaintenanceSchedules.AsNoTracking(),branch)
                .Select(s=>new{Id=s.ScheduleId,s.AssetId,s.RoomId,s.AreaId,s.AssignedDepartmentId,s.IsActive}).ToArrayAsync(ct))
                Add("PM_AMBIGUOUS_SOURCE",s.Id,s.IsActive?"ACTIVE":"INACTIVE",version:s);
        }
        unresolved.Add("PM_AMBIGUOUS_SOURCE_AND_ORPHAN_WORK_COVERAGE_REQUIRED");
        unresolved.Add("NOTIFICATION_ROUTING_DEACTIVATION_POLICY_REQUIRED");
        if(kind=="BRANCH")unresolved.Add("NOTIFICATION_FOUNDATION_NOT_PRESENT_IN_SELECTED_BASE");
        if(kind=="ROOM")unresolved.Add("DEV1_CURRENT_CYCLE_CLEANING_INSPECTION_ROOMBLOCK_REQUIRED");
        // Reauthorize after all reads. Never leak a partial snapshot on cancellation/failure.
        await AuthorizeReadAsync(kind,id,branch,globalAuthority,ct);
        return Canonical(kind,id,branch,facts,unresolved);
        void Add(string category,long record,string state,bool approved=false,object? version=null)=>facts.Add(new(category,record,branch,state,Dev2DependencyQueries.Classify(category,state,approved),version is null?null:SourceFingerprint(version)));
    }
    public static string SourceFingerprint(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static DependencySnapshot Canonical(string kind,long id,long branch,IEnumerable<DependencyFact> facts,IEnumerable<string> unresolved)
    {
        if(kind is not ("USER" or "BRANCH" or "ROOM" or "DEPARTMENT")||id<=0||branch<=0||kind=="BRANCH"&&id!=branch)throw new DependencyScopeException();
        var ordered=facts.Distinct().OrderBy(f=>f.Category,StringComparer.Ordinal).ThenBy(f=>f.RecordId).ThenBy(f=>f.State,StringComparer.Ordinal).ThenBy(f=>f.Classification).ThenBy(f=>f.SourceFingerprint,StringComparer.Ordinal).ToArray();
        if(ordered.Any(f=>f.BranchId!=branch))throw new DependencyScopeException();
        var gaps=unresolved.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{kind,id,branch,facts=ordered,gaps})));
        return new(kind,id,branch,Array.AsReadOnly(ordered),Array.AsReadOnly(gaps),hash);
    }
}
