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

public sealed record InternalRequestCompletionResult(long RequestId,long EventId,string Status,string Version,string Message);
public sealed record InternalRequestCompletionInput(string Result);
public sealed record InternalRequestCompletionView(long RequestId,string Status,string Version,bool CanComplete,string? Blocker);

// UC120 business transaction. The public HTTP/UI and delivery adapters are separate.
public sealed class InternalRequestCompletionService(HotelOpsDbContext db,InternalRequestProcessAuthority authority,
    ISqlSessionService sessions,ICurrentUserAccessor current)
{
    public static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>
        await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestCompletion.V1' AND CONVERT(nvarchar(20),value)='1')
         AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.internal_request_closures'),OBJECT_ID('dbo.internal_request_completion_outbox'),OBJECT_ID('dbo.internal_request_escalations'),OBJECT_ID('dbo.internal_request_material_links')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=13
         AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_escalations') AND name='UX_ir_escalation_open' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''OPEN'')')
         AND COL_LENGTH('dbo.internal_request_closures','result_note')=8000
         AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_ir_closure_result','CK_ir_escalation_state') AND is_disabled=0 AND is_not_trusted=0)=2
         THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;

    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
    }

    private Task<int> MaterialBlockersAsync(long id,long branch,CancellationToken ct)=>db.Database.SqlQuery<int>($"""
        SELECT COUNT(*) AS Value FROM dbo.internal_request_material_links l
        JOIN dbo.material_requests m ON m.request_id=l.material_request_id
        WHERE l.internal_request_id={id} AND (l.is_mandatory=1 OR m.branch_id<>{branch})
        """).SingleAsync(ct);

    public async Task<InternalRequestCompletionView> ReadAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        if(id<=0)throw new ValidationException("Request required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);
        if(!await InstalledAsync(db,ct)||!await InternalRequestDecisionStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_COMPLETION_SCHEMA_REQUIRED");
        var allowed=await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct);
        var now=DateTime.UtcNow;var scope=await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId,now,ct);
        var request=await scope.Apply(db.InternalRequests.AsNoTracking(),now).SingleOrDefaultAsync(r=>r.InternalRequestId==id,ct)
            ??throw new InternalRequestReadException(404,"IR_NOT_FOUND");
        string? blocker=null;
        if(allowed&&request.Status=="IN_PROGRESS")
        {
            var active=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE").ToArrayAsync(ct);
            if(active.Length!=1||active[0].EndedAt!=null||active[0].AssignedAt>now)blocker="IR_ASSIGNMENT_INCONSISTENT";
            else if((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,id,ct)).Count!=0)blocker="MSG34";
            else if(await MaterialBlockersAsync(id,request.BranchId,ct)!=0)blocker="IR_MATERIAL_COMPLETION_UNRESOLVED";
        }
        var version=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);await SessionAsync(ticket,sid,ct);
        var finalNow=DateTime.UtcNow;var finalScope=await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId,finalNow,ct);
        if(!await finalScope.Apply(db.InternalRequests.AsNoTracking(),finalNow).AnyAsync(r=>r.InternalRequestId==id,ct)
            ||allowed!=await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_SCOPE_CHANGED");
        await tx.CommitAsync(ct);
        // No linked target IDs/titles/statuses are disclosed here. Existing target panels independently authorize them.
        return new(id,request.Status,Convert.ToBase64String(version),allowed&&request.Status=="IN_PROGRESS"&&blocker==null,blocker);
    }

    public async Task<InternalRequestCompletionResult> CompleteAsync(SessionReference ticket,long sid,long id,byte[] version,string result,CancellationToken ct)
    {
        result=result?.Trim()??"";
        if(result.Length is 0 or >4000||result.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t'))
            throw new ValidationException("A nonblank result note of at most 4000 characters is required.");
        if(id<=0||version.Length!=8)throw new ValidationException("Loaded request and row version required.");
        if(db.ChangeTracker.Entries().Any()||db.Database.CurrentTransaction!=null)throw new InvalidOperationException("Clean completion context required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);
            if(!await InstalledAsync(db,ct)||!await InternalRequestDecisionStore.InstalledAsync(db,ct))
                throw new InternalRequestReadException(503,"IR_COMPLETION_SCHEMA_REQUIRED");
            if(!await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_PROCESS_REQUIRED");
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            var scope=await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId,now,ct);
            var request=await scope.Apply(db.InternalRequests.AsNoTracking(),now).SingleOrDefaultAsync(r=>r.InternalRequestId==id,ct)
                ??throw new InternalRequestReadException(404,"IR_NOT_FOUND");
            if(!(await InternalRequestDecisionStore.RevisionAsync(db,id,ct)).SequenceEqual(version))throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            if(request.Status!="IN_PROGRESS")throw new InternalRequestReadException(409,"IR_NOT_IN_PROGRESS");
            var assignments=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==id&&a.Status=="ACTIVE").ToArrayAsync(ct);
            if(assignments.Length!=1||assignments[0].EndedAt!=null||assignments[0].AssignedAt>now)throw new InternalRequestReadException(409,"IR_ASSIGNMENT_INCONSISTENT");
            if((await InternalRequestCompletionWork.ReadKnownBlockersAsync(db,id,ct)).Count!=0)
                throw new InternalRequestReadException(409,"IR_COMPLETION_BLOCKED");
            // Pending approved mapping of Fully Issued / remainder closure. Do not infer closed.
            // Optional links still require valid branch provenance.
            if(await MaterialBlockersAsync(id,request.BranchId,ct)!=0)throw new InternalRequestReadException(409,"IR_MATERIAL_COMPLETION_UNRESOLVED");
            var entry=new InternalRequestEvent{InternalRequestId=id,EventType="COMPLETE",OldStatus="IN_PROGRESS",NewStatus="CLOSED",EventNote=result,ActorId=ticket.UserId,OccurredAt=now};
            var audit=new AuditLog{ActorUserId=ticket.UserId,BranchId=request.BranchId,EntityType="internal_requests",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=JsonSerializer.Serialize(new{status=request.Status}),NewValues=JsonSerializer.Serialize(new{status="CLOSED",result}),ChangedFields="[\"status\",\"result\",\"closed_at\"]"};
            db.AddRange(entry,audit);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_closures(event_id,internal_request_id,assignment_id,audit_id,result_note,closed_at) VALUES({entry.EventId},{id},{assignments[0].AssignmentId},{audit.AuditId},{result},{now})",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_escalations SET state='CLOSED',closed_at={now},closed_event_id={entry.EventId} WHERE internal_request_id={id} AND state='OPEN'",ct);
            if(await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignments[0].AssignmentId&&a.Status=="ACTIVE"&&a.EndedAt==null)
                .ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"COMPLETED").SetProperty(a=>a.EndedAt,now),ct)!=1)throw new InternalRequestReadException(409,"IR_HANDLER_CHANGED");
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET status='CLOSED',completed_at={now},closed_at={now},updated_at={now} WHERE internal_request_id={id} AND row_version={version}",ct)!=1)
                throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_completion_outbox(event_id,requester_id,created_at) VALUES({entry.EventId},{request.RequesterId},{now})",ct);
            await SessionAsync(ticket,sid,ct);
            if(!await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
            var finalNow=DateTime.UtcNow;var finalScope=await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId,finalNow,ct);
            if(!await finalScope.Apply(db.InternalRequests.AsNoTracking(),finalNow).AnyAsync(r=>r.InternalRequestId==id,ct))throw new InternalRequestReadException(403,"IR_SCOPE_CHANGED");
            var next=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);await tx.CommitAsync(ct);
            return new(id,entry.EventId,"CLOSED",Convert.ToBase64String(next),"MSG08");
        }
        finally{db.ChangeTracker.Clear();}
    }
}
