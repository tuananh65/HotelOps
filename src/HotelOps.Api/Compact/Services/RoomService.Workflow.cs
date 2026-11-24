using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

public sealed partial class RoomService
{
    public async Task<object> Workflow(long id, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.RoomId == id, ct) ?? throw new ApiException(404, "ROOM_NOT_FOUND");
        await access.Require("ROOM_VIEW", room.BranchId, ct: ct);
        var cleaning = await access.Allows("CLEANING_VIEW", room.BranchId, ct: ct);
        var inspection = await access.Allows("INSPECTION_VIEW", room.BranchId, ct: ct);
        var incident = await access.Allows("INCIDENT_VIEW", room.BranchId, ct: ct);
        var cycle = db.WorkItems.AsNoTracking().Where(w => w.RoomId == id && w.BranchId == room.BranchId && w.TurnoverNo == room.TurnoverNo);
        return new {
            room.TurnoverNo,
            cleaning = cleaning ? await cycle.Where(w => w.Kind == "CLEANING" || w.Kind == "REWORK").OrderByDescending(w => w.WorkId).Take(20)
                .Select(w => new { w.WorkId, w.Kind, w.Title, w.Status, w.CompletedAt, w.DueAt }).ToArrayAsync(ct) : null,
            inspections = inspection ? await cycle.Where(w => w.Kind == "INSPECTION").OrderByDescending(w => w.WorkId).Take(20)
                .Select(w => new { w.WorkId, w.Kind, w.Title, w.Status, w.Result, w.VerifiedAt }).ToArrayAsync(ct) : null,
            incidents = incident ? await db.WorkItems.AsNoTracking().Where(w => w.RoomId == id && w.BranchId == room.BranchId && w.Kind == "INCIDENT").OrderByDescending(w => w.WorkId).Take(20)
                .Select(w => new { w.WorkId, w.Kind, w.Title, w.Status, w.DueAt }).ToArrayAsync(ct) : null
        };
    }
}
