using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetRegistrationInput(string AssetCode, string AssetName, long AssetTypeId,
    long BranchId, long CurrentDepartmentId, string? SerialNumber, string? Brand, string? Model,
    DateOnly? PurchaseDate, DateOnly? WarrantyExpiryDate, string? Description, IReadOnlyList<EvidenceUpload>? Photos);

public sealed class AssetRegistrationWriter(HotelOpsDbContext db, InsightAccess access, AssetRegistrationAuthority authority)
{
    public async Task<object> CreateAsync(AssetRegistrationInput input, string evidenceRoot, CancellationToken ct)
    {
        var fields = AssetEditService.Normalize(new(input.AssetCode, input.AssetName, input.AssetTypeId,
            input.SerialNumber, input.Brand, input.Model, input.PurchaseDate, input.WarrantyExpiryDate, input.Description));
        AssetRegistrationPhotos.Validate(input.Photos);
        using var files = new EvidenceFileLifetime();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await authority.RequireAsync(input.BranchId, ct);
        if (!await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == input.CurrentDepartmentId && d.BranchId == input.BranchId && d.IsActive, ct)
            || !await db.AssetTypes.AsNoTracking().AnyAsync(t => t.AssetTypeId == fields.AssetTypeId && t.IsActive, ct))
            throw new InsightException(400, "MSG03", "Select an active category and custodian department in the asset branch.");
        if (await db.Assets.AsNoTracking().AnyAsync(a => EF.Functions.Collate(a.AssetCode, "Latin1_General_100_CI_AS") == fields.AssetCode, ct))
            throw new InsightException(409, "MSG35", "Asset code already exists.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var asset = new Asset { AssetCode = fields.AssetCode, AssetName = fields.AssetName, AssetTypeId = fields.AssetTypeId,
            BranchId = input.BranchId, CurrentDepartmentId = input.CurrentDepartmentId, SerialNumber = fields.SerialNumber,
            Brand = fields.Brand, Model = fields.Model, PurchaseDate = fields.PurchaseDate, WarrantyExpiryDate = fields.WarrantyExpiryDate,
            Description = fields.Description, LifecycleStatus = "IN_USE", ConditionStatus = "GOOD", CreatedAt = now };
        db.Assets.Add(asset);
        await db.SaveChangesAsync(ct);
        if (input.Photos is { Count: > 0 })
            await AssetRegistrationPhotos.StageAsync(db, asset.AssetId, access.UserId, now, input.Photos, evidenceRoot, files, ct);
        db.AuditLogs.Add(new AuditLog { EntityType = "assets", EntityId = asset.AssetId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BranchId = asset.BranchId, ActorUserId = access.UserId, Action = "CREATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(AssetEditService.Snapshot(asset)) });
        await db.SaveChangesAsync(ct);
        await authority.RequireAsync(input.BranchId, ct);
        await files.BeforeCommitAsync(ct);
        await tx.CommitAsync(ct);
        return new { asset.AssetId, Message = "MSG08" };
    }
}
