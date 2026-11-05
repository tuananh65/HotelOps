using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task70BatchCases
{
    internal static List<object> Results { get; } = [];
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        HttpClient http, Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> users,
        long branch, long foreign, Action<bool, string> check, CancellationToken ct)
    {
        var count = 0; string uc = "setup";
        void Verify(bool ok, string label) { check(ok, uc + ": " + label); count++; }
        var admin = await login("admin"); var scoped = await login("branch"); var viewer = await login("viewer");
        var denied = await login("denied");
        var anonymous = new CookieJar();
        using (var bootstrap = await http.GetAsync("/api/auth/csrf", ct))
        {
            anonymous.Read(bootstrap);
            using var json = JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));
            anonymous.Token = json.RootElement.GetProperty("csrfToken").GetString();
        }
        async Task<(JsonElement Body, string? Tag)> Send(HttpMethod method, string path, CookieJar jar, int expected,
            object? body = null, string? tag = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (jar.Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            if (csrf && jar.Token != null) request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            Verify((int)response.StatusCode == expected, method + " " + path + " expected " + expected + ", actual " + (int)response.StatusCode);
            Verify(!text.Contains("SqlException") && !text.Contains("passwordHash") && !text.Contains("stackTrace"), "no provider/credential disclosure");
            using var json = JsonDocument.Parse(text);
            return (json.RootElement.Clone(), response.Headers.ETag?.Tag);
        }
        if (await RoomTypeSchema.ReadAsync(db, ct) == RoomTypeContract.Legacy)
            await Task69DeploymentCases.DeployAsync(db, "Apply", true, check, ct);
        var template = await db.InspectionChecklists.SingleOrDefaultAsync(x => x.ChecklistCode == "T68_TEMPLATE", ct);
        if (template == null)
        {
            template = new InspectionChecklist { ChecklistCode = "T68_TEMPLATE", ChecklistName = "Task70 trial template", IsActive = true };
            db.Add(template); await db.SaveChangesAsync(ct);
            var version = new InspectionChecklistVersion { ChecklistId = template.ChecklistId, VersionNo = 1, Status = "PUBLISHED", EffectiveAt = DateTime.UtcNow.AddDays(-1) };
            db.Add(version); await db.SaveChangesAsync(ct);
            db.Add(new InspectionChecklistItem { VersionId = version.VersionId, Criterion = "Clean floor", PassCondition = "No visible dirt", IsRequired = true, IsActive = true, SortOrder = 1 });
            db.Add(new RoomType { TypeCode = "T68_LEGACY", TypeName = "Legacy type", IsActive = true });
            await db.SaveChangesAsync(ct);
        }
        var checklist = template.ChecklistId; db.ChangeTracker.Clear();
        async Task<long> CreateType(string code, long b)
        {
            var result = await Send(HttpMethod.Post, "/api/room-types", admin, 201,
                new { branchId = b, code, name = code, standardCleaningMinutes = 30, defaultChecklistId = checklist });
            return result.Body.GetProperty("id").GetInt64();
        }
        var id = await CreateType("T70_TYPE", branch); var otherId = await CreateType("T70_TYPE", foreign);
        var floor = new Floor { BranchId = branch, FloorCode = "T70_F", FloorName = "Task70", IsActive = true };
        var foreignFloor = new Floor { BranchId = foreign, FloorCode = "T70_F", FloorName = "Foreign", IsActive = true };
        db.AddRange(floor, foreignFloor); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var retainedRoom = new Room { BranchId = branch, FloorId = floor.FloorId, RoomTypeId = id,
            RoomNumber = "T70_HISTORY", OperationalStatus = "DIRTY", IsActive = false };
        db.Add(retainedRoom); await db.SaveChangesAsync(ct);
        var originalVersion = await db.InspectionChecklistVersions.Where(x => x.ChecklistId == checklist).Select(x => x.VersionId).FirstAsync(ct);
        var historicalInspection = new RoomInspection { InspectionNo = "T70_HISTORY", RoomId = retainedRoom.RoomId,
            VersionId = originalVersion, InspectorUserId = users["admin"].UserId, Status = "COMPLETED", Result = "PASS",
            CreatedAt = DateTime.UtcNow.AddDays(-1), InspectedAt = DateTime.UtcNow.AddDays(-1) };
        db.Add(historicalInspection); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        string TypePath(long b = 0, long target = 0) => $"/api/room-types/{(target == 0 ? id : target)}?branchId={(b == 0 ? branch : b)}";
        object Update(string name = "Updated operational type", int minutes = 45, long template = 0) =>
            new { name, description = " Updated description ", standardCleaningMinutes = minutes, defaultChecklistId = template == 0 ? checklist : template };
        async Task<string> TypeSnapshot() => await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.room_types WHERE room_type_id={id} FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        async Task<long> AuditCount(string entity, long target) => await db.AuditLogs.LongCountAsync(x => x.EntityType == entity && x.EntityId == target.ToString(), ct);
        uc = "UC018"; count = 0;
        var loaded = await Send(HttpMethod.Get, TypePath(), admin, 200);
        Verify(loaded.Tag is { Length: 66 }, "edit GET strong ETag");
        await Send(HttpMethod.Get, TypePath(), new CookieJar(), 401);
        await Send(HttpMethod.Get, TypePath(), viewer, 403);
        await Send(HttpMethod.Get, TypePath(foreign, otherId), scoped, 403);
        await Send(HttpMethod.Get, TypePath(branch, long.MaxValue), admin, 404);
        var legacy = await db.RoomTypes.Where(x => x.TypeCode == "T68_LEGACY").Select(x => x.RoomTypeId).SingleAsync(ct);
        await Send(HttpMethod.Get, TypePath(branch, legacy), admin, 404);
        await Send(HttpMethod.Put, TypePath(), admin, 428, Update());
        await Send(HttpMethod.Put, TypePath(), admin, 400, Update(), "*");
        await Send(HttpMethod.Put, TypePath(), admin, 400, Update(" "), loaded.Tag);
        await Send(HttpMethod.Put, TypePath(), admin, 400, Update(minutes: 0), loaded.Tag);
        await Send(HttpMethod.Put, TypePath(), admin, 400, Update(template: long.MaxValue), loaded.Tag);
        await Send(HttpMethod.Put, TypePath(), admin, 409,
            new { name = "Duplicate", standardCleaningMinutes = 30, defaultChecklistId = checklist, code = "t68_legacy" }, loaded.Tag);
        await Send(HttpMethod.Put, TypePath(), admin, 400,
            new { name = "Invalid code", standardCleaningMinutes = 30, defaultChecklistId = checklist, code = "bad code" }, loaded.Tag);
        await Send(HttpMethod.Put, TypePath(), admin, 400, new { name = "Illegal", standardCleaningMinutes = 30, defaultChecklistId = checklist, branchId = foreign }, loaded.Tag);
        await Send(HttpMethod.Put, TypePath(), admin, 400, Update(), loaded.Tag, false);
        var oldAudit = await AuditCount("room_types", id);
        var changed = await Send(HttpMethod.Put, TypePath(), scoped, 200, Update(), loaded.Tag);
        Verify(changed.Tag != loaded.Tag && changed.Body.GetProperty("name").GetString() == "Updated operational type", "scoped update returns changed ETag and normalized DTO");
        Verify(await AuditCount("room_types", id) == oldAudit + 1, "exactly one committed UPDATE audit");
        Verify(await db.BranchRoomTypes.AnyAsync(x => x.RoomTypeId == id && x.BranchId == branch && x.TypeCode == "T70_TYPE" && x.StandardCleaningMinutes == 45, ct), "SQL persistence and immutable code/ownership");
        Verify(await db.Rooms.AnyAsync(x => x.RoomId == retainedRoom.RoomId && x.RoomTypeId == id, ct)
            && await db.RoomInspections.AnyAsync(x => x.InspectionId == historicalInspection.InspectionId && x.VersionId == originalVersion && x.Result == "PASS", ct), "Room FK and completed inspection version/result preserved");
        await Send(HttpMethod.Put, TypePath(), admin, 412, Update("Stale"), loaded.Tag);
        // Same ETag, two independent real HTTP writers: one wins, one must reload.
        var outcomes = await Task.WhenAll(Race("Race A"), Race("Race B"));
        Verify(outcomes.Order().SequenceEqual(new[] { 200, 412 }), "concurrent If-Match updates commit once");
        Verify(await AuditCount("room_types", id) == oldAudit + 2, "loser creates no audit");
        async Task<int> Race(string name)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, TypePath());
            request.Headers.Add("Cookie", string.Join("; ", admin.Cookies.Select(x => x.Key + "=" + x.Value)));
            request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, admin.Token);
            request.Headers.TryAddWithoutValidation("If-Match", changed.Tag);
            request.Content = JsonContent.Create(Update(name));
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        var snapshot = await TypeSnapshot(); var beforeFaultAudit = await AuditCount("room_types", id);
        var fresh = await Send(HttpMethod.Get, TypePath(), admin, 200);
        await Fault("room_types", async () => await Send(HttpMethod.Put, TypePath(), admin, 500, Update("Must rollback"), fresh.Tag));
        Verify(await TypeSnapshot() == snapshot && await AuditCount("room_types", id) == beforeFaultAudit, "real audit fault rolls back UPDATE and audit");
        await db.RoomTypes.Where(x => x.RoomTypeId == id).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, false), ct);
        try { await Send(HttpMethod.Put, TypePath(), admin, 409, Update(), fresh.Tag); }
        finally { await db.RoomTypes.Where(x => x.RoomTypeId == id).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, true), ct); }
        var codeTag = await Send(HttpMethod.Get, TypePath(), admin, 200);
        var codeChange = await Send(HttpMethod.Put, TypePath(), admin, 200,
            new { name = "Code edit", standardCleaningMinutes = 45, defaultChecklistId = checklist, code = " t70_renamed " }, codeTag.Tag);
        Verify(codeChange.Body.GetProperty("code").GetString() == "T70_RENAMED" && await db.Rooms.AnyAsync(x => x.RoomId == retainedRoom.RoomId && x.RoomTypeId == id, ct), "optional code normalized, IDs/references preserved");
        await Send(HttpMethod.Put, TypePath(), admin, 200,
            new { name = "Type restored", standardCleaningMinutes = 45, defaultChecklistId = checklist, code = "T70_TYPE" }, codeChange.Tag);
        Results.Add(new { uc, checks = count });

        uc = "UC142"; count = 0;
        var listAudit = await db.AuditLogs.CountAsync(ct); var listSnapshot = await TypeSnapshot();
        var list = await Send(HttpMethod.Get, $"/api/room-types?branchId={branch}&search=T70_TYPE", scoped, 200);
        Verify(list.Body.GetProperty("total").GetInt32() == 1, "branch filter/search/count");
        var auto = await Send(HttpMethod.Get, "/api/room-types?search=T70_TYPE", scoped, 200);
        Verify(auto.Body.GetProperty("total").GetInt32() == 1 && auto.Body.GetProperty("items")[0].GetProperty("details").GetProperty("branchId").GetInt64() == branch, "default scoped list excludes foreign branch");
        var all = await Send(HttpMethod.Get, "/api/room-types?search=T70_TYPE&pageSize=1", admin, 200);
        Verify(all.Body.GetProperty("total").GetInt32() == 2 && all.Body.GetProperty("items").GetArrayLength() == 1, "global list pagination and stable total");
        await Send(HttpMethod.Get, $"/api/room-types?branchId={foreign}", scoped, 403);
        await Send(HttpMethod.Get, "/api/room-types", denied, 403);
        await Send(HttpMethod.Get, "/api/room-types", new CookieJar(), 401);
        await Send(HttpMethod.Get, "/api/room-types?pageSize=101", admin, 400);
        await Send(HttpMethod.Get, "/api/room-types?page=0", admin, 400);
        await Send(HttpMethod.Get, "/api/room-types?isActive=bogus", admin, 400);
        await Send(HttpMethod.Get, "/api/room-types?sort=bogus", admin, 400);
        var sorted = await Send(HttpMethod.Get, "/api/room-types?sort=name&descending=true", admin, 200);
        Verify(sorted.Body.GetProperty("sort").GetString() == "name" && sorted.Body.GetProperty("descending").GetBoolean(), "sort filters echoed");
        var inactiveList = await Send(HttpMethod.Get, $"/api/room-types?branchId={branch}&isActive=false", admin, 200);
        Verify(inactiveList.Body.GetProperty("items").EnumerateArray().All(x => !x.GetProperty("details").GetProperty("isActive").GetBoolean()), "inactive status filter");
        var empty = await Send(HttpMethod.Get, $"/api/room-types?branchId={branch}&search=NO_MATCH_T70", scoped, 200);
        Verify(empty.Body.GetProperty("total").GetInt32() == 0 && empty.Body.GetProperty("items").GetArrayLength() == 0, "empty result includes filters");
        var legacyList = await Send(HttpMethod.Get, "/api/room-types?search=T68_LEGACY", admin, 200);
        Verify(legacyList.Body.GetProperty("items")[0].GetProperty("details").GetProperty("branchId").ValueKind == JsonValueKind.Null, "global legacy ownership remains null");
        await Send(HttpMethod.Get, $"/api/room-types/{legacy}", admin, 200);
        await Send(HttpMethod.Get, $"/api/room-types/{legacy}", scoped, 403);
        Verify(await TypeSnapshot() == listSnapshot && await db.AuditLogs.CountAsync(ct) == listAudit, "read-only list/detail preserve business data/audit");
        Results.Add(new { uc, checks = count });

        uc = "UC020"; count = 0;
        object Room(string number, long? type = null, long? b = null, long? f = null, long? area = null) =>
            new { branchId = b ?? branch, floorId = f ?? floor.FloorId, roomTypeId = type ?? id, roomNumber = number, areaId = area, notes = " Trial notes " };
        await Send(HttpMethod.Post, "/api/rooms", anonymous, 401, Room("T70_AUTH"));
        await Send(HttpMethod.Post, "/api/rooms", viewer, 403, Room("T70_DENIED"));
        await Send(HttpMethod.Post, "/api/rooms", scoped, 403, Room("T70_FOREIGN", otherId, foreign, foreignFloor.FloorId));
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_BAD_TYPE", type: otherId));
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_BAD_FLOOR", f: foreignFloor.FloorId));
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_MISSING", type: long.MaxValue));
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_AREA", area: long.MaxValue));
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_LEGACY", type: legacy));
        await db.RoomTypes.Where(x => x.RoomTypeId == id).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, false), ct);
        try { await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_INACTIVE_TYPE")); }
        finally { await db.RoomTypes.Where(x => x.RoomTypeId == id).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, true), ct); }
        await db.Floors.Where(x => x.FloorId == floor.FloorId).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, false), ct);
        try { await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_INACTIVE_FLOOR")); }
        finally { await db.Floors.Where(x => x.FloorId == floor.FloorId).ExecuteUpdateAsync(x => x.SetProperty(v => v.IsActive, true), ct); }
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room(" "));
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, new { branchId = branch, floorId = floor.FloorId, roomTypeId = id, roomNumber = "T70_OVERRIDE", operationalStatus = "READY" });
        await Send(HttpMethod.Post, "/api/rooms", admin, 400, Room("T70_CSRF"), csrf: false);
        var created = await Send(HttpMethod.Post, "/api/rooms", scoped, 201, Room(" t70_room "));
        var roomId = created.Body.GetProperty("id").GetInt64();
        Verify(created.Body.GetProperty("operationalStatus").GetString() == "DIRTY", "initial Pending Cleaning status");
        Verify(await db.Rooms.AnyAsync(x => x.RoomId == roomId && x.IsActive && x.RoomNumber == "T70_ROOM" && x.RoomTypeId == id && x.Description == "Trial notes", ct), "SQL normalized room persisted");
        Verify(await db.RoomStatusHistories.CountAsync(x => x.RoomId == roomId && x.FromStatus == null && x.ToStatus == "DIRTY" && x.ChangedBy == users["branch"].UserId, ct) == 1
            && await AuditCount("rooms", roomId) == 1, "initial history and actor audit committed once");
        await Send(HttpMethod.Post, "/api/rooms", admin, 409, Room("t70_ROOM"));
        var foreignRoom = await Send(HttpMethod.Post, "/api/rooms", admin, 201, Room("T70_ROOM", otherId, foreign, foreignFloor.FloorId));
        Verify(foreignRoom.Body.GetProperty("branchId").GetInt64() == foreign, "same room number allowed in another branch");
        var roomsBefore = await db.Rooms.CountAsync(ct); var historyBefore = await db.RoomStatusHistories.CountAsync(ct); var auditBefore = await db.AuditLogs.CountAsync(ct);
        await Fault("rooms", async () => await Send(HttpMethod.Post, "/api/rooms", admin, 500, Room("T70_ROLLBACK")));
        Verify(await db.Rooms.CountAsync(ct) == roomsBefore && await db.RoomStatusHistories.CountAsync(ct) == historyBefore && await db.AuditLogs.CountAsync(ct) == auditBefore,
            "post-INSERT audit fault rolls back room/history/audit");
        await Send(HttpMethod.Post, "/api/rooms", admin, 201, Room("T70_ROLLBACK"));
        var refreshedList = await Send(HttpMethod.Get, $"/api/room-types?branchId={branch}&search=T70_TYPE", scoped, 200);
        Verify(refreshedList.Body.GetProperty("items")[0].GetProperty("activeRooms").GetInt32() == 2, "UC142 SQL active room count reflects UC020");
        var roomRace = await Task.WhenAll(CreateRace(), CreateRace());
        Verify(roomRace.Order().SequenceEqual(new[] { 201, 409 }), "same-number concurrent HTTP creates commit once");
        Verify(await db.Rooms.CountAsync(x => x.BranchId == branch && x.RoomNumber == "T70_RACE", ct) == 1
            && await db.RoomStatusHistories.CountAsync(x => x.Room.RoomNumber == "T70_RACE", ct) == 1, "duplicate loser leaves no room/history");
        async Task<int> CreateRace()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms");
            request.Headers.Add("Cookie", string.Join("; ", admin.Cookies.Select(x => x.Key + "=" + x.Value)));
            request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, admin.Token);
            request.Content = JsonContent.Create(Room("T70_RACE"));
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        Results.Add(new { uc, checks = count });

        async Task Fault(string entity, Func<Task> operation)
        {
            // A real SQL trigger raises an application error after the business write.
            var sql = "CREATE TRIGGER dbo.TR_Task70_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='" + entity + "') THROW 51070,'Task70 audit rollback probe',1; END;";
            await db.Database.ExecuteSqlRawAsync(sql, ct);
            try { await operation(); }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_Task70_AuditFault", CancellationToken.None); }
        }
        await Task70SqlCases.RunAsync(db, open, users["admin"].UserId, branch, id, checklist, floor.FloorId, check, ct);
    }
}
