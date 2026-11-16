using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal sealed partial class MaterialApprovalNotificationDelivery
{
    internal const string ReplenishmentCreatedKind = "REPLENISHMENT_CREATED";

    // Header and CREATE audit are committed together by the creation workflow.
    // Its identity remains stable when the replenishment is received or closed.
    private static IQueryable<NoticeEvent> ReplenishmentCreatedEvents(HotelOpsDbContext db) =>
        db.MaterialReplenishments.AsNoTracking().Where(r => r.Warehouse.IsActive && r.Warehouse.Branch.IsActive
            && r.MaterialReplenishmentLines.Any()
            && !r.MaterialReplenishmentLines.Any(l => l.WarehouseId != r.WarehouseId
                || l.SourceMaterialRequestLine != null && l.SourceMaterialRequestLine.Request.BranchId != r.Warehouse.BranchId)
            && db.AuditLogs.Any(a => a.EntityType == "material_replenishments" && a.EntityId == r.ReplenishmentId.ToString()
                && a.Action == "CREATE" && a.ActorUserId == r.RequestedBy && a.BranchId == r.Warehouse.BranchId))
        .Select(r => new NoticeEvent { Id = r.ReplenishmentId, RequestId = r.ReplenishmentId,
            BranchId = r.Warehouse.BranchId, ActorId = r.RequestedBy, At = r.CreatedAt });

    private static IQueryable<long> ReplenishmentManagers(HotelOpsDbContext db, long branch, DateTime now) =>
        db.UserRoles.AsNoTracking().Where(r => r.Role.RoleCode == "OPERATIONS_MANAGER" && r.IsActive && r.Role.IsActive
            && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE"
            && db.UserBranchAccesses.Any(b => b.UserId == r.UserId && b.BranchId == branch && b.Branch.IsActive
                && b.GrantedAt <= now && b.RevokedAt == null))
        .Select(r => r.UserId).Distinct();

    internal async Task<long[]> PendingReplenishmentCreatedAsync(long after, CancellationToken ct)
    {
        if (!await ReplenishmentReceiptsInstalledAsync(db, ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        return await ReplenishmentCreatedEvents(db).Where(e => e.Id > after && e.At >= cutoff && e.At <= now)
            .OrderBy(e => e.Id).Select(e => e.Id).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<long?> ReplenishmentCreatedTargetAsync(HotelOpsDbContext db, long id, long user,
        long? actor, DateTime at, CancellationToken ct)
    {
        if (!await ReplenishmentReceiptsInstalledAsync(db, ct)) return null;
        await new UserEligibilityLock(db).LockAsync([user], ct);
        var e = await ReplenishmentCreatedEvents(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (e == null || e.ActorId != actor || e.At != at) return null;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await ReplenishmentManagers(db, e.BranchId, now).ContainsAsync(user, ct)) return null;
        var grants = await new EfPermissionDataSource(db).ReadPathsAsync(user, "REPLENISHMENT_VIEW", now, ct);
        if (!grants.Any(g => g.PermissionCode == "REPLENISHMENT_VIEW" && PermissionGrantRules.IsEligible(g, now)
            && (PermissionGrantRules.EffectiveScope(g) == "GLOBAL"
                || g.BranchId == e.BranchId && PermissionGrantRules.EffectiveScope(g) == "BRANCH"))) return null;
        var linked = await db.MaterialReplenishmentLines.AsNoTracking().Where(l => l.ReplenishmentId == id
            && l.SourceMaterialRequestLine != null).Select(l => l.SourceMaterialRequestLine!.RequestId).Distinct().ToArrayAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, e.BranchId, now, ct);
        foreach (var request in linked)
            if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == request, ct)
                || !await MaterialReadProvenance.ValidAsync(db, request, e.BranchId, ct)) return null;
        return id;
    }

    internal async Task DeliverReplenishmentCreatedAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await ReplenishmentReceiptsInstalledAsync(db, ct)) return;
        var hint = await ReplenishmentCreatedEvents(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (hint == null) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var candidates = await ReplenishmentManagers(db, hint.BranchId, now).OrderBy(u => u).ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates, ct);
        var current = await ReplenishmentCreatedEvents(db).SingleOrDefaultAsync(e => e.Id == id, ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (current != hint || !NotificationRetention.At(now, retention.Value).Contains(hint.At)) return;
        var eligible = new List<long>();
        foreach (var user in candidates)
            if (await ReplenishmentCreatedTargetAsync(db, id, user, hint.ActorId, hint.At, ct) == id) eligible.Add(user);
        if (eligible.Count == 0) return;
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type={ReplenishmentCreatedKind} AND source_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != ReplenishmentCreatedKind
            || n.CreatedBy != hint.ActorId || n.CreatedAt != hint.At))
            throw new InvalidOperationException("Replenishment creation notice provenance conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = ReplenishmentCreatedKind, SourceType = ReplenishmentCreatedKind, SourceId = id,
                Title = "Replenishment requested", Message = "A replenishment request was created in your branch. Open it to review the current status.",
                Priority = "NORMAL", CreatedBy = hint.ActorId, CreatedAt = hint.At };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.GroupBy(r => r.UserId).Any(g => g.Count() > 1) || receipts.Any(r => r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null))
            throw new InvalidOperationException("Replenishment creation receipt conflict.");
        foreach (var user in eligible)
        {
            if (await ReplenishmentCreatedTargetAsync(db, id, user, hint.ActorId, hint.At, ct) != id)
                throw new InvalidOperationException("Replenishment recipient authority changed.");
            if (!receipts.Any(r => r.UserId == user)) db.NotificationRecipients.Add(new NotificationRecipient {
                NotificationId = notice.NotificationId, UserId = user, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}
