using System.Linq.Expressions;
using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

internal static class Dev1PredicateSuite
{
    internal static async Task RunAsync()
    {
        var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Users.Dev1DeactivationPredicates", true)!;
        var factory = type.GetMethod("KnownExecutingCleaning", BindingFlags.Static | BindingFlags.NonPublic)!;
        var now = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);
        var expression = (Expression<Func<CleaningAssignment, bool>>)factory.Invoke(null, [7L, now])!;
        var predicate = expression.Compile();
        var checks = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            checks++; Console.WriteLine("PASS: " + label);
        }
        CleaningAssignment Fixture(string status = "ACTIVE", DateTime? ended = null, string parent = "IN_PROGRESS",
            long branch = 2, bool roomActive = true, long assignee = 7, DateTime? assigned = null)
        {
            var task = new CleaningTask { Status = parent, Room = new Room { BranchId = branch, IsActive = roomActive } };
            var assignment = new CleaningAssignment { AssigneeUserId = assignee, AssignmentStatus = status,
                AssignedAt = assigned ?? now.AddHours(-1), EndedAt = ended, CleaningTask = task };
            task.CleaningAssignments.Add(assignment);
            return assignment;
        }
        var cases = new (string Label, CleaningAssignment Assignment, bool Expected)[]
        {
            ("current execution", Fixture(), true),
            ("another branch remains visible", Fixture(branch: 999), true),
            ("another assignee", Fixture(assignee: 8), false),
            ("former reassigned assignee", Fixture("REASSIGNED", now.AddMinutes(-1)), false),
            ("completed historical assignment", Fixture("COMPLETED", now.AddMinutes(-1)), false),
            ("cancelled historical assignment", Fixture("CANCELLED", now.AddMinutes(-1)), false),
            ("ended ACTIVE is unresolved, not proven execution", Fixture(ended: now), false),
            ("future ACTIVE is unresolved, not proven execution", Fixture(assigned: now.AddTicks(1)), false),
            ("exact assignment time included", Fixture(assigned: now), true),
            ("pending parent remains outside proven subset", Fixture(parent: "PENDING"), false),
            ("completed parent remains outside proven subset", Fixture(parent: "COMPLETED"), false),
            ("cancelled parent remains outside proven subset", Fixture(parent: "CANCELLED"), false),
            ("inactive room remains outside proven subset", Fixture(roomActive: false), false),
            ("unknown branch remains outside proven subset", Fixture(branch: 0), false)
        };
        foreach (var item in cases)
        {
            Check(predicate(item.Assignment) == item.Expected, item.Label);
            var task = item.Assignment.CleaningTask;
            var access = new CleaningTaskAccess(task.Room.BranchId, task.Room.IsActive, task.Status,
                task.CleaningAssignments.Select(a => new CleaningTaskAssignee(a.AssigneeUserId, a.AssignmentStatus, a.AssignedAt, a.EndedAt)).ToArray());
            var resolver = new CleaningTaskResourceResolver(new Reader(access), new Clock(now));
            Check((await resolver.ResolveAsync(CleaningTaskResourceResolver.PermissionCode,
                new(CleaningTaskResourceResolver.ResourceType, 1), 7, CancellationToken.None) is not null) == item.Expected,
                "existing ownership contract agrees: " + item.Label);
        }
        var current = Fixture();
        current.CleaningTask.CleaningAssignments.Add(new CleaningAssignment { AssigneeUserId = 8,
            AssignmentStatus = "REASSIGNED", AssignedAt = now.AddHours(-2), EndedAt = now.AddHours(-1) });
        Check(predicate(current), "closed history does not displace current assignment");
        current.CleaningTask.CleaningAssignments.Add(new CleaningAssignment { AssigneeUserId = 8,
            AssignmentStatus = "ACTIVE", AssignedAt = now.AddHours(-1) });
        Check(!predicate(current), "ambiguous legacy ownership remains unresolved");
        foreach (var arguments in new object[][] { [0L, now], [7L, DateTime.SpecifyKind(now, DateTimeKind.Unspecified)] })
        {
            var rejected = false;
            try { factory.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { rejected = true; }
            Check(rejected, "invalid predicate input rejected");
        }
        // Translation only: no SQL connection opened, no credentials or local server required.
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=translation.invalid;Database=PredicateTranslation;Integrated Security=True;TrustServerCertificate=True").Options);
        var sql = db.CleaningAssignments.AsNoTracking().Where(expression).ToQueryString();
        Check(sql.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase) && sql.Contains("cleaning_assignments")
            && sql.Contains("rooms") && sql.Contains("IN_PROGRESS"), "SQL Server provider translates correlated ownership predicate");
        Check(!db.ChangeTracker.Entries().Any(), "query generation tracks no entities");
        Console.WriteLine($"PASS: {checks} DEV1 predicate assertions; SQL runtime/races NOT RUN.");
    }

    private sealed class Reader(CleaningTaskAccess access) : ICleaningTaskAccessReader
    {
        public Task<CleaningTaskAccess?> ReadAsync(long id, CancellationToken ct) => Task.FromResult<CleaningTaskAccess?>(access);
    }
    private sealed class Clock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
