using System.Collections.Frozen;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Security.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Services.Dev2Authorization;

public sealed class Dev2AccessDenied(int status) : Exception("DEV2 access denied.")
{
    public int Status { get; } = status;
}
public enum Dev2AuthorizationPhase { Admission, BeforePersistence, BeforeStockPosting, BeforeDisclosure }
internal sealed record Dev2BoundAction(Dev2KnownAction Identity, HttpContext Http,
    CurrentUserInfo Actor, IReadOnlyDictionary<string, object?> Arguments);
internal sealed record Dev2DomainInvocation(string Controller, string Action, long? RootId, object? Input);

// Only compiled, source-reviewed bindings can create admission. No config/request/role-name approvals.
// All delegates are mandatory: incomplete technical values cannot be defaulted to permissive functions.
internal sealed record Dev2ActionContract(
    string DecisionId, string AcceptedSourceReference, string AcceptedSha256,
    Func<Dev2BoundAction, CancellationToken, Task<IReadOnlyList<Dev2PermissionRequirement>>> Requirements,
    Dev2PermissionCombination Combination,
    Func<Dev2BoundAction, Dev2AuthorizationPhase, CancellationToken, Task<bool>> ValidateFactsAndWorkflow,
    Func<Dev2BoundAction, Dev2DomainInvocation, bool> MatchesServiceInvocation,
    Func<Dev2BoundAction, Dev2ResourceFactReader, IPermissionResourceResolver>? ResourceResolver,
    Func<Dev2BoundAction, CancellationToken, Task<IActionResult>>? ScopedReadHandler,
    Func<Dev2BoundAction, Dev2AuthorizationPhase, CancellationToken, Task<bool>> TransactionAuthorityBoundary,
    bool RequiresReadTransaction = false,
    bool RequiresActionTransaction = false)
{
    public bool IsComplete(Dev2KnownAction identity) => DecisionId == identity.DecisionId
        && !string.IsNullOrWhiteSpace(AcceptedSourceReference)
        && AcceptedSha256.Length == 64 && AcceptedSha256.All(Uri.IsHexDigit)
        && Requirements is not null && ValidateFactsAndWorkflow is not null
        && MatchesServiceInvocation is not null && TransactionAuthorityBoundary is not null
        && (identity.HttpMethod != "GET" || ScopedReadHandler is not null);
}

internal static class Dev2ActionContracts
{
    // Production acceptance remains separate from the owner's trial-only V1 policy.
    // The demo slice requires its compiled policy hash and exact trial database configuration.
    private static readonly FrozenDictionary<string, Dev2ActionContract> Accepted =
        IncidentReadContract.Contracts.Concat(MaterialCreateContract.Contracts).Concat(MaterialReadContract.Contracts)
            .Concat(MaterialApprovalContract.Contracts).Concat(MaterialRequesterContract.Contracts)
            .Concat(MaterialIssueContract.Contracts)
            .ToFrozenDictionary(c => c.DecisionId, StringComparer.Ordinal);
    internal static IReadOnlySet<string> DecisionIds { get; } = Accepted.Keys.ToFrozenSet(StringComparer.Ordinal);
    internal static Dev2ActionContract? Find(Dev2KnownAction identity, HttpContext? http = null) =>
        Accepted.TryGetValue(identity.DecisionId, out var contract) && contract.IsComplete(identity) ? contract
        : http is not null ? Dev2DemoBindings.Find(identity, http) : null;
}
