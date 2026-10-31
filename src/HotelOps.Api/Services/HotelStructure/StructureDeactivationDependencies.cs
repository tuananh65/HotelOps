using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Called only inside the retained SQL transaction/authorization boundary of UC015/016.
internal static class StructureDeactivationDependencies
{
    internal static async Task<IReadOnlyList<StructureDependencyDto>> ReadAsync(HotelOpsDbContext db,
        long branchId, long id, bool area, CancellationToken ct)
    {
        var result = new List<StructureDependencyDto>();
        var roomIds = db.Rooms.AsNoTracking().Where(x => x.BranchId == branchId
            && (area ? x.AreaId == id : x.FloorId == id)).Select(x => x.RoomId);
        var areaIds = db.Areas.AsNoTracking().Where(x => x.BranchId == branchId
            && (area ? x.AreaId == id : x.FloorId == id)).Select(x => x.AreaId);
        await Add("ACTIVE_ROOM", db.Rooms.AsNoTracking().Where(x => roomIds.Contains(x.RoomId) && x.IsActive).Select(x => x.RoomId));
        if (!area) await Add("ACTIVE_CHILD_AREA", db.Areas.AsNoTracking()
            .Where(x => areaIds.Contains(x.AreaId) && x.IsActive).Select(x => x.AreaId));
        // Current physical location, never historical location rows. Unknown lifecycle fails closed.
        var assetIds = db.Assets.AsNoTracking().Where(x => x.BranchId == branchId &&
            ((x.CurrentRoomId != null && roomIds.Contains(x.CurrentRoomId.Value)) ||
             (x.CurrentAreaId != null && areaIds.Contains(x.CurrentAreaId.Value)))).Select(x => x.AssetId);
        await Add("ACTIVE_ASSET", db.Assets.AsNoTracking().Where(x => assetIds.Contains(x.AssetId)
            && x.LifecycleStatus != "RETIRED").Select(x => x.AssetId));
        // RESOLVED is reopenable in IncidentService; only CLOSED/CANCELLED are terminal there.
        var incidentIds = db.Incidents.AsNoTracking().Where(x => x.BranchId == branchId &&
            ((x.RoomId != null && roomIds.Contains(x.RoomId.Value)) ||
             (x.AreaId != null && areaIds.Contains(x.AreaId.Value)) ||
             (x.AssetId != null && assetIds.Contains(x.AssetId.Value)))).Select(x => x.IncidentId);
        await Add("OPEN_INCIDENT", db.Incidents.AsNoTracking().Where(x => incidentIds.Contains(x.IncidentId)
            && x.Status != "CLOSED" && x.Status != "CANCELLED").Select(x => x.IncidentId));
        // No current OperationalTask writer proves COMPLETED has no remaining verification/rework.
        // V1 therefore permits only CANCELLED history; COMPLETED remains an explicit dependency.
        var tasks = db.OperationalTasks.AsNoTracking().Where(x => x.BranchId == branchId
            && ((x.RoomId != null && roomIds.Contains(x.RoomId.Value)) || (x.AreaId != null && areaIds.Contains(x.AreaId.Value)))
            && x.Status != "CANCELLED");
        await Add("OPEN_OPERATIONAL_TASK", tasks.Where(x => x.Status != "COMPLETED").Select(x => x.TaskId));
        await Add("TASK_COMPLETION_CONTRACT_UNRESOLVED", tasks.Where(x => x.Status == "COMPLETED").Select(x => x.TaskId));
        await Add("OPEN_MAINTENANCE_WORK", db.MaintenanceWorks.AsNoTracking().Where(x => x.IncidentId != null
            && incidentIds.Contains(x.IncidentId.Value) && x.Status != "CLOSED" && x.Status != "CANCELLED").Select(x => x.MaintenanceWorkId));
        return result;

        async Task Add(string kind, IQueryable<long> query)
        {
            var count = await query.LongCountAsync(ct);
            if (count != 0) result.Add(new(kind, count, await query.OrderBy(x => x).Take(100).ToArrayAsync(ct)));
        }
    }
}
