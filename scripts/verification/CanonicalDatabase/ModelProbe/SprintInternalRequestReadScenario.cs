using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestReadScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, List<object> checks, CancellationToken ct, string username, string password)
    {
        // Called only after SprintAuthProbe's exact disposable identity and empty-fixture checks.
        var prefix = "D3IR" + Guid.NewGuid().ToString("N")[..8];
        var branches = new[] { new Branch { BranchCode=prefix+"A", BranchName="Synthetic A", IsActive=true },
            new Branch { BranchCode=prefix+"B", BranchName="Synthetic B", IsActive=true } };
        var other = new User { EmployeeCode=prefix, Username=prefix, Email=prefix+"@example.invalid", FullName="Synthetic requester",
            AccountStatus="ACTIVE", EmploymentStatus="ACTIVE", CreatedAt=DateTime.UtcNow };
        var type = new InternalRequestType { TypeCode=prefix, TypeName="Synthetic history", IsActive=true };
        var createdPermission = !await db.Permissions.AnyAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        var addedGrants = new List<long>();
        long[] ids=[];
        void Check(bool condition,string label) { if(!condition)throw new Exception(label);checks.Add(new{internalRequestReadCheck=label}); }
        async Task<JsonElement> Get(string path,int status=200)
        {
            using var response=await http.GetAsync(path,ct);
            Check((int)response.StatusCode==status,$"{path}: {status}");
            return status==200 ? await response.Content.ReadFromJsonAsync<JsonElement>(ct) : default;
        }
        try
        {
            await Get("/api/internal-requests",403);
            var seed=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/InternalRequestViewIsolated.sql",ct);
            await db.Database.ExecuteSqlRawAsync(seed,ct);await db.Database.ExecuteSqlRawAsync(seed,ct);
            var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_VIEW",ct);
            Check(await db.Permissions.CountAsync(p=>p.PermissionCode=="IR_VIEW",ct)==1,"catalog seed idempotent");
            db.Branches.AddRange(branches);db.Users.Add(other);db.InternalRequestTypes.Add(type);await db.SaveChangesAsync(ct);
            var branch=branches[0].BranchId;var wrong=branches[1].BranchId;
            var departments=new[]{new Department{BranchId=branch,DepartmentCode=prefix+"A",DepartmentName="Synthetic A",IsActive=true},
                new Department{BranchId=branch,DepartmentCode=prefix+"B",DepartmentName="Synthetic B",IsActive=true}};
            db.Departments.AddRange(departments);await db.SaveChangesAsync(ct);
            db.UserBranchAccesses.Add(new(){UserId=actor,BranchId=branch,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            var rows=Enumerable.Range(0,27).Select(i=>new InternalRequest {RequestNo=prefix+i,BranchId=i==3?wrong:branch,
                RequestTypeId=type.RequestTypeId,RequesterId=i==0?actor:other.UserId,ReceivingDepartmentId=i==3?null:departments[0].DepartmentId,
                Subject="Synthetic history "+i,Description="Synthetic only",Status="SUBMITTED",CreatedAt=DateTime.UtcNow,DueAt=DateTime.UtcNow.AddHours(i-2)}).ToArray();
            db.InternalRequests.AddRange(rows);await db.SaveChangesAsync(ct);ids=rows.Select(r=>r.InternalRequestId).ToArray();
            db.InternalRequestAssignments.Add(new(){InternalRequestId=ids[1],AssigneeId=actor,AssignedBy=other.UserId,AssignedAt=DateTime.UtcNow.AddMinutes(-1),Status="ACTIVE"});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            async Task Role(string code,string scope)
            {
                await db.UserRoles.Where(r=>r.UserId==actor).ExecuteDeleteAsync(ct);
                var role=await db.Roles.SingleAsync(r=>r.RoleCode==code&&r.IsActive,ct);
                if(await db.RolePermissions.AnyAsync(p=>p.RoleId==role.RoleId&&p.PermissionId==permission.PermissionId,ct))
                    throw new Exception("Pre-existing grant must not be overwritten by fixture");
                var grant=new RolePermission{RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope=scope,GrantedAt=DateTime.UtcNow};
                db.RolePermissions.Add(grant);db.UserRoles.Add(new(){UserId=actor,RoleId=role.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
                await db.SaveChangesAsync(ct);addedGrants.Add(grant.RolePermissionId);db.ChangeTracker.Clear();
            }
            await Role("HK_STAFF","OWN");
            var own=await Get("/api/internal-requests");
            Check(own.GetProperty("totalCount").GetInt32()==2,"requester and current handler see exactly related requests");
            await Get($"/api/internal-requests/{ids[2]}/timeline",404);await Get($"/api/internal-requests/{ids[3]}/timeline",404);
            await Get($"/api/internal-requests?branchId={wrong}",403);
            await Get($"/api/internal-requests/{ids[1]}/timeline");
            checks.Add(new { internalRequestBrowser = await SprintInternalRequestBrowser.RunAsync(username,password,ids[1],ids[3],"related",ct) });
            await db.InternalRequests.Where(r=>r.InternalRequestId==ids[1]).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,departments[1].DepartmentId),ct);
            await Get($"/api/internal-requests/{ids[1]}/timeline");
            await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==ids[1]).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.EndedAt,DateTime.UtcNow),ct);
            await Get($"/api/internal-requests/{ids[1]}/timeline",404);
            await Role("DEPT_MANAGER","BRANCH");
            var manager=await Get("/api/internal-requests?pageSize=2");
            Check(manager.GetProperty("totalCount").GetInt32()==26&&manager.GetProperty("items").GetArrayLength()==2,"DH branch scope precedes count and pagination without department inference");
            checks.Add(new { internalRequestBrowser = await SprintInternalRequestBrowser.RunAsync(username,password,ids[1],ids[3],"manager",ct) });
            await Get($"/api/internal-requests/{ids[1]}/timeline");
            await Get($"/api/internal-requests?branchId={wrong}",403);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await Get("/api/internal-requests",403);
            checks.Add(new { internalRequestBrowser = await SprintInternalRequestBrowser.RunAsync(username,password,ids[1],ids[3],"denied",ct) });
            await Role("OPERATIONS_MANAGER","BRANCH");
            Check((await Get("/api/internal-requests")).GetProperty("totalCount").GetInt32()==26,"Operations Manager scoped read");
            await Role("SYS_ADMIN","BRANCH");
            Check((await Get("/api/internal-requests")).GetProperty("totalCount").GetInt32()==26,"Administrator explicit scoped grant");
            await Get($"/api/internal-requests/{ids[3]}/timeline",404);
            await Get("/api/internal-requests?pageSize=101",400);
            await db.RolePermissions.Where(p=>addedGrants.Contains(p.RolePermissionId)).ExecuteDeleteAsync(ct);
            await Get("/api/internal-requests",403);
            Check(await db.InternalRequests.CountAsync(r=>ids.Contains(r.InternalRequestId)&&r.Status=="SUBMITTED",ct)==27,"read endpoints leave business status unchanged");
        }
        finally
        {
            db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(CancellationToken.None);
            await db.InternalRequestAssignments.Where(a=>ids.Contains(a.InternalRequestId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequests.Where(r=>r.RequestNo.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestTypes.Where(t=>t.TypeCode==prefix).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.RolePermissions.Where(p=>addedGrants.Contains(p.RolePermissionId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&branches.Select(b=>b.BranchId).Contains(a.BranchId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Departments.Where(d=>d.DepartmentCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Branches.Where(b=>b.BranchCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u=>u.Username==prefix).ExecuteDeleteAsync(CancellationToken.None);
            if(createdPermission)await db.Permissions.Where(p=>p.PermissionCode=="IR_VIEW").ExecuteDeleteAsync(CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);checks.Add(new{internalRequestReadCleanup=true});
        }
    }
}
