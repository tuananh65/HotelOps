using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Rooms;

public sealed record EffectiveRoomState(long RoomId, string Readiness)
{
    public bool CanUse => Readiness == "READY";
}

// Caller authorizes room IDs and owns SERIALIZABLE consistency. Stored operational_status
// is deliberately not an input: neither READY nor OUT_OF_SERVICE proves effective readiness.
public sealed class EffectiveRoomReadiness(HotelOpsDbContext db)
{
    internal sealed class Facts
    {
        public long RoomId { get; set; }
        public bool IsActive { get; set; }
        public bool Blocked { get; set; }
        public string? Cleaning { get; set; }
        public string? Inspection { get; set; }
        public bool EverPassed { get; set; }
        public bool ReworkOutstanding { get; set; }
        public bool ReworkSubmitted { get; set; }
        public bool SourceAvailable { get; set; }
    }

    internal static string Calculate(Facts source)
    {
        if (!source.IsActive) return "INACTIVE";
        if (source.Blocked) return "BLOCKED";
        if (!source.SourceAvailable) return "UNKNOWN";
        // Reject precedes Request Rework (UC045 -> UC046). The current cycle already
        // needs rework even before a rework row exists; never fall back to never-passed.
        if (source.ReworkOutstanding || source.Cleaning == "REWORK_REQUIRED"
            && source.Inspection == "FAILED" && !source.ReworkSubmitted) return "REWORK_REQUIRED";
        if (source.Cleaning == "IN_PROGRESS") return "CLEANING_IN_PROGRESS";
        // Approved UC034 clarification: never-passed still precedes submitted cleaning/rework.
        // Pending Inspection is the task state; it does not override this room priority.
        if (source.Cleaning is "PENDING_ASSIGNMENT" or "ASSIGNED" or "ON_HOLD" || !source.EverPassed)
            return "PENDING_CLEANING";
        if (source.Cleaning == "PENDING_INSPECTION" || source.ReworkSubmitted) return "PENDING_INSPECTION";
        if (source.Cleaning == "COMPLETED" && source.Inspection == "PASSED") return "READY";
        return "UNKNOWN";
    }

    public async Task<IReadOnlyDictionary<long, EffectiveRoomState>> ReadAsync(long[] authorizedIds, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Effective readiness requires caller SERIALIZABLE transaction.");
        if (authorizedIds.Length == 0) return new Dictionary<long, EffectiveRoomState>();
        var ids = JsonSerializer.Serialize(authorizedIds.Distinct().Order());
        var installed = await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN COL_LENGTH('dbo.cleaning_tasks','workflow_status')=30
            AND COL_LENGTH('dbo.room_inspections','workflow_status')=30
            AND COL_LENGTH('dbo.cleaning_reworks','workflow_status')=30 THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if (installed != 1 || !await HousekeepingSchema.InstalledAsync(db, ct))
        {
            var fallback = await db.Rooms.AsNoTracking().Where(r => authorizedIds.Contains(r.RoomId))
                .Select(r => new { r.RoomId, r.IsActive, Blocked = db.RoomBlocks.Any(b => b.RoomId == r.RoomId
                    && b.BlockedAt <= now && (b.ReleasedAt == null || b.ReleasedAt > now)) }).ToArrayAsync(ct);
            return fallback.ToDictionary(r => r.RoomId, r => new EffectiveRoomState(r.RoomId,
                !r.IsActive ? "INACTIVE" : r.Blocked ? "BLOCKED" : "UNKNOWN"));
        }
        // Current cycle is the latest non-cancelled cleaning task (GBR-044). A cancelled
        // newer task cannot hide the last real cycle. Future block/expected-release times
        // never imply a current effective block or an actual release respectively.
        var facts = await db.Database.SqlQuery<Facts>($"""
            SELECT r.room_id AS RoomId,r.is_active AS IsActive,
            CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.room_blocks b WITH(HOLDLOCK) WHERE b.room_id=r.room_id
                AND b.blocked_at<={now} AND (b.released_at IS NULL OR b.released_at>{now})) THEN 1 ELSE 0 END) AS Blocked,
            c.workflow_status AS Cleaning,i.workflow_status AS Inspection,
            CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.room_inspections p WITH(HOLDLOCK)
                JOIN dbo.cleaning_tasks pc WITH(HOLDLOCK) ON pc.cleaning_task_id=p.cleaning_task_id AND pc.room_id=p.room_id
                WHERE p.room_id=r.room_id AND p.workflow_status='PASSED' AND p.status='COMPLETED' AND p.result='PASS'
                AND pc.status<>'CANCELLED' AND pc.workflow_status<>'CANCELLED') THEN 1 ELSE 0 END) AS EverPassed,
            CONVERT(bit,CASE WHEN c.workflow_status='REWORK_REQUIRED' AND EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=c.cleaning_task_id AND w.workflow_status IN('PENDING_ACCEPTANCE','IN_PROGRESS')) THEN 1 ELSE 0 END) AS ReworkOutstanding,
            CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=c.cleaning_task_id AND w.workflow_status='PENDING_REINSPECTION') THEN 1 ELSE 0 END) AS ReworkSubmitted,
            CONVERT(bit,CASE WHEN c.cleaning_task_id IS NULL OR c.workflow_status IS NOT NULL THEN 1 ELSE 0 END) AS SourceAvailable
            FROM dbo.rooms r WITH(HOLDLOCK)
            OUTER APPLY(SELECT TOP(1) t.cleaning_task_id,t.workflow_status FROM dbo.cleaning_tasks t WITH(HOLDLOCK)
                WHERE t.room_id=r.room_id AND t.status<>'CANCELLED' AND (t.workflow_status IS NULL OR t.workflow_status<>'CANCELLED')
                ORDER BY t.created_at DESC,t.cleaning_task_id DESC) c
            OUTER APPLY(SELECT TOP(1) p.workflow_status FROM dbo.room_inspections p WITH(HOLDLOCK)
                WHERE p.room_id=r.room_id AND p.cleaning_task_id=c.cleaning_task_id AND p.status<>'CANCELLED'
                ORDER BY p.created_at DESC,p.inspection_id DESC) i
            WHERE r.room_id IN(SELECT CONVERT(bigint,value) FROM OPENJSON({ids}))
            """).ToArrayAsync(ct);
        return facts.ToDictionary(f => f.RoomId, f => new EffectiveRoomState(f.RoomId, Calculate(f)));
    }
}
