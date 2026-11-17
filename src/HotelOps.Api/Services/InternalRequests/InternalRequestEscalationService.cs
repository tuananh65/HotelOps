using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestEscalationInput(string Reason, long? PriorityId = null, DateTimeOffset? Deadline = null, long? HandlerId = null, long? DepartmentId = null);
public sealed record InternalRequestEscalationDepartment(long Id,string Name,InternalRequestFilterValue[] Handlers);
public sealed record InternalRequestEscalationResult(long RequestId, long EventId, string Version);
public sealed record InternalRequestEscalationView(long RequestId,string Version,string TimingStatus,bool CanEscalate,bool HasOpen,
    DateTime CreatedAt,DateTime? Deadline,long? PriorityId,InternalRequestFilterValue[] Priorities)
{
    public long? DepartmentId { get; init; }
    public string Status { get; init; } = "";
    public DateTime EvaluatedAt { get; init; }
    public InternalRequestEscalationDepartment[] Departments { get; init; } = [];
    public long? HandlerId { get; init; }
    public InternalRequestFilterValue[] Handlers { get; init; } = [];
}

// UC122 priority/deadline/handler intervention with durable, acknowledgment-required notification intent.
public sealed class InternalRequestEscalationService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static string Validate(InternalRequestEscalationInput input)
    {
        var reason=input.Reason?.Trim();
        if(reason is null || reason.Length is <10 or >4000 || reason.Any(char.IsControl)
            || input.PriorityId is <=0 || input.HandlerId is <=0 || input.DepartmentId is <=0
            || input.PriorityId==null && input.Deadline==null && input.HandlerId==null && input.DepartmentId==null)
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

    private async Task<bool> HandlerEligibleAsync(long user,InternalRequest request,DateTime now,CancellationToken ct)
        => request.ReceivingDepartmentId is {} department && await HandlerInDepartmentAsync(user,request.BranchId,department,now,ct);

    private async Task<bool> HandlerInDepartmentAsync(long user,long branch,long department,DateTime now,CancellationToken ct)
        => await InternalRequestHandoverService.EligibleAsync(db,user,branch,department,now,ct)
            && await InternalRequestHandoverReadAccess.HasScopeAsync(db,user,branch,now,ct);

    private async Task<bool> CanChangeHandlerAsync(InternalRequest request,CancellationToken ct)
        => request.Status=="IN_PROGRESS" && request.ReceivingDepartmentId!=null
            && await InternalRequestAssignmentService.InstalledAsync(db,ct)
            && await InternalRequestEmployeeMembership.InstalledAsync(db,ct)
            && await InternalRequestHandoverStore.InstalledAsync(db,ct)
            && !await InternalRequestHandoverStore.Rows(db).AnyAsync(h=>h.RequestId==request.InternalRequestId&&h.State=="PENDING_ACKNOWLEDGMENT",ct);

    public async Task<InternalRequestEscalationView> ReadAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        if(id<=0)throw new ValidationException("Request required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);
        var authority=new InternalRequestEscalationAuthority(db);
        if(!await authority.AllowsAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_ESCALATION_AUTHORITY_REQUIRED");
        if(!await InstalledAsync(db,ct)||!await NotificationAcknowledgmentStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_ESCALATION_SCHEMA_REQUIRED");
        var row=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==id,ct);
        var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
        var timing=InternalRequestTiming.Classify(row.Status,row.CreatedAt,row.DueAt,now);
        var open=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalations WHERE internal_request_id={id} AND state='OPEN'").SingleAsync(ct)>0;
        var priorities=await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode=="PRIORITY"&&r.IsActive).OrderBy(r=>r.SortOrder).ThenBy(r=>r.ReferenceId)
            .Select(r=>new InternalRequestFilterValue(r.ReferenceId,r.DisplayName)).ToArrayAsync(ct);
        var version=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);
        var assignments=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE").ToArrayAsync(ct);
        var handler=assignments.Length==1&&assignments[0].EndedAt==null&&assignments[0].AssignedAt<=now?assignments[0].AssigneeId:(long?)null;
        var handlers=new List<InternalRequestFilterValue>();
        if(handler!=null&&!open&&timing is ("AT_RISK" or "OVERDUE")&&await CanChangeHandlerAsync(row,ct))
            foreach(var candidate in await InternalRequestEmployeeMembership.Members(db,row.BranchId,row.ReceivingDepartmentId,now)
                .Where(u=>u.UserId!=handler).OrderBy(u=>u.FullName).ThenBy(u=>u.UserId)
                .Select(u=>new InternalRequestFilterValue(u.UserId,u.FullName)).ToArrayAsync(ct))
                if(await HandlerEligibleAsync(candidate.Id,row,now,ct))handlers.Add(candidate);
        var departments = new List<InternalRequestEscalationDepartment>();
        var routeState = row.Status is ("PENDING_ACCEPTANCE" or "ACCEPTED") && assignments.Length == 0
            || row.Status == "IN_PROGRESS" && handler != null && await CanChangeHandlerAsync(row,ct);
        if(routeState && !open && timing is ("AT_RISK" or "OVERDUE") && await InternalRequestHandoverStore.InstalledAsync(db,ct)
            && !await InternalRequestHandoverStore.Rows(db).AnyAsync(h=>h.RequestId==id&&h.State=="PENDING_ACKNOWLEDGMENT",ct))
        {
            foreach(var department in await db.Departments.AsNoTracking().Where(d=>d.BranchId==row.BranchId&&d.IsActive&&d.DepartmentId!=row.ReceivingDepartmentId)
                .OrderBy(d=>d.DepartmentName).ThenBy(d=>d.DepartmentId).Select(d=>new InternalRequestFilterValue(d.DepartmentId,d.DepartmentName)).ToArrayAsync(ct))
            {
                var targetHandlers = new List<InternalRequestFilterValue>();
                if(row.Status=="IN_PROGRESS")
                    foreach(var candidate in await InternalRequestEmployeeMembership.Members(db,row.BranchId,department.Id,now)
                        .OrderBy(u=>u.FullName).ThenBy(u=>u.UserId).Select(u=>new InternalRequestFilterValue(u.UserId,u.FullName)).ToArrayAsync(ct))
                        if(await HandlerInDepartmentAsync(candidate.Id,row.BranchId,department.Id,now,ct))targetHandlers.Add(candidate);
                departments.Add(new(department.Id,department.Name,targetHandlers.ToArray()));
            }
        }
        await SessionAsync(ticket,sid,ct);
        if(!await authority.AllowsAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
        await tx.CommitAsync(ct);
        return new(id,Convert.ToBase64String(version),timing,!open&&timing is "AT_RISK" or "OVERDUE",open,
            DateTime.SpecifyKind(row.CreatedAt,DateTimeKind.Utc),row.DueAt is {} due?DateTime.SpecifyKind(due,DateTimeKind.Utc):null,row.PriorityId,priorities)
        {HandlerId=handler,Handlers=handlers.ToArray(),Status=row.Status,EvaluatedAt=now,DepartmentId=row.ReceivingDepartmentId,Departments=departments.ToArray()};
    }

    private Task<long[]> CandidatesAsync(long request,long? targetDepartment,CancellationToken ct) => db.Database.SqlQuery<long>($"""
        SELECT requester_id AS Value FROM dbo.internal_requests WHERE internal_request_id={request}
        UNION SELECT a.assignee_id FROM dbo.internal_request_assignments a WHERE a.internal_request_id={request} AND a.status='ACTIVE'
        UNION SELECT m.user_id FROM dbo.department_management_access m JOIN dbo.internal_requests r
         ON r.branch_id=m.branch_id AND (r.receiving_department_id=m.department_id OR m.department_id={targetDepartment})
         WHERE r.internal_request_id={request} AND m.revoked_at IS NULL
        """).OrderBy(v=>v).ToArrayAsync(ct);

    public async Task<InternalRequestEscalationResult> EscalateAsync(SessionReference ticket,long sid,long id,byte[] version,
        InternalRequestEscalationInput input,CancellationToken ct)
    {
        var reason=Validate(input);
        if(id<=0||version.Length!=8)throw new ValidationException("Loaded request and version required.");
        if(db.ChangeTracker.Entries().Any()||db.Database.CurrentTransaction!=null)throw new InvalidOperationException("Clean escalation context required.");
        if(!await InstalledAsync(db,ct)||!await NotificationAcknowledgmentStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_ESCALATION_SCHEMA_REQUIRED");
        var candidates=await CandidatesAsync(id,input.DepartmentId,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync(candidates.Append(ticket.UserId).Concat(input.HandlerId is {} selectedHandler?[selectedHandler]:[]),ct);
            await SessionAsync(ticket,sid,ct);
            var authority=new InternalRequestEscalationAuthority(db);
            if(!await authority.AllowsAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_ESCALATION_AUTHORITY_REQUIRED");
            var lockedCandidates=await CandidatesAsync(id,input.DepartmentId,ct);
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
            if(input.PriorityId is {} selected&&!await db.ReferenceValues.AsNoTracking().AnyAsync(r=>r.ReferenceId==selected&&r.GroupCode=="PRIORITY"&&r.IsActive,ct))
                throw new ValidationException("Active priority reference required.");
            var assignments=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE").ToArrayAsync(ct);
            if(assignments.Length>1||assignments.Any(a=>a.EndedAt!=null||a.AssignedAt>now))throw new InternalRequestReadException(409,"IR_ASSIGNMENT_INCONSISTENT");
            var handler=assignments.SingleOrDefault()?.AssigneeId;
            var previousHandler=handler;
            var previousDepartment=request.ReceivingDepartmentId;
            var targetDepartment=input.DepartmentId??previousDepartment;
            var changeDepartment=targetDepartment!=previousDepartment;
            var nextStatus=request.Status;
            if(changeDepartment)
            {
                if(request.Status is not ("PENDING_ACCEPTANCE" or "ACCEPTED" or "IN_PROGRESS")
                    || request.Status=="IN_PROGRESS" && handler==null
                    || request.Status!="IN_PROGRESS" && (assignments.Length!=0 || input.HandlerId!=null))
                    throw new InternalRequestReadException(409,"IR_ESCALATION_ROUTE_STATE_REQUIRED");
                if(request.Status=="ACCEPTED")nextStatus="PENDING_ACCEPTANCE";
                if(!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==targetDepartment&&d.BranchId==request.BranchId&&d.IsActive&&d.Branch.IsActive,ct))
                    throw new InternalRequestReadException(400,"IR_TARGET_DEPARTMENT_INVALID");
                if(!await InternalRequestHandoverStore.InstalledAsync(db,ct))
                    throw new InternalRequestReadException(503,"IR_HANDOVER_SCHEMA_REQUIRED");
                await InternalRequestHandoverStore.EnsureNoPendingAsync(db,id,ct);
                if(request.Status=="IN_PROGRESS" && (input.HandlerId is not {} confirmedHandler
                    || !await CanChangeHandlerAsync(request,ct)
                    || !await HandlerInDepartmentAsync(confirmedHandler,request.BranchId,targetDepartment!.Value,now,ct)))
                    throw new InternalRequestReadException(403,"IR_TARGET_HANDLER_CONFIRMATION_REQUIRED");
            }
            var changeHandler=input.HandlerId is {} replacement&&replacement!=handler;
            if(deadline==request.DueAt&&priority==request.PriorityId&&!changeHandler&&!changeDepartment)
                throw new ValidationException("Intervention must change priority, deadline, receiving department or handler.");
            if(changeHandler)
            {
                if(handler==null||!await CanChangeHandlerAsync(request,ct))throw new InternalRequestReadException(409,"IR_HANDLER_STATE_REQUIRED");
                if(targetDepartment==null||!await HandlerInDepartmentAsync(input.HandlerId!.Value,request.BranchId,targetDepartment.Value,now,ct))throw new InternalRequestReadException(403,"IR_HANDLER_NOT_ELIGIBLE");
                if(await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignments[0].AssignmentId&&a.Status=="ACTIVE"&&a.EndedAt==null)
                    .ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"REASSIGNED").SetProperty(a=>a.EndedAt,now),ct)!=1)
                    throw new InternalRequestReadException(409,"IR_HANDLER_CHANGED");
                db.InternalRequestAssignments.Add(new(){InternalRequestId=id,AssigneeId=input.HandlerId.Value,AssignedBy=ticket.UserId,
                    AssignedAt=now,Status="ACTIVE",AssignmentNote=reason});
                await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();handler=input.HandlerId;
            }
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET priority_id={priority},due_at={deadline},receiving_department_id={targetDepartment},status={nextStatus},updated_at={now} WHERE internal_request_id={id} AND row_version={version}",ct)!=1)
                throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            request.ReceivingDepartmentId=targetDepartment;
            var recipients=new List<(long User,bool Requester,bool Handler,bool Manager)>();
            var departmentAuthority=new InternalRequestProcessAuthority(db);
            foreach(var candidate in candidates.Concat(handler is {} currentHandler?[currentHandler]:[]).Distinct().Order())
            {
                var scope=await new InternalRequestReadAccess(db).ReadAsync(candidate,now,ct);
                if(!await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==id,ct))continue;
                var manager=request.ReceivingDepartmentId is {} department&&await departmentAuthority.AllowsDepartmentAsync(candidate,request.BranchId,department,ct);
                if(candidate==request.RequesterId||candidate==handler||manager)recipients.Add((candidate,candidate==request.RequesterId,candidate==handler,manager));
            }
            var entry=new InternalRequestEvent{InternalRequestId=id,EventType="ESCALATE",OldStatus=request.Status,NewStatus=nextStatus,EventNote=reason,ActorId=ticket.UserId,OccurredAt=now};
            var audit=new AuditLog{ActorUserId=ticket.UserId,BranchId=request.BranchId,EntityType="internal_requests",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=JsonSerializer.Serialize(new{request.PriorityId,request.DueAt,handlerId=previousHandler,receivingDepartmentId=previousDepartment,status=request.Status}),NewValues=JsonSerializer.Serialize(new{operation="ESCALATE",priorityId=priority,dueAt=deadline,handlerId=handler,receivingDepartmentId=targetDepartment,status=nextStatus,reason}),ChangedFields=JsonSerializer.Serialize(new[]{"priority_id","due_at","escalation"}.Concat(changeDepartment?new[]{"receiving_department_id"}:Array.Empty<string>()).Concat(changeHandler?new[]{"handler"}:Array.Empty<string>()).Concat(nextStatus!=request.Status?new[]{"status"}:Array.Empty<string>()))};
            db.AddRange(entry,audit);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalations(internal_request_id,opened_event_id,state,opened_at) VALUES({id},{entry.EventId},'OPEN',{now})",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalation_actions(event_id,audit_id,reason,old_priority_id,new_priority_id,old_due_at,new_due_at,department_id,handler_id) VALUES({entry.EventId},{audit.AuditId},{reason},{request.PriorityId},{priority},{request.DueAt},{deadline},{request.ReceivingDepartmentId},{handler})",ct);
            foreach(var recipient in recipients)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalation_outbox(event_id,recipient_id,is_requester,is_handler,is_manager,created_at) VALUES({entry.EventId},{recipient.User},{recipient.Requester},{recipient.Handler},{recipient.Manager},{now})",ct);
            await SessionAsync(ticket,sid,ct);
            if(!await authority.AllowsAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
            if((changeHandler || changeDepartment && handler!=null)&&!await HandlerEligibleAsync(handler!.Value,request,
                await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),ct))
                throw new InternalRequestReadException(403,"IR_HANDLER_NOT_ELIGIBLE");
            var next=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);
            await tx.CommitAsync(ct);
            return new(id,entry.EventId,Convert.ToBase64String(next));
        }
        finally{db.ChangeTracker.Clear();}
    }
}
