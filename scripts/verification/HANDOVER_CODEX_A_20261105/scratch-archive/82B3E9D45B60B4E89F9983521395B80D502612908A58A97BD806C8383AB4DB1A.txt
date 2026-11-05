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

public enum AddBranchStatus { Success, Forbidden, SessionInvalid, NotFound, Inactive, InvalidBranch, Stale, Duplicate, RoleConflict, OperationsManagerConflict }
public sealed record AddBranchResult(AddBranchStatus Status, UserBranchMembershipDto? Membership = null, byte[]? Version = null);

public sealed class UserBranchAssignmentService(HotelOpsDbContext db, UserBranchAssignmentAccess access,
    ICurrentUserAccessor current, ISqlSessionService sessions, TimeProvider clock)
{
    public async Task<AddBranchResult> AddAsync(SessionReference reference, long sessionId, long targetId,
        long branchId, byte[] expectedVersion, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(branchId);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (expectedVersion.Length != 8) throw new ArgumentException("An eight-byte rowversion is required.", nameof(expectedVersion));
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(AddBranchStatus.SessionInvalid);
        if (actor.IsRestricted || actor.UserId == targetId) return new(AddBranchStatus.Forbidden);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null || Transaction.Current is not null
            || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Add membership requires a clean context and its own SQL transaction.");
        var expected = expectedVersion.ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        foreach (var id in new[] { actor.UserId, targetId }.Order())
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH (UPDLOCK,HOLDLOCK) WHERE user_id={id}")
                .AsNoTracking().ToListAsync(ct);
        await access.LockAuthorityAsync(actor.UserId, targetId, ct);
        // Lock target assignment rows AND absent key ranges before checking dormant paths/OM cardinality.
        _ = await db.UserRoles.FromSqlInterpolated($"SELECT * FROM dbo.user_roles WITH (HOLDLOCK) WHERE user_id={targetId}")
            .AsNoTracking().ToListAsync(ct);
        _ = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles r WITH (HOLDLOCK) WHERE EXISTS (SELECT 1 FROM dbo.user_roles ur WHERE ur.user_id={targetId} AND ur.role_id=r.role_id)")
            .AsNoTracking().ToListAsync(ct);
        _ = await db.RolePermissions.FromSqlInterpolated($"SELECT * FROM dbo.role_permissions rp WITH (HOLDLOCK) WHERE EXISTS (SELECT 1 FROM dbo.user_roles ur WHERE ur.user_id={targetId} AND ur.role_id=rp.role_id)")
            .AsNoTracking().ToListAsync(ct);
        var branch = (await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (HOLDLOCK) WHERE branch_id={branchId}")
            .AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        async Task<AddBranchStatus?> Gate(long? insertedId)
        {
            var valid = await sessions.ValidateSessionAsync(reference, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return AddBranchStatus.SessionInvalid;
            if (!await access.CanAddAsync(targetId, branchId, insertedId, ct)) return AddBranchStatus.Forbidden;
            return null;
        }
        if (await Gate(null) is { } denied) return new(denied);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId && u.DeletedAt == null, ct);
        if (target is null) return new(AddBranchStatus.NotFound);
        if (target.AccountStatus != "ACTIVE") return new(AddBranchStatus.Inactive);
        if (branch is null || !branch.IsActive) return new(AddBranchStatus.InvalidBranch);
        if (!target.RowVersion.AsSpan().SequenceEqual(expected)) return new(AddBranchStatus.Stale);
        if (await db.UserBranchAccesses.AnyAsync(a => a.UserId == targetId && a.BranchId == branchId, ct)) return new(AddBranchStatus.Duplicate);
        // Fail closed for all existing destination assignments, including currently inactive/expired ones.
        // This slice does not reconcile/reactivate dormant role paths or infer their intended eligibility.
        if (await db.UserRoles.AnyAsync(r => r.UserId == targetId && r.BranchId == branchId, ct)) return new(AddBranchStatus.RoleConflict);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await db.UserRoles.AnyAsync(r => r.UserId == targetId && r.IsActive && r.Role.IsActive
            && r.Role.RoleCode == "OPERATIONS_MANAGER" && (r.ExpiresAt == null || r.ExpiresAt > now), ct))
            return new(AddBranchStatus.OperationsManagerConflict);
        if (await db.Users.Where(u => u.UserId == targetId && u.RowVersion == expected && u.AccountStatus == "ACTIVE" && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UpdatedAt, now), ct) != 1) return new(AddBranchStatus.Stale);
        var membership = new UserBranchAccess { UserId = targetId, BranchId = branchId, AccessLevel = "STANDARD",
            IsPrimary = false, GrantedBy = actor.UserId, GrantedAt = now };
        db.Add(membership);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { return new(AddBranchStatus.Duplicate); }
        finally { db.Entry(membership).State = EntityState.Detached; }
        var audit = new AuditLog { ActorUserId = actor.UserId, BranchId = branchId, EntityType = "user_branch_access",
            EntityId = membership.AccessId.ToString(CultureInfo.InvariantCulture), Action = "CREATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { membership = (object?)null }),
            NewValues = JsonSerializer.Serialize(new { userId = targetId, branchId, accessLevel = "STANDARD", isPrimary = false,
                grantedBy = actor.UserId, grantedAt = now }),
            ChangedFields = JsonSerializer.Serialize(new[] { "membership" }) };
        db.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
        var version = await db.Users.Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        // Do not let this request's new membership bootstrap its own source/target authorization.
        if (await Gate(membership.AccessId) is { } finalDenied) return new(finalDenied);
        await transaction.CommitAsync(ct);
        return new(AddBranchStatus.Success, new(membership.AccessId, targetId, branchId, "STANDARD", false), version);
    }
}
