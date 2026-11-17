using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>Reads the existing FE04/05 workflow under retained locks; never changes assignments.</summary>
internal sealed class RevokeHousekeepingResponsibilities(HotelOpsDbContext db)
{
    private sealed class Work
    {
        public string Kind { get; set; } = "";
        public long Id { get; set; }
        public long BranchId { get; set; }
        public string? Workflow { get; set; }
        public string Physical { get; set; } = "";
        public bool Consistent { get; set; }
    }

    internal async Task<Dev1ResponsibilityReview> ReadAsync(long target, IReadOnlyList<RevokeGrant> before,
        IReadOnlyList<RevokeGrant> after, DateTime now, CancellationToken ct,
        IReadOnlyList<RevokeActorAssignment>? beforeActors = null, IReadOnlyList<RevokeActorAssignment>? afterActors = null)
    {
        if (db.Database.CurrentTransaction == null || now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Owner transaction and UTC time required.");
        var cleaning = await db.CleaningAssignments.FromSqlInterpolated($"SELECT * FROM dbo.cleaning_assignments WITH(HOLDLOCK) WHERE assignee_user_id={target} AND assignment_status='ACTIVE'")
            .AsNoTracking().AnyAsync(ct);
        var rework = await db.CleaningReworks.FromSqlInterpolated($"SELECT * FROM dbo.cleaning_reworks WITH(HOLDLOCK) WHERE assignee_user_id={target}")
            .AsNoTracking().AnyAsync(ct);
        var inspecting = await db.RoomInspections.AsNoTracking().AnyAsync(i => i.InspectorUserId == target
            && (i.Status == "PENDING" || i.Status == "IN_PROGRESS"), ct);
        if (!cleaning && !rework && !inspecting) return new([], []);
        if (!await HousekeepingSchema.InstalledAsync(db, ct)) return new([], ["HOUSEKEEPING_WORKFLOW_REQUIRED"]);
        var rows = await db.Database.SqlQuery<Work>($"""
            SELECT TOP(10001) q.Kind,q.Id,q.BranchId,q.Workflow,q.Physical,q.Consistent
            FROM (
              SELECT 'CLEANING_EXECUTOR' AS Kind,t.cleaning_task_id AS Id,r.branch_id AS BranchId,
                t.workflow_status AS Workflow,t.status AS Physical,
                CONVERT(bit,CASE WHEN a.ended_at IS NULL AND a.assigned_at<={now}
                  AND (SELECT COUNT(*) FROM dbo.cleaning_assignments other WITH(HOLDLOCK)
                    WHERE other.cleaning_task_id=t.cleaning_task_id AND other.assignment_status='ACTIVE' AND other.ended_at IS NULL)=1
                  AND r.is_active=1 AND b.is_active=1 THEN 1 ELSE 0 END) AS Consistent
              FROM dbo.cleaning_assignments a WITH(HOLDLOCK)
              JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=a.cleaning_task_id
              JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id
              JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=r.branch_id
              WHERE a.assignee_user_id={target} AND a.assignment_status='ACTIVE'
              UNION ALL
              SELECT 'REWORK_EXECUTOR',w.rework_id,r.branch_id,w.workflow_status,w.status,
                CONVERT(bit,CASE WHEN w.requested_at<={now} AND w.cleaning_task_id=i.cleaning_task_id
                  AND i.room_id=t.room_id AND r.is_active=1 AND b.is_active=1
                  AND (w.status IN('COMPLETED','CANCELLED') OR
                    (SELECT COUNT(*) FROM dbo.cleaning_reworks other WITH(HOLDLOCK)
                      WHERE other.inspection_id=w.inspection_id AND other.status NOT IN('COMPLETED','CANCELLED'))=1)
                  THEN 1 ELSE 0 END)
              FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
              JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id
              JOIN dbo.room_inspections i WITH(HOLDLOCK) ON i.inspection_id=w.inspection_id
              JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id
              JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=r.branch_id
              WHERE w.assignee_user_id={target}
            ) q ORDER BY q.Kind,q.Id
            """).ToArrayAsync(ct);
        if (rows.Length > 10000) return new([], ["HOUSEKEEPING_RESPONSIBILITY_LIMIT"]);
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var work in rows)
        {
            var isRework = work.Kind == "REWORK_EXECUTOR";
            // A finished cycle retains historical assignee attribution; it is not open work.
            if (work.Workflow == "COMPLETED" && work.Physical == "COMPLETED"
                || work.Workflow == "CANCELLED" && work.Physical == "CANCELLED"
                || isRework && work.Workflow == "FAILED" && work.Physical == "COMPLETED") continue;
            var physicalValid = isRework
                ? work.Workflow is "PENDING_ACCEPTANCE" or "ACCEPTED" && work.Physical == "PENDING"
                    || work.Workflow is "IN_PROGRESS" or "ON_HOLD" or "PENDING_REINSPECTION" && work.Physical == "IN_PROGRESS"
                : work.Workflow == "ASSIGNED" && work.Physical == "PENDING"
                    || work.Workflow is "IN_PROGRESS" or "ON_HOLD" or "PENDING_INSPECTION" or "REWORK_REQUIRED" && work.Physical == "IN_PROGRESS";
            var fact = $"{work.Kind}:{work.Id}";
            if (!work.Consistent || !physicalValid) { unresolved.Add(fact + "_INCONSISTENT"); continue; }
            var impact = RevokeRoleGrantImpact.HousekeepingExecutor(work.Workflow, isRework, before, after, work.BranchId, now,
                beforeActors == null ? null : RevokeActorAssignment.Has(beforeActors, work.BranchId, now, false, "HK_STAFF"),
                afterActors == null ? null : RevokeActorAssignment.Has(afterActors, work.BranchId, now, false, "HK_STAFF"));
            if (impact == RevokeCapabilityImpact.Lost) blockers.Add(fact);
            else if (impact == RevokeCapabilityImpact.Unknown) unresolved.Add(fact + "_UNKNOWN");
        }
        if (inspecting)
        {
            var inspections = await db.Database.SqlQuery<Work>($"""
                SELECT 'INSPECTION' AS Kind,i.inspection_id AS Id,r.branch_id AS BranchId,
                  i.workflow_status AS Workflow,i.status AS Physical,
                  CONVERT(bit,CASE WHEN r.is_active=1 AND b.is_active=1 AND i.result IS NULL THEN 1 ELSE 0 END) AS Consistent
                FROM dbo.room_inspections i WITH(HOLDLOCK) JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id
                JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=r.branch_id
                WHERE i.inspector_user_id={target} AND i.status IN('PENDING','IN_PROGRESS')
                """).ToArrayAsync(ct);
            foreach (var inspection in inspections)
            {
                var fact = $"INSPECTOR:{inspection.Id}";
                if (!inspection.Consistent || !(inspection.Workflow == "ASSIGNED" && inspection.Physical == "PENDING"
                    || inspection.Workflow is "INSPECTING" or "PENDING_CONCLUSION" && inspection.Physical == "IN_PROGRESS"))
                { unresolved.Add(fact + "_INCONSISTENT"); continue; }
                foreach (var permission in new[] { "INSPECTION_VIEW", "INSPECTION_PERFORM" })
                {
                    var impact = RevokeRoleGrantImpact.Evaluate(before, after, permission, inspection.BranchId, true, now);
                    if (impact == RevokeCapabilityImpact.Lost) blockers.Add(fact);
                    if (impact == RevokeCapabilityImpact.Unknown) unresolved.Add(fact + "_UNKNOWN");
                }
                if (beforeActors == null || afterActors == null
                    || !RevokeActorAssignment.Has(beforeActors, inspection.BranchId, now, false, "HK_SUPERVISOR"))
                    unresolved.Add(fact + "_ACTOR_UNKNOWN");
                else if (!RevokeActorAssignment.Has(afterActors, inspection.BranchId, now, false, "HK_SUPERVISOR"))
                    blockers.Add(fact + "_ACTOR");
            }
        }
        return new(blockers.ToArray(), unresolved.ToArray());
    }
}
