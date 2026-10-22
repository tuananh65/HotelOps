namespace HotelOps.Api.Security.Authorization;

public enum PermissionDecision { Denied, Granted, Unauthenticated, Restricted, ResourceContextRequired, Unavailable, InvalidRequest }

/// <summary>A scope decision, not approval of workflow transitions or separation-of-duties rules.</summary>
public readonly record struct PermissionEvaluation(PermissionDecision Decision)
{
    public bool IsGranted => Decision == PermissionDecision.Granted;
}

public interface IPermissionService
{
    /// <summary>Only a global ALL grant can satisfy this check. Not resource authorization.</summary>
    Task<PermissionEvaluation> EvaluateGlobalAsync(string permissionCode, CancellationToken cancellationToken);
    /// <summary>Validates branch access in SQL. Does not establish a resource's branch or ownership.</summary>
    Task<PermissionEvaluation> EvaluateBranchAsync(string permissionCode, long branchId, CancellationToken cancellationToken);
    /// <summary>Requires a backend resolver for this permission/resource pair, including for ALL grants.</summary>
    Task<PermissionEvaluation> EvaluateResourceAsync(string permissionCode, PermissionResource resource, CancellationToken cancellationToken);
}

public sealed record PermissionResource(string ResourceType, long ResourceId);

/// <summary>Produced only by trusted backend adapters, never model-bound from request data.</summary>
public sealed record VerifiedPermissionResource(long BranchId, bool HasOwnership);

public interface IPermissionResourceResolver
{
    /// <summary>
    /// Load the resource from an authoritative store, verify the permission/resource-type binding,
    /// derive its branch and apply the specific ownership/assignment rule for this user.
    /// Return null if the rule, resource or binding cannot be established. Never echo client ownership.
    /// </summary>
    Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource,
        long currentUserId, CancellationToken cancellationToken);
}

public sealed class UnresolvedPermissionResourceResolver : IPermissionResourceResolver
{
    public Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource,
        long currentUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<VerifiedPermissionResource?>(null);
    }
}
