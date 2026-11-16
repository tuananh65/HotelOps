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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BranchId);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (expectedVersion.Length != 8) throw new ArgumentException("An eight-byte rowversion is required.", nameof(expectedVersion));
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(AssignRoleStatus.SessionInvalid);
        if (actor.IsRestricted || actor.UserId == targetId || string.IsNullOrWhiteSpace(request.RoleCode)
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
        _ = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (HOLDLOCK) WHERE branch_id={request.BranchId}")
            .AsNoTracking().ToListAsync(ct);

        async Task<AssignRoleStatus?> Gate(byte[] version, long? insertedId)
        {
            var valid = await sessions.ValidateSessionAsync(reference, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return AssignRoleStatus.SessionInvalid;
            if (!(await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted
                || !(await permissions.EvaluateGlobalAsync("ROLE_MANAGE", ct)).IsGranted) return AssignRoleStatus.Forbidden;
            var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId && u.DeletedAt == null, ct);
            if (target is null) return AssignRoleStatus.NotFound;
            if (target.AccountStatus != "ACTIVE" || target.EmploymentStatus != "ACTIVE") return AssignRoleStatus.Inactive;
            if (!target.RowVersion.AsSpan().SequenceEqual(version)) return AssignRoleStatus.Stale;
            var now = clock.GetUtcNow().UtcDateTime;
            if (!await db.Branches.AnyAsync(b => b.BranchId == request.BranchId && b.IsActive, ct)
                || !await db.UserBranchAccesses.AnyAsync(a => a.UserId == targetId && a.BranchId == request.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now, ct)) return AssignRoleStatus.ScopeConflict;
            var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.RoleCode == request.RoleCode, ct);
            if (role is null || !role.IsActive || role.RoleCode != request.RoleCode) return AssignRoleStatus.Forbidden;
            // UC009 owns current grants. Adding a role never bypasses resource/department
            // authority or segregation of duties enforced by each business operation.
            var operationsManager = role.RoleCode == "OPERATIONS_MANAGER" || await db.UserRoles.AnyAsync(a => a.UserId == targetId
                && a.IsActive && a.Role.IsActive && a.Role.RoleCode == "OPERATIONS_MANAGER" && (a.ExpiresAt == null || a.ExpiresAt > now), ct);
            if (operationsManager && await db.UserBranchAccesses.Where(a => a.UserId == targetId && a.RevokedAt == null
                && a.Branch.IsActive).Select(a => a.BranchId).Distinct().CountAsync(ct) != 1) return AssignRoleStatus.ScopeConflict;
            // The schema retains one record per user/role/branch. Preserve old assignments;
            // a prior assignment for another role or branch does not prohibit additional roles.
            if (await db.UserRoles.AnyAsync(r => r.UserId == targetId && r.RoleId == role.RoleId && r.BranchId == request.BranchId
                && (!insertedId.HasValue || r.UserRoleId != insertedId.Value), ct))
                return AssignRoleStatus.RoleConflict;
            return null;
        }
        if (await Gate(expected, null) is { } denied) return new(denied);
        var selectedRoleId = await db.Roles.Where(r => r.RoleCode == request.RoleCode).Select(r => r.RoleId).SingleAsync(ct);
        var assignedAt = clock.GetUtcNow().UtcDateTime;
        if (await db.Users.Where(u => u.UserId == targetId && u.RowVersion == expected && u.AccountStatus == "ACTIVE" && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, assignedAt), ct) != 1) return new(AssignRoleStatus.Stale);
        var assignment = new UserRole { UserId = targetId, RoleId = selectedRoleId, BranchId = request.BranchId,
            AssignedBy = actor.UserId, AssignedAt = assignedAt, IsActive = true, ExpiresAt = null };
        db.Add(assignment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { return new(AssignRoleStatus.RoleConflict); }
        finally { db.Entry(assignment).State = EntityState.Detached; }
        var dto = new UserRoleAssignmentDto(assignment.UserRoleId, targetId, request.RoleCode, request.BranchId,
            actor.UserId, assignedAt, true, null);
        var audit = new AuditLog { ActorUserId = actor.UserId, BranchId = request.BranchId, EntityType = "user_roles",
            EntityId = assignment.UserRoleId.ToString(CultureInfo.InvariantCulture), Action = "CREATE", OccurredAt = assignedAt,
            OldValues = JsonSerializer.Serialize(new { assignment = (object?)null }), NewValues = JsonSerializer.Serialize(dto),
            ChangedFields = JsonSerializer.Serialize(new[] { "assignment" }) };
        db.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        // The new role is branch-bound and cannot remove an old eligible principal. Keep
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
