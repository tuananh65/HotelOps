using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.MaterialRequests;

// Approved GBR-111: a current mapped Department Head, with the exact independent
// material approval grant. IR_PROCESS/MR_VIEW/MR_APPROVE_L2 do not substitute for it.
internal static class MaterialApprovalAuthority
{
    internal sealed record Route(long DepartmentId, string Permission);
    internal sealed record Candidate(long UserId, string EmployeeCode, string FullName);
    internal sealed record Options(long BranchId, long? SourceWarehouseId, long DepartmentId, string RequiredPermission, Candidate[] Items);

    internal static async Task<Options> CandidatesAsync(HotelOpsDbContext db, long requester, long branch, long? warehouse, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var route = await ResolveAsync(db, new MaterialRequest { RequesterId = requester, BranchId = branch, SourceWarehouseId = warehouse }, now, ct);
        var mapped = db.Database.SqlQuery<long>($"""
            SELECT user_id AS Value FROM dbo.department_management_access
            WHERE branch_id={branch} AND department_id={route.DepartmentId} AND granted_at<={now} AND revoked_at IS NULL
            """);
        var candidates = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId != requester && assignment.BranchId == branch && assignment.IsActive
                && assignment.Role.IsActive && assignment.Role.RoleCode == "DEPT_MANAGER"
                && assignment.AssignedAt <= now && (assignment.ExpiresAt == null || assignment.ExpiresAt > now)
                && assignment.Branch!.IsActive && assignment.User.DeletedAt == null
                && assignment.User.AccountStatus == "ACTIVE" && assignment.User.EmploymentStatus == "ACTIVE"
                && grant.Permission.PermissionCode == route.Permission && (grant.DataScope == "ALL" || grant.DataScope == "BRANCH")
                && mapped.Contains(assignment.UserId)
                && db.UserBranchAccesses.Any(b => b.UserId == assignment.UserId && b.BranchId == branch && b.GrantedAt <= now && b.RevokedAt == null)
            select new Candidate(assignment.UserId, assignment.User.EmployeeCode, assignment.User.FullName))
            .Distinct().OrderBy(c => c.FullName).ThenBy(c => c.UserId).ToArrayAsync(ct);
        return new(branch, warehouse, route.DepartmentId, route.Permission, candidates);
    }

    private static async Task<bool> MappedAsync(HotelOpsDbContext db, long user, long branch, long department, DateTime now, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(UPDLOCK,HOLDLOCK)
            WHERE user_id={user} AND branch_id={branch} AND department_id={department}
              AND granted_at<={now} AND revoked_at IS NULL
            """).SingleAsync(ct) == 1;

    internal static async Task<Route> ResolveAsync(HotelOpsDbContext db, MaterialRequest request, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Material approval authority requires the caller's Serializable transaction.");
        if (!await new InternalRequestProcessAuthority(db).InstalledAsync(ct))
            throw new ValidationException("Department authority mapping capability is required for material approval.");
        // Membership locates the requester's department; it never establishes who manages it.
        var department = await db.Users.AsNoTracking().Where(u => u.UserId == request.RequesterId && u.DeletedAt == null
            && u.Department != null && u.Department.IsActive && u.Department.BranchId == request.BranchId && u.Department.Branch.IsActive)
            .Select(u => u.DepartmentId).SingleOrDefaultAsync(ct)
            ?? throw new ValidationException("Requester department must be active and in the material request branch.");
        var requesterIsHead = await db.UserRoles.AsNoTracking().AnyAsync(a => a.UserId == request.RequesterId
            && a.Role.RoleCode == "DEPT_MANAGER" && a.IsActive && a.Role.IsActive && a.BranchId == request.BranchId
            && a.AssignedAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now) && a.Branch!.IsActive
            && db.UserBranchAccesses.Any(b => b.UserId == request.RequesterId && b.BranchId == request.BranchId
                && b.RevokedAt == null && b.GrantedAt <= now), ct)
            && await MappedAsync(db, request.RequesterId, request.BranchId, department, now, ct);
        if (!requesterIsHead) return new(department, "MR_APPROVE_L1");
        var warehouseDepartment = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == request.SourceWarehouseId
            && w.IsActive && w.BranchId == request.BranchId && w.Department != null && w.Department.IsActive
            && w.Department.BranchId == request.BranchId).Select(w => w.DepartmentId).SingleOrDefaultAsync(ct)
            ?? throw new ValidationException("Fallback approval requires an active issuing-warehouse department in the request branch.");
        return new(warehouseDepartment, "MR_APPROVE_FALLBACK");
    }

    internal static async Task ValidateAsync(HotelOpsDbContext db, MaterialRequest request, long approver, CancellationToken ct)
    {
        if (approver <= 0 || approver == request.RequesterId)
            throw new ValidationException("An independent material approver is required.");
        // The caller acquires these users before source/catalog/material locks.
        var locked = await new UserEligibilityLock(db).LockAsync([request.RequesterId, approver], ct);
        UserEligibilityLock.RequireEligible(approver, locked);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var route = await ResolveAsync(db, request, now, ct);
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == approver && assignment.Role.RoleCode == "DEPT_MANAGER"
                && assignment.BranchId == request.BranchId && grant.Permission.PermissionCode == route.Permission
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == approver && b.BranchId == request.BranchId
                    && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
        if (!grants.Any(g => PermissionGrantRules.IsEligible(g, now) && PermissionGrantRules.EffectiveScope(g) == "BRANCH")
            || !await MappedAsync(db, approver, request.BranchId, route.DepartmentId, now, ct))
            throw new ValidationException("Current mapped Department Head with the required material approval grant is required.");
    }
}
