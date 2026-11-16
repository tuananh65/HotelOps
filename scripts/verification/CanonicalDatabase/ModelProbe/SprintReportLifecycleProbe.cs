using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

// Called only inside the exact-identity-guarded report probe's disposable transaction.
internal static class SprintReportLifecycleProbe
{
    internal static async Task RunAsync(HotelOpsDbContext db, ReportSnapshotQueries queries, ReportQuery query,
        InsightFilters filters, DateTimeOffset now, long actor, long branch, long department, long task,
        long request, Action<bool, string> check, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Guarded caller transaction required");
        await tx.CreateSavepointAsync("ReportLifecycle", ct);
        try
        {
            var at = now.UtcDateTime;
            var snapshot = now.AddHours(1);
            var code = "RPT" + Guid.NewGuid().ToString("N")[..8];
            var employee = new User { EmployeeCode = code, Username = code, Email = code + "@example.invalid", FullName = "Synthetic reassignment employee",
                AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = at, PrimaryBranchId = branch, DepartmentId = department };
            db.Users.Add(employee); await db.SaveChangesAsync(ct);
            var other = employee.UserId;
            var nextDepartment = new Department { BranchId = branch, DepartmentCode = "RPT" + Guid.NewGuid().ToString("N")[..8], DepartmentName = "Synthetic route", IsActive = true };
            db.Departments.Add(nextDepartment); await db.SaveChangesAsync(ct);
            await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReceivingDepartmentId, department).SetProperty(r => r.Status, "CLOSED")
                .SetProperty(r => r.CompletedAt, at.AddMinutes(25)).SetProperty(r => r.ClosedAt, at.AddMinutes(25)), ct);
            InternalRequestEvent Event(string type, int minute, string old, string next) => new() {
                InternalRequestId = request, ActorId = actor, EventType = type, OccurredAt = at.AddMinutes(minute), OldStatus = old, NewStatus = next };
            db.InternalRequestAssignments.AddRange(
                new() { InternalRequestId = request, AssigneeId = actor, AssignedBy = actor, AssignedAt = at.AddMinutes(2), EndedAt = at.AddMinutes(5), Status = "REASSIGNED" },
                new() { InternalRequestId = request, AssigneeId = other, AssignedBy = actor, AssignedAt = at.AddMinutes(5), EndedAt = at.AddMinutes(10), Status = "COMPLETED" });
            db.InternalRequestEvents.AddRange(Event("ASSIGN", 2, "ACCEPTED", "IN_PROGRESS"), Event("REASSIGN", 5, "IN_PROGRESS", "IN_PROGRESS"),
                Event("COMPLETE", 10, "IN_PROGRESS", "CLOSED"), Event("REOPEN", 20, "CLOSED", "IN_PROGRESS"), Event("COMPLETE", 25, "IN_PROGRESS", "CLOSED"));
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var complete = await queries.MaterializeAsync("requests", query, filters, snapshot, ct);
            var row = complete.Rows.Single(r => ((JsonElement)r["id"]!).GetInt64() == request);
            check(((JsonElement)row["completionSeconds"]!).GetString() == "780" && ((JsonElement)row["completedCycles"]!).GetInt32() == 2
                && ((JsonElement)row["reopenCount"]!).GetInt32() == 1, "RPT07 SQL processing cycles exclude closed gap and retain time before reassign");
            var summary = complete.Sections.Single(s => s.Key == "request_type_department_summary").Rows.Single();
            check(((JsonElement)summary["averageCompletionSeconds"]!).GetString() == "780"
                && ((JsonElement)summary["measuredClosedRequests"]!).GetInt32() == 1,
                "RPT07 completion average counts each closed request once, not each cycle");
            var departmentQuery = new ReportQuery { BranchId = branch, DepartmentId = department };
            var departmentFilters = filters with { DepartmentId = department };
            check((await queries.MaterializeAsync("requests", departmentQuery, departmentFilters, snapshot, ct)).Rows.Count == 1,
                "RPT07 approved receiving department filter admits current department");
            await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.ReceivingDepartmentId, nextDepartment.DepartmentId), ct);
            check((await queries.MaterializeAsync("requests", departmentQuery, departmentFilters, snapshot, ct)).Rows.Count == 0,
                "RPT07 route removes request from previous receiving department without inferring requester department");
            departmentQuery.DepartmentId = nextDepartment.DepartmentId;
            check((await queries.MaterializeAsync("requests", departmentQuery, filters with { DepartmentId = nextDepartment.DepartmentId }, snapshot, ct)).Rows.Count == 1,
                "RPT07 routed request appears only in current receiving department");
            check(((JsonElement)row["receivingDepartmentId"]!).GetInt64() == department,
                "RPT07 frozen snapshot retains prior department after route");

            db.OperationalTaskAssignments.Add(new() { TaskId = task, AssigneeId = actor, AssignedBy = actor, AssignedAt = at, AssignmentStatus = "ACTIVE" });
            var day = new Workday { UserId = actor, BranchId = branch, DepartmentId = department, WorkDate = DateOnly.FromDateTime(at),
                StartedAt = at.AddMinutes(5), EndedAt = at.AddMinutes(10), Status = "ENDED", IsUnscheduled = true, UnscheduledReason = "Synthetic report attribution" };
            db.Workdays.Add(day); await db.SaveChangesAsync(ct);
            db.WorkIntervals.Add(new() { WorkdayId = day.WorkdayId, UserId = actor, BranchId = branch, WorkItemKind = "TASK", WorkItemId = task,
                OpenedBy = actor, OpenedAt = day.StartedAt, EffectiveStartedAt = day.StartedAt, ClosedBy = actor, ClosedAt = day.EndedAt, EffectiveEndedAt = day.EndedAt });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            async Task<ReportResult> Employee(long employee) => await queries.MaterializeAsync("tasks", new ReportQuery { BranchId = branch, EmployeeId = employee }, filters, snapshot, ct);
            var before = await Employee(actor);
            check(before.Rows.Count == 1, "RPT06 employee task list uses current assignment");
            await db.OperationalTaskAssignments.Where(a => a.TaskId == task && a.AssignmentStatus == "ACTIVE")
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AssignmentStatus, "REASSIGNED").SetProperty(a => a.EndedAt, at.AddMinutes(15)), ct);
            db.OperationalTaskAssignments.Add(new() { TaskId = task, AssigneeId = other, AssignedBy = actor, AssignedAt = at.AddMinutes(15), AssignmentStatus = "ACTIVE" });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var former = await Employee(actor);
            var current = await Employee(other);
            check(former.Rows.Count == 0 && current.Rows.Count == 1, "RPT06 reassignment moves current task population to new assignee");
            var effort = former.Sections.Single(s => s.Key == "task_effort_by_employee").Rows.Single();
            var workday = former.Sections.Single(s => s.Key == "workday_time_by_employee").Rows.Single();
            check(((JsonElement)effort["closedEffortSeconds"]!).GetInt64() == 300 && ((JsonElement)workday["closedWorkdaySeconds"]!).GetInt64() == 300,
                "RPT06 historical effort and workday remain with actual employee after reassignment");
            check(current.Sections.Single(s => s.Key == "task_effort_by_employee").Rows.Count == 0
                && current.Sections.Single(s => s.Key == "workday_time_by_employee").Rows.Count == 0,
                "RPT06 new assignee does not inherit former employee time");
        }
        finally { await tx.RollbackToSavepointAsync("ReportLifecycle", CancellationToken.None); db.ChangeTracker.Clear(); }
    }
}
