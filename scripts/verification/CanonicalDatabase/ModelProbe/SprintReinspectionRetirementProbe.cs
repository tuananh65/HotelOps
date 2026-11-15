using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintReinspectionRetirementProbe
{
    private sealed class Source
    {
        public long Inspection { get; set; }
        public long Original { get; set; }
        public long Version { get; set; }
    }
    // Fixture input change only, not a claim of UC040 API acceptance. The parent
    // verified the exact disposable GUID/run and owns deletion of this graph.
    internal static async Task RetireAfterAssignmentAsync(HotelOpsDbContext db, long actor, long branch,
        long rework, long inspection, CancellationToken ct)
    {
        var source = await db.Database.SqlQuery<Source>($"""
            SELECT b.inspection_id AS Inspection,b.original_inspection_id AS Original,b.version_id AS Version
            FROM dbo.fe05_reinspection_bindings_v1 b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id
              AND i.version_id=b.version_id AND i.workflow_status='ASSIGNED' AND i.status='PENDING' AND i.inspector_user_id={actor}
            JOIN dbo.room_inspections original ON original.inspection_id=b.original_inspection_id
              AND original.version_id=b.version_id AND original.inspection_no='D3-EVIDENCE'
            JOIN dbo.branches branch ON branch.branch_id=b.branch_id AND branch.branch_code='D3-EVIDENCE'
            WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.branch_id={branch} AND b.reinspector_user_id={actor}
            """).SingleAsync(ct);
        async Task<string> Snapshot()
        {
            var version = await db.InspectionChecklistVersions.AsNoTracking().Where(v => v.VersionId == source.Version)
                .Select(v => new { v.VersionId, v.ChecklistId, v.VersionNo, v.PublishedBy, v.EffectiveAt }).SingleAsync(ct);
            var criteria = await db.InspectionChecklistItems.AsNoTracking().Where(i => i.VersionId == source.Version)
                .OrderBy(i => i.ItemId).Select(i => new { i.ItemId, i.VersionId, i.Criterion, i.Description, i.IsRequired, i.PassCondition, i.SortOrder, i.IsActive }).ToArrayAsync(ct);
            var revisions = await db.Database.SqlQuery<string>($"""
                SELECT CONVERT(varchar(16),row_version,2) AS Value FROM dbo.room_inspections WHERE inspection_id IN({source.Inspection},{source.Original})
                UNION ALL SELECT CONVERT(varchar(16),row_version,2) FROM dbo.fe05_reinspection_bindings_v1 WHERE inspection_id={source.Inspection}
                """).ToArrayAsync(ct);
            return JsonSerializer.Serialize(new { version, criteria, revisions = revisions.Order().ToArray() });
        }
        var before = await Snapshot();
        async Task Retire()
        {
            if (await db.InspectionChecklistVersions.Where(v => v.VersionId == source.Version && v.Status == "PUBLISHED")
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, "RETIRED"), ct) != 1)
                throw new Exception("Expected one previously Published assigned version");
            if (await Snapshot() != before || await db.RoomInspections.CountAsync(i =>
                (i.InspectionId == source.Inspection || i.InspectionId == source.Original) && i.VersionId == source.Version, ct) != 2)
                throw new Exception("Retirement changed historical content or binding");
        }
        await using (var rehearsal = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        { await Retire(); await rehearsal.RollbackAsync(ct); }
        if (!await db.InspectionChecklistVersions.AnyAsync(v => v.VersionId == source.Version && v.Status == "PUBLISHED", ct))
            throw new Exception("Retirement rollback failed");
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        { await Retire(); await transaction.CommitAsync(ct); }
        // Never unretire a used version or disable its immutability guard. Parent
        // deletes the owned inspections before deleting their version at cleanup.
    }
}
