using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Rbac;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Authentication;

public sealed class HomeNavigationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, TimeProvider clock, AuditAccess audit, HotelOps.Api.Services.Assets.AssetMaintenanceHistoryReader assetHistory,
    HotelOps.Api.Services.Assets.AssetLocationReview assetLocations, HotelOps.Api.Services.Assets.AssetProfileAccess assetProfiles)
{
    public sealed record OperationGrant(long? BranchId, string Permission, string Scope);
    public Task<UserDirectoryReadResult<OperationGrant[]>> OperationGrantsAsync(SessionReference ticket, CancellationToken ct)
        => new UserDirectoryReadGuard(db, current, permissions, clock).RunAuthorizedAsync(ticket,
            _ => Task.FromResult(new PermissionEvaluation(PermissionDecision.Granted)), async () =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var paths = await (from assignment in db.UserRoles.AsNoTracking()
                    join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
                    where assignment.UserId == ticket.UserId
                    select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                        assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                        assignment.Branch != null && assignment.Branch.IsActive,
                        db.UserBranchAccesses.Any(b => b.UserId == ticket.UserId && b.BranchId == assignment.BranchId
                            && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
                return paths.Where(p => PermissionGrantRules.IsEligible(p, now) && PermissionGrantRules.EffectiveScope(p) != null)
                    .Select(p => new OperationGrant(p.BranchId, p.PermissionCode, PermissionGrantRules.EffectiveScope(p)!)).Distinct().ToArray();
            }, ct);
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
        var links = new List<string> { "/profile", "/change-password", "/notifications" };
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
        if (await AnyBranch("INSPECTION_VIEW")) links.Add("/inspection-history");
        if (await AnyBranch("CHECKLIST_MANAGE")) links.Add("/inspections/checklists");
        if (await AnyBranch("CLEANING_VIEW")) links.Add("/rooms/turnover");
        foreach (var branch in branches)
            if (Granted(await permissions.EvaluateBranchAsync("CLEANING_VIEW", branch, ct))
                && Granted(await permissions.EvaluateBranchAsync("CLEANING_ASSIGN", branch, ct)))
            { links.Add("/cleaning/assignment"); break; }
        if (Granted(await RoleCatalogAuthority.EvaluateAsync(db, current, permissions, clock, ct))) links.Add("/roles");
        if (!current.TryGetCurrentUser(out var actor)) return [];
        foreach (var branch in branches)
            if (await HotelOps.Api.Services.Inventory.StockReturnReadAccess.QueueBranchAsync(db, actor.UserId, branch, "mine", ct)
                || await HotelOps.Api.Services.Inventory.StockReturnReadAccess.QueueBranchAsync(db, actor.UserId, branch, "post", ct)
                || await HotelOps.Api.Services.Inventory.StockReturnReadAccess.QueueBranchAsync(db, actor.UserId, branch, "approve", ct))
            { links.Add("/inventory/return-requests"); break; }
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var branch in branches)
            if (await assetHistory.AllowedAsync(branch, ct)) { links.Add("/assets/maintenance-history"); break; }
        foreach (var branch in branches)
            if (await assetLocations.AllowedAsync(branch, ct)) { links.Add("/assets/locations"); break; }
        foreach (var branch in branches)
            if (await assetProfiles.AllowedAsync(branch, ct)) { links.Add("/assets"); break; }
        var data = new EfPermissionDataSource(db);
        async Task<bool> AnyOwnOrBranch(string code)
        {
            var paths = await data.ReadPathsAsync(actor.UserId, code, now, ct);
            return paths.Any(p => p.PermissionCode == code && PermissionGrantRules.IsEligible(p, now)
                && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                    || p.BranchId is { } branch && branches.Contains(branch)
                        && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN"));
        }
        if (await AnyOwnOrBranch("TASK_VIEW")) links.Add("/operational-tasks");
        if (await AnyOwnOrBranch("MR_VIEW")) links.Add("/operations/material-requests");
        if ((await HotelOps.Api.Services.MaterialRequests.MaterialCreationAccess.BranchesAsync(db, actor.UserId, now, ct)).Length > 0)
            links.Add("/material-requests/new");
        var requestScope = await new HotelOps.Api.Services.InternalRequests.InternalRequestReadAccess(db).ReadAsync(actor.UserId, now, ct);
        if (requestScope.Global || requestScope.Branches.Length > 0 || requestScope.RelatedBranches.Length > 0)
            links.Add("/internal-requests");
        var intake = new HotelOps.Api.Services.InternalRequests.InternalRequestIntakeAccess(db);
        foreach (var branch in branches)
            if (await intake.AllowsAsync(actor.UserId, branch, ct))
            { links.Add("/internal-requests/new"); break; }
        if (await AnyOwnOrBranch("CLEANING_VIEW")) { links.Add("/cleaning/tasks"); links.Add("/rework"); }
        foreach (var branch in branches)
            if (Granted(await permissions.EvaluateResourceAsync("ATTENDANCE_RECORD", new("workday_self_branch", branch), ct)))
            { links.Add("/workday"); links.Add("/workday/scheduled"); links.Add("/my-time-log"); break; }
        foreach (var branch in branches)
            if (await HotelOps.Api.Services.WorkTime.TimeCorrectionAccess.OwnAsync(db, actor.UserId, branch, now, ct))
            { links.Add("/time-corrections"); break; }
        foreach (var branch in branches)
            if (await HotelOps.Api.Services.WorkTime.ShiftHandoverAccess.FeatureAsync(db, actor.UserId, branch, "SHIFT_HANDOVER_VIEW", true, now, ct))
            { links.Add("/workday/handovers"); break; }
        var masterPaths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == actor.UserId && assignment.Role.RoleCode == "SYS_ADMIN"
                && grant.Permission.PermissionCode == "MASTER_DATA_MANAGE"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == actor.UserId && b.BranchId == assignment.BranchId
                    && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
        if (masterPaths.Any(p => PermissionGrantRules.IsEligible(p, now)
            && PermissionGrantRules.EffectiveScope(p) is "GLOBAL" or "BRANCH"))
        {
            links.Add("/structure/work-categories");
        }
        if (masterPaths.Any(p => PermissionGrantRules.IsEligible(p, now)
            && PermissionGrantRules.EffectiveScope(p) == "GLOBAL"))
        { links.Add("/structure/priority-sla"); links.Add("/structure/reference-data"); }
        var replenishmentPaths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == actor.UserId && (assignment.Role.RoleCode == "STOREKEEPER" || assignment.Role.RoleCode == "OPERATIONS_MANAGER")
                && grant.Permission.PermissionCode == "REPLENISHMENT_VIEW"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == actor.UserId && b.BranchId == assignment.BranchId
                    && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
        if (replenishmentPaths.Any(p => PermissionGrantRules.IsEligible(p, now)
            && PermissionGrantRules.EffectiveScope(p) is "GLOBAL" or "BRANCH")) links.Add("/operations/replenishments");
        foreach (var branch in branches)
        {
            var scope = await IncidentReadAccess.ReadAsync(db, actor.UserId, branch, ct);
            if (scope.Lead || scope.Staff) { links.Add("/operations/incidents"); break; }
        }
        foreach (var branch in branches)
        {
            var scope = await HotelOps.Api.Services.Maintenance.MaintenanceReadAccess.ReadAsync(db, actor.UserId, branch, ct);
            if (scope.Lead || scope.Staff) { links.Add("/operations/maintenance"); break; }
        }
        // Reporters need a creation entry even when the maintenance list is not
        // available to their role. Keep creation and list access independent.
        var incidentCreationPaths = await data.ReadPathsAsync(actor.UserId, "INCIDENT_CREATE", now, ct);
        foreach (var branch in branches)
        {
            if (!incidentCreationPaths.Any(p => p.BranchId == branch
                && p.PermissionCode == "INCIDENT_CREATE" && PermissionGrantRules.IsEligible(p, now)
                && PermissionGrantRules.EffectiveScope(p) is "OWN" or "BRANCH")) continue;
            var scope = await IncidentReadAccess.ReadAsync(db, actor.UserId, branch, ct, sourceRecord: true);
            if (scope.Lead || scope.Staff) { links.Add("/incidents/new"); break; }
        }
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive && d.Branch.IsActive)
            .Select(d => new { d.DepartmentId, d.BranchId }).ToArrayAsync(ct);
        var shiftScope = new HotelOps.Api.Services.WorkTime.SqlShiftPreviewScopeData(db, clock);
        foreach (var department in departments)
            if (await shiftScope.ActorScopeAsync(actor.UserId, department.BranchId, department.DepartmentId, ct))
            { links.Add("/workday/shift-plans"); break; }
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
