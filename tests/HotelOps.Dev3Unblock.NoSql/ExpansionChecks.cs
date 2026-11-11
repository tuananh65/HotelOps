using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dependencies;
using Microsoft.EntityFrameworkCore;

internal static class ExpansionChecks
{
    internal static async Task<IReadOnlyList<string>> RunAsync(HotelOpsDbContext db)
    {
        var checks=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Deny<T>(Func<Task> run,string name)where T:Exception
        {try{await run();}catch(T){checks.Add(name);return;}throw new Exception("Expected "+typeof(T).Name+": "+name);}
        var clock=TimeProvider.System;
        var now=DateTime.SpecifyKind(clock.GetUtcNow().UtcDateTime.AddMinutes(-1),DateTimeKind.Utc);
        var reference=HotelOps.Api.Services.Authentication.SessionReference.FromProtectedTicket(9,
            HotelOps.Api.Services.Authentication.SessionCredential.Generate(),false,now);
        var session=new HotelOps.Api.Services.Authentication.SessionSnapshot(1,9,now,now.AddMinutes(30),now.AddHours(12),false);
        var cookie=HotelOps.Api.Security.HotelOpsCookieTicket.Create(new(reference,session),clock);
        var http=new Microsoft.AspNetCore.Http.HttpContextAccessor{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext{User=cookie.Principal}};
        var authority=new Dev2PlacementAuthority(db,new Identity(new(9,1,false)),http,null!,clock);
        Check(authority.Actor(9)==9,"Writer actor is bound to exact protected cookie/current identity");
        await Deny<Dev2PlacementDeniedException>(()=>Task.Run(()=>authority.Actor(10)),"Writer refuses supplied actor spoofing");
        await Deny<Dev2PlacementDeniedException>(()=>Task.Run(()=>new Dev2PlacementAuthority(db,new Identity(new(9,2,false)),http,null!,clock).Actor(9)),"Writer refuses mismatched current/cookie session ID");
        await Deny<Dev2PlacementDeniedException>(()=>Task.Run(()=>new Dev2PlacementAuthority(db,new Identity(new(9,1,true)),http,null!,clock).Actor(9)),"Restricted actor cannot invoke placement writer");
        await Deny<InvalidOperationException>(()=>authority.RequireAsync(9,7,"ASSET_MANAGE",default),"Valid identity alone cannot bypass caller transaction before SQL authorization");
        var first=Dev2DependencyReader.SourceFingerprint(new{AssigneeId=7,DepartmentId=11,IncidentId=13});
        var next=Dev2DependencyReader.SourceFingerprint(new{AssigneeId=8,DepartmentId=11,IncidentId=13});
        Check(first!=next,"Same-state reassignment changes source fingerprint");
        var fact=new DependencyFact("MAINTENANCE",1,7,"IN_PROGRESS",DependencyClassification.UNKNOWN_POLICY,first);
        var a=Dev2DependencyReader.Canonical("BRANCH",7,7,[fact],[]);
        var b=Dev2DependencyReader.Canonical("BRANCH",7,7,[fact with{SourceFingerprint=next}],[]);
        await Deny<DependencySnapshotConflictException>(()=>Task.Run(()=>Dev2DependencyReader.RequireUnchanged(a,b)),"Snapshot conflict detects changed assignment with identical ID/status");
        Check(a.Fingerprint==Dev2DependencyReader.Canonical("BRANCH",7,7,[fact],[]).Fingerprint,"Source-bound snapshot stable on identical retry");
        var item=fact with{Category="NONZERO_STOCK",RecordId=2};
        Check(Dev2DependencyReader.Canonical("BRANCH",7,7,[item,fact],[]).Fingerprint==Dev2DependencyReader.Canonical("BRANCH",7,7,[fact,item],[]).Fingerprint,"Source-bound facts remain deterministically sorted");
        Check(Dev2DependencyReader.SourceFingerprint(new{RoomId=9,FloorId=3,AreaId=4,IsActive=true})!=
            Dev2DependencyReader.SourceFingerprint(new{RoomId=9,FloorId=3,AreaId=5,IsActive=true}),"Parent relocation changes fingerprint even while active");
        Check(Dev2DependencyReader.SourceFingerprint(new{WarehouseId=1,ItemId=2,Quantity=3m})!=
            Dev2DependencyReader.SourceFingerprint(new{WarehouseId=1,ItemId=2,Quantity=4m}),"Per-item stock quantity changes snapshot identity");
        Check(!a.CanDeactivate&&a.Coordination.Contains("FINAL_OWNER"),"Known facts never remove final owner coordination fuse");
        var requestSql=Dev2DependencyQueries.RoomRequests(db,7,9).ToQueryString();
        Check(requestSql.Contains("receiving_room_id")&&requestSql.Contains("incident_id")&&requestSql.Contains("current_room_id"),"Room requests include actual receiving-room and linked Incident/Asset relationships");
        var workSql=Dev2DependencyQueries.RoomMaintenance(db,7,9).ToQueryString();
        Check(workSql.Contains("preventive_maintenance_runs")&&workSql.Contains("incident_id"),"Room Maintenance includes corrective and supported PM-run links");
        Check(Dev2DependencyQueries.ScopeMismatches(db.MaintenanceWorks,7).ToQueryString().Contains("branch_id"),"Cross-branch inconsistent Maintenance references are visible as unresolved facts");
        var ambiguous=new[]{new PreventiveMaintenanceSchedule{ScheduleId=1,RoomId=1,AreaId=2,Room=new(){BranchId=7},Area=new(){BranchId=7}},
            new PreventiveMaintenanceSchedule{ScheduleId=2,AssignedDepartment=new(){BranchId=7}},
            new PreventiveMaintenanceSchedule{ScheduleId=3,RoomId=1,Room=new(){BranchId=7}}}.AsQueryable();
        Check(Dev2DependencyQueries.AmbiguousSchedules(ambiguous,7).Select(s=>s.ScheduleId).SequenceEqual(new long[]{1,2}),"Ambiguous multi-source and department-scoped orphan PM schedules remain visible");
        Check(Dev2DependencyQueries.AmbiguousSchedules(db.PreventiveMaintenanceSchedules,7).ToQueryString().Contains("assigned_department_id"),"Ambiguous PM source query translates against actual mapped department relationship");
        var rows=new[]{new MaterialFulfillment{FulfillmentId=1,Request=new(){BranchId=7,DepartmentId=8},Warehouse=new(){BranchId=7,DepartmentId=11}},
            new MaterialFulfillment{FulfillmentId=2,Request=new(){BranchId=7,DepartmentId=8},Warehouse=new(){BranchId=9,DepartmentId=11}}}.AsQueryable();
        Check(Dev2DependencyQueries.DepartmentFulfillments(rows,7,11).Single().FulfillmentId==1,"Department fulfillment scope includes owned Warehouse but rejects foreign Warehouse");
        var crossRequest=new[]{new MaterialFulfillment{FulfillmentId=3,Request=new(){BranchId=9,DepartmentId=8},Warehouse=new(){BranchId=7,DepartmentId=11}}}.AsQueryable();
        Check(Dev2DependencyQueries.DepartmentFulfillments(crossRequest,7,11).Any(),"Local Warehouse reference cannot disappear behind a mismatched foreign Request branch");
        Check(Dev2DependencyQueries.DepartmentHandovers(new[]{new AssetHandover{FromDepartmentId=11,ToDepartmentId=12},new AssetHandover{FromDepartmentId=8,ToDepartmentId=11}}.AsQueryable(),11).Count()==2,"Department handover links retain both current destination and historical source references");
        var reader=new Dev2DependencyReader(db,new NoActor(),null!,null!);
        await Deny<DependencyScopeException>(()=>reader.ReadUserGlobalAsync(1,default),"Global user review rejects anonymous identity before SQL");
        await Deny<InvalidOperationException>(()=>reader.ReadUserGlobalInCallerTransactionAsync(1,default),"Global user review requires owner transaction before lock access");
        await Deny<InvalidOperationException>(()=>reader.RevalidateUserGlobalInCallerTransactionAsync(1,null!,default),"Global user reread requires matching target/transaction lease");
        await Deny<OperationCanceledException>(()=>reader.ReadUserGlobalAsync(1,new(true)),"Global review cancellation never returns partial success");
        Check(db.Database.GetDbConnection().State==System.Data.ConnectionState.Closed,"Expanded SQL translation and negative checks open zero SQL connections");
        return checks;
    }
    private sealed class NoActor:ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo u){u=default;return false;}}
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo u){u=value;return true;}}
}
