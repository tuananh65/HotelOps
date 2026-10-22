namespace HotelOps.Api.Security.Authorization;

/// <summary>
/// UC-030/031/032: only the current assignee may execute cleaning work.
/// This first adapter supports IN_PROGRESS execution scope, not start/resume transitions.
/// </summary>
public sealed class CleaningTaskResourceResolver(ICleaningTaskAccessReader reader, TimeProvider clock)
    : IPermissionResourceResolver
{
    public const string ResourceType = "cleaning_task";
    public const string PermissionCode = "CLEANING_EXECUTE";

    public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource,
        long currentUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (resource is null || resource.ResourceId <= 0 || currentUserId <= 0
            || resource.ResourceType != ResourceType || permissionCode != PermissionCode) return null;

        var task = await reader.ReadAsync(resource.ResourceId, cancellationToken);
        if (task is null || task.BranchId <= 0 || !task.RoomActive || task.TaskStatus != "IN_PROGRESS") return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var assignments = task.Assignments.Where(a => a.Status == "ACTIVE"
            && a.EndedAt is null && a.AssignedAt <= now).Take(2).ToArray();
        // There is no unique-current-assignee constraint: ambiguous data fails closed.
        if (assignments.Length != 1 || assignments[0].UserId != currentUserId) return null;

        // Being current assignee is intrinsic to this permission/resource pair, even for an ALL grant.
        // Role/grant/branch isolation is still evaluated separately by PermissionService.
        return new VerifiedPermissionResource(task.BranchId, HasOwnership: true);
    }
}
