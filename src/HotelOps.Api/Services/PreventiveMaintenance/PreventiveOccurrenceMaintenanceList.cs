using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed partial class PreventiveOccurrenceReadService
{
    // Compose the canonical UC062 reader into UC144 without manufacturing maintenance
    // work rows or copying execution state. The caller owns the serializable transaction.
    internal async Task<IReadOnlyList<MaintenanceWorkListDto>> MaintenanceListAsync(
        SessionReference ticket, long sid, long branch, MaintenanceQueryRequest input,
        DateTime evaluatedAt, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Owning list transaction required.");
        var source = input.Source?.Trim().ToUpperInvariant();
        var status = input.Status?.Trim().ToUpperInvariant();
        if (source == "INCIDENT" || input.IncidentId != null || input.PriorityId != null
            || status is "PENDING" or "CLOSED") return [];
        if (string.IsNullOrEmpty(status)) status = null;
        var result = new List<MaintenanceWorkListDto>();
        long after = 0;
        while (true)
        {
            // Do not apply overdue in separate SQL clock windows: the merged list uses
            // one evaluation time for both sources, filters and pagination.
            var page = await ReadAsync(ticket, sid, branch, null, after, ct, false, status,
                input.DueFrom, input.DueTo, null, input.AssigneeId, input.Keyword);
            if (page.Count == 0) break;
            var ids = page.Select(r => r.RunId).ToArray();
            var standalone = await db.PreventiveMaintenanceRuns.AsNoTracking()
                .Where(r => ids.Contains(r.RunId) && r.MaintenanceWorkId == null
                    && (input.DepartmentId == null || r.Schedule.AssignedDepartmentId == input.DepartmentId))
                .Select(r => new { r.RunId, r.Schedule.AssignedDepartmentId }).ToDictionaryAsync(r => r.RunId, ct);
            foreach (var row in page)
            {
                if (!standalone.TryGetValue(row.RunId, out var run)) continue;
                var overdue = evaluatedAt > row.DueAt && row.Status is "PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD";
                if (input.Overdue != null && input.Overdue != overdue) continue;
                result.Add(new MaintenanceWorkListDto
                {
                    OccurrenceId = row.RunId, BranchId = branch, Source = "PREVENTIVE",
                    WorkNo = $"PM-{row.ScheduleId}/{row.RunId}", Title = row.Title, Status = row.Status,
                    DepartmentId = run.AssignedDepartmentId, AssigneeId = row.AssigneeId, AssigneeName = row.AssigneeName,
                    RoomNumber = row.RoomNumber, AreaName = row.AreaName, AssetCode = row.AssetCode, AssetName = row.AssetName,
                    Deadline = new(row.DueAt, overdue, "PREVENTIVE_OCCURRENCE", null, evaluatedAt)
                });
            }
            after = page[^1].RunId;
            if (page.Count < 50) break;
        }
        await AccessAsync(ticket, sid, branch, ct);
        return result;
    }
}
