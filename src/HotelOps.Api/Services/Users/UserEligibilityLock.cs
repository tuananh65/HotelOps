using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

// Bound to the caller's scoped DbContext; never owns or commits a transaction.
public sealed class UserEligibilityLock(HotelOpsDbContext db)
{
    public sealed record LockedUser(long UserId, DateTime? DeletedAt, string AccountStatus, string EmploymentStatus);

    public async Task<IReadOnlyDictionary<long, LockedUser?>> LockAsync(IEnumerable<long> userIds, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Responsibility User locks require an existing caller transaction.");
        var ids = userIds.Distinct().Order().ToArray();
        if (ids.Any(id => id <= 0)) throw new ValidationException("Responsibility UserId must be positive.");
        var result = new Dictionary<long, LockedUser?>();
        foreach (var id in ids)
        {
            // No tracking: eligibility must reflect SQL after any lock wait,
            // rather than a stale User already attached to this DbContext.
            result[id] = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM dbo.users WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {id}")
                .AsNoTracking().Select(u => new LockedUser(u.UserId, u.DeletedAt, u.AccountStatus, u.EmploymentStatus))
                .SingleOrDefaultAsync(ct);
        }
        return result;
    }

    public static void RequireEligible(long userId, IReadOnlyDictionary<long, LockedUser?> locked)
    {
        if (!locked.TryGetValue(userId, out var user) || user is null || user.DeletedAt is not null
            || user.AccountStatus != "ACTIVE" || user.EmploymentStatus != "ACTIVE")
            throw new ValidationException("Future responsibility requires an existing, nondeleted, account-active and employment-active User.");
    }

    public async Task RequireEligibleAsync(long userId, CancellationToken ct) =>
        RequireEligible(userId, await LockAsync([userId], ct));
}
