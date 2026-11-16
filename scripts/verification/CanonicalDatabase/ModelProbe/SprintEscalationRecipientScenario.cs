using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintEscalationRecipientScenario
{
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo result){result=user;return true;}}

    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long sourceId,SessionReference ticket,long sid,List<object> checks,CancellationToken ct)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(new{escalationRecipientCheck=name});}
        var source=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==sourceId,ct);
        var head=await db.Roles.AsNoTracking().SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var prefix="D3ERC"+Guid.NewGuid().ToString("N")[..8];var now=DateTime.UtcNow;
        User User(string suffix)=>new(){Username=prefix+suffix,EmployeeCode=prefix+suffix,Email=prefix+suffix+"@example.invalid",FullName="Synthetic "+suffix,
            AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=now};
        var manager=User("Manager");var handler=User("Handler");var foreign=User("Foreign");
        db.Users.AddRange(manager,handler,foreign);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var other=await db.Departments.AsNoTracking().FirstAsync(d=>d.BranchId!=source.BranchId,ct);
        foreach(var item in new[]{(manager,source.BranchId),(handler,source.BranchId),(foreign,other.BranchId)})
        {
            db.UserRoles.Add(new(){UserId=item.Item1.UserId,RoleId=head.RoleId,BranchId=item.Item2,AssignedAt=now.AddMinutes(-1),IsActive=true});
            db.UserBranchAccesses.Add(new(){UserId=item.Item1.UserId,BranchId=item.Item2,GrantedAt=now.AddMinutes(-1)});
        }
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({manager.UserId},{source.BranchId},{source.ReceivingDepartmentId},{now})",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({foreign.UserId},{other.BranchId},{other.DepartmentId},{now})",ct);
        var request=new InternalRequest{RequestNo=prefix,BranchId=source.BranchId,RequestTypeId=source.RequestTypeId,RequesterId=actor,ReceivingDepartmentId=source.ReceivingDepartmentId,
            Subject="Synthetic recipient authority",Description="Synthetic",Status="IN_PROGRESS",CreatedAt=now.AddHours(-10),DueAt=now.AddHours(-1)};
        db.InternalRequests.Add(request);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var assignment=new InternalRequestAssignment{InternalRequestId=request.InternalRequestId,AssigneeId=handler.UserId,AssignedBy=actor,AssignedAt=now,Status="ACTIVE"};
        db.InternalRequestAssignments.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var version=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request.InternalRequestId}").SingleAsync(ct);
        var service=new InternalRequestEscalationService(db,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),new Identity(new(actor,sid,false)));
        var result=await service.EscalateAsync(ticket,sid,request.InternalRequestId,version,new("Synthetic distinct recipients",Deadline:new DateTimeOffset(now.AddHours(2))),ct);
        var recipients=await db.Database.SqlQuery<long>($"SELECT recipient_id AS Value FROM dbo.internal_request_escalation_outbox WHERE event_id={result.EventId}").ToArrayAsync(ct);
        Check(recipients.Order().SequenceEqual(new[]{actor,manager.UserId,handler.UserId}.Order()),"intent includes requester current handler mapped manager; excludes foreign manager");
        Check(manager.DepartmentId==null,"manager authority is explicit mapping, not home department");
        var delivery=new InternalRequestEscalationDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={manager.UserId} AND branch_id={source.BranchId} AND department_id={source.ReceivingDepartmentId}",ct);
        Check(await delivery.DeliverAsync(result.EventId,manager.UserId,ct)=="RECIPIENT_READ_DENIED","manager-only recipient loses delivery when mapping revoked");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={manager.UserId} AND branch_id={source.BranchId} AND department_id={source.ReceivingDepartmentId}",ct);
        async Task RemoveGrant(string code)
        {
            var grant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==head.RoleId&&g.Permission.PermissionCode==code,ct);
            await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
            Check(await delivery.DeliverAsync(result.EventId,manager.UserId,ct)=="RECIPIENT_READ_DENIED","manager-only recipient requires current "+code);
            db.RolePermissions.Add(new(){RoleId=grant.RoleId,PermissionId=grant.PermissionId,DataScope=grant.DataScope,GrantedAt=grant.GrantedAt,GrantedBy=grant.GrantedBy});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        }
        await RemoveGrant("IR_VIEW");await RemoveGrant("IR_PROCESS");
        Check(await delivery.DeliverAsync(result.EventId,manager.UserId,ct)=="DELIVERED","eligible manager-only recipient receives alert");
        await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"REASSIGNED").SetProperty(a=>a.EndedAt,DateTime.UtcNow),ct);
        Check(await delivery.DeliverAsync(result.EventId,handler.UserId,ct)=="RECIPIENT_READ_DENIED","former handler-only recipient cannot receive after responsibility ends");
        await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"ACTIVE").SetProperty(a=>a.EndedAt,(DateTime?)null),ct);
        Check(await delivery.DeliverAsync(result.EventId,handler.UserId,ct)=="DELIVERED","current handler-only recipient receives alert");
        var sessions=new List<UserSession>();
        async Task<NotificationCenterService> Center(long user)
        {
            var session=new UserSession{UserId=user,SessionKeyHash=SessionCredential.Hash(SessionCredential.Generate()),CreatedAt=DateTime.UtcNow,ExpiresAt=DateTime.UtcNow.AddMinutes(10)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();sessions.Add(session);
            return new(db,new Identity(new(user,session.SessionId,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions()));
        }
        var managerCenter=await Center(manager.UserId);var handlerCenter=await Center(handler.UserId);
        var managerNotice=await db.NotificationRecipients.Where(r=>r.UserId==manager.UserId&&r.Notification.SourceId==result.EventId&&r.Notification.SourceType=="INTERNAL_REQUEST_ESCALATION").Select(r=>r.NotificationId).SingleAsync(ct);
        var handlerNotice=await db.NotificationRecipients.Where(r=>r.UserId==handler.UserId&&r.Notification.SourceId==result.EventId&&r.Notification.SourceType=="INTERNAL_REQUEST_ESCALATION").Select(r=>r.NotificationId).SingleAsync(ct);
        Check((await managerCenter.ResolveSourceAsync(managerNotice,ct)).Path==$"/internal-requests?requestId={request.InternalRequestId}","manager-only source authorized with current mapping");
        Check((await handlerCenter.ResolveSourceAsync(handlerNotice,ct)).Path==$"/internal-requests?requestId={request.InternalRequestId}","handler-only source authorized with current assignment");
        var target=new Department{BranchId=source.BranchId,DepartmentCode=prefix,DepartmentName="Synthetic route target",IsActive=true};
        db.Departments.Add(target);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        // Fixture-only responsibility changes test read revocation; this is not UC122 reroute implementation.
        await db.InternalRequests.Where(r=>r.InternalRequestId==request.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,target.DepartmentId),ct);
        async Task Denied(NotificationCenterService center,long notice,string label)
        {var denied=false;try{await center.ResolveSourceAsync(notice,ct);}catch(InsightException e)when(e.Status==404){denied=true;}Check(denied,label);}
        await Denied(managerCenter,managerNotice,"manager source denied after receiving department changes");
        await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,"REASSIGNED").SetProperty(a=>a.EndedAt,DateTime.UtcNow),ct);
        await Denied(handlerCenter,handlerNotice,"former handler source denied after current assignment ends");
        Check(await db.NotificationRecipients.CountAsync(r=>r.Notification.SourceId==result.EventId&&r.Notification.SourceType=="INTERNAL_REQUEST_ESCALATION",ct)==2,"source revocation retains delivered recipient history");
        foreach(var session in sessions)await db.UserSessions.Where(s=>s.SessionId==session.SessionId&&s.UserId==session.UserId&&s.SessionKeyHash==session.SessionKeyHash).ExecuteDeleteAsync(ct);
    }
}
