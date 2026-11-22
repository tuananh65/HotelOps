using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed class UserService(HotelOpsCompactDbContext db,Access access,Atomic atomic,Actor actor,Audit audit,
    IPasswordHasher<User> passwords,TimeProvider clock)
{
    public async Task<object> RolePermissions(long roleId,CancellationToken ct){
        await access.Require("ROLE_MANAGE",ct:ct);
        var role=await db.Roles.AsNoTracking().SingleOrDefaultAsync(r=>r.RoleId==roleId,ct)??throw new ApiException(404,"ROLE_NOT_FOUND");
        return new{role=Output.Record(role),permissions=await (from grant in db.RolePermissions.AsNoTracking() join permission in db.Permissions on grant.PermissionId equals permission.PermissionId
            where grant.RoleId==roleId select new{grant.PermissionId,permission.Code,grant.DataScope}).ToArrayAsync(ct)};
    }
    public async Task<object> List(CancellationToken ct)
    {
        await access.Require("USER_VIEW",ct:ct);
        var now=clock.GetUtcNow().UtcDateTime;
        var users=await db.Users.AsNoTracking().OrderBy(u=>u.UserId).Take(500).ToArrayAsync(ct);
        var ids=users.Select(u=>u.UserId).ToArray();
        var branches=(await (from m in db.Memberships.AsNoTracking()
            join b in db.Branches on m.BranchId equals b.BranchId
            where ids.Contains(m.UserId) && (m.Kind=="EMPLOYMENT" || m.Kind=="BRANCH_ACCESS")
                && m.ValidFrom<=now && (m.ValidTo==null || m.ValidTo>now)
            select new UserDirectoryBranchDto(m.UserId,m.BranchId,b.Name))
            .Distinct().ToArrayAsync(ct)).ToLookup(b=>b.UserId);
        var employment=(await (from m in db.Memberships.AsNoTracking()
            join b in db.Branches on m.BranchId equals b.BranchId
            join d in db.Departments on m.DepartmentId equals (long?)d.DepartmentId into departments
            from d in departments.DefaultIfEmpty()
            where ids.Contains(m.UserId) && m.Kind=="EMPLOYMENT" && m.ValidFrom<=now && (m.ValidTo==null || m.ValidTo>now)
            select new UserEmploymentDto(m.UserId,m.BranchId,b.Name,m.DepartmentId,d==null?null:d.Name))
            .ToArrayAsync(ct)).ToLookup(m=>m.UserId);
        var roles=(await (from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles on ur.RoleId equals r.RoleId
            where ids.Contains(ur.UserId) && r.IsActive && ur.AssignedAt<=now && ur.RevokedAt==null && (ur.ExpiresAt==null || ur.ExpiresAt>now)
            select new UserDirectoryRoleDto(ur.UserId,ur.RoleId,r.Name,ur.BranchId))
            .ToArrayAsync(ct)).ToLookup(r=>r.UserId);
        return users.Select(user=>{
            var record=Output.Record(user);
            var fields=new Dictionary<string,object?>(record.Fields){
                ["employments"]=employment[user.UserId].ToArray(),
                ["branches"]=branches[user.UserId].ToArray(),
                ["roles"]=roles[user.UserId].ToArray()
            };
            return new ResourceDto(record.Type,fields);
        }).ToArray();
    }
    public Task<object> Create(CreateUserRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);
        var user=new User{EmployeeCode=request.EmployeeCode.Trim(),Username=request.Username.Trim(),FullName=request.FullName.Trim(),Email=request.Email,
            AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",SecurityStamp=Guid.NewGuid(),CreatedAt=clock.GetUtcNow().UtcDateTime};
        user.PasswordHash=passwords.HashPassword(user,request.Password);user.TemporaryPasswordExpiresAt=clock.GetUtcNow().UtcDateTime.AddDays(1);
        db.Users.Add(user);await db.SaveChangesAsync(ct);audit.Add("User",user.UserId,"CREATE");return (object)Output.Record(user);},ct);
    public Task<object> Status(long id,string action,ActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);var user=await db.Users.SingleOrDefaultAsync(u=>u.UserId==id,ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Version(user.RowVersion,request.RowVersion);Output.Reason(request.Reason);
        Output.Require(action is "activate" or "deactivate","UNKNOWN_ACTION",400);
        if(action=="deactivate"){
            await GuardWorkday(id,ct);
            Output.Require(id!=actor.Id,"CANNOT_DEACTIVATE_SELF");await ProtectAdmin(id,null,ct);
            Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==id && a.AssignmentStatus=="ACTIVE",ct),"USER_HAS_ACTIVE_ASSIGNMENTS");
            Output.Require(!await db.WorkIntervals.AnyAsync(w=>w.UserId==id && w.ClosedAt==null,ct),"USER_HAS_OPEN_TIME");}
        user.AccountStatus=action=="activate"?"ACTIVE":"INACTIVE";user.SecurityStamp=Guid.NewGuid();
        audit.Add("User",id,action.ToUpperInvariant(),after:new {request.Reason});await db.SaveChangesAsync(ct);return (object)Output.Record(user);},ct);
    public Task<object> Membership(long id,MembershipRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);Output.Require(request.Kind is "BRANCH_ACCESS" or "EMPLOYMENT" or "DEPARTMENT_AUTHORITY","INVALID_MEMBERSHIP_KIND",400);
        var now=clock.GetUtcNow().UtcDateTime;
        if(request.IsPrimary)await GuardWorkday(id,ct);
        if(request.IsPrimary)foreach(var old in await db.Memberships.Where(m=>m.UserId==id && m.IsPrimary && m.ValidTo==null).ToArrayAsync(ct))old.ValidTo=now;
        await db.SaveChangesAsync(ct);
        var membership=new Membership{UserId=id,BranchId=request.BranchId,DepartmentId=request.DepartmentId,Kind=request.Kind,IsPrimary=request.IsPrimary,
            RecordedBy=actor.Id,ValidFrom=now,Provenance="COMPACT_API"};db.Memberships.Add(membership);await db.SaveChangesAsync(ct);
        audit.Add("Membership",membership.MembershipId,"GRANT",request.BranchId);return (object)Output.Record(membership);},ct);
    public Task<object> RevokeMembership(long id,VersionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("USER_MANAGE",ct:ct);var m=await db.Memberships.FindAsync([id],ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Version(m.RowVersion,request.RowVersion);
        await GuardWorkday(m.UserId,ct);
        Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==m.UserId && a.BranchId==m.BranchId && a.AssignmentStatus=="ACTIVE",ct),"USER_HAS_ACTIVE_ASSIGNMENTS");
        m.ValidTo=clock.GetUtcNow().UtcDateTime;audit.Add("Membership",id,"REVOKE",m.BranchId);await db.SaveChangesAsync(ct);return (object)Output.Record(m);},ct);
    public Task<object> AssignRole(long id,UserRoleRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("ROLE_MANAGE",ct:ct);if(request.BranchId!=null)await access.Eligible(id,request.BranchId.Value,ct);
        var role=await db.Roles.FindAsync([request.RoleId],ct)??throw new ApiException(404,"ROLE_NOT_FOUND");Output.Require(role.IsActive,"ROLE_INACTIVE");
        if(role.Code=="SYS_ADMIN")Output.Require(request.BranchId==null&&request.ExpiresAt==null,"SYSTEM_ADMIN_REQUIRES_DURABLE_GLOBAL_ASSIGNMENT",400);
        var assignment=new UserRole{UserId=id,RoleId=request.RoleId,BranchId=request.BranchId,ExpiresAt=request.ExpiresAt,
            AssignedAt=clock.GetUtcNow().UtcDateTime,AssignedBy=actor.Id};db.UserRoles.Add(assignment);await db.SaveChangesAsync(ct);
        audit.Add("UserRole",assignment.UserRoleId,"GRANT",request.BranchId);return (object)Output.Record(assignment);},ct);
    public Task<object> RevokeRole(long id,VersionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("ROLE_MANAGE",ct:ct);var role=await db.UserRoles.FindAsync([id],ct)??throw new ApiException(404,"NOT_FOUND");
        Output.Version(role.RowVersion,request.RowVersion);await ProtectAdmin(role.UserId,id,ct);
        await GuardWorkday(role.UserId,ct);
        Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssigneeUserId==role.UserId&&a.AssignmentStatus=="ACTIVE",ct),"USER_HAS_ACTIVE_ASSIGNMENTS");
        role.RevokedAt=clock.GetUtcNow().UtcDateTime;audit.Add("UserRole",id,"REVOKE",role.BranchId);return (object)new {revoked=true};},ct);
    public Task<object> ConfigureRole(long roleId,IReadOnlyList<GrantRequest> request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.Require("ROLE_MANAGE",ct:ct);var role=await db.Roles.FindAsync([roleId],ct)??throw new ApiException(404,"ROLE_NOT_FOUND");
        Output.Require(role.Code!="SYS_ADMIN","SYSTEM_ADMIN_GRANTS_IMMUTABLE");
        Output.Require(!await db.WorkdayAssignments.AnyAsync(a=>(a.Status=="WORKING"||a.Status=="PLANNED")&&db.UserRoles.Any(ur=>ur.UserId==a.UserId&&ur.RoleId==roleId&&ur.RevokedAt==null),ct),"ROLE_HAS_WORKDAY_RESPONSIBILITIES");
        Output.Require(!await db.WorkAssignments.AnyAsync(a=>a.AssignmentStatus=="ACTIVE"&&db.UserRoles.Any(ur=>ur.RoleId==roleId&&ur.UserId==a.AssigneeUserId&&ur.RevokedAt==null),ct),"ROLE_HAS_ACTIVE_RESPONSIBILITIES");
        Output.Require(request.All(r=>r.DataScope is "OWN" or "BRANCH" or "ALL"),"UNSUPPORTED_DATA_SCOPE",400);
        Output.Require(request.Select(r=>r.PermissionId).Distinct().Count()==request.Count,"DUPLICATE_PERMISSION",400);
        db.RolePermissions.RemoveRange(await db.RolePermissions.Where(r=>r.RoleId==roleId).ToArrayAsync(ct));await db.SaveChangesAsync(ct);
        db.RolePermissions.AddRange(request.Select(r=>new RolePermission{RoleId=roleId,PermissionId=r.PermissionId,DataScope=r.DataScope}));
        audit.Add("Role",roleId,"CONFIGURE_PERMISSIONS",after:request);return (object)new {configured=true};},ct);
    private async Task GuardWorkday(long userId,CancellationToken ct) => Output.Require(
        !await db.WorkdayAssignments.AnyAsync(a=>a.UserId==userId&&(a.Status=="WORKING"||a.Status=="PLANNED"),ct),"USER_HAS_WORKDAY_RESPONSIBILITIES");
    private async Task ProtectAdmin(long userId,long? assignmentId,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var admins=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code=="SYS_ADMIN" && r.IsActive && ur.BranchId==null && ur.RevokedAt==null && (ur.ExpiresAt==null||ur.ExpiresAt>now)
            && u.AccountStatus=="ACTIVE" && u.EmploymentStatus=="ACTIVE" && u.DeletedAt==null select new{ur.UserId,ur.UserRoleId}).ToArrayAsync(ct);
        if(admins.Any(a=>assignmentId!=null?a.UserRoleId==assignmentId:a.UserId==userId))
            Output.Require(admins.Any(a=>assignmentId!=null?a.UserRoleId!=assignmentId:a.UserId!=userId),"LAST_ADMIN_PROTECTED");
    }
}
