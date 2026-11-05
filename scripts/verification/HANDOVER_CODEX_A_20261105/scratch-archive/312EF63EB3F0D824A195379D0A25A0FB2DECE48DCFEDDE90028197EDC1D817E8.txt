using System.Data;
using System.Transactions;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Transaction-wide serialization foundation. Call FIRST, before User/catalog/resource locks.
/// Credential reset, account creation and profile writers participate; external/catalog
/// writers still require separate integration.
/// The caller owns commit/rollback and must roll back on any failure. No early lock release is offered.
/// </summary>
public sealed class GlobalAdminSerialization(HotelOpsDbContext db, TimeProvider clock)
{
    public const string Resource = "HotelOps:GlobalAdminEligibility:v1";

    public async Task<GlobalAdminTransaction> AcquireAsync(int timeoutMilliseconds = 5000, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMilliseconds, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutMilliseconds, 30000);
        ct.ThrowIfCancellationRequested();
        if (!db.Database.IsSqlServer() || Transaction.Current is not null)
            throw new InvalidOperationException("A caller-owned local SQL Server transaction is required.");
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A caller-owned transaction is required.");
        if (transaction.GetDbTransaction().IsolationLevel is not (System.Data.IsolationLevel.ReadCommitted
            or System.Data.IsolationLevel.RepeatableRead or System.Data.IsolationLevel.Serializable))
            throw new InvalidOperationException("Locking isolation is required.");
        if (db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Acquire before tracked work and before any feature locks.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = 35;
        command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=@timeout,@DbPrincipal='public'; SELECT @result;";
        command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = Resource });
        command.Parameters.Add(new SqlParameter("@timeout", SqlDbType.Int) { Value = timeoutMilliseconds });
        object? scalar;
        try { scalar = await command.ExecuteScalarAsync(ct); }
        catch (SqlException error) when (ct.IsCancellationRequested)
        { throw new OperationCanceledException("Global administration lock acquisition was cancelled; roll back the caller transaction.", error, ct); }
        ct.ThrowIfCancellationRequested();
        if (scalar is not int result)
            throw new InvalidOperationException("SQL did not confirm serialization acquisition.");
        if (result < 0) throw new GlobalAdminLockException(result);
        return new GlobalAdminTransaction(db, transaction, clock);
    }
}

public sealed class GlobalAdminLockException(int result)
    : InvalidOperationException("Global administration serialization was not acquired; roll back the caller transaction.")
{
    public int Result { get; } = result;
}

/// <summary>No credential/session material is exposed. SQL eligibility is structural, not password knowledge.</summary>
public sealed class GlobalAdminSnapshot
{
    internal Guid Scope { get; }
    public DateTime EvaluatedAtUtc { get; }
    public IReadOnlyList<long> CapabilityCandidateUserIds { get; }
    public IReadOnlyList<long> DurableEligibleUserIds { get; }
    public IReadOnlyList<long> ImmediatelyLoginCapableUserIds { get; }
    public bool HasNonExpiringDurableAdministrator { get; }
    public DateTime? NextKnownCapabilityLossUtc { get; }
    internal GlobalAdminSnapshot(Guid scope, DateTime now, long[] candidates, long[] durable, long[] immediate,
        bool nonExpiring, DateTime? nextLoss)
    {
        Scope = scope; EvaluatedAtUtc = now;
        CapabilityCandidateUserIds = Array.AsReadOnly(candidates);
        DurableEligibleUserIds = Array.AsReadOnly(durable);
        ImmediatelyLoginCapableUserIds = Array.AsReadOnly(immediate);
        HasNonExpiringDurableAdministrator = nonExpiring; NextKnownCapabilityLossUtc = nextLoss;
    }
}

public sealed class GlobalAdministrationInvariantException()
    : InvalidOperationException("Durable global administration cannot be preserved; roll back the caller transaction.");

public sealed class GlobalAdminTransaction
{
    private readonly HotelOpsDbContext db;
    private readonly IDbContextTransaction transaction;
    private readonly TimeProvider clock;
    private readonly Guid scope = Guid.NewGuid();
    internal GlobalAdminTransaction(HotelOpsDbContext db, IDbContextTransaction transaction, TimeProvider clock)
        => (this.db, this.transaction, this.clock) = (db, transaction, clock);

    /// <summary>
    /// Read under retained SQL locks: Users -> UserRoles -> Roles -> RolePermissions -> Permissions.
    /// Conservatively locks these tables/ranges, including the identifier namespace; not for routine reads.
    /// Finite lockout affects immediate login, not durable eligibility. Temporary credentials are excluded.
    /// Locks do not freeze time; report expiry separately. No existing mutation is implicitly protected.
    /// </summary>
    public async Task<GlobalAdminSnapshot> ReadSnapshotAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (db.Database.CurrentTransaction != transaction || Transaction.Current is not null)
            throw new InvalidOperationException("The original caller transaction must still be active.");
        if (db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Read authoritative SQL state using a clean context.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT APPLOCK_MODE('public',@resource,'Transaction')";
        command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = GlobalAdminSerialization.Resource });
        if (!string.Equals(await command.ExecuteScalarAsync(ct) as string, "Exclusive", StringComparison.Ordinal))
            throw new InvalidOperationException("The transaction serialization lock is required.");
        // Conservative stable universe, including missing/new identifier and grant rows. This foundation
        // intentionally trades concurrency for proof; integration/load/legacy lock-order review is separate.
        // Take the write lock up front. A retained table S lock followed by a User UPDLOCK
        // can deadlock with authentication holding U and waiting to promote it to X.
        var users = await db.Users.FromSqlRaw("SELECT * FROM dbo.users WITH (TABLOCKX,HOLDLOCK)").AsNoTracking().ToArrayAsync(ct);
        // Shared row/range locks remain compatible with Create's existing Role UPDLOCK.
        // Forcing catalog table S locks would conflict with its intent locks while it awaits User INSERT.
        _ = await db.UserRoles.FromSqlRaw("SELECT * FROM dbo.user_roles WITH (ROWLOCK,HOLDLOCK)").AsNoTracking().ToListAsync(ct);
        _ = await db.Roles.FromSqlRaw("SELECT * FROM dbo.roles WITH (ROWLOCK,HOLDLOCK)").AsNoTracking().ToListAsync(ct);
        _ = await db.RolePermissions.FromSqlRaw("SELECT * FROM dbo.role_permissions WITH (ROWLOCK,HOLDLOCK)").AsNoTracking().ToListAsync(ct);
        _ = await db.Permissions.FromSqlRaw("SELECT * FROM dbo.permissions WITH (ROWLOCK,HOLDLOCK)").AsNoTracking().ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        // Same SQL account/assignment/time predicates as the existing permission data source.
        // Scope/code comparisons remain ordinal in .NET, as in PermissionService.
        var paths = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.User.AccountStatus == "ACTIVE" && assignment.User.DeletedAt == null
                && assignment.IsActive && assignment.Role.IsActive && assignment.BranchId == null
                && assignment.AssignedAt <= now && (assignment.ExpiresAt == null || assignment.ExpiresAt > now)
                && (grant.Permission.PermissionCode == "USER_MANAGE" || grant.Permission.PermissionCode == "ROLE_MANAGE")
            select new { assignment.UserId, grant.DataScope, grant.Permission.PermissionCode, assignment.ExpiresAt }).ToListAsync(ct);
        var ids = paths.Where(p => p.DataScope == "ALL").GroupBy(p => p.UserId)
            .Where(g => g.Any(p => p.PermissionCode == "USER_MANAGE") && g.Any(p => p.PermissionCode == "ROLE_MANAGE"))
            .Select(g => g.Key).Order().ToArray();
        var durable = new List<long>(); var immediate = new List<long>(); var horizons = new List<DateTime?>();
        foreach (var id in ids)
        {
            var user = users.Single(u => u.UserId == id);
            if (user.TemporaryPasswordExpiresAt.HasValue || !PasswordCredentialService.IsSupportedV3Hash(user.PasswordHash)) continue;
            var usableIdentifier = false;
            foreach (var identifier in new[] { user.Username, user.Email }.Select(v => v.Trim()).Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 150) continue;
                // Match login Candidates exactly under SQL collation; deleted/inactive colliders still count.
                var matches = await db.Users.AsNoTracking().Where(u => u.Username == identifier || u.Email == identifier)
                    .Select(u => u.UserId).Take(2).ToArrayAsync(ct);
                if (matches.Length == 1 && matches[0] == id) { usableIdentifier = true; break; }
            }
            if (!usableIdentifier) continue;
            durable.Add(id);
            if (user.LockedUntil is null || user.LockedUntil <= now) immediate.Add(id);
            DateTime? LastPath(string code)
            {
                var expiries = paths.Where(p => p.UserId == id && p.DataScope == "ALL" && p.PermissionCode == code).Select(p => p.ExpiresAt).ToArray();
                return expiries.Any(v => v is null) ? null : expiries.Max();
            }
            var manage = LastPath("USER_MANAGE"); var roles = LastPath("ROLE_MANAGE");
            horizons.Add(manage is null ? roles : roles is null ? manage : (manage < roles ? manage : roles));
        }
        if (paths.Any(p => p.ExpiresAt > now && p.ExpiresAt <= clock.GetUtcNow().UtcDateTime))
            return await ReadSnapshotAsync(ct); // A slow read must not certify a path that expired during it.
        return new(scope, now, ids, durable.ToArray(), immediate.ToArray(), horizons.Any(v => v is null),
            horizons.Where(v => v.HasValue).Select(v => v!.Value).Cast<DateTime?>().Min());
    }

    /// <summary>Call after the proposed write and audit, before caller commit. Failure requires rollback.</summary>
    public async Task<GlobalAdminSnapshot> EnsurePreservedAsync(GlobalAdminSnapshot before, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(before);
        if (before.Scope != scope) throw new InvalidOperationException("Snapshot belongs to another guard acquisition.");
        var after = await ReadSnapshotAsync(ct);
        if (before.DurableEligibleUserIds.Count == 0 || after.DurableEligibleUserIds.Count == 0)
            throw new GlobalAdministrationInvariantException();
        return after;
    }
}
