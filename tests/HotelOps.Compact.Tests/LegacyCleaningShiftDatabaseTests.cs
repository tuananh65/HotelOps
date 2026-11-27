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
public sealed class LegacyCleaningShiftDatabaseTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTime Now = new(2026,11,27,8,5,0,DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    [AutomaticShiftDatabaseFact]
    public async Task CurrentShiftConsumesFullNormForPreviouslyStartedCleaningAndPreservesHistory()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var clock=new Clock();
        var work=await db.WorkItems.Where(w=>w.Kind=="CLEANING"&&w.Status=="IN_PROGRESS"&&w.ActualStartAt<clock.Now.Date
            && !db.StaffSupplyLedgers.Any(l=>l.WorkId==w.WorkId&&l.Kind=="USE")).OrderBy(w=>w.WorkId).FirstAsync(ct);
        var assignment=await db.WorkAssignments.SingleAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct);
        var userId=assignment.AssigneeUserId;
        // The approved Norm clone already contains the active current-day allocation.
        var shift=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
            where s.BranchId==work.BranchId&&s.IsActive&&r.Code=="HK_STAFF"&&s.StartMinute==480&&s.EndMinute==960 select s).SingleAsync(ct);
        var day=await db.WorkdayAssignments.SingleOrDefaultAsync(a=>a.ShiftId==shift.ShiftId&&a.UserId==userId&&a.WorkDate==new DateOnly(2026,11,27),ct);
        if(day==null){day=new(){ShiftId=shift.ShiftId,UserId=userId,WorkDate=new(2026,11,27),CreatedAt=clock.Now,CreatedBy=userId};db.WorkdayAssignments.Add(day);}
        day.Status="WORKING";day.StartedAt=clock.Now.AddMinutes(-1);day.EndedAt=null;
        assignment.AssignedAt=clock.Now.AddYears(-1);
        var user=await db.Users.SingleAsync(u=>u.UserId==userId,ct);
        var session=new UserSession{UserId=userId,SecurityStamp=user.SecurityStamp,TokenHash=RandomNumberGenerator.GetBytes(32),CreatedAt=clock.Now,ExpiresAt=clock.Now.AddHours(12)};
        db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
        var actor=new Actor(new HttpContextAccessor{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier,userId.ToString()),new Claim("session_id",session.SessionId.ToString())],"sql-test"))}});
        var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var service=new WorkService(db,access,actor,atomic,audit,clock,notifications,documents,new RoomReadinessService(db,actor,clock),supplies,
            new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notifications));
        var guard=typeof(WorkService).GetMethod("RequireExistingCleaningExecution",BindingFlags.Instance|BindingFlags.NonPublic)!;
        await (Task)guard.Invoke(service,[work.WorkId,work.BranchId,ct])!;
        var history=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.Kind=="USE"&&l.WorkId!=work.WorkId).OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
        Assert.NotEmpty(history);
        var movements=await db.StockMovements.CountAsync(ct);
        var form=await supplies.RoomAssessment(work.WorkId,ct);
        Assert.False(form.Confirmed);
        Assert.True(form.ReadOnly);
        Assert.NotEmpty(form.Lines);
        var validate=typeof(StaffSupplyService).GetMethod("ConsumeForAcceptance",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var disposition=await (Task<string>)validate.Invoke(supplies,[work.WorkId,Guid.NewGuid(),ct])!;
        Assert.Equal("FULL_NORM_AT_ACCEPTANCE",disposition);
        var uses=await (from l in db.StaffSupplyLedgers join b in db.StaffSupplyBalances on l.BalanceId equals b.BalanceId
            where l.WorkId==work.WorkId&&l.Kind=="USE" select new{b.ItemId,l.Quantity}).ToArrayAsync(ct);
        foreach(var line in form.Lines)Assert.Equal(line.StandardQuantity,uses.Where(l=>l.ItemId==line.ItemId).Sum(l=>l.Quantity));
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        await (Task<string>)validate.Invoke(supplies,[work.WorkId,Guid.NewGuid(),ct])!;
        Assert.Equal(uses.Length,await db.StaffSupplyLedgers.CountAsync(l=>l.WorkId==work.WorkId&&l.Kind=="USE",ct));
        var afterHistory=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.Kind=="USE"&&l.WorkId!=work.WorkId).OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
        Assert.Equal(history.Select(l=>(l.LedgerId,l.Quantity,l.BalanceId,l.ReservationId)),afterHistory.Select(l=>(l.LedgerId,l.Quantity,l.BalanceId,l.ReservationId)));
        clock.Now=clock.Now.AddHours(9);
        var completion=typeof(StaffSupplyService).GetMethod("ValidateForCompletion",BindingFlags.Instance|BindingFlags.NonPublic)!;
        await (Task)completion.Invoke(supplies,[work.WorkId,Guid.NewGuid(),ct])!;
        Assert.Equal(uses.Length,await db.StaffSupplyLedgers.CountAsync(l=>l.WorkId==work.WorkId&&l.Kind=="USE",ct));
        await transaction.RollbackAsync(ct);
    }
}
