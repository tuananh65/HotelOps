using HotelOps.Api.DTOs.Notifications;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public sealed partial class NotificationCenterService
{
    public async Task<AssetConditionFollowupDto> ReadAssetConditionAsync(long id, CancellationToken ct)
    {
        if(id<=0) throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=clock.GetUtcNow().UtcDateTime;var user=await UserAsync(now,ct);
        var notice=await Inbox(user,now).AsNoTracking().Where(r=>r.NotificationId==id).Select(r=>r.Notification).SingleOrDefaultAsync(ct);
        if(notice is not {NotificationType:IncidentAssetFollowupSource.Kind,SourceType:IncidentAssetFollowupSource.SourceType,SourceId:>0})
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        var source=await IncidentAssetFollowupSource.ReadAsync(db,notice.SourceId.Value,ct);
        if(source==null||source.At!=notice.CreatedAt||source.ActorId!=notice.CreatedBy
            ||!await IncidentAssetFollowupSource.CanReadAsync(db,user,source,ct))
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        var asset=await db.Assets.AsNoTracking().SingleAsync(a=>a.AssetId==source.AssetId,ct);
        var result=new AssetConditionFollowupDto(asset.AssetId,asset.BranchId,asset.AssetName,source.Condition,
            asset.ConditionStatus,DateTime.SpecifyKind(source.At,DateTimeKind.Utc));
        await RevalidateSessionAsync(user,ct);
        if(!await IncidentAssetFollowupSource.CanReadAsync(db,user,source,ct))
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        await tx.CommitAsync(ct);return result;
    }
}
