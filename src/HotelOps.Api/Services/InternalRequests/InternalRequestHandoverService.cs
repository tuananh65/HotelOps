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

public sealed record InternalRequestHandoverInput(long ReceiverId,string Summary,string? Note);
public sealed record InternalRequestHandoverDecision(long HandoverId,bool Acknowledge,string? Reason);
public sealed record InternalRequestHandoverResult(long RequestId,long HandoverId,string State,string Version,string Message);
public sealed record InternalRequestHandoverView(long RequestId,string Version,long? HandoverId,string? State,long? ReceiverId,
    string? Summary,string? Note,bool CanSubmit,bool CanDecide,InternalRequestFilterValue[] Receivers)
{
    public DateTime? CreatedAt { get; init; }
    public DateTime? DecisionAt { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class InternalRequestHandoverService(HotelOpsDbContext db,ISqlSessionService sessions,ICurrentUserAccessor current)
{
    private async Task<DateTime> NowAsync(CancellationToken ct)=>DateTime.SpecifyKind(
        await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
    }
    private async Task SchemaAsync(CancellationToken ct)
    {
        if(!await InternalRequestEmployeeMembership.InstalledAsync(db,ct)||!await InternalRequestHandoverStore.InstalledAsync(db,ct)||!await InternalRequestDecisionStore.InstalledAsync(db,ct)
            ||!await InternalRequestAssignmentService.InstalledAsync(db,ct)||!await new InternalRequestProcessAuthority(db).InstalledAsync(ct))throw new InternalRequestReadException(503,"IR_HANDOVER_SCHEMA_REQUIRED");
    }
    internal static async Task<bool> EligibleAsync(HotelOpsDbContext db,long user,long branch,long department,DateTime now,CancellationToken ct)
    {
        if(!await InternalRequestEmployeeMembership.InstalledAsync(db,ct)
            ||!await InternalRequestEmployeeMembership.Members(db,branch,department,now).AnyAsync(u=>u.UserId==user,ct))return false;
        var grants=await(from assignment in db.UserRoles.AsNoTracking() join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId==user&&grant.Permission.PermissionCode=="IR_PROCESS"
            select new{Role=assignment.Role.RoleCode,Path=new PermissionGrantPath(grant.Permission.PermissionCode,grant.DataScope,assignment.BranchId,
                assignment.IsActive,assignment.Role.IsActive,assignment.AssignedAt,assignment.ExpiresAt,assignment.Branch!=null&&assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(a=>a.UserId==user&&a.BranchId==assignment.BranchId&&a.RevokedAt==null&&a.GrantedAt<=now))}).ToArrayAsync(ct);
        return grants.Any(g=>InternalRequestProgressAuthority.EligibleHandler(g.Role,g.Path,branch,now));
    }
    private async Task<InternalRequest> ReadRequestAsync(long user,long id,CancellationToken ct,long? receiptId=null)
    {
        var now=await NowAsync(ct);var scope=await new InternalRequestReadAccess(db).ReadAsync(user,now,ct);
        if(receiptId is {} selected)
        {
            var receipt=await InternalRequestHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==selected&&h.RequestId==id,ct);
            if(receipt==null||!await InternalRequestHandoverReadAccess.AllowsAsync(db,user,receipt,now,ct))
                throw new InternalRequestReadException(404,"IR_HANDOVER_NOT_FOUND");
            return await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}")
                .AsNoTracking().SingleAsync(ct);
        }
        var row=await scope.Apply(db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={id}").AsNoTracking(),now)
            .SingleOrDefaultAsync(ct);
        return row??throw new InternalRequestReadException(404,"IR_NOT_FOUND");
    }
    private async Task<bool> CanReceiveAsync(long user,InternalRequest request,DateTime now,CancellationToken ct)
    {
        if(request.ReceivingDepartmentId is not {} department
            ||!await EligibleAsync(db,user,request.BranchId,department,now,ct))return false;
        return await InternalRequestHandoverReadAccess.HasScopeAsync(db,user,request.BranchId,now,ct);
    }
    private async Task<InternalRequestAssignment> HandlerAsync(InternalRequest row,CancellationToken ct)
    {
        var active=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==row.InternalRequestId&&a.Status=="ACTIVE").ToArrayAsync(ct);
        var now=await NowAsync(ct);
        if(row.Status!="IN_PROGRESS"||row.ReceivingDepartmentId==null||active.Length!=1||active[0].EndedAt!=null||active[0].AssignedAt>now)
            throw new InternalRequestReadException(409,"IR_HANDLER_STATE_REQUIRED");
        return active[0];
    }
    private async Task VersionAsync(long id,byte[] version,CancellationToken ct)
    {
        if(version.Length!=8||!(await InternalRequestDecisionStore.RevisionAsync(db,id,ct)).SequenceEqual(version))throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
    }
    private Task<long[]> ManagersAsync(long branch,long department,CancellationToken ct)=>db.Database.SqlQuery<long>($"SELECT user_id AS Value FROM dbo.department_management_access WHERE branch_id={branch} AND department_id={department} AND revoked_at IS NULL AND granted_at<=SYSUTCDATETIME()").OrderBy(v=>v).ToArrayAsync(ct);
    private async Task<InternalRequestEvent> EventAsync(InternalRequest row,long actor,string kind,string note,DateTime now,CancellationToken ct)
    {
        var entry=new InternalRequestEvent{InternalRequestId=row.InternalRequestId,EventType=kind,ActorId=actor,OldStatus=row.Status,NewStatus=row.Status,EventNote=note,OccurredAt=now};
        db.InternalRequestEvents.Add(entry);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();return entry;
    }
    private async Task AuditAsync(InternalRequest row,long actor,string operation,long handover,long previous,long receiver,DateTime now,CancellationToken ct)
    {
        db.AuditLogs.Add(new(){ActorUserId=actor,BranchId=row.BranchId,EntityType="internal_requests",EntityId=row.InternalRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            NewValues=JsonSerializer.Serialize(new{operation,handover,previousHandler=previous,receiver}),ChangedFields="[\"handover\"]"});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
    }
    public async Task<InternalRequestHandoverView> ReadAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);await SchemaAsync(ct);
        var row=await ReadRequestAsync(ticket.UserId,id,ct);var pending=await InternalRequestHandoverStore.Rows(db).Where(h=>h.RequestId==id&&h.State=="PENDING_ACKNOWLEDGMENT").SingleOrDefaultAsync(ct);
        var now=await NowAsync(ct);
        var active=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now).ToArrayAsync(ct);
        var eligible=row.Status=="IN_PROGRESS"&&row.ReceivingDepartmentId is {} department&&await EligibleAsync(db,ticket.UserId,row.BranchId,department,now,ct);
        var submit=eligible&&active.Length==1&&active[0].AssigneeId==ticket.UserId&&pending==null;
        var decide=eligible&&pending?.ReceiverId==ticket.UserId&&active.Length==1&&active[0].AssignmentId==pending.SenderAssignmentId&&pending.DepartmentId==row.ReceivingDepartmentId;
        var receivers=new List<InternalRequestFilterValue>();
        if(submit)foreach(var user in await InternalRequestEmployeeMembership.Members(db,row.BranchId,row.ReceivingDepartmentId,now).Where(u=>u.UserId!=ticket.UserId)
            .OrderBy(u=>u.FullName).ThenBy(u=>u.UserId).Select(u=>new InternalRequestFilterValue(u.UserId,u.FullName)).ToArrayAsync(ct))
            if(await CanReceiveAsync(user.Id,row,now,ct))receivers.Add(user);
        var version=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);await SessionAsync(ticket,sid,ct);await ReadRequestAsync(ticket.UserId,id,ct);
        await tx.CommitAsync(ct);return new(id,Convert.ToBase64String(version),pending?.Id,pending?.State,pending?.ReceiverId,pending?.Summary,pending?.Note,submit,decide,receivers.ToArray());
    }
    public async Task<InternalRequestHandoverView> ReadReceiptAsync(SessionReference ticket,long sid,long id,long receiptId,CancellationToken ct)
    {
        if(id<=0||receiptId<=0)throw new InternalRequestReadException(404,"IR_HANDOVER_NOT_FOUND");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);await SchemaAsync(ct);
        var row=await ReadRequestAsync(ticket.UserId,id,ct,receiptId);
        var receipt=await InternalRequestHandoverStore.Rows(db).SingleAsync(h=>h.Id==receiptId&&h.RequestId==id,ct);
        var now=await NowAsync(ct);
        var active=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now).ToArrayAsync(ct);
        var decide=receipt.State=="PENDING_ACKNOWLEDGMENT"&&receipt.ReceiverId==ticket.UserId&&row.Status=="IN_PROGRESS"
            &&row.ReceivingDepartmentId==receipt.DepartmentId&&active.Length==1&&active[0].AssignmentId==receipt.SenderAssignmentId
            &&await EligibleAsync(db,ticket.UserId,row.BranchId,receipt.DepartmentId,now,ct);
        var version=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);
        await SessionAsync(ticket,sid,ct);await ReadRequestAsync(ticket.UserId,id,ct,receiptId);
        if(decide)decide=await EligibleAsync(db,ticket.UserId,row.BranchId,receipt.DepartmentId,await NowAsync(ct),ct);
        await tx.CommitAsync(ct);
        return new(id,Convert.ToBase64String(version),receipt.Id,receipt.State,receipt.ReceiverId,receipt.Summary,receipt.Note,false,decide,[])
        {CreatedAt=DateTime.SpecifyKind(receipt.CreatedAt,DateTimeKind.Utc),DecisionAt=receipt.DecisionAt is {} at?DateTime.SpecifyKind(at,DateTimeKind.Utc):null,
            RejectionReason=receipt.RejectionReason};
    }

    public async Task<InternalRequestHandoverResult> SubmitAsync(SessionReference ticket,long sid,long id,byte[] version,InternalRequestHandoverInput input,CancellationToken ct)
    {
        var summary=InternalRequestHandoverStore.Required(input.Summary);var note=InternalRequestHandoverStore.Optional(input.Note);
        if(id<=0||input.ReceiverId<=0||input.ReceiverId==ticket.UserId)throw new ValidationException("Select another eligible handler.");
        if(db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Clean handover context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try{
            await new UserEligibilityLock(db).LockAsync([ticket.UserId,input.ReceiverId],ct);await SessionAsync(ticket,sid,ct);await SchemaAsync(ct);
            var row=await ReadRequestAsync(ticket.UserId,id,ct);await VersionAsync(id,version,ct);var sender=await HandlerAsync(row,ct);
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            if(sender.AssigneeId!=ticket.UserId||!await EligibleAsync(db,ticket.UserId,row.BranchId,row.ReceivingDepartmentId!.Value,now,ct)
                ||!await CanReceiveAsync(input.ReceiverId,row,now,ct))throw new InternalRequestReadException(403,"IR_HANDOVER_AUTHORITY_REQUIRED");
            if(await InternalRequestHandoverStore.Rows(db).AnyAsync(h=>h.RequestId==id&&h.State=="PENDING_ACKNOWLEDGMENT",ct))throw new InternalRequestReadException(409,"IR_HANDOVER_PENDING");
            var entry=await EventAsync(row,ticket.UserId,"HANDOVER_SUBMIT",summary,now,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_handovers(handover_id,request_id,branch_id,department_id,sender_assignment_id,receiver_id,summary,note,state,created_at) VALUES({entry.EventId},{id},{row.BranchId},{row.ReceivingDepartmentId},{sender.AssignmentId},{input.ReceiverId},{summary},{note},'PENDING_ACKNOWLEDGMENT',{now})",ct);
            await AuditAsync(row,ticket.UserId,"HANDOVER_SUBMIT",entry.EventId,sender.AssigneeId,input.ReceiverId,now,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_handover_outbox(event_id,recipient_id,handover_id,recipient_kind,created_at) VALUES({entry.EventId},{input.ReceiverId},{entry.EventId},'RECEIVER',{now})",ct);
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={id} AND row_version={version}",ct)!=1)
                throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            await SessionAsync(ticket,sid,ct);await ReadRequestAsync(ticket.UserId,id,ct);
            var finalNow=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            if(!await EligibleAsync(db,ticket.UserId,row.BranchId,row.ReceivingDepartmentId.Value,finalNow,ct)||!await CanReceiveAsync(input.ReceiverId,row,finalNow,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
            var next=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);await tx.CommitAsync(ct);return new(id,entry.EventId,"PENDING_ACKNOWLEDGMENT",Convert.ToBase64String(next),"MSG08");
        }finally{db.ChangeTracker.Clear();}
    }

    public async Task<InternalRequestHandoverResult> DecideAsync(SessionReference ticket,long sid,long id,byte[] version,InternalRequestHandoverDecision input,CancellationToken ct)
    {
        var reason=input.Acknowledge?InternalRequestHandoverStore.Optional(input.Reason):InternalRequestHandoverStore.Required(input.Reason);
        if(id<=0||input.HandoverId<=0||version.Length!=8)throw new ValidationException("Loaded handover and request version required.");
        if(db.ChangeTracker.Entries().Any()||db.Database.CurrentTransaction!=null)throw new InvalidOperationException("Clean handover context required.");
        await SchemaAsync(ct);
        var snapshot=await InternalRequestHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==input.HandoverId&&h.RequestId==id,ct)
            ??throw new InternalRequestReadException(404,"IR_HANDOVER_NOT_FOUND");
        var senderId=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.AssignmentId==snapshot.SenderAssignmentId).Select(a=>a.AssigneeId).SingleAsync(ct);
        var managers=await ManagersAsync(snapshot.BranchId,snapshot.DepartmentId,ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try{
            await new UserEligibilityLock(db).LockAsync(managers.Append(senderId).Append(ticket.UserId).Append(snapshot.ReceiverId),ct);
            await SessionAsync(ticket,sid,ct);
            var row=await ReadRequestAsync(ticket.UserId,id,ct,input.HandoverId);await VersionAsync(id,version,ct);var sender=await HandlerAsync(row,ct);
            var handover=await InternalRequestHandoverStore.Rows(db).SingleAsync(h=>h.Id==input.HandoverId,ct);
            if(handover.State!="PENDING_ACKNOWLEDGMENT"||handover.SenderAssignmentId!=sender.AssignmentId||sender.AssigneeId!=senderId
                ||handover.ReceiverId!=snapshot.ReceiverId||handover.DepartmentId!=row.ReceivingDepartmentId||handover.BranchId!=row.BranchId)
                throw new InternalRequestReadException(409,"IR_HANDOVER_CHANGED");
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            if(handover.ReceiverId!=ticket.UserId||!await EligibleAsync(db,ticket.UserId,row.BranchId,handover.DepartmentId,now,ct))
                throw new InternalRequestReadException(403,"IR_HANDOVER_RECEIVER_REQUIRED");
            var currentManagers=await ManagersAsync(row.BranchId,handover.DepartmentId,ct);
            if(!managers.SequenceEqual(currentManagers))throw new InternalRequestReadException(409,"IR_RECIPIENT_SCOPE_CHANGED");
            var newState=input.Acknowledge?"ACKNOWLEDGED":"REJECTED";
            var operation=input.Acknowledge?"HANDOVER_ACKNOWLEDGE":"HANDOVER_REJECT";
            var entry=await EventAsync(row,ticket.UserId,operation,reason??handover.Summary,now,ct);
            long? assignmentId=null;
            if(input.Acknowledge){
                var ended=await db.InternalRequestAssignments.Where(a=>a.AssignmentId==sender.AssignmentId&&a.Status=="ACTIVE"&&a.EndedAt==null)
                    .ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"REASSIGNED").SetProperty(a=>a.EndedAt,now),ct);
                if(ended!=1)throw new InternalRequestReadException(409,"IR_HANDLER_CHANGED");
                var assignment=new InternalRequestAssignment{InternalRequestId=id,AssigneeId=ticket.UserId,AssignedBy=ticket.UserId,AssignedAt=now,Status="ACTIVE",AssignmentNote="Acknowledged handover"};
                db.InternalRequestAssignments.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();assignmentId=assignment.AssignmentId;
            }
            var rejection=input.Acknowledge?null:reason;
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_handovers SET state={newState},decision_event_id={entry.EventId},decision_at={now},rejection_reason={rejection},new_assignment_id={assignmentId} WHERE handover_id={handover.Id} AND state='PENDING_ACKNOWLEDGMENT'",ct);
            if(changed!=1)throw new InternalRequestReadException(409,"IR_HANDOVER_CHANGED");
            await AuditAsync(row,ticket.UserId,operation,handover.Id,sender.AssigneeId,handover.ReceiverId,now,ct);
            if(input.Acknowledge){
                var recipients=new Dictionary<long,string>{{senderId,"SENDER"}};var authority=new InternalRequestProcessAuthority(db);
                foreach(var manager in managers)if(await authority.AllowsDepartmentAsync(manager,row.BranchId,handover.DepartmentId,ct))recipients.TryAdd(manager,"MANAGER");
                foreach(var recipient in recipients.OrderBy(r=>r.Key))await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_handover_outbox(event_id,recipient_id,handover_id,recipient_kind,created_at) VALUES({entry.EventId},{recipient.Key},{handover.Id},{recipient.Value},{now})",ct);
            }
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={id} AND row_version={version}",ct)!=1)
                throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            await SessionAsync(ticket,sid,ct);await ReadRequestAsync(ticket.UserId,id,ct,input.HandoverId);
            if(!await EligibleAsync(db,ticket.UserId,row.BranchId,handover.DepartmentId,await NowAsync(ct),ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
            var next=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);await tx.CommitAsync(ct);return new(id,handover.Id,newState,Convert.ToBase64String(next),"MSG08");
        }finally{db.ChangeTracker.Clear();}
    }
}
