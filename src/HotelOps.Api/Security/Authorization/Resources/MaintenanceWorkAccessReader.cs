using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Security.Authorization;

public sealed record MaintenanceWorkAccess(long MaintenanceWorkId, long? IncidentId, long? BranchId,
    long? AssigneeId, string Status);
public sealed record MaintenanceOwnWorkSample(long BranchId, long MaintenanceWorkId);

// Shared query projection: unresolved/ambiguous sources retain a null branch.
// Member initializers and scalar navigation aggregates remain query expressions
// for EF, and execute against the same entity graph in the no-SQL LINQ harness.
public sealed class MaintenanceWorkSource
{
    public MaintenanceWork Work { get; init; } = null!;
    public long? BranchId { get; init; }
}

public static class MaintenanceWorkSources
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

public interface IMaintenanceWorkAccessReader
{
    Task<MaintenanceWorkAccess?> ReadAsync(long id, CancellationToken ct);
    Task<long?> ReadIncidentBranchAsync(long incidentId, CancellationToken ct);
    Task<long[]> ReadActiveBranchesAsync(CancellationToken ct);
    Task<MaintenanceOwnWorkSample[]> ReadOwnWorkSamplesAsync(long userId, CancellationToken ct);
}

public sealed class MaintenanceWorkAccessReader(HotelOpsDbContext db) : IMaintenanceWorkAccessReader
{
    public Task<MaintenanceWorkAccess?> ReadAsync(long id, CancellationToken ct) =>
        MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == id))
            .Select(s => new MaintenanceWorkAccess(s.Work.MaintenanceWorkId, s.Work.IncidentId,
                s.BranchId, s.Work.AssigneeId, s.Work.Status))
            .SingleOrDefaultAsync(ct);

    public Task<long?> ReadIncidentBranchAsync(long incidentId, CancellationToken ct) =>
        db.Incidents.AsNoTracking().Where(i => i.IncidentId == incidentId)
            .Select(i => (long?)i.BranchId).SingleOrDefaultAsync(ct);

    public Task<long[]> ReadActiveBranchesAsync(CancellationToken ct) =>
        db.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).ToArrayAsync(ct);

    public Task<MaintenanceOwnWorkSample[]> ReadOwnWorkSamplesAsync(long userId, CancellationToken ct) =>
        MaintenanceWorkSources.Project(db.MaintenanceWorks.AsNoTracking().Where(w => w.AssigneeId == userId))
            .Where(s => s.BranchId != null && db.Branches.Any(b => b.BranchId == s.BranchId && b.IsActive))
            .GroupBy(s => s.BranchId!.Value)
            .Select(g => new MaintenanceOwnWorkSample(g.Key, g.Min(s => s.Work.MaintenanceWorkId)))
            .ToArrayAsync(ct);
}
