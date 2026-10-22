using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    private static string Required(string? value, string field, int? maximum = null, int minimum = 1)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length < minimum)
            throw new ValidationException($"{field} must contain at least {minimum} trimmed characters.");
        CheckLength(normalized, field, maximum);
        return normalized;
    }

    private static void CheckLength(string? value, string field, int? maximum)
    {
        if (maximum.HasValue && value?.Length > maximum.Value)
            throw new ValidationException($"{field} must not exceed {maximum} characters.");
    }

    private static string? OptionalNote(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task RequireActorAsync(long actor, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Users.AsNoTracking().AnyAsync(u => u.UserId == actor, cancellationToken))
            throw new ValidationException("ActorUserId does not exist.");
    }

    private async Task ValidateMasterAsync(AssetMasterRequest request, CancellationToken cancellationToken)
    {
        Required(request.AssetCode, "AssetCode", 50);
        Required(request.AssetName, "AssetName", 200);
        CheckLength(request.SerialNumber, "SerialNumber", 100);
        CheckLength(request.Brand, "Brand", 100);
        CheckLength(request.Model, "Model", 100);
        await RequireActorAsync(request.ActorUserId, cancellationToken);
        if (!await _dbContext.AssetTypes.AsNoTracking()
            .AnyAsync(t => t.AssetTypeId == request.AssetTypeId && t.IsActive, cancellationToken))
            throw new ValidationException("AssetTypeId must identify an active AssetType.");
    }

    private static void RequireOperational(Asset asset)
    {
        if (asset.LifecycleStatus is not ("IN_USE" or "UNDER_REPAIR"))
            throw new ValidationException("This lifecycle does not allow Phase 1 mutations.");
    }

    private static string Condition(string? value, string field)
    {
        var status = Required(value, field, 40).ToUpperInvariant();
        if (status is not ("GOOD" or "FAIR" or "POOR" or "FAULTY"))
            throw new ValidationException($"{field} must be GOOD, FAIR, POOR or FAULTY.");
        return status;
    }

    private static int Offset(AssetPageRequest request)
    {
        if (request.Page <= 0 || request.PageSize <= 0 || request.PageSize > 100)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 100.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            throw new ValidationException("Pagination offset exceeds the supported range.");
        return (int)offset;
    }

    private static AssetPagedResponse<T> Page<T>(IReadOnlyList<T> items, int count, AssetPageRequest request) => new()
    {
        Items = items, Page = request.Page, PageSize = request.PageSize,
        TotalCount = count,
        TotalPages = (int)(((long)count + request.PageSize - 1) / request.PageSize)
    };

    // SQL Server names auto-generated UNIQUE constraints differently per database.
    // Match the AssetCode constraint family AND the exact target table; do not
    // turn other unique-key, FK or audit failures into a duplicate-code response.
    private static bool IsDuplicateAssetCode(DbUpdateException exception) =>
        exception.InnerException is SqlException sql &&
        sql.Errors.Cast<SqlError>().Any(error =>
            error.Number is 2601 or 2627 &&
            error.Message.Contains("'dbo.assets'", StringComparison.Ordinal) &&
            error.Message.Contains("'UQ__assets__", StringComparison.Ordinal));

    private static void ApplyMaster(Asset asset, AssetMasterRequest request)
    {
        asset.AssetCode = request.AssetCode.Trim();
        asset.AssetName = request.AssetName.Trim();
        asset.AssetTypeId = request.AssetTypeId;
        asset.SerialNumber = request.SerialNumber;
        asset.Brand = request.Brand;
        asset.Model = request.Model;
        asset.PurchaseDate = request.PurchaseDate;
        asset.WarrantyExpiryDate = request.WarrantyExpiryDate;
        asset.Description = request.Description;
    }
}
