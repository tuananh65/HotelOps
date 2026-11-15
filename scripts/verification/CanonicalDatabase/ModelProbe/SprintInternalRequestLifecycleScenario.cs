using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestLifecycleScenario
{
    // Parent has established exact owned database identity and a temporary scoped IR_VIEW grant.
    internal static async Task RunAsync(HotelOpsDbContext db,long actor,long request,string username,string password,List<object> checks,CancellationToken ct)
    {
        var original=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var target=await db.Departments.AsNoTracking().FirstAsync(d=>d.BranchId==original.BranchId&&d.DepartmentId!=original.ReceivingDepartmentId,ct);
        var prefix="D3LIFE"+Guid.NewGuid().ToString("N")[..8];
        var managerPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"aA1!";
        var handlerPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"aA1!";
        var users=new[]{new User{EmployeeCode=prefix+"M",Username=prefix+"M",Email=prefix+"M@example.invalid",FullName="Synthetic destination manager",
            DepartmentId=target.DepartmentId,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow},
            new User{EmployeeCode=prefix+"H",Username=prefix+"H",Email=prefix+"H@example.invalid",FullName="Synthetic destination handler",
            DepartmentId=target.DepartmentId,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow}};
        users[0].PasswordHash=new PasswordHasher<User>().HashPassword(users[0],managerPassword);
        users[1].PasswordHash=new PasswordHasher<User>().HashPassword(users[1],handlerPassword);
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(new{internalRequestLifecycleCheck=label});}
        try
        {
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/InternalRequestRouteIsolated.sql",ct),ct);
            db.Users.AddRange(users);await db.SaveChangesAsync(ct);
            var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
            foreach(var user in users)
            {
                db.UserRoles.Add(new(){UserId=user.UserId,RoleId=role.RoleId,BranchId=original.BranchId,AssignedAt=DateTime.UtcNow.AddMinutes(-1),IsActive=true});
                db.UserBranchAccesses.Add(new(){UserId=user.UserId,BranchId=original.BranchId,GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            }
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            // Only the destination manager has processing authority; the other handler has no mapping.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({users[0].UserId},{original.BranchId},{target.DepartmentId},DATEADD(minute,-1,SYSUTCDATETIME()))",ct);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"PENDING_ACCEPTANCE")
                .SetProperty(r=>r.SubmittedAt,DateTime.UtcNow.AddMinutes(-5)).SetProperty(r=>r.DueAt,DateTime.UtcNow.AddDays(1)),ct);
            checks.Add(new{sourceManagerRoute=await SprintInternalRequestRouteBrowser.RunAsync(username,password,request,target.DepartmentId,ct)});
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintInternalRequestLifecycleBrowser.mjs");
            using(var process=Process.Start(start)??throw new Exception("Lifecycle browser start failed"))
            {
                var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{request,originDepartment=original.ReceivingDepartmentId,
                        manager=new{id=users[0].UserId,username=users[0].Username,password=managerPassword},
                        handler=new{id=users[1].UserId,username=users[1].Username,password=handlerPassword}}));process.StandardInput.Close();
                    await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;
                    if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Lifecycle browser failed at "+result.GetProperty("stage").GetString());
                    checks.Add(new{destinationManagerLifecycle=result});
                }
                finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
            }
            var events=await db.InternalRequestEvents.AsNoTracking().Where(e=>e.InternalRequestId==request).OrderBy(e=>e.EventId).ToArrayAsync(ct);
            Check(events.Select(e=>e.EventType).SequenceEqual(new[]{"ROUTE","ACCEPT","ASSIGN","REASSIGN"}),"public lifecycle persists Route Accept Assign Reassign in order");
            Check(events[0].ActorId==actor&&events.Skip(1).All(e=>e.ActorId==users[0].UserId),"each public event retains its actual acting manager");
            Check(await db.InternalRequests.AnyAsync(r=>r.InternalRequestId==request&&r.ReceivingDepartmentId==target.DepartmentId&&r.Status=="IN_PROGRESS",ct),"request remains In Progress in the destination department");
            var assignments=await db.InternalRequestAssignments.AsNoTracking().Where(a=>a.InternalRequestId==request).OrderBy(a=>a.AssignmentId).ToArrayAsync(ct);
            Check(assignments.Length==2&&assignments[0].AssigneeId==users[0].UserId&&assignments[0].Status=="REASSIGNED"&&assignments[0].EndedAt!=null
                &&assignments[1].AssigneeId==users[1].UserId&&assignments[1].Status=="ACTIVE"&&assignments[1].EndedAt==null,"public reassignment preserves ended assignment and one current handler");
            Check(await db.AuditLogs.CountAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString(),ct)==4,"all four public operations persist audit");
            var eventIds=events.Select(e=>e.EventId).ToArray();
            for(var attempt=0;attempt<40;attempt++)
            {
                var delivered=await db.NotificationRecipients.CountAsync(r=>r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED"
                    &&r.Notification.SourceId!=null&&eventIds.Contains(r.Notification.SourceId.Value)
                    &&(r.Notification.SourceType=="INTERNAL_REQUEST_ROUTE"||r.Notification.SourceType=="INTERNAL_REQUEST_DECISION"||r.Notification.SourceType=="INTERNAL_REQUEST_ASSIGNMENT"),ct);
                if(delivered==7)break;await Task.Delay(250,ct);
            }
            await Task.Delay(1500,ct);
            var receipts=await db.NotificationRecipients.AsNoTracking().Where(r=>r.Notification.SourceId!=null&&eventIds.Contains(r.Notification.SourceId.Value)
                &&(r.Notification.SourceType=="INTERNAL_REQUEST_ROUTE"||r.Notification.SourceType=="INTERNAL_REQUEST_DECISION"||r.Notification.SourceType=="INTERNAL_REQUEST_ASSIGNMENT"))
                .Select(r=>new{r.UserId,r.Notification.SourceId,r.DeliveryStatus,r.DeliveryChannel}).ToArrayAsync(ct);
            var expected=new[]{(actor,events[0].EventId),(users[0].UserId,events[0].EventId),(actor,events[1].EventId),
                (actor,events[2].EventId),(users[0].UserId,events[2].EventId),(actor,events[3].EventId),(users[1].UserId,events[3].EventId)};
            Check(receipts.Length==7&&receipts.All(r=>r.DeliveryChannel=="IN_APP"&&r.DeliveryStatus=="DELIVERED")
                &&expected.All(e=>receipts.Count(r=>r.UserId==e.Item1&&r.SourceId==e.Item2)==1),"hosted worker delivers exactly seven expected recipient/event receipts without duplicates");
        }
        finally
        {
            db.ChangeTracker.Clear();
            var notices=await db.Notifications.Where(n=>(n.SourceType=="INTERNAL_REQUEST_ROUTE"||n.SourceType=="INTERNAL_REQUEST_DECISION"||n.SourceType=="INTERNAL_REQUEST_ASSIGNMENT")
                &&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==n.SourceId)).Select(n=>n.NotificationId).ToArrayAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_route_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_assignment_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_decision_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}",CancellationToken.None);
            await db.NotificationRecipients.Where(r=>notices.Contains(r.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Notifications.Where(n=>notices.Contains(n.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestEvents.Where(e=>e.InternalRequestId==request).ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType=="internal_requests"&&a.EntityId==request.ToString()).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,original.Status).SetProperty(r=>r.ReceivingDepartmentId,original.ReceivingDepartmentId)
                .SetProperty(r=>r.SubmittedAt,original.SubmittedAt).SetProperty(r=>r.DueAt,original.DueAt).SetProperty(r=>r.UpdatedAt,original.UpdatedAt),CancellationToken.None);
            foreach(var user in users.Where(u=>u.UserId>0))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.department_management_access WHERE user_id={user.UserId}",CancellationToken.None);
                await db.UserRoles.Where(r=>r.UserId==user.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.UserBranchAccesses.Where(a=>a.UserId==user.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.UserSessions.Where(s=>s.UserId==user.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.AuditLogs.Where(a=>a.ActorUserId==user.UserId||a.EntityType=="users"&&a.EntityId==user.UserId.ToString()).ExecuteDeleteAsync(CancellationToken.None);
                await db.Users.Where(u=>u.UserId==user.UserId&&u.Username==user.Username).ExecuteDeleteAsync(CancellationToken.None);
            }
            checks.Add(new{internalRequestLifecycleCleanup=true});
        }
    }
}
