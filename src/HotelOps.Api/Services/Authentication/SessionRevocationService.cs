using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Authentication;

public interface ISessionRevocationService
{
    /// <summary>Uses the caller transaction/context; takes user lock before sessions.</summary>
    Task<int> RevokeAllAsync(long userId, DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed class SessionRevocationService(HotelOpsDbContext db) : ISessionRevocationService
{
    public async Task<int> RevokeAllAsync(long userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("UTC time is required.", nameof(utcNow));
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Session revocation requires the caller transaction.");
        if (!db.Database.IsSqlServer() || transaction.GetDbTransaction().IsolationLevel
            is not (IsolationLevel.ReadCommitted or IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("SQL Server locking isolation is required.");
        if (db.ChangeTracker.HasChanges() || db.ChangeTracker.Entries<Entities.UserSession>().Any()
            || db.ChangeTracker.Entries<Entities.User>().Any())
            throw new InvalidOperationException("Session revocation requires a clean, untracked context.");

        var users = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM dbo.users WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId}")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (users.Count != 1)
            throw new InvalidOperationException("Credential target was not found.");

        return await db.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAt, utcNow), cancellationToken);
    }
}
