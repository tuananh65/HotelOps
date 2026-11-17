using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class WorkAlertSourceAccess
{
    internal static Task<bool> CanReadAsync(HotelOpsDbContext db,long user,WorkAlertSourceRow episode,CancellationToken ct)
        => episode.SourceType switch
        {
            "INTERNAL_REQUEST" => InternalRequestAlertAccess.CanReadAsync(db,user,episode,ct),
            "TASK" => TaskAlertSource.CanReadAsync(db,user,episode,ct),
            "CLEANING" or "REWORK" => CleaningAlertSource.CanReadAsync(db,user,episode,ct),
            "MAINTENANCE_OCCURRENCE" => PreventiveAlertSource.CanReadAsync(db,user,episode,ct),
            "INCIDENT" => IncidentAlertSource.CanReadAsync(db,user,episode,ct),
            _ => Task.FromResult(false)
        };

    internal static async Task<WorkAlertCondition> ConditionAsync(HotelOpsDbContext db,WorkAlertSourceRow episode,
        DateTime now,decimal threshold,CancellationToken ct)
    {
        if(episode.SourceType is "TASK" or "INTERNAL_REQUEST"
            && await WorkAlertCycle.ReadAsync(db,episode.SourceType,episode.SourceId,now,ct)!=episode.CycleId)
            return WorkAlertCondition.Unknown;
        if(episode.SourceType=="TASK")return await TaskAlertSource.ConditionAsync(db,episode.SourceId,now,threshold,ct);
        if(episode.SourceType=="INCIDENT")return await IncidentAlertSource.ConditionAsync(db,episode,now,threshold,ct);
        if(episode.SourceType=="MAINTENANCE_OCCURRENCE")return await PreventiveAlertSource.ConditionAsync(db,episode,now,threshold,ct);
        if(episode.SourceType is "CLEANING" or "REWORK")return await CleaningAlertSource.ConditionAsync(db,episode,now,threshold,ct);
        if(episode.SourceType!="INTERNAL_REQUEST")return WorkAlertCondition.Unknown;
        var request=await db.InternalRequests.AsNoTracking().SingleOrDefaultAsync(r=>r.InternalRequestId==episode.SourceId&&r.BranchId==episode.BranchId,ct);
        if(request==null)return WorkAlertCondition.Unknown;
        if(request.Status=="DRAFT")return WorkAlertCondition.Clear;
        return WorkAlertWindow.Evaluate(request.Status is "CLOSED" or "REJECTED" or "CANCELLED",request.DueAt,request.CreatedAt,now,threshold);
    }
}
