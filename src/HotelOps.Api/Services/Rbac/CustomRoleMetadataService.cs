using System.Data;
using System.Text.Json;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Rbac;

public sealed partial class CustomRoleService
{
    public Task<CustomRoleResult> ReadMetadataAsync(long id, CancellationToken ct)
        => MetadataAsync(id, null, null, ct);

    public Task<CustomRoleResult> UpdateAsync(long id, CustomRoleUpdateRequest input,
        byte[] expectedVersion, CancellationToken ct)
        => MetadataAsync(id, input.Normalize(), expectedVersion, ct);

    public Task<CustomRoleResult> DeactivateAsync(long id, byte[] expectedVersion, CancellationToken ct)
        => MetadataAsync(id, null, expectedVersion, ct, deactivate: true);

    private async Task<CustomRoleResult> MetadataAsync(long id, CustomRoleUpdateRequest? request,
        byte[]? expectedVersion, CancellationToken ct, bool deactivate = false)
    {
        if (id <= 0) return new(400, "INVALID_ROLE_ID");
        if ((request != null || deactivate) && expectedVersion?.Length != 8) return new(428, "ROLE_VERSION_REQUIRED");
        var identity = Identity(out var actor);
        if (identity != 200) return new(identity, "SESSION_INVALID");
        if (!db.Database.IsSqlServer() || db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Clean SQL context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct);
            var beforeAdmin = await guard.ReadSnapshotAsync(ct); // User/catalog locks precede session locks.
            if (await Gate(actor, ct) is { } denied) return denied;
            var capability = await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.roles') AND name=N'row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct);
            if (capability != 1) return new(503, "ROLE_VERSION_STORAGE_REQUIRED");
            var role = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id={id}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (role == null) return new(404, "ROLE_NOT_FOUND");
            var version = await ReadVersionAsync(id, ct);
            if (request != null || deactivate)
            {
                if (role.IsSystemRole) return new(409, "SYSTEM_ROLE_PROTECTED");
                if (!version.AsSpan().SequenceEqual(expectedVersion)) return new(412, "ROLE_VERSION_CHANGED");
                if (deactivate && !role.IsActive) return new(409, "ROLE_ALREADY_INACTIVE");
                var before = new { role.RoleCode, role.RoleName, role.Description, role.IsActive, role.IsSystemRole };
                if (await Gate(actor, ct) is { } beforeWrite) return beforeWrite;
                // Native version changes for every writer, including a later change back to old metadata.
                var changed = deactivate
                    ? await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.roles SET is_active=0 WHERE role_id={id} AND row_version={expectedVersion} AND is_system_role=0 AND is_active=1", ct)
                    : await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.roles SET role_name={request!.RoleName},description={request.Description} WHERE role_id={id} AND row_version={expectedVersion} AND is_system_role=0", ct);
                if (changed != 1) return new(412, "ROLE_VERSION_CHANGED");
                var audit = new AuditLog
                {
                    ActorUserId = actor.UserId, EntityType = "roles",
                    EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE",
                    OccurredAt = clock.GetUtcNow().UtcDateTime,
                    OldValues = JsonSerializer.Serialize(before, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    NewValues = JsonSerializer.Serialize(new { role.RoleCode,
                        RoleName = deactivate ? role.RoleName : request!.RoleName,
                        Description = deactivate ? role.Description : request!.Description,
                        IsActive = deactivate ? false : role.IsActive, role.IsSystemRole }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    ChangedFields = deactivate ? "[\"isActive\"]" : "[\"roleName\",\"description\"]"
                };
                db.AuditLogs.Add(audit);
                try { await db.SaveChangesAsync(ct); }
                finally { db.Entry(audit).State = EntityState.Detached; }
                version = await ReadVersionAsync(id, ct);
                await guard.EnsurePreservedAsync(beforeAdmin, ct);
            }
            var detail = await db.Roles.AsNoTracking().Where(r => r.RoleId == id)
                .Select(RoleCatalogQueries.Projection(clock.GetUtcNow().UtcDateTime)).SingleAsync(ct);
            if (deactivate)
            {
                // Authorization was checked immediately before the write under retained catalog locks.
                // Deactivating one's own role can intentionally remove that authority; it is not a
                // reason to resurrect the role. Recheck the session, then revoke affected sessions.
                if (!await AccountWriterSessionGate.IsValidAsync(db, actor, clock, ct)) return new(401, "SESSION_INVALID");
                var now = clock.GetUtcNow().UtcDateTime;
                var affectedUsers = await db.UserRoles.AsNoTracking()
                    .Where(a => a.RoleId == id && a.IsActive && a.AssignedAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now))
                    .Select(a => a.UserId).Distinct().OrderBy(userId => userId).ToArrayAsync(ct);
                var revocation = new SessionRevocationService(db);
                foreach (var userId in affectedUsers) await revocation.RevokeAllAsync(userId, now, ct);
                // Preserve assignments, grants, and all history; other active roles remain assigned.
            }
            else if (await Gate(actor, ct) is { } final) return final;
            ct.ThrowIfCancellationRequested();
            await tx.CommitAsync(ct);
            return new(200, Value: new(detail.RoleId, detail.RoleCode, detail.RoleName, detail.Description,
                detail.IsSystemRole, detail.IsActive, detail.AssignedUserCount, detail.PermissionGrantCount), Version: version);
        }
        catch (GlobalAdminLockException) { return new(503, "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"); }
        catch (GlobalAdministrationInvariantException) { return new(409, "LAST_DURABLE_ADMIN"); }
    }

    private Task<byte[]> ReadVersionAsync(long id, CancellationToken ct)
        => db.Database.SqlQuery<byte[]>($"SELECT row_version AS [Value] FROM dbo.roles WHERE role_id={id}").SingleAsync(ct);
}
