using HotelOps.Api.DTOs.HotelStructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class HistoryCycleCount
    {
        public long CleaningTaskId { get; set; }
        public int Count { get; set; }
    }

    private async Task<HistoryInspectionDto[]> WithHistoryCounts(HistoryInspectionDto[] rows, CancellationToken ct)
    {
        if (rows.Length == 0) return rows;
        var ids = rows.Select(i => i.Id).ToArray();
        var cycles = rows.Where(i => i.CleaningTaskId != null).Select(i => i.CleaningTaskId!.Value).Distinct().ToArray();
        var defects = await db.InspectionDefects.AsNoTracking().Where(d => ids.Contains(d.InspectionId)
            && (d.ChecklistItemId == null || d.ChecklistItem!.VersionId == d.Inspection.VersionId))
            .GroupBy(d => d.InspectionId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        var failed = await db.RoomInspections.AsNoTracking().Where(i => i.CleaningTaskId != null && cycles.Contains(i.CleaningTaskId.Value)
            && i.RoomId == i.CleaningTask!.RoomId && i.Status == "COMPLETED" && i.Result == "FAIL")
            .GroupBy(i => i.CleaningTaskId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        var installed = await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_bindings_v1','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_reinspection_binding_immutable_v1')
                AND parent_id=OBJECT_ID('dbo.fe05_reinspection_bindings_v1') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Reinspection.V1'
                AND CONVERT(nvarchar(128),value)='FE05_REINSPECTION_V1') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
        // A reinspection is an explicitly bound record, not "all inspections minus one".
        // Count bound rounds in this cleaning cycle, including assigned rounds, regardless
        // of the room's current-cycle pointer. Missing capability is unknown, never zero.
        var reinspections = installed ? await db.Database.SqlQueryRaw<HistoryCycleCount>("""
            SELECT b.cleaning_task_id AS CleaningTaskId,COUNT(*) AS Count
            FROM dbo.fe05_reinspection_bindings_v1 b
            JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.cleaning_task_id=b.cleaning_task_id
                AND i.room_id=b.room_id AND i.version_id=b.version_id
            JOIN dbo.room_inspections original ON original.inspection_id=b.original_inspection_id
                AND original.cleaning_task_id=b.cleaning_task_id AND original.room_id=b.room_id
            JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.inspection_id=b.original_inspection_id
                AND w.cleaning_task_id=b.cleaning_task_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=b.cleaning_task_id AND t.room_id=b.room_id
            JOIN dbo.rooms r ON r.room_id=b.room_id AND r.branch_id=b.branch_id
            GROUP BY b.cleaning_task_id
            """).Where(g => cycles.Contains(g.CleaningTaskId)).ToDictionaryAsync(g => g.CleaningTaskId, g => g.Count, ct)
            : new Dictionary<long, int>();
        return rows.Select(row => row with { DefectCount = defects.GetValueOrDefault(row.Id),
            ReinspectionCount = installed && row.CleaningTaskId is { } cycle ? reinspections.GetValueOrDefault(cycle) : null,
            CycleFailedInspectionCount = row.CleaningTaskId is { } task ? failed.GetValueOrDefault(task) : null }).ToArray();
    }
}
