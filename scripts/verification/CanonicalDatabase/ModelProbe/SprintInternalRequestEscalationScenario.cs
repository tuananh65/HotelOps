using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Insights;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestEscalationScenario
{
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo result){result=user;return true;}}
    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,List<object> checks,CancellationToken ct)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(new{escalationCheck=name});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var manager=await db.Roles.SingleAsync(r=>r.RoleCode=="OPERATIONS_MANAGER",ct);
        var head=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var create=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_CREATE",ct);
        var view=await db.Permissions.SingleOrDefaultAsync(p=>p.PermissionCode=="IR_VIEW",ct);
        if(view==null){view=new(){PermissionCode="IR_VIEW",PermissionName="View requests",ModuleCode=create.ModuleCode,ResourceCode=create.ResourceCode,ActionCode="VIEW"};db.Permissions.Add(view);await db.SaveChangesAsync(ct);}
        var handle=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_OVERDUE_HANDLE",ct);
        foreach(var role in new[]{head,manager})foreach(var permission in new[]{view,handle})
            if(!await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct))
                db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
        var credential=SessionCredential.Generate();var now=DateTime.UtcNow;
        var session=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(credential),CreatedAt=now,ExpiresAt=now.AddMinutes(20)};
        var assignment=new InternalRequestAssignment{InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=now,Status="ACTIVE"};
        db.AddRange(session,assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.CreatedAt,now.AddHours(-10)).SetProperty(r=>r.DueAt,now.AddHours(2)),ct);
        var ticket=SessionReference.FromProtectedTicket(actor,credential,false,DateTime.SpecifyKind(now,DateTimeKind.Utc));
        InternalRequestEscalationService Service(HotelOpsDbContext context)=>new(context,new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),new Identity(new(actor,session.SessionId,false)));
        Task<byte[]> Version()=>db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        var input=new InternalRequestEscalationInput("Synthetic deadline intervention",Deadline:new DateTimeOffset(now.AddHours(4)));
        async Task Denied(int status,string label,byte[]? tag=null)
        {
            var denied=false;try{await Service(db).EscalateAsync(ticket,session.SessionId,request,tag??await Version(),input,ct);}
            catch(InternalRequestReadException e)when(e.Status==status){denied=true;}Check(denied,label);
        }
        await Denied(403,"Department Head with IR_OVERDUE_HANDLE cannot escalate");
        var roleAssignment=new UserRole{UserId=actor,RoleId=manager.RoleId,BranchId=source.BranchId,AssignedAt=now.AddMinutes(-1),IsActive=true};
        db.UserRoles.Add(roleAssignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await Denied(412,"stale version denied",new byte[8]);
        await db.UserRoles.Where(r=>r.UserRoleId==roleAssignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        await Denied(403,"revoked Operations Manager role denied");
        await db.UserRoles.Where(r=>r.UserRoleId==roleAssignment.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        foreach(var status in new[]{"CLOSED","REJECTED","CANCELLED"})
        {
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,status),ct);
            await Denied(409,"terminal "+status+" denied");
        }
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"IN_PROGRESS").SetProperty(r=>r.DueAt,now.AddHours(100)),ct);
        await Denied(409,"under eighty percent denied");
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.DueAt,now.AddHours(2)),ct);
        foreach(var invalid in new[]{input with{Deadline=new DateTimeOffset(now.AddHours(-11))},input with{PriorityId=long.MaxValue},input with{Reason="short"},input with{Deadline=new DateTimeOffset(now.AddHours(2))}})
        {
            var denied=false;try{await Service(db).EscalateAsync(ticket,session.SessionId,request,await Version(),invalid,ct);}catch(ValidationException){denied=true;}
            Check(denied,"invalid intervention rejected without side effects");
        }
        var events=await db.InternalRequestEvents.CountAsync(ct);var audits=await db.AuditLogs.CountAsync(ct);var version=await Version();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_ir_escalation_fixture ON dbo.internal_request_escalation_outbox AFTER INSERT AS THROW 51924,'Synthetic outbox fault',1;",ct);
        var failed=false;
        try{await Service(db).EscalateAsync(ticket,session.SessionId,request,version,input,ct);}catch(Microsoft.Data.SqlClient.SqlException){failed=true;}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_ir_escalation_fixture",ct);}
        Check(failed&&events==await db.InternalRequestEvents.CountAsync(ct)&&audits==await db.AuditLogs.CountAsync(ct),"outbox fault rolls back event and audit");
        var afterFault=await Version();Check(version.SequenceEqual(afterFault),"outbox fault rolls back request and version");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalations WHERE internal_request_id={request}").SingleAsync(ct)==0,"outbox fault leaves no escalation");
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options;
        async Task<(InternalRequestEscalationResult? Result,int Status)> Attempt()
        {
            await using var context=new HotelOpsDbContext(options);
            try{return(await Service(context).EscalateAsync(ticket,session.SessionId,request,version,input,ct),200);}
            catch(InternalRequestReadException e){return(null,e.Status);}
        }
        var race=await Task.WhenAll(Attempt(),Attempt());
        Check(race.Count(r=>r.Status==200)==1&&race.Count(r=>r.Status==412)==1,"concurrent requests create exactly one escalation and reject stale loser");
        var result=race.Single(r=>r.Result!=null).Result!;
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalations WHERE internal_request_id={request} AND state='OPEN'").SingleAsync(ct)==1,"one Open escalation persists");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalation_actions a JOIN dbo.audit_logs l ON l.audit_id=a.audit_id JOIN dbo.internal_request_events e ON e.event_id=a.event_id WHERE a.event_id={result.EventId} AND e.event_type='ESCALATE' AND l.actor_user_id={actor}").SingleAsync(ct)==1,"escalation reason and audit provenance persisted");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_escalation_outbox WHERE event_id={result.EventId} AND recipient_id={actor} AND is_requester=1 AND is_handler=1 AND is_manager=1").SingleAsync(ct)==1,"requester handler manager deduplicated into one durable intent");
        var updated=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        Check(updated.DueAt==input.Deadline!.Value.UtcDateTime&&updated.CreatedAt==now.AddHours(-10)&&updated.Status=="IN_PROGRESS","deadline updated without resetting creation or authoritative lifecycle");
        await Denied(409,"second Open escalation denied with current version");
        var priority=new ReferenceValue{GroupCode="PRIORITY",ValueCode="D3ESC"+Guid.NewGuid().ToString("N")[..8],DisplayName="Synthetic intervention",IsActive=true};
        db.ReferenceValues.Add(priority);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var overdue=new InternalRequest{RequestNo="D3ESC"+Guid.NewGuid().ToString("N")[..12],BranchId=source.BranchId,RequestTypeId=source.RequestTypeId,
            RequesterId=actor,ReceivingDepartmentId=source.ReceivingDepartmentId,Subject="Synthetic overdue priority",Description="Synthetic",Status="PENDING_ACCEPTANCE",CreatedAt=now.AddHours(-10),DueAt=now.AddHours(-1)};
        db.InternalRequests.Add(overdue);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var overdueVersion=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={overdue.InternalRequestId}").SingleAsync(ct);
        await db.ReferenceValues.Where(r=>r.ReferenceId==priority.ReferenceId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
        var inactiveDenied=false;try{await Service(db).EscalateAsync(ticket,session.SessionId,overdue.InternalRequestId,overdueVersion,new("Synthetic priority intervention",priority.ReferenceId),ct);}catch(ValidationException){inactiveDenied=true;}
        Check(inactiveDenied,"inactive priority cannot be used for intervention");
        await db.ReferenceValues.Where(r=>r.ReferenceId==priority.ReferenceId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,true),ct);
        await Service(db).EscalateAsync(ticket,session.SessionId,overdue.InternalRequestId,overdueVersion,new("Synthetic priority intervention",priority.ReferenceId),ct);
        var reprioritized=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==overdue.InternalRequestId,ct);
        Check(reprioritized.PriorityId==priority.ReferenceId&&reprioritized.DueAt==overdue.DueAt&&reprioritized.Status==overdue.Status,"overdue request priority intervention preserves deadline and lifecycle");
        var noticeOptions=Options.Create(new NotificationCenterOptions());
        InternalRequestEscalationDelivery Delivery(HotelOpsDbContext context)=>new(context,TimeProvider.System,noticeOptions);
        Check((await Delivery(db).PendingAsync(0,ct)).Contains(result.EventId),"committed escalation is pending delivery");
        await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==source.BranchId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
        Check(await Delivery(db).DeliverAsync(result.EventId,actor,ct)=="RECIPIENT_READ_DENIED","branch access revoked after intent prevents delivery");
        await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==source.BranchId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,(DateTime?)null),ct);
        var noticeCount=await db.Notifications.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_ir_escalation_delivery_fixture ON dbo.internal_request_escalation_outbox AFTER UPDATE AS THROW 51925,'Synthetic delivery receipt fault',1;",ct);
        var deliveryFailed=false;
        try{await Delivery(db).DeliverAsync(result.EventId,actor,ct);}catch(Microsoft.Data.SqlClient.SqlException){deliveryFailed=true;}
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_ir_escalation_delivery_fixture",ct);}
        Check(deliveryFailed&&await db.Notifications.CountAsync(ct)==noticeCount,"delivery fault rolls back notification and acknowledgment requirement");
        async Task<string> Deliver(){await using var context=new HotelOpsDbContext(options);return await Delivery(context).DeliverAsync(result.EventId,actor,ct);}
        var deliveries=await Task.WhenAll(Deliver(),Deliver());
        Check(deliveries.Count(r=>r=="DELIVERED")==1&&deliveries.Count(r=>r=="ALREADY_DELIVERED")==1,"concurrent delivery produces one notification");
        var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="INTERNAL_REQUEST_ESCALATION"&&n.SourceId==result.EventId,ct);
        var inbox=new NotificationCenterService(db,new Identity(new(actor,session.SessionId,false)),TimeProvider.System,noticeOptions);
        Check((await inbox.ResolveSourceAsync(notice.NotificationId,ct)).Path==$"/internal-requests?requestId={request}","notification resolves authorized canonical request");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notifications n JOIN dbo.notification_recipients r ON r.notification_id=n.notification_id WHERE n.notification_id={notice.NotificationId} AND n.requires_acknowledgment=1 AND r.acknowledged_at IS NULL AND r.read_at IS NULL").SingleAsync(ct)==1,"delivered escalation requires explicit acknowledgment");
        await inbox.AcknowledgeAsync([notice.NotificationId],ct);db.ChangeTracker.Clear();
        var ackAudits=await db.AuditLogs.CountAsync(ct);
        await inbox.AcknowledgeAsync([notice.NotificationId],ct);db.ChangeTracker.Clear();
        Check(await db.AuditLogs.CountAsync(ct)==ackAudits&&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_recipients WHERE notification_id={notice.NotificationId} AND acknowledged_at IS NOT NULL AND read_at IS NOT NULL").SingleAsync(ct)==1,"acknowledgment persists read and audit once without mutating request");
        await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==source.BranchId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,DateTime.UtcNow),ct);
        var sourceDenied=false;try{await inbox.ResolveSourceAsync(notice.NotificationId,ct);}catch(InsightException e)when(e.Status==404){sourceDenied=true;}
        Check(sourceDenied,"opening source after branch revocation is denied");
        await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==source.BranchId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.RevokedAt,(DateTime?)null),ct);
        // Only this fixture's independent service credential; parent separately verifies browser logout.
        await db.UserSessions.Where(s=>s.SessionId==session.SessionId&&s.UserId==actor&&s.SessionKeyHash==session.SessionKeyHash).ExecuteDeleteAsync(ct);
    }
}
