using HotelOps.Api.DTOs.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService
{
    public async Task<AssetLocationStatisticsResponse> GetStatisticsAsync(AssetStatisticsRequest request, CancellationToken cancellationToken)
    {
        var snapshotAt = DateTime.UtcNow;
        var query = _dbContext.Assets.AsNoTracking();
        if (request.BranchId is long branch) query = query.Where(a => a.BranchId == branch);
        if (request.AssetTypeId is long type) query = query.Where(a => a.AssetTypeId == type);
        if (request.DepartmentId is long department) query = query.Where(a => a.CurrentDepartmentId == department);
        var condition = OptionalNote(request.ConditionStatus)?.ToUpperInvariant();
        var lifecycle = OptionalNote(request.LifecycleStatus)?.ToUpperInvariant();
        if (condition is not null) query = query.Where(a => a.ConditionStatus == condition);
        if (lifecycle is not null) query = query.Where(a => a.LifecycleStatus == lifecycle);
        // One aggregate statement over CURRENT assets. No history joins or double counts.
        var groups = await query.GroupBy(a => new
        {
            a.BranchId,
            LocationType = a.CurrentRoomId != null && a.CurrentAreaId != null ? "CORRUPT" :
                a.CurrentRoomId != null ? "ROOM" : a.CurrentAreaId != null ? "AREA" : "UNASSIGNED",
            LocationId = a.CurrentRoomId != null && a.CurrentAreaId != null ? (long?)null : a.CurrentRoomId ?? a.CurrentAreaId,
            DisplayName = a.CurrentRoomId != null && a.CurrentAreaId != null ? null :
                a.CurrentRoom != null ? a.CurrentRoom.RoomNumber : a.CurrentArea != null ? a.CurrentArea.AreaName : null,
            a.LifecycleStatus
        }).Select(g => new { g.Key.BranchId, g.Key.LocationType, g.Key.LocationId,
            g.Key.DisplayName, g.Key.LifecycleStatus, Count = g.Count() }).ToListAsync(cancellationToken);
        return new AssetLocationStatisticsResponse
        {
            SnapshotAt = snapshotAt,
            Items = groups.GroupBy(g => new { g.BranchId, g.LocationType, g.LocationId, g.DisplayName })
                .OrderBy(g => g.Key.BranchId).ThenBy(g => g.Key.LocationType, StringComparer.Ordinal).ThenBy(g => g.Key.LocationId)
                .Select(g => new AssetLocationStatisticsDto
                {
                    BranchId = g.Key.BranchId, LocationType = g.Key.LocationType, LocationId = g.Key.LocationId,
                    DisplayName = g.Key.DisplayName, TotalCount = g.Sum(x => x.Count),
                    OperationalCount = g.Where(x => x.LifecycleStatus is "IN_USE" or "UNDER_REPAIR").Sum(x => x.Count),
                    RetiredCount = g.Where(x => x.LifecycleStatus == "RETIRED").Sum(x => x.Count),
                    LifecycleCounts = g.OrderBy(x => x.LifecycleStatus, StringComparer.Ordinal)
                        .Select(x => new AssetLifecycleCountDto { LifecycleStatus = x.LifecycleStatus, Count = x.Count }).ToList()
                }).ToList()
        };
    }
}
