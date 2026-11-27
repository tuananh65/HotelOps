using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

[Collection("Note2511 SQL")]
public sealed class SupervisorHandoverDatabaseTests
{
    private sealed class FixtureClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,11,27,8,25,0,TimeSpan.Zero);
    }

    [TaskSupplyDatabaseFact]
    public async Task PendingSupervisorRequestBlocksAcceptanceStartAndLegacyHandoverWithoutConsumption()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.StaffSupplies',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
            var shift = await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
                where s.IsActive && r.Code == "HK_STAFF" && s.StartMinute == 480 && s.EndMinute == 960
                orderby s.ShiftId select s).FirstAsync(ct);
            var staffId = await (from ur in db.UserRoles join u in db.Users on ur.UserId equals u.UserId
                where ur.RoleId == shift.RoleId && ur.BranchId == shift.BranchId && ur.RevokedAt == null
                    && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
                orderby u.UserId select u.UserId).FirstAsync(ct);
            var day = await db.WorkdayAssignments.SingleAsync(d => d.ShiftId==shift.ShiftId && d.UserId==staffId
                && d.WorkDate==new DateOnly(2026,11,27) && d.Status=="WORKING",ct);
            var clock = new FixtureClock();
            var now = clock.GetUtcNow().UtcDateTime;
            var user = await db.Users.SingleAsync(u => u.UserId == day.UserId, ct);
            var session = new UserSession { UserId=user.UserId, TokenHash=RandomNumberGenerator.GetBytes(32),
                SecurityStamp=user.SecurityStamp, CreatedAt=now, ExpiresAt=now.AddHours(12) };
            db.UserSessions.Add(session);

            await db.SaveChangesAsync(ct);
            var actor = new Actor(new HttpContextAccessor { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.UserId.ToString()),
                    new Claim("session_id",session.SessionId.ToString())],"isolated-fixture")) }});
            var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
            var notifications=new NotificationService(db,actor,access,atomic,clock);
            var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
            var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
            var room=await db.Rooms.FirstAsync(r=>r.BranchId==shift.BranchId&&r.IsActive&&r.OperationalStatus!="OCCUPIED"
                && !db.WorkItems.Any(w=>w.RoomId==r.RoomId&&(w.Kind=="CLEANING"||w.Kind=="REWORK")
                    &&w.Status!="COMPLETED"&&w.Status!="CANCELLED"&&w.Status!="CLOSED"),ct);
            var work=new WorkItem{BranchId=shift.BranchId,WorkNo=Guid.NewGuid().ToString("N"),Kind="CLEANING",Status="ASSIGNED",
                Title="Isolated per-task supply verification",RoomId=room.RoomId,TurnoverNo=room.TurnoverNo,
                CreatedBy=user.UserId,CreatedAt=now,DueAt=now.AddMinutes(30)};
            db.WorkItems.Add(work);await db.SaveChangesAsync(ct);
            db.WorkAssignments.Add(new WorkAssignment{BranchId=shift.BranchId,WorkId=work.WorkId,AssigneeUserId=user.UserId,
                AssignedBy=user.UserId,AssignmentStatus="ACTIVE",AssignedAt=now});await db.SaveChangesAsync(ct);

        var requests=new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notifications);
        var service=new WorkService(db,access,actor,atomic,audit,clock,notifications,documents,new RoomReadinessService(db,actor,clock),supplies,requests);
        var assignment=await db.WorkAssignments.SingleAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct);
        Assert.True(await CleaningContinuationAccess.CanRequestHandover(db,actor,clock,work.WorkId,work.BranchId,ct));
        var request=new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),Reason:"Cannot accept before shift ends");
        await service.Act(work.WorkId,"request-handover",request,ct);
        await service.Act(work.WorkId,"request-handover",request,ct);
        Assert.Equal(1,await db.WorkEvents.CountAsync(e=>e.WorkId==work.WorkId&&e.EventType=="SUPERVISOR_HANDOVER_REQUESTED",ct));
        Assert.True(await CleaningContinuationAccess.HasPendingHandover(db,work.WorkId,ct));
        Assert.False(await CleaningContinuationAccess.CanRequestHandover(db,actor,clock,work.WorkId,work.BranchId,ct));
        foreach(var action in new[]{"accept","start","handover-offer","handover-accept","handover-reject","request-handover"}){
            var error=await Assert.ThrowsAsync<ApiException>(()=>service.Act(work.WorkId,action,
                new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),Reason:"Cannot accept before shift ends",UserId:user.UserId),ct));
            Assert.Equal(action.StartsWith("handover-")?"USE_SUPERVISOR_HANDOVER":action=="request-handover"?"HANDOVER_REQUEST_ALREADY_OPEN":"HANDOVER_REQUEST_PENDING",error.Code);
        }
        Assert.Null(assignment.AcceptedAt);
        Assert.Null(work.ActualStartAt);
        Assert.False(await db.WorkIntervals.AnyAsync(i=>i.WorkId==work.WorkId,ct));
        Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==work.WorkId,ct));
        // A later assignment is a new decision for the assignee, not a lifetime ban on handover.
        assignment.AssignmentStatus="REASSIGNED";
        assignment.EndedAt=now.AddSeconds(1);
        await db.SaveChangesAsync(ct);
        var reassigned=new WorkAssignment{BranchId=work.BranchId,WorkId=work.WorkId,AssigneeUserId=user.UserId,
            AssignedBy=user.UserId,AssignmentStatus="ACTIVE",AssignedAt=now.AddSeconds(1)};
        db.WorkAssignments.Add(reassigned);await db.SaveChangesAsync(ct);
        Assert.False(await CleaningContinuationAccess.HasPendingHandover(db,work.WorkId,ct));
        Assert.True(await CleaningContinuationAccess.CanRequestHandover(db,actor,clock,work.WorkId,work.BranchId,ct));
        reassigned.AcceptedAt=now.AddSeconds(2);await db.SaveChangesAsync(ct);
        Assert.False(await CleaningContinuationAccess.CanRequestHandover(db,actor,clock,work.WorkId,work.BranchId,ct));
        var acceptedError=await Assert.ThrowsAsync<ApiException>(()=>service.Act(work.WorkId,"request-handover",
            new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),Reason:"Cannot hand over accepted work"),ct));
        Assert.Equal("HOUSEKEEPING_HANDOVER_BEFORE_ACCEPT_ONLY",acceptedError.Code);
        await transaction.RollbackAsync(ct);
    }
}
