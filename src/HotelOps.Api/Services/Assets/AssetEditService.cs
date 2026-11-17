using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetEditInput(string AssetCode, string AssetName, long AssetTypeId, string? SerialNumber,
    string? Brand, string? Model, DateOnly? PurchaseDate, DateOnly? WarrantyExpiryDate, string? Description);

public sealed class AssetEditService(HotelOpsDbContext db, AssetProfileAccess profile, InsightAccess access, IAssetService assets)
{
    public async Task<bool> CanEditAsync(long id, CancellationToken ct)
    {
        try { await LoadAsync(id, false, ct); return true; }
        catch (InsightException e) when (e.Status is 403 or 404 or 409) { return false; }
    }
    private async Task RequireAsync(long branch, CancellationToken ct)
    {
        await profile.RequireAsync(branch, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == access.UserId && r.Role.RoleCode == "OPERATIONS_MANAGER"
            && r.IsActive && r.Role.IsActive && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now
            && (r.ExpiresAt == null || r.ExpiresAt > now), ct)) throw new InsightException(403, "ACCESS_DENIED", "Operations Manager required.");
        await access.BranchesAsync("ASSET_MANAGE", branch, ct);
    }
    private async Task<Asset> LoadAsync(long id, bool write, CancellationToken ct)
    {
        var asset = write
            ? await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={id}").AsNoTracking().SingleOrDefaultAsync(ct)
            : await db.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.AssetId == id, ct);
        if (asset == null) throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
        await RequireAsync(asset.BranchId, ct);
        if (!await profile.Query(asset.BranchId).AnyAsync(a => a.AssetId == id, ct)) throw new InsightException(409, "ASSET_SCOPE_INCONSISTENT", "Asset placement/custody requires correction.");
        if (asset.CurrentDepartmentId == null || !await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == asset.CurrentDepartmentId && d.BranchId == asset.BranchId, ct))
            throw new InsightException(409, "ASSET_CUSTODY_REQUIRED", "A valid custodian department is required; use the custody workflow to correct it.");
        // Preserve the existing AssetService.RequireOperational lifecycle guard.
        if (asset.LifecycleStatus is not ("IN_USE" or "UNDER_REPAIR")) throw new InsightException(409, "ASSET_NOT_OPERATIONAL", "This asset cannot be edited.");
        return asset;
    }
    public async Task<object> ReadAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await LoadAsync(id, false, ct);
        var version = await AssetVersion.ReadAsync(db, id, ct);
        var categories = await db.AssetTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.TypeCode)
            .Select(t => new { Id = t.AssetTypeId, Name = t.TypeName }).ToArrayAsync(ct);
        var detail = await assets.GetAssetAsync(id, ct);
        await RequireAsync(asset.BranchId, ct); await tx.CommitAsync(ct);
        return new { Asset = detail, Version = version, Categories = categories };
    }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static AssetEditInput Normalize(AssetEditInput input)
    {
        var normalized = input with { AssetCode = input.AssetCode?.Trim() ?? "", AssetName = input.AssetName?.Trim() ?? "",
            SerialNumber = Optional(input.SerialNumber), Brand = Optional(input.Brand), Model = Optional(input.Model), Description = Optional(input.Description) };
        if (normalized.AssetCode.Length is < 1 or > 50 || normalized.AssetName.Length is < 1 or > 200 || normalized.AssetTypeId <= 0
            || normalized.SerialNumber?.Length > 100 || normalized.Brand?.Length > 100 || normalized.Model?.Length > 100
            || normalized.PurchaseDate != null && normalized.WarrantyExpiryDate < normalized.PurchaseDate)
            throw new InsightException(400, "MSG03", "Code (1–50), name (1–200), category and valid warranty dates required; serial/brand/model maximum 100 characters.");
        return normalized;
    }
    private static AssetEditInput Fields(Asset a) => new(a.AssetCode, a.AssetName, a.AssetTypeId, a.SerialNumber, a.Brand, a.Model, a.PurchaseDate, a.WarrantyExpiryDate, a.Description);
    private static Dictionary<string, object?> Snapshot(Asset a) => new()
    {
        ["asset_id"] = a.AssetId, ["branch_id"] = a.BranchId, ["asset_code"] = a.AssetCode, ["asset_name"] = a.AssetName,
        ["asset_type_id"] = a.AssetTypeId, ["serial_number"] = a.SerialNumber, ["brand"] = a.Brand, ["model"] = a.Model,
        ["purchase_date"] = a.PurchaseDate, ["warranty_expiry_date"] = a.WarrantyExpiryDate, ["description"] = a.Description,
        ["condition_status"] = a.ConditionStatus, ["lifecycle_status"] = a.LifecycleStatus,
        ["current_room_id"] = a.CurrentRoomId, ["current_area_id"] = a.CurrentAreaId,
        ["current_department_id"] = a.CurrentDepartmentId, ["custodian_user_id"] = a.CustodianUserId,
        ["acquisition_cost"] = a.AcquisitionCost, ["created_at"] = a.CreatedAt, ["updated_at"] = a.UpdatedAt
    };

    public async Task<object> SaveAsync(long id, string? version, AssetEditInput input, CancellationToken ct)
    {
        input = Normalize(input);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        var asset = await LoadAsync(id, true, ct);
        var token = await AssetVersion.RequireAsync(db, id, version, ct);
        if (!await db.AssetTypes.AsNoTracking().AnyAsync(t => t.AssetTypeId == input.AssetTypeId && t.IsActive, ct))
            throw new InsightException(400, "MSG03", "Select an active asset category.");
        if (await db.Assets.AsNoTracking().AnyAsync(a => a.AssetId != id && EF.Functions.Collate(a.AssetCode, "Latin1_General_100_CI_AS") == input.AssetCode, ct))
            throw new InsightException(409, "MSG35", "Asset code already exists.");
        var before = Fields(asset);
        if (before != input)
        {
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.assets SET asset_code={input.AssetCode},asset_name={input.AssetName},asset_type_id={input.AssetTypeId},
                  serial_number={input.SerialNumber},brand={input.Brand},model={input.Model},purchase_date={input.PurchaseDate},
                  warranty_expiry_date={input.WarrantyExpiryDate},description={input.Description},updated_at={now}
                WHERE asset_id={id} AND branch_id={asset.BranchId} AND row_version={token}
                """, ct);
            if (changed != 1) throw new InsightException(412, "MSG04", "Asset changed. Reload before saving.");
            var oldValues = Snapshot(asset);
            var newValues = Snapshot(await db.Assets.AsNoTracking().SingleAsync(a => a.AssetId == id, ct));
            db.AuditLogs.Add(new AuditLog { EntityType = "assets", EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BranchId = asset.BranchId, ActorUserId = access.UserId, Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(oldValues), NewValues = JsonSerializer.Serialize(newValues),
                ChangedFields = JsonSerializer.Serialize(newValues.Keys.Where(k => k != "updated_at" && !Equals(oldValues[k], newValues[k])).ToArray()) });
            await db.SaveChangesAsync(ct);
        }
        await RequireAsync(asset.BranchId, ct);
        var result = new { AssetId = id, Version = await AssetVersion.ReadAsync(db, id, ct), Message = "MSG08" };
        await tx.CommitAsync(ct); return result;
    }
}
