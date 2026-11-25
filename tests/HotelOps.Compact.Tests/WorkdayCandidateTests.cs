using System.Security.Claims;
using System.Text.Json;
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

public sealed class WorkdayCandidatesDatabaseFactAttribute : FactAttribute
{
    public WorkdayCandidatesDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_WORKDAY_CANDIDATE_TEST")))
            Skip = "Requires an isolated HotelOpsCompactWorkdayTestCandidates clone of the recovered roster.";
    }
}

public sealed class WorkdayCandidateTests
{
    private sealed class CandidateClock(DateTime instant) : TimeProvider
    {
        public DateTime Instant { get; set; } = instant;
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Instant, DateTimeKind.Utc));
    }

    [WorkdayCandidatesDatabaseFact]
    public async Task RecoveredMaintenanceStaffCanJoinRemainingShiftWithoutBackdatingTheirRoles()
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_WORKDAY_CANDIDATE_TEST")!;
        Assert.Matches("^HotelOpsCompactWorkdayTestCandidates[0-9]+$", new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        await using var db = new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>()
            .UseSqlServer(connection).Options);

        // The clone contains the exact incident: the restored technical roster and
        // the 16:00–24:00 shift which was still running when recovery took place.
        var shift = await (from s in db.WorkdayShifts.AsNoTracking()
                           join r in db.Roles on s.RoleId equals r.RoleId
                           where s.IsActive && r.Code == "MAINTENANCE_STAFF" && s.StartMinute == 960 && s.EndMinute == 1440
                           orderby s.ShiftId
                           select s).FirstAsync(ct);
        var grants = await db.UserRoles.AsNoTracking()
            .Where(r => r.RoleId == shift.RoleId && r.BranchId == shift.BranchId && r.RevokedAt == null)
            .Select(r => new { r.UserId, r.AssignedAt }).ToArrayAsync(ct);
        Assert.Equal(5, grants.Length);
        var recoveredAt = grants.Max(r => r.AssignedAt);
        var clock = new CandidateClock(recoveredAt.AddMinutes(5));
        var timezone = await db.Branches.Where(b => b.BranchId == shift.BranchId).Select(b => b.Timezone).SingleAsync(ct);
        var today = WorkdayService.LocalDate(clock.GetUtcNow(), timezone);
        var start = ShiftExpiryService.Boundary(today, shift.StartMinute, timezone);
        var end = ShiftExpiryService.Boundary(today, shift.EndMinute, timezone);
        Assert.InRange(clock.Instant, start, end.AddTicks(-1));
        Assert.All(grants, r => Assert.True(r.AssignedAt > start, "Fixture must reproduce role recovery after shift start."));

        var session = await (from ur in db.UserRoles.AsNoTracking()
                             join r in db.Roles on ur.RoleId equals r.RoleId
                             join s in db.UserSessions on ur.UserId equals s.UserId
                             where ur.BranchId == shift.BranchId && r.Code == "OPERATION_MANAGER"
                                 && ur.RevokedAt == null && s.RevokedAt == null && s.ExpiresAt > end
                             orderby s.SessionId descending
                             select new { s.UserId, s.SessionId }).FirstAsync(ct);
        var actor = new Actor(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                    new Claim("session_id", session.SessionId.ToString())
                ], "isolated-read-only-test"))
            }
        });
        var access = new Access(db, actor, clock);
        var atomic = new Atomic(db);
        var service = new WorkdayService(db, access, actor, atomic, new Audit(db, actor, clock), clock,
            new ShiftExpiryService(db, atomic, clock), null!); // Candidate reads never use supply mutations.
        var assignmentsBefore = await db.WorkdayAssignments.CountAsync(ct);
        var eventsBefore = await db.WorkdayEvents.CountAsync(ct);
        var auditsBefore = await db.AuditLogs.CountAsync(ct);

        async Task<long[]> ExpectedFree(DateOnly date)
        {
            var busy = await (from a in db.WorkdayAssignments.AsNoTracking()
                              join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                              where a.WorkDate == date && a.Status != "CANCELLED"
                                  && s.StartMinute < shift.EndMinute && s.EndMinute > shift.StartMinute
                              select a.UserId).ToArrayAsync(ct);
            return grants.Select(r => r.UserId).Except(busy).Order().ToArray();
        }

        var expectedToday = await ExpectedFree(today);
        Assert.NotEmpty(expectedToday);
        var batch = await service.BatchCandidates(shift.ShiftId, [today], ct);
        Assert.Equal(expectedToday, batch.Users.Select(u => u.UserId).Order().ToArray());
        var single = JsonSerializer.SerializeToElement(await service.Candidates(shift.ShiftId, today, ct));
        Assert.Equal(expectedToday, single.EnumerateArray().Select(u => u.GetProperty("UserId").GetInt64()).Order().ToArray());

        // Tomorrow still uses the complete scheduled interval. The current-time fix
        // must not exclude the newly restored roster from future scheduling.
        var tomorrow = today.AddDays(1);
        var future = await service.BatchCandidates(shift.ShiftId, [tomorrow], ct);
        Assert.Equal(await ExpectedFree(tomorrow), future.Users.Select(u => u.UserId).Order().ToArray());
        clock.Instant = end;
        var expired = await Assert.ThrowsAsync<ApiException>(() => service.Candidates(shift.ShiftId, today, ct));
        Assert.Equal("SHIFT_ALREADY_ENDED", expired.Code);

        Assert.Equal(assignmentsBefore, await db.WorkdayAssignments.CountAsync(ct));
        Assert.Equal(eventsBefore, await db.WorkdayEvents.CountAsync(ct));
        Assert.Equal(auditsBefore, await db.AuditLogs.CountAsync(ct));
        Assert.All(db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }
}
