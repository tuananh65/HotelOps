using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Notifications;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace HotelOps.Api.Services.Notifications;
public sealed class NotificationCenterOptions { public int RetentionDays {get;set;}=365; }
public sealed partial class NotificationCenterService(HotelOpsDbContext db,ICurrentUserAccessor caller,TimeProvider clock,IOptions<NotificationCenterOptions> options,
    HotelOps.Api.Services.HotelStructure.RoomTypeCreationService? cleaning=null)
{
    private async Task<long> UserAsync(DateTime now,CancellationToken ct)
    {
        if(!caller.TryGetCurrentUser(out var user)||user.UserId<=0||user.SessionId<=0)throw new InsightException(401,"AUTH_REQUIRED","Authentication required.");
        if(user.IsRestricted)throw new InsightException(403,"RESTRICTED","Restricted session.");
        if(!await db.UserSessions.AsNoTracking().AnyAsync(NotificationSessionPolicy.Current(user.UserId,user.SessionId,now),ct))throw new InsightException(401,"SESSION_INVALID","Session invalid.");
        return user.UserId;
    }
    private async Task RevalidateSessionAsync(long expectedUser,CancellationToken ct)
    {
        // A Serializable transaction retains account/session locks but cannot stop
        // wall-clock expiry while waiting for recipient or acknowledgment rows.
        if(await UserAsync(clock.GetUtcNow().UtcDateTime,ct)!=expectedUser)
            throw new InsightException(401,"SESSION_INVALID","Session changed during notification access.");
    }
    private IQueryable<NotificationRecipient> Inbox(long user,DateTime now)
    {
        var cutoff=NotificationRetention.At(now,options.Value).Cutoff;
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
        var acknowledgmentAvailable=await NotificationAcknowledgmentStore.InstalledAsync(db,ct);
        if(filter.Acknowledged is {} acknowledged)
        {
            if(!acknowledgmentAvailable)throw new InsightException(503,"ACK_SCHEMA_REQUIRED","Acknowledgment storage is unavailable.");
            var matching=NotificationAcknowledgmentStore.Query(db).Where(a=>a.RequiresAcknowledgment&&(a.AcknowledgedAt!=null)==acknowledged).Select(a=>a.RecipientId);
            query=query.Where(r=>matching.Contains(r.RecipientId));
        }
        if(filter.Unread is {} onlyUnread)query=query.Where(r=>(r.ReadAt==null)==onlyUnread);
        if(!string.IsNullOrWhiteSpace(filter.Type))query=query.Where(r=>r.Notification.NotificationType==filter.Type);
        if(!string.IsNullOrWhiteSpace(filter.Priority))query=query.Where(r=>r.Notification.Priority==filter.Priority);
        if(!string.IsNullOrWhiteSpace(filter.Search)){var term=filter.Search.Trim();query=query.Where(r=>r.Notification.Title.Contains(term)||r.Notification.Message.Contains(term));}
        if(filter.From is {} start){var date=start.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);query=query.Where(r=>r.Notification.CreatedAt>=date);}
        if(filter.To is {} end){var date=end.AddDays(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);query=query.Where(r=>r.Notification.CreatedAt<date);}
        var total=await query.CountAsync(ct);
        var raw=await query.OrderByDescending(r=>r.Notification.CreatedAt).ThenByDescending(r=>r.NotificationId)
            .Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize)
            .Select(r=>new {r.RecipientId,Id=r.NotificationId,Type=r.Notification.NotificationType,r.Notification.Title,r.Notification.Message,r.Notification.Priority,r.Notification.CreatedAt,r.ReadAt}).ToArrayAsync(ct);
        var recipientIds=raw.Select(r=>r.RecipientId).ToArray();
        var acknowledgments=acknowledgmentAvailable
            ? await NotificationAcknowledgmentStore.Query(db).Where(a=>recipientIds.Contains(a.RecipientId)).ToDictionaryAsync(a=>a.RecipientId,ct)
            : new Dictionary<long,NotificationAcknowledgmentRow>();
        await RevalidateSessionAsync(user,ct);
        await tx.CommitAsync(ct);
        return new(raw.Select(r=>new NotificationRow(r.Id,r.Type,r.Title,r.Message,r.Priority,Utc(r.CreatedAt),r.ReadAt is {} read?Utc(read):null,
            acknowledgments.GetValueOrDefault(r.RecipientId)?.RequiresAcknowledgment==true,
            acknowledgments.GetValueOrDefault(r.RecipientId)?.AcknowledgedAt is {} ack?Utc(ack):null)).ToArray(),total,unread,filter.Page,filter.PageSize,now,acknowledgmentAvailable);
    }
    public async Task AcknowledgeAsync(long[]? ids,CancellationToken ct)
    {
        if(ids is null || ids.Length is <1 or >100 || ids.Any(id=>id<=0))throw new InsightException(400,"INVALID_IDS","Select between one and 100 valid notifications.");
        ids=ids.Distinct().ToArray();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(!caller.TryGetCurrentUser(out var actor))throw new InsightException(401,"AUTH_REQUIRED","Authentication required.");
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([actor.UserId],ct);
        var now=clock.GetUtcNow().UtcDateTime;var user=await UserAsync(now,ct);
        if(!await NotificationAcknowledgmentStore.InstalledAsync(db,ct))throw new InsightException(503,"ACK_SCHEMA_REQUIRED","Acknowledgment storage is unavailable.");
        var recipients=await Inbox(user,now).AsNoTracking().Where(r=>ids.Contains(r.NotificationId)).Select(r=>new {r.RecipientId,r.NotificationId,r.ReadAt}).ToArrayAsync(ct);
        if(recipients.Length!=ids.Length)throw new InsightException(404,"NOT_FOUND","Notification not found.");
        var keys=recipients.Select(r=>r.RecipientId).ToArray();
        var states=await NotificationAcknowledgmentStore.Query(db).Where(a=>keys.Contains(a.RecipientId)).ToDictionaryAsync(a=>a.RecipientId,ct);
        if(states.Count!=keys.Length || states.Values.Any(a=>!a.RequiresAcknowledgment))throw new InsightException(409,"ACK_NOT_REQUIRED","A selected notification does not require acknowledgment.");
        foreach(var recipient in recipients)
        {
            if(states[recipient.RecipientId].AcknowledgedAt!=null)continue;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET acknowledged_at={now},read_at=COALESCE(read_at,{now}) WHERE recipient_id={recipient.RecipientId} AND acknowledged_at IS NULL",ct);
            db.AuditLogs.Add(new AuditLog {ActorUserId=user,EntityType="notification_recipients",EntityId=recipient.RecipientId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=System.Text.Json.JsonSerializer.Serialize(new {acknowledgedAt=(DateTimeOffset?)null,readAt=recipient.ReadAt is {} read?Utc(read):(DateTimeOffset?)null}),
                NewValues=System.Text.Json.JsonSerializer.Serialize(new {notificationId=recipient.NotificationId,acknowledgedAt=Utc(now),readAt=Utc(recipient.ReadAt??now)}),
                ChangedFields=recipient.ReadAt==null?"[\"acknowledgedAt\",\"readAt\"]":"[\"acknowledgedAt\"]"});
        }
        await db.SaveChangesAsync(ct);await RevalidateSessionAsync(user,ct);await tx.CommitAsync(ct);
    }
    public async Task MarkReadAsync(long? id,CancellationToken ct)
    {
        if(id is <=0)throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await MarkReadCoreAsync(id is {} value ? [value] : null,ct);
    }
    public Task MarkSelectedReadAsync(long[]? ids,CancellationToken ct)
    {
        if(ids is null || ids.Length is <1 or >100 || ids.Any(id=>id<=0))
            throw new InsightException(400,"INVALID_IDS","Select between one and100 valid notifications.");
        return MarkReadCoreAsync(ids.Distinct().ToArray(),ct);
    }
    private async Task MarkReadCoreAsync(long[]? ids,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(!caller.TryGetCurrentUser(out var actor))throw new InsightException(401,"AUTH_REQUIRED","Authentication required.");
        await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([actor.UserId],ct);
        var now=clock.GetUtcNow().UtcDateTime;
        var user=await UserAsync(now,ct);var query=Inbox(user,now);
        if(ids is not null)
        {
            query=query.Where(r=>ids.Contains(r.NotificationId));
            if(await query.CountAsync(ct)!=ids.Length)throw new InsightException(404,"NOT_FOUND","Notification not found.");
        }
        await query.Where(r=>r.ReadAt==null).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReadAt,now),ct);
        await RevalidateSessionAsync(user,ct);
        await tx.CommitAsync(ct);
    }
    public async Task<NotificationSource> ResolveSourceAsync(long id,CancellationToken ct)
    {
        if(id<=0)throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=clock.GetUtcNow().UtcDateTime;var user=await UserAsync(now,ct);
        var source=await Inbox(user,now).AsNoTracking().Where(r=>r.NotificationId==id)
            .Select(r=>new{r.Notification.NotificationType,r.Notification.SourceType,r.Notification.SourceId,r.Notification.CreatedBy,r.Notification.CreatedAt}).SingleOrDefaultAsync(ct);
        if(source is {SourceType:StocktakeNotifications.Source,NotificationType:StocktakeNotifications.Kind,SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            var reconciliation=await StocktakeNotifications.Events(db).SingleOrDefaultAsync(e=>e.Id==source.SourceId,ct);
            var checkedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(reconciliation==null||reconciliation.Actor!=source.CreatedBy||reconciliation.At!=source.CreatedAt
                ||!await StocktakeNotifications.CanReadAsync(db,user,reconciliation,checkedAt,ct))
                throw new InsightException(403,"ACCESS_DENIED","MSG02: Stocktake source access denied.");
            await RevalidateSessionAsync(user,ct);
            checkedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(!await StocktakeNotifications.CanReadAsync(db,user,reconciliation,checkedAt,ct))
                throw new InsightException(403,"ACCESS_DENIED","MSG02: Stocktake source access denied.");
            await tx.CommitAsync(ct);
            return new($"/inventory/stocktakes/{reconciliation.StocktakeId}");
        }
        if(source is {SourceType:TransferNotifications.Source,SourceId:>0}
            && source.NotificationType is TransferNotifications.Dispatched or TransferNotifications.Received)
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            var transfer = await TransferNotifications.Events(db).SingleOrDefaultAsync(e=>e.Id==source.SourceId,ct);
            var checkedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(transfer==null||!TransferNotifications.Matches(transfer,source.NotificationType,source.CreatedBy,source.CreatedAt)
                ||!await TransferNotifications.CanReadAsync(db,user,transfer,checkedAt,ct))
                throw new InsightException(403,"ACCESS_DENIED","MSG02: Transfer source access denied.");
            await RevalidateSessionAsync(user,ct);
            checkedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(!await TransferNotifications.CanReadAsync(db,user,transfer,checkedAt,ct))
                throw new InsightException(403,"ACCESS_DENIED","MSG02: Transfer source access denied.");
            await tx.CommitAsync(ct);
            return new($"/inventory/transfers/{transfer.DocumentId}");
        }
        if(source is {NotificationType:LowStockAlerts.Kind,SourceType:LowStockAlerts.Source,SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await LowStockAlerts.InstalledAsync(db,ct)) throw new InsightException(404,"SOURCE_UNAVAILABLE","MSG02: Stock source unavailable.");
            var episode=await LowStockAlerts.Query(db).SingleOrDefaultAsync(e=>e.Id==source.SourceId,ct);
            var checkedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(episode==null||source.CreatedBy!=null||source.CreatedAt!=episode.OpenedAt||!await LowStockAlerts.CanReadAsync(db,user,episode,checkedAt,ct))
                throw new InsightException(403,"ACCESS_DENIED","MSG02: Stock source access denied.");
            await RevalidateSessionAsync(user,ct);
            checkedAt=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(!await LowStockAlerts.CanReadAsync(db,user,episode,checkedAt,ct)) throw new InsightException(403,"ACCESS_DENIED","MSG02: Stock source access denied.");
            await tx.CommitAsync(ct);
            return new($"/inventory/stock?warehouseId={episode.WarehouseId}&itemId={episode.ItemId}");
        }
        if(source is {NotificationType:InternalRequestAlertDelivery.Kind,SourceType:InternalRequestAlertDelivery.Source,SourceId:>0})
        {
            if(!await WorkAlertEpisodeStore.InstalledAsync(db,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Alert source unavailable.");
            var episode=await WorkAlertEpisodeStore.SourcesQuery(db).SingleOrDefaultAsync(e=>e.Id==source.SourceId,ct);
            if(episode==null||source.CreatedBy!=null||source.CreatedAt!=episode.OpenedAt
                ||!await WorkAlertSourceAccess.CanReadAsync(db,user,episode,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Alert source unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await WorkAlertSourceAccess.CanReadAsync(db,user,episode,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Alert source unavailable.");
            var path=episode.SourceType switch
            {
                "TASK"=>$"/operational-tasks?branchId={episode.BranchId}&taskId={episode.SourceId}",
                "MAINTENANCE_OCCURRENCE"=>$"/operations/maintenance?branchId={episode.BranchId}&runId={episode.SourceId}",
                "INCIDENT"=>$"/operations/incidents/{episode.SourceId}?action=A004A-023",
                "CLEANING"=>$"/cleaning/tasks/{episode.SourceId}?branchId={episode.BranchId}&monitor=1",
                "REWORK"=>$"/cleaning/reworks/{episode.SourceId}?branchId={episode.BranchId}",
                "INTERNAL_REQUEST"=>$"/internal-requests?requestId={episode.SourceId}",
                _=>throw new InsightException(404,"SOURCE_UNAVAILABLE","Alert source unavailable.")
            };
            if(episode.SourceType=="CLEANING")
            {
                var assignment=await db.CleaningAssignments.AsNoTracking().Where(a=>a.CleaningTaskId==episode.SourceId
                    &&a.AssigneeUserId==user&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now)
                    .Select(a=>(long?)a.AssignmentId).SingleOrDefaultAsync(ct);
                if(assignment is {} own)path=$"/cleaning/tasks/{episode.SourceId}?branchId={episode.BranchId}&assignmentId={own}";
            }
            if(episode.SourceType=="REWORK"&&!await db.CleaningReworks.AsNoTracking()
                .AnyAsync(r=>r.ReworkId==episode.SourceId&&r.AssigneeUserId==user,ct))
                path=$"/cleaning/tasks/{episode.SourceId}?branchId={episode.BranchId}&monitor=1&kind=rework";
            await RevalidateSessionAsync(user,ct);
            if(!await WorkAlertSourceAccess.CanReadAsync(db,user,episode,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Alert source unavailable.");
            await tx.CommitAsync(ct);return new(path);
        }
        if(source is {NotificationType:"TIME_CORRECTION_REQUESTED" or "TIME_CORRECTION_DECIDED",SourceType:"TIME_CORRECTION",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            var correction=await TimeCorrectionDelivery.SourceAsync(db,source.SourceId.Value,ct);
            var currentTime=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(correction==null||correction.Kind!=source.NotificationType||correction.ActorId!=source.CreatedBy||correction.At!=source.CreatedAt
                ||!await TimeCorrectionDelivery.CanReadAsync(db,correction,user,currentTime,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Correction source unavailable.");
            await RevalidateSessionAsync(user,ct);await tx.CommitAsync(ct);
            return new($"/time-corrections?branchId={correction.BranchId}&correctionId={correction.CorrectionId}");
        }
        if(source is {NotificationType:ShiftCancellationSource.Kind,SourceType:ShiftCancellationSource.Kind,SourceId:>0})
        {
            var receipt=await ShiftCancellationSource.TargetAsync(db,source.SourceId.Value,user,source.CreatedBy,source.CreatedAt,ct);
            if(receipt==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Cancellation source unavailable.");
            await RevalidateSessionAsync(user,ct);await tx.CommitAsync(ct);
            return new($"/workday?branchId={receipt.BranchId}&shiftAssignmentId={receipt.AssignmentId}");
        }
        if(source is {NotificationType:ShiftAssignmentNoticeAccess.Kind,SourceType:ShiftAssignmentNoticeAccess.Kind,SourceId:>0})
        {
            var shiftBranch=await ShiftAssignmentNoticeAccess.TargetAsync(db,source.SourceId.Value,user,source.CreatedBy,source.CreatedAt,ct);
            if(shiftBranch==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment unavailable.");
            await RevalidateSessionAsync(user,ct);await tx.CommitAsync(ct);
            return new($"/workday?branchId={shiftBranch}&shiftAssignmentId={source.SourceId}");
        }
        if(source is {NotificationType:Incidents.IncidentCriticalNotificationDelivery.Kind,SourceType:Incidents.IncidentCriticalNotificationDelivery.Kind,SourceId:>0})
        {
            var incidentId = await Incidents.IncidentCriticalNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct);
            if (incidentId == null) throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user, ct);
            if (await Incidents.IncidentCriticalNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct) != incidentId)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct); return new($"/operations/incidents/{incidentId}?action=A004A-023");
        }
        if(source is {NotificationType:Incidents.IncidentEscalatedNotificationDelivery.Kind,SourceType:Incidents.IncidentEscalatedNotificationDelivery.Kind,SourceId:>0})
        {
            var incidentId = await Incidents.IncidentEscalatedNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct);
            if (incidentId == null) throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user, ct);
            if (await Incidents.IncidentEscalatedNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct) != incidentId)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct); return new($"/operations/incidents/{incidentId}?action=A004A-023");
        }
        if(source is {NotificationType:PreventiveMaintenance.PreventiveAssignmentNotificationDelivery.Kind,SourceType:PreventiveMaintenance.PreventiveAssignmentNotificationDelivery.Kind,SourceId:>0})
        {
            var target = await PreventiveMaintenance.PreventiveAssignmentNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct);
            if (target == null) throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime, ct);
            await tx.CommitAsync(ct); return new($"/operations/maintenance?branchId={target.BranchId}&runId={target.RunId}");
        }
        if(source is {NotificationType:Incidents.IncidentReopenedNotificationDelivery.Kind,SourceType:Incidents.IncidentReopenedNotificationDelivery.Kind,SourceId:>0})
        {
            var target = await Incidents.IncidentReopenedNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct);
            if (target == null) throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime, ct);
            await tx.CommitAsync(ct); return new(target.PreviousWorkId is {} work
                ? $"/operations/maintenance/{work}?action=A004A-036"
                : $"/operations/incidents/{target.IncidentId}?action=A004A-023");
        }
        if(source is {NotificationType:Incidents.IncidentCancelledNotificationDelivery.Kind,SourceType:Incidents.IncidentCancelledNotificationDelivery.Kind,SourceId:>0})
        {
            var incidentId = await Incidents.IncidentCancelledNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct);
            if (incidentId == null) throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime, ct);
            await tx.CommitAsync(ct); return new($"/operations/incidents/{incidentId}?action=A004A-023");
        }
        if(source is {NotificationType:Incidents.IncidentReportedNotificationDelivery.Kind,SourceType:Incidents.IncidentReportedNotificationDelivery.Kind,SourceId:>0})
        {
            var incidentId = await Incidents.IncidentReportedNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct);
            if (incidentId == null) throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user, ct);
            if (await Incidents.IncidentReportedNotificationDelivery.TargetAsync(db, source.SourceId.Value, user, source.CreatedBy, source.CreatedAt, ct) != incidentId)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct); return new($"/operations/incidents/{incidentId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.Kind,SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.Source,SourceId:>0})
        {
            var hint=await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c=>c.ApprovalCycleId==source.SourceId)
                .Select(c=>new{c.RequestId,c.Request.RequesterId}).SingleOrDefaultAsync(ct);
            if(hint==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await new Users.UserEligibilityLock(db).LockAsync([user,hint.RequesterId],ct);
            var cycle=await db.MaterialRequestApprovalCycles.AsNoTracking().Include(c=>c.Request)
                .SingleOrDefaultAsync(c=>c.ApprovalCycleId==source.SourceId&&c.RequestId==hint.RequestId&&c.Request.RequesterId==hint.RequesterId,ct);
            if(cycle==null||cycle.AssignedApproverId!=user||cycle.SubmittedBy!=source.CreatedBy||cycle.SubmittedAt!=source.CreatedAt)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            try{await MaterialRequests.MaterialApprovalAuthority.ValidateAsync(db,cycle.Request,user,ct);}
            catch(System.ComponentModel.DataAnnotations.ValidationException){throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");}
            var scope=await MaterialRequests.MaterialRequestReadAccess.ReadAsync(db,user,cycle.Request.BranchId,clock.GetUtcNow().UtcDateTime,ct);
            if(!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r=>r.RequestId==cycle.RequestId,ct)
                ||!await MaterialRequests.MaterialReadProvenance.ValidAsync(db,cycle.RequestId,cycle.Request.BranchId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{cycle.RequestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.CancellationKind,SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.CancellationSource,SourceId:>0})
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.CancellationTargetAsync(db,id,user,now,ct);
            if(requestId==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {SourceId:>0} && source.NotificationType == source.SourceType
            && source.SourceType is MaterialRequests.MaterialApprovalNotificationDelivery.StoreApproval or MaterialRequests.MaterialApprovalNotificationDelivery.StoreRemainder)
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.StoreTargetAsync(db,source.SourceId.Value,user,
                source.SourceType==MaterialRequests.MaterialApprovalNotificationDelivery.StoreRemainder,source.CreatedBy,source.CreatedAt,ct);
            if(requestId==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.DecisionKind,SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.DecisionSource,SourceId:>0})
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.DecisionTargetAsync(db,source.SourceId.Value,user,ct);
            if(requestId==null||!await db.MaterialRequestApprovals.AsNoTracking().AnyAsync(d=>d.ApprovalId==source.SourceId
                &&d.ApproverId==source.CreatedBy&&d.DecidedAt==source.CreatedAt,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.RemainderKind,SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.RemainderSource,SourceId:>0})
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.DecisionTargetAsync(db,source.SourceId.Value,user,ct,MaterialRequests.MaterialApprovalNotificationDelivery.NoticeType.Remainder);
            if(requestId==null||!await db.MaterialRequests.AsNoTracking().AnyAsync(r=>r.RequestId==source.SourceId
                &&r.RemainderClosedBy==source.CreatedBy&&r.RemainderClosedAt==source.CreatedAt,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.FulfillmentKind,SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.FulfillmentSource,SourceId:>0})
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.DecisionTargetAsync(db,source.SourceId.Value,user,ct,MaterialRequests.MaterialApprovalNotificationDelivery.NoticeType.Fulfillment);
            if(requestId==null||!await db.MaterialFulfillments.AsNoTracking().AnyAsync(f=>f.FulfillmentId==source.SourceId
                &&f.FulfilledBy==source.CreatedBy&&f.FulfilledAt==source.CreatedAt,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.ReceiptWaitingKind,
            SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.ReceiptWaitingSource,SourceId:>0})
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.ReceiptWaitingTargetAsync(db,
                source.SourceId.Value,user,source.CreatedBy,source.CreatedAt,ct);
            if(requestId==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.ReplenishmentReceiptKind,
            SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.ReplenishmentReceiptSource,SourceId:>0})
        {
            var requestId=await MaterialRequests.MaterialApprovalNotificationDelivery.ReplenishmentReceiptTargetAsync(db,
                source.SourceId.Value,user,source.CreatedBy,source.CreatedAt,ct);
            if(requestId==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/material-requests/{requestId}");
        }
        if(source is {NotificationType:MaterialRequests.MaterialApprovalNotificationDelivery.ReplenishmentCreatedKind,
            SourceType:MaterialRequests.MaterialApprovalNotificationDelivery.ReplenishmentCreatedKind,SourceId:>0})
        {
            var replenishmentId=await MaterialRequests.MaterialApprovalNotificationDelivery.ReplenishmentCreatedTargetAsync(db,
                source.SourceId.Value,user,source.CreatedBy,source.CreatedAt,ct);
            if(replenishmentId==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);return new($"/operations/replenishments/{replenishmentId}");
        }
        if(source is {NotificationType:"CLEANING_TASK_ASSIGNED",SourceType:"CLEANING_TASK",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(cleaning==null||!await CleaningAssignmentSchema.InstalledAsync(db,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");
            var assignments=await db.Database.SqlQuery<long>($"""
                SELECT p.assignment_id AS Value FROM dbo.notification_delivery_intents i
                JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
                WHERE i.inbox_notification_id={id} AND i.user_id={user} AND i.status='DELIVERED'
                  AND i.delivery_channel='IN_APP' AND p.event_type='CLEANING_TASK_ASSIGNED'
                """).ToArrayAsync(ct);
            if(assignments.Length==0)
            {
                var target=await ProvisionalCleaningReassignmentDelivery.TargetAsync(db,cleaning,id,user,ct);
                if(target==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");
                await RevalidateSessionAsync(user,ct);
                if(await ProvisionalCleaningReassignmentDelivery.TargetAsync(db,cleaning,id,user,ct)!=target)
                    throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");
                await tx.CommitAsync(ct);return new(target);
            }
            if(assignments.Length!=1)throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");
            var resolver=new HotelOps.Api.Services.Housekeeping.CleaningAssignmentNotificationSource(db,cleaning,clock);
            HotelOps.Api.Services.Housekeeping.CleaningAssignmentNotificationContent content;
            try{content=await resolver.ResolveAsync(assignments[0],false,ct);}
            catch(InvalidOperationException){throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");}
            if(content.Facts.AssigneeUserId!=user||content.Facts.ActorId!=source.CreatedBy||content.Facts.TaskId!=source.SourceId
                ||content.Facts.AssignedAtUtc>source.CreatedAt)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");
            await RevalidateSessionAsync(user,ct);
            try{await resolver.ResolveAsync(assignments[0],false,ct);}
            catch(InvalidOperationException){throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source unavailable.");}
            await tx.CommitAsync(ct);return new(content.ActionPath);
        }
        if(source is {NotificationType:"CLEANING_TASK_CANCELLED",SourceType:"CLEANING_TASK",SourceId:>0})
        {
            await CancellationReceiptAsync(id,user,now,ct);
            await UserAsync(clock.GetUtcNow().UtcDateTime,ct);
            await tx.CommitAsync(ct);
            return new($"/notifications/{id}/cleaning-cancellation");
        }
        if(source is {NotificationType:"CLEANING_TASK_COMPLETED" or "CLEANING_BLOCKER_REPORTED",SourceType:"CLEANING_TASK",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            var path=await CleaningClosureNotificationSource.ResolveAsync(db,id,user,source.NotificationType,
                source.SourceId.Value,source.CreatedBy ?? 0,now,ct);
            if(path==null)throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if (await CleaningClosureNotificationSource.ResolveAsync(db,id,user,source.NotificationType,
                source.SourceId.Value,source.CreatedBy ?? 0,clock.GetUtcNow().UtcDateTime,ct) != path)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new(path);
        }
        if(source is {NotificationType:"IR_CREATED",SourceType:"INTERNAL_REQUEST_INTAKE",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await InternalRequests.InternalRequestIntakeService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var intake=await InternalRequests.InternalRequestIntakeDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id&&r.RecipientId==user,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(intake==null||intake.ActorId!=source.CreatedBy||intake.At!=source.CreatedAt
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==intake.RequestId&&r.ReceivingDepartmentId==intake.DepartmentId,ct)
                ||!await new InternalRequests.InternalRequestProcessAuthority(db).AllowsDepartmentAsync(user,intake.BranchId,intake.DepartmentId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={intake.RequestId}");
        }
        if(source is {NotificationType:"SHIFT_HANDOVER",SourceType:"SHIFT_HANDOVER",SourceId:>0})
        {
            if(!await WorkTime.ShiftHandoverStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Handover source unavailable.");
            var receipt=await WorkTime.ShiftHandoverDelivery.Query(db).SingleOrDefaultAsync(n=>n.EventId==source.SourceId&&n.NotificationId==id&&n.RecipientId==user,ct);
            if(receipt==null||receipt.ActorId!=source.CreatedBy||receipt.At!=source.CreatedAt
                ||!await WorkTime.ShiftHandoverDelivery.CanReadAsync(db,receipt,now,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Handover source unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await WorkTime.ShiftHandoverDelivery.CanReadAsync(db,receipt,clock.GetUtcNow().UtcDateTime,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Handover source unavailable.");
            await tx.CommitAsync(ct);return new($"/workday/handovers/{receipt.HandoverId}");
        }
        if(source is {NotificationType:"IR_HANDOVER",SourceType:"INTERNAL_REQUEST_HANDOVER",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await InternalRequests.InternalRequestHandoverStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var handover=await InternalRequests.InternalRequestHandoverDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id&&r.RecipientId==user,ct);
            if(handover==null||handover.ActorId!=source.CreatedBy||handover.At!=source.CreatedAt
                ||!await InternalRequests.InternalRequestHandoverDelivery.CanReadAsync(db,handover,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await InternalRequests.InternalRequestHandoverDelivery.CanReadAsync(db,handover,clock.GetUtcNow().UtcDateTime,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new(handover.Kind=="MANAGER"
                ?$"/internal-requests?requestId={handover.RequestId}"
                :$"/internal-requests/{handover.RequestId}/handovers/{handover.HandoverId}");
        }
        if(source is {NotificationType:"IR_WITHDRAWN",SourceType:"INTERNAL_REQUEST_WITHDRAWAL",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await InternalRequests.InternalRequestWithdrawalService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var withdrawal=await InternalRequests.InternalRequestWithdrawalDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id&&r.RecipientId==user,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(withdrawal==null||withdrawal.ActorId!=source.CreatedBy||withdrawal.At!=source.CreatedAt
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==withdrawal.RequestId&&r.ReceivingDepartmentId==withdrawal.DepartmentId,ct)
                ||!await new InternalRequests.InternalRequestProcessAuthority(db).AllowsDepartmentAsync(user,withdrawal.BranchId,withdrawal.DepartmentId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={withdrawal.RequestId}");
        }
        if(source is {NotificationType:"IR_ROUTED",SourceType:"INTERNAL_REQUEST_ROUTE",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await InternalRequests.InternalRequestRouteService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var route=await InternalRequests.InternalRequestRouteDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id&&r.RecipientId==user,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(route==null||route.ActorId!=source.CreatedBy||route.At!=source.CreatedAt
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==route.RequestId&&(route.IsRequester||r.ReceivingDepartmentId==route.DepartmentId),ct)
                ||!route.IsRequester&&!await new InternalRequests.InternalRequestProcessAuthority(db).AllowsDepartmentAsync(user,route.BranchId,route.DepartmentId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={route.RequestId}");
        }
        if(source is {NotificationType:"IR_ASSIGNED",SourceType:"INTERNAL_REQUEST_ASSIGNMENT",SourceId:>0})
        {
            if(!await InternalRequests.InternalRequestAssignmentService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var assignment=await InternalRequests.InternalRequestAssignmentDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id&&r.RecipientId==user,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(assignment==null||assignment.ActorId!=source.CreatedBy||assignment.At!=source.CreatedAt
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==assignment.RequestId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={assignment.RequestId}");
        }
        if(source is {NotificationType:"IR_REOPENED",SourceType:"INTERNAL_REQUEST_REOPEN",SourceId:>0})
        {
            await new Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await InternalRequests.InternalRequestReopenService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Reopen source unavailable.");
            var reopened=await InternalRequests.InternalRequestReopenDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId,ct);
            if(reopened==null||reopened.ActorId!=source.CreatedBy||reopened.At!=source.CreatedAt
                ||!await InternalRequests.InternalRequestReopenDelivery.CanReadAsync(db,reopened,user,clock.GetUtcNow().UtcDateTime,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Reopen source unavailable.");
            await RevalidateSessionAsync(user,ct);await tx.CommitAsync(ct);return new($"/internal-requests?requestId={reopened.RequestId}");
        }
        if(source is {NotificationType:"IR_CLOSED",SourceType:"INTERNAL_REQUEST_COMPLETION",SourceId:>0})
        {
            if(!await InternalRequests.InternalRequestCompletionService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var progress=await InternalRequests.InternalRequestCompletionDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(progress==null||progress.RequesterId!=user||progress.ActorId!=source.CreatedBy||progress.At!=source.CreatedAt
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==progress.RequestId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={progress.RequestId}");
        }
        if(source is {NotificationType:"IR_ESCALATED",SourceType:"INTERNAL_REQUEST_ESCALATION",SourceId:>0})
        {
            await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([user],ct);
            if(!await InternalRequests.InternalRequestEscalationService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var escalation=await InternalRequests.InternalRequestEscalationDelivery.Query(db)
                .SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id&&r.RecipientId==user,ct);
            if(escalation==null||escalation.ActorId!=source.CreatedBy||escalation.At!=source.CreatedAt
                ||!await InternalRequests.InternalRequestEscalationDelivery.CanReadAsync(db,escalation,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={escalation.RequestId}");
        }
        if(source is {NotificationType:"IR_READY",SourceType:"INTERNAL_REQUEST_PROGRESS",SourceId:>0})
        {
            if(!await InternalRequests.InternalRequestProgressService.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var progress=await InternalRequests.InternalRequestProgressDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(progress==null||progress.RequesterId!=user||progress.ActorId!=source.CreatedBy||progress.At!=source.CreatedAt
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==progress.RequestId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={progress.RequestId}");
        }
        if(source is {NotificationType:"IR_ACCEPTED" or "IR_REJECTED",SourceType:"INTERNAL_REQUEST_DECISION",SourceId:>0})
        {
            if(!await InternalRequests.InternalRequestDecisionStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var decision=await InternalRequests.InternalRequestDecisionDelivery.Query(db).SingleOrDefaultAsync(r=>r.EventId==source.SourceId&&r.NotificationId==id,ct);
            var scope=await new InternalRequests.InternalRequestReadAccess(db).ReadAsync(user,now,ct);
            if(decision==null||decision.RequesterId!=user||decision.ActorId!=source.CreatedBy||decision.At!=source.CreatedAt
                ||source.NotificationType!=(decision.Decision=="ACCEPT"?"IR_ACCEPTED":"IR_REJECTED")
                ||!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==decision.RequestId,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/internal-requests?requestId={decision.RequestId}");
        }
        if(source is {NotificationType:"TASK_VERIFIED" or "TASK_RETURNED",SourceType:"TASK_VERIFICATION",SourceId:>0})
        {
            if(!await TaskVerificationStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var decision=await TaskVerificationStore.Query(db).SingleOrDefaultAsync(v=>v.SubmissionId==source.SourceId,ct);
            if(decision==null||decision.SubmittedBy!=user||decision.VerifiedBy!=source.CreatedBy||decision.DecidedAt!=source.CreatedAt
                ||source.NotificationType!=(decision.Decision=="ACCEPT"?"TASK_VERIFIED":"TASK_RETURNED")
                ||!await TaskSubmissionStore.Query(db).AnyAsync(s=>s.ProgressId==decision.SubmissionId,ct)
                ||!await TaskVerificationStore.ExecutorReadAsync(db,user,decision.BranchId,decision.TaskId,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/operational-tasks?branchId={decision.BranchId}&taskId={decision.TaskId}");
        }
        if(source is {NotificationType:TaskSubmissionStore.NotificationKind,SourceType:"TASK_SUBMISSION",SourceId:>0})
        {
            if(!await TaskSubmissionStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var submission=await TaskSubmissionStore.Query(db).SingleOrDefaultAsync(s=>s.ProgressId==source.SourceId,ct);
            if(submission==null||submission.SubmittedBy!=source.CreatedBy||submission.SubmittedAt!=source.CreatedAt
                ||user==submission.SubmittedBy||!(System.Text.Json.JsonSerializer.Deserialize<long[]>(submission.RecipientsJson)??[]).Contains(user)
                ||!(await TaskSubmissionAccess.ManagersAsync(db,submission.BranchId,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operational-tasks?branchId={submission.BranchId}&taskId={submission.TaskId}");
        }
        if(source is {NotificationType:TaskReopenStore.NotificationKind,SourceType:"TASK_REOPEN",SourceId:>0})
        {
            if(!await TaskReopenStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var reopened=await TaskReopenStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==source.SourceId,ct);
            if(reopened==null||reopened.AssigneeId!=user||reopened.ReopenedBy!=source.CreatedBy||reopened.ReopenedAt!=source.CreatedAt
                ||!await TaskVerificationStore.ExecutorReadAsync(db,user,reopened.BranchId,reopened.TaskId,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/operational-tasks?branchId={reopened.BranchId}&taskId={reopened.TaskId}");
        }
        if(source is {NotificationType:"TASK_SERIES_STOPPED",SourceType:"TASK_SERIES",SourceId:>0})
        {
            if(!await TaskSeriesStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var stopped=await TaskSeriesNotificationStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==source.SourceId,ct);
            if(stopped==null||stopped.RecipientId!=user||stopped.ActorId!=source.CreatedBy||stopped.At!=source.CreatedAt
                ||!await TaskVerificationStore.ExecutorReadAsync(db,user,stopped.BranchId,stopped.TaskId,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/operational-tasks?branchId={stopped.BranchId}&taskId={stopped.TaskId}");
        }
        if(source is {NotificationType:AssetHandoverSource.Kind,SourceType:AssetHandoverSource.SourceType,SourceId:>0})
        {
            var handover=await AssetHandoverSource.ReadAsync(db,source.SourceId.Value,ct);
            if(handover==null||handover.ActorId!=source.CreatedBy||handover.At!=source.CreatedAt
                ||!await AssetHandoverSource.CanReadAsync(db,user,handover,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await AssetHandoverSource.CanReadAsync(db,user,handover,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/notifications/{id}/asset-handover");
        }
        if(source is {NotificationType:IncidentAssetFollowupSource.Kind,SourceType:IncidentAssetFollowupSource.SourceType,SourceId:>0})
        {
            var asset=await IncidentAssetFollowupSource.ReadAsync(db,source.SourceId.Value,ct);
            if(asset==null||asset.ActorId!=source.CreatedBy||asset.At!=source.CreatedAt
                ||!await IncidentAssetFollowupSource.CanReadAsync(db,user,asset,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await IncidentAssetFollowupSource.CanReadAsync(db,user,asset,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new($"/notifications/{id}/asset-condition");
        }
        if(source is {NotificationType:IncidentRoomBlockSource.Kind,SourceType:IncidentRoomBlockSource.SourceType,SourceId:>0})
        {
            var released=await IncidentRoomBlockSource.ReadAsync(db,source.SourceId.Value,ct);
            if(released==null||released.ActorId!=source.CreatedBy||released.At!=source.CreatedAt
                ||!await IncidentRoomBlockSource.CanReadAsync(db,user,released,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await IncidentRoomBlockSource.CanReadAsync(db,user,released,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/rooms/{released.RoomId}?branchId={released.BranchId}");
        }
        if(source is {NotificationType:IncidentRoomReleaseSource.Kind,SourceType:IncidentRoomReleaseSource.SourceType,SourceId:>0})
        {
            var released=await IncidentRoomReleaseSource.ReadAsync(db,source.SourceId.Value,ct);
            if(released==null||released.ActorId!=source.CreatedBy||released.At!=source.CreatedAt
                ||!await IncidentRoomReleaseSource.CanReadAsync(db,user,released,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await IncidentRoomReleaseSource.CanReadAsync(db,user,released,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/rooms/{released.RoomId}?branchId={released.BranchId}");
        }
        if(source is {NotificationType:MaintenanceClosureSource.Kind,SourceType:MaintenanceClosureSource.SourceType,SourceId:>0})
        {
            var closure=await MaintenanceClosureSource.ReadHistoricalAsync(db,source.SourceId.Value,ct);
            if(closure==null||closure.ActorId!=source.CreatedBy||closure.At!=source.CreatedAt)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var target=await MaintenanceClosureSource.TargetAsync(db,user,closure,ct);
            await RevalidateSessionAsync(user,ct);
            if(target==null||target!=await MaintenanceClosureSource.TargetAsync(db,user,closure,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);return new(target);
        }
        if(source is {NotificationType:MaintenanceAssignmentSource.Kind,SourceType:MaintenanceAssignmentSource.SourceType,SourceId:>0})
        {
            var receipt=await MaintenanceAssignmentSource.ReadAsync(db,source.SourceId.Value,ct);
            if(receipt==null || receipt.ActorId!=source.CreatedBy || receipt.OccurredAt!=source.CreatedAt)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source is unavailable.");
            var target=await MaintenanceAssignmentSource.TargetAsync(db,user,receipt,ct);
            await RevalidateSessionAsync(user,ct);
            if(target==null || target!=await MaintenanceAssignmentSource.TargetAsync(db,user,receipt,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Assignment source is unavailable.");
            await tx.CommitAsync(ct);return new(target);
        }
        if(source is {NotificationType:MaintenanceDeclineSource.Kind,SourceType:MaintenanceDeclineSource.SourceType,SourceId:>0})
        {
            var returned=await MaintenanceDeclineSource.ReadAsync(db,source.SourceId.Value,ct);
            if(returned==null||returned.ActorId!=source.CreatedBy||returned.At!=source.CreatedAt
                ||!await MaintenanceDeclineSource.CanReadAsync(db,user,returned,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await MaintenanceDeclineSource.CanReadAsync(db,user,returned,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operations/maintenance/{returned.WorkId}?action=A004A-036");
        }
        if(source is {NotificationType:MaintenanceReturnSource.Kind,SourceType:MaintenanceReturnSource.SourceType,SourceId:>0})
        {
            var returned=await MaintenanceReturnSource.ReadAsync(db,source.SourceId.Value,ct);
            if(returned==null||returned.ActorId!=source.CreatedBy||returned.At!=source.CreatedAt
                ||!await MaintenanceReturnSource.CanReadAsync(db,user,returned,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await MaintenanceReturnSource.CanReadAsync(db,user,returned,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operations/maintenance/{returned.WorkId}?action=A004A-036");
        }
        if(source is {NotificationType:MaintenanceSubmissionSource.Kind,SourceType:MaintenanceSubmissionSource.SourceType,SourceId:>0})
        {
            var submission=await MaintenanceSubmissionSource.ReadAsync(db,source.SourceId.Value,ct);
            if(submission==null||submission.ActorId!=source.CreatedBy||submission.At!=source.CreatedAt
                ||!await MaintenanceSubmissionSource.CanReviewAsync(db,user,submission,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await MaintenanceSubmissionSource.CanReviewAsync(db,user,submission,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operations/maintenance/{submission.WorkId}?action=A004A-036");
        }
        if(source is {NotificationType:"WORKDAY_UNSCHEDULED",SourceType:"UNSCHEDULED_WORKDAY",SourceId:>0})
        {
            if(!await WorkTime.WorkdayMembershipStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var day=await db.Workdays.AsNoTracking().SingleOrDefaultAsync(d=>d.WorkdayId==source.SourceId&&d.IsUnscheduled,ct);
            if(day==null||day.UserId!=source.CreatedBy||day.StartedAt!=source.CreatedAt
                ||!await db.Database.SqlQuery<int>($"SELECT 1 AS Value FROM dbo.unscheduled_workday_notifications WHERE workday_id={day.WorkdayId} AND notification_id={id} AND status='DELIVERED'").AnyAsync(ct)
                ||!await WorkTime.UnscheduledWorkdayAccess.CanReadAsync(db,user,day.BranchId,day.WorkdayId,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            if(!await WorkTime.UnscheduledWorkdayAccess.CanReadAsync(db,user,day.BranchId,day.WorkdayId,clock.GetUtcNow().UtcDateTime,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/workday?branchId={day.BranchId}&unscheduledId={day.WorkdayId}");
        }
        if(source is {NotificationType:"TASK_PLANNED" or "TASK_CANCELLED",SourceType:"TASK_PLAN",SourceId:>0})
        {
            if(!await TaskPlanningStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var plan=await TaskPlanNotificationStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==source.SourceId,ct);
            if(plan==null||plan.ActorId!=source.CreatedBy||plan.At!=source.CreatedAt||plan.NotificationKind!=source.NotificationType
                ||user!=plan.RecipientId||!await TaskVerificationStore.ExecutorReadAsync(db,user,plan.BranchId,plan.TaskId,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operational-tasks?branchId={plan.BranchId}&taskId={plan.TaskId}");
        }
        if(source is {NotificationType:TaskAssignmentStore.NotificationKind,SourceType:"TASK_ASSIGNMENT",SourceId:>0})
        {
            if(!await TaskDeclineStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var assignment=await TaskAssignmentStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==source.SourceId,ct);
            if(assignment==null||assignment.AssignedBy!=source.CreatedBy||assignment.AssignedAt!=source.CreatedAt
                ||(user!=assignment.AssigneeId&&user!=assignment.PreviousAssigneeId)
                ||!await TaskVerificationStore.ExecutorReadAsync(db,user,assignment.BranchId,assignment.TaskId,now,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operational-tasks?branchId={assignment.BranchId}&taskId={assignment.TaskId}");
        }
        if(source is {NotificationType:TaskDeclineStore.NotificationKind,SourceType:"TASK_DECLINE",SourceId:>0})
        {
            if(!await TaskDeclineStore.InstalledAsync(db,ct))throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var submission=await TaskDeclineStore.Query(db).SingleOrDefaultAsync(s=>s.AuditId==source.SourceId,ct);
            if(submission==null||submission.DeclinedBy!=source.CreatedBy||submission.DeclinedAt!=source.CreatedAt
                ||user==submission.DeclinedBy||!(System.Text.Json.JsonSerializer.Deserialize<long[]>(submission.RecipientsJson)??[]).Contains(user)
                ||!(await TaskSubmissionAccess.ManagersAsync(db,submission.BranchId,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/operational-tasks?branchId={submission.BranchId}&taskId={submission.TaskId}");
        }
        if(source is {NotificationType:InspectionFailedNotificationSource.Kind,SourceType:"INSPECTION",SourceId:>0})
        {
            var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="INSPECTION"&&a.EntityId==source.SourceId.Value.ToString()&&a.Action=="UPDATE").ToArrayAsync(ct);
            var candidates=audits.Where(a=>InspectionFailedNotificationSource.Read(a.NewValues)!=null).ToArray();
            var verified=candidates.Length==1?await InspectionFailedNotificationSource.VerifyAsync(db,candidates[0],ct):default;
            var proof=verified.Source;
            if(proof==null||proof.ActorId!=source.CreatedBy||candidates[0].OccurredAt!=source.CreatedAt
                ||!proof.Intent.Recipients.Contains(user)
                ||!await InspectionRecipientReadAccess.CanReadAsync(db,user,proof.BranchId,now,ct)
                ||(user!=proof.Intent.OriginalCleaner.CleanerUserId
                    &&!(await InspectionFailureRecipients.SupervisorsAsync(db,proof.BranchId,now,ct)).Contains(user)))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await RevalidateSessionAsync(user,ct);
            var finalNow=clock.GetUtcNow().UtcDateTime;
            if(!await InspectionRecipientReadAccess.CanReadAsync(db,user,proof.BranchId,finalNow,ct)
                ||(user!=proof.Intent.OriginalCleaner.CleanerUserId
                    &&!(await InspectionFailureRecipients.SupervisorsAsync(db,proof.BranchId,finalNow,ct)).Contains(user)))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/inspection-history/{proof.InspectionId}?branchId={proof.BranchId}");
        }
        if(source is {NotificationType:InspectionReassignmentSource.Kind,SourceType:"INSPECTION_ASSIGNMENT",SourceId:>0})
        {
            var audit=await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a=>a.AuditId==source.SourceId,ct);
            var proof=audit==null?null:await InspectionReassignmentSource.VerifyAsync(db,audit,ct);
            if(proof==null||proof.ActorUserId!=source.CreatedBy||audit!.OccurredAt!=source.CreatedAt
                ||proof.Notification.RecipientUserId!=user
                ||!await db.RoomInspections.AsNoTracking().AnyAsync(i=>i.InspectionId==proof.InspectionId&&i.InspectorUserId==user&&i.Status!="CANCELLED",ct)
                ||!(await InspectionFailureRecipients.SupervisorsAsync(db,proof.BranchId,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/inspections/{proof.BranchId}/{proof.InspectionId}/execution");
        }
        if(source is {NotificationType:InitialInspectionAssignmentSource.Kind,SourceType:"INSPECTION",SourceId:>0})
        {
            var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="INSPECTION_ASSIGNMENT"&&a.EntityId==source.SourceId.Value.ToString()&&a.Action=="CREATE").ToArrayAsync(ct);
            var proof=audits.Length==1?await InitialInspectionAssignmentSource.VerifyAsync(db,audits[0],ct):null;
            if(proof==null||proof.ActorUserId!=source.CreatedBy||audits[0].OccurredAt!=source.CreatedAt
                ||proof.Notification.RecipientUserId!=user
                ||!await db.RoomInspections.AsNoTracking().AnyAsync(i=>i.InspectionId==proof.InspectionId&&i.InspectorUserId==user&&i.Status!="CANCELLED",ct)
                ||!(await InspectionFailureRecipients.SupervisorsAsync(db,proof.BranchId,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/inspections/{proof.BranchId}/{proof.InspectionId}/execution");
        }
        if(source is {NotificationType:ReinspectionReassignmentSource.Kind,SourceType:"REINSPECTION_ASSIGNMENT",SourceId:>0})
        {
            var audit=await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a=>a.AuditId==source.SourceId,ct);
            var proof=audit==null?null:await ReinspectionReassignmentSource.VerifyAsync(db,audit,ct);
            if(proof==null||proof.ActorUserId!=source.CreatedBy||audit!.OccurredAt!=source.CreatedAt||proof.Notification.RecipientUserId!=user
                ||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_responsibilities_v1 a JOIN dbo.room_inspections i ON i.inspection_id=a.inspection_id WHERE a.inspection_id={proof.InspectionId} AND a.inspector_user_id={user} AND i.status<>'CANCELLED'").SingleAsync(ct)!=1
                ||!(await InspectionFailureRecipients.SupervisorsAsync(db,proof.BranchId,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/reinspections/{proof.BranchId}/{proof.InspectionId}/execution");
        }
        if(source is {NotificationType:ReinspectionAssignedNotificationSource.Kind,SourceType:"INSPECTION",SourceId:>0})
        {
            var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="REINSPECTION"&&a.EntityId==source.SourceId.Value.ToString()&&a.Action=="CREATE").ToArrayAsync(ct);
            var proof=audits.Length==1?await ReinspectionAssignedNotificationSource.VerifyAsync(db,audits[0],ct):null;
            if(proof==null||proof.ActorUserId!=source.CreatedBy||audits[0].OccurredAt!=source.CreatedAt
                ||proof.Notification.RecipientUserId!=user
                ||!(await InspectionFailureRecipients.SupervisorsAsync(db,proof.BranchId,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/reinspections/{proof.BranchId}/{proof.InspectionId}/execution");
        }
        if(source is {NotificationType:ReworkSubmittedNotificationSource.Kind,SourceType:"CLEANING_REWORK",SourceId:>0})
        {
            var audits=await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM dbo.audit_logs WHERE entity_type='cleaning_reworks' AND action='UPDATE'
                  AND entity_id=CONVERT(nvarchar(50),{source.SourceId.Value})
                  AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.contract')='FE04_REWORK_SUBMISSION_V1'
                """).AsNoTracking().ToArrayAsync(ct);
            if(audits.Length!=1||audits[0].ActorUserId!=source.CreatedBy||audits[0].OccurredAt!=source.CreatedAt
                ||await ReworkSubmittedNotificationSource.VerifyAsync(db,audits[0],ct)!="VALID"
                ||!(await InspectionFailureRecipients.SupervisorsAsync(db,audits[0].BranchId!.Value,now,ct)).Contains(user))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            await tx.CommitAsync(ct);
            return new($"/inspections/reworks/{source.SourceId.Value}/assign?branchId={audits[0].BranchId!.Value}");
        }
        if(source is not {NotificationType:"REWORK_ASSIGNED" or "REINSPECTION_PASSED",SourceType:"CLEANING_REWORK",SourceId:>0})
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        var branch=await db.CleaningReworks.AsNoTracking().Where(w=>w.ReworkId==source.SourceId&&w.AssigneeUserId==user
            &&w.CleaningTask.Room.IsActive&&w.CleaningTask.Room.Floor.IsActive
            &&(w.CleaningTask.Room.AreaId==null||w.CleaningTask.Room.Area!.IsActive))
            .Select(w=>(long?)w.CleaningTask.Room.BranchId).SingleOrDefaultAsync(ct);
        if(branch==null||!(await ReworkRecipientReadAccess.EvaluateAsync(db,user,branch.Value,source.SourceId.Value,"CLEANING_VIEW",now,ct)).IsGranted)
            throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        if(source.NotificationType==ReinspectionPassedNotificationSource.Kind)
        {
            if(!await HotelOps.Api.Services.WorkTime.WorkdayMembershipStore.InstalledAsync(db,ct)
                ||!await ReworkRecipientReadAccess.EligibleAssignees(db,branch.Value,now).AnyAsync(u=>u.UserId==user,ct))
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
            var audits=await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM dbo.audit_logs WHERE entity_type='REINSPECTION' AND action='UPDATE' AND branch_id={branch.Value}
                  AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.reworkId'))={source.SourceId.Value}
                  AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.notification.kind')='REINSPECTION_PASSED'
                """).AsNoTracking().ToArrayAsync(ct);
            var proof=audits.Length==1?await ReinspectionPassedNotificationSource.VerifyAsync(db,audits[0],ct):null;
            if(proof==null||proof.Notification.RecipientUserId!=user||proof.ReworkId!=source.SourceId.Value||proof.BranchId!=branch)
                throw new InsightException(404,"SOURCE_UNAVAILABLE","Notification source is unavailable.");
        }
        await tx.CommitAsync(ct);
        // Never redirect to stored/user-provided ActionUrl. Target API authenticates source again.
        return new($"/cleaning/reworks/{source.SourceId.Value}?branchId={branch.Value}");
    }
    private static DateTimeOffset Utc(DateTime value)=>new(DateTime.SpecifyKind(value,DateTimeKind.Utc));
}
public sealed record NotificationSource(string Path);
