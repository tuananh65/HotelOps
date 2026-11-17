using System.Globalization;
using System.Text.Json;
using System.Transactions;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public enum AssignRoleStatus { Success, SessionInvalid, Forbidden, NotFound, Inactive, Stale, ScopeConflict, RoleConflict, SerializationUnavailable, LastAdministrator }
public sealed record AssignRoleResult(AssignRoleStatus Status, UserRoleAssignmentDto? Assignment = null, byte[]? Version = null);

public sealed class UserRoleAssignmentService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, UserCredentialResetAccess authorityLocks, TimeProvider clock)
{
    public async Task<AssignRoleResult> AssignAsync(SessionReference reference, long sessionId, long targetId,
        AssignUserRoleRequest request, byte[] expectedVersion, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.BranchId is { } requestedBranch) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedBranch);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (expectedVersion.Length != 8) throw new ArgumentException("An eight-byte rowversion is required.", nameof(expectedVersion));
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(AssignRoleStatus.SessionInvalid);
        if (actor.IsRestricted || reference.IsRestricted || string.IsNullOrWhiteSpace(request.RoleCode)
            || request.RoleCode.Length > 60 || request.RoleCode.Any(char.IsControl))
            return new(AssignRoleStatus.Forbidden);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null || Transaction.Current is not null
            || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Role assignment requires a clean SQL context and its own transaction.");
        var expected = expectedVersion.ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        GlobalAdminTransaction guard;
        try { guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct); }
        catch (GlobalAdminLockException) { return new(AssignRoleStatus.SerializationUnavailable); }
        // Global FIRST, including before User/membership/catalog feature locks.
        var before = await guard.ReadSnapshotAsync(ct);
        foreach (var id in new[] { actor.UserId, targetId }.Order())
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH (UPDLOCK,HOLDLOCK) WHERE user_id={id}")
                .AsNoTracking().ToListAsync(ct);
        await authorityLocks.LockAuthorityAsync(actor.UserId, targetId, ct);
        // Snapshot already retains all grant/permission ranges (including ROLE_MANAGE).
        // Retain the destination branch while final checks may advance time.
        if (request.BranchId is { } branch)
            _ = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (HOLDLOCK) WHERE branch_id={branch}")
                .AsNoTracking().ToListAsync(ct);
        // Retain the assignment range: an explicit reactivation must not race a
        // revoke, expiry edit or another grant for the same unique role/branch.
        _ = await db.UserRoles.FromSqlInterpolated($"SELECT * FROM dbo.user_roles WITH (UPDLOCK,HOLDLOCK) WHERE user_id={targetId}")
            .AsNoTracking().ToListAsync(ct);

        async Task<AssignRoleStatus?> Gate(byte[] version, long? insertedId)
        {
            var valid = await sessions.ValidateSessionAsync(reference, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return AssignRoleStatus.SessionInvalid;
            if (!(await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted
                || !(await permissions.EvaluateGlobalAsync("ROLE_MANAGE", ct)).IsGranted) return AssignRoleStatus.Forbidden;
            if (!await UserRoleAdministrationAccess.IsAdministratorAsync(db, actor.UserId, clock.GetUtcNow().UtcDateTime, ct))
                return AssignRoleStatus.Forbidden;
            var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId && u.DeletedAt == null, ct);
            if (target is null) return AssignRoleStatus.NotFound;
            if (target.AccountStatus != "ACTIVE" || target.EmploymentStatus != "ACTIVE") return AssignRoleStatus.Inactive;
            if (!target.RowVersion.AsSpan().SequenceEqual(version)) return AssignRoleStatus.Stale;
            var now = clock.GetUtcNow().UtcDateTime;
            if (request.BranchId.HasValue && (!await db.Branches.AnyAsync(b => b.BranchId == request.BranchId && b.IsActive, ct)
                || !await db.UserBranchAccesses.AnyAsync(a => a.UserId == targetId && a.BranchId == request.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now, ct))) return AssignRoleStatus.ScopeConflict;
            var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.RoleCode == request.RoleCode, ct);
            if (role is null || !role.IsActive || role.RoleCode != request.RoleCode) return AssignRoleStatus.Forbidden;
            // Existing PermissionGrantRules makes ALL global only for an unbound
            // assignment. Do not turn BRANCH/OWN grants into global grants, or
            // issue an unbound Operations Manager role with no valid branch scope.
            if (request.BranchId is null && (role.RoleCode == "OPERATIONS_MANAGER"
                || !await db.RolePermissions.AnyAsync(g => g.RoleId == role.RoleId && g.DataScope == "ALL", ct)))
                return AssignRoleStatus.ScopeConflict;
            // UC009 owns current grants. Adding a role never bypasses resource/department
            // authority or segregation of duties enforced by each business operation.
            var operationsManager = role.RoleCode == "OPERATIONS_MANAGER" || await db.UserRoles.AnyAsync(a => a.UserId == targetId
                && a.IsActive && a.Role.IsActive && a.Role.RoleCode == "OPERATIONS_MANAGER" && (a.ExpiresAt == null || a.ExpiresAt > now), ct);
            if (operationsManager && await db.UserBranchAccesses.Where(a => a.UserId == targetId && a.RevokedAt == null
                && a.Branch.IsActive).Select(a => a.BranchId).Distinct().CountAsync(ct) != 1) return AssignRoleStatus.ScopeConflict;
            var existing = await db.UserRoles.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == targetId
                && r.RoleId == role.RoleId && r.BranchId == request.BranchId, ct);
            if (insertedId.HasValue)
            {
                if (existing?.UserRoleId != insertedId || !existing.IsActive) return AssignRoleStatus.RoleConflict;
            }
            else if (request.ReactivateExisting)
            {
                // Renewal of an active or scheduled assignment is a different action.
                // A missing record must not silently turn reactivation into a new grant.
                if (existing is null || existing.IsActive) return AssignRoleStatus.RoleConflict;
            }
            else if (existing is not null) return AssignRoleStatus.RoleConflict;
            return null;
        }
        if (await Gate(expected, null) is { } denied) return new(denied);
        var selectedRoleId = await db.Roles.Where(r => r.RoleCode == request.RoleCode).Select(r => r.RoleId).SingleAsync(ct);
        var previousAssignment = request.ReactivateExisting
            ? await db.UserRoles.AsNoTracking().SingleAsync(r => r.UserId == targetId && r.RoleId == selectedRoleId
                && r.BranchId == request.BranchId && !r.IsActive, ct)
            : null;
        var assignedAt = clock.GetUtcNow().UtcDateTime;
        if (await db.Users.Where(u => u.UserId == targetId && u.RowVersion == expected && u.AccountStatus == "ACTIVE" && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, assignedAt), ct) != 1) return new(AssignRoleStatus.Stale);
        var assignment = new UserRole { UserId = targetId, RoleId = selectedRoleId, BranchId = request.BranchId,
            AssignedBy = actor.UserId, AssignedAt = assignedAt, IsActive = true, ExpiresAt = null };
        if (previousAssignment is not null)
        {
            assignment.UserRoleId = previousAssignment.UserRoleId;
            if (await db.UserRoles.Where(r => r.UserRoleId == previousAssignment.UserRoleId && !r.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true)
                    .SetProperty(r => r.AssignedAt, assignedAt).SetProperty(r => r.AssignedBy, actor.UserId)
                    .SetProperty(r => r.ExpiresAt, (DateTime?)null), ct) != 1)
                return new(AssignRoleStatus.RoleConflict);
        }
        else
        {
            db.Add(assignment);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
            { return new(AssignRoleStatus.RoleConflict); }
            finally { db.Entry(assignment).State = EntityState.Detached; }
        }
        var dto = new UserRoleAssignmentDto(assignment.UserRoleId, targetId, request.RoleCode, request.BranchId,
            actor.UserId, assignedAt, true, null);
        var audit = new AuditLog { ActorUserId = actor.UserId, BranchId = request.BranchId, EntityType = "user_roles",
            EntityId = assignment.UserRoleId.ToString(CultureInfo.InvariantCulture), Action = previousAssignment is null ? "CREATE" : "UPDATE", OccurredAt = assignedAt,
            OldValues = previousAssignment is null ? JsonSerializer.Serialize(new { assignment = (object?)null })
                : JsonSerializer.Serialize(new { id = previousAssignment.UserRoleId, userId = targetId, roleId = selectedRoleId,
                    roleCode = request.RoleCode, branchId = previousAssignment.BranchId,
                    assignedBy = previousAssignment.AssignedBy, assignedAtUtc = previousAssignment.AssignedAt,
                    isActive = previousAssignment.IsActive, expiresAtUtc = previousAssignment.ExpiresAt }),
            NewValues = JsonSerializer.Serialize(dto),
            ChangedFields = JsonSerializer.Serialize(previousAssignment is null ? new[] { "assignment" }
                : new[] { "isActive", "assignedBy", "assignedAtUtc", "expiresAtUtc" }) };
        db.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        // Adding/reactivating a role cannot remove an old eligible principal. Keep
        // the existing nonzero-baseline poststate check without blocking zero-admin workforce setup.
        if (before.DurableEligibleUserIds.Count > 0)
        {
            try { await guard.EnsurePreservedAsync(before, ct); }
            catch (GlobalAdministrationInvariantException) { return new(AssignRoleStatus.LastAdministrator); }
        }
        if (await Gate(version, assignment.UserRoleId) is { } finalDenied) return new(finalDenied);
        await new SessionRevocationService(db).RevokeAllAsync(targetId, clock.GetUtcNow().UtcDateTime, ct);
        if (!await db.UserRoles.AnyAsync(r => r.UserRoleId == assignment.UserRoleId && r.UserId == targetId && r.RoleId == selectedRoleId
            && r.BranchId == request.BranchId && r.AssignedBy == actor.UserId && r.AssignedAt == assignedAt && r.IsActive && r.ExpiresAt == null, ct))
            throw new DbUpdateConcurrencyException("Staged role assignment was not preserved.");
        await transaction.CommitAsync(ct);
        return new(AssignRoleStatus.Success, dto, version);
    }
}
