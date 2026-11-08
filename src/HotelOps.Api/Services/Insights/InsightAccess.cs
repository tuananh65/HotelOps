using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

public sealed class InsightException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class InsightAccess(HotelOpsDbContext db, IPermissionService permissions, ICurrentUserAccessor caller)
{
    public long UserId
    {
        get
        {
            if (!caller.TryGetCurrentUser(out var user) || user.UserId <= 0 || user.SessionId <= 0)
                throw new InsightException(401, "AUTH_REQUIRED", "Authentication required.");
            if (user.IsRestricted) throw new InsightException(403, "RESTRICTED", "Restricted session.");
            return user.UserId;
        }
    }

    public static void Require(PermissionEvaluation evaluation)
    {
        if (evaluation.IsGranted) return;
        throw evaluation.Decision switch
        {
            PermissionDecision.Unauthenticated => new InsightException(401, "AUTH_REQUIRED", "Authentication required."),
            PermissionDecision.Unavailable => new InsightException(503, "AUTH_UNAVAILABLE", "Authorization source unavailable."),
            _ => new InsightException(403, "ACCESS_DENIED", "Access denied.")
        };
    }

    public async Task<long[]> BranchesAsync(string permission, long? branchId, CancellationToken ct)
    {
        _ = UserId;
        if (branchId is { } id)
        {
            Require(await permissions.EvaluateBranchAsync(permission, id, ct));
            return [id];
        }
        var candidates = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId)
            .Select(b => b.BranchId).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var candidate in candidates)
        {
            var evaluation = await permissions.EvaluateBranchAsync(permission, candidate, ct);
            if (evaluation.Decision is PermissionDecision.Unavailable or PermissionDecision.Unauthenticated)
                Require(evaluation);
            if (evaluation.IsGranted) allowed.Add(candidate);
        }
        if (allowed.Count == 0 && !await HasGlobalAsync(permission, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Access denied.");
        return allowed.ToArray();
    }

    public async Task<bool> HasGlobalAsync(string permission, CancellationToken ct)
    {
        _ = UserId;
        var evaluation = await permissions.EvaluateGlobalAsync(permission, ct);
        if (evaluation.Decision is PermissionDecision.Unavailable or PermissionDecision.Unauthenticated) Require(evaluation);
        return evaluation.IsGranted;
    }

    public async Task<InsightFilters> FiltersAsync(string permission, InsightQuery query, DateTimeOffset now, CancellationToken ct)
    {
        var branches = await BranchesAsync(permission, query.BranchId, ct);
        if (query.DepartmentId is { } department && !await db.Departments.AsNoTracking()
                .AnyAsync(d => d.DepartmentId == department && branches.Contains(d.BranchId), ct))
            throw new InsightException(403, "DEPARTMENT_SCOPE", "Department outside permitted branch scope.");
        var to = query.To ?? DateOnly.FromDateTime(now.UtcDateTime);
        var from = query.From ?? (to.DayNumber >= 29 ? to.AddDays(-29) : DateOnly.MinValue);
        if (from > to || to == DateOnly.MaxValue)
            throw new InsightException(400, "INVALID_PERIOD", "Select a valid UTC calendar period.");
        return new(branches, query.DepartmentId, from, to);
    }

    public async Task<InsightOptions> OptionsAsync(string permission, CancellationToken ct)
    {
        var ids = await BranchesAsync(permission, null, ct);
        var branches = await db.Branches.AsNoTracking().Where(b => ids.Contains(b.BranchId)).OrderBy(b => b.BranchId)
            .Select(b => new InsightBranch(b.BranchId, b.BranchCode, b.BranchName)).ToArrayAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => ids.Contains(d.BranchId)).OrderBy(d => d.DepartmentId)
            .Select(d => new InsightDepartment(d.DepartmentId, d.BranchId, d.DepartmentName)).ToArrayAsync(ct);
        return new(branches, departments, "UTC", 30, 50000);
    }

    public static DateTime Start(InsightFilters filters) => filters.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public static DateTime End(InsightFilters filters) => filters.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public static DateTimeOffset Utc(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Utc));
}
