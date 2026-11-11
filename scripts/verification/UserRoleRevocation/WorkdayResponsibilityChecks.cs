using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

internal static class WorkdayResponsibilityChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var start = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        Workday Day(string status = "WORKING", DateTime? end = null, long user = 8, long branch = 2) => new()
        { WorkdayId = 1, UserId = user, BranchId = branch, DepartmentId = 3, Status = status, StartedAt = start, EndedAt = end };
        var working = WorkdayResponsibilityQueries.Assess(8, [Day()]);
        check(working.Blockers.SequenceEqual(["WORKING_WORKDAY_END_REQUIRED"]) && working.Unresolved.Count == 0,
            "D05 Working without intervals requires employee End");
        var unscheduled = Day(); unscheduled.IsUnscheduled = true;
        check(WorkdayResponsibilityQueries.Assess(8, [unscheduled]).Blockers.Count == 1, "D05 applies to unscheduled Working too");
        check(WorkdayResponsibilityQueries.Assess(8, [Day("ENDED", start.AddHours(8))]).Blockers.Count == 0, "Ended is not Working, but never clears assignments");
        foreach (var row in new[] { Day("working"), Day("CANCELLED"), Day("UNKNOWN"), Day("ENDED"), Day("WORKING", start.AddHours(1)), Day("ENDED", start.AddMinutes(-1)), Day(user: 9), Day(branch: 0) })
            check(WorkdayResponsibilityQueries.Assess(8, [row]).Unresolved.Count != 0, "unknown/inconsistent Workday facts fail closed");
        check(WorkdayResponsibilityQueries.Assess(8, [Day(), Day(branch: 4)]).Unresolved.Count != 0, "duplicate/multiple Working records fail closed");
        check(WorkdayResponsibilityQueries.Assess(8, []).Blockers.Count == 0 && UserRoleRevocationService.PendingReleaseGates.Count != 0,
            "empty Workday facts cannot clear immutable release gates");
        var rows = new[] { Day(), Day(user: 9) }.AsQueryable();
        check(WorkdayResponsibilityQueries.ForTarget(rows, 8).All(w => w.UserId == 8), "query binds exact target without primary-branch filter");
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=invalid;Database=unopened;Integrated Security=true").Options);
        var sql = WorkdayResponsibilityQueries.ForTarget(db.Workdays.AsNoTracking(), 8).ToQueryString();
        check(sql.Contains("[workdays]") && sql.Contains("[user_id]"), "actual mapped Workday query translates offline (not SQL execution)");
        check(db.Database.GetDbConnection().State == System.Data.ConnectionState.Closed, "translation opens no SQL connection");
        check(UserRoleRevocationService.ValidateResponsibilities(working.Blockers, [], [], []) == HotelOps.Api.DTOs.Users.RevokeRoleStatus.ResponsibilitiesBlocked,
            "Working fact feeds existing revoke business denial");
        check(UserRoleRevocationService.ValidateResponsibilities(working.Blockers, ["WORKDAY_COVERAGE_PENDING"], [], []) == HotelOps.Api.DTOs.Users.RevokeRoleStatus.ResponsibilitiesUnresolved,
            "positive D05 blocker never masks incomplete coverage");
    }
}
