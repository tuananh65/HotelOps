using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Dev1PredicateSqlSuite
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 2 || !Regex.IsMatch(args[0], @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
            || File.Exists(args[1])) throw new ArgumentException("Guarded trial and new receipt required.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = timeout.Token;
        var connection = new SqlConnectionStringBuilder { DataSource = @"localhost\SQLEXPRESS", InitialCatalog = args[0],
            IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
        var fault = new QueryFault();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(connection.ConnectionString).AddInterceptors(fault).Options);
        await db.Database.OpenConnectionAsync(ct);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != args[0]
                || reader.GetString(1) != @"DESKTOP-JI8U50T\SQLEXPRESS" || reader.IsDBNull(2) || reader.GetString(2) != "Task17")
                throw new InvalidOperationException("Trial guard failed.");
        }
        var checks = new List<string>();
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            checks.Add(label); Console.WriteLine("PASS: " + label);
        }
        var method = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Users.Dev1DeactivationPredicates", true)!
            .GetMethod("HasKnownExecutingCleaningAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var now = DateTime.UtcNow;
        Task<bool> Read(long userId, CancellationToken token) => (Task<bool>)method.Invoke(null, [db, userId, now, token])!;
        var approvedType = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.Users.ApprovedDev1ResponsibilityQueries", true)!;
        var approvedInstance = Activator.CreateInstance(approvedType, db)!;
        var approvedRead = approvedType.GetMethod("ReadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task<(IReadOnlyList<string> Blockers, IReadOnlyList<string> Unresolved)> Review(long id)
        {
            var pending = (Task)approvedRead.Invoke(approvedInstance, [id, ct])!;
            await pending;
            var result = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
            return ((IReadOnlyList<string>)result.GetType().GetProperty("Blockers")!.GetValue(result)!,
                (IReadOnlyList<string>)result.GetType().GetProperty("Unresolved")!.GetValue(result)!);
        }
        var prefix = "T47_" + Guid.NewGuid().ToString("N")[..10];
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var branches = await db.Branches.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BranchId).Take(2).ToArrayAsync(ct);
            Check(branches.Length == 2, "two guarded canonical branches available");
            var user = new User { EmployeeCode = prefix, Username = prefix, Email = prefix + "@example.invalid",
                FullName = "Predicate trial", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", PrimaryBranchId = branches[0].BranchId, CreatedAt = now };
            var floor = new Floor { BranchId = branches[1].BranchId, FloorCode = prefix, FloorName = prefix, IsActive = true };
            var roomType = new RoomType { TypeCode = prefix, TypeName = prefix, IsActive = true };
            db.AddRange(user, floor, roomType); await db.SaveChangesAsync(ct);
            var room = new Room { BranchId = branches[1].BranchId, FloorId = floor.FloorId, RoomTypeId = roomType.RoomTypeId,
                RoomNumber = prefix, OperationalStatus = "CLEANING", IsActive = true };
            db.Add(room); await db.SaveChangesAsync(ct);
            var cleaningTypeId = await db.ReferenceValues.AsNoTracking().Where(x => x.GroupCode == "CLEANING_TYPE").Select(x => x.ReferenceId).FirstAsync(ct);
            var task = new CleaningTask { TaskNo = prefix, RoomId = room.RoomId, CleaningTypeId = cleaningTypeId,
                Status = "IN_PROGRESS", CreatedBy = user.UserId, CreatedAt = now };
            db.Add(task); await db.SaveChangesAsync(ct);
            var assignment = new CleaningAssignment { CleaningTaskId = task.CleaningTaskId, AssigneeUserId = user.UserId,
                AssignedBy = user.UserId, AssignedAt = now.AddHours(-1), AssignmentStatus = "ACTIVE" };
            db.Add(assignment); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            Check(await Read(user.UserId, ct), "SQL current assignee blocks across primary-branch boundary");
            Check(!await Read(long.MaxValue, ct), "SQL other user has no proven cleaning execution");
            foreach (var state in new[] { "COMPLETED", "REASSIGNED", "CANCELLED" })
            {
                await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignmentStatus, state).SetProperty(x => x.EndedAt, now), ct);
                Check(!await Read(user.UserId, ct), "SQL historical assignment excluded: " + state);
            }
            await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignmentStatus, "ACTIVE").SetProperty(x => x.EndedAt, (DateTime?)null)
                    .SetProperty(x => x.AssignedAt, now.AddMinutes(1)), ct);
            Check(!await Read(user.UserId, ct), "SQL future assignment outside proven subset");
            await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignedAt, now), ct);
            Check(await Read(user.UserId, ct), "SQL exact time boundary included");
            foreach (var state in new[] { "PENDING", "COMPLETED", "CANCELLED" })
            {
                await db.CleaningTasks.Where(x => x.CleaningTaskId == task.CleaningTaskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, state), ct);
                Check(!await Read(user.UserId, ct), "SQL parent outside proven subset: " + state);
            }
            await db.CleaningTasks.Where(x => x.CleaningTaskId == task.CleaningTaskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "IN_PROGRESS"), ct);
            await db.Rooms.Where(x => x.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
            Check(!await Read(user.UserId, ct), "SQL inactive room remains unresolved outside proven subset");
            Check(!db.ChangeTracker.Entries().Any(), "SQL helper keeps caller context untracked");
            Check((await Review(user.UserId)).Blockers.Contains("CLEANING_ASSIGNMENT"), "approved cleaning blocks even with inactive room across branch");
            await db.CleaningTasks.Where(x => x.CleaningTaskId == task.CleaningTaskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "PENDING"), ct);
            Check((await Review(user.UserId)).Blockers.Contains("CLEANING_ASSIGNMENT"), "approved cleaning PENDING parent blocks");
            await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, now), ct);
            Check((await Review(user.UserId)).Unresolved.Contains("CLEANING_DATA_INCONSISTENCY"), "ACTIVE with ended_at fails closed");
            await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, (DateTime?)null), ct);
            await db.CleaningTasks.Where(x => x.CleaningTaskId == task.CleaningTaskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "COMPLETED"), ct);
            Check((await Review(user.UserId)).Unresolved.Contains("CLEANING_TERMINAL_MAPPING"), "ambiguous SQL COMPLETED parent remains unresolved");
            await db.CleaningAssignments.Where(x => x.AssignmentId == assignment.AssignmentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignmentStatus, "COMPLETED").SetProperty(x => x.EndedAt, now), ct);
            Check(!(await Review(user.UserId)).Blockers.Contains("CLEANING_ASSIGNMENT"), "completed ended cleaning assignment is historical");
            var checklist = new InspectionChecklist { ChecklistCode = prefix, ChecklistName = prefix, IsActive = true, CreatedBy = user.UserId };
            db.Add(checklist); await db.SaveChangesAsync(ct);
            var version = new InspectionChecklistVersion { ChecklistId = checklist.ChecklistId, VersionNo = 1, Status = "DRAFT" };
            db.Add(version); await db.SaveChangesAsync(ct);
            var inspection = new RoomInspection { InspectionNo = prefix, RoomId = room.RoomId, CleaningTaskId = task.CleaningTaskId,
                VersionId = version.VersionId, InspectorUserId = user.UserId, Status = "PENDING", CreatedAt = now };
            db.Add(inspection); await db.SaveChangesAsync(ct);
            var rework = new CleaningRework { CleaningTaskId = task.CleaningTaskId, InspectionId = inspection.InspectionId,
                AssigneeUserId = user.UserId, Status = "PENDING", Reason = "Trial rework", RequestedAt = now };
            var defect = new InspectionDefect { InspectionId = inspection.InspectionId, AssignedTo = user.UserId, Status = "OPEN", Description = "Trial defect" };
            var department = new Department { BranchId = branches[1].BranchId, DepartmentCode = prefix, DepartmentName = prefix,
                ManagerUserId = user.UserId, IsActive = true };
            db.AddRange(rework, defect, department); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var review = await Review(user.UserId);
            Check(new[] { "CLEANING_REWORK", "INSPECTION_ASSIGNEE", "INSPECTION_DEFECT_ASSIGNEE", "DEPARTMENT_MANAGER" }.All(review.Blockers.Contains),
                "designated rework inspection defect and active manager block independently of completed parent");
            await db.RoomInspections.Where(x => x.InspectionId == inspection.InspectionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "IN_PROGRESS"), ct);
            await db.CleaningReworks.Where(x => x.ReworkId == rework.ReworkId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "IN_PROGRESS"), ct);
            await db.InspectionDefects.Where(x => x.DefectId == defect.DefectId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "IN_PROGRESS"), ct);
            review = await Review(user.UserId);
            Check(new[] { "CLEANING_REWORK", "INSPECTION_ASSIGNEE", "INSPECTION_DEFECT_ASSIGNEE" }.All(review.Blockers.Contains), "all designated IN_PROGRESS responsibilities block");
            await db.CleaningReworks.Where(x => x.ReworkId == rework.ReworkId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAt, now), ct);
            await db.InspectionDefects.Where(x => x.DefectId == defect.DefectId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ResolvedAt, now), ct);
            review = await Review(user.UserId);
            Check(review.Unresolved.Contains("REWORK_DATA_INCONSISTENCY") && review.Unresolved.Contains("DEFECT_DATA_INCONSISTENCY"), "open responsibilities with completion markers remain unresolved");
            await db.CleaningReworks.Where(x => x.ReworkId == rework.ReworkId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "COMPLETED"), ct);
            await db.InspectionDefects.Where(x => x.DefectId == defect.DefectId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "RESOLVED"), ct);
            await db.RoomInspections.Where(x => x.InspectionId == inspection.InspectionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "COMPLETED").SetProperty(x => x.Result, "PASS"), ct);
            await db.Departments.Where(x => x.DepartmentId == department.DepartmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
            review = await Review(user.UserId);
            Check(review.Blockers.Count == 0, "completed designated history and inactive department do not block");
            Check(review.Unresolved.Contains("WORKDAY_CONTRACT") && review.Unresolved.Contains("INTERNAL_REQUEST_CONTRACT")
                && review.Unresolved.Contains("SHIFT_CONTRACT"), "empty approved blockers cannot clear unresolved modules");
            await db.InspectionDefects.Where(x => x.DefectId == defect.DefectId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "OPEN")
                .SetProperty(x => x.ResolvedAt, (DateTime?)null).SetProperty(x => x.AssignedTo, (long?)null), ct);
            Check(!(await Review(user.UserId)).Blockers.Contains("INSPECTION_DEFECT_ASSIGNEE"), "historical inspector is not owner of unassigned open defect");
            Check((await Review(long.MaxValue)).Blockers.Count == 0, "unrelated user does not inherit FK responsibilities");
            var cancelled = false;
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                try { await Read(user.UserId, cancel.Token); }
                catch (TargetInvocationException e) when (e.InnerException is OperationCanceledException) { cancelled = true; }
                catch (OperationCanceledException) { cancelled = true; }
            }
            Check(cancelled, "cancelled query cannot produce clear result");
            // Deliberate fault in the production helper's SELECT after caller fixture writes.
            fault.Enabled = true;
            var failed = false;
            try { await Read(user.UserId, ct); }
            catch (InvalidOperationException e) when (e.Message == QueryFault.Message) { failed = true; }
            finally { fault.Enabled = false; }
            Check(failed, "query failure propagates instead of returning no responsibility");
            fault.Enabled = true;
            failed = false;
            try { await Review(user.UserId); }
            catch (InvalidOperationException e) when (e.Message == QueryFault.Message) { failed = true; }
            finally { fault.Enabled = false; }
            Check(failed, "approved query failure propagates instead of clearing unresolved state");
            Check(db.Database.CurrentTransaction == transaction, "helper preserves caller transaction ownership");
            await transaction.RollbackAsync(ct);
        }
        Check(!await db.Users.AnyAsync(x => x.Username == prefix, ct) && !await db.CleaningTasks.AnyAsync(x => x.TaskNo == prefix, ct)
            && !await db.Rooms.AnyAsync(x => x.RoomNumber == prefix, ct), "caller rollback removes fixture writes after query failure");
        await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { checks = checks.Count, cases = checks,
            database = args[0], scope = "Task47 approved DEV1 predicates and known cleaning subset", racesCertified = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine($"PASS: {checks.Count} SQL predicate assertions; assignment/deactivation races NOT RUN.");
    }

    private sealed class QueryFault : DbCommandInterceptor
    {
        internal const string Message = "Task47 injected predicate query failure";
        internal bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("cleaning_assignments", StringComparison.Ordinal)) throw new InvalidOperationException(Message);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
