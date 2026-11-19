using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed record PersonalRoleDto(string Name, string? Branch);
public sealed record PersonalProfileDto(string EmployeeCode, string Username, string FullName,
    string Email, string? Phone, string? Branch, string? Department, PersonalRoleDto[] Roles);

public sealed class PersonalProfileService(HotelOpsDbContext db, ICurrentUserAccessor current, DashboardSessionGuard session, TimeProvider clock)
{
    public async Task<PersonalProfileDto> ReadAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await session.RequireAsync(ct);
        if (!current.TryGetCurrentUser(out var actor)) throw new InsightException(401, "SESSION_INVALID", "Session required.");
        var user = await db.Users.AsNoTracking().Where(u => u.UserId == actor.UserId && u.DeletedAt == null)
            .Select(u => new { u.EmployeeCode, u.Username, u.FullName, u.Email, u.Phone,
                Branch = u.PrimaryBranch == null ? null : u.PrimaryBranch.BranchName,
                Department = u.Department == null ? null : u.Department.DepartmentName }).SingleAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == actor.UserId && r.IsActive && r.Role.IsActive
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.Branch!.IsActive && db.UserBranchAccesses.Any(a => a.UserId == actor.UserId
                && a.BranchId == r.BranchId && a.RevokedAt == null && a.GrantedAt <= now)))
            .Select(r => new PersonalRoleDto(r.Role.RoleName, r.Branch == null ? null : r.Branch.BranchName)).Distinct().ToArrayAsync(ct);
        await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return new(user.EmployeeCode, user.Username, user.FullName, user.Email, user.Phone, user.Branch, user.Department, roles);
    }
}
