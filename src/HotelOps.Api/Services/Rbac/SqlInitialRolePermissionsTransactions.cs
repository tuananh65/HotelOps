using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace HotelOps.Api.Services.Rbac;

public sealed class InitialRoleAuthorityException(int status) : Exception { public int Status { get; } = status; }
public sealed class SqlInitialRolePermissionsTransactions(HotelOpsDbContext db, ICurrentUserAccessor current, IPermissionService permissions, TimeProvider clock, IHttpContextAccessor http) : IInitialRolePermissionsTransactions
{
    private TimeProvider Clock => clock;
    private IPermissionService Permissions => permissions;
    private int Identity(out CurrentUserInfo actor)
    {
        if (!current.TryGetCurrentUser(out actor) || !HotelOpsCookieTicket.TryRead(http.HttpContext?.User, out var ticket, out var sid) || ticket!.UserId != actor.UserId || sid != actor.SessionId) return 401;
        return actor.IsRestricted || ticket.IsRestricted ? 403 : 200;
    }
    public async Task<IInitialRolePermissionsTransaction> BeginAsync(CancellationToken ct)
    {
        var status = Identity(out var actor); if (status != 200) throw new InitialRoleAuthorityException(status);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Clean local SQL context required.");
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var guard = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct);
            var before = await guard.ReadSnapshotAsync(ct);
            return new Scope(this, actor, db, tx, guard, before);
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private sealed class Scope(SqlInitialRolePermissionsTransactions owner, CurrentUserInfo actor, HotelOpsDbContext db, IDbContextTransaction tx, GlobalAdminTransaction guard, GlobalAdminSnapshot before) : IInitialRolePermissionsTransaction
    {
        public async Task<int> AuthorizeAsync(CancellationToken ct)
        {
            var status = owner.Identity(out var fresh);
            if (status != 200 || fresh.UserId != actor.UserId || fresh.SessionId != actor.SessionId) return status == 403 ? 403 : 401;
            if (!await AccountWriterSessionGate.IsValidAsync(db, actor, owner.Clock, ct)) return 401;
            var authority = await RoleCatalogAuthority.EvaluateAsync(owner.Permissions, ct);
            return authority.IsGranted ? 200 : authority.Decision == PermissionDecision.Unavailable ? 503 : authority.Decision == PermissionDecision.Unauthenticated ? 401 : 403;
        }
        public async Task<InitialRoleState?> ReadAsync(long roleId, CancellationToken ct)
        {
            var role = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id={roleId}").AsNoTracking().SingleOrDefaultAsync(ct);
            if (role is null) return null;
            var assignments = await db.UserRoles.FromSqlInterpolated($"SELECT * FROM dbo.user_roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id={roleId}").AsNoTracking().AnyAsync(ct);
            var grants = await db.RolePermissions.FromSqlInterpolated($"SELECT * FROM dbo.role_permissions WITH(UPDLOCK,HOLDLOCK) WHERE role_id={roleId}").AsNoTracking().OrderBy(g => g.PermissionId).Select(g => new InitialPermissionGrant(g.PermissionId, g.DataScope)).ToArrayAsync(ct);
            return new(role, assignments, grants);
        }
        public Task<Permission[]> CatalogAsync(long[] ids, CancellationToken ct) => db.Permissions.FromSqlRaw("SELECT * FROM dbo.permissions WITH(HOLDLOCK)").AsNoTracking().Where(p => ids.Contains(p.PermissionId)).ToArrayAsync(ct);
        public async Task InsertAndAuditAsync(long roleId, InitialPermissionGrant[] desired, CancellationToken ct)
        {
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
            var rows = desired.Select(g => new RolePermission { RoleId = roleId, PermissionId = g.PermissionId, DataScope = g.DataScope, GrantedAt = now, GrantedBy = actor.UserId }).ToArray();
            db.RolePermissions.AddRange(rows);
            try { await db.SaveChangesAsync(ct); } finally { foreach (var row in rows) db.Entry(row).State = EntityState.Detached; }
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var audit = new AuditLog
            {
                ActorUserId = actor.UserId,
                EntityType = "role_permissions",
                EntityId = roleId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "CREATE",
                OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new InitialRolePermissionsDto(1, roleId, []), options),
                NewValues = JsonSerializer.Serialize(new InitialRolePermissionsDto(1, roleId, desired), options)
            };
            db.AuditLogs.Add(audit);
            try { await db.SaveChangesAsync(ct); } finally { db.Entry(audit).State = EntityState.Detached; }
        }
        public Task PreserveAdministrationAsync(CancellationToken ct) => guard.EnsurePreservedAsync(before, ct);
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);
        public ValueTask DisposeAsync() => tx.DisposeAsync(); // Uncommitted result/error always rolls back.
    }
}