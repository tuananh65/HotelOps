using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dependencies;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public static IReadOnlyList<string> DepartmentDeactivationReleaseGates { get; } = Array.AsReadOnly(new[] { "ACTIVE_USER_BINDING_WRITERS", "WAREHOUSE_TASK_REQUEST_CUSTODY_COVERAGE", "CROSS_MODULE_LOCK_ORDER", "ATOMIC_AUDIT_SQL_ACCEPTANCE" });

    /// <summary>Owned transaction writer remains unreachable until immutable release gates are accepted.</summary>
    public async Task<RoomTypeBatchResult> DeactivateDepartmentCandidateAsync(SessionReference ticket, long sessionId,
        long branchId, long id, string expected, DeactivateStructureRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(request);
        if (!request.Confirmation) throw new ValidationException("Confirmation required.");
        var reason = Batch2Reason(request.Reason); Batch2Tag(expected);
        if (branchId <= 0 || id <= 0) throw new ValidationException("Positive branch and department required.");
        ct.ThrowIfCancellationRequested();
        if (sessionId <= 0 || !current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId
            || actor.SessionId != sessionId || actor.IsRestricted || ticket.IsRestricted)
            return new(RoomTypeBatchStatus.SessionInvalid);
        if (DepartmentDeactivationReleaseGates.Count != 0 || departmentDependencies == null)
            return new(RoomTypeBatchStatus.Unavailable);

        // BatchAsync retains Serializable, User-first/Branch/catalog locks, final session/permission
        // admission and commit. Non-success/exception paths dispose its uncommitted transaction.
        return await BatchAsync(ticket, sessionId, branchId, async (actorId, token) =>
        {
            var row = await db.Departments.FromSqlInterpolated(
                    $"SELECT * FROM dbo.departments WITH(UPDLOCK,HOLDLOCK) WHERE department_id={id} AND branch_id={branchId}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            if (!row.IsActive) return new(RoomTypeBatchStatus.Inactive);
            var before = DepartmentDto(row);
            if (await DepartmentTag(before, token) != expected) return new(RoomTypeBatchStatus.Stale);
            try
            {
                var dependencies = await departmentDependencies.ReadStructureInCallerTransactionAsync("DEPARTMENT", id, branchId, token);
                var status = await DepartmentDependencyStatus(dependencies, token);
                if (status != RoomTypeBatchStatus.Success) return new(status);
                var valid = await sessions.ValidateSessionAsync(ticket, token);
                if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.UserId != actorId
                    || valid.Session.IsRestricted || !current.TryGetCurrentUser(out var observed) || observed != actor)
                    return new(RoomTypeBatchStatus.SessionInvalid);
                var access = await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE", branchId, token);
                if (!access.IsGranted) return new(access.Decision == HotelOps.Api.Security.Authorization.PermissionDecision.Unavailable
                    ? RoomTypeBatchStatus.Unavailable : RoomTypeBatchStatus.Forbidden);
                var fresh = await db.Departments.AsNoTracking().SingleOrDefaultAsync(d => d.DepartmentId == id && d.BranchId == branchId, token);
                if (fresh == null || DepartmentDto(fresh) != before || await DepartmentTag(before, token) != expected)
                    return new(RoomTypeBatchStatus.Stale);
                var finalDependencies = await departmentDependencies.RevalidateInCallerTransactionAsync(dependencies, token);
                status = await DepartmentDependencyStatus(finalDependencies, token);
                if (status != RoomTypeBatchStatus.Success) return new(status);
                token.ThrowIfCancellationRequested();

                // Department has no rowversion. Retained row/audit locks protect the canonical ETag;
                // the conditional update also binds the real fields and original active state.
                var affected = await DepartmentDeactivationRows(db.Departments, before)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsActive, false), token);
                if (affected != 1) return new(RoomTypeBatchStatus.Stale);
                var after = before with { IsActive = false };
                await BatchAuditAsync(actorId, branchId, "departments", id, before,
                    new { record = after, reason }, ["isActive"], token);
                return new(RoomTypeBatchStatus.Success, after, await DepartmentTag(after, token));
            }
            catch (DependencyScopeException) { return new(RoomTypeBatchStatus.Forbidden); }
            catch (DependencySnapshotConflictException) { return new(RoomTypeBatchStatus.Stale); }
        }, ct, requireRoomTypeSchema: false);
    }

    private async Task<RoomTypeBatchStatus> DepartmentDependencyStatus(DependencySnapshot snapshot, CancellationToken ct)
    {
        // GBR-042: do not reassign accounts or discard warehouse references.
        var activeUsers = await db.Users.AsNoTracking().AnyAsync(u => u.DepartmentId == snapshot.ResourceId && u.AccountStatus == "ACTIVE", ct);
        var warehouses = await db.Warehouses.AsNoTracking().AnyAsync(w => w.DepartmentId == snapshot.ResourceId, ct);
        // DEV1 task/internal-request predicates and cross-module writer coverage are not accepted.
        // An empty diagnostic snapshot cannot replace their explicit acceptance.
        return DepartmentDeactivationDecision(snapshot, activeUsers, warehouses, ownerCoverageComplete: false);
    }

    internal static RoomTypeBatchStatus DepartmentDeactivationDecision(DependencySnapshot snapshot,
        bool activeUsers, bool warehouses, bool ownerCoverageComplete)
    {
        if (activeUsers || warehouses || snapshot.Facts.Any(f => f.Classification is DependencyClassification.BLOCKING or DependencyClassification.TRANSFER_REQUIRED))
            return RoomTypeBatchStatus.DependenciesExist;
        if (!ownerCoverageComplete || !snapshot.CanDeactivate || snapshot.Unresolved.Count != 0
            || snapshot.Facts.Any(f => f.Classification == DependencyClassification.UNKNOWN_POLICY))
            return RoomTypeBatchStatus.Unavailable;
        return RoomTypeBatchStatus.Success;
    }

    internal static IQueryable<HotelOps.Api.Entities.Department> DepartmentDeactivationRows(
        IQueryable<HotelOps.Api.Entities.Department> rows, DepartmentInformationDto before)
        => rows.Where(d => d.DepartmentId == before.Id && d.BranchId == before.BranchId && d.IsActive
            && d.DepartmentCode == before.Code && d.DepartmentName == before.Name
            && d.ScopeDescription == before.Description && d.ManagerUserId == before.ManagerUserId);
}
