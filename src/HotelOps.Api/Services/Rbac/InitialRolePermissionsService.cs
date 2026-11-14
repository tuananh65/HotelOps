using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
namespace HotelOps.Api.Services.Rbac;

public sealed record InitialRoleState(Role Role, bool HasAssignmentHistory, IReadOnlyList<InitialPermissionGrant> Grants);
public sealed record InitialRolePermissionsResult(int Status, string? Code = null, InitialRolePermissionsDto? Value = null);
/// <summary>Trusted transaction port. SQL adapter owns retained global-first locks; no HTTP/user input authority.</summary>
public interface IInitialRolePermissionsTransaction : IAsyncDisposable
{
    Task<int> AuthorizeAsync(CancellationToken ct);
    Task<InitialRoleState?> ReadAsync(long roleId, CancellationToken ct);
    Task<Permission[]> CatalogAsync(long[] ids, CancellationToken ct);
    Task InsertAndAuditAsync(long roleId, InitialPermissionGrant[] grants, CancellationToken ct);
    Task PreserveAdministrationAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
public interface IInitialRolePermissionsTransactions { Task<IInitialRolePermissionsTransaction> BeginAsync(CancellationToken ct); }
public static class InitialRolePermissionPolicy
{
    private static readonly HashSet<string> Canonical = new(StringComparer.OrdinalIgnoreCase) { "SYS_ADMIN", "OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF", "HK_INSPECTOR", "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER", "STAFF", "RECEPTION", "OWNER" };
    // Restricted V1 ceiling: proven scopes from existing delegated envelopes and resource resolvers.
    public static bool Allowed(string code, string scope) => (code, scope) is ("ROOM_VIEW", "BRANCH") or ("SHIFT_VIEW", "OWN") or ("ATTENDANCE_RECORD", "OWN");
    public static bool Protected(Role r) => r.IsSystemRole || Canonical.Contains(r.RoleCode.Trim()) || new[] { "SYS_", "SYSTEM_", "ADMIN", "OWNER" }.Any(p => r.RoleCode.Trim().StartsWith(p, StringComparison.OrdinalIgnoreCase));
    public static string? TargetFailure(InitialRoleState state, bool requireEmpty = true) => !state.Role.IsActive || Protected(state.Role) ? "ROLE_PROTECTED" : state.HasAssignmentHistory ? "ROLE_HAS_ASSIGNMENT_HISTORY" : requireEmpty && state.Grants.Count != 0 ? "ROLE_ALREADY_CONFIGURED" : null;
}
/// <summary>One successful empty-set to nonempty-set transition. No replace/delete, role ETag or assignment mutation.</summary>
public sealed class InitialRolePermissionsService(IInitialRolePermissionsTransactions transactions)
{
    public async Task<InitialRolePermissionsResult> ConfigureAsync(long roleId, InitialRolePermissionsRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await using var tx = await transactions.BeginAsync(ct);
        var authority = await tx.AuthorizeAsync(ct);
        if (authority != 200) return new(authority, "ROLE_AUTHORITY_REQUIRED");
        if (roleId <= 0) return new(400, "INVALID_ROLE_ID");
        InitialPermissionGrant[] desired;
        try { desired = request.Validate(); } catch (ValidationException) { return new(400, "INITIAL_GRANTS_INVALID"); }
        var state = await tx.ReadAsync(roleId, ct);
        if (state is null) return new(404, "ROLE_NOT_FOUND");
        if (InitialRolePermissionPolicy.TargetFailure(state) is { } conflict) return new(409, conflict);
        var catalog = await tx.CatalogAsync(desired.Select(g => g.PermissionId).ToArray(), ct);
        if (catalog.Length != desired.Length || catalog.Select(p => p.PermissionId).Distinct().Count() != desired.Length || desired.Any(g => !catalog.Any(p => p.PermissionId == g.PermissionId && InitialRolePermissionPolicy.Allowed(p.PermissionCode, g.DataScope))))
            return new(400, "GRANT_CEILING_DENIED");
        await tx.InsertAndAuditAsync(roleId, desired, ct);
        // Re-read role/history/grants after audit, not a client hash or stale catalog snapshot.
        var after = await tx.ReadAsync(roleId, ct);
        if (after is null || InitialRolePermissionPolicy.TargetFailure(after, false) is not null || after.Role.RoleCode != state.Role.RoleCode || !after.Grants.OrderBy(g => g.PermissionId).SequenceEqual(desired))
            return new(409, "ROLE_CONFIGURATION_CHANGED");
        await tx.PreserveAdministrationAsync(ct);
        authority = await tx.AuthorizeAsync(ct);
        if (authority != 200) return new(authority, "ROLE_AUTHORITY_REQUIRED");
        ct.ThrowIfCancellationRequested();
        await tx.CommitAsync(ct);
        return new(201, Value: new(1, roleId, desired));
    }
}