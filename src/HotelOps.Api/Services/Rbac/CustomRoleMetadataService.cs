using System.Data;
using System.Text.Json;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Rbac;

public sealed partial class CustomRoleService
{
    public Task<CustomRoleResult> ReadMetadataAsync(long id, CancellationToken ct)
        => MetadataAsync(id, null, null, ct);

    public Task<CustomRoleResult> UpdateAsync(long id, CustomRoleUpdateRequest input,
        byte[] expectedVersion, CancellationToken ct)
        => MetadataAsync(id, input.Normalize(), expectedVersion, ct);

    private async Task<CustomRoleResult> MetadataAsync(long id, CustomRoleUpdateRequest? request,
        byte[]? expectedVersion, CancellationToken ct)
    {
        if (id <= 0) return new(400, "INVALID_ROLE_ID");
        if (request != null && expectedVersion?.Length != 8) return new(428, "ROLE_VERSION_REQUIRED");
        var identity = Identity(out var actor);
        if (identity != 200) return new(identity, "SESSION_INVALID");
        if (!db.Database.IsSqlServer() || db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Clean SQL context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct);
            if (await Gate(actor, ct) is { } denied) return denied;
            var capability = await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.roles') AND name=N'row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct);
            if (capability != 1) return new(503, "ROLE_VERSION_STORAGE_REQUIRED");
            var beforeAdmin = await guard.ReadSnapshotAsync(ct);
            var role = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id={id}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (role == null) return new(404, "ROLE_NOT_FOUND");
            var version = await ReadVersionAsync(id, ct);
            if (request != null)
            {
                if (role.IsSystemRole) return new(409, "SYSTEM_ROLE_PROTECTED");
                if (!version.AsSpan().SequenceEqual(expectedVersion)) return new(412, "ROLE_VERSION_CHANGED");
                var before = new { role.RoleCode, role.RoleName, role.Description, role.IsActive, role.IsSystemRole };
                // Native version changes for every writer, including a later change back to old metadata.
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.roles SET role_name={request.RoleName},description={request.Description} WHERE role_id={id} AND row_version={expectedVersion} AND is_system_role=0", ct);
                if (changed != 1) return new(412, "ROLE_VERSION_CHANGED");
                var audit = new AuditLog
                {
                    ActorUserId = actor.UserId, EntityType = "roles",
                    EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE",
                    OccurredAt = clock.GetUtcNow().UtcDateTime,
                    OldValues = JsonSerializer.Serialize(before, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    NewValues = JsonSerializer.Serialize(new { role.RoleCode, request.RoleName, request.Description, role.IsActive, role.IsSystemRole }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                };
                db.AuditLogs.Add(audit);
                try { await db.SaveChangesAsync(ct); }
                finally { db.Entry(audit).State = EntityState.Detached; }
                version = await ReadVersionAsync(id, ct);
                await guard.EnsurePreservedAsync(beforeAdmin, ct);
            }
            var detail = await db.Roles.AsNoTracking().Where(r => r.RoleId == id)
                .Select(RoleCatalogQueries.Projection(clock.GetUtcNow().UtcDateTime)).SingleAsync(ct);
            if (await Gate(actor, ct) is { } final) return final;
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
