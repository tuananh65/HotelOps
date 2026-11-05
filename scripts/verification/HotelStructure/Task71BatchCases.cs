using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task71BatchCases
{
    internal static List<object> Results { get; } = [];
    internal static async Task LegacyAsync(HotelOpsDbContext db, HttpClient http, Func<string, Task<CookieJar>> login,
        long branch, Action<bool, string> check, CancellationToken ct)
    {
        check(await RoomTypeSchema.ReadAsync(db, ct) == RoomTypeContract.Legacy, "UC024/025 compatibility trial starts on unpatched RoomType schema");
        var jar = await login("admin"); var path = $"/api/branches/{branch}/departments";
        using var create = new HttpRequestMessage(HttpMethod.Post, path);
        Headers(create); create.Content = JsonContent.Create(new { code = "T71_LEGACY_SCHEMA", name = "Legacy-compatible department" });
        using var created = await http.SendAsync(create, ct);
        check((int)created.StatusCode == 201, "UC024 production HTTPS create works without UC017 schema deployment");
        using var value = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct)); var id = value.RootElement.GetProperty("id").GetInt64();
        using var update = new HttpRequestMessage(HttpMethod.Put, path + "/" + id); Headers(update);
        update.Headers.TryAddWithoutValidation("If-Match", created.Headers.ETag!.Tag);
        update.Content = JsonContent.Create(new { name = "Updated on legacy schema" });
        using var updated = await http.SendAsync(update, ct);
        check((int)updated.StatusCode == 200 && await db.Departments.AnyAsync(x => x.DepartmentId == id && x.DepartmentName == "Updated on legacy schema", ct), "UC025 production HTTPS update persists on legacy schema");
        void Headers(HttpRequestMessage r)
        {
            r.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            r.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
        }
    }
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        HttpClient http, Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> users,
        long branch, long foreign, Action<bool, string> check, CancellationToken ct)
    {
        var admin = await login("admin"); var scoped = await login("branch"); var viewer = await login("viewer");
        var anonymous = new CookieJar();
        using (var r = await http.GetAsync("/api/auth/csrf", ct)) { anonymous.Read(r); using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); anonymous.Token = j.RootElement.GetProperty("csrfToken").GetString(); }
        var uc = "setup"; var count = 0;
        void Verify(bool ok, string label) { check(ok, uc + ": " + label); count++; }
        async Task<(JsonElement Body, string? Tag)> Send(HttpMethod method, string path, CookieJar jar, int expected, object? body = null, string? tag = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (jar.Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            if (csrf && jar.Token != null) request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct); var text = await response.Content.ReadAsStringAsync(ct);
            Verify((int)response.StatusCode == expected, method + " " + path + " expected " + expected + " actual " + (int)response.StatusCode);
            Verify(!text.Contains("SqlException") && !text.Contains("passwordHash") && !text.Contains("stackTrace"), "no provider/credential disclosure");
            using var json = JsonDocument.Parse(text); return (json.RootElement.Clone(), response.Headers.ETag?.Tag);
        }
        var template = await db.InspectionChecklists.Where(x => x.ChecklistCode == "T68_TEMPLATE").Select(x => x.ChecklistId).SingleAsync(ct);
        async Task<long> Type(string code) => (await Send(HttpMethod.Post, "/api/room-types", admin, 201,
            new { branchId = branch, code, name = code, standardCleaningMinutes = 30, defaultChecklistId = template })).Body.GetProperty("id").GetInt64();
        var used = await Type("T71_USED"); var unused = await Type("T71_UNUSED"); var replacement = await Type("T71_REPLACEMENT");
        var floor = await db.Floors.Where(x => x.BranchId == branch && x.IsActive).Select(x => x.FloorId).FirstAsync(ct);
        var foreignFloor = await db.Floors.Where(x => x.BranchId == foreign && x.IsActive).Select(x => x.FloorId).FirstAsync(ct);
        var room = (await Send(HttpMethod.Post, "/api/rooms", admin, 201,
            new { branchId = branch, floorId = floor, roomTypeId = used, roomNumber = "T71_ROOM" })).Body.GetProperty("id").GetInt64();
        string TypePath(long id, long? b = null) => $"/api/room-types/{id}?branchId={b ?? branch}";
        string Deactivate(long id, long? b = null) => $"/api/room-types/{id}/deactivate?branchId={b ?? branch}";
        object Reason(bool confirmation = true, string reason = "Retire unused operational type") => new { confirmation, reason };
        async Task<long> Audits(string entity, long id) => await db.AuditLogs.LongCountAsync(x => x.EntityType == entity && x.EntityId == id.ToString(), ct);
        uc = "UC019"; count = 0;
        var typeTag = await Send(HttpMethod.Get, TypePath(unused), admin, 200);
        await Send(HttpMethod.Post, Deactivate(unused), anonymous, 401, Reason(), typeTag.Tag);
        await Send(HttpMethod.Post, Deactivate(unused), viewer, 403, Reason(), typeTag.Tag);
        await Send(HttpMethod.Post, Deactivate(unused, foreign), scoped, 403, Reason(), typeTag.Tag);
        await Send(HttpMethod.Post, Deactivate(long.MaxValue), admin, 404, Reason(), typeTag.Tag);
        await Send(HttpMethod.Post, Deactivate(unused), admin, 428, Reason());
        await Send(HttpMethod.Post, Deactivate(unused), admin, 400, Reason(), "*");
        await Send(HttpMethod.Post, Deactivate(unused), admin, 400, Reason(false), typeTag.Tag);
        await Send(HttpMethod.Post, Deactivate(unused), admin, 400, Reason(reason: "short"), typeTag.Tag);
        await Send(HttpMethod.Post, Deactivate(unused), admin, 400, Reason(), typeTag.Tag, false);
        var usedTag = await Send(HttpMethod.Get, TypePath(used), admin, 200);
        var blocked = await Send(HttpMethod.Post, Deactivate(used), admin, 409, Reason(), usedTag.Tag);
        Verify(blocked.Body.GetProperty("rooms").EnumerateArray().Any(x => x.GetProperty("id").GetInt64() == room), "active-room blocker lists scoped Room DTO");
        var before = await Audits("room_types", unused);
        await Fault("room_types", () => Send(HttpMethod.Post, Deactivate(unused), admin, 500, Reason(), typeTag.Tag));
        Verify(await db.RoomTypes.AnyAsync(x => x.RoomTypeId == unused && x.IsActive, ct) && await Audits("room_types", unused) == before, "audit fault rolls back type and audit");
        await Send(HttpMethod.Post, Deactivate(unused), scoped, 200, Reason(), typeTag.Tag);
        Verify(await db.RoomTypes.AnyAsync(x => x.RoomTypeId == unused && !x.IsActive, ct) && await Audits("room_types", unused) == before + 1, "soft deactivation and one atomic audit persisted");
        await Send(HttpMethod.Post, Deactivate(unused), admin, 409, Reason(), typeTag.Tag);
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, new { branchId = branch, floorId = floor, roomTypeId = unused, roomNumber = "T71_INACTIVE" });
        var raceType = await Type("T71_TYPE_RACE"); var raceTag = await Send(HttpMethod.Get, TypePath(raceType), admin, 200);
        var typeRace = await Task.WhenAll(Race(HttpMethod.Post, Deactivate(raceType), Reason(), raceTag.Tag!),
            Race(HttpMethod.Post, "/api/rooms", new { branchId = branch, floorId = floor, roomTypeId = raceType, roomNumber = "T71_TYPE_RACE" }, ""));
        Verify(typeRace.SequenceEqual(new[] { 200, 400 }) || typeRace.SequenceEqual(new[] { 412, 201 }) || typeRace.SequenceEqual(new[] { 409, 201 }), "deactivate/create HTTP race permits one consistent outcome");
        Verify(!await db.Rooms.AnyAsync(x => x.RoomTypeId == raceType && x.IsActive && !x.RoomType.IsActive, ct), "race never leaves active Room on inactive type");
        Results.Add(new { uc, checks = count });

        uc = "UC021"; count = 0;
        var cleaning = new CleaningTask { TaskNo = "T71_OPEN_WORK", RoomId = room,
            CleaningTypeId = await db.ReferenceValues.Where(x => x.GroupCode == "CLEANING_TYPE").Select(x => x.ReferenceId).FirstAsync(ct),
            Status = "PENDING", CreatedBy = users["admin"].UserId, CreatedAt = DateTime.UtcNow };
        db.Add(cleaning); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        string RoomPath(long id, bool edit = false, long? b = null) => $"/api/rooms/{id}{(edit ? "/edit" : "")}?branchId={b ?? branch}";
        object Update(string number = "T71_UPDATED", long? type = null, long? f = null, string reason = "Update room operational information") =>
            new { floorId = f ?? floor, roomTypeId = type ?? replacement, roomNumber = number, reason, notes = " Updated notes " };
        var loaded = await Send(HttpMethod.Get, RoomPath(room, true), admin, 200);
        var corrupt = new Room { BranchId = branch, FloorId = foreignFloor, RoomTypeId = replacement,
            RoomNumber = "T71_FOREIGN_PARENT", OperationalStatus = "DIRTY", IsActive = true };
        db.Add(corrupt); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await Send(HttpMethod.Get, RoomPath(corrupt.RoomId, true), admin, 404);
        await Send(HttpMethod.Put, RoomPath(corrupt.RoomId), admin, 404, Update(), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), anonymous, 401, Update(), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), viewer, 403, Update(), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room, b: foreign), scoped, 403, Update(), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(long.MaxValue), admin, 404, Update(), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), admin, 428, Update());
        await Send(HttpMethod.Put, RoomPath(room), admin, 400, Update(), "*");
        await Send(HttpMethod.Put, RoomPath(room), admin, 400, Update(reason: "bad"), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), admin, 400, Update(type: unused), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), admin, 400, Update(type: long.MaxValue), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), admin, 400, Update(f: foreignFloor), loaded.Tag);
        await Send(HttpMethod.Put, RoomPath(room), admin, 400, new { floorId = floor, roomTypeId = used, roomNumber = "X", reason = "An explicit reason", operationalStatus = "READY" }, loaded.Tag);
        var history = await db.RoomStatusHistories.CountAsync(x => x.RoomId == room, ct); before = await Audits("rooms", room);
        await Fault("rooms", () => Send(HttpMethod.Put, RoomPath(room), admin, 500, Update(), loaded.Tag));
        Verify(await db.Rooms.AnyAsync(x => x.RoomId == room && x.RoomNumber == "T71_ROOM" && x.RoomTypeId == used, ct) && await Audits("rooms", room) == before, "post-UPDATE audit rollback preserves room");
        var changed = await Send(HttpMethod.Put, RoomPath(room), scoped, 200, Update(), loaded.Tag);
        Verify(changed.Tag != loaded.Tag && changed.Body.GetProperty("operationalStatus").GetString() == "DIRTY"
            && await db.RoomStatusHistories.CountAsync(x => x.RoomId == room, ct) == history && await Audits("rooms", room) == before + 1, "room identity/status/history retained with atomic audit");
        await Send(HttpMethod.Put, RoomPath(room), admin, 412, Update(), loaded.Tag);
        var current = await Send(HttpMethod.Get, RoomPath(room, true), admin, 200);
        Verify(current.Tag == changed.Tag, "PUT ETag matches authoritative GET after SQL datetime rounding");
        Verify(await db.CleaningTasks.AnyAsync(x => x.CleaningTaskId == cleaning.CleaningTaskId && x.RoomId == room && x.Status == "PENDING", ct), "existing open cleaning work retains Room FK and status");
        var outcomes = await Task.WhenAll(Race(HttpMethod.Put, RoomPath(room), Update("T71_RACE_A"), current.Tag!), Race(HttpMethod.Put, RoomPath(room), Update("T71_RACE_B"), current.Tag!));
        Verify(outcomes.Order().SequenceEqual(new[] { 200, 412 }), "same version concurrent writers commit once");
        Results.Add(new { uc, checks = count });

        var manager = users["denied"].UserId; var role = await db.Roles.Where(x => x.RoleCode == "DEPT_MANAGER").Select(x => x.RoleId).SingleAsync(ct);
        db.Add(new UserRole { UserId = manager, BranchId = branch, RoleId = role, AssignedAt = DateTime.UtcNow.AddHours(-1), IsActive = true });
        db.Add(new UserBranchAccess { UserId = manager, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Task<string> Rbac() => db.Database.SqlQueryRaw<string>("SELECT (SELECT * FROM dbo.roles ORDER BY role_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.role_permissions ORDER BY role_id,permission_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.user_branch_access ORDER BY access_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        var rbacBefore = await Rbac();
        string Departments(long? b = null) => $"/api/branches/{b ?? branch}/departments";
        object Department(string code, long? m = null, string name = " Trial department ") => new { code, name, description = " Trial scope ", managerUserId = m };
        uc = "UC024"; count = 0;
        await Send(HttpMethod.Post, Departments(), anonymous, 401, Department("T71_AUTH"));
        await Send(HttpMethod.Post, Departments(), viewer, 403, Department("T71_DENIED"));
        await Send(HttpMethod.Post, Departments(foreign), scoped, 403, Department("T71_FOREIGN"));
        await Send(HttpMethod.Post, Departments(long.MaxValue), admin, 403, Department("T71_MISSING"));
        await Send(HttpMethod.Post, Departments(), admin, 400, Department("BAD CODE"));
        await Send(HttpMethod.Post, Departments(), admin, 400, Department("T71_BAD", name: " "));
        await Send(HttpMethod.Post, Departments(), admin, 400, Department("T71_MISSING_MANAGER", long.MaxValue));
        await Send(HttpMethod.Post, Departments(), admin, 400, Department("T71_WRONG_ROLE", users["other"].UserId));
        await Send(HttpMethod.Post, Departments(foreign), admin, 400, Department("T71_FOREIGN_MANAGER", manager));
        await Fault("departments", () => Send(HttpMethod.Post, Departments(), admin, 500, Department("T71_ROLLBACK", manager)));
        Verify(!await db.Departments.AnyAsync(x => x.DepartmentCode == "T71_ROLLBACK", ct), "create audit fault rolls back department");
        var created = await Send(HttpMethod.Post, Departments(), scoped, 201, Department(" t71_dept ", manager));
        var dept = created.Body.GetProperty("id").GetInt64();
        Verify(await db.Departments.AnyAsync(x => x.DepartmentId == dept && x.BranchId == branch && x.ManagerUserId == manager && x.IsActive && x.DepartmentCode == "T71_DEPT", ct)
            && await Audits("departments", dept) == 1, "manager designation and creation audit persisted");
        await Send(HttpMethod.Post, Departments(), admin, 409, Department("T71_DEPT"));
        await Send(HttpMethod.Post, Departments(), admin, 201, Department("T71_SECOND", manager));
        await Send(HttpMethod.Post, Departments(), admin, 201, Department("T71_OPTIONAL"));
        Verify(await db.Departments.CountAsync(x => x.BranchId == branch && x.ManagerUserId == manager, ct) == 2, "one branch DH can manage multiple departments");
        var departmentRace = await Task.WhenAll(Race(HttpMethod.Post, Departments(), Department("T71_DUP_RACE", manager), ""), Race(HttpMethod.Post, Departments(), Department("T71_DUP_RACE", manager), ""));
        Verify(departmentRace.Order().SequenceEqual(new[] { 201, 409 }) && await db.Departments.CountAsync(x => x.DepartmentCode == "T71_DUP_RACE" && x.BranchId == branch, ct) == 1, "duplicate department create race commits once");
        Results.Add(new { uc, checks = count });

        uc = "UC025"; count = 0;
        var path = Departments() + "/" + dept;
        object DeptUpdate(string name = "Updated department", long? m = null) => new { name, description = " Updated scope ", managerUserId = m };
        var edit = await Send(HttpMethod.Get, path, admin, 200);
        await Send(HttpMethod.Put, path, anonymous, 401, DeptUpdate(), edit.Tag);
        await Send(HttpMethod.Put, path, viewer, 403, DeptUpdate(), edit.Tag);
        await Send(HttpMethod.Put, Departments(foreign) + "/" + dept, scoped, 403, DeptUpdate(), edit.Tag);
        await Send(HttpMethod.Put, Departments() + "/" + long.MaxValue, admin, 404, DeptUpdate(), edit.Tag);
        await Send(HttpMethod.Put, path, admin, 428, DeptUpdate());
        await Send(HttpMethod.Put, path, admin, 400, DeptUpdate(), "*");
        await Send(HttpMethod.Put, path, admin, 400, DeptUpdate(" "), edit.Tag);
        await Send(HttpMethod.Put, path, admin, 400, DeptUpdate(m: long.MaxValue), edit.Tag);
        await Send(HttpMethod.Put, path, admin, 400, new { name = "Illegal reparent", branchId = foreign }, edit.Tag);
        before = await Audits("departments", dept);
        await Fault("departments", () => Send(HttpMethod.Put, path, admin, 500, DeptUpdate(m: manager), edit.Tag));
        Verify(await Audits("departments", dept) == before && await db.Departments.AnyAsync(x => x.DepartmentId == dept && x.DepartmentName == "Trial department", ct), "update audit fault rolls back department");
        var saved = await Send(HttpMethod.Put, path, scoped, 200, DeptUpdate(m: manager), edit.Tag);
        Verify(saved.Tag != edit.Tag && await Audits("departments", dept) == before + 1, "update persisted with one audit and new ETag");
        await Send(HttpMethod.Put, path, admin, 412, DeptUpdate(), edit.Tag);
        outcomes = await Task.WhenAll(Race(HttpMethod.Put, path, DeptUpdate("Race A", manager), saved.Tag!), Race(HttpMethod.Put, path, DeptUpdate("Race B", manager), saved.Tag!));
        Verify(outcomes.Order().SequenceEqual(new[] { 200, 412 }), "concurrent department updates commit once");
        await db.Departments.Where(x => x.DepartmentId == dept).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, false), ct);
        try { await Send(HttpMethod.Put, path, admin, 409, DeptUpdate(), saved.Tag); }
        finally { await db.Departments.Where(x => x.DepartmentId == dept).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, true), ct); }
        Results.Add(new { uc, checks = count });
        await Task71SqlCases.RunAsync(db, open, users["admin"].UserId, manager, branch, used, replacement, room, floor, dept, check, ct);
        Verify(await Rbac() == rbacBefore, "manager designation does not change roles/grants/memberships");

        async Task<int> Race(HttpMethod method, string uri, object value, string tag)
        {
            using var request = new HttpRequestMessage(method, uri); request.Headers.Add("Cookie", string.Join("; ", admin.Cookies.Select(x => x.Key + "=" + x.Value)));
            request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, admin.Token); request.Headers.TryAddWithoutValidation("If-Match", tag); request.Content = JsonContent.Create(value);
            using var r = await http.SendAsync(request, ct); return (int)r.StatusCode;
        }
        async Task Fault(string entity, Func<Task<(JsonElement, string?)>> action)
        {
            if (entity is not ("rooms" or "room_types" or "departments")) throw new InvalidOperationException("Unknown fault fixture entity.");
            var sql = "CREATE TRIGGER dbo.TR_Task71_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='" + entity + "') THROW 51071,'Task71 deliberate audit failure',1; END;";
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            try { await action(); } finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_Task71_AuditFault", CancellationToken.None); }
        }
    }
}
