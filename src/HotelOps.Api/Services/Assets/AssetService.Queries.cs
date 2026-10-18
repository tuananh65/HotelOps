using HotelOps.Api.DTOs.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetPagedResponse<AssetListDto>> GetAssetsAsync(AssetQueryRequest request, CancellationToken cancellationToken)
    {
        var offset = Offset(request);
        var query = _dbContext.Assets.AsNoTracking();
        if (request.BranchId is long branch) query = query.Where(a => a.BranchId == branch);
        if (request.AssetTypeId is long type) query = query.Where(a => a.AssetTypeId == type);
        if (request.RoomId is long room) query = query.Where(a => a.CurrentRoomId == room);
        if (request.AreaId is long area) query = query.Where(a => a.CurrentAreaId == area);
        if (request.DepartmentId is long department) query = query.Where(a => a.CurrentDepartmentId == department);
        var condition = OptionalNote(request.ConditionStatus)?.ToUpperInvariant();
        var lifecycle = OptionalNote(request.LifecycleStatus)?.ToUpperInvariant();
        if (condition is not null) query = query.Where(a => a.ConditionStatus == condition);
        if (lifecycle is not null) query = query.Where(a => a.LifecycleStatus == lifecycle);
        var search = OptionalNote(request.Search);
        if (search is not null)
        {
            // EF translates Contains with literal substring semantics, including %, _ and [.
            query = query.Where(a => a.AssetCode.Contains(search) || a.AssetName.Contains(search) ||
                (a.SerialNumber != null && a.SerialNumber.Contains(search)));
        }
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(a => a.AssetCode).ThenBy(a => a.AssetId)
            .Skip(offset).Take(request.PageSize).Select(a => new AssetListDto
            {
                AssetId = a.AssetId, AssetCode = a.AssetCode, AssetName = a.AssetName,
                AssetTypeId = a.AssetTypeId, AssetTypeCode = a.AssetType.TypeCode, AssetTypeName = a.AssetType.TypeName,
                BranchId = a.BranchId, BranchName = a.Branch.BranchName,
                CurrentRoomId = a.CurrentRoomId, RoomNumber = a.CurrentRoom == null ? null : a.CurrentRoom.RoomNumber,
                CurrentAreaId = a.CurrentAreaId, AreaName = a.CurrentArea == null ? null : a.CurrentArea.AreaName,
                CurrentDepartmentId = a.CurrentDepartmentId, DepartmentName = a.CurrentDepartment == null ? null : a.CurrentDepartment.DepartmentName,
                CustodianUserId = a.CustodianUserId, CustodianName = a.CustodianUser == null ? null : a.CustodianUser.FullName,
                ConditionStatus = a.ConditionStatus, LifecycleStatus = a.LifecycleStatus
            }).ToListAsync(cancellationToken);
        return Page(items, count, request);
    }

    public Task<AssetDetailDto?> GetAssetAsync(long id, CancellationToken cancellationToken) =>
        _dbContext.Assets.AsNoTracking().Where(a => a.AssetId == id).Select(a => new AssetDetailDto
        {
                AssetId = a.AssetId, AssetCode = a.AssetCode, AssetName = a.AssetName,
                AssetTypeId = a.AssetTypeId, AssetTypeCode = a.AssetType.TypeCode, AssetTypeName = a.AssetType.TypeName,
                BranchId = a.BranchId, BranchName = a.Branch.BranchName,
                CurrentRoomId = a.CurrentRoomId, RoomNumber = a.CurrentRoom == null ? null : a.CurrentRoom.RoomNumber,
                CurrentAreaId = a.CurrentAreaId, AreaName = a.CurrentArea == null ? null : a.CurrentArea.AreaName,
                CurrentDepartmentId = a.CurrentDepartmentId, DepartmentName = a.CurrentDepartment == null ? null : a.CurrentDepartment.DepartmentName,
                CustodianUserId = a.CustodianUserId, CustodianName = a.CustodianUser == null ? null : a.CustodianUser.FullName,
                ConditionStatus = a.ConditionStatus, LifecycleStatus = a.LifecycleStatus,
                SerialNumber = a.SerialNumber, Brand = a.Brand, Model = a.Model,
                PurchaseDate = a.PurchaseDate, WarrantyExpiryDate = a.WarrantyExpiryDate,
                AcquisitionCost = a.AcquisitionCost, Description = a.Description,
                CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt
        }).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AssetTypeLookupDto>> GetAssetTypesAsync(CancellationToken cancellationToken) =>
        await _dbContext.AssetTypes.AsNoTracking().Where(t => t.IsActive)
            .OrderBy(t => t.TypeCode).ThenBy(t => t.AssetTypeId)
            .Select(t => new AssetTypeLookupDto
            {
                AssetTypeId = t.AssetTypeId, TypeCode = t.TypeCode,
                TypeName = t.TypeName, Description = t.Description
            }).ToListAsync(cancellationToken);

    public async Task<AssetPagedResponse<AssetConditionHistoryDto>?> GetConditionHistoryAsync(
        long id, AssetPageRequest request, CancellationToken cancellationToken)
    {
        var offset = Offset(request);
        if (!await _dbContext.Assets.AsNoTracking().AnyAsync(a => a.AssetId == id, cancellationToken))
            return null;
        var query = _dbContext.AssetConditionHistories.AsNoTracking().Where(h => h.AssetId == id);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.ConditionHistoryId)
            .Skip(offset).Take(request.PageSize).Select(h => new AssetConditionHistoryDto
            {
                ConditionHistoryId = h.ConditionHistoryId, AssetId = h.AssetId,
                OldCondition = h.OldCondition, NewCondition = h.NewCondition, Note = h.Note,
                RelatedIncidentId = h.RelatedIncidentId, ChangedBy = h.ChangedBy, ChangedAt = h.ChangedAt
            }).ToListAsync(cancellationToken);
        return Page(items, count, request);
    }

    public async Task<AssetPagedResponse<AssetLocationHistoryDto>?> GetLocationHistoryAsync(
        long id, AssetPageRequest request, CancellationToken cancellationToken)
    {
        var offset = Offset(request);
        if (!await _dbContext.Assets.AsNoTracking().AnyAsync(a => a.AssetId == id, cancellationToken))
            return null;
        var query = _dbContext.AssetLocationHistories.AsNoTracking().Where(h => h.AssetId == id);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(h => h.EffectiveAt).ThenByDescending(h => h.LocationHistoryId)
            .Skip(offset).Take(request.PageSize).Select(h => new AssetLocationHistoryDto
            {
                LocationHistoryId = h.LocationHistoryId, AssetId = h.AssetId,
                FromLocationType = h.FromLocationType, FromLocationId = h.FromLocationId,
                ToLocationType = h.ToLocationType, ToLocationId = h.ToLocationId,
                EffectiveAt = h.EffectiveAt, MovedBy = h.MovedBy, Reason = h.Reason
            }).ToListAsync(cancellationToken);
        return Page(items, count, request);
    }
}
