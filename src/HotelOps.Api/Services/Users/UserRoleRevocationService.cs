using System.Globalization;
using System.Text.Json;
using System.Transactions;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>Source candidate only. No caller, config or empty responsibility result can open release.</summary>
public sealed class UserRoleRevocationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, UserCredentialResetAccess authorityLocks, TimeProvider clock)
{
    public static IReadOnlyList<string> PendingReleaseGates { get; } = Array.AsReadOnly(new[] {
        "OPEN_WORK_PREDICATES_AND_WRITERS", "ATOMIC_SESSION_REVOCATION_ACCEPTANCE", "SQL_RACE_ROLLBACK_HTTPS_ACCEPTANCE" });

    public async Task<RevokeRoleResult> RevokeAsync(SessionReference reference, long sessionId, long targetId,
        long assignmentId, byte[] expectedVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(assignmentId);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (expectedVersion.Length != 8) throw new ArgumentException("Eight-byte User version required.", nameof(expectedVersion));
        ct.ThrowIfCancellationRequested();
        if (sessionId <= 0 || !current.TryGetCurrentUser(out var actor) || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(RevokeRoleStatus.SessionInvalid);
        if (actor.IsRestricted || reference.IsRestricted || actor.UserId == targetId) return new(RevokeRoleStatus.Forbidden);
        // Mandatory source-level fuse BEFORE SQL: even direct callers cannot dispatch the candidate writer.
        if (PendingReleaseGates.Count != 0) return new(RevokeRoleStatus.ReleaseAcceptanceRequired);
        return await ExecuteReleasedAsync(reference, sessionId, actor, targetId, assignmentId, expectedVersion.ToArray(), ct);
    }

    private async Task<RevokeRoleResult> ExecuteReleasedAsync(SessionReference reference, long sessionId,
        CurrentUserInfo actor, long targetId, long assignmentId, byte[] expected, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Revoke requires its own clean SQL transaction.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        GlobalAdminTransaction guard;
        try { guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct); }
        catch (GlobalAdminLockException) { return new(RevokeRoleStatus.SerializationUnavailable); }
        var before = await guard.ReadSnapshotAsync(ct); // Global first; retained Users universe before feature locks.
        await new UserEligibilityLock(db).LockAsync([actor.UserId, targetId], ct);
        await authorityLocks.LockAuthorityAsync(actor.UserId, targetId, ct);
        async Task<RevokeRoleStatus?> Authority()
        {
            var valid = await sessions.ValidateSessionAsync(reference, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.UserId != actor.UserId || valid.Session.IsRestricted) return RevokeRoleStatus.SessionInvalid;
            if (!(await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted || !(await permissions.EvaluateGlobalAsync("ROLE_MANAGE", ct)).IsGranted) return RevokeRoleStatus.Forbidden;
            ct.ThrowIfCancellationRequested(); return null;
        }
        if (await Authority() is { } denied) return new(denied);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId && u.DeletedAt == null, ct);
        if (target == null) return new(RevokeRoleStatus.NotFound);
        if (!target.RowVersion.AsSpan().SequenceEqual(expected)) return new(RevokeRoleStatus.Stale);
        if (target.AccountStatus != "ACTIVE") return new(RevokeRoleStatus.Conflict);
        var assignments = await db.UserRoles.FromSqlInterpolated($"SELECT * FROM dbo.user_roles WITH (UPDLOCK,HOLDLOCK) WHERE user_id={targetId}").AsNoTracking().ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (ValidateAssignment(target, assignments, assignmentId, expected, now) is { } invalid) return new(invalid);
        var assignment = assignments.Single(a => a.UserRoleId == assignmentId);
        var branchId = assignment.BranchId;
        var role = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleId == assignment.RoleId, ct);
        if (!role.IsActive || role.IsSystemRole || !UserRoleDelegationPolicy.TryGetDepartment(role.RoleCode, out var departmentCode)) return new(RevokeRoleStatus.Forbidden);
        var grants = await db.RolePermissions.AsNoTracking().Where(g => g.RoleId == role.RoleId).Select(g => g.Permission.PermissionCode + "/" + g.DataScope).ToArrayAsync(ct);
        if (!UserRoleDelegationPolicy.Matches(role.RoleCode, grants)) return new(RevokeRoleStatus.Forbidden);
        _ = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (UPDLOCK,HOLDLOCK) WHERE branch_id={branchId}").AsNoTracking().ToArrayAsync(ct);
        _ = await db.Departments.FromSqlInterpolated($"SELECT * FROM dbo.departments WITH (HOLDLOCK) WHERE department_id={target.DepartmentId}").AsNoTracking().ToArrayAsync(ct);
        if (!await db.Branches.AnyAsync(b => b.BranchId == branchId && b.IsActive, ct)
            || !await db.Departments.AnyAsync(d => d.DepartmentId == target.DepartmentId && d.BranchId == branchId && d.IsActive && d.DepartmentCode == departmentCode, ct)
            || !await db.UserBranchAccesses.AnyAsync(a => a.UserId == targetId && a.BranchId == branchId && a.RevokedAt == null && a.GrantedAt <= now, ct)) return new(RevokeRoleStatus.Forbidden);
        // Read the complete target, not just the role branch; unscoped/inactive-branch
        // responsibilities may still require this executor. Readers never certify writers.
        async Task<RevokeRoleStatus?> Responsibilities()
        {
            var review = await new IntegratedDeactivationResponsibilityQueries(db).ReadAsync(targetId, ct);
            return ValidateResponsibilities(review.Blockers, review.Unresolved, review.Unsupported,
                PendingResponsibilityWriters);
        }
        if (await Responsibilities() is { } dependency) return new(dependency);
        if (PendingReleaseGates.Count != 0) return new(RevokeRoleStatus.ReleaseAcceptanceRequired);
        if (await Authority() is { } preWrite) return new(preWrite);
        // Refresh mutable eligibility/expiry at the write boundary under retained locks.
        var finalTarget = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == targetId, ct);
        var finalAssignments = await db.UserRoles.AsNoTracking().Where(a => a.UserId == targetId).ToArrayAsync(ct);
        if (ValidateAssignment(finalTarget, finalAssignments, assignmentId, expected, clock.GetUtcNow().UtcDateTime) is { } changed)
            return new(changed);
        if (await Responsibilities() is { } changedWork) return new(changedWork);
        if (await db.Users.Where(u => u.UserId == targetId && u.RowVersion == expected && u.DeletedAt == null && u.AccountStatus == "ACTIVE")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, now), ct) != 1) return new(RevokeRoleStatus.Stale);
        if (await db.UserRoles.Where(a => a.UserRoleId == assignmentId && a.UserId == targetId && a.BranchId == branchId && a.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false), ct) != 1) return new(RevokeRoleStatus.Conflict);
        var revoked = await new SessionRevocationService(db).RevokeAllAsync(targetId, now, ct);
        var audit = new AuditLog
        {
            ActorUserId = actor.UserId,
            BranchId = branchId,
            EntityType = "user_roles",
            EntityId = assignmentId.ToString(CultureInfo.InvariantCulture),
            Action = "UPDATE",
            OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { isActive = true }),
            NewValues = JsonSerializer.Serialize(new { isActive = false, revokedSessions = revoked }),
            ChangedFields = "[\"isActive\"]"
        };
        db.Add(audit); try { await db.SaveChangesAsync(ct); } finally { db.Entry(audit).State = EntityState.Detached; }
        try { await guard.EnsurePreservedAsync(before, ct); } catch (GlobalAdministrationInvariantException) { return new(RevokeRoleStatus.LastAdministrator); }
        if (await Authority() is { } final) return new(final);
        if (await Responsibilities() is { } finalWork) return new(finalWork);
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        await tx.CommitAsync(ct); return new(RevokeRoleStatus.Success, version);
        // All early returns/errors dispose the uncommitted transaction: no role/session/audit partial commit.
    }
    /// <summary>Pure validation of server facts, not permission/open-work/release authority.</summary>
    public static RevokeRoleStatus? ValidateAssignment(User target, IReadOnlyList<UserRole> history, long assignmentId, byte[] expected, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        if (target.DeletedAt != null) return RevokeRoleStatus.NotFound;
        if (!target.RowVersion.AsSpan().SequenceEqual(expected)) return RevokeRoleStatus.Stale;
        if (target.AccountStatus != "ACTIVE") return RevokeRoleStatus.Conflict;
        var selected = history.SingleOrDefault(a => a.UserRoleId == assignmentId);
        if (selected == null) return RevokeRoleStatus.NotFound;
        if (selected.UserId != target.UserId || selected.BranchId == null || selected.BranchId != target.PrimaryBranchId || target.DepartmentId == null) return RevokeRoleStatus.Forbidden;
        if (history.Count != 1 || !selected.IsActive || selected.AssignedAt > nowUtc || selected.ExpiresAt <= nowUtc) return RevokeRoleStatus.Conflict;
        return null;
    }
    // Missing external participation is explicit, immutable evidence debt; never a config switch.
    public static IReadOnlyList<string> PendingResponsibilityWriters { get; } = Array.AsReadOnly(new[] {
        "DEV1_TASK_CLEANING_INSPECTION_REQUEST_SCHEDULE_WRITERS",
        "DEV2_MAINTENANCE_ASSET_INVENTORY_REQUEST_WRITERS",
        "EXTERNAL_IMPORT_JOB_DIRECT_SQL_WRITERS" });

    /// <summary>Pure classification only; a clear result cannot bypass PendingReleaseGates.</summary>
    public static RevokeRoleStatus? ValidateResponsibilities(IReadOnlyCollection<string> blockers,
        IReadOnlyCollection<string> unresolved, IReadOnlyCollection<string> unsupported,
        IReadOnlyCollection<string> uncoordinatedWriters)
    {
        ArgumentNullException.ThrowIfNull(blockers);
        ArgumentNullException.ThrowIfNull(unresolved);
        ArgumentNullException.ThrowIfNull(unsupported);
        ArgumentNullException.ThrowIfNull(uncoordinatedWriters);
        if (unresolved.Count != 0 || unsupported.Count != 0 || uncoordinatedWriters.Count != 0)
            return RevokeRoleStatus.ResponsibilitiesUnresolved;
        return blockers.Count != 0 ? RevokeRoleStatus.ResponsibilitiesBlocked : null;
    }
}
