using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Services.Dev2Authorization;

/// <summary>Feature-local adapter for the existing evaluator. Never replaces the shared resolver.</summary>
public sealed class Dev2FactPermissionResolver(Dev2ResourceFactReader reader, string boundPermission,
    string boundKind, long boundId, long? expectedParentId,
    Func<Dev2ResourceFacts, long, bool>? acceptedOwnPredicate) : IPermissionResourceResolver
{
    public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource,
        long currentUserId, CancellationToken cancellationToken)
    {
        if (permissionCode != boundPermission || resource.ResourceType != boundKind
            || resource.ResourceId != boundId || currentUserId <= 0) return null;
        var facts = await reader.ReadAsync(boundKind, boundId, expectedParentId, cancellationToken);
        return facts is null ? null : new(facts.BranchId, acceptedOwnPredicate?.Invoke(facts, currentUserId) == true);
    }
}
