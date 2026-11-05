using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task73PlanningCases
{
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open, HttpClient http,
        Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> users, long branch, long foreign, Action<bool,string> check, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var hk = await db.Departments.Where(d => d.BranchId == branch && d.DepartmentCode == "HK").Select(d => d.DepartmentId).SingleAsync(ct);
        var mt = await db.Departments.Where(d => d.BranchId == branch && d.DepartmentCode == "MT").Select(d => d.DepartmentId).SingleAsync(ct);
        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleCode, r => r.RoleId, ct);
        async Task<User> Staff(string code, string role = "HK_STAFF", long? department = null, bool member = true, bool expired = false, string status = "ACTIVE", long? roleBranch = null)
        {
            var user = new User { Username = "t73." + code, EmployeeCode = "T73_" + code, Email = "t73." + code + "@example.invalid", FullName = "Task73 " + code,
                DepartmentId = department ?? hk, PrimaryBranchId = branch, CreatedAt = now, AccountStatus = status, EmploymentStatus = "ACTIVE", PasswordHash = users["admin"].PasswordHash };
            db.Add(user); await db.SaveChangesAsync(ct);
            db.Add(new UserRole { UserId = user.UserId, RoleId = roles[role], BranchId = roleBranch ?? branch, IsActive = true, AssignedAt = now.AddHours(-1), ExpiresAt = expired ? now.AddMinutes(-1) : null });
            if (member) db.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = now.AddHours(-1) });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); return user;
        }
        var worker = await Staff("A_worker"); var free = await Staff("B_free");
        var wrongDepartment = await Staff("WrongDepartment", department: mt);
        var noMembership = await Staff("NoMembership", member: false);
        var expired = await Staff("Expired", expired: true);
        var inactive = await Staff("Inactive", status: "INACTIVE");
        var otherBranch = await Staff("ForeignRole", roleBranch: foreign);
        var inspector = await Staff("Inspector", "HK_SUPERVISOR");
        var cleanerInspector = await Staff("CleanerInspector", "HK_SUPERVISOR");
        var room = await db.Rooms.AsNoTracking().SingleAsync(r => r.RoomNumber == "T72_INSPECT", ct);
        var type = await db.ReferenceValues.Where(r => r.GroupCode == "CLEANING_TYPE").Select(r => r.ReferenceId).FirstAsync(ct);
        CleaningTask TaskRow(string number, string status, DateTime at) => new() { TaskNo = number, RoomId = room.RoomId, CleaningTypeId = type, Status = status, CreatedBy = users["viewer"].UserId, CreatedAt = at };
        var target = TaskRow("T73_TARGET", "PENDING", now);
        var progress = TaskRow("T73_PROGRESS", "IN_PROGRESS", now);
        var old = TaskRow("T73_OLD", "IN_PROGRESS", now.AddDays(-1));
        var done = TaskRow("T73_DONE", "COMPLETED", now);
        db.AddRange(target, progress, old, done); await db.SaveChangesAsync(ct);
        foreach (var task in new[] { target, progress, old, done }) db.Add(new CleaningAssignment { CleaningTaskId = task.CleaningTaskId, AssigneeUserId = worker.UserId,
            AssignedBy = users["viewer"].UserId, AssignedAt = now, AssignmentStatus = "ACTIVE" });
        db.Add(new CleaningAssignment { CleaningTaskId = target.CleaningTaskId, AssigneeUserId = cleanerInspector.UserId, AssignedBy = users["viewer"].UserId, AssignedAt = now.AddHours(-1), AssignmentStatus = "COMPLETED", EndedAt = now });
        var template = new ShiftTemplate { BranchId = branch, ShiftCode = "T73_RAW", ShiftName = "Recorded window", StartTime = new TimeOnly(0,0), EndTime = new TimeOnly(23,59), IsActive = true };
        var shift = new WorkShift { BranchId = branch, DepartmentId = hk, ShiftTemplate = template, ShiftDate = DateOnly.FromDateTime(now), ScheduledStartAt = now.Date,
            ScheduledEndAt = now.Date.AddDays(1), Status = "PLANNED", CreatedBy = users["viewer"].UserId, CreatedAt = now, RequiredStaffCount = 1 };
        db.Add(new ShiftAssignment { Shift = shift, UserId = worker.UserId, AssignmentStatus = "ASSIGNED", AssignedBy = users["viewer"].UserId, AssignedAt = now });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var jar = await login("viewer"); var denied = await login("denied"); var admin = await login("admin");
        string Route(bool inspection, long? b = null, long? id = null) => $"/api/branches/{b ?? branch}/cleaning-tasks/{id ?? target.CleaningTaskId}/{(inspection ? "inspection-options" : "assignment-options")}";
        async Task<JsonElement> Get(string route, CookieJar? who, int expected)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            if (who != null) request.Headers.Add("Cookie", string.Join("; ", who.Cookies.Select(x => x.Key + "=" + x.Value)));
            using var response = await http.SendAsync(request, ct); var text = await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode == expected, "Task73 HTTPS " + route + " expected=" + expected + " actual=" + (int)response.StatusCode + (expected != (int)response.StatusCode ? " " + text : ""));
            check(!new[] { "passwordHash", "email", "phone", "SqlException", "stackTrace", "storageKey" }.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)), "Task73 planning returns only scoped public DTO fields");
            if (expected == 200) check(response.Headers.CacheControl?.NoStore == true, "Task73 planning no-store");
            using var body = JsonDocument.Parse(text); return body.RootElement.Clone();
        }
        var audits = await db.AuditLogs.CountAsync(ct); var assignments = await db.CleaningAssignments.CountAsync(ct);
        foreach (var inspectionMode in new[] {false,true})
        {
            await Get(Route(inspectionMode), null, 401); await Get(Route(inspectionMode), denied, 403); await Get(Route(inspectionMode), admin, 403);
            await Get(Route(inspectionMode, foreign), jar, 403); await Get(Route(inspectionMode, id: long.MaxValue), jar, 404);
            foreach (var filter in new[] { "page=0", "pageSize=101", "from=invalid", "from=2026-10-01T00:00:00", "from=2026-10-01T00:00:00Z&to=2026-10-10T00:00:00Z" }) await Get(Route(inspectionMode) + "?" + filter, jar, 400);
            var missing = await Get(Route(inspectionMode) + "?search=NO_MATCH_T73", jar, 200);
            check(missing.GetProperty("total").GetInt32() == 0 && missing.GetProperty("filters").GetProperty("search").GetString() == "NO_MATCH_T73", "empty planning list echoes applied filters");
        }
        var staff = await Get(Route(false), jar, 200);
        var ids = staff.GetProperty("candidates").EnumerateArray().Select(c => c.GetProperty("id").GetInt64()).ToArray();
        check(ids.Order().SequenceEqual(new[] {worker.UserId,free.UserId}.Order()), "active HK role/department/membership/expiry filters exclude ineligible and foreign candidates");
        var load = staff.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("id").GetInt64() == worker.UserId);
        check(load.GetProperty("openTasks").GetInt32() == 3 && load.GetProperty("openTasksInWindow").GetInt32() == 2, "workload uses distinct current assignments, excludes completed and separates UTC window");
        check(!staff.GetProperty("workflowStateVerified").GetBoolean() && staff.GetProperty("task").GetProperty("recordedStatus").GetString() == "PENDING", "raw PENDING never silently converted to SRS Pending Assignment");
        check(staff.GetProperty("recordedShifts")[0].GetProperty("recordedShiftStatus").GetString() == "PLANNED" && staff.GetProperty("recordedShifts")[0].GetProperty("recordedAssignmentStatus").GetString() == "ASSIGNED", "recorded shifts retain source states; no invented on-shift interpretation");
        var paged = await Get(Route(false) + "?pageSize=1&page=2", jar, 200);
        check(paged.GetProperty("total").GetInt32() == 2 && paged.GetProperty("candidates")[0].GetProperty("id").GetInt64() == free.UserId, "SQL count/page stable by name/ID");
        var inspectors = await Get(Route(true), jar, 200);
        var supervisorIds = inspectors.GetProperty("candidates").EnumerateArray().Select(c => c.GetProperty("id").GetInt64()).ToArray();
        check(supervisorIds.Contains(inspector.UserId) && supervisorIds.Contains(users["viewer"].UserId) && !supervisorIds.Contains(cleanerInspector.UserId), "inspection planning excludes recorded cycle cleaner while allowing other branch supervisors");
        check(inspectors.GetProperty("checklist").GetProperty("activeCriteria").GetInt32() > 0 && inspectors.GetProperty("checklist").GetProperty("versionNumber").GetInt32() >= 2,
            "inspection planning selects applicable effective Published default from Task72 contract");
        await db.Rooms.Where(r => r.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        await Get(Route(false), jar, 409); await Get(Route(true), jar, 409);
        await db.Rooms.Where(r => r.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
        foreach (var inspectionMode in new[] {false,true})
        {
            await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
            var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            await holder.CleaningTasks.Where(t => t.CleaningTaskId == target.CleaningTaskId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Instruction, "Uncommitted blocker"), ct);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, Route(inspectionMode)); request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            var pending = http.SendAsync(request, stop.Token); var api = 0;
            for (var i = 0; i < 200 && !pending.IsCompleted; i++)
            {
                var waiting = await db.Database.SqlQuery<int>($"SELECT CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                if (waiting.Length > 0) { api = waiting[0]; break; } await Task.Delay(30,ct);
            }
            check(api > 0, "Task73 actual DMV task-lock wait " + inspectionMode); stop.Cancel(); var canceled = false;
            try { using var response = await pending; } catch (OperationCanceledException) { canceled = true; }
            var released = false;
            for (var i = 0; i < 200; i++)
            {
                if (!await db.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)) { released = true; break; }
                await Task.Delay(30,ct);
            }
            check(canceled && released, "actual HTTPS cancellation releases planning transaction before blocker rollback"); await tx.RollbackAsync(ct);
            await Get(Route(inspectionMode), jar, 200);
        }
        check(await db.AuditLogs.CountAsync(ct) == audits && await db.CleaningAssignments.CountAsync(ct) == assignments
            && await db.CleaningTasks.AnyAsync(t => t.CleaningTaskId == target.CleaningTaskId && t.Status == "PENDING" && t.Instruction == null, ct), "planning persists no assignment/status/audit changes");
        await db.UserBranchAccesses.Where(a => a.UserId == worker.UserId && a.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, now), ct);
        var revokedCandidate = await Get(Route(false), jar, 200);
        check(!revokedCandidate.GetProperty("candidates").EnumerateArray().Any(c => c.GetProperty("id").GetInt64() == worker.UserId), "candidate membership revocation reflected in next SQL read");
        await db.UserRoles.Where(r => r.UserId == users["viewer"].UserId && r.RoleId == roles["HK_SUPERVISOR"]).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        await Get(Route(false), jar, 403); await Get(Route(true), jar, 403);
        await db.UserRoles.Where(r => r.UserId == users["viewer"].UserId && r.RoleId == roles["HK_SUPERVISOR"]).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
    }
}
