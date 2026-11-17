using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.MaterialRequests;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>UC010-specific facts. Deactivation blockers are not revoke policy.</summary>
internal sealed class RevokeRoleResponsibilities(HotelOpsDbContext db, TimeProvider clock)
{
    private Guid? transaction;
    private long? user;
    private long? selected;
    private IReadOnlyList<RevokeGrant>? initial;
    private IReadOnlyList<RevokeActorAssignment>? initialActors;
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
        var actors = await db.UserRoles.AsNoTracking().Where(a => a.UserId == target)
            .Select(a => new RevokeActorAssignment(a.UserRoleId, a.Role.RoleCode, a.BranchId,
                a.IsActive, a.Role.IsActive, a.AssignedAt, a.ExpiresAt)).ToArrayAsync(ct);
        var paths = await (from a in db.UserRoles.AsNoTracking()
            join g in db.RolePermissions.AsNoTracking() on a.RoleId equals g.RoleId
            where a.UserId == target
            select new RevokeGrant(a.UserRoleId, new PermissionGrantPath(g.Permission.PermissionCode, g.DataScope,
                a.BranchId, a.IsActive, a.Role.IsActive, a.AssignedAt, a.ExpiresAt,
                a.Branch != null && a.Branch.IsActive,
                db.UserBranchAccesses.Any(m => m.UserId == target && m.BranchId == a.BranchId && m.RevokedAt == null && m.GrantedAt <= now)), a.Role.RoleCode))
            .ToArrayAsync(ct);
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        initial ??= paths;
        initialActors ??= actors;
        var before = afterMutation ? initial : paths;
        var after = afterMutation ? paths : RevokeRoleGrantImpact.Without(paths, assignment);
        var beforeActors = afterMutation ? initialActors : actors;
        var afterActors = afterMutation ? actors : actors.Where(a => a.AssignmentId != assignment).ToArray();
        if (afterMutation && !initialActors.Where(a => a.AssignmentId != assignment && a.Effective(now)).ToHashSet()
                .SetEquals(afterActors.Where(a => a.Effective(now))))
            unresolved.Add("POST_REVOKE_ACTORS_CHANGED");
        if (afterMutation && (!await db.UserRoles.AnyAsync(a => a.UserId == target && a.UserRoleId == assignment && !a.IsActive, ct)
            || !RevokeRoleGrantImpact.SameEffectivePaths(RevokeRoleGrantImpact.Without(initial, assignment), after, now)))
            unresolved.Add("POST_REVOKE_GRANTS_CHANGED");
        Before = RevokeRoleGrantImpact.Effective(before, now);
        After = RevokeRoleGrantImpact.Effective(after, now);
        void Record(RevokeCapabilityImpact result, string fact)
        {
            if (result == RevokeCapabilityImpact.Lost) blockers.Add(fact);
            if (result == RevokeCapabilityImpact.Unknown) unresolved.Add(fact + "_UNKNOWN");
        }
        var housekeeping = await new RevokeHousekeepingResponsibilities(db).ReadAsync(target, before, after, now, ct, beforeActors, afterActors);
        blockers.UnionWith(housekeeping.Blockers);
        unresolved.UnionWith(housekeeping.Unresolved);
        var handovers = await RevokeHandoverResponsibilities.ReadAsync(db, target, before, after, beforeActors, afterActors, now, ct);
        blockers.UnionWith(handovers.Blockers);
        unresolved.UnionWith(handovers.Unresolved);
        var worktime = await RevokeWorkTimeResponsibilities.ReadAsync(db, target, before, after, beforeActors, afterActors, now, ct);
        blockers.UnionWith(worktime.Blockers);
        unresolved.UnionWith(worktime.Unresolved);
        var designated = await RevokeDesignatedResponsibilities.ReadAsync(db, target, before, after, beforeActors, afterActors, now, ct);
        blockers.UnionWith(designated.Blockers);
        unresolved.UnionWith(designated.Unresolved);
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
                    var wasExecutor = RevokeActorAssignment.Has(beforeActors, task.Branch, now, true, "HK_STAFF", "MAINTENANCE_STAFF", "STOREKEEPER");
                    var remainsExecutor = RevokeActorAssignment.Has(afterActors, task.Branch, now, true, "HK_STAFF", "MAINTENANCE_STAFF", "STOREKEEPER");
                    Record(!wasExecutor ? RevokeCapabilityImpact.Unknown : remainsExecutor
                        ? RevokeCapabilityImpact.Preserved : RevokeCapabilityImpact.Lost, $"TASK_EXECUTOR_ROLE:{task.TaskId}");
                }
            }
        }
        // Retain both the assignment
        // range and request row so Route/reassignment cannot invalidate the
        // current responsibility between this read and the final reread/commit.
        var internalAssignments=await (from a in db.InternalRequestAssignments
            .FromSqlInterpolated($"SELECT * FROM dbo.internal_request_assignments WITH(HOLDLOCK) WHERE assignee_id={target} AND status='ACTIVE'").AsNoTracking()
            join request in db.InternalRequests.FromSqlRaw("SELECT * FROM dbo.internal_requests WITH(HOLDLOCK)").AsNoTracking()
                on a.InternalRequestId equals request.InternalRequestId
            select new {a.InternalRequestId,a.AssignedAt,a.EndedAt,request.Status,request.BranchId,
                request.ReceivingDepartmentId,
                HandlerActive=a.Assignee.AccountStatus=="ACTIVE"&&a.Assignee.EmploymentStatus=="ACTIVE"&&a.Assignee.DeletedAt==null,
                DepartmentValid=request.ReceivingDepartmentId!=null&&request.ReceivingDepartment!.BranchId==request.BranchId
                    && request.ReceivingDepartment.IsActive && request.Branch.IsActive,
                Owners=request.InternalRequestAssignments.Count(x=>x.Status=="ACTIVE"&&x.EndedAt==null)})
            .ToArrayAsync(ct);
        bool? managerMappingInstalled = null;
        bool? membershipInstalled = null;
        foreach(var request in internalAssignments)
        {
            var fact=$"INTERNAL_REQUEST_HANDLER:{request.InternalRequestId}";
            if(request.EndedAt!=null||request.AssignedAt>now||request.Owners!=1||!request.DepartmentValid)
            {unresolved.Add(fact+"_INCONSISTENT");continue;}
            var mappedManager = false;
            var handlerMembership = false;
            if (request.HandlerActive && request.Status == "IN_PROGRESS")
            {
                membershipInstalled ??= await InternalRequestEmployeeMembership.InstalledAsync(db, ct);
                if (membershipInstalled == true)
                    handlerMembership = await InternalRequestEmployeeMembership.Members(db, request.BranchId,
                        request.ReceivingDepartmentId, now).AnyAsync(u => u.UserId == target, ct);
                managerMappingInstalled ??= await new InternalRequestProcessAuthority(db).InstalledAsync(ct);
                if (managerMappingInstalled == true)
                    mappedManager = await db.Database.SqlQuery<int>($"""
                        SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
                        WHERE user_id={target} AND branch_id={request.BranchId}
                          AND department_id={request.ReceivingDepartmentId}
                          AND granted_at<={now} AND revoked_at IS NULL
                        """).SingleAsync(ct) == 1;
            }
            Record(RevokeRoleGrantImpact.InternalRequestHandler(request.Status,before,after,request.BranchId,now,
                handlerMembership,mappedManager),fact);
        }
        // Current assignee only; never CreatedBy, ReviewedBy or Incident.ReportedBy.
        var preventive=await new PreventiveResponsibilityQueries(db).ReadAsync(target,ct,before,after,now);
        blockers.UnionWith(preventive.Blockers);
        unresolved.UnionWith(preventive.Unresolved);
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
        {
            Record(RevokeRoleGrantImpact.Evaluate(before.Where(g => MaterialCreationAccess.IsCreatorRole(g.RoleCode)).ToArray(),
                after.Where(g => MaterialCreationAccess.IsCreatorRole(g.RoleCode)).ToArray(), "MR_CREATE", r.BranchId, true, now), $"MR_REQUESTER:{r.RequestId}");
            Record(RevokeRoleGrantImpact.Evaluate(before, after, "MR_VIEW", r.BranchId, true, now), $"MR_REQUESTER_READ:{r.RequestId}");
        }
        // Reuse latest-cycle predicate; historical approvers and fulfilled_by are not assignments.
        var branches = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.AssignedApproverId == target)
            .Select(c => c.Request.BranchId).Distinct().ToArrayAsync(ct);
        foreach (var branch in branches)
        {
            var approvals = await Dev2DependencyQueries.PendingApprovals(db.MaterialRequestApprovalCycles.AsNoTracking(), target, branch)
                .Select(c => c.Request).ToArrayAsync(ct);
            foreach (var request in approvals)
            {
                var fact = $"MR_APPROVER:{request.RequestId}";
                MaterialApprovalAuthority.Route route;
                try { route = await MaterialApprovalAuthority.ResolveAsync(db, request, now, ct); }
                catch (ValidationException) { unresolved.Add(fact + "_AUTHORITY_UNRESOLVED"); continue; }
                var mapped = await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.department_management_access m WITH(HOLDLOCK)
                    JOIN dbo.departments d WITH(HOLDLOCK) ON d.department_id=m.department_id AND d.branch_id=m.branch_id
                    WHERE m.user_id={target} AND m.branch_id={branch} AND m.department_id={route.DepartmentId}
                      AND m.granted_at<={now} AND m.revoked_at IS NULL AND d.is_active=1 AND d.manager_user_id={target}
                    """).SingleAsync(ct) == 1;
                bool Allows(IReadOnlyList<RevokeGrant> grants) => mapped && target != request.RequesterId
                    && grants.Any(g => g.RoleCode == "DEPT_MANAGER" && g.Path.BranchId == branch
                        && g.Path.PermissionCode == route.Permission && PermissionGrantRules.IsEligible(g.Path, now)
                        && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH");
                Record(!Allows(before) ? RevokeCapabilityImpact.Unknown
                    : Allows(after) ? RevokeCapabilityImpact.Preserved : RevokeCapabilityImpact.Lost, fact);
                Record(RevokeRoleGrantImpact.Evaluate(before, after, "MR_VIEW", branch, false, now), fact + "_READ");
            }
        }
        return new(blockers.ToArray(), unresolved.ToArray());
    }
}
