using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public sealed record AssetHandoverNoticeDto(long HandoverId, string Number, long AssetId, string AssetCode,
    string State, string? ConditionNote, DateTime SubmittedAtUtc);

public sealed partial class NotificationCenterService
{
    public async Task<AssetHandoverNoticeDto> ReadAssetHandoverAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await UserAsync(now, ct);
        var notice = await Inbox(user, now).AsNoTracking().Where(r => r.NotificationId == id)
            .Select(r => r.Notification).SingleOrDefaultAsync(ct);
        if (notice is not { NotificationType: AssetHandoverSource.Kind or AssetHandoverSource.DecisionKind, SourceType: AssetHandoverSource.SourceType, SourceId: > 0 })
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        var source = await AssetHandoverSource.ReadAsync(db, notice.SourceId.Value, ct);
        if (source == null || source.EventKind != notice.NotificationType || source.At != notice.CreatedAt || source.ActorId != notice.CreatedBy
            || !await AssetHandoverSource.CanReadAsync(db, user, source, ct))
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        var result = await db.AssetHandovers.AsNoTracking().Where(h => h.HandoverId == source.HandoverId)
            .Select(h => new AssetHandoverNoticeDto(h.HandoverId,h.HandoverNo,h.AssetId,h.Asset.AssetCode,
                h.HandoverStatus,h.ConditionNote,DateTime.SpecifyKind(h.CreatedAt,DateTimeKind.Utc))).SingleAsync(ct);
        await RevalidateSessionAsync(user, ct);
        if (!await AssetHandoverSource.CanReadAsync(db,user,source,ct)
            || await AssetHandoverSource.ReadAsync(db,source.AuditId,ct) != source)
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        await tx.CommitAsync(ct);
        return result;
    }
}
