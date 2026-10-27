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

public enum AccountActivationStatus { Success, Forbidden, SessionInvalid, NotFound, Stale, Conflict, DeactivationBlocked }
public sealed record AccountActivationResult(AccountActivationStatus Status, byte[]? Version = null);

/// <summary>
/// Activation-only transaction, shared by the admin activation API and verification.
/// Deactivation cannot execute through this primitive, including direct service calls.
/// </summary>
public sealed class UserAccountActivationService(HotelOpsDbContext db, UserAccountStatusAccess access,
    ICurrentUserAccessor current, ISqlSessionService sessions, TimeProvider clock)
{
    public async Task<AccountActivationResult> ActivateAsync(SessionReference reference, long sessionId,
        long targetId, byte[] expectedVersion, UserAccountStatusChange change, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        ArgumentNullException.ThrowIfNull(change);
        if (expectedVersion.Length != 8) throw new ArgumentException("An eight-byte rowversion is required.", nameof(expectedVersion));
        if (!change.IsValid) throw new ArgumentException("Invalid status change.", nameof(change));
        if (change.Status != "ACTIVE") return new(AccountActivationStatus.DeactivationBlocked);
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(AccountActivationStatus.SessionInvalid);
        if (actor.IsRestricted || actor.UserId == targetId) return new(AccountActivationStatus.Forbidden);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null || Transaction.Current is not null
            || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Activation requires a clean context and its own SQL transaction.");
        var expected = expectedVersion.ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        foreach (var id in new[] { actor.UserId, targetId }.Order())
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH (UPDLOCK,HOLDLOCK) WHERE user_id={id}")
                .AsNoTracking().ToListAsync(ct);
        async Task<AccountActivationStatus?> Gate()
        {
            await access.LockAuthorityAsync(actor.UserId, targetId, ct);
            var validation = await sessions.ValidateSessionAsync(reference, ct);
            if (!validation.IsValid || validation.Session?.SessionId != sessionId || validation.Session.IsRestricted)
                return AccountActivationStatus.SessionInvalid;
            if (!await access.CanManageAsync(targetId, ct)) return AccountActivationStatus.Forbidden;
            return null;
        }
        if (await Gate() is { } denied) return new(denied);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == targetId && u.DeletedAt == null, ct);
        if (target is null) return new(AccountActivationStatus.NotFound);
        if (!target.RowVersion.AsSpan().SequenceEqual(expected)) return new(AccountActivationStatus.Stale);
        if (!change.IsTransitionFrom(target.AccountStatus)) return new(AccountActivationStatus.Conflict);
        var now = clock.GetUtcNow().UtcDateTime;
        var count = await db.Users.Where(u => u.UserId == targetId && u.DeletedAt == null
                && u.AccountStatus == "INACTIVE" && u.RowVersion == expected)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.AccountStatus, "ACTIVE")
                .SetProperty(u => u.UpdatedAt, now), ct);
        if (count != 1) return new(AccountActivationStatus.Stale);
        var audit = new AuditLog { ActorUserId = actor.UserId, EntityType = "users",
            EntityId = targetId.ToString(CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(new { accountStatus = "INACTIVE" }),
            NewValues = JsonSerializer.Serialize(new { accountStatus = "ACTIVE", reason = change.Reason }),
            ChangedFields = JsonSerializer.Serialize(new[] { "accountStatus" }) };
        db.AuditLogs.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == targetId).Select(u => u.RowVersion).SingleAsync(ct);
        if (await Gate() is { } finalDenied) return new(finalDenied);
        await transaction.CommitAsync(ct);
        return new(AccountActivationStatus.Success, version);
    }
}
