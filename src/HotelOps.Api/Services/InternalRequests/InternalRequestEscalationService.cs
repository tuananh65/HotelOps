using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestEscalationInput(string Reason, long? PriorityId = null, DateTimeOffset? Deadline = null);
public sealed record InternalRequestEscalationResult(long RequestId, long EventId, string Version);

// UC122 priority/deadline intervention. No public adapter until acknowledgment delivery is integrated.
public sealed class InternalRequestEscalationService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static string Validate(InternalRequestEscalationInput input)
    {
        var reason=input.Reason?.Trim();
        if(reason is null || reason.Length is <10 or >4000 || reason.Any(char.IsControl)
            || input.PriorityId is <=0 || input.PriorityId==null && input.Deadline==null)
            throw new ValidationException("An intervention and a reason of 10-4000 characters without controls are required.");
        return reason;
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct) =>
        await InternalRequestCompletionService.InstalledAsync(db,ct)
        && await InternalRequestDecisionStore.InstalledAsync(db,ct)
        && await new InternalRequestProcessAuthority(db).InstalledAsync(ct)
        && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestEscalation.V1' AND CONVERT(nvarchar(20),value)='1')
             AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.internal_request_escalation_actions'),OBJECT_ID('dbo.internal_request_escalation_outbox')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=5
             AND COL_LENGTH('dbo.internal_request_escalation_actions','reason')=8000
             AND (SELECT COUNT(*) FROM sys.indexes WHERE name IN('PK_ir_escalation_actions','PK_ir_escalation_outbox') AND is_primary_key=1 AND is_disabled=0)=2
             AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_ir_escalation_reason','CK_ir_escalation_recipient') AND is_disabled=0 AND is_not_trusted=0)=2
             THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;

    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var user)||user.UserId!=ticket.UserId||user.SessionId!=sid||user.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
    }

    private Task<long[]> CandidatesAsync(long request,CancellationToken ct) => db.Database.SqlQuery<long>($"""
        SELECT requester_id AS Value FROM dbo.internal_requests WHERE internal_request_id={request}
        UNION SELECT a.assignee_id FROM dbo.internal_request_assignments a WHERE a.internal_request_id={request} AND a.status='ACTIVE'
        UNION SELECT m.user_id FROM dbo.department_management_access m JOIN dbo.internal_requests r
         ON r.branch_id=m.branch_id AND r.receiving_department_id=m.department_id
         WHERE r.internal_request_id={request} AND m.revoked_at IS NULL
        """).OrderBy(v=>v).ToArrayAsync(ct);

    public async Task<InternalRequestEscalationResult> EscalateAsync(SessionReference ticket,long sid,long id,byte[] version,
        InternalRequestEscalationInput input,CancellationToken ct)
    {
        var reason=Validate(input);
        if(id<=0||version.Length!=8)throw new ValidationException("Loaded request and version required.");
        if(db.ChangeTracker.Entries().Any()||db.Database.CurrentTransaction!=null)throw new InvalidOperationException("Clean escalation context required.");
        if(!await InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_ESCALATION_SCHEMA_REQUIRED");
        var candidates=await CandidatesAsync(id,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync(candidates.Append(ticket.UserId),ct);
            await SessionAsync(ticket,sid,ct);
            var authority=new InternalRequestEscalationAuthority(db);
            if(!await authority.AllowsAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_ESCALATION_AUTHORITY_REQUIRED");
            var lockedCandidates=await CandidatesAsync(id,ct);
            if(!candidates.SequenceEqual(lockedCandidates))throw new InternalRequestReadException(409,"IR_RECIPIENT_SCOPE_CHANGED");
            if(!(await InternalRequestDecisionStore.RevisionAsync(db,id,ct)).SequenceEqual(version))throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            var request=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==id,ct);
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            if(InternalRequestTiming.Classify(request.Status,request.CreatedAt,request.DueAt,now) is not ("AT_RISK" or "OVERDUE"))
                throw new InternalRequestReadException(409,"IR_ESCALATION_NOT_ELIGIBLE");
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalations WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id} AND state='OPEN'").SingleAsync(ct)!=0)
                throw new InternalRequestReadException(409,"IR_ESCALATION_ALREADY_OPEN");
            var deadline=input.Deadline?.UtcDateTime??request.DueAt;
            var priority=input.PriorityId??request.PriorityId;
            if(deadline<=request.CreatedAt)throw new ValidationException("Deadline must be after official creation time.");
            if(deadline==request.DueAt&&priority==request.PriorityId)throw new ValidationException("Intervention must change priority or deadline.");
            if(input.PriorityId is {} selected&&!await db.ReferenceValues.AsNoTracking().AnyAsync(r=>r.ReferenceId==selected&&r.GroupCode=="PRIORITY"&&r.IsActive,ct))
                throw new ValidationException("Active priority reference required.");
            var assignments=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE").ToArrayAsync(ct);
            if(assignments.Length>1||assignments.Any(a=>a.EndedAt!=null||a.AssignedAt>now))throw new InternalRequestReadException(409,"IR_ASSIGNMENT_INCONSISTENT");
            var handler=assignments.SingleOrDefault()?.AssigneeId;
            var recipients=new List<(long User,bool Requester,bool Handler,bool Manager)>();
            var departmentAuthority=new InternalRequestProcessAuthority(db);
            foreach(var candidate in candidates)
            {
                var scope=await new InternalRequestReadAccess(db).ReadAsync(candidate,now,ct);
                if(!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==id,ct))continue;
                var manager=request.ReceivingDepartmentId is {} department&&await departmentAuthority.AllowsDepartmentAsync(candidate,request.BranchId,department,ct);
                if(candidate==request.RequesterId||candidate==handler||manager)recipients.Add((candidate,candidate==request.RequesterId,candidate==handler,manager));
            }
            var entry=new InternalRequestEvent{InternalRequestId=id,EventType="ESCALATE",OldStatus=request.Status,NewStatus=request.Status,EventNote=reason,ActorId=ticket.UserId,OccurredAt=now};
            var audit=new AuditLog{ActorUserId=ticket.UserId,BranchId=request.BranchId,EntityType="internal_requests",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=JsonSerializer.Serialize(new{request.PriorityId,request.DueAt}),NewValues=JsonSerializer.Serialize(new{operation="ESCALATE",priorityId=priority,dueAt=deadline,reason}),ChangedFields="[\"priority_id\",\"due_at\",\"escalation\"]"};
            db.AddRange(entry,audit);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET priority_id={priority},due_at={deadline},updated_at={now} WHERE internal_request_id={id} AND row_version={version}",ct)!=1)
                throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalations(internal_request_id,opened_event_id,state,opened_at) VALUES({id},{entry.EventId},'OPEN',{now})",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalation_actions(event_id,audit_id,reason,old_priority_id,new_priority_id,old_due_at,new_due_at,department_id,handler_id) VALUES({entry.EventId},{audit.AuditId},{reason},{request.PriorityId},{priority},{request.DueAt},{deadline},{request.ReceivingDepartmentId},{handler})",ct);
            foreach(var recipient in recipients)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalation_outbox(event_id,recipient_id,is_requester,is_handler,is_manager,created_at) VALUES({entry.EventId},{recipient.User},{recipient.Requester},{recipient.Handler},{recipient.Manager},{now})",ct);
            await SessionAsync(ticket,sid,ct);
            if(!await authority.AllowsAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
            var next=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);
            await tx.CommitAsync(ct);
            return new(id,entry.EventId,Convert.ToBase64String(next));
        }
        finally{db.ChangeTracker.Clear();}
    }
}
