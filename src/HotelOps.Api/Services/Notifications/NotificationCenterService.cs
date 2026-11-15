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
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
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
        await tx.CommitAsync(ct);
    }
    public async Task<NotificationSource> ResolveSourceAsync(long id,CancellationToken ct)
    {
        if(id<=0)throw new InsightException(400,"INVALID_ID","Invalid notification identifier.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=clock.GetUtcNow().UtcDateTime;var user=await UserAsync(now,ct);
        var source=await Inbox(user,now).AsNoTracking().Where(r=>r.NotificationId==id)
            .Select(r=>new{r.Notification.NotificationType,r.Notification.SourceType,r.Notification.SourceId,r.Notification.CreatedBy,r.Notification.CreatedAt}).SingleOrDefaultAsync(ct);
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
            if(!await ReworkRecipientReadAccess.EligibleAssignees(db,branch.Value,now).AnyAsync(u=>u.UserId==user,ct))
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
