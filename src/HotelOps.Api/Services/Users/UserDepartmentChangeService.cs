using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Users;

/// <summary>Narrow role-less same-primary-branch correction. Not registered or released.</summary>
public sealed class UserDepartmentChangeService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, UserCredentialResetAccess locks, TimeProvider clock)
{
    public static IReadOnlyList<string> PendingReleaseGates { get; } = Array.AsReadOnly(new[] {
        "ROLELESS_TARGET_FIXTURE_ACCEPTANCE", "COMPLETE_RESPONSIBILITY_WRITER_CENSUS", "SQL_SESSION_AUDIT_RACE_ROLLBACK_HTTPS" });

    public async Task<DepartmentChangeResult> ChangeAsync(SessionReference ticket, long sessionId, long targetId,
        ChangeDepartmentRequest request, byte[] expectedVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DepartmentId);
        if (expectedVersion.Length != 8) throw new ArgumentException("Eight-byte User rowversion required.", nameof(expectedVersion));
        ct.ThrowIfCancellationRequested();
        if (sessionId <= 0 || !current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId)
            return new(DepartmentChangeStatus.SessionInvalid);
        if (actor.IsRestricted || ticket.IsRestricted || actor.UserId == targetId) return new(DepartmentChangeStatus.Forbidden);
        // Immutable, pre-SQL release boundary. Neither request nor configuration can open it.
        if (PendingReleaseGates.Count != 0) return new(DepartmentChangeStatus.ReleaseRequired);
        return await ExecuteAsync(ticket, sessionId, actor.UserId, targetId, request.DepartmentId, expectedVersion.ToArray(), ct);
    }

    public async Task<DepartmentReviewResult> ReadAsync(SessionReference ticket, long sessionId, long targetId, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId)
            return new(DepartmentChangeStatus.SessionInvalid);
        if (actor.IsRestricted || ticket.IsRestricted || actor.UserId == targetId) return new(DepartmentChangeStatus.Forbidden);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Clean owned read transaction required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await new UserEligibilityLock(db).LockAsync([actor.UserId, targetId], ct);
        await locks.LockAuthorityAsync(actor.UserId, targetId, ct);
        var session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sessionId || session.Session.UserId != actor.UserId || session.Session.IsRestricted)
            return new(DepartmentChangeStatus.SessionInvalid);
        var authority = await permissions.EvaluateGlobalAsync("USER_MANAGE", ct);
        if (!authority.IsGranted) return new(authority.Decision == PermissionDecision.Unavailable ? DepartmentChangeStatus.CoverageRequired : DepartmentChangeStatus.Forbidden);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId && u.DeletedAt == null, ct);
        if (target == null) return new(DepartmentChangeStatus.NotFound);
        if (target.PrimaryBranchId == null || target.DepartmentId == null) return new(DepartmentChangeStatus.InvalidScope);
        session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sessionId || session.Session.UserId != actor.UserId || session.Session.IsRestricted)
            return new(DepartmentChangeStatus.SessionInvalid);
        authority = await permissions.EvaluateGlobalAsync("USER_MANAGE", ct);
        if (!authority.IsGranted) return new(authority.Decision == PermissionDecision.Unavailable ? DepartmentChangeStatus.CoverageRequired : DepartmentChangeStatus.Forbidden);
        await tx.CommitAsync(ct);
        return new(DepartmentChangeStatus.Success, new(targetId, target.PrimaryBranchId.Value, target.DepartmentId.Value), target.RowVersion.ToArray());
    }

    private async Task<DepartmentChangeResult> ExecuteAsync(SessionReference ticket, long sessionId, long actorId,
        long targetId, long destinationId, byte[] expected, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Clean caller context and owned SQL transaction required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        GlobalAdminTransaction guard;
        try { guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct); }
        catch (GlobalAdminLockException) { return new(DepartmentChangeStatus.CoverageRequired); }
        var before = await guard.ReadSnapshotAsync(ct);
        await new UserEligibilityLock(db).LockAsync([actorId, targetId], ct);
        await locks.LockAuthorityAsync(actorId, targetId, ct);
        async Task<DepartmentChangeStatus?> Authority()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.UserId != actorId || valid.Session.IsRestricted)
                return DepartmentChangeStatus.SessionInvalid;
            // Restrictive global admission covers BOTH old and destination department scope.
            var permission = await permissions.EvaluateGlobalAsync("USER_MANAGE", ct);
            if (!permission.IsGranted) return permission.Decision == PermissionDecision.Unavailable ? DepartmentChangeStatus.CoverageRequired : DepartmentChangeStatus.Forbidden;
            return null;
        }
        if (await Authority() is { } denied) return new(denied);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId, ct);
        if (target == null || target.DeletedAt != null) return new(DepartmentChangeStatus.NotFound);
        var branchId = target.PrimaryBranchId;
        var branch = (await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (UPDLOCK,HOLDLOCK) WHERE branch_id={branchId}")
            .AsNoTracking().ToArrayAsync(ct)).SingleOrDefault();
        // Deterministic Department ordering; retain row/range locks across old and new scope.
        var departments = await db.Departments.FromSqlInterpolated($"SELECT * FROM dbo.departments WITH (UPDLOCK,HOLDLOCK) WHERE department_id IN ({target.DepartmentId},{destinationId}) ORDER BY department_id")
            .AsNoTracking().ToArrayAsync(ct);
        var membership = await db.UserBranchAccesses.AsNoTracking().Where(a => a.UserId == targetId).ToArrayAsync(ct);
        var roles = await db.UserRoles.FromSqlInterpolated($"SELECT * FROM dbo.user_roles WITH (UPDLOCK,HOLDLOCK) WHERE user_id={targetId}")
            .AsNoTracking().ToArrayAsync(ct);
        var old = departments.SingleOrDefault(d => d.DepartmentId == target.DepartmentId);
        var destination = departments.SingleOrDefault(d => d.DepartmentId == destinationId);
        if (ValidateState(target, branch, old, destination, membership, roles, expected, clock.GetUtcNow().UtcDateTime) is { } invalid) return new(invalid);
        async Task<DepartmentChangeStatus?> Work()
        {
            var review = await new IntegratedDeactivationResponsibilityQueries(db).ReadAsync(targetId, ct);
            var status = UserRoleRevocationService.ValidateResponsibilities(review.Blockers, review.Unresolved, review.Unsupported,
                UserRoleRevocationService.PendingResponsibilityWriters.ToArray());
            return status == RevokeRoleStatus.ResponsibilitiesBlocked ? DepartmentChangeStatus.OpenWork
                : status != null ? DepartmentChangeStatus.CoverageRequired : null;
        }
        if (await Work() is { } work) return new(work);
        if (await Authority() is { } finalDenied) return new(finalDenied);
        var finalTarget = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == targetId, ct);
        if (ValidateState(finalTarget, branch, old, destination, membership, roles, expected, clock.GetUtcNow().UtcDateTime) is { } finalInvalid)
            return new(finalInvalid);
        if (await Work() is { } finalWork) return new(finalWork);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await db.Users.Where(u => u.UserId == targetId && u.RowVersion == expected && u.PrimaryBranchId == branchId
            && u.DepartmentId == target.DepartmentId && u.AccountStatus == "ACTIVE" && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, (long?)destinationId).SetProperty(u => u.UpdatedAt, now), ct) != 1)
            return new(DepartmentChangeStatus.Stale);
        var revoked = await new SessionRevocationService(db).RevokeAllAsync(targetId, now, ct);
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            BranchId = branchId,
            EntityType = "users",
            EntityId = targetId.ToString(CultureInfo.InvariantCulture),
            Action = "UPDATE",
            OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { departmentId = target.DepartmentId }),
            NewValues = JsonSerializer.Serialize(new { departmentId = destinationId, revokedSessions = revoked }),
            ChangedFields = "[\"departmentId\"]"
        };
        db.Add(audit); try { await db.SaveChangesAsync(ct); } finally { db.Entry(audit).State = EntityState.Detached; }
        try { await guard.EnsurePreservedAsync(before, ct); } catch (GlobalAdministrationInvariantException) { return new(DepartmentChangeStatus.LastAdministrator); }
        if (await Authority() is { } commitDenied) return new(commitDenied);
        if (await Work() is { } commitWork) return new(commitWork);
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        await tx.CommitAsync(ct);
        return new(DepartmentChangeStatus.Success, new(targetId, branchId!.Value, target.DepartmentId!.Value, destinationId), version);
    }

    /// <summary>Server facts only. No caller-supplied authorization, writer or release clearance.</summary>
    public static DepartmentChangeStatus? ValidateState(User target, Branch? branch, Department? old, Department? destination,
        IReadOnlyList<UserBranchAccess> memberships, IReadOnlyList<UserRole> history, byte[] expected, DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(now));
        if (target.DeletedAt != null) return DepartmentChangeStatus.NotFound;
        if (!target.RowVersion.AsSpan().SequenceEqual(expected)) return DepartmentChangeStatus.Stale;
        if (target.AccountStatus != "ACTIVE" || target.EmploymentStatus != "ACTIVE" || branch == null || !branch.IsActive
            || target.PrimaryBranchId != branch.BranchId || old == null || !old.IsActive || old.DepartmentId != target.DepartmentId
            || old.BranchId != branch.BranchId || destination == null || !destination.IsActive || destination.BranchId != branch.BranchId)
            return DepartmentChangeStatus.InvalidScope;
        // Any role path, including inactive/expired/unbound, needs a separate envelope/transfer design.
        if (history.Count != 0) return DepartmentChangeStatus.RoleConflict;
        var primary = memberships.Where(a => a.IsPrimary).ToArray();
        if (primary.Length != 1 || primary[0].UserId != target.UserId || primary[0].BranchId != branch.BranchId
            || primary[0].RevokedAt != null || primary[0].GrantedAt > now || primary[0].AccessLevel != "STANDARD")
            return DepartmentChangeStatus.InvalidScope;
        if (destination.DepartmentId == old.DepartmentId) return DepartmentChangeStatus.NoChange;
        return null;
    }
}