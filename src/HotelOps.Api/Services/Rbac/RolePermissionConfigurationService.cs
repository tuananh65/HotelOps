using System.Data;
using System.Text.Json;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Rbac;

public sealed partial class CustomRoleService
{
    public async Task<RolePermissionConfigurationResult> ConfigurePermissionsAsync(long id,
        RolePermissionConfigurationRequest? input, byte[]? expectedVersion, CancellationToken ct)
    {
        if (id <= 0) return new(400, "INVALID_ROLE_ID");
        var desired = input?.Validate();
        if (desired != null && expectedVersion?.Length != 8) return new(428, "ROLE_VERSION_REQUIRED");
        var identity = Identity(out var actor);
        if (identity != 200) return new(identity, "SESSION_INVALID");
        if (!db.Database.IsSqlServer() || db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Clean SQL context required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct);
            var beforeAdmin = await guard.ReadSnapshotAsync(ct);
            if (await Gate(actor, ct) is { } denied) return new(denied.Status, denied.Code);
            var capability = await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_role_permissions_version_v1') AND parent_id=OBJECT_ID(N'dbo.role_permissions') AND is_disabled=0) AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.roles') AND name=N'row_version' AND system_type_id=189) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct);
            if (capability != 1) return new(503, "ROLE_PERMISSION_VERSION_REQUIRED");
            var role = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id={id}").AsNoTracking().SingleOrDefaultAsync(ct);
            if (role == null) return new(404, "ROLE_NOT_FOUND");
            var version = await ReadVersionAsync(id, ct);
            var grants = await db.RolePermissions.AsNoTracking().Where(g => g.RoleId == id).OrderBy(g => g.PermissionId).ToArrayAsync(ct);
            var catalog = await db.Permissions.AsNoTracking().OrderBy(p => p.PermissionId)
                .Select(p => new PermissionCatalogDto(p.PermissionId,p.PermissionCode,p.PermissionName,p.ModuleCode,p.ResourceCode,p.ActionCode,p.Description)).Take(2001).ToArrayAsync(ct);
            if (catalog.Length > 2000) return new(503, "ROLE_PERMISSION_CATALOG_LIMIT");
            if (desired != null)
            {
                if (!role.IsActive) return new(409, "ROLE_INACTIVE");
                if (!version.AsSpan().SequenceEqual(expectedVersion)) return new(412, "ROLE_VERSION_CHANGED");
                if (desired.Any(g => !catalog.Any(p => p.PermissionId == g.PermissionId))) return new(400, "PERMISSION_NOT_IN_CATALOG");
                if (await Gate(actor, ct) is { } preWrite) return new(preWrite.Status, preWrite.Code);
                var now = clock.GetUtcNow().UtcDateTime;
                foreach (var existing in grants)
                {
                    var next = desired.SingleOrDefault(g => g.PermissionId == existing.PermissionId);
                    if (next == null) await db.RolePermissions.Where(g => g.RolePermissionId == existing.RolePermissionId).ExecuteDeleteAsync(ct);
                    else if (next.DataScope != existing.DataScope)
                        await db.RolePermissions.Where(g => g.RolePermissionId == existing.RolePermissionId).ExecuteUpdateAsync(s => s
                            .SetProperty(g => g.DataScope, next.DataScope).SetProperty(g => g.GrantedAt, now).SetProperty(g => g.GrantedBy, actor.UserId), ct);
                }
                foreach (var next in desired.Where(g => !grants.Any(old => old.PermissionId == g.PermissionId)))
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.role_permissions(role_id,permission_id,data_scope,granted_at,granted_by) VALUES({id},{next.PermissionId},{next.DataScope},{now},{actor.UserId})", ct);
                // Consume the expected token even for an explicitly saved unchanged selection.
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.roles SET role_name=role_name WHERE role_id={id}", ct);
                var audit = new AuditLog { ActorUserId=actor.UserId, EntityType="role_permissions", EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Action="UPDATE",OccurredAt=now,ChangedFields="[\"grants\"]",
                    OldValues=JsonSerializer.Serialize(grants.Select(g=>new { g.RolePermissionId,g.PermissionId,g.DataScope,g.GrantedAt,g.GrantedBy }),new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    NewValues=JsonSerializer.Serialize(desired,new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
                db.AuditLogs.Add(audit);
                try { await db.SaveChangesAsync(ct); } finally { db.Entry(audit).State=EntityState.Detached; }
                await guard.EnsurePreservedAsync(beforeAdmin,ct);
                // Intentional self-removal of role administration is allowed only after the durable-admin guard.
                if (!await AccountWriterSessionGate.IsValidAsync(db,actor,clock,ct)) return new(401,"SESSION_INVALID");
                var users = await db.UserRoles.AsNoTracking().Where(a=>a.RoleId==id&&a.IsActive&&a.AssignedAt<=now&&(a.ExpiresAt==null||a.ExpiresAt>now))
                    .Select(a=>a.UserId).Distinct().OrderBy(x=>x).ToArrayAsync(ct);
                foreach (var userId in users) await new SessionRevocationService(db).RevokeAllAsync(userId,now,ct);
                version=await ReadVersionAsync(id,ct);
            }
            else if (await Gate(actor,ct) is {} final) return new(final.Status,final.Code);
            var summary = await db.Roles.AsNoTracking().Where(r=>r.RoleId==id).Select(RoleCatalogQueries.Projection(clock.GetUtcNow().UtcDateTime)).SingleAsync(ct);
            var values=desired ?? grants.Select(g=>new ConfiguredRoleGrant(g.PermissionId,g.DataScope)).ToArray();
            await tx.CommitAsync(ct);
            return new(200,Value:new(summary,values,catalog),Version:version);
        }
        catch(GlobalAdminLockException) { return new(503,"GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"); }
        catch(GlobalAdministrationInvariantException) { return new(409,"LAST_DURABLE_ADMIN"); }
    }
}
