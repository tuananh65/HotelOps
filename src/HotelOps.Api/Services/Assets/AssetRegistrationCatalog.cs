using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed class AssetRegistrationCatalog(HotelOpsDbContext db, InsightAccess access, AssetRegistrationAuthority authority)
{
    public async Task<object> ReadAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BranchId)
            .Select(b => new { id = b.BranchId, name = b.BranchName }).ToArrayAsync(ct);
        var allowed = new List<long>();
        foreach (var branch in branches)
        {
            try { await authority.RequireAsync(branch.id, ct); allowed.Add(branch.id); }
            catch (InsightException e) when (e.Status == 403) { }
        }
        if (allowed.Count == 0) throw new InsightException(403, "ACCESS_DENIED", "No branch permits asset registration.");
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive && allowed.Contains(d.BranchId))
            .OrderBy(d => d.DepartmentId).Select(d => new { id = d.DepartmentId, name = d.DepartmentName, d.BranchId }).ToArrayAsync(ct);
        var categories = await db.AssetTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.AssetTypeId)
            .Select(t => new { id = t.AssetTypeId, name = t.TypeName }).ToArrayAsync(ct);
        foreach (var branch in allowed) await authority.RequireAsync(branch, ct);
        await tx.CommitAsync(ct);
        return new { branches = branches.Where(b => allowed.Contains(b.id)), departments, categories };
    }
}
