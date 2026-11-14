using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintDashboardScenario
{
    // The outer auth probe verifies exact disposable instance/GUID/run identity before mutation.
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, List<object> checks, CancellationToken ct)
    {
        if(await db.Branches.AnyAsync(ct)||await db.RoomTypes.AnyAsync(ct))throw new Exception("Empty dashboard fixture required");
        var prefix="D3-DASH-"+Guid.NewGuid().ToString("N")[..8];
        var own=new Branch {BranchCode=prefix+"A",BranchName="Synthetic permitted dashboard",IsActive=true};
        var foreign=new Branch {BranchCode=prefix+"B",BranchName="Synthetic foreign dashboard",IsActive=true};
        var empty=new Branch {BranchCode=prefix+"C",BranchName="Synthetic empty dashboard",IsActive=true};
        var type=new RoomType {TypeCode=prefix,TypeName="Synthetic type",StandardCapacity=1,IsActive=true};
        long[] branches=[];
        var inventory=new SprintDashboardInventory(db,prefix,actor);
        async Task<JsonElement> Get(string path,int expected)
        {
            using var response=await http.GetAsync(path,ct);
            checks.Add(new {dashboardPath=path,expected,actual=(int)response.StatusCode});
            if((int)response.StatusCode!=expected)throw new Exception("Dashboard HTTP status mismatch: "+path+" actual="+(int)response.StatusCode);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return json.RootElement.Clone();
        }
        static JsonElement Metric(JsonElement result,string key)=>result.GetProperty("metrics").EnumerateArray().Single(m=>m.GetProperty("key").GetString()==key).GetProperty("value");
        try
        {
            await Get("/api/dashboards/operations",403);
            db.Branches.AddRange(own,foreign,empty);db.RoomTypes.Add(type);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            branches=[own.BranchId,foreign.BranchId,empty.BranchId];
            var ownFloor=new Floor {BranchId=own.BranchId,FloorCode="F1",FloorName="Synthetic floor",IsActive=true};
            var foreignFloor=new Floor {BranchId=foreign.BranchId,FloorCode="F1",FloorName="Foreign floor",IsActive=true};
            var ownDept=new Department {BranchId=own.BranchId,DepartmentCode="HK",DepartmentName="Synthetic HK",IsActive=true};
            var foreignDept=new Department {BranchId=foreign.BranchId,DepartmentCode="HK",DepartmentName="Foreign HK",IsActive=true};
            db.Floors.AddRange(ownFloor,foreignFloor);db.Departments.AddRange(ownDept,foreignDept);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            db.Rooms.AddRange(
                new Room {BranchId=own.BranchId,FloorId=ownFloor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="D1",OperationalStatus="READY",IsActive=true},
                new Room {BranchId=own.BranchId,FloorId=ownFloor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="D2",OperationalStatus="DIRTY",IsActive=true},
                new Room {BranchId=own.BranchId,FloorId=ownFloor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="D3",OperationalStatus="READY",IsActive=false},
                new Room {BranchId=foreign.BranchId,FloorId=foreignFloor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="D1",OperationalStatus="READY",IsActive=true});
            db.OperationalTasks.AddRange(
                new OperationalTask {TaskCode=prefix+"A",BranchId=own.BranchId,DepartmentId=ownDept.DepartmentId,Title="Synthetic task",Status="TODO",CreatedBy=actor,CreatedAt=DateTime.UtcNow},
                new OperationalTask {TaskCode=prefix+"B",BranchId=foreign.BranchId,DepartmentId=foreignDept.DepartmentId,Title="Foreign task",Status="COMPLETED",CreatedBy=actor,CreatedAt=DateTime.UtcNow});
            var incident=new Incident {IncidentNo=prefix+"A",BranchId=own.BranchId,IncidentTitle="Synthetic incident",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
            db.Incidents.AddRange(incident,new Incident {IncidentNo=prefix+"B",BranchId=foreign.BranchId,IncidentTitle="Foreign incident",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            db.MaintenanceWorks.Add(new MaintenanceWork {WorkNo=prefix,IncidentId=incident.IncidentId,DepartmentId=ownDept.DepartmentId,Title="Synthetic maintenance",Status="PENDING",CreatedBy=actor,CreatedAt=DateTime.UtcNow});
            var role=await db.Roles.Where(r=>r.RoleCode=="OPERATIONS_MANAGER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            foreach(var branch in new[]{own.BranchId,empty.BranchId})
            {
                db.UserRoles.Add(new UserRole {UserId=actor,RoleId=role,BranchId=branch,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});
                db.UserBranchAccesses.Add(new UserBranchAccess {UserId=actor,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            }
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await inventory.SeedAsync(own.BranchId,foreign.BranchId,ownDept.DepartmentId,ct);
            await inventory.VerifyAsync(Get,own.BranchId,ownDept.DepartmentId,checks);
            var data=await Get("/api/dashboards/operations?branchId="+own.BranchId,200);
            if(Metric(data,"activeRooms").GetInt32()!=2||Metric(data,"readyRooms").GetInt32()!=1||Metric(data,"readyRatio").GetDecimal()!=50||Metric(data,"openIncidents").GetInt32()!=1)
                throw new Exception("Dashboard counts leaked foreign/inactive records");
            if(Metric(data,"firstInspectionPassRate").ValueKind!=JsonValueKind.Null||data.GetProperty("missingSources").GetArrayLength()==0)
                throw new Exception("Unavailable metric fabricated");
            if(data.GetProperty("distributions").EnumerateArray().Single(d=>d.GetProperty("source").GetString()=="maintenance_works.status").GetProperty("count").GetInt32()!=1)
                throw new Exception("Maintenance branch projection missing");
            var aggregate=await Get("/api/dashboards/operations",200);
            if(Metric(aggregate,"activeRooms").GetInt32()!=2)throw new Exception("Aggregate branch scope leaked");
            var department=await Get($"/api/dashboards/operations?branchId={own.BranchId}&departmentId={ownDept.DepartmentId}",200);
            if(Metric(department,"activeRooms").ValueKind!=JsonValueKind.Null||Metric(department,"openIncidents").ValueKind!=JsonValueKind.Null)
                throw new Exception("Department-unmapped figures were not withheld");
            var zero=await Get("/api/dashboards/operations?branchId="+empty.BranchId,200);
            if(Metric(zero,"activeRooms").GetInt32()!=0||Metric(zero,"readyRatio").ValueKind!=JsonValueKind.Null)throw new Exception("Zero denominator mishandled");
            var options=await Get("/api/dashboards/options",200);
            if(options.GetProperty("branches").EnumerateArray().Any(b=>b.GetProperty("id").GetInt64()==foreign.BranchId))throw new Exception("Foreign branch option leaked");
            await Get("/api/dashboards/operations?branchId="+foreign.BranchId,403);
            await Get($"/api/dashboards/operations?branchId={own.BranchId}&departmentId={foreignDept.DepartmentId}",403);
            await Get("/api/dashboards/operations?branchId=0",400);
            await Get("/api/dashboards/operations?from=2026-11-15&to=2026-11-14",400);
            await Get("/api/dashboards/operations?to=9999-12-31",400);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await Get("/api/dashboards/operations?branchId="+own.BranchId,403);
            checks.Add(new {scopedDashboardCounts=true,unknownMetricsRemainNull=true,departmentAndZeroDenominator=true});
        }
        finally
        {
            db.ChangeTracker.Clear();await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);
            await inventory.CleanupAsync(CancellationToken.None);
            await db.MaintenanceWorks.Where(w=>w.WorkNo==prefix&&w.CreatedBy==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.Incidents.Where(i=>branches.Contains(i.BranchId)&&i.IncidentNo.StartsWith(prefix)&&i.ReportedBy==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.OperationalTasks.Where(t=>branches.Contains(t.BranchId)&&t.TaskCode.StartsWith(prefix)&&t.CreatedBy==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.Rooms.Where(r=>branches.Contains(r.BranchId)&&r.RoomTypeId==type.RoomTypeId).ExecuteDeleteAsync(CancellationToken.None);
            await db.Floors.Where(f=>branches.Contains(f.BranchId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Departments.Where(d=>branches.Contains(d.BranchId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor&&r.BranchId!=null&&branches.Contains(r.BranchId.Value)).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&branches.Contains(a.BranchId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Branches.Where(b=>branches.Contains(b.BranchId)&&b.BranchCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.RoomTypes.Where(t=>t.RoomTypeId==type.RoomTypeId&&t.TypeCode==prefix).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None);
            if(await db.Branches.AnyAsync()||await db.Rooms.AnyAsync()||await db.OperationalTasks.AnyAsync()||await db.Incidents.AnyAsync()||await db.MaintenanceWorks.AnyAsync())throw new Exception("Dashboard fixture cleanup failed");
            checks.Add(new {dashboardFixtureCleanup=true});
        }
    }
}
