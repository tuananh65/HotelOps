using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> CanLinkCleaningBlocker(long actor, long branch, long task, long room,
        long? incident, long? material, CancellationToken ct)
    {
        // Caller owns the Serializable closure transaction and current assignee checks.
        // CLEANING_EXECUTE does not grant access to either linked source.
        if (incident is {} incidentId)
        {
            var access = await IncidentReadAccess.ReadAsync(db, actor, branch, ct, sourceRecord: true);
            var source = await access.Apply(db.Incidents.AsNoTracking()).Where(i => i.IncidentId == incidentId
                && i.Status != "CANCELLED").Select(i => new { i.RoomId }).SingleOrDefaultAsync(ct);
            if (source == null) return false;
            if (source.RoomId != room)
            {
                // UC056 approved scope: direct RoomId or an explicit affected-room
                // mapping. An area or the asset's current location is not evidence.
                if (!await IncidentRoomBlockService.InstalledAsync(db, ct)) return false;
                var at = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
                if (!await db.Database.SqlQuery<int>($"""
                    SELECT 1 AS Value FROM dbo.incident_affected_rooms m WITH(HOLDLOCK)
                    JOIN dbo.rooms r ON r.room_id=m.room_id AND r.branch_id={branch}
                    WHERE m.incident_id={incidentId} AND m.room_id={room} AND m.added_at<={at}
                    """).AnyAsync(ct)) return false;
            }
        }
        if (material is {} materialId)
        {
            if (!await MaterialCleaningLinks.InstalledAsync(db, ct)) return false;
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var access = await MaterialRequestReadAccess.ReadAsync(db, actor, branch, now, ct);
            if (!await access.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(m => m.RequestId == materialId
                && m.Status != "CANCELLED" && m.Status != "REJECTED", ct)
                || !await MaterialReadProvenance.ValidAsync(db, materialId, branch, ct)) return false;
            // The explicit UC092 task link must already exist; a room coincidence is not a task link.
            if (await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.material_request_cleaning_links WITH(HOLDLOCK)
                WHERE request_id={materialId} AND task_id={task}
                """).SingleAsync(ct) != 1) return false;
        }
        return true;
    }
}
