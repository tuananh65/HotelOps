using System.Data;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    // Internal Verify participant only. Caller owns authorization, source locks and commit.
    // A failure rolls back the Incident, Work, blocks, room projection and all audits together.
    private async Task ReleaseVerifiedIncidentBlocksAsync(MaintenanceWork work, Incident incident,
        string note, DateTime now, CancellationToken ct)
    {
        if (_dbContext.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new Dev2AccessDenied(503);
        if (work.Status != "CLOSED" || incident.Status != "CLOSED" || work.IncidentId != incident.IncidentId)
            throw new ValidationException("Verified Incident closure required before releasing blocks.");

        var roomIds = await _dbContext.RoomBlocks.AsNoTracking().Where(b => b.IncidentId == incident.IncidentId
            && b.BlockedAt <= now && (b.ReleasedAt == null || b.ReleasedAt > now))
            .Select(b => b.RoomId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        var readiness = new EffectiveRoomReadiness(_dbContext);
        foreach (var roomId in roomIds)
        {
            var room = await _dbContext.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={roomId}")
                .AsTracking().SingleOrDefaultAsync(ct);
            if (room == null || room.BranchId != incident.BranchId)
                throw new ValidationException("Incident block room scope is inconsistent.");
            await _dbContext.Entry(room).ReloadAsync(ct);
            if (room.BranchId != incident.BranchId) throw new ValidationException("Incident block room scope changed.");
            // Retain the whole room range, including blocks of other incidents and manual blocks.
            var blocks = await _dbContext.RoomBlocks.FromSqlInterpolated($"SELECT * FROM dbo.room_blocks WITH(UPDLOCK,HOLDLOCK) WHERE room_id={roomId}")
                .AsTracking().ToArrayAsync(ct);
            foreach (var block in blocks) await _dbContext.Entry(block).ReloadAsync(ct);
            var before = (await readiness.ReadAsync([roomId], now, ct))[roomId];
            var released = blocks.Where(b => b.IncidentId == incident.IncidentId && b.BlockedAt <= now
                && (b.ReleasedAt == null || b.ReleasedAt > now)).ToArray();
            if (released.Length == 0) continue;
            foreach (var block in released)
            {
                var old = BlockClosureSnapshot(block);
                block.ReleasedAt = now;
                block.ReleasedBy = TrustedActorId;
                block.ReleaseNote = note;
                AddAudit("room_blocks", block.BlockId, incident.BranchId, TrustedActorId, now, old, BlockClosureSnapshot(block));
            }
            // Readiness queries persisted lifecycle/block facts. Flush inside this transaction,
            // never calculate from stale tracking values or ExpectedReleaseAt.
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, ct);
            var after = (await readiness.ReadAsync([roomId], now, ct))[roomId];
            var oldPhysical = room.OperationalStatus;
            if (after.CanUse && oldPhysical != "READY")
            {
                room.OperationalStatus = "READY";
                _dbContext.RoomStatusHistories.Add(new RoomStatusHistory
                {
                    RoomId = roomId, FromStatus = oldPhysical, ToStatus = "READY",
                    TriggerType = "MAINTENANCE", TriggerId = work.MaintenanceWorkId,
                    ChangedBy = TrustedActorId, ChangedAt = now, Reason = note
                });
            }
            var oldRoom = new Dictionary<string, object?>
            {
                ["operational_status"] = oldPhysical, ["effective_readiness"] = before.Readiness,
                ["released_block_ids"] = Array.Empty<long>(), ["maintenance_work_id"] = work.MaintenanceWorkId
            };
            AddAudit("rooms", roomId, incident.BranchId, TrustedActorId, now, oldRoom, new()
            {
                ["operational_status"] = room.OperationalStatus, ["effective_readiness"] = after.Readiness,
                ["released_block_ids"] = released.Select(b => b.BlockId).ToArray(), ["maintenance_work_id"] = work.MaintenanceWorkId,
                ["notification_kind"] = "INCIDENT_ROOM_RELEASE", ["notification_version"] = 1,
                ["incident_id"] = incident.IncidentId
            });
        }
    }

    private static Dictionary<string, object?> BlockClosureSnapshot(RoomBlock block) => new()
    {
        ["room_id"] = block.RoomId, ["incident_id"] = block.IncidentId,
        ["released_at"] = block.ReleasedAt, ["released_by"] = block.ReleasedBy, ["release_note"] = block.ReleaseNote
    };
}
