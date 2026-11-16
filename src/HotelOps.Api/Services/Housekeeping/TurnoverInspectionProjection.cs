using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Housekeeping;

// Historical inspection outcome only. Never a room-use/readiness decision.
public static class TurnoverInspectionProjection
{
    public static IQueryable<string?> Latest(IQueryable<RoomInspection> inspections) => inspections
        .OrderByDescending(i => i.InspectedAt ?? i.ScheduledAt ?? i.CreatedAt)
        .ThenByDescending(i => i.InspectionId)
        .Select(i => i.Status == "COMPLETED" && (i.Result == "PASS" || i.Result == "FAIL" || i.Result == "CONDITIONAL")
            ? i.Result : null).Take(1);
}
