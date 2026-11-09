using HotelOps.Api.Data;
using HotelOps.Api.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public sealed class NotificationInbox(HotelOpsDbContext db, NotificationSourcePolicy policy, TimeProvider clock)
{
    private static NotificationItem Item(NotificationIntent n) => new(n.IntentId, n.Title, n.Message, "NORMAL",
        n.CreatedUtc, n.ReadAt, n.ActionPath, Convert.ToBase64String(n.RowVersion));

    public async Task<NotificationPage> ListAsync(long userId, int page, int pageSize, bool? unread, CancellationToken ct,
        NotificationFilter? filter = null)
    {
        var visible = await policy.VisibleAsync(userId, ct);
        var unreadCount = await visible.CountAsync(x => x.ReadAt == null, ct);
        if (!string.IsNullOrWhiteSpace(filter?.Type)) visible = visible.Where(x => x.EventType == filter.Type);
        if (!string.IsNullOrWhiteSpace(filter?.Search)) visible = visible.Where(x => x.Title.Contains(filter.Search) || x.Message.Contains(filter.Search));
        if (!string.IsNullOrWhiteSpace(filter?.Priority)) visible = visible.Where(x => db.Notifications.Any(n => n.NotificationId == x.NotificationId && n.Priority == filter.Priority));
        if (filter?.From is { } from) visible = visible.Where(x => x.CreatedUtc >= from.UtcDateTime);
        if (filter?.To is { } to) visible = visible.Where(x => x.CreatedUtc <= to.UtcDateTime);
        if (unread is not null) visible = visible.Where(x => (x.ReadAt == null) == unread.Value);
        var count = await visible.CountAsync(ct);
        var rows = await visible.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.IntentId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(Item).ToArray(), count, unreadCount, page, pageSize);
    }
    public async Task<NotificationItem?> GetAsync(long userId, long id, CancellationToken ct)
    {
        var row = await (await policy.VisibleAsync(userId, ct)).SingleOrDefaultAsync(x => x.IntentId == id, ct);
        return row is null ? null : Item(row);
    }
    public async Task<NotificationItem?> SetReadAsync(long userId, long id, NotificationReadRequest request, CancellationToken ct)
    {
        byte[] version;
        try { version = Convert.FromBase64String(request.ExpectedRowVersion); }
        catch (Exception e) when (e is FormatException or ArgumentNullException) { throw new ArgumentException("Invalid row version."); }
        if (version.Length != 8) throw new ArgumentException("Invalid row version.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await (await policy.VisibleAsync(userId, ct)).AsTracking().SingleOrDefaultAsync(x => x.IntentId == id, ct);
        if (row is null) return null;
        if (!row.RowVersion.SequenceEqual(version)) throw new NotificationConflictException();
        row.ReadAt = request.IsRead ? clock.GetUtcNow().UtcDateTime : null;
        var recipient = await db.NotificationRecipients.SingleAsync(x => x.NotificationId == row.NotificationId &&
            x.UserId == userId && x.DeliveryChannel == "IN_APP", ct);
        recipient.ReadAt = row.ReadAt;
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new NotificationConflictException(); }
        return Item(row);
    }
}
