using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public enum UserProfileStatus { Success, Forbidden, NotFound, Stale, EmailInUse }
public sealed record UserProfileResult(UserProfileStatus Status, UserProfileDto? Profile = null, byte[]? Version = null);

public sealed class UserProfileService(HotelOpsDbContext db, UserProfileAccess access,
    ICurrentUserAccessor current, TimeProvider clock)
{
    public async Task<UserProfileResult> ReadAsync(long id, CancellationToken ct)
    {
        if (!await access.CanManageAsync(id, ct)) return new(UserProfileStatus.Forbidden);
        var row = await ReadRow(id, ct);
        return row is null ? new(UserProfileStatus.NotFound) : Success(row);
    }

    public async Task<UserProfileResult> UpdateAsync(long id, byte[] expected, UserProfilePatch patch, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(patch);
        if (expected.Length != 8) throw new ArgumentException("An eight-byte rowversion is required.", nameof(expected));
        patch = patch.Normalize();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await access.CanManageAsync(id, ct) || !current.TryGetCurrentUser(out var actor))
            return new(UserProfileStatus.Forbidden);
        var before = await ReadRow(id, ct);
        if (before is null) return new(UserProfileStatus.NotFound);
        if (!before.Version.AsSpan().SequenceEqual(expected)) return new(UserProfileStatus.Stale);
        var after = new UserProfileDto(id, patch.HasFullName ? patch.FullName! : before.Profile.FullName,
            patch.HasEmail ? patch.Email! : before.Profile.Email, patch.HasPhone ? patch.Phone : before.Profile.Phone);
        var oldValues = new SortedDictionary<string, object?>();
        var newValues = new SortedDictionary<string, object?>();
        void Changed(string key, string? oldValue, string? newValue)
        {
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return;
            oldValues[key] = oldValue; newValues[key] = newValue;
        }
        Changed("fullName", before.Profile.FullName, after.FullName);
        Changed("email", before.Profile.Email, after.Email);
        Changed("phone", before.Profile.Phone, after.Phone);
        if (oldValues.Count == 0) return Success(before);
        var now = clock.GetUtcNow().UtcDateTime;
        int updated;
        try
        {
            updated = await db.Users.Where(u => u.UserId == id && u.DeletedAt == null && u.RowVersion == expected)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, after.FullName)
                    .SetProperty(u => u.Email, after.Email).SetProperty(u => u.Phone, after.Phone)
                    .SetProperty(u => u.UpdatedAt, now), ct);
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            // Fresh schema uses SQL-generated constraint names, which differ from scaffold names.
            // This catch surrounds only the profile UPDATE, never audit insertion.
            await transaction.RollbackAsync(ct);
            if (await db.Users.AsNoTracking().AnyAsync(u => u.UserId != id && u.Email == after.Email, ct))
                return new(UserProfileStatus.EmailInUse);
            throw;
        }
        if (updated != 1) return new(UserProfileStatus.Stale);
        var audit = new AuditLog { ActorUserId = actor.UserId, EntityType = "users",
            EntityId = id.ToString(CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
            OldValues = JsonSerializer.Serialize(oldValues), NewValues = JsonSerializer.Serialize(newValues),
            ChangedFields = JsonSerializer.Serialize(oldValues.Keys) };
        db.AuditLogs.Add(audit);
        try
        {
            await db.SaveChangesAsync(ct);
            var result = Success((await ReadRow(id, ct))!);
            await transaction.CommitAsync(ct);
            return result;
        }
        finally { db.Entry(audit).State = EntityState.Detached; }
    }

    private Task<Row?> ReadRow(long id, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.UserId == id && u.DeletedAt == null)
        .Select(u => new Row(new UserProfileDto(u.UserId, u.FullName, u.Email, u.Phone), u.RowVersion))
        .SingleOrDefaultAsync(ct);
    private static UserProfileResult Success(Row row) => new(UserProfileStatus.Success, row.Profile, row.Version);
    private sealed record Row(UserProfileDto Profile, byte[] Version);
}
