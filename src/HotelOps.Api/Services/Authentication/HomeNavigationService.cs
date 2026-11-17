using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Authentication;

public sealed class HomeNavigationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, TimeProvider clock, AuditAccess audit)
{
    public Task<UserDirectoryReadResult<string[]>> ReadAsync(SessionReference ticket, CancellationToken ct)
        => new UserDirectoryReadGuard(db, current, permissions, clock).RunAuthorizedAsync(ticket,
            _ => Task.FromResult(new PermissionEvaluation(PermissionDecision.Granted)), async () =>
            {
                var first = await LinksAsync(ct);
                var final = await LinksAsync(ct);
                return first.Intersect(final, StringComparer.Ordinal).ToArray();
            }, ct);

    private async Task<string[]> LinksAsync(CancellationToken ct)
    {
        var links = new List<string> { "/profile", "/notifications" };
        if (Granted(await permissions.EvaluateGlobalAsync("USER_VIEW", ct))) links.Add("/users");
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).ToArrayAsync(ct);
        async Task<bool> AnyBranch(string permission)
        {
            if (Granted(await permissions.EvaluateGlobalAsync(permission, ct))) return true;
            foreach (var branch in branches)
                if (Granted(await permissions.EvaluateBranchAsync(permission, branch, ct))) return true;
            return false;
        }
        if (await AnyBranch("ROOM_VIEW")) { links.Add("/room-directory"); links.Add("/rooms/status"); }
        if (links.Contains("/room-directory") || await AnyBranch("MASTER_DATA_MANAGE")) links.Add("/hotel");
        if (await AnyBranch("DASHBOARD_VIEW")) links.Add("/dashboard");
        try { await audit.BranchesAsync(null, ct); links.Add("/audit"); }
        catch (InsightException error) when (error.Status == 403) { }
        return links.ToArray();
    }

    private static bool Granted(PermissionEvaluation result)
    {
        if (result.Decision == PermissionDecision.Unavailable)
            throw new InsightException(503, "AUTH_UNAVAILABLE", "Authorization source unavailable.");
        return result.IsGranted;
    }
}
