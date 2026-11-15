using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestWorkerScenario
{
    // Called only inside the exact-identity, empty-disposable authority fixture.
    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,string username,string password,List<object> checks,CancellationToken ct)
    {
        var original=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var department=await db.Users.Where(u=>u.UserId==actor).Select(u=>u.DepartmentId).SingleAsync(ct);
        var createdPermission=!await db.Permissions.AnyAsync(p=>p.PermissionCode=="IR_VIEW",ct);long grantId=0;
        async Task Clear()
        {
            var notices=await db.Notifications.Where(n=>(n.SourceType=="INTERNAL_REQUEST_DECISION"||n.SourceType=="INTERNAL_REQUEST_ASSIGNMENT")
                &&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==n.SourceId)).Select(n=>n.NotificationId).ToArrayAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_decision_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_assignment_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            await db.NotificationRecipients.Where(r=>notices.Contains(r.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Notifications.Where(n=>notices.Contains(n.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestEvents.Where(e=>e.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()).ExecuteDeleteAsync(CancellationToken.None);
        }
        try
        {
            foreach(var file in new[]{"InternalRequestDecisionIsolated.sql","InternalRequestAssignmentIsolated.sql","InternalRequestViewIsolated.sql"})
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/"+file,ct),ct);
            var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
            var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="IR_VIEW",ct);
            if(await db.RolePermissions.AnyAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct))throw new Exception("Preserve existing read grant");
            var grant=new RolePermission{RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow};
            db.RolePermissions.Add(grant);await db.SaveChangesAsync(ct);grantId=grant.RolePermissionId;db.ChangeTracker.Clear();
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,original.ReceivingDepartmentId),ct);
            if(Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_LIFECYCLE")=="1")
            {
                await Clear();
                await SprintInternalRequestLifecycleScenario.RunAsync(db,actor,request,username,password,checks,ct);
                return;
            }
            foreach(var accept in new[]{true,false})
            {
                await Clear();
                await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"PENDING_ACCEPTANCE")
                    .SetProperty(r=>r.SubmittedAt,DateTime.UtcNow.AddMinutes(-5)).SetProperty(r=>r.DueAt,DateTime.UtcNow.AddDays(1)),ct);
                checks.Add(new{workerBrowser=await SprintInternalRequestDecisionBrowser.RunAsync(username,password,request,accept,ct)});
                var expected=accept?2:1;
                var delivered=false;
                for(var attempt=0;attempt<40;attempt++)
                {
                    var count=await db.NotificationRecipients.CountAsync(r=>r.UserId==actor&&r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED"
                        &&(r.Notification.SourceType=="INTERNAL_REQUEST_DECISION"||r.Notification.SourceType=="INTERNAL_REQUEST_ASSIGNMENT")
                        &&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==r.Notification.SourceId),ct);
                    if(count==expected){delivered=true;break;}
                    await Task.Delay(250,ct);
                }
                if(!delivered)throw new Exception("Scheduled worker failed to deliver committed browser events");
                if(!await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.Status==(accept?"IN_PROGRESS":"REJECTED"),ct))throw new Exception("Browser state mismatch");
                // A second scan must not create a duplicate notification.
                await Task.Delay(1500,ct);
                var total=await db.Notifications.CountAsync(n=>(n.SourceType=="INTERNAL_REQUEST_DECISION"||n.SourceType=="INTERNAL_REQUEST_ASSIGNMENT")
                    &&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==n.SourceId),ct);
                if(total!=expected)throw new Exception("Scheduled delivery duplicated an event");
                checks.Add(new{scheduledDelivery=accept?"Accept and Assign delivered automatically once":"Reject delivered automatically once",notificationCount=total});
            }
        }
        finally
        {
            db.ChangeTracker.Clear();await Clear();
            if(grantId>0)await db.RolePermissions.Where(g=>g.RolePermissionId==grantId).ExecuteDeleteAsync(CancellationToken.None);
            if(createdPermission)await db.Permissions.Where(p=>p.PermissionCode=="IR_VIEW").ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u=>u.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.DepartmentId,department),CancellationToken.None);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,original.Status)
                .SetProperty(r=>r.SubmittedAt,original.SubmittedAt).SetProperty(r=>r.DueAt,original.DueAt).SetProperty(r=>r.UpdatedAt,original.UpdatedAt),CancellationToken.None);
            checks.Add(new{workerBrowserCleanup=true});
        }
    }
}
