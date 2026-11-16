using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// A filter over an already IR_VIEW-authorized query, never an independent read grant.
internal sealed class InternalRequestDirection(HotelOpsDbContext db)
{
    internal sealed record Scope(long User, long[] ManagedDepartments)
    {
        internal IQueryable<InternalRequest> Apply(IQueryable<InternalRequest> query,string? direction,DateTime now) => direction switch
        {
            null or "" => query,
            "SENT" => query.Where(r=>r.RequesterId==User),
            "RECEIVED" => query.Where(r=>r.ReceivingDepartmentId!=null&&r.ReceivingDepartment!=null
                &&r.ReceivingDepartment.BranchId==r.BranchId&&ManagedDepartments.Contains(r.ReceivingDepartmentId.Value)
                || r.InternalRequestAssignments.Count(a=>a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now)==1
                && r.InternalRequestAssignments.Any(a=>a.AssigneeId==User&&a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now)),
            _ => throw new ValidationException("Direction must be SENT or RECEIVED.")
        };
    }

    internal async Task<Scope> ResolveAsync(long user,string? direction,DateTime now,CancellationToken ct)
    {
        if(direction is not (null or "" or "SENT" or "RECEIVED")) throw new ValidationException("Direction must be SENT or RECEIVED.");
        if(direction!="RECEIVED")return new(user,[]);
        var actor=await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null,ct);
        if(!actor)return new(user,[]);
        var paths=await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId==user&&assignment.Role.RoleCode=="DEPT_MANAGER"&&grant.Permission.PermissionCode=="IR_PROCESS"
            select new PermissionGrantPath(grant.Permission.PermissionCode,grant.DataScope,assignment.BranchId,assignment.IsActive,
                assignment.Role.IsActive,assignment.AssignedAt,assignment.ExpiresAt,assignment.Branch!=null&&assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a=>a.UserId==user&&a.BranchId==assignment.BranchId&&a.RevokedAt==null&&a.GrantedAt<=now))).ToArrayAsync(ct);
        var branches=paths.Where(p=>p.BranchId is {} branch&&InternalRequestProcessAuthority.Eligible("DEPT_MANAGER",p,branch,now))
            .Select(p=>p.BranchId!.Value).Distinct().ToArray();
        if(branches.Length==0)return new(user,[]);
        if(!await new InternalRequestProcessAuthority(db).InstalledAsync(ct))throw new InternalRequestReadException(503,"IR_DIRECTION_AUTHORITY_UNAVAILABLE");
        var mappings=db.Database.SqlQuery<DirectionMapping>($"SELECT branch_id AS BranchId,department_id AS DepartmentId FROM dbo.department_management_access WHERE user_id={user} AND granted_at<={now} AND revoked_at IS NULL");
        var departments=await (from mapping in mappings
            join department in db.Departments.AsNoTracking() on mapping.DepartmentId equals department.DepartmentId
            where branches.Contains(mapping.BranchId)&&department.BranchId==mapping.BranchId&&department.IsActive&&department.Branch.IsActive
            select department.DepartmentId).Distinct().Order().ToArrayAsync(ct);
        return new(user,departments);
    }
    private sealed class DirectionMapping { public long BranchId{get;set;} public long DepartmentId{get;set;} }
}
