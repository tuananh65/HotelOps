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

public sealed record InternalRequestDecisionInput(bool Accept,string? Reason,DateTimeOffset? Deadline);
public sealed record InternalRequestDecisionResult(long RequestId,string Status,string Version,long EventId,string Message);

// No HTTP endpoint is enabled until the complete decision/notification acceptance is verified.
public sealed class InternalRequestDecisionService(HotelOpsDbContext db,InternalRequestProcessAuthority authority,
    ISqlSessionService sessions,ICurrentUserAccessor current)
{
    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
    }

    internal static string? Validate(InternalRequestDecisionInput input)
    {
        var reason=input.Reason?.Trim();
        if(reason?.Any(char.IsControl)==true||reason?.Length>4000||!input.Accept&&string.IsNullOrWhiteSpace(reason)
            ||!input.Accept&&input.Deadline!=null)
            throw new ValidationException("Reject requires a reason and cannot change the deadline; reason must contain at most 4000 characters without controls.");
        return string.IsNullOrEmpty(reason)?null:reason;
    }

    public async Task<InternalRequestDecisionResult> DecideAsync(SessionReference ticket,long sid,long id,
        byte[] expectedVersion,InternalRequestDecisionInput input,CancellationToken ct)
    {
        var reason=Validate(input);
        if(id<=0||expectedVersion.Length!=8)throw new ValidationException("A request and its loaded row version are required.");
        if(db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Decision requires a clean scoped context.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
            await SessionAsync(ticket,sid,ct);
            if(!await InternalRequestDecisionStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_SCHEMA_REQUIRED");
            if(!await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_PROCESS_REQUIRED");
            var request=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==id,ct);
            var version=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);
            if(!version.SequenceEqual(expectedVersion))throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            if(request.Status!="PENDING_ACCEPTANCE")throw new InternalRequestReadException(409,"IR_NOT_PENDING_ACCEPTANCE");
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            if(request.SubmittedAt==null||request.SubmittedAt>now)throw new InternalRequestReadException(409,"IR_SUBMISSION_TIME_INVALID");
            var deadline=input.Deadline?.UtcDateTime??request.DueAt;
            if(deadline!=request.DueAt&&reason==null)throw new ValidationException("Changing the deadline requires a reason.");
            var status=input.Accept?"ACCEPTED":"REJECTED";
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.internal_requests SET status={status},due_at={deadline},updated_at={now}
                WHERE internal_request_id={id} AND row_version={expectedVersion} AND status='PENDING_ACCEPTANCE'
                """,ct);
            if(changed!=1)throw new InternalRequestReadException(412,"IR_VERSION_CHANGED");
            var decision=new InternalRequestEvent{InternalRequestId=id,EventType=input.Accept?"ACCEPT":"REJECT",OldStatus=request.Status,
                NewStatus=status,EventNote=reason,ActorId=ticket.UserId,OccurredAt=now};
            db.InternalRequestEvents.Add(decision);
            db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,BranchId=request.BranchId,EntityType="internal_requests",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action="UPDATE",OccurredAt=now,OldValues=JsonSerializer.Serialize(new{status=request.Status,deadline=request.DueAt}),
                NewValues=JsonSerializer.Serialize(new{operation=decision.EventType,status,deadline,reason,responseAt=now,responseSeconds=(now-request.SubmittedAt.Value).TotalSeconds}),
                ChangedFields="[\"status\",\"due_at\",\"decision\"]"});
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_decision_outbox(event_id,requester_id,created_at) VALUES({decision.EventId},{request.RequesterId},{now})",ct);
            await SessionAsync(ticket,sid,ct);
            if(!await authority.AllowsCurrentRequestAsync(ticket.UserId,id,ct))throw new InternalRequestReadException(403,"IR_AUTHORITY_CHANGED");
            var finalVersion=await InternalRequestDecisionStore.RevisionAsync(db,id,ct);
            await tx.CommitAsync(ct);
            return new(id,status,Convert.ToBase64String(finalVersion),decision.EventId,input.Accept?"MSG08":"MSG19");
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
