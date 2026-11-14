using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Notifications;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace HotelOps.Api.Services.Notifications;
public sealed class NotificationCenterOptions { public int RetentionDays {get;set;}=365; }
public sealed class NotificationCenterService(HotelOpsDbContext db,ICurrentUserAccessor caller,TimeProvider clock,IOptions<NotificationCenterOptions> options)
{
    private async Task<long> UserAsync(DateTime now,CancellationToken ct)
    {
        if(!caller.TryGetCurrentUser(out var user)||user.UserId<=0||user.SessionId<=0)throw new InsightException(401,"AUTH_REQUIRED","Authentication required.");
        if(user.IsRestricted)throw new InsightException(403,"RESTRICTED","Restricted session.");
        var lifetimeHours=SqlSessionPolicy.NormalAbsoluteLifetime.TotalHours;
        if(!await db.UserSessions.AsNoTracking().AnyAsync(s=>s.SessionId==user.SessionId&&s.UserId==user.UserId&&s.RevokedAt==null&&s.ExpiresAt>now
            &&s.CreatedAt<=now&&s.CreatedAt.AddHours(lifetimeHours)>now&&s.ExpiresAt<=s.CreatedAt.AddHours(lifetimeHours)
            &&s.User.DeletedAt==null&&s.User.AccountStatus=="ACTIVE"&&s.User.TemporaryPasswordExpiresAt==null,ct))throw new InsightException(401,"SESSION_INVALID","Session invalid.");
        return user.UserId;
    }
    private IQueryable<NotificationRecipient> Inbox(long user,DateTime now)
    {
        var cutoff=now.AddDays(-options.Value.RetentionDays);
        return db.NotificationRecipients.Where(r=>r.UserId==user&&r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED"
            &&r.Notification.CreatedAt>=cutoff&&r.Notification.CreatedAt<=now&&(r.Notification.ExpiresAt==null||r.Notification.ExpiresAt>now));
    }
    public async Task<NotificationPage> ListAsync(NotificationQuery filter,CancellationToken ct)
    {
        if(filter.Page<1||filter.PageSize is <1 or >100||(long)(filter.Page-1)*filter.PageSize>int.MaxValue
            ||filter.To==DateOnly.MaxValue||(filter.From is {} from&&filter.To is {} to&&from>to))throw new InsightException(400,"INVALID_FILTERS","Invalid notification filters.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=clock.GetUtcNow();var user=await UserAsync(now.UtcDateTime,ct);var inbox=Inbox(user,now.UtcDateTime).AsNoTracking();
        var unread=await inbox.CountAsync(r=>r.ReadAt==null,ct);var query=inbox;
        if(filter.Unread is {} onlyUnread)query=query.Where(r=>(r.ReadAt==null)==onlyUnread);
        if(!string.IsNullOrWhiteSpace(filter.Type))query=query.Where(r=>r.Notification.NotificationType==filter.Type);
        if(!string.IsNullOrWhiteSpace(filter.Priority))query=query.Where(r=>r.Notification.Priority==filter.Priority);
        if(!string.IsNullOrWhiteSpace(filter.Search)){var term=filter.Search.Trim();query=query.Where(r=>r.Notification.Title.Contains(term)||r.Notification.Message.Contains(term));}
        if(filter.From is {} start){var date=start.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);query=query.Where(r=>r.Notification.CreatedAt>=date);}
        if(filter.To is {} end){var date=end.AddDays(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);query=query.Where(r=>r.Notification.CreatedAt<date);}
        var total=await query.CountAsync(ct);
        var raw=await query.OrderByDescending(r=>r.Notification.CreatedAt).ThenByDescending(r=>r.NotificationId)
            .Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize)
            .Select(r=>new {Id=r.NotificationId,Type=r.Notification.NotificationType,r.Notification.Title,r.Notification.Message,r.Notification.Priority,r.Notification.CreatedAt,r.ReadAt}).ToArrayAsync(ct);
        await tx.CommitAsync(ct);
        return new(raw.Select(r=>new NotificationRow(r.Id,r.Type,r.Title,r.Message,r.Priority,Utc(r.CreatedAt),r.ReadAt is {} read?Utc(read):null)).ToArray(),total,unread,filter.Page,filter.PageSize,now);
    }
    public async Task MarkReadAsync(long? id,CancellationToken ct)
    {
        if(id is <=0)throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=clock.GetUtcNow().UtcDateTime;var user=await UserAsync(now,ct);var query=Inbox(user,now);
        if(id is {} notification){query=query.Where(r=>r.NotificationId==notification);if(!await query.AnyAsync(ct))throw new InsightException(404,"NOT_FOUND","Notification not found.");}
        await query.Where(r=>r.ReadAt==null).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReadAt,now),ct);
        await tx.CommitAsync(ct);
    }
    private static DateTimeOffset Utc(DateTime value)=>new(DateTime.SpecifyKind(value,DateTimeKind.Utc));
}
