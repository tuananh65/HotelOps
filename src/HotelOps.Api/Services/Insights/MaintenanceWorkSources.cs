using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Insights;

// Query-only projection recovered from 4ce3c470; does not register resource authorization.
internal sealed class MaintenanceWorkSource
{
    public MaintenanceWork Work { get; init; } = null!;
    public long? BranchId { get; init; }
}

internal static class MaintenanceWorkSources
{
    public static IQueryable<MaintenanceWorkSource> Project(IQueryable<MaintenanceWork> works) =>
        works.Select(w => new MaintenanceWorkSource
        {
            Work = w,
            BranchId = w.MaintenanceWorkId > 0
                && w.IncidentId > 0 && w.Incident != null && w.Incident.BranchId > 0
                && !w.PreventiveMaintenanceRuns.Any()
                ? (long?)w.Incident.BranchId
                : w.MaintenanceWorkId > 0 && w.IncidentId == null && w.PreventiveMaintenanceRuns.Count() == 1
                    // MAX is a nullable scalar projection of the sole run, never
                    // a way to choose between runs. The count above is exclusive.
                    ? w.PreventiveMaintenanceRuns.Select(r =>
                        r.Schedule != null
                        && (r.Schedule.AssetId != null ? 1 : 0)
                            + (r.Schedule.RoomId != null ? 1 : 0)
                            + (r.Schedule.AreaId != null ? 1 : 0) == 1
                            ? r.Schedule.AssetId != null && r.Schedule.Asset != null && r.Schedule.Asset.BranchId > 0
                                ? (long?)r.Schedule.Asset.BranchId
                                : r.Schedule.RoomId != null && r.Schedule.Room != null && r.Schedule.Room.BranchId > 0
                                    ? (long?)r.Schedule.Room.BranchId
                                    : r.Schedule.AreaId != null && r.Schedule.Area != null && r.Schedule.Area.BranchId > 0
                                        ? (long?)r.Schedule.Area.BranchId : null
                            : null).Max()
                    : null
        });
}
