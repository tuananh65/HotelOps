using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Security.Authorization;

public sealed class PermissionService(ICurrentUserAccessor currentUser, IPermissionDataSource data,
    IPermissionResourceResolver resources, TimeProvider clock) : IPermissionService
{
    public Task<PermissionEvaluation> EvaluateGlobalAsync(string permissionCode, CancellationToken cancellationToken) =>
        EvaluateAsync(permissionCode, null, null, cancellationToken);

    public Task<PermissionEvaluation> EvaluateBranchAsync(string permissionCode, long branchId, CancellationToken cancellationToken) =>
        branchId <= 0 ? Task.FromResult(new PermissionEvaluation(PermissionDecision.InvalidRequest))
            : EvaluateAsync(permissionCode, branchId, null, cancellationToken);

    public Task<PermissionEvaluation> EvaluateResourceAsync(string permissionCode, PermissionResource resource, CancellationToken cancellationToken) =>
        resource is null || resource.ResourceId <= 0 || string.IsNullOrWhiteSpace(resource.ResourceType)
            ? Task.FromResult(new PermissionEvaluation(PermissionDecision.InvalidRequest))
            : EvaluateAsync(permissionCode, null, resource, cancellationToken);

    private async Task<PermissionEvaluation> EvaluateAsync(string permissionCode, long? branchId,
        PermissionResource? resource, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(permissionCode) || permissionCode.Length > 100)
            return new(PermissionDecision.InvalidRequest);
        if (!currentUser.TryGetCurrentUser(out var user) || user.UserId <= 0 || user.SessionId <= 0)
            return new(PermissionDecision.Unauthenticated);
        if (user.IsRestricted) return new(PermissionDecision.Restricted);

        try
        {
            if (!await data.IsActiveUserAsync(user.UserId, ct)) return new(PermissionDecision.Denied);
            var ownsResource = false;
            if (resource is not null)
            {
                var verified = await resources.ResolveAsync(permissionCode, resource, user.UserId, ct);
                if (verified is null || verified.BranchId <= 0)
                    return new(PermissionDecision.ResourceContextRequired);
                branchId = verified.BranchId;
                ownsResource = verified.HasOwnership;
            }
            if (branchId is { } target && !await data.IsActiveBranchAsync(target, ct))
                return new(PermissionDecision.Denied);

            var now = clock.GetUtcNow().UtcDateTime;
            var paths = await data.ReadPathsAsync(user.UserId, permissionCode, now, ct);
            var needsContext = false;
            foreach (var path in paths)
            {
                if (!string.Equals(path.PermissionCode, permissionCode, StringComparison.Ordinal)
                    || !path.AssignmentActive || !path.RoleActive || path.AssignedAt > now
                    || (path.ExpiresAt is { } expiry && expiry <= now)) continue;

                // A branch-bound path never borrows another role's assignment/access.
                if (path.BranchId is not null && (!path.BranchActive || !path.BranchAccessActive)) continue;
                switch (path.DataScope)
                {
                    case "ALL":
                        if (path.BranchId is null || (branchId is not null && path.BranchId == branchId))
                            return new(PermissionDecision.Granted);
                        if (branchId is null) needsContext = true;
                        break;
                    case "BRANCH":
                        if (path.BranchId is null) break;
                        if (branchId is null) needsContext = true;
                        else if (path.BranchId == branchId) return new(PermissionDecision.Granted);
                        break;
                    case "OWN":
                        if (path.BranchId is null) break;
                        if (resource is null) needsContext = true;
                        else if (path.BranchId == branchId && ownsResource) return new(PermissionDecision.Granted);
                        break;
                    // Unknown, legacy DEPARTMENT or null scopes never grant access.
                }
            }
            return new(needsContext ? PermissionDecision.ResourceContextRequired : PermissionDecision.Denied);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (IsDataFailure(error))
        {
            return new(PermissionDecision.Unavailable);
        }
        // Programming/adapter exceptions propagate; callers must never treat failure as permission.
    }

    private static bool IsDataFailure(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbException or DbUpdateException or TimeoutException) return true;
        return false;
    }
}
