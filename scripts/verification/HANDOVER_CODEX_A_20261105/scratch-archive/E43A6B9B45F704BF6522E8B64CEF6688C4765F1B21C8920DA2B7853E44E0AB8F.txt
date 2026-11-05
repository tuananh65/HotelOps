using HotelOps.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Security.Authorization;

public interface IPermissionPolicyCatalog
{
    Task<bool> ContainsAsync(string permissionCode, CancellationToken cancellationToken);
}

public sealed class EfPermissionPolicyCatalog(HotelOpsDbContext db) : IPermissionPolicyCatalog
{
    public async Task<bool> ContainsAsync(string permissionCode, CancellationToken cancellationToken)
    {
        // Ordinal comparison also rejects differently cased codes under a case-insensitive SQL collation.
        var codes = await db.Permissions.AsNoTracking().Where(p => p.PermissionCode == permissionCode)
            .Select(p => p.PermissionCode).ToListAsync(cancellationToken);
        return codes.Contains(permissionCode, StringComparer.Ordinal);
    }
}

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options, IServiceScopeFactory scopes,
    IHttpContextAccessor? httpAccessor = null)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override bool AllowsCachingPolicies => false;

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var parts = policyName[PermissionPolicy.Prefix.Length..].Split(':');
        if (parts.Length != 2 || !Enum.TryParse<PermissionCheck>(parts[0], out var check)
            || !Enum.IsDefined(check) || parts[0] != check.ToString()
            || !PermissionPolicy.IsValidCode(parts[1])) return null;

        await using var scope = scopes.CreateAsyncScope();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(
            httpAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        if (!await scope.ServiceProvider.GetRequiredService<IPermissionPolicyCatalog>()
            .ContainsAsync(parts[1], budget.Token)) return null;

        return new AuthorizationPolicyBuilder(HotelOpsCookieTicket.Scheme)
            .RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(parts[1], check)).Build();
    }
}
