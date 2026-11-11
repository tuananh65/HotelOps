using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;
public sealed record CleaningInboxItem(Guid PublicationId,long IntentId,long NotificationId,string Title,string Message,
    string ActionPath,DateTime CreatedAt,DateTime? ReadAt,string RowVersion);
public sealed record CleaningInboxPage(IReadOnlyList<CleaningInboxItem> Items,int TotalCount,int UnreadCount);
public interface ICleaningNotificationRuntime
{
    Task<int> RunContainedBatchAsync(CancellationToken ct);
    Task<CleaningInboxPage> ListAsync(long user,int page,int size,CancellationToken ct);
    Task<CleaningInboxItem?> GetAsync(long user,Guid publication,CancellationToken ct);
    Task<CleaningInboxItem?> SetReadAsync(long user,Guid publication,NotificationReadRequest request,CancellationToken ct);
}

// Existing FS013 intents/inbox, not another worker, queue or publisher. Explicit invocation only.
public sealed class CleaningNotificationRuntime(HotelOpsDbContext db,ICleaningAssignmentStore store,
    ICleaningNotificationActivation activation,ICleaningActorAuthority authority,TimeProvider clock,ICleaningCommonTargetAdmission? commonTarget=null):ICleaningNotificationRuntime
{
    private const string Title="Cleaning task assigned",Message="A cleaning task has been assigned to you.";
    private async Task RequireClean(string use,CancellationToken ct)
    {if(db.Database.CurrentTransaction is not null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Clean runtime context required.");
     var connection=db.Database.GetDbConnection();
     if(connection.DataSource==IntegrationSqlGuard.Server&&connection.Database==IntegrationSqlGuard.Database)
         IntegrationSqlGuard.ValidateConnectionString(connection.ConnectionString,requirePassword:false);
     else {if(commonTarget is null)throw new NotificationContractUnavailableException();await commonTarget.RequireRuntimeCleanAsync(db,use,ct);}}
    private async Task LockScopeAsync(long user,IEnumerable<long> branches,CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([user],ct);
        foreach(var branch in branches.Distinct().Order())
            _=await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}").AsNoTracking().SingleOrDefaultAsync(ct);
        await CleaningActorAuthority.LockCatalogAsync(db,ct);
    }
    public async Task<int> RunContainedBatchAsync(CancellationToken ct)
    {
        await activation.RequireAsync(CleaningNotificationCapability.ContainedDelivery,ct);await RequireClean("CONTAINED_DELIVERY",ct);
        // No startup or timer calls this method. Gate must include current lease, fixtures and target approval.
        List<IntentRow> ids;
        await using(var scan=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {await store.EnsureAsync(ct);ids=await Candidates(null,null,true,ct);await scan.RollbackAsync(ct);}
        var delivered=0;
        foreach(var hint in ids.Take(50))
        {
            try
            {
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                await store.EnsureAsync(ct);await LockScopeAsync(hint.UserId,[hint.BranchId],ct);
                var row=await db.Database.SqlQuery<IntentRow>($"""
                    SELECT i.intent_id AS IntentId,i.user_id AS UserId,i.status AS Status,i.inbox_notification_id AS NotificationId,
                      i.row_version AS Revision,p.publication_id AS PublicationId,p.actor_user_id AS ActorId,
                      p.operation_key AS OperationId,o.fingerprint AS Fingerprint,
                      p.assignment_id AS AssignmentId,TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId')) AS BranchId,
                      TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId')) AS TaskId
                    FROM dbo.notification_delivery_intents i WITH(UPDLOCK,HOLDLOCK)
                    JOIN dbo.notification_publications p WITH(HOLDLOCK) ON p.publication_id=i.publication_id
                    JOIN dbo.notification_operation_reservations o WITH(HOLDLOCK) ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
                    WHERE i.intent_id={hint.IntentId} AND i.status='PENDING' AND i.delivery_channel='IN_APP'
                      AND i.next_attempt_at<={clock.GetUtcNow().UtcDateTime} AND p.event_type='CLEANING_TASK_ASSIGNED'
                    """).SingleOrDefaultAsync(ct);
                if(row is null)continue;
                if(row.UserId!=hint.UserId||row.BranchId!=hint.BranchId)throw new NotificationConflictException();
                var state=await store.SourceAsync(row.AssignmentId,ct);
                var disposition=state is null?CleaningSourceDisposition.Stale:CleaningAssignmentProfile.Current(state,true);
                if(state is not null)
                {
                    var graph=await store.GraphAsync(row.ActorId,row.OperationId,ct);
                    var e=CleaningAssignmentProfile.Verify(graph,Command(row));
                    if(disposition==CleaningSourceDisposition.Current)
                    {
                        CleaningAssignmentProfile.VerifySource(e,state,false);
                        if(!await store.CanReadAsync(state,row.UserId,ct))disposition=CleaningSourceDisposition.Denied;
                    }
                }
                if(disposition!=CleaningSourceDisposition.Current)
                {
                    var reason=disposition==CleaningSourceDisposition.Stale?"SOURCE_SUPERSEDED_OR_CANCELLED":"SOURCE_ACCESS_REVOKED";
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='TERMINAL',failure_code={reason},attempt_count=attempt_count+1 WHERE intent_id={row.IntentId} AND status='PENDING'",ct);
                }
                else
                {
                    var s=state!;var now=clock.GetUtcNow().UtcDateTime;
                    var notice=new Notification{NotificationType=CleaningAssignmentProfile.EventType,Title=Title,Message=Message,Priority="NORMAL",
                        SourceType="CLEANING_TASK",SourceId=s.TaskId,ActionUrl=CleaningAssignmentProfile.ActionPath(s),CreatedBy=s.ActorId,CreatedAt=now};
                    db.Notifications.Add(notice);await db.SaveChangesAsync(ct);
                    db.NotificationRecipients.Add(new(){NotificationId=notice.NotificationId,UserId=row.UserId,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=now});
                    await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                    if(!await store.CanReadAsync(s,row.UserId,ct))throw new UnauthorizedAccessException();
                    var affected=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='DELIVERED',inbox_notification_id={notice.NotificationId},failure_code=NULL,attempt_count=attempt_count+1 WHERE intent_id={row.IntentId} AND status='PENDING'",ct);
                    if(affected!=1)throw new NotificationConflictException();
                }
                await activation.RequireAsync(CleaningNotificationCapability.ContainedDelivery,ct);
                await tx.CommitAsync(ct);if(disposition==CleaningSourceDisposition.Current)delivered++;
            }
            catch(OperationCanceledException){throw;}
            catch(Exception ex)when(!NotificationDeliveryFailures.IsDeadlock(ex))
            {
                db.ChangeTracker.Clear();
                var retry=NotificationDeliveryFailures.IsRetryable(ex);
                // Transaction disposal rolled back partial inbox inserts. Infrastructure is not denial.
                var code=retry?"DELIVERY_UNAVAILABLE":"DELIVERY_REQUIRES_REVIEW";
                var next=retry?clock.GetUtcNow().UtcDateTime.AddMinutes(5):DateTime.MaxValue;
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET failure_code={code},next_attempt_at={next},attempt_count=attempt_count+1 WHERE intent_id={hint.IntentId} AND status='PENDING'",ct);
                if(!retry)throw;
            }
            finally{db.ChangeTracker.Clear();}
        }
        return delivered;
    }
    public async Task<CleaningInboxPage> ListAsync(long user,int page,int size,CancellationToken ct)
    {
        if(user<=0||page<1||size is <1 or >100||page>int.MaxValue/size)throw new ArgumentException("Invalid inbox page.");
        await activation.RequireAsync(CleaningNotificationCapability.Inbox,ct);await RequireClean("INBOX_READ",ct);
        var hints=await ReadHintsAsync(user,null,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await store.EnsureAsync(ct);await new UserEligibilityLock(db).LockAsync([user],ct);await authority.RequireViewerAsync(user,ct);
        await LockScopeAsync(user,hints.Select(x=>x.BranchId),ct);
        var rows=await Candidates(user,null,false,ct);RequireKnownScopes(hints,rows);
        var visible=new List<CleaningInboxItem>();
        foreach(var row in rows)if(await VisibleAsync(user,row,ct) is {} item)visible.Add(item);
        await authority.RequireViewerAsync(user,ct);await tx.CommitAsync(ct);
        return new(visible.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.IntentId).Skip((page-1)*size).Take(size).ToArray(),visible.Count,visible.Count(x=>x.ReadAt is null));
    }
    public async Task<CleaningInboxItem?> GetAsync(long user,Guid publication,CancellationToken ct)
    {
        await activation.RequireAsync(CleaningNotificationCapability.Inbox,ct);await RequireClean("INBOX_READ",ct);
        if(user<=0||publication==Guid.Empty)throw new ArgumentException("Invalid inbox identity.");
        var hints=await ReadHintsAsync(user,publication,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await store.EnsureAsync(ct);await new UserEligibilityLock(db).LockAsync([user],ct);await authority.RequireViewerAsync(user,ct);
        await LockScopeAsync(user,hints.Select(x=>x.BranchId),ct);
        var rows=await Candidates(user,publication,false,ct);RequireKnownScopes(hints,rows);
        var item=rows.Count==1?await VisibleAsync(user,rows[0],ct):null;
        await authority.RequireViewerAsync(user,ct);await tx.CommitAsync(ct);return item;
    }
    public async Task<CleaningInboxItem?> SetReadAsync(long user,Guid publication,NotificationReadRequest request,CancellationToken ct)
    {
        byte[] version;try{version=Convert.FromBase64String(request.ExpectedRowVersion);}catch(Exception e)when(e is FormatException or ArgumentNullException){throw new ArgumentException("Invalid version.");}
        if(version.Length!=8||user<=0||publication==Guid.Empty)throw new ArgumentException("Invalid read command.");
        await activation.RequireAsync(CleaningNotificationCapability.Inbox,ct);await RequireClean("INBOX_WRITE",ct);
        var hints=await ReadHintsAsync(user,publication,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await store.EnsureAsync(ct);await new UserEligibilityLock(db).LockAsync([user],ct);await authority.RequireViewerAsync(user,ct);
        await LockScopeAsync(user,hints.Select(x=>x.BranchId),ct);
        var rows=await Candidates(user,publication,false,ct);RequireKnownScopes(hints,rows);
        if(rows.Count!=1||await VisibleAsync(user,rows[0],ct) is not {} item)return null;
        var row=rows[0];
        var affected=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET next_attempt_at=next_attempt_at WHERE intent_id={row.IntentId} AND user_id={user} AND status='DELIVERED' AND row_version={version}",ct);
        if(affected!=1)throw new NotificationConflictException();
        DateTime? readAt=request.IsRead?clock.GetUtcNow().UtcDateTime:null;
        affected=await db.NotificationRecipients.Where(r=>r.NotificationId==item.NotificationId&&r.UserId==user&&r.DeliveryChannel=="IN_APP")
            .ExecuteUpdateAsync(set=>set.SetProperty(r=>r.ReadAt,readAt),ct);
        if(affected!=1)throw new NotificationConflictException();
        await authority.RequireViewerAsync(user,ct);var refreshed=(await Candidates(user,publication,false,ct)).Single();
        var result=await VisibleAsync(user,refreshed,ct)??throw new UnauthorizedAccessException();
        await tx.CommitAsync(ct);return result;
    }
    // Discovery is a separate rolled-back read transaction. Never retain graph/range locks
    // while waiting for Branch/catalog locks required by the authoritative FE04 writer.
    private async Task<List<IntentRow>> ReadHintsAsync(long user,Guid? publication,CancellationToken ct)
    {
        await using var scan=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await store.EnsureAsync(ct);await new UserEligibilityLock(db).LockAsync([user],ct);await authority.RequireViewerAsync(user,ct);
        var hints=await Candidates(user,publication,false,ct);
        await scan.RollbackAsync(CancellationToken.None);return hints;
    }
    private static void RequireKnownScopes(List<IntentRow> hints,List<IntentRow> rows)
    {if(rows.Any(r=>!hints.Any(h=>h.BranchId==r.BranchId)))throw new NotificationContractUnavailableException();}
    private async Task<CleaningInboxItem?> VisibleAsync(long user,IntentRow row,CancellationToken ct)
    {
        if(row.UserId!=user||row.Status!="DELIVERED"||row.NotificationId is null)return null;
        var s=await store.SourceAsync(row.AssignmentId,ct);
        if(s is null||CleaningAssignmentProfile.Current(s,true)!=CleaningSourceDisposition.Current||!await store.CanReadAsync(s,user,ct))return null;
        var graph=await store.GraphAsync(row.ActorId,row.OperationId,ct);var e=CleaningAssignmentProfile.Verify(graph,Command(row));
        CleaningAssignmentProfile.VerifySource(e,s,false);
        var notice=await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n=>n.NotificationId==row.NotificationId,ct);
        if(notice is null||notice.ExpiresAt is {} expiry&&expiry<=clock.GetUtcNow().UtcDateTime)return null;
        if(notice.NotificationType!=CleaningAssignmentProfile.EventType||notice.SourceType!="CLEANING_TASK"||notice.SourceId!=s.TaskId||notice.ActionUrl!=CleaningAssignmentProfile.ActionPath(s))throw new NotificationConflictException();
        var recipient=await db.NotificationRecipients.AsNoTracking().SingleOrDefaultAsync(r=>r.NotificationId==notice.NotificationId&&r.UserId==user&&r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED",ct);
        return recipient is null?null:new(row.PublicationId,row.IntentId,notice.NotificationId,Title,Message,CleaningAssignmentProfile.ActionPath(s),notice.CreatedAt,recipient.ReadAt,Convert.ToBase64String(row.Revision));
    }
    private static CleaningAssignmentCommand Command(IntentRow r)=>new(r.ActorId,r.BranchId,r.TaskId,r.UserId,r.OperationId,Convert.ToHexString(r.Fingerprint));
    private async Task<List<IntentRow>> Candidates(long? user,Guid? publication,bool pending,CancellationToken ct)=>await db.Database.SqlQuery<IntentRow>($"""
        SELECT i.intent_id AS IntentId,i.user_id AS UserId,i.status AS Status,i.inbox_notification_id AS NotificationId,
          i.row_version AS Revision,p.publication_id AS PublicationId,p.actor_user_id AS ActorId,p.operation_key AS OperationId,
          o.fingerprint AS Fingerprint,p.assignment_id AS AssignmentId,
          TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId')) AS BranchId,
          TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId')) AS TaskId
        FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
        JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
        WHERE p.event_type='CLEANING_TASK_ASSIGNED' AND p.schema_version=1 AND i.delivery_channel='IN_APP'
          AND ({user} IS NULL OR i.user_id={user}) AND ({publication} IS NULL OR p.publication_id={publication})
          AND (({pending}=1 AND i.status='PENDING' AND i.next_attempt_at<={clock.GetUtcNow().UtcDateTime}) OR ({pending}=0 AND i.status='DELIVERED'))
        ORDER BY i.intent_id
        """).ToListAsync(ct);
    private sealed class IntentRow
    {public long IntentId{get;set;}public long UserId{get;set;}public string Status{get;set;}="";public long? NotificationId{get;set;}
     public byte[] Revision{get;set;}=[];public Guid PublicationId{get;set;}public long ActorId{get;set;}public Guid OperationId{get;set;}
     public byte[] Fingerprint{get;set;}=[];public long AssignmentId{get;set;}public long BranchId{get;set;}public long TaskId{get;set;}}
}
