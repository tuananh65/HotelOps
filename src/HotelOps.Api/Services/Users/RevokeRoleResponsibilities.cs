using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>UC010-specific facts. Deactivation blockers are not revoke policy.</summary>
internal sealed class RevokeRoleResponsibilities(HotelOpsDbContext db, TimeProvider clock)
{
    private Guid? transaction;
    private long? user;
    private long? selected;
    private IReadOnlyList<RevokeGrant>? initial;
    internal string[] Before { get; private set; } = [];
    internal string[] After { get; private set; } = [];

    internal async Task<Dev1ResponsibilityReview> ReadAsync(long target, long assignment, bool afterMutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (db.Database.CurrentTransaction is not { } tx || transaction is { } id && id != tx.TransactionId
            || user is { } u && u != target || selected is { } selectedId && selectedId != assignment || afterMutation && initial is null)
            throw new InvalidOperationException("Same owner transaction, target and selected assignment required.");
        transaction = tx.TransactionId; user = target; selected = assignment;
        var now = clock.GetUtcNow().UtcDateTime;
        var paths = await (from a in db.UserRoles.AsNoTracking()
            join g in db.RolePermissions.AsNoTracking() on a.RoleId equals g.RoleId
            where a.UserId == target
            select new RevokeGrant(a.UserRoleId, new PermissionGrantPath(g.Permission.PermissionCode, g.DataScope,
                a.BranchId, a.IsActive, a.Role.IsActive, a.AssignedAt, a.ExpiresAt,
                a.Branch != null && a.Branch.IsActive,
                db.UserBranchAccesses.Any(m => m.UserId == target && m.BranchId == a.BranchId && m.RevokedAt == null && m.GrantedAt <= now))))
            .ToArrayAsync(ct);
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        // These contracts cannot be certified by an empty SQL result or surviving grant.
        var unresolved = new SortedSet<string>(StringComparer.Ordinal) {
            "DEV1_ROLE_DEPENDENT_RESPONSIBILITIES_PENDING",
            "DEV2_REVIEWER_TRIAGE_FULFILLMENT_LAST_CAPABLE_PENDING",
            "DEV2_WAREHOUSE_ASSET_HANDOVER_ROLE_POLICY_PENDING",
            "USER_FIRST_WRITER_FINAL_REREAD_ACCEPTANCE_PENDING"
        };
        initial ??= paths;
        var before = afterMutation ? initial : paths;
        var after = afterMutation ? paths : RevokeRoleGrantImpact.Without(paths, assignment);
        if (afterMutation && (!await db.UserRoles.AnyAsync(a => a.UserId == target && a.UserRoleId == assignment && !a.IsActive, ct)
            || !RevokeRoleGrantImpact.Effective(RevokeRoleGrantImpact.Without(initial, assignment), now)
                .SequenceEqual(RevokeRoleGrantImpact.Effective(after, now))))
            unresolved.Add("POST_REVOKE_GRANTS_CHANGED");
        Before = RevokeRoleGrantImpact.Effective(before, now);
        After = RevokeRoleGrantImpact.Effective(after, now);
        void Record(RevokeCapabilityImpact result, string fact)
        {
            if (result == RevokeCapabilityImpact.Lost) blockers.Add(fact);
            if (result == RevokeCapabilityImpact.Unknown) unresolved.Add(fact + "_UNKNOWN");
        }
        // FE11 uses task_progress_versions, not the physical TODO/IN_PROGRESS shorthand.
        // Keep all branches (including inactive ones); a scope loss cannot erase assigned work.
        var taskAssignments = await db.OperationalTaskAssignments.AsNoTracking()
            .Where(a => a.AssigneeId == target && a.AssignmentStatus == "ACTIVE")
            .Select(a => new { a.AssignmentId, a.TaskId, a.AssignedAt, a.EndedAt,
                Branch = a.Task.BranchId,
                ValidScope = a.Task.Department.BranchId == a.Task.BranchId
                    && (a.Task.RoomId == null || a.Task.Room!.BranchId == a.Task.BranchId)
                    && (a.Task.AreaId == null || a.Task.Area!.BranchId == a.Task.BranchId),
                Owners = a.Task.OperationalTaskAssignments.Count(x => x.AssignmentStatus == "ACTIVE" && x.EndedAt == null) })
            .ToArrayAsync(ct);
        if (taskAssignments.Length > 0)
        {
            if (!await ProgressSchema.InstalledAsync(db, ct)) unresolved.Add("OPERATIONAL_TASK_WORKFLOW_REQUIRED");
            else
            {
                var ids = taskAssignments.Select(a => a.TaskId).Distinct().ToArray();
                var states = await TaskWorkflowRead.Query(db, now).Where(t => ids.Contains(t.TaskId)).ToArrayAsync(ct);
                foreach (var task in taskAssignments)
                {
                    if (task.EndedAt != null || task.AssignedAt > now || task.Owners != 1 || !task.ValidScope)
                    { unresolved.Add($"TASK_EXECUTOR:{task.TaskId}_INCONSISTENT"); continue; }
                    var matching = states.Where(s => s.TaskId == task.TaskId).ToArray();
                    Record(RevokeRoleGrantImpact.OperationalTask(matching.Length == 1 ? matching[0].State : null,
                        true, before, after, task.Branch, now), $"TASK_EXECUTOR:{task.TaskId}");
                }
            }
        }
        var internalAssignments=await db.InternalRequestAssignments.AsNoTracking()
            .Where(a=>a.AssigneeId==target&&a.Status=="ACTIVE")
            .Select(a=>new {a.InternalRequestId,a.AssignedAt,a.EndedAt,a.InternalRequest.Status,a.InternalRequest.BranchId,
                DepartmentValid=a.InternalRequest.ReceivingDepartmentId!=null&&a.InternalRequest.ReceivingDepartment!.BranchId==a.InternalRequest.BranchId,
                Owners=a.InternalRequest.InternalRequestAssignments.Count(x=>x.Status=="ACTIVE"&&x.EndedAt==null)})
            .ToArrayAsync(ct);
        foreach(var request in internalAssignments)
        {
            var fact=$"INTERNAL_REQUEST_HANDLER:{request.InternalRequestId}";
            if(request.EndedAt!=null||request.AssignedAt>now||request.Owners!=1||!request.DepartmentValid)
            {unresolved.Add(fact+"_INCONSISTENT");continue;}
            Record(RevokeRoleGrantImpact.InternalRequestHandler(request.Status,before,after,request.BranchId,now),fact);
        }
        // Other designated FE11 roles and all writer coordination remain explicit gates;
        // an empty target assignment set is not complete role-dependency acceptance.
        // Current assignee only; never CreatedBy, ReviewedBy or Incident.ReportedBy.
        var works = await Dev2DependencyQueries.AssignedWorks(db.MaintenanceWorks.AsNoTracking(), target)
            .Select(w => new { w.MaintenanceWorkId, w.Status,
                IncidentBranch = w.Incident == null ? (long?)null : w.Incident.BranchId,
                DepartmentBranch = w.Department == null ? (long?)null : w.Department.BranchId }).ToArrayAsync(ct);
        foreach (var w in works)
        {
            var branch = w.IncidentBranch ?? w.DepartmentBranch;
            if (w.IncidentBranch != null && w.DepartmentBranch != null && w.IncidentBranch != w.DepartmentBranch) branch = null;
            Record(RevokeRoleGrantImpact.Maintenance(w.Status, true, before, after, branch, now), $"MAINTENANCE:{w.MaintenanceWorkId}");
        }
        var requests = await Dev2DependencyQueries.UserRequests(db.MaterialRequests.AsNoTracking(), target)
            .Where(r => r.Status == "DRAFT" || r.Status == "NEEDS_INFORMATION")
            .Select(r => new { r.RequestId, r.BranchId }).ToArrayAsync(ct);
        foreach (var r in requests)
            Record(RevokeRoleGrantImpact.Evaluate(before, after, "MR_CREATE", r.BranchId, true, now), $"MR_REQUESTER:{r.RequestId}");
        // Reuse latest-cycle predicate; historical approvers and fulfilled_by are not assignments.
        var branches = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.AssignedApproverId == target)
            .Select(c => c.Request.BranchId).Distinct().ToArrayAsync(ct);
        foreach (var branch in branches)
        {
            var approvals = await Dev2DependencyQueries.PendingApprovals(db.MaterialRequestApprovalCycles.AsNoTracking(), target, branch)
                .Select(c => c.RequestId).ToArrayAsync(ct);
            foreach (var request in approvals)
                Record(RevokeRoleGrantImpact.Evaluate(before, after, "MR_APPROVE", branch, false, now), $"MR_APPROVER:{request}");
        }
        return new(blockers.ToArray(), unresolved.ToArray());
    }
}
