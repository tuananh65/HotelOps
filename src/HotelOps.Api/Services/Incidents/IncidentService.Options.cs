using HotelOps.Api.DTOs.Incidents;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Services.Incidents;

public sealed partial class IncidentService
{
    // Read-only creation hint. POST independently locks and verifies all sources again.
    public async Task<IncidentOptionsDto> GetOptionsAsync(long? branchId, long? roomId, CancellationToken ct)
    {
        var authority = _createAuthority ?? throw new IncidentAccessException(401);
        authority.Actor(null);
        if (branchId is <= 0 || roomId is <= 0 || roomId.HasValue && !branchId.HasValue)
            throw new ValidationException("Positive branchId is required for roomId.");
        var candidates = await _dbContext.Branches.AsNoTracking().Where(b => b.IsActive)
            .OrderBy(b => b.BranchName).Select(b => new IncidentOptionDto(b.BranchId, b.BranchName)).ToListAsync(ct);
        var branches = new List<IncidentOptionDto>();
        foreach (var branch in candidates)
            if (await authority.CanCreateAsync(branch.Id, ct)) branches.Add(branch);
        if (branchId.HasValue && !branches.Any(b => b.Id == branchId)) throw new IncidentAccessException(403);
        if (branches.Count == 0) throw new IncidentAccessException(403);
        if (!branchId.HasValue) return new() { Branches = branches };
        var rooms = await _dbContext.Rooms.AsNoTracking().Where(r => r.BranchId == branchId && r.IsActive &&
            r.Floor.IsActive && r.Floor.BranchId == branchId && (r.AreaId == null ||
                r.Area != null && r.Area.IsActive && r.Area.BranchId == branchId &&
                (r.Area.FloorId == null || r.Area.FloorId == r.FloorId)))
            .OrderBy(r => r.RoomNumber).Select(r => new IncidentOptionDto(r.RoomId, r.RoomNumber)).ToListAsync(ct);
        if (roomId.HasValue && !rooms.Any(r => r.Id == roomId))
            throw new ValidationException("Room context is unavailable. Reload the source room before reporting.");
        return new()
        {
            Branches = branches, Rooms = rooms, CanCreate = true,
            Areas = await _dbContext.Areas.AsNoTracking().Where(a => a.BranchId == branchId && a.IsActive &&
                (a.FloorId == null || a.Floor != null && a.Floor.IsActive && a.Floor.BranchId == branchId))
                .OrderBy(a => a.AreaName).Select(a => new IncidentOptionDto(a.AreaId, a.AreaName)).ToListAsync(ct),
            Assets = await _dbContext.Assets.AsNoTracking().Where(a => a.BranchId == branchId && a.LifecycleStatus != "RETIRED")
                .OrderBy(a => a.AssetName).Select(a => new IncidentOptionDto(a.AssetId, a.AssetName)).ToListAsync(ct),
            Severities = await _dbContext.ReferenceValues.AsNoTracking().Where(v => v.GroupCode == "SEVERITY" && v.IsActive)
                .Select(v => new IncidentOptionDto(v.ReferenceId, v.DisplayName)).ToListAsync(ct),
            Priorities = await _dbContext.ReferenceValues.AsNoTracking().Where(v => v.GroupCode == "PRIORITY" && v.IsActive)
                .Select(v => new IncidentOptionDto(v.ReferenceId, v.DisplayName)).ToListAsync(ct)
        };
    }
}
