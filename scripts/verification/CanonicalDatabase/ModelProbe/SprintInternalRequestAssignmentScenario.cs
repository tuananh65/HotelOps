using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.Options;

internal static class SprintInternalRequestAssignmentScenario
{
    private sealed class Identity(CurrentUserInfo user) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo result) { result = user; return true; } }

    internal static async Task RunAsync(HotelOpsDbContext db, long actor, long request, SessionReference ticket, long sid,
        List<object> checks, CancellationToken ct)
    {
        var row = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request, ct);
        var actorDepartment = await db.Users.Where(u => u.UserId == actor).Select(u => u.DepartmentId).SingleAsync(ct);
        var name = "D3ASSIGN" + Guid.NewGuid().ToString("N")[..8];
        var handler = new User { EmployeeCode = name, Username = name, Email = name + "@example.invalid", FullName = "Synthetic handler",
            DepartmentId = row.ReceivingDepartmentId, EmploymentStatus = "ACTIVE", AccountStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
        var trigger = "TR_DEV3_IRAssignment_" + Guid.NewGuid().ToString("N");
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { internalRequestAssignmentCheck = label }); }
        Task<byte[]> Revision() => db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        InternalRequestAssignmentService Service(HotelOpsDbContext context) => new(context, new(context),
            new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), new Identity(new(actor, sid, false)));
        try
        {
            var sql = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/InternalRequestAssignmentIsolated.sql", ct);
            await db.Database.ExecuteSqlRawAsync(sql, ct); await db.Database.ExecuteSqlRawAsync(sql, ct);
            db.Users.Add(handler); await db.SaveChangesAsync(ct);
            db.UserBranchAccesses.Add(new() { UserId = handler.UserId, BranchId = row.BranchId, GrantedAt = DateTime.UtcNow.AddMinutes(-1) });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, row.ReceivingDepartmentId), ct);
            var service = Service(db);
            async Task Denied(int status, string label)
            {
                var before = await Revision(); var denied = false;
                try { await service.AssignAsync(ticket, sid, request, before, new(handler.UserId, null), ct); }
                catch (InternalRequestReadException e) when (e.Status == status) { denied = true; }
                Check(denied && Enumerable.SequenceEqual(before, await Revision()), label);
            }
            await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "PENDING_ACCEPTANCE"), ct);
            await Denied(409, "pending request must be accepted before assignment");
            await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "ACCEPTED"), ct);
            await db.Users.Where(u => u.UserId == handler.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, (long?)null), ct);
            await Denied(400, "handler outside receiving department denied without state change");
            await db.Users.Where(u => u.UserId == handler.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, row.ReceivingDepartmentId).SetProperty(u => u.AccountStatus, "INACTIVE"), ct);
            await Denied(400, "inactive handler denied");
            await db.Users.Where(u => u.UserId == handler.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE"), ct);
            await db.UserBranchAccesses.Where(a => a.UserId == handler.UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
            await Denied(400, "revoked handler branch access denied");
            await db.UserBranchAccesses.Where(a => a.UserId == handler.UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, (DateTime?)null), ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND department_id={row.ReceivingDepartmentId}", ct);
            await Denied(403, "revoked manager mapping denies assignment despite membership");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND department_id={row.ReceivingDepartmentId}", ct);
            var version = await Revision();
            var first = await service.AssignAsync(ticket, sid, request, version, new(handler.UserId, "Synthetic first assignment"), ct);
            Check(first.Status == "IN_PROGRESS" && await db.InternalRequestAssignments.AnyAsync(a => a.AssignmentId == first.AssignmentId && a.AssigneeId == handler.UserId && a.Status == "ACTIVE" && a.EndedAt == null, ct), "first assignment commits active handler and In Progress");
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_assignment_outbox WHERE event_id={first.EventId}").SingleAsync(ct) == 2,
                "requester and handler receive distinct durable intents");
            var delivery=new InternalRequestAssignmentDelivery(db,TimeProvider.System,Options.Create(new NotificationCenterOptions()));
            Check(await delivery.DeliverAsync(first.EventId,handler.UserId,ct)=="RECIPIENT_READ_DENIED","handler without IR_VIEW cannot receive assignment details");
            Check(await delivery.DeliverAsync(first.EventId,actor,ct)=="DELIVERED"&&await delivery.DeliverAsync(first.EventId,actor,ct)=="ALREADY_DELIVERED","requester assignment delivery is idempotent");
            var notice=await db.Notifications.AsNoTracking().SingleAsync(n=>n.SourceType=="INTERNAL_REQUEST_ASSIGNMENT"&&n.SourceId==first.EventId,ct);
            var center=new NotificationCenterService(db,new Identity(new(actor,sid,false)),TimeProvider.System,Options.Create(new NotificationCenterOptions()));
            Check((await center.ResolveSourceAsync(notice.NotificationId,ct)).Path==$"/internal-requests?requestId={request}","assignment notification resolves authorized source");
            var stale = false;
            try { await service.AssignAsync(ticket, sid, request, version, new(actor, null), ct); } catch (InternalRequestReadException e) when (e.Status == 412) { stale = true; }
            Check(stale, "stale assignment cannot replace current handler");
            await Denied(409, "same handler retry does not create duplicate assignment");
            var beforeFault = await Revision();
            var ddl = $"CREATE TRIGGER dbo.[{trigger}] ON dbo.internal_request_assignment_outbox AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.internal_request_events e ON e.event_id=i.event_id WHERE e.internal_request_id={request}) THROW 51995,'Synthetic assignment fault',1; END";
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}", ct);
            var fault = false;
            try { await service.AssignAsync(ticket, sid, request, beforeFault, new(actor, null), ct); }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 51995) { fault = true; }
            Check(fault && Enumerable.SequenceEqual(beforeFault, await Revision()) && await db.InternalRequestAssignments.CountAsync(a => a.InternalRequestId == request, ct) == 1
                && await db.InternalRequestAssignments.AnyAsync(a => a.AssignmentId == first.AssignmentId && a.Status == "ACTIVE" && a.EndedAt == null, ct), "outbox fault rolls back replacement and preserves old active assignment and version");
            ddl = $"DROP TRIGGER dbo.[{trigger}]"; await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}", ct);
            async Task<int> Compete()
            {
                await using var competitor = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                try { await Service(competitor).AssignAsync(ticket, sid, request, beforeFault, new(actor, "Synthetic reassign"), ct); return 200; }
                catch (InternalRequestReadException e) when (e.Status == 412) { return 412; }
            }
            Check((await Task.WhenAll(Compete(), Compete())).Order().SequenceEqual(new[] { 200, 412 }), "concurrent reassignments yield one commit and one stale rejection");
            Check(await db.InternalRequestAssignments.CountAsync(a => a.InternalRequestId == request && a.Status == "ACTIVE", ct) == 1
                && await db.InternalRequestAssignments.AnyAsync(a => a.AssignmentId == first.AssignmentId && a.Status == "REASSIGNED" && a.EndedAt != null, ct), "reassignment retains ended history and exactly one current handler");
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_assignment_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request} AND e.event_type='REASSIGN'").SingleAsync(ct) == 1,
                "requester equals handler deduplicates durable intent");
            Check(await db.AuditLogs.CountAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString() && a.NewValues!.Contains("handler"), ct) == 2,
                "only committed assignments create audit records");
        }
        finally
        {
            db.ChangeTracker.Clear();
            var ddl = $"DROP TRIGGER IF EXISTS dbo.[{trigger}]"; await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_executesql {ddl}", CancellationToken.None);
            var notices=await db.Notifications.Where(n=>n.SourceType=="INTERNAL_REQUEST_ASSIGNMENT"&&db.InternalRequestEvents.Any(e=>e.InternalRequestId==request&&e.EventId==n.SourceId)).Select(n=>n.NotificationId).ToArrayAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE o FROM dbo.internal_request_assignment_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={request}", CancellationToken.None);
            await db.NotificationRecipients.Where(r=>notices.Contains(r.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.Notifications.Where(n=>notices.Contains(n.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestAssignments.Where(a => a.InternalRequestId == request).ExecuteDeleteAsync(CancellationToken.None);
            await db.InternalRequestEvents.Where(e => e.InternalRequestId == request && (e.EventType == "ASSIGN" || e.EventType == "REASSIGN")).ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString() && a.NewValues!.Contains("handler")).ExecuteDeleteAsync(CancellationToken.None);
            await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, actorDepartment), CancellationToken.None);
            await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, row.Status).SetProperty(r => r.UpdatedAt, row.UpdatedAt), CancellationToken.None);
            if (handler.UserId > 0)
            {
                await db.UserBranchAccesses.Where(a => a.UserId == handler.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.Users.Where(u => u.UserId == handler.UserId).ExecuteDeleteAsync(CancellationToken.None);
            }
            checks.Add(new { internalRequestAssignmentCleanup = true });
        }
    }
}
