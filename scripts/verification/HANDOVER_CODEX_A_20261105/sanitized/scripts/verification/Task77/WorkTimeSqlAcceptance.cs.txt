using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

internal sealed class TimeTrial(string database)
{
    public string Root { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
    public string ConnectionString { get; } = new SqlConnectionStringBuilder { DataSource = @"REDACTED_LOCAL_SQL_HOST\SQLEXPRESS",
        InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10,
        ApplicationName = "HotelOps Task77 A1 owned trial" }.ConnectionString;
    public List<string> Cases { get; } = [];
    public Dictionary<string, User> Users { get; } = [];
    public Dictionary<string, CleaningTask> Tasks { get; } = [];
    public Dictionary<string, long> Assignments { get; } = [];
    public long A { get; set; }
    public long B { get; set; }
    public long ReworkId { get; set; }
    public long OperationalId { get; set; }
    public string Password { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1-";
    public HotelOpsDbContext Open(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(ConnectionString);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    public void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + label);
        Cases.Add(label); Console.WriteLine("PASS: " + label);
    }
    public async Task Reject(Func<Task> action, string code, string label)
    {
        try { await action(); }
        catch (WorkTimeContractException e) when (e.Code == code) { Check(true, label); return; }
        throw new InvalidOperationException("FAIL: " + label + " expected " + code);
    }
    public async Task<string> Fingerprint(HotelOpsDbContext db, string[] tables, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var output = new StringBuilder();
        foreach (var table in tables)
        {
            if (!Regex.IsMatch(table, "^[a-z_]+$")) throw new InvalidOperationException("Invalid fixture table");
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) output.Append(reader.GetString(0));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(output.ToString())));
    }
}

internal static class WorkTimeSqlAcceptance
{
    public static async Task RunAsync(string database, string receipt)
    {
        if (!Regex.IsMatch(database, @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$") || File.Exists(receipt))
            throw new ArgumentException("Unused guarded trial and NEW receipt required");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        var ct = deadline.Token;
        var t = new TimeTrial(database);
        await using var db = t.Open();
        await db.Database.OpenConnectionAsync(ct);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')";
            await using var r = await command.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct) || r.GetString(0) != database || r.GetString(1) != @"REDACTED_LOCAL_SQL_HOST\SQLEXPRESS"
                || r.IsDBNull(2) || r.GetString(2) != "Task17") throw new InvalidOperationException("Trial identity rejected");
        }
        if (await db.Users.AnyAsync(ct)) throw new InvalidOperationException("Unused trial required; no reset/reuse");
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(t.Root,
            "scripts/verification/UserProfileUpdate/rowversion-forward.sql"), ct), ct);
        // Seed old-model users/assignments/cleaning/attendance BEFORE A1, to exercise an
        // upgrade with real legacy rows rather than only an empty fresh reference seed.
        await Fixtures(t, db, ct);
        await using (var legacy=t.Open())
        await using (var transaction=await legacy.Database.BeginTransactionAsync(ct))
        {
            await t.Reject(()=>new SqlWorkTimeTransactionParticipant(legacy).LockUsersAsync([t.Users["own"].UserId],ct),
                "TIME_SOURCE_UNRESOLVED","Legacy database without time schema fails closed with typed unavailable contract error");
            await transaction.RollbackAsync(ct);
        }
        var oldTables = await db.Database.SqlQuery<string>($"SELECT name AS [Value] FROM sys.tables WHERE is_ms_shipped=0").ToArrayAsync(ct);
        var before = await t.Fingerprint(db, oldTables.Order().ToArray(), ct);
        var forward = await File.ReadAllTextAsync(Path.Combine(t.Root, "scripts/verification/Task77/proposal/01_WorkTime.forward.sql"), ct);
        var rollback = await File.ReadAllTextAsync(Path.Combine(t.Root, "scripts/verification/Task77/proposal/02_WorkTime.rollback.sql"), ct);
        try { await db.Database.ExecuteSqlRawAsync(forward, ct); throw new InvalidOperationException("Approval guard did not reject"); }
        catch (SqlException e) when (e.Number == 51770) { t.Check(true, "DDL rejects missing approval session flag"); }
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'HotelOps.Task77Approved',@value=1", ct);
        await db.Database.ExecuteSqlRawAsync(forward, ct);
        t.Check(before == await t.Fingerprint(db, oldTables.Order().ToArray(), ct), "Fresh additive DDL preserves all legacy table data");
        try { await db.Database.ExecuteSqlRawAsync(forward, ct); throw new InvalidOperationException("Replay was not rejected"); }
        catch (SqlException e) when (e.Number == 51773) { t.Check(true, "Migration replay safely rejects existing contract without overwriting"); }
        t.Check(await db.Workdays.CountAsync(ct) == 0 && await db.WorkIntervals.CountAsync(ct) == 0, "Replay leaves empty time state unchanged");
        await db.Database.ExecuteSqlRawAsync(rollback, ct);
        t.Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name IN ('workdays','work_intervals','time_correction_requests')").SingleAsync(ct) == 0,
            "Owned empty-schema rollback removes only three time tables");
        await db.Database.ExecuteSqlRawAsync(forward, ct);
        t.Check(before == await t.Fingerprint(db, oldTables.Order().ToArray(), ct), "Reapply after rollback recovers schema without legacy data loss");
        foreach (var type in new[] { typeof(Workday), typeof(WorkInterval), typeof(TimeCorrectionRequest) })
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var expected = entity.GetProperties().Select(p => p.GetColumnName(store)).Order().ToArray();
            var actual = await db.Database.SqlQuery<string>($"SELECT name AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID({"dbo." + table})").ToArrayAsync(ct);
            t.Check(expected.SequenceEqual(actual.Order()), "EF maps every actual column: " + table);
            t.Check(entity.FindProperty("RowVersion")!.IsConcurrencyToken, "EF rowversion concurrency token: " + table);
        }
        t.Check(db.Model.GetEntityTypes().Count() == oldTables.Length + 3, "Combined EF model matches legacy plus three A1 entities");
        t.Check(await db.ShiftAttendances.AnyAsync(ct) && !await db.Workdays.AnyAsync(ct),
            "Legacy ShiftAttendance survives migration and does not create or substitute a Working workday");
        var untouched = new[] { "roles", "permissions", "role_permissions", "shift_attendance", "shift_templates", "work_shifts", "shift_assignments" };
        var fixtureFingerprint = await t.Fingerprint(db, untouched, ct);
        var user = t.Users["own"].UserId;
        var branch = t.A;
        WorkTimeOperation Op(string name = "own") => new(t.Users[name].UserId, t.Users[name].UserId,
            name == "other" ? t.B : branch, "CLEANING", t.Tasks[name].CleaningTaskId);
        await t.Reject(() => new SqlWorkTimeTransactionParticipant(db).LockUsersAsync([user], ct), "TIME_TRANSACTION_REQUIRED", "Participant requires caller transaction");
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope);
            await t.Reject(() => p.StartScheduledWorkdayAsync(user, branch, t.Assignments["own"], ct), "TIME_LOCK_SET_REQUIRED", "Caller must acquire participant User locks");
            await transaction.RollbackAsync(ct);
        }
        await WorkdayRace(t, ct);
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([user], ct);
            await t.Reject(() => p.StartScheduledWorkdayAsync(user, branch, t.Assignments["own"], ct), "TIME_ALREADY_WORKING", "Repeated Start Workday conflicts");
            await t.Reject(() => p.OpenIntervalAsync(Op() with { BranchId = t.B }, ct), "TIME_SCOPE_DENIED", "Participant rejects foreign branch");
            await t.Reject(() => p.OpenIntervalAsync(Op() with { WorkItemId = long.MaxValue }, ct), "TIME_SCOPE_DENIED", "Missing source cannot acquire interval");
            await t.Reject(() => p.OpenIntervalAsync(Op() with { WorkItemKind = "MAINTENANCE_OCCURRENCE" }, ct), "TIME_SOURCE_UNRESOLVED", "Unapproved PM identity fails closed");
            await t.Reject(() => p.OpenIntervalAsync(Op() with { WorkItemId = t.Tasks["http"].CleaningTaskId }, ct), "TIME_SCOPE_DENIED", "Different current assignee cannot be used by a trusted caller either");
            await scope.CleaningTasks.Where(x => x.CleaningTaskId == t.Tasks["own"].CleaningTaskId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "COMPLETED"), ct);
            await t.Reject(() => p.OpenIntervalAsync(Op(), ct), "TIME_SCOPE_DENIED", "Invalid terminal source cannot open interval");
            await transaction.RollbackAsync(ct);
        }
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope); var fault = t.Users["fault"].UserId;
            await p.LockUsersAsync([fault], ct);
            await t.Reject(() => p.OpenIntervalAsync(new(fault,fault,t.A,"CLEANING",t.Tasks["fault"].CleaningTaskId), ct),
                "TIME_WORKDAY_REQUIRED", "Cannot open interval without Working workday");
            await transaction.RollbackAsync(ct);
        }
        await IntervalRace(t, ct);
        var interval = await db.WorkIntervals.AsNoTracking().SingleAsync(x => x.UserId == user && x.ClosedAt == null, ct);
        var day = await db.Workdays.AsNoTracking().SingleAsync(x => x.UserId == user && x.Status == "WORKING", ct);
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([user], ct);
            await t.Reject(() => p.EndWorkdayAsync(user, branch, day.WorkdayId, day.RowVersion, ct), "TIME_INTERVAL_ALREADY_OPEN", "End Workday refuses open interval");
            await t.Reject(() => p.EndWorkdayAsync(user, branch, day.WorkdayId, new byte[8], ct), "TIME_STALE", "Workday End rejects stale rowversion");
            await t.Reject(() => p.CloseIntervalAsync(Op(), interval.IntervalId, new byte[8], ct), "TIME_STALE", "Stale interval close rejected");
            await transaction.RollbackAsync(ct);
        }
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var other = t.Users["denied"].UserId;
            var p = new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([user, other], ct);
            await t.Reject(() => p.OpenIntervalAsync(Op() with { ActorUserId = other }, ct), "TIME_SCOPE_DENIED", "Another active user cannot open owner's interval");
            await transaction.RollbackAsync(ct);
        }
        var auditBefore = await db.AuditLogs.CountAsync(ct);
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([user], ct);
            var closed = await p.CloseIntervalAsync(Op(), interval.IntervalId, interval.RowVersion, ct);
            t.Check(closed.ClosedAtUtc >= closed.OpenedAtUtc && closed.OpenedAtUtc.Kind == DateTimeKind.Utc, "Close uses nonnegative authoritative UTC range");
            var ended = await p.EndWorkdayAsync(user, branch, day.WorkdayId, day.RowVersion, ct);
            t.Check(ended.Status == "ENDED" && ended.EndedAt >= ended.StartedAt, "Close then End share caller transaction");
            await transaction.RollbackAsync(ct);
        }
        t.Check(await db.WorkIntervals.AnyAsync(x => x.IntervalId == interval.IntervalId && x.ClosedAt == null, ct)
            && await db.Workdays.AnyAsync(x => x.WorkdayId == day.WorkdayId && x.Status == "WORKING", ct)
            && await db.AuditLogs.CountAsync(ct) == auditBefore, "Caller rollback restores interval, Workday and all audit rows atomically");
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([user], ct);
            await p.CloseIntervalAsync(Op(), interval.IntervalId, interval.RowVersion, ct);
            await p.EndWorkdayAsync(user, branch, day.WorkdayId, day.RowVersion, ct);
            await transaction.CommitAsync(ct);
            await t.Reject(() => p.LockUsersAsync([user], ct), "TIME_TRANSACTION_REQUIRED", "Participant lease cannot survive completed transaction");
        }
        t.Check(await db.Workdays.AnyAsync(x => x.WorkdayId == day.WorkdayId && x.Status == "ENDED", ct), "Workday End persists after caller commit");
        t.Check(await db.AuditLogs.CountAsync(ct) == auditBefore + 2, "Exactly one close and one End audit committed");
        await using (var scope = t.Open())
        await using (var transaction = await scope.Database.BeginTransactionAsync(ct))
        {
            var p = new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([user], ct);
            await t.Reject(() => p.EndWorkdayAsync(user, branch, day.WorkdayId, day.RowVersion, ct), "TIME_STATE_CONFLICT", "Ended Workday cannot End again");
            await transaction.RollbackAsync(ct);
        }
        await AuditRollback(t, ct);
        await CallerTransactionRollback(t, ct);
        await using (var scope=t.Open())
        await using (var transaction=await scope.Database.BeginTransactionAsync(ct))
        {
            var fault=t.Users["fault"].UserId;
            var p=new SqlWorkTimeTransactionParticipant(scope); await p.LockUsersAsync([fault],ct);
            await scope.Users.Where(x=>x.UserId==fault).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.AccountStatus,"INACTIVE"),ct);
            await t.Reject(()=>p.OpenIntervalAsync(new(fault,fault,t.A,"CLEANING",t.Tasks["fault"].CleaningTaskId),ct),
                "TIME_ACTOR_INVALID","Inactive account cannot acquire future time responsibility");
            await transaction.RollbackAsync(ct);
        }
        await WorkTimeHttpsAcceptance.RunAsync(t, ct);
        t.Check(fixtureFingerprint == await t.Fingerprint(db, untouched, ct), "Time actions leave Shift/Attendance and RBAC fixtures unchanged");
        try { await db.Database.ExecuteSqlRawAsync(rollback, ct); throw new InvalidOperationException("Nonempty rollback accepted"); }
        catch (SqlException e) when (e.Number == 51775) { t.Check(true, "Rollback refuses recorded time history"); }
        await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new { success = true, checks = t.Cases.Count, cases = t.Cases,
            database, actualSql = true, actualHttpsComponentHost = true, productionWorkdayEndpoints = "NOT_IMPLEMENTED_PERMISSION_BINDING_OUTSIDE_A1",
            pmOccurrenceAdapter = "BLOCKED_UNRESOLVED_IDENTITY", fullUcAcceptance = false }, new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine($"PASS: {t.Cases.Count} A1 SQL/HTTPS component checks; no full UC completion.");
    }

    private static async Task Fixtures(TimeTrial t, HotelOpsDbContext db, CancellationToken ct)
    {
        var branches = await db.Branches.ToArrayAsync(ct); t.A = branches.Single(x => x.BranchCode == "BR001").BranchId;
        t.B = branches.Single(x => x.BranchCode == "BR002").BranchId;
        var departments = await db.Departments.ToArrayAsync(ct);
        var role = await db.Roles.SingleAsync(x => x.RoleCode == "HK_STAFF", ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { IterationCount = PasswordCredentialService.BaselineIterationCount }));
        foreach (var name in new[] { "own", "other", "denied", "http", "fault" })
        {
            var branch = name == "other" ? t.B : t.A;
            var department = departments.Single(x => x.BranchId == branch && x.DepartmentCode == "HK").DepartmentId;
            var user = new User { Username = "wt." + name, EmployeeCode = "WT_" + name, FullName = "Time " + name,
                Email = "wt." + name + "@example.invalid", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE",
                PrimaryBranchId = branch, DepartmentId = department, CreatedAt = now };
            user.PasswordHash = hasher.HashPassword(user, t.Password); db.Add(user); await db.SaveChangesAsync(ct); t.Users[name] = user;
            db.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch, AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now.AddMinutes(-1) });
            if (name != "denied") db.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId, BranchId = branch, IsActive = true, AssignedAt = now.AddMinutes(-1) });
            var template = new ShiftTemplate { BranchId = branch, ShiftCode = "WT_" + name, ShiftName = "Time test", StartTime = new(0,0), EndTime = new(23,59), IsActive = true };
            db.Add(template); await db.SaveChangesAsync(ct);
            var shift = new WorkShift { BranchId = branch, DepartmentId = department, ShiftTemplateId = template.ShiftTemplateId,
                ShiftDate = date, ScheduledStartAt = now.AddHours(-1), ScheduledEndAt = now.AddHours(1), Status = "PLANNED", CreatedBy = user.UserId, CreatedAt = now, RequiredStaffCount = 1 };
            db.Add(shift); await db.SaveChangesAsync(ct);
            var assignment = new ShiftAssignment { ShiftId = shift.ShiftId, UserId = user.UserId, AssignmentStatus = "ASSIGNED", AssignedBy = user.UserId, AssignedAt = now.AddMinutes(-1) };
            db.Add(assignment); await db.SaveChangesAsync(ct); t.Assignments[name] = assignment.ShiftAssignmentId;
            if (name == "own")
            {
                db.Add(new ShiftAttendance { ShiftAssignmentId = assignment.ShiftAssignmentId, AttendanceStatus = "NOT_STARTED" });
                await db.SaveChangesAsync(ct);
            }
            var floor = new Floor { BranchId = branch, FloorCode = "WT_" + name, FloorName = name, IsActive = true };
            var type = new RoomType { TypeCode = "WT_" + name, TypeName = name, IsActive = true };
            db.AddRange(floor, type); await db.SaveChangesAsync(ct);
            var room = new Room { BranchId = branch, FloorId = floor.FloorId, RoomTypeId = type.RoomTypeId,
                RoomNumber = "WT_" + name, IsActive = true, OperationalStatus = "CLEANING", CreatedAt = now };
            db.Add(room); await db.SaveChangesAsync(ct);
            var cleaningType = await db.ReferenceValues.FirstAsync(x => x.GroupCode == "CLEANING_TYPE" && x.IsActive, ct);
            var task = new CleaningTask { RoomId = room.RoomId, TaskNo = "WT_" + name, CleaningTypeId = cleaningType.ReferenceId,
                Status = "IN_PROGRESS", CreatedBy = user.UserId, CreatedAt = now };
            db.Add(task); await db.SaveChangesAsync(ct); t.Tasks[name] = task;
            db.Add(new CleaningAssignment { CleaningTaskId = task.CleaningTaskId, AssigneeUserId = user.UserId, AssignedBy = user.UserId,
                AssignedAt = now.AddMinutes(-1), AssignmentStatus = "ACTIVE" }); await db.SaveChangesAsync(ct);
        }
        var owner=t.Users["own"].UserId; var cleaning=t.Tasks["own"];
        var departmentA=departments.Single(x=>x.BranchId==t.A&&x.DepartmentCode=="HK").DepartmentId;
        var operational=new OperationalTask {TaskCode="WT_OPERATIONAL",BranchId=t.A,DepartmentId=departmentA,Title="Time source fixture",
            Status="IN_PROGRESS",CreatedBy=owner,CreatedAt=now};
        db.Add(operational); await db.SaveChangesAsync(ct); t.OperationalId=operational.TaskId;
        db.Add(new OperationalTaskAssignment {TaskId=operational.TaskId,AssigneeId=owner,AssignedBy=owner,
            AssignmentStatus="ACTIVE",AssignedAt=now.AddMinutes(-1)});
        var checklist=new InspectionChecklist {ChecklistCode="WT_REWORK",ChecklistName="Time source",IsActive=true,CreatedBy=owner,CreatedAt=now};
        db.Add(checklist); await db.SaveChangesAsync(ct);
        var version=new InspectionChecklistVersion {ChecklistId=checklist.ChecklistId,VersionNo=1,Status="PUBLISHED",PublishedBy=owner,EffectiveAt=now};
        db.Add(version); await db.SaveChangesAsync(ct);
        var inspection=new RoomInspection {InspectionNo="WT_REWORK",RoomId=cleaning.RoomId,CleaningTaskId=cleaning.CleaningTaskId,
            VersionId=version.VersionId,Status="COMPLETED",Result="FAIL",InspectorUserId=owner,CreatedAt=now};
        db.Add(inspection); await db.SaveChangesAsync(ct);
        var rework=new CleaningRework {CleaningTaskId=cleaning.CleaningTaskId,InspectionId=inspection.InspectionId,AssigneeUserId=owner,
            Status="IN_PROGRESS",Reason="Legacy time source fixture",RequestedAt=now};
        db.Add(rework); await db.SaveChangesAsync(ct); t.ReworkId=rework.ReworkId;
        db.ChangeTracker.Clear();
    }

    private static async Task WaitForSqlBlock(TimeTrial t, int waiter, int blocker, CancellationToken ct)
    {
        await using var monitor = t.Open();
        var found = false;
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await monitor.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={blocker}").SingleAsync(ct) > 0)
            { found = true; break; }
            await Task.Delay(20, ct);
        }
        t.Check(found, "DMV proves actual competing SQL session blocked on retained User/time locks");
    }

    private static async Task WorkdayRace(TimeTrial t, CancellationToken ct)
    {
        await using var first = t.Open(); await using var second = t.Open();
        await first.Database.OpenConnectionAsync(ct); await second.Database.OpenConnectionAsync(ct);
        var id1 = await first.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        var id2 = await second.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        await using var tx1 = await first.Database.BeginTransactionAsync(ct); await using var tx2 = await second.Database.BeginTransactionAsync(ct);
        var user = t.Users["own"].UserId;
        var p1 = new SqlWorkTimeTransactionParticipant(first); var p2 = new SqlWorkTimeTransactionParticipant(second);
        await p1.LockUsersAsync([user], ct);
        var lower = await first.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        var day = await p1.StartScheduledWorkdayAsync(user, t.A, t.Assignments["own"], ct);
        var upper = await first.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        t.Check(day.StartedAt >= lower && day.StartedAt <= upper, "Workday timestamp lies within SQL server measurements");
        var competing = Task.Run(async () => { await p2.LockUsersAsync([user], ct); await t.Reject(() => p2.StartScheduledWorkdayAsync(user, t.A, t.Assignments["own"], ct), "TIME_ALREADY_WORKING", "Concurrent Start Workday loser gets state conflict"); }, ct);
        await WaitForSqlBlock(t, id2, id1, ct); await tx1.CommitAsync(ct); await competing; await tx2.RollbackAsync(ct);
        await using var check = t.Open();
        t.Check(await check.Workdays.CountAsync(x => x.UserId == user && x.Status == "WORKING", ct) == 1, "Concurrent Start produces exactly one Working day");
        try { await check.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.workdays(user_id,branch_id,department_id,work_date,shift_assignment_id,is_unscheduled,status,started_at) SELECT user_id,branch_id,department_id,work_date,shift_assignment_id,is_unscheduled,status,started_at FROM dbo.workdays WHERE workday_id={day.WorkdayId}", ct); throw new InvalidOperationException("Unique day index failed"); }
        catch (SqlException e) when (e.Number is 2601 or 2627) { t.Check(true, "SQL unique index independently rejects duplicate Working day without application checks"); }
    }

    private static async Task IntervalRace(TimeTrial t, CancellationToken ct)
    {
        await using var first = t.Open(); await using var second = t.Open();
        await first.Database.OpenConnectionAsync(ct); await second.Database.OpenConnectionAsync(ct);
        var id1 = await first.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        var id2 = await second.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        await using var tx1 = await first.Database.BeginTransactionAsync(ct); await using var tx2 = await second.Database.BeginTransactionAsync(ct);
        var user = t.Users["own"].UserId; var op = new WorkTimeOperation(user,user,t.A,"CLEANING",t.Tasks["own"].CleaningTaskId);
        var p1 = new SqlWorkTimeTransactionParticipant(first); var p2 = new SqlWorkTimeTransactionParticipant(second);
        await p1.LockUsersAsync([user], ct);
        var before = await first.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        var row = await p1.OpenIntervalAsync(op, ct);
        var after = await first.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        t.Check(row.OpenedAtUtc >= before && row.OpenedAtUtc <= after, "Interval uses measured SQL server time");
        var competing = Task.Run(async () => { await p2.LockUsersAsync([user], ct); await t.Reject(() => p2.OpenIntervalAsync(op, ct), "TIME_INTERVAL_ALREADY_OPEN", "Concurrent Open Interval loser rejects"); }, ct);
        await WaitForSqlBlock(t,id2,id1,ct); await tx1.CommitAsync(ct); await competing; await tx2.RollbackAsync(ct);
        await using var check = t.Open();
        t.Check(await check.WorkIntervals.CountAsync(x => x.UserId == user && x.ClosedAt == null, ct) == 1, "Concurrent Open creates exactly one global open interval");
        try { await check.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at) SELECT workday_id,user_id,branch_id,'TASK',work_item_id,opened_by,opened_at,effective_started_at FROM dbo.work_intervals WHERE interval_id={row.IntervalId}", ct); throw new InvalidOperationException("Unique interval index failed"); }
        catch (SqlException e) when (e.Number is 2601 or 2627) { t.Check(true, "SQL index rejects cross-module second open interval independently of participant"); }
    }

    private static async Task AuditRollback(TimeTrial t, CancellationToken ct)
    {
        var user=t.Users["fault"].UserId;
        await using var check=t.Open(); var count=await check.AuditLogs.CountAsync(ct);
        await using (var failing=t.Open(new InvalidAudit()))
        await using (var tx=await failing.Database.BeginTransactionAsync(ct))
        {
            var p=new SqlWorkTimeTransactionParticipant(failing); await p.LockUsersAsync([user],ct);
            try { await p.StartScheduledWorkdayAsync(user,t.A,t.Assignments["fault"],ct); throw new InvalidOperationException("Expected actual audit CHECK failure"); }
            catch(DbUpdateException e) when(e.InnerException is SqlException {Number:547}) { t.Check(true,"Existing SQL audit CHECK rejects injected invalid audit after actual Workday INSERT"); }
            await tx.RollbackAsync(ct);
        }
        t.Check(!await check.Workdays.AnyAsync(x=>x.UserId==user,ct) && await check.AuditLogs.CountAsync(ct)==count,"SQL audit failure plus caller rollback leaves no Workday or orphan audit");
        await using(var ready=t.Open())
        await using(var tx=await ready.Database.BeginTransactionAsync(ct))
        {
            var p=new SqlWorkTimeTransactionParticipant(ready); await p.LockUsersAsync([user],ct);
            await p.StartScheduledWorkdayAsync(user,t.A,t.Assignments["fault"],ct); await tx.CommitAsync(ct);
        }
        count=await check.AuditLogs.CountAsync(ct);
        await using(var failing=t.Open(new InvalidAudit()))
        await using(var tx=await failing.Database.BeginTransactionAsync(ct))
        {
            var p=new SqlWorkTimeTransactionParticipant(failing); await p.LockUsersAsync([user],ct);
            try { await p.OpenIntervalAsync(new(user,user,t.A,"CLEANING",t.Tasks["fault"].CleaningTaskId),ct); throw new InvalidOperationException("Audit fault expected"); }
            catch(DbUpdateException e) when(e.InnerException is SqlException {Number:547}) { t.Check(true,"SQL audit CHECK fails after actual interval INSERT"); }
            await tx.RollbackAsync(ct);
        }
        t.Check(!await check.WorkIntervals.AnyAsync(x=>x.UserId==user,ct)&&await check.AuditLogs.CountAsync(ct)==count,
            "Failed interval audit rolls back interval without losing previously committed Workday");
    }

    private static async Task CallerTransactionRollback(TimeTrial t,CancellationToken ct)
    {
        await using var check=t.Open(); var user=t.Users["own"].UserId; var task=t.Tasks["own"].CleaningTaskId;
        var before=await check.CleaningTasks.AsNoTracking().Where(x=>x.CleaningTaskId==task).Select(x=>x.Instruction).SingleAsync(ct);
        var days=await check.Workdays.CountAsync(ct); var intervals=await check.WorkIntervals.CountAsync(ct); var audits=await check.AuditLogs.CountAsync(ct);
        await using(var scope=t.Open())
        await using(var tx=await scope.Database.BeginTransactionAsync(ct))
        {
            IWorkTimeTransactionParticipant participant=new SqlWorkTimeTransactionParticipant(scope);
            await participant.LockUsersAsync([user],ct);
            await ((SqlWorkTimeTransactionParticipant)participant).StartScheduledWorkdayAsync(user,t.A,t.Assignments["own"],ct);
            await scope.CleaningTasks.Where(x=>x.CleaningTaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Instruction,"Caller transaction rollback evidence"),ct);
            var cleaning=await participant.OpenIntervalAsync(new(user,user,t.A,"CLEANING",task),ct);
            await t.Reject(()=>participant.OpenIntervalAsync(new(user,user,t.A,"TASK",t.OperationalId),ct),
                "TIME_INTERVAL_ALREADY_OPEN","Existing cleaning interval prevents valid Operational Task start across modules");
            await participant.CloseIntervalAsync(new(user,user,t.A,"CLEANING",task),cleaning.IntervalId,cleaning.RowVersion,ct);
            var rework=await participant.OpenIntervalAsync(new(user,user,t.A,"REWORK",t.ReworkId),ct);
            t.Check(rework.IntervalId>0,"Real SQL rework source/inspection lineage accepted for current owner");
            await participant.CloseIntervalAsync(new(user,user,t.A,"REWORK",t.ReworkId),rework.IntervalId,rework.RowVersion,ct);
            var operational=await participant.OpenIntervalAsync(new(user,user,t.A,"TASK",t.OperationalId),ct);
            t.Check(operational.IntervalId>0,"Real SQL Operational Task current assignment accepted");
            await tx.RollbackAsync(ct);
        }
        t.Check(await check.Workdays.CountAsync(ct)==days&&await check.WorkIntervals.CountAsync(ct)==intervals
            &&await check.AuditLogs.CountAsync(ct)==audits
            &&await check.CleaningTasks.Where(x=>x.CleaningTaskId==task).Select(x=>x.Instruction).SingleAsync(ct)==before,
            "Reusable interface joins caller source/Workday/interval/audit transaction and all roll back together; no inner commit");
    }

    private sealed class InvalidAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result,CancellationToken cancellationToken=default)
        {
            foreach(var entry in eventData.Context!.ChangeTracker.Entries<AuditLog>().Where(x=>x.State==EntityState.Added)) entry.Entity.Action="INVALID_TEST_ACTION";
            return base.SavingChangesAsync(eventData,result,cancellationToken);
        }
    }
}
