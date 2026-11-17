using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.PreventiveMaintenance;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed record UserDeactivationWorkItem(string Kind, long Id, long BranchId);
public sealed record UserDeactivationPreview(long UserId, string Version, string AccountStatus,
    IReadOnlyList<UserDeactivationWorkItem> WorkItems, bool HasRestrictedWork, bool HasMore,
    bool LastDurableAdministrator, bool CoverageIncomplete, bool HasOpenResponsibilities);
public sealed record UserDeactivationPreviewResult(AccountDeactivationStatus Status, UserDeactivationPreview? Data = null);

/// <summary>
/// MSG34 account-management dependency metadata, never authority to read a work record
/// or to deactivate. The mutation must independently repeat its full responsibility review.
/// </summary>
public sealed class UserDeactivationPreviewService(HotelOpsDbContext db, UserAccountStatusAccess access,
    ICurrentUserAccessor current, ISqlSessionService sessions, TimeProvider clock)
{
    public async Task<UserDeactivationPreviewResult> ReadAsync(SessionReference reference, long sessionId,
        long target, CancellationToken ct)
    {
        if (target <= 0 || sessionId <= 0 || !current.TryGetCurrentUser(out var actor)
            || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(AccountDeactivationStatus.SessionInvalid);
        if (actor.IsRestricted || reference.IsRestricted || actor.UserId == target)
            return new(AccountDeactivationStatus.Forbidden);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null
            || System.Transactions.Transaction.Current is not null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Account preview requires its own clean SQL context.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        GlobalAdminTransaction guard;
        try { guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct); }
        catch (GlobalAdminLockException) { return new(AccountDeactivationStatus.SerializationUnavailable); }
        var administrators = await guard.ReadSnapshotAsync(ct);
        await access.LockAuthorityAsync(actor.UserId, target, ct);
        async Task<AccountDeactivationStatus?> Denied()
        {
            var session = await sessions.ValidateSessionAsync(reference, ct);
            if (!session.IsValid || session.Session?.SessionId != sessionId || session.Session.IsRestricted)
                return AccountDeactivationStatus.SessionInvalid;
            return await access.CanManageAsync(target, ct) ? null : AccountDeactivationStatus.Forbidden;
        }
        if (await Denied() is { } initialDenial) return new(initialDenial);
        var user = await db.Users.AsNoTracking().Where(u => u.UserId == target && u.DeletedAt == null)
            .Select(u => new { u.AccountStatus, u.RowVersion }).SingleOrDefaultAsync(ct);
        if (user is null) return new(AccountDeactivationStatus.NotFound);
        if (!await ShiftHandoverStore.InstalledAsync(db, ct)
            || !await InternalRequestHandoverStore.InstalledAsync(db, ct)
            || !await PreventiveOccurrenceGeneration.InstalledAsync(db, ct)
            || !await TaskSeriesStore.InstalledAsync(db, ct))
            return new(AccountDeactivationStatus.ResponsibilitiesUnresolved);
        var review = await new AccountDeactivationResponsibilities(db).ReadAsync(target, ct);
        var rows = db.Database.SqlQuery<UserDeactivationWorkItem>($"""
            SELECT 'TASK' AS Kind,t.task_id AS Id,t.branch_id AS BranchId
            FROM dbo.operational_task_assignments a JOIN dbo.operational_tasks t ON t.task_id=a.task_id
            WHERE a.assignee_id={target} AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL
            UNION
            SELECT 'TASK_SERIES',s.series_id,s.branch_id FROM dbo.operational_task_series s
            WHERE s.is_active=1 AND s.next_due_at IS NOT NULL AND (s.created_by={target} OR s.default_assignee_id={target})
            UNION
            SELECT 'CLEANING',t.cleaning_task_id,r.branch_id
            FROM dbo.cleaning_assignments a JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=a.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE a.assignee_user_id={target} AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL
              AND t.status IN('PENDING','IN_PROGRESS')
            UNION
            SELECT 'REWORK',w.rework_id,r.branch_id
            FROM dbo.cleaning_reworks w JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE w.assignee_user_id={target} AND w.status IN('PENDING','IN_PROGRESS')
            UNION
            SELECT 'INSPECTION',i.inspection_id,r.branch_id
            FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id
            WHERE i.inspector_user_id={target} AND i.status IN('PENDING','IN_PROGRESS')
            UNION
            SELECT 'MAINTENANCE',w.maintenance_work_id,
              CASE WHEN i.branch_id IS NOT NULL AND d.branch_id IS NOT NULL AND i.branch_id<>d.branch_id
                THEN CAST(0 AS bigint) ELSE COALESCE(i.branch_id,d.branch_id,CAST(0 AS bigint)) END
            FROM dbo.maintenance_works w
            LEFT JOIN dbo.incidents i ON i.incident_id=w.incident_id
            LEFT JOIN dbo.departments d ON d.department_id=w.department_id
            WHERE w.assignee_id={target} AND w.status IN('PENDING','IN_PROGRESS','ON_HOLD')
            UNION
            SELECT 'PREVENTIVE_OCCURRENCE',o.run_id,o.branch_id FROM dbo.preventive_occurrence_details o
            WHERE o.assignee_id={target} AND o.workflow_status IN('ASSIGNED','IN_PROGRESS','ON_HOLD')
            UNION
            SELECT 'INTERNAL_REQUEST',r.internal_request_id,r.branch_id
            FROM dbo.internal_request_assignments a JOIN dbo.internal_requests r ON r.internal_request_id=a.internal_request_id
            WHERE a.assignee_id={target} AND a.status='ACTIVE' AND a.ended_at IS NULL
            UNION
            SELECT 'WORKDAY',w.workday_id,w.branch_id FROM dbo.workdays w WHERE w.user_id={target} AND w.status='WORKING'
            UNION
            SELECT 'SHIFT',s.shift_id,s.branch_id FROM dbo.shift_assignments a JOIN dbo.work_shifts s ON s.shift_id=a.shift_id
            WHERE a.user_id={target} AND a.assignment_status='ASSIGNED' AND s.status='PLANNED' AND s.scheduled_end_at>SYSUTCDATETIME()
            UNION
            SELECT 'INTERNAL_REQUEST_HANDOVER',h.handover_id,h.branch_id FROM dbo.internal_request_handovers h
            JOIN dbo.internal_request_assignments a ON a.assignment_id=h.sender_assignment_id
            WHERE h.state='PENDING_ACKNOWLEDGMENT' AND (h.receiver_id={target} OR a.assignee_id={target})
            UNION
            SELECT 'SHIFT_HANDOVER',h.handover_id,h.branch_id FROM dbo.operational_shift_handovers h
            WHERE h.state='PENDING_ACKNOWLEDGMENT' AND (h.sender_id={target} OR h.receiver_id={target})
            UNION
            SELECT 'INSPECTION_DEFECT',d.defect_id,r.branch_id FROM dbo.inspection_defects d
            JOIN dbo.room_inspections i ON i.inspection_id=d.inspection_id JOIN dbo.rooms r ON r.room_id=i.room_id
            WHERE d.assigned_to={target} AND d.status IN('OPEN','IN_PROGRESS')
            UNION
            SELECT 'OPEN_WORK_INTERVAL',i.interval_id,i.branch_id FROM dbo.work_intervals i
            WHERE i.user_id={target} AND i.closed_at IS NULL
            UNION
            SELECT 'DEPARTMENT_HEAD',d.department_id,d.branch_id FROM dbo.departments d
            WHERE d.manager_user_id={target} AND d.is_active=1
            UNION
            SELECT 'LINE_MANAGER',u.user_id,COALESCE(u.primary_branch_id,CAST(0 AS bigint)) FROM dbo.users u
            WHERE u.manager_user_id={target} AND u.deleted_at IS NULL AND u.employment_status='ACTIVE'
            UNION
            SELECT 'WAREHOUSE_MANAGER',w.warehouse_id,w.branch_id FROM dbo.warehouses w
            WHERE w.manager_user_id={target} AND w.is_active=1
            UNION
            SELECT 'ASSET_CUSTODY',a.asset_id,a.branch_id FROM dbo.assets a
            WHERE a.custodian_user_id={target} AND a.lifecycle_status IN('IN_USE','UNDER_REPAIR')
            UNION
            SELECT 'ASSET_HANDOVER',h.handover_id,a.branch_id FROM dbo.asset_handovers h
            JOIN dbo.assets a ON a.asset_id=h.asset_id WHERE h.to_user_id={target} AND h.handover_status='PENDING'
            UNION
            SELECT 'MATERIAL_REQUEST',r.request_id,r.branch_id FROM dbo.material_requests r
            WHERE r.requester_id={target} AND r.status IN('DRAFT','NEEDS_INFORMATION')
            UNION
            SELECT 'MATERIAL_APPROVAL',c.approval_cycle_id,r.branch_id FROM dbo.material_request_approval_cycles c
            JOIN dbo.material_requests r ON r.request_id=c.request_id
            WHERE c.assigned_approver_id={target} AND c.status='PENDING' AND r.status='PENDING_APPROVAL'
              AND NOT EXISTS(SELECT 1 FROM dbo.material_request_approval_cycles newer
                WHERE newer.request_id=c.request_id AND newer.revision_no>c.revision_no)
            """);
        var branches = await rows.Select(r => r.BranchId).Distinct().ToArrayAsync(ct);
        var visible = new List<long>();
        foreach (var branch in branches)
            if (await access.CanInspectDependenciesInBranchAsync(branch, ct)) visible.Add(branch);
        var items = await rows.Where(r => visible.Contains(r.BranchId)).OrderBy(r => r.Kind).ThenBy(r => r.Id)
            .Take(201).ToArrayAsync(ct);
        if (await Denied() is { } finalDenial) return new(finalDenial);
        // Repeat scope at the read boundary; never include titles, content, or source URLs.
        foreach (var branch in visible)
            if (!await access.CanInspectDependenciesInBranchAsync(branch, ct)) return new(AccountDeactivationStatus.Forbidden);
        var data = new UserDeactivationPreview(target, Convert.ToHexString(user.RowVersion), user.AccountStatus,
            items.Take(200).ToArray(), branches.Any(b => !visible.Contains(b)), items.Length > 200,
            administrators.DurableEligibleUserIds.Count == 1 && administrators.DurableEligibleUserIds.Contains(target),
            review.Unresolved.Count != 0 || review.Unsupported.Count != 0, review.Blockers.Count != 0);
        await tx.CommitAsync(ct);
        return new(AccountDeactivationStatus.Success, data);
    }
}
