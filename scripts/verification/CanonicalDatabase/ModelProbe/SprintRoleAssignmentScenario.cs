using System.Net.Http.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintRoleAssignmentScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,string csrf,List<object> checks,CancellationToken ct)
    {
        if(await db.Branches.AnyAsync(ct)||await db.UserRoles.AnyAsync(ct))throw new Exception("Empty synthetic branch/grant baseline required");
        var prefix="D3-ROLE-"+Guid.NewGuid().ToString("N")[..8];
        var branch=new Branch {BranchCode=prefix,BranchName="Synthetic role branch",IsActive=true};
        var department=new Department {DepartmentCode="HK",DepartmentName="Synthetic housekeeping",IsActive=true};
        var target=new User {EmployeeCode=prefix,Username=prefix,Email=prefix+"@example.invalid",FullName="Synthetic role target",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        async Task<string?> Post(int expected,string? tag,bool token=true,string role="HK_STAFF")
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,$"/api/users/{target.UserId}/roles")
            {Content=JsonContent.Create(new {roleCode=role,branchId=branch.BranchId})};
            if(token)request.Headers.Add("X-CSRF-TOKEN",csrf);
            if(tag!=null)request.Headers.TryAddWithoutValidation("If-Match",tag);
            using var response=await http.SendAsync(request,ct);
            checks.Add(new {roleAssignmentExpected=expected,actual=(int)response.StatusCode,hasIfMatch=tag!=null,hasCsrf=token});
            if((int)response.StatusCode!=expected)throw new Exception("Role assignment response mismatch: "+(int)response.StatusCode);
            return response.Headers.ETag?.Tag;
        }
        try
        {
            db.Branches.Add(branch);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            department.BranchId=branch.BranchId;db.Departments.Add(department);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            target.PrimaryBranchId=branch.BranchId;target.DepartmentId=department.DepartmentId;
            db.Users.Add(target);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            db.UserBranchAccesses.Add(new UserBranchAccess {UserId=target.UserId,BranchId=branch.BranchId,AccessLevel="STANDARD",IsPrimary=true,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var version=await db.Users.Where(u=>u.UserId==target.UserId).Select(u=>u.RowVersion).SingleAsync(ct);
            var tag='"'+Convert.ToHexString(version)+'"';
            await Post(400,tag,false);await Post(428,null);await Post(403,tag);
            var admin=await db.Roles.Where(r=>r.RoleCode=="SYS_ADMIN"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            db.UserRoles.Add(new UserRole {UserId=actor,RoleId=admin,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            using(var profile=await http.GetAsync($"/api/users/{target.UserId}/profile",ct))
            {
                if((int)profile.StatusCode!=200||profile.Headers.ETag?.Tag!=tag)
                    throw new Exception("Management profile did not provide the current assignment ETag");
                using var body=System.Text.Json.JsonDocument.Parse(await profile.Content.ReadAsStringAsync(ct));
                if(body.RootElement.GetProperty("id").GetInt64()!=target.UserId)
                    throw new Exception("Management profile target mismatch");
                checks.Add(new {managementProfileStatus=200,currentEtag=true,targetMatches=true});
            }
            await Post(403,tag,role:"SYS_ADMIN");await Post(412,"\"0000000000000000\"");
            var next=await Post(201,tag);
            var assigned=await db.UserRoles.AsNoTracking().SingleAsync(r=>r.UserId==target.UserId,ct);
            var roleCode=await db.Roles.Where(r=>r.RoleId==assigned.RoleId).Select(r=>r.RoleCode).SingleAsync(ct);
            var persisted='"'+Convert.ToHexString(await db.Users.Where(u=>u.UserId==target.UserId).Select(u=>u.RowVersion).SingleAsync(ct))+'"';
            if(next==null||next==tag||next!=persisted||roleCode!="HK_STAFF"||assigned.BranchId!=branch.BranchId||assigned.AssignedBy!=actor||!assigned.IsActive)
                throw new Exception("Persisted assignment/revision mismatch");
            var entityId=assigned.UserRoleId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(await db.AuditLogs.CountAsync(a=>a.EntityType=="user_roles"&&a.EntityId==entityId&&a.ActorUserId==actor,ct)!=1)
                throw new Exception("Role assignment audit missing/duplicated");
            await Post(412,tag);await Post(409,next);
            if(await db.UserRoles.CountAsync(r=>r.UserId==target.UserId,ct)!=1)throw new Exception("Duplicate grant created");
            checks.Add(new {persistedRoleAndAudit=true,userRevisionChanged=true,duplicatePrevented=true});
        }
        finally
        {
            db.ChangeTracker.Clear();await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.ActorUserId==actor&&a.BranchId==branch.BranchId&&a.EntityType=="user_roles").ExecuteDeleteAsync(CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor||r.UserId==target.UserId).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(a=>a.UserId==target.UserId&&a.BranchId==branch.BranchId).ExecuteDeleteAsync(CancellationToken.None);
            if(target.UserId>0)await db.Users.Where(u=>u.UserId==target.UserId&&u.Username==prefix).ExecuteDeleteAsync(CancellationToken.None);
            if(department.DepartmentId>0)await db.Departments.Where(d=>d.DepartmentId==department.DepartmentId&&d.BranchId==branch.BranchId).ExecuteDeleteAsync(CancellationToken.None);
            if(branch.BranchId>0)await db.Branches.Where(b=>b.BranchId==branch.BranchId&&b.BranchCode==prefix).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None);
            if(await db.Branches.AnyAsync()||await db.UserRoles.AnyAsync()||await db.Users.AnyAsync(u=>u.UserId==target.UserId))throw new Exception("Role fixture cleanup failed");
            checks.Add(new {roleFixtureCleanup=true});
        }
    }
}
