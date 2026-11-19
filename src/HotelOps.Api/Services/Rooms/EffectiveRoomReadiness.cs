using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Rooms;

public sealed record EffectiveRoomState(long RoomId, string Readiness)
{
    public long? CleaningTaskId { get; init; }
    public string? CleaningState { get; init; }
    public long? InspectionId { get; init; }
    public string? InspectionState { get; init; }
    public long? ReworkId { get; init; }
    public bool CanUse => Readiness == "READY";
}

// Caller authorizes room IDs and owns SERIALIZABLE consistency. Stored operational_status
// is deliberately not an input: neither READY nor OUT_OF_SERVICE proves effective readiness.
public sealed class EffectiveRoomReadiness(HotelOpsDbContext db)
{
    internal static string InspectionOperationalStatus(string previous, bool canUse)
        => canUse && previous != "OCCUPIED" ? "READY" : previous;

    internal sealed class Facts
    {
        public long RoomId { get; set; }
        public bool IsActive { get; set; }
        public long? CleaningTaskId { get; set; }
        public long? InspectionId { get; set; }
        public long? ReworkId { get; set; }
        public bool Blocked { get; set; }
        public string? Cleaning { get; set; }
        public string? Inspection { get; set; }
        public bool EverPassed { get; set; }
        public bool CompletedCycleVerified { get; set; }
        public bool ReworkOutstanding { get; set; }
        public bool ReworkSubmitted { get; set; }
        public bool SourceAvailable { get; set; }
        public bool AwaitingNewCycle { get; set; }
    }

    internal static string Calculate(Facts source)
    {
        if (!source.IsActive) return "INACTIVE";
        if (source.Blocked) return "BLOCKED";
        if (!source.SourceAvailable) return "UNKNOWN";
        if (source.AwaitingNewCycle) return "PENDING_CLEANING";
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
        if (source.Cleaning == "COMPLETED" && source.Inspection == "PASSED" && source.CompletedCycleVerified) return "READY";
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
            c.cleaning_task_id AS CleaningTaskId,i.inspection_id AS InspectionId,
            CONVERT(bit,CASE WHEN departure.OccurredAt IS NOT NULL AND c.cleaning_task_id IS NULL THEN 1 ELSE 0 END) AS AwaitingNewCycle,
            (SELECT TOP(1) w.rework_id FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=c.cleaning_task_id AND w.workflow_status<>'CANCELLED'
                ORDER BY w.requested_at DESC,w.rework_id DESC) AS ReworkId,
            CONVERT(bit,CASE WHEN c.workflow_status='COMPLETED' AND c.status='COMPLETED'
                AND i.workflow_status='PASSED' AND i.status='COMPLETED' AND i.result='PASS' THEN 1 ELSE 0 END) AS CompletedCycleVerified,
            CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.room_inspections p WITH(HOLDLOCK)
                JOIN dbo.cleaning_tasks pc WITH(HOLDLOCK) ON pc.cleaning_task_id=p.cleaning_task_id AND pc.room_id=p.room_id
                WHERE p.room_id=r.room_id AND p.workflow_status='PASSED' AND p.status='COMPLETED' AND p.result='PASS'
                AND (activation.OccurredAt IS NULL OR pc.created_at>=activation.OccurredAt)
                AND pc.status<>'CANCELLED' AND pc.workflow_status<>'CANCELLED') THEN 1 ELSE 0 END) AS EverPassed,
            CONVERT(bit,CASE WHEN c.workflow_status='REWORK_REQUIRED' AND EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=c.cleaning_task_id AND w.workflow_status IN('PENDING_ACCEPTANCE','ACCEPTED','IN_PROGRESS','ON_HOLD')) THEN 1 ELSE 0 END) AS ReworkOutstanding,
            CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=c.cleaning_task_id AND w.workflow_status='PENDING_REINSPECTION') THEN 1 ELSE 0 END) AS ReworkSubmitted,
            CONVERT(bit,CASE WHEN c.cleaning_task_id IS NULL OR c.workflow_status IS NOT NULL THEN 1 ELSE 0 END) AS SourceAvailable
            FROM dbo.rooms r WITH(HOLDLOCK)
            OUTER APPLY(SELECT MAX(a.occurred_at) AS OccurredAt FROM dbo.audit_logs a WITH(HOLDLOCK)
                WHERE a.entity_type='rooms' AND a.entity_id=CONVERT(varchar(30),r.room_id)
                  AND a.branch_id=r.branch_id AND a.action='UPDATE' AND a.actor_user_id IS NOT NULL
                  AND a.occurred_at<={now}
                  AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE '[]' END,'$.operation')='REACTIVATE_ROOM'
                  AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE '[]' END,'$.record.isActive')='true'
                  AND JSON_VALUE(CASE WHEN ISJSON(a.old_values)=1 THEN a.old_values ELSE '[]' END,'$.isActive')='false') activation
            OUTER APPLY(SELECT MAX(h.changed_at) AS OccurredAt FROM dbo.room_status_history h WITH(HOLDLOCK)
                WHERE h.room_id=r.room_id AND h.trigger_type='CHECKOUT' AND h.from_status='OCCUPIED'
                  AND h.to_status='DIRTY' AND h.changed_at<={now}) departure
            OUTER APPLY(SELECT TOP(1) t.cleaning_task_id,t.workflow_status,t.status FROM dbo.cleaning_tasks t WITH(HOLDLOCK)
                WHERE t.room_id=r.room_id AND t.status<>'CANCELLED' AND (t.workflow_status IS NULL OR t.workflow_status<>'CANCELLED')
                  AND (activation.OccurredAt IS NULL OR t.created_at>=activation.OccurredAt)
                  AND (departure.OccurredAt IS NULL OR t.created_at>departure.OccurredAt)
                ORDER BY t.created_at DESC,t.cleaning_task_id DESC) c
            OUTER APPLY(SELECT TOP(1) p.inspection_id,p.workflow_status,p.status,p.result FROM dbo.room_inspections p WITH(HOLDLOCK)
                WHERE p.room_id=r.room_id AND p.cleaning_task_id=c.cleaning_task_id AND p.status<>'CANCELLED'
                ORDER BY p.created_at DESC,p.inspection_id DESC) i
            WHERE r.room_id IN(SELECT CONVERT(bigint,value) FROM OPENJSON({ids}))
            """).ToArrayAsync(ct);
        return facts.ToDictionary(f => f.RoomId, f => new EffectiveRoomState(f.RoomId, Calculate(f))
        { CleaningTaskId=f.CleaningTaskId, CleaningState=f.Cleaning, InspectionId=f.InspectionId,
          InspectionState=f.Inspection, ReworkId=f.ReworkId });
    }
}
