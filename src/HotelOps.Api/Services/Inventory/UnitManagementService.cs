using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Inventory;

public sealed record ManagedConversion(long ConversionId, long ItemId, long FromUomId, long ToUomId, string Factor, bool IsActive, string RowVersion);
public sealed record UnitManagementSnapshot(InventoryPaged<InventoryUnitDto> Units, InventoryUnitDto[] Choices, InventoryItemDto? Item, ManagedConversion[] Conversions)
{
    public int ConversionPage { get; init; }
    public int ConversionPageSize { get; init; } = 25;
    public int ConversionTotalCount { get; init; }
}

public sealed class UnitManagementService(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session,
    IPermissionService permissions, InventoryService inventory)
{
    public async Task RequireAsync(CancellationToken ct)
    {
        await session.RequireAsync(ct);
        var user = access.UserId;
        // Units/conversions have no branch owner in the existing schema. Keep
        // the existing GLOBAL catalog guard; a branch grant never becomes global.
        InsightAccess.Require(await permissions.EvaluateGlobalAsync("ITEM_MASTER_MANAGE", ct));
        _ = await access.BranchesAsync("INVENTORY_VIEW", null, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && (r.BranchId == null || r.Branch!.IsActive && db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == r.BranchId && a.GrantedAt <= now && a.RevokedAt == null)), ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current Storekeeper role and global ITEM_MASTER_MANAGE catalog grant required; no scope expansion is inferred.");
    }
    public async Task<UnitManagementSnapshot> ReadAsync(int page, int size, long? itemId, int conversionPage, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || conversionPage is < 1 or > 1000000 || size is < 1 or > 100 || itemId is <= 0) throw new InsightException(400, "INVALID_FILTER", "Invalid catalogue filters.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await RequireAsync(ct);
        var units = await inventory.GetUnits(new(page, size), ct);
        if (await db.UnitsOfMeasures.CountAsync(ct) > 50000) throw new InsightException(503, "CATALOG_LIMIT", "Unit lookup exceeds the supported catalogue size.");
        var choices = await db.UnitsOfMeasures.AsNoTracking().OrderBy(u => u.UomId)
            .Select(u => new InventoryUnitDto(u.UomId, u.UomCode, u.UomName, u.IsActive, u.Description, "")).ToArrayAsync(ct);
        InventoryItemDto? item = null;
        ManagedConversion[] conversions = [];
        var conversionCount = 0;
        if (itemId is {} id)
        {
            item = await inventory.GetItem(id, ct) ?? throw new InsightException(404, "ITEM_NOT_FOUND", "Item not found.");
            var source = db.UomConversions.AsNoTracking().Where(c => c.ItemId == id);
            conversionCount = await source.CountAsync(ct);
            var raw = await source.OrderBy(c => c.ConversionId).Skip((conversionPage - 1) * 25).Take(25).ToArrayAsync(ct);
            conversions = raw.Select(c => new ManagedConversion(c.ConversionId, id, c.FromUomId, c.ToUomId,
                c.ConversionFactor.ToString(CultureInfo.InvariantCulture), c.IsActive, Convert.ToBase64String(c.RowVersion))).ToArray();
        }
        await RequireAsync(ct); await tx.CommitAsync(ct);
        return new(units, choices, item, conversions) { ConversionPage = conversionPage, ConversionTotalCount = conversionCount };
    }
}

public sealed partial class InventoryService
{
    private async Task<IDbContextTransaction> BeginUnitWrite(long actor, Func<CancellationToken, Task> authorize, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([actor], ct);
            await Gate("HotelOps:Inventory:Catalog", ct);
            await Actor(actor, ct);
            await authorize(ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
}
