using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.Options;

internal static class SprintInternalRequestRouteScenario
{
    private sealed class Identity(CurrentUserInfo user) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo result) { result=user;return true; } }

    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,SessionReference ticket,long sid,List<object> checks,CancellationToken ct,string? username=null,string? password=null)
    {
        var original=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var target=await db.Departments.AsNoTracking().FirstAsync(d=>d.BranchId==original.BranchId&&d.DepartmentId!=original.ReceivingDepartmentId,ct);
        var outside=await db.Departments.AsNoTracking().FirstAsync(d=>d.BranchId!=original.BranchId,ct);
        var name="D3ROUTE"+Guid.NewGuid().ToString("N")[..8];
        var manager=new User{EmployeeCode=name,Username=name,Email=name+"@example.invalid",FullName="Synthetic destination manager",EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        var trigger="TR_DEV3_IRRoute_"+Guid.NewGuid().ToString("N");var installed=false;
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(new{internalRequestRouteCheck=label});}
        Task<byte[]> Revision()=>db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        InternalRequestRouteService Service(HotelOpsDbContext context)=>new(context,new(context),new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),new Identity(new(actor,sid,false)));
        async Task Clear()
        {
            var notices=await db.Notifications.Where(n=>n.SourceType=="INTERNAL_REQUEST_ROUTE"&&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==n.SourceId)).Select(n=>n.NotificationId).ToArrayAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_route_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            await db.NotificationRecipients.Where(r=>notices.Contains(r.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Notifications.Where(n=>notices.Contains(n.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestEvents.Where(e=>e.InternalRequestId==request&&e.EventType=="ROUTE").ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()&&a.NewValues!.Contains("ROUTE")).ExecuteDeleteAsync(CancellationToken.None);
        }
        try
        {
            var script=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/InternalRequestRouteIsolated.sql",ct);
            await db.Database.ExecuteSqlRawAsync(script,ct);await db.Database.ExecuteSqlRawAsync(script,ct);installed=true;
            db.Users.Add(manager);await db.SaveChangesAsync(ct);
            var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
            db.UserRoles.Add(new(){UserId=manager.UserId,RoleId=role.RoleId,BranchId=original.BranchId,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});
            db.UserBranchAccesses.Add(new(){UserId=manager.UserId,BranchId=original.BranchId,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({manager.UserId},{original.BranchId},{target.DepartmentId},DATEADD(minute,-1,SYSUTCDATETIME()))",ct);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"ACCEPTED"),ct);
            var service=Service(db);
            async Task Denied(long destination,int status,string label)
            {
                var version=await Revision();var denied=false;
                try{await service.RouteAsync(ticket,sid,request,version,new(destination,"Synthetic routing reason"),ct);}
                catch(InternalRequestReadException e)when(e.Status==status){denied=true;}
                Check(denied&&Enumerable.SequenceEqual(version,await Revision()),label);
            }
            await Denied(target.DepartmentId,409,"accepted request cannot route");
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"PENDING_ACCEPTANCE"),ct);
            await Denied(original.ReceivingDepartmentId!.Value,400,"same department routing denied");
            await Denied(outside.DepartmentId,400,"cross branch routing denied");
            await db.Departments.Where(d=>d.DepartmentId==target.DepartmentId).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.IsActive,false),ct);
            await Denied(target.DepartmentId,400,"inactive target denied");
            await db.Departments.Where(d=>d.DepartmentId==target.DepartmentId).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.IsActive,true),ct);
            var stale=await Revision();
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.UpdatedAt,DateTime.UtcNow),ct);
            var staleDenied=false;try{await service.RouteAsync(ticket,sid,request,stale,new(target.DepartmentId,"Synthetic routing reason"),ct);}catch(InternalRequestReadException e)when(e.Status==412){staleDenied=true;}
            Check(staleDenied,"stale route version denied");
            var before=await Revision();
            var ddl=$"CREATE TRIGGER dbo.[{trigger}] ON dbo.internal_request_route_outbox AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.internal_request_events e ON e.event_id=i.event_id WHERE e.internal_request_id={request}) THROW 51996,'Synthetic route fault',1; END";
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}",ct);
            var failed=false;try{await service.RouteAsync(ticket,sid,request,before,new(target.DepartmentId,"Synthetic routing reason"),ct);}catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==51996){failed=true;}
            Check(failed&&Enumerable.SequenceEqual(before,await Revision())&&!await db.InternalRequestEvents.AnyAsync(e=>e.InternalRequestId==request&&e.EventType=="ROUTE",ct)
                &&!await db.AuditLogs.AnyAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()&&a.NewValues!.Contains("ROUTE"),ct),"outbox fault rolls back department version event audit");
            ddl=$"DROP TRIGGER dbo.[{trigger}]";await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}",ct);
            async Task<int> Compete()
            {
                await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                try{await Service(context).RouteAsync(ticket,sid,request,before,new(target.DepartmentId,"Synthetic routing reason"),ct);return 200;}
                catch(InternalRequestReadException e)when(e.Status is 403 or 412){return e.Status;}
            }
            var results=await Task.WhenAll(Compete(),Compete());
            Check(results.Count(r=>r==200)==1&&results.Count(r=>r is 403 or 412)==1,"concurrent routing commits once and denies the superseded actor/version");
            Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status=="PENDING_ACCEPTANCE"&&r.ReceivingDepartmentId==target.DepartmentId,ct),"route preserves Pending Acceptance and changes receiving department");
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_route_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request} AND (o.recipient_id={actor} OR o.recipient_id={manager.UserId})").SingleAsync(ct)==2,"requester and explicit target manager intents persist atomically");
            await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
            {
                var authority=new InternalRequestProcessAuthority(db);
                Check(!await authority.AllowsCurrentRequestAsync(actor,request,ct)&&await authority.AllowsCurrentRequestAsync(manager.UserId,request,ct),"old department loses mutation authority and new mapped manager gains it");
                await tx.RollbackAsync(ct);
            }
            var entry=await db.InternalRequestEvents.AsNoTracking().SingleAsync(e=>e.InternalRequestId==request&&e.EventType=="ROUTE",ct);
            var delivery=new InternalRequestRouteDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
            Check(await delivery.DeliverAsync(entry.EventId,actor,ct)=="DELIVERED"&&await delivery.DeliverAsync(entry.EventId,actor,ct)=="ALREADY_DELIVERED","route requester notification is idempotent");
            Check(await delivery.DeliverAsync(entry.EventId,manager.UserId,ct)=="DELIVERED","current mapped destination manager receives route notice");
            var managerSession=new UserSession{UserId=manager.UserId,SessionKeyHash=SessionCredential.Hash(SessionCredential.Generate()),CreatedAt=DateTime.UtcNow,ExpiresAt=DateTime.UtcNow.AddMinutes(10)};
            db.UserSessions.Add(managerSession);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var notice=await db.NotificationRecipients.AsNoTracking().Where(r=>r.UserId==manager.UserId&&r.Notification.SourceType=="INTERNAL_REQUEST_ROUTE"&&r.Notification.SourceId==entry.EventId).Select(r=>r.NotificationId).SingleAsync(ct);
            var center=new NotificationCenterService(db,new Identity(new(manager.UserId,managerSession.SessionId,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions()));
            Check((await center.ResolveSourceAsync(notice,ct)).Path==$"/internal-requests?requestId={request}","route notice opens authorized source with valid recipient session");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={manager.UserId}",ct);
            Check(await delivery.DeliverAsync(entry.EventId,manager.UserId,ct)=="RECIPIENT_READ_DENIED","revoked destination mapping denies delivery despite IR_VIEW");
            var sourceDenied=false;try{await center.ResolveSourceAsync(notice,ct);}catch(HotelOps.Api.Services.Insights.InsightException e)when(e.Status==404){sourceDenied=true;}
            Check(sourceDenied,"revoked destination mapping also denies historical notification source");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={manager.UserId}",ct);
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_ROUTE_BROWSER")=="1")
            {
                await Clear();
                await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,original.ReceivingDepartmentId)
                    .SetProperty(r=>r.Status,"PENDING_ACCEPTANCE").SetProperty(r=>r.SubmittedAt,DateTime.UtcNow.AddMinutes(-5)),ct);
                checks.Add(new{routeBrowser=await SprintInternalRequestRouteBrowser.RunAsync(username??throw new Exception("Username required"),password??throw new Exception("Password required"),request,target.DepartmentId,ct)});
                entry=await db.InternalRequestEvents.AsNoTracking().SingleAsync(e=>e.InternalRequestId==request&&e.EventType=="ROUTE",ct);
                Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.ReceivingDepartmentId==target.DepartmentId&&r.Status=="PENDING_ACCEPTANCE",ct)
                    &&await db.AuditLogs.AnyAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()&&a.NewValues!.Contains("ROUTE"),ct),"browser route department event and audit persisted");
                Check(await delivery.DeliverAsync(entry.EventId,actor,ct)=="DELIVERED"&&await delivery.DeliverAsync(entry.EventId,manager.UserId,ct)=="DELIVERED","browser route produces deliverable requester and target-manager intents");
            }
        }
        finally
        {
            db.ChangeTracker.Clear();var ddl=$"DROP TRIGGER IF EXISTS dbo.[{trigger}]";await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}",CancellationToken.None);
            if(installed)await Clear();
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,original.Status).SetProperty(r=>r.ReceivingDepartmentId,original.ReceivingDepartmentId)
                .SetProperty(r=>r.SubmittedAt,original.SubmittedAt).SetProperty(r=>r.UpdatedAt,original.UpdatedAt),CancellationToken.None);
            if(manager.UserId>0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.department_management_access WHERE user_id={manager.UserId}",CancellationToken.None);
                await db.UserRoles.Where(r=>r.UserId==manager.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.UserBranchAccesses.Where(a=>a.UserId==manager.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.UserSessions.Where(s=>s.UserId==manager.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.Users.Where(u=>u.UserId==manager.UserId).ExecuteDeleteAsync(CancellationToken.None);
            }
            checks.Add(new{internalRequestRouteCleanup=true});
        }
    }
}
