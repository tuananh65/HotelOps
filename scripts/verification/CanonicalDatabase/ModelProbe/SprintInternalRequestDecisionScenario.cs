using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintInternalRequestDecisionScenario
{
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo result){result=user;return true;}}

    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,List<object> checks,CancellationToken ct,string? username=null,string? password=null)
    {
        var original=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var originalDepartment=await db.Users.Where(u=>u.UserId==actor).Select(u=>u.DepartmentId).SingleAsync(ct);
        var credential=SessionCredential.Generate();var now=DateTime.UtcNow;
        var session=new UserSession{UserId=actor,SessionKeyHash=SessionCredential.Hash(credential),CreatedAt=now,ExpiresAt=now.AddMinutes(20)};
        var trigger="TR_DEV3_IRDecision_"+Guid.NewGuid().ToString("N");var installed=false;
        var createdPermission=!await db.Permissions.AnyAsync(p=>p.PermissionCode=="IR_VIEW",ct);long viewGrant=0;
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(new{internalRequestDecisionCheck=label});}
        async Task<byte[]> Revision()=>await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        async Task Pending()
        {
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"PENDING_ACCEPTANCE")
                .SetProperty(r=>r.SubmittedAt,DateTime.UtcNow.AddMinutes(-5)).SetProperty(r=>r.DueAt,DateTime.UtcNow.AddDays(1)),ct);
        }
        async Task ClearDecisions()
        {
            var notices=await db.Notifications.Where(n=>(n.SourceType=="INTERNAL_REQUEST_DECISION"||n.SourceType=="INTERNAL_REQUEST_ASSIGNMENT")
                &&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==n.SourceId)).Select(n=>n.NotificationId).ToArrayAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_decision_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_ASSIGNMENT")=="1")
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_assignment_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
                await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            }
            await db.NotificationRecipients.Where(r=>notices.Contains(r.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Notifications.Where(n=>notices.Contains(n.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestEvents.Where(e=>e.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()).ExecuteDeleteAsync(CancellationToken.None);
        }
        try
        {
            var script=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/InternalRequestDecisionIsolated.sql",ct);
            await db.Database.ExecuteSqlRawAsync(script,ct);await db.Database.ExecuteSqlRawAsync(script,ct);installed=true;
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var persisted=await db.UserSessions.AsNoTracking().SingleAsync(s=>s.SessionId==session.SessionId,ct);
            var ticket=SessionReference.FromProtectedTicket(actor,credential,false,DateTime.SpecifyKind(persisted.CreatedAt,DateTimeKind.Utc));
            InternalRequestDecisionService Service(HotelOpsDbContext context)=>new(context,new(context),new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),new Identity(new(actor,session.SessionId,false)));
            var service=Service(db);
            await Pending();var version=await Revision();
            var wrongSession=new InternalRequestDecisionService(db,new(db),new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),new Identity(new(actor,session.SessionId+1,false)));
            var sessionDenied=false;try{await wrongSession.DecideAsync(ticket,session.SessionId,request,version,new(true,null,null),ct);}catch(InternalRequestReadException e)when(e.Status==401){sessionDenied=true;}
            Check(sessionDenied,"decision rejects principal/session mismatch");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}",ct);
            var mappingDenied=false;try{await service.DecideAsync(ticket,session.SessionId,request,version,new(true,null,null),ct);}catch(InternalRequestReadException e)when(e.Status==403){mappingDenied=true;}
            Check(mappingDenied,"decision rejects revoked current-department mapping");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}",ct);
            Check(!await db.InternalRequestEvents.AnyAsync(e=>e.InternalRequestId==request,ct),"failed authorization creates no decision");
            var accepted=await service.DecideAsync(ticket,session.SessionId,request,version,new(true,"Synthetic deadline change",DateTimeOffset.UtcNow.AddDays(2)),ct);
            Check(accepted.Status=="ACCEPTED"&&accepted.Message=="MSG08"&&!version.SequenceEqual(Convert.FromBase64String(accepted.Version)),"accept records ACCEPTED and new row version");
            Check(await db.InternalRequestEvents.AnyAsync(e=>e.EventId==accepted.EventId&&e.OldStatus=="PENDING_ACCEPTANCE"&&e.NewStatus=="ACCEPTED"&&e.ActorId==actor,ct)
                &&await db.AuditLogs.AnyAsync(a=>a.EntityId==request.ToString()&&a.EntityType=="internal_requests"&&a.ActorUserId==actor&&a.NewValues!.Contains("responseSeconds"),ct)
                &&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_decision_outbox WHERE event_id={accepted.EventId} AND requester_id={original.RequesterId}").SingleAsync(ct)==1,"decision event response time audit and requester intent committed together");
            var delivery=new InternalRequestDecisionDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
            Check(await delivery.DeliverAsync(accepted.EventId,ct)=="RECIPIENT_READ_DENIED","decision notice waits while requester lacks IR_VIEW");
            var readSeed=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/InternalRequestViewIsolated.sql",ct);
            await db.Database.ExecuteSqlRawAsync(readSeed,ct);
            var readPermission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_VIEW",ct);
            var managerRole=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
            if(await db.RolePermissions.AnyAsync(g=>g.RoleId==managerRole.RoleId&&g.PermissionId==readPermission.PermissionId,ct))throw new Exception("Do not overwrite pre-existing IR_VIEW grant");
            var grant=new RolePermission{RoleId=managerRole.RoleId,PermissionId=readPermission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow};
            db.RolePermissions.Add(grant);await db.SaveChangesAsync(ct);viewGrant=grant.RolePermissionId;db.ChangeTracker.Clear();
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_ROUTE")=="1")
                await SprintInternalRequestRouteScenario.RunAsync(db,actor,request,ticket,session.SessionId,checks,ct,username,password);
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_ASSIGNMENT")=="1")
                await SprintInternalRequestAssignmentScenario.RunAsync(db,actor,request,ticket,session.SessionId,checks,ct);
            Check((await delivery.PendingAsync(0,100,ct)).Contains(accepted.EventId),"committed decision intent is discoverable for retry");
            Check(await delivery.DeliverAsync(accepted.EventId,ct)=="DELIVERED"&&await delivery.DeliverAsync(accepted.EventId,ct)=="ALREADY_DELIVERED","requester notification delivery is idempotent");
            var notice=await db.Notifications.SingleAsync(n=>n.SourceType=="INTERNAL_REQUEST_DECISION"&&n.SourceId==accepted.EventId,ct);db.ChangeTracker.Clear();
            var center=new NotificationCenterService(db,new Identity(new(actor,session.SessionId,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions()));
            Check((await center.ResolveSourceAsync(notice.NotificationId,ct)).Path==$"/internal-requests?requestId={request}","notification source resolves to authorized current request");
            await db.RolePermissions.Where(g=>g.RolePermissionId==viewGrant).ExecuteDeleteAsync(ct);
            var sourceDenied=false;try{await center.ResolveSourceAsync(notice.NotificationId,ct);}catch(HotelOps.Api.Services.Insights.InsightException e)when(e.Status==404){sourceDenied=true;}
            Check(sourceDenied,"revoked IR_VIEW denies opening historical decision notification");
            var stale=false;try{await service.DecideAsync(ticket,session.SessionId,request,version,new(false,"Stale decision",null),ct);}catch(InternalRequestReadException e)when(e.Status==412){stale=true;}
            Check(stale,"stale version cannot overwrite accepted decision");
            var notPending=false;try{await service.DecideAsync(ticket,session.SessionId,request,await Revision(),new(false,"Duplicate decision",null),ct);}catch(InternalRequestReadException e)when(e.Status==409){notPending=true;}
            Check(notPending,"current version still cannot decide an already accepted request");
            await ClearDecisions();await Pending();version=await Revision();
            var invalid=false;try{await service.DecideAsync(ticket,session.SessionId,request,version,new(false,"  ",null),ct);}catch(ValidationException){invalid=true;}
            Check(invalid,"reject requires nonblank reason");
            invalid=false;try{await service.DecideAsync(ticket,session.SessionId,request,version,new(true,null,DateTimeOffset.UtcNow.AddDays(3)),ct);}catch(ValidationException){invalid=true;}
            Check(invalid,"deadline adjustment requires reason");
            var rejected=await service.DecideAsync(ticket,session.SessionId,request,version,new(false,"Synthetic cannot serve",null),ct);
            Check(rejected.Status=="REJECTED"&&rejected.Message=="MSG19","reject commits reason and REJECTED status");
            await ClearDecisions();await Pending();version=await Revision();
            // Owned disposable fault, matching only this synthetic request's decision intent.
            var ddl=$"CREATE TRIGGER dbo.[{trigger}] ON dbo.internal_request_decision_outbox AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.internal_request_events e ON e.event_id=i.event_id WHERE e.internal_request_id={request}) THROW 51994,'Synthetic decision fault',1; END";
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}",ct);
            var failed=false;try{await service.DecideAsync(ticket,session.SessionId,request,version,new(true,null,null),ct);}catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==51994){failed=true;}
            var afterFault=await Revision();
            Check(failed&&version.SequenceEqual(afterFault)&&await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="PENDING_ACCEPTANCE",ct)
                &&!await db.InternalRequestEvents.AnyAsync(e=>e.InternalRequestId==request,ct)
                &&!await db.AuditLogs.AnyAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString(),ct),"outbox fault rolls back state rowversion event audit and intent");
            ddl=$"DROP TRIGGER dbo.[{trigger}]";await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}",ct);
            async Task<int> Compete(bool accept)
            {
                await using var competitor=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                try{await Service(competitor).DecideAsync(ticket,session.SessionId,request,version,new(accept,accept?null:"Concurrent rejection",null),ct);return 200;}
                catch(InternalRequestReadException e)when(e.Status==412){return 412;}
            }
            var outcomes=await Task.WhenAll(Compete(true),Compete(false));
            Check(outcomes.Order().SequenceEqual(new[]{200,412})&&await db.InternalRequestEvents.CountAsync(e=>e.InternalRequestId==request,ct)==1,
                "concurrent accept reject produces one decision and one stale-version rejection");
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_DECISION_BROWSER")=="1")
            {
                if(username==null||password==null)throw new Exception("Browser fixture credentials required");
                var browserGrant=new RolePermission{RoleId=managerRole.RoleId,PermissionId=readPermission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow};
                db.RolePermissions.Add(browserGrant);await db.SaveChangesAsync(ct);viewGrant=browserGrant.RolePermissionId;db.ChangeTracker.Clear();
                var assignBrowser=Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_ASSIGNMENT")=="1";
                if(assignBrowser)await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,original.ReceivingDepartmentId),ct);
                foreach(var accept in new[]{true,false})
                {
                    await ClearDecisions();await Pending();
                    checks.Add(new{internalRequestDecisionBrowser=await SprintInternalRequestDecisionBrowser.RunAsync(username,password,request,accept,ct)});
                    var committed=await db.InternalRequestEvents.AsNoTracking().SingleAsync(e=>e.InternalRequestId==request&&(e.EventType=="ACCEPT"||e.EventType=="REJECT"),ct);
                    Check(committed.NewStatus==(accept?"ACCEPTED":"REJECTED")&&committed.ActorId==actor
                        &&await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status==(assignBrowser&&accept?"IN_PROGRESS":committed.NewStatus),ct)
                        &&await db.AuditLogs.AnyAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()&&a.ActorUserId==actor,ct),
                        "browser decision state event and audit persisted in SQL");
                    Check(await delivery.DeliverAsync(committed.EventId,ct)=="DELIVERED","browser decision produces deliverable requester intent");
                    if(assignBrowser&&accept)
                    {
                        var assigned=await db.InternalRequestEvents.AsNoTracking().SingleAsync(e=>e.InternalRequestId==request&&e.EventType=="ASSIGN",ct);
                        Check(await db.InternalRequestAssignments.AnyAsync(a=>a.InternalRequestId==request&&a.AssigneeId==actor&&a.Status=="ACTIVE",ct),"browser Accept to Assign persists handler and In Progress");
                        Check(await new InternalRequestAssignmentDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions())).DeliverAsync(assigned.EventId,actor,ct)=="DELIVERED","browser assignment intent delivers after commit");
                    }
                }
            }
        }
        finally
        {
            db.ChangeTracker.Clear();
            var ddl=$"DROP TRIGGER IF EXISTS dbo.[{trigger}]";await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}",CancellationToken.None);
            if(installed)await ClearDecisions();
            if(viewGrant>0)await db.RolePermissions.Where(g=>g.RolePermissionId==viewGrant).ExecuteDeleteAsync(CancellationToken.None);
            if(createdPermission)await db.Permissions.Where(p=>p.PermissionCode=="IR_VIEW").ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,originalDepartment),CancellationToken.None);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,original.Status)
                .SetProperty(r=>r.SubmittedAt,original.SubmittedAt).SetProperty(r=>r.DueAt,original.DueAt).SetProperty(r=>r.UpdatedAt,original.UpdatedAt),CancellationToken.None);
            if(session.SessionId>0)await db.UserSessions.Where(s=>s.SessionId==session.SessionId).ExecuteDeleteAsync(CancellationToken.None);
            checks.Add(new{internalRequestDecisionCleanup=true});
        }
    }
}
