using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestCompletionScenario
{
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo result){result=user;return true;}}
    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,List<object> checks,CancellationToken ct)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(new{completionCheck=name});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View requests",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==view.PermissionId,ct))db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=view.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var credential=SessionCredential.Generate();var now=DateTime.UtcNow;
        var session=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(credential),CreatedAt=now,ExpiresAt=now.AddMinutes(20)};
        var assignment=new InternalRequestAssignment{InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=now,Status="ACTIVE"};
        var escalation=new InternalRequestEvent{InternalRequestId=request,EventType="ESCALATE",ActorId=actor,OccurredAt=now};
        db.AddRange(session,assignment,escalation);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_escalations(internal_request_id,opened_event_id,state,opened_at) VALUES({request},{escalation.EventId},'OPEN',{now})",ct);
        var ticket=SessionReference.FromProtectedTicket(actor,credential,false,DateTime.SpecifyKind(now,DateTimeKind.Utc));
        InternalRequestCompletionService Service(HotelOpsDbContext context)=>new(context,new(context),new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),new Identity(new(actor,session.SessionId,false)));
        Task<byte[]> Version()=>db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        var version=await Version();
        async Task Denied(int status,string label,byte[]? tag=null){var denied=false;try{await Service(db).CompleteAsync(ticket,session.SessionId,request,tag??version,"Synthetic completion",ct);}catch(InternalRequestReadException e)when(e.Status==status){denied=true;}Check(denied,label);}
        await Denied(412,"stale version denied",new byte[8]);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={source.BranchId} AND department_id={source.ReceivingDepartmentId}",ct);
        await Denied(403,"revoked department authority denied");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={source.BranchId} AND department_id={source.ReceivingDepartmentId}",ct);
        var beforeAudit=await db.AuditLogs.CountAsync(ct);var beforeEvents=await db.InternalRequestEvents.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_ir_completion_fixture ON dbo.internal_request_completion_outbox AFTER INSERT AS THROW 51911,'Synthetic completion outbox fault',1;",ct);
        var failed=false;try{await Service(db).CompleteAsync(ticket,session.SessionId,request,version,"Synthetic completion",ct);}catch(Microsoft.Data.SqlClient.SqlException){failed=true;}
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_ir_completion_fixture",ct);
        Check(failed&&await db.AuditLogs.CountAsync(ct)==beforeAudit&&await db.InternalRequestEvents.CountAsync(ct)==beforeEvents
            &&(await Version()).SequenceEqual(version)&&await db.InternalRequestAssignments.AnyAsync(a=>a.AssignmentId==assignment.AssignmentId&&a.Status=="ACTIVE"&&a.EndedAt==null,ct)
            &&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalations WHERE internal_request_id={request} AND state='OPEN'").SingleAsync(ct)==1,"outbox fault rolls back receipt audit event assignment escalation and request version");
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
        async Task<int> Close(){await using var context=new HotelOpsDbContext(options);try{await Service(context).CompleteAsync(ticket,session.SessionId,request,version,"  Verified synthetic result  ",ct);return 200;}catch(InternalRequestReadException e){return e.Status;}}
        var race=await Task.WhenAll(Close(),Close());Check(race.Order().SequenceEqual(new[]{200,412}),"concurrent closure commits once");
        Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="CLOSED"&&r.CompletedAt!=null&&r.ClosedAt!=null,ct)
            &&await db.InternalRequestAssignments.AnyAsync(a=>a.AssignmentId==assignment.AssignmentId&&a.Status=="COMPLETED"&&a.EndedAt!=null,ct),"request closed and last handler preserved in ended assignment");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_closures c JOIN dbo.internal_request_escalations e ON e.closed_event_id=c.event_id JOIN dbo.internal_request_completion_outbox o ON o.event_id=c.event_id WHERE c.internal_request_id={request} AND c.result_note='Verified synthetic result' AND e.state='CLOSED' AND o.requester_id={source.RequesterId}").SingleAsync(ct)==1,"result audit-linked receipt escalation and requester intent persist once");
        checks.Add(new{completionLimit="Direct service and SQL, not public API/browser; mandatory material mapping and notification delivery remain incomplete"});
        // This separately created service-test session is not the browser's logout session.
        // Remove only the credential owned by this fixture before the parent checks browser logout.
        await db.UserSessions.Where(s=>s.SessionId==session.SessionId&&s.UserId==actor&&s.SessionKeyHash==session.SessionKeyHash).ExecuteDeleteAsync(ct);
    }
}
