using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Services.Dev2Authorization;

public enum Dev2PermissionTargetKind { Global, Branch, Resource }
public sealed record Dev2PermissionTarget(Dev2PermissionTargetKind Kind, long? BranchId = null,
    PermissionResource? Resource = null);
public sealed record Dev2PermissionRequirement(string Code, Dev2PermissionTarget Target);
public enum Dev2PermissionCombination { All, Any }

/// <summary>Composition only. All grant, assignment, branch and OWN decisions stay in PermissionService.</summary>
public sealed class Dev2PermissionGate(IPermissionService permissions)
{
    public async Task<PermissionEvaluation> EvaluateAsync(IReadOnlyList<Dev2PermissionRequirement> requirements,
        Dev2PermissionCombination combination, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (requirements is null || !Enum.IsDefined(combination))
            return new(PermissionDecision.InvalidRequest);
        // Freeze the complete expression before the first await. A caller-owned list may change
        // while PermissionService loads authority; it must not remove a denied AND operand.
        var rules = requirements.ToArray();
        if (rules.Length == 0 || rules.Any(rule => rule is null
            || string.IsNullOrWhiteSpace(rule.Code) || rule.Code.Length > 100
            || !ValidTarget(rule.Target)))
            return new(PermissionDecision.InvalidRequest);
        var granted = 0;
        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();
            PermissionEvaluation result;
            try
            {
                result = rule.Target switch
                {
                    { Kind: Dev2PermissionTargetKind.Global, BranchId: null, Resource: null } =>
                        await permissions.EvaluateGlobalAsync(rule.Code, ct),
                    { Kind: Dev2PermissionTargetKind.Branch, BranchId: > 0, Resource: null } =>
                        await permissions.EvaluateBranchAsync(rule.Code, rule.Target.BranchId!.Value, ct),
                    { Kind: Dev2PermissionTargetKind.Resource, BranchId: null, Resource: not null } =>
                        await permissions.EvaluateResourceAsync(rule.Code, rule.Target.Resource!, ct),
                    _ => new(PermissionDecision.InvalidRequest)
                };
            }
            catch (Exception error) when (error is TimeoutException or System.Data.Common.DbException)
            {
                return new(PermissionDecision.Unavailable);
            }
            // An OR expression cannot mask unavailable/invalid authority with another grant.
            if (result.Decision is PermissionDecision.Unavailable or PermissionDecision.Unauthenticated
                or PermissionDecision.Restricted or PermissionDecision.InvalidRequest)
                return result;
            if (result.IsGranted) granted++;
        }
        return new((combination == Dev2PermissionCombination.All ? granted == rules.Length : granted > 0)
            ? PermissionDecision.Granted : PermissionDecision.Denied);
    }

    private static bool ValidTarget(Dev2PermissionTarget? target) => target switch
    {
        { Kind: Dev2PermissionTargetKind.Global, BranchId: null, Resource: null } => true,
        { Kind: Dev2PermissionTargetKind.Branch, BranchId: > 0, Resource: null } => true,
        { Kind: Dev2PermissionTargetKind.Resource, BranchId: null,
            Resource: { ResourceId: > 0 } resource } => !string.IsNullOrWhiteSpace(resource.ResourceType),
        _ => false
    };
}
