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
    internal const string StoreApproval = "MATERIAL_STORE_APPROVAL";
    internal const string StoreRemainder = "MATERIAL_STORE_REMAINDER";

    private static IQueryable<NoticeEvent> StoreEvents(HotelOpsDbContext db, bool remainder) => remainder
        ? Events(db, NoticeType.Remainder)
        : Decisions(db).Where(d => d.Decision == "APPROVED")
            .Select(d => new NoticeEvent { Id = d.ApprovalId, RequestId = d.RequestId,
                RequesterId = d.Request.RequesterId, BranchId = d.Request.BranchId, ActorId = d.ApproverId, At = d.DecidedAt });

    private static IQueryable<long> StoreUsers(HotelOpsDbContext db, long branch, DateTime now) => db.UserRoles.AsNoTracking()
        .Where(r => r.Role.RoleCode == "STOREKEEPER" && r.IsActive && r.Role.IsActive
            && r.BranchId == branch && r.Branch!.IsActive && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE"
            && db.UserBranchAccesses.Any(b => b.UserId == r.UserId && b.BranchId == branch && b.GrantedAt <= now && b.RevokedAt == null))
        .Select(r => r.UserId).Distinct();

    internal async Task<long[]> PendingStoreAsync(long after, bool remainder, CancellationToken ct)
    {
        if (!await InstalledAsync(ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        // Rescan retained events so newly eligible recipients are not lost after partial delivery.
        return await StoreEvents(db, remainder).Where(e => e.Id > after && e.At >= cutoff && e.At <= now)
            .OrderBy(e => e.Id).Select(e => e.Id).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<long?> StoreTargetAsync(HotelOpsDbContext db, long id, long user, bool remainder,
        long? actor, DateTime at, CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([user], ct);
        var e = await StoreEvents(db, remainder).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (e == null || e.ActorId != actor || e.At != at || user == e.RequesterId || user == e.ActorId) return null;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await StoreUsers(db, e.BranchId, now).ContainsAsync(user, ct)) return null;
        if (!await db.MaterialRequests.AsNoTracking().AnyAsync(r => r.RequestId == e.RequestId
            && r.SourceWarehouse != null && r.SourceWarehouse.IsActive && r.SourceWarehouse.BranchId == e.BranchId, ct)) return null;
        var grants = await new EfPermissionDataSource(db).ReadPathsAsync(user, "MR_FULFILL", now, ct);
        if (!grants.Any(g => g.PermissionCode == "MR_FULFILL" && PermissionGrantRules.IsEligible(g, now)
            && (PermissionGrantRules.EffectiveScope(g) == "GLOBAL"
                || g.BranchId == e.BranchId && PermissionGrantRules.EffectiveScope(g) == "BRANCH"))) return null;
        var read = await MaterialRequestReadAccess.ReadAsync(db, user, e.BranchId, now, ct);
        return await read.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == e.RequestId, ct)
            && await MaterialReadProvenance.ValidAsync(db, e.RequestId, e.BranchId, ct) ? e.RequestId : null;
    }

    internal async Task DeliverStoreAsync(long id, bool remainder, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await InstalledAsync(ct)) return;
        var hint = await StoreEvents(db, remainder).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (hint == null) return;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var candidates = await StoreUsers(db, hint.BranchId, now).Where(u => u != hint.RequesterId && u != hint.ActorId).OrderBy(u => u).ToArrayAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync(candidates, ct);
        var current = await StoreEvents(db, remainder).SingleOrDefaultAsync(e => e.Id == id, ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (current != hint || !NotificationRetention.At(now, retention.Value).Contains(hint.At)) return;
        var eligible = new List<long>();
        foreach (var user in candidates)
            if (await StoreTargetAsync(db, id, user, remainder, hint.ActorId, hint.At, ct) == hint.RequestId) eligible.Add(user);
        if (eligible.Count == 0) return;
        var source = remainder ? StoreRemainder : StoreApproval;
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type={source} AND source_id={id}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != source || n.CreatedBy != hint.ActorId || n.CreatedAt != hint.At))
            throw new InvalidOperationException("Storekeeper notice provenance conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            notice = new Notification { NotificationType = source, SourceType = source, SourceId = id,
                Title = remainder ? "Material remainder closed" : "Material request approved",
                Message = "A material request decision was recorded for your warehouse branch. Open the request to review its current status.",
                Priority = "NORMAL", CreatedBy = hint.ActorId, CreatedAt = hint.At };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.GroupBy(r => r.UserId).Any(g => g.Count() > 1) || receipts.Any(r => r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null || r.UserId == hint.ActorId || r.UserId == hint.RequesterId))
            throw new InvalidOperationException("Storekeeper receipt conflict.");
        foreach (var user in eligible)
        {
            if (await StoreTargetAsync(db, id, user, remainder, hint.ActorId, hint.At, ct) != hint.RequestId)
                throw new InvalidOperationException("Storekeeper authority changed.");
            if (!receipts.Any(r => r.UserId == user)) db.NotificationRecipients.Add(new NotificationRecipient {
                NotificationId = notice.NotificationId, UserId = user, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}
