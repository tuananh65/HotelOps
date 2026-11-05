using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task64StructureCases
{
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        HttpClient http, Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> identities,
        long branch, long otherBranch, Action<bool, string> check, CancellationToken ct)
    {
        var role = await db.Roles.Where(x => x.RoleCode == "OPERATIONS_MANAGER").Select(x => x.RoleId).SingleAsync(ct);
        var omGrant = new UserRole { UserId = identities["denied"].UserId, RoleId = role, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddHours(-1) };
        var membership = new UserBranchAccess { UserId = identities["denied"].UserId, BranchId = branch, AccessLevel = "STANDARD", GrantedAt = DateTime.UtcNow.AddHours(-1) };
        var emptyBranch = new Branch { BranchCode = "T64_EMPTY", BranchName = "Empty branch", IsActive = true };
        var archivedBranch = new Branch { BranchCode = "T64_ARCHIVE", BranchName = "Inactive branch", IsActive = false };
        var type = new RoomType { TypeCode = "T64_TYPE", TypeName = "Fixture room type", IsActive = true };
        db.AddRange(omGrant, membership, emptyBranch, archivedBranch, type);
        var floors = Enumerable.Range(0, 26).Select(i => new Floor { BranchId = branch, FloorCode = $"T64_F{i:00}", FloorName = $"Floor {i:00}",
            FloorNumber = i == 25 ? null : i / 2, Description = "T64_ADMIN_ONLY", IsActive = i != 2 }).ToArray();
        var foreignFloor = new Floor { BranchId = otherBranch, FloorCode = "T64_B", FloorName = "T64_FOREIGN_SECRET", IsActive = true };
        db.AddRange(floors); db.Add(foreignFloor); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Area[] areas = [new() { BranchId = branch, FloorId = floors[0].FloorId, AreaCode = "T64_A", AreaName = "First area", Description = "T64_ADMIN_ONLY", IsActive = true },
            new() { BranchId = branch, FloorId = floors[0].FloorId, AreaCode = "T64_INACTIVE", AreaName = "Inactive area", IsActive = false },
            new() { BranchId = branch, FloorId = null, AreaCode = "T64_ROOT", AreaName = "Branch level", IsActive = true },
            new() { BranchId = branch, FloorId = floors[1].FloorId, AreaCode = "T64_OTHER", AreaName = "Other floor", IsActive = true },
            new() { BranchId = otherBranch, FloorId = foreignFloor.FloorId, AreaCode = "T64_B", AreaName = "T64_FOREIGN_SECRET", IsActive = true },
            new() { BranchId = branch, FloorId = foreignFloor.FloorId, AreaCode = "T64_CORRUPT", AreaName = "T64_FOREIGN_SECRET", IsActive = true }];
        db.AddRange(areas);
        var department = new Department { BranchId = branch, DepartmentCode = "T64_DEPT", DepartmentName = "Structure department", IsActive = true,
            ManagerUserId = identities["other"].UserId, ScopeDescription = "T64_ADMIN_ONLY" };
        db.Add(department); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var rooms = Enumerable.Range(0, 25).Select(i => new Room { BranchId = branch, FloorId = floors[0].FloorId,
            AreaId = areas[0].AreaId, RoomTypeId = type.RoomTypeId, RoomNumber = $"T64_{i:00}", IsActive = i != 1,
            OperationalStatus = "DIRTY", Description = "T64_OPERATIONAL_SECRET" }).ToList();
        rooms.AddRange([new() { BranchId = branch, FloorId = floors[0].FloorId, RoomTypeId = type.RoomTypeId, RoomNumber = "T64_NO_AREA", IsActive = true, OperationalStatus = "AVAILABLE" },
            new() { BranchId = branch, FloorId = floors[0].FloorId, AreaId = areas[2].AreaId, RoomTypeId = type.RoomTypeId, RoomNumber = "T64_BRANCH_AREA", IsActive = true, OperationalStatus = "AVAILABLE" },
            new() { BranchId = branch, FloorId = floors[2].FloorId, RoomTypeId = type.RoomTypeId, RoomNumber = "T64_HISTORICAL", IsActive = true, OperationalStatus = "DIRTY" },
            new() { BranchId = branch, FloorId = foreignFloor.FloorId, RoomTypeId = type.RoomTypeId, RoomNumber = "T64_FOREIGN_SECRET_1", IsActive = true, OperationalStatus = "DIRTY" },
            new() { BranchId = branch, FloorId = floors[0].FloorId, AreaId = areas[4].AreaId, RoomTypeId = type.RoomTypeId, RoomNumber = "T64_FOREIGN_SECRET_2", IsActive = true, OperationalStatus = "DIRTY" },
            new() { BranchId = branch, FloorId = floors[0].FloorId, AreaId = areas[3].AreaId, RoomTypeId = type.RoomTypeId, RoomNumber = "T64_FOREIGN_SECRET_3", IsActive = true, OperationalStatus = "DIRTY" }]);
        db.AddRange(rooms); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var jars = new Dictionary<string, CookieJar>();
        foreach (var key in new[] { "admin", "other", "branch", "denied", "viewer", "owner", "restricted", "inactiveAssignment", "expiredAssignment", "futureAssignment" })
            jars[key] = await login(key);
        var admin = jars["admin"]; var om = jars["denied"];
        string Route(long b) => $"/api/branches/{b}/structure";
        async Task<HttpResponseMessage> Get(string path, CookieJar jar, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (jar.Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            var response = await http.SendAsync(request, token); jar.Read(response); return response;
        }
        async Task<JsonElement> Read(string path, CookieJar jar)
        {
            using var response = await Get(path, jar, ct); var body = await response.Content.ReadAsStringAsync(ct);
            check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "UC013 production HTTPS 200/no-store: " + path);
            using var json = JsonDocument.Parse(body); Fields(json.RootElement);
            check(!body.Contains("T64_FOREIGN_SECRET") && !body.Contains("T64_OPERATIONAL_SECRET"), "UC013 no foreign-branch/operational marker leaked");
            return json.RootElement.Clone();
        }
        async Task Denied(string path, CookieJar jar, int status, string label)
        {
            using var response = await Get(path, jar, ct); var body = await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode == status && !body.Contains("totalCount") && !body.Contains("selectedFloor")
                && !body.Contains("T64_") && !body.Contains("SqlException"), "UC013 " + label + $" ({(int)response.StatusCode})");
        }
        JsonElement[] Items(JsonElement root, string collection) => root.GetProperty(collection).GetProperty("items").EnumerateArray().ToArray();
        async Task<string> Snapshot()
        {
            var parts = new List<string>();
            foreach (var table in new[] { "branches", "floors", "areas", "rooms", "departments", "users", "user_roles", "user_branch_access", "audit_logs", "roles", "role_permissions", "permissions" })
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
                await using var reader = await command.ExecuteReaderAsync(ct); var text = new System.Text.StringBuilder();
                while (await reader.ReadAsync(ct)) text.Append(reader.GetString(0));
                parts.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString()))));
            }
            return string.Join(':', parts);
        }
        try
        {
            var before = await Snapshot(); var sessions = await db.UserSessions.CountAsync(ct);
            await Denied(Route(branch), new(), 401, "anonymous denied");
            foreach (var key in new[] { "owner", "restricted", "inactiveAssignment", "expiredAssignment", "futureAssignment" })
                await Denied(Route(branch), jars[key], 403, "unsupported/ineffective path denied " + key);
            await Denied(Route(otherBranch), om, 403, "ROOM_VIEW BRANCH cannot cross branches");
            await Denied(Route(otherBranch), jars["branch"], 403, "management ALL-bound cannot cross branches");
            var full = await Read(Route(branch), admin); var basic = await Read(Route(branch), om);
            var scoped = await Read(Route(branch), jars["branch"]);
            var viewer = await Read(Route(branch), jars["viewer"]);
            check(scoped.GetProperty("visibility").GetString() == "management" && viewer.GetProperty("visibility").GetString() == "basic",
                "UC013 SQL grant scope controls access independently of role name or primary branch");
            check(full.GetProperty("visibility").GetString() == "management" && basic.GetProperty("visibility").GetString() == "basic", "UC013 independent management/basic grant paths");
            check(Items(full, "floors").Length == 20 && full.GetProperty("floors").GetProperty("pageSize").GetInt32() == 20, "UC013 GBR015 default20 pagination");
            check(Items(basic, "floors").All(x => x.GetProperty("description").ValueKind == JsonValueKind.Null)
                && !basic.GetRawText().Contains("T64_ADMIN_ONLY") && !basic.GetProperty("availableCollections").EnumerateArray().Any(x => x.GetString() == "departments"),
                "UC013 ROOM_VIEW exposes physical fields only, no administrative descriptions/departments");
            var all = await Read(Route(branch) + "?pageSize=100", admin);
            var expectedOrder = await db.Floors.Where(x => x.BranchId == branch).OrderBy(x => x.FloorNumber == null ? 1 : 0)
                .ThenBy(x => x.FloorNumber).ThenBy(x => x.FloorCode).ThenBy(x => x.FloorId).Select(x => x.FloorId).ToArrayAsync(ct);
            check(Items(all, "floors").Select(x => x.GetProperty("id").GetInt64()).SequenceEqual(expectedOrder), "UC013 deterministic numeric/null/code/ID Floor order matches SQL");
            var page2 = await Read(Route(branch) + "?page=2", admin);
            check(!Items(full, "floors").Select(x => x.GetProperty("id").GetInt64()).Intersect(Items(page2, "floors").Select(x => x.GetProperty("id").GetInt64())).Any(), "UC013 pages do not overlap");
            var far = await Read(Route(branch) + "?page=1000", admin);
            check(Items(far, "floors").Length == 0 && far.GetProperty("floors").GetProperty("totalCount").GetInt64() == expectedOrder.Length, "UC013 beyond-last page retains total with empty items");
            foreach (var flag in new[] { true, false })
            {
                var filtered = await Read(Route(branch) + "?isActive=" + flag, admin);
                check(Items(filtered, "floors").All(x => x.GetProperty("isActive").GetBoolean() == flag), "UC013 active/inactive filter retained");
            }
            var empty = await Read(Route(emptyBranch.BranchId), admin);
            check(Items(empty, "floors").Length == 0, "UC013 empty branch returns valid empty page");
            await Read(Route(archivedBranch.BranchId), admin);
            await Denied(Route(archivedBranch.BranchId), om, 403, "ROOM_VIEW inactive branch remains denied by existing evaluator");
            await Denied(Route(long.MaxValue), admin, 404, "global manager missing branch404");
            await Denied(Route(long.MaxValue), om, 403, "scoped missing/out-of-scope branch indistinguishable");
            var floorPath = Route(branch) + "?nodeType=floor&nodeId=" + floors[0].FloorId;
            var floorView = await Read(floorPath, admin);
            check(Items(floorView, "areas").Length == 2 && Items(floorView, "areas").All(x => x.GetProperty("floorId").GetInt64() == floors[0].FloorId), "UC013 Floor expansion returns exactly its Areas, including inactive");
            var basicFloor = await Read(floorPath, om);
            check(basicFloor.GetProperty("selectedFloor").GetProperty("description").ValueKind == JsonValueKind.Null
                && Items(basicFloor, "areas").All(x => x.GetProperty("description").ValueKind == JsonValueKind.Null), "UC013 basic node details and children redact admin metadata");
            var emptyFloor = await Read(Route(branch) + "?nodeType=floor&nodeId=" + floors[24].FloorId, admin);
            check(Items(emptyFloor, "areas").Length == 0, "UC013 empty Floor expansion works");
            var floorRooms = await Read(floorPath + "&collection=rooms&pageSize=100", om);
            check(Items(floorRooms, "rooms").Length == 27 && Items(floorRooms, "rooms").All(x => x.GetProperty("floorId").GetInt64() == floors[0].FloorId), "UC013 actual Floor→Room FK includes valid no-area/branch-area links, excludes corrupt links");
            var areaView = await Read(Route(branch) + "?nodeType=area&nodeId=" + areas[0].AreaId, om);
            check(Items(areaView, "rooms").Length == 20 && areaView.GetProperty("rooms").GetProperty("totalCount").GetInt64() == 25, "UC013 Area→Rooms expansion is paginated with correct total");
            var areaPage2 = await Read(Route(branch) + "?nodeType=area&nodeId=" + areas[0].AreaId + "&page=2", om);
            check(Items(areaPage2, "rooms").Length == 5, "UC013 room pagination boundary");
            check(Items(areaView, "rooms").Concat(Items(areaPage2, "rooms")).Select(x => x.GetProperty("roomNumber").GetString())
                .SequenceEqual(Enumerable.Range(0, 25).Select(i => $"T64_{i:00}")), "UC013 Room ordering and page continuity match the complete fixture");
            var inactiveRooms = await Read(floorPath + "&collection=rooms&isActive=false", om);
            check(Items(inactiveRooms, "rooms").Single().GetProperty("roomNumber").GetString() == "T64_01", "UC013 Room active filter selects exact inactive child");
            var inactiveAreas = await Read(floorPath + "&isActive=false", om);
            check(Items(inactiveAreas, "areas").Single().GetProperty("id").GetInt64() == areas[1].AreaId, "UC013 Area active filter selects exact inactive child");
            var emptyArea = await Read(Route(branch) + "?nodeType=area&nodeId=" + areas[1].AreaId, om);
            check(Items(emptyArea, "rooms").Length == 0 && !emptyArea.GetProperty("selectedArea").GetProperty("isActive").GetBoolean(), "UC013 empty inactive Area retains selected header and empty page");
            var branchAreas = await Read(Route(branch) + "?collection=areas", om);
            check(Items(branchAreas, "areas").All(x => x.GetProperty("floorId").ValueKind == JsonValueKind.Null), "UC013 branch-level Areas are not invented Floor children");
            var rootArea = await Read(Route(branch) + "?nodeType=area&nodeId=" + areas[2].AreaId, om);
            check(Items(rootArea, "rooms").Single().GetProperty("floorId").GetInt64() == floors[0].FloorId, "UC013 branch-level Area Room retains its required actual Floor FK");
            await Read(Route(branch) + "?nodeType=floor&nodeId=" + floors[2].FloorId + "&collection=rooms", om);
            foreach (var record in new[] { ("floor", foreignFloor.FloorId), ("area", areas[4].AreaId), ("area", areas[5].AreaId), ("room", rooms[^1].RoomId), ("room", rooms[^2].RoomId), ("room", rooms[^3].RoomId) })
                await Denied(Route(branch) + $"?nodeType={record.Item1}&nodeId={record.Item2}", admin, 404, "foreign/inconsistent node fails closed");
            var selectedRoom = await Read(Route(branch) + "?nodeType=room&nodeId=" + rooms[0].RoomId, om);
            check(selectedRoom.GetProperty("selectedRoom").GetProperty("areaId").GetInt64() == areas[0].AreaId
                && selectedRoom.GetProperty("rooms").ValueKind == JsonValueKind.Null, "UC013 Room leaf details do not invent child lists");
            await Read(Route(branch) + "?collection=departments", admin);
            await Read(Route(branch) + "?nodeType=department&nodeId=" + department.DepartmentId, admin);
            await Denied(Route(branch) + "?collection=departments", om, 403, "ROOM_VIEW cannot read administrative department collection");
            await Denied(Route(branch) + "?nodeType=department&nodeId=" + department.DepartmentId, om, 403, "ROOM_VIEW cannot read administrative department details");
            foreach (var query in new[] { "nodeType=wrong", "nodeType=floor", "nodeId=1", "nodeType=area&nodeId=0", "collection=rooms", "collection=users", "page=0", "pageSize=101", "page=2147483647&pageSize=100", "isActive=wrong" })
                await Denied(Route(branch) + "?" + query, admin, 400, "invalid query rejected");
            await Denied(Route(0), admin, 400, "invalid branch ID rejected");
            check(await Snapshot() == before && await db.UserSessions.CountAsync(ct) == sessions, "UC013 functional HTTPS phase preserves business/User/catalog/audit rows and creates no sessions");
            await Task64StructureConcurrency.RunAsync(db, open, login, identities, branch, floors[0].FloorId, Get, check, ct);
        }
        finally
        {
            await db.UserRoles.Where(x => x.UserRoleId == omGrant.UserRoleId).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(x => x.AccessId == membership.AccessId).ExecuteDeleteAsync(CancellationToken.None);
        }
    }

    private static void Fields(JsonElement element)
    {
        string[] allowed = ["branch", "id", "code", "name", "isActive", "visibility", "nodeType", "collection", "availableCollections", "selectedFloor", "selectedArea", "selectedRoom", "selectedDepartment",
            "floors", "areas", "rooms", "departments", "items", "page", "pageSize", "totalCount", "branchId", "floorId", "areaId", "floorNumber", "description", "roomNumber"];
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject())
        { if (!allowed.Contains(property.Name)) throw new InvalidOperationException("Unexpected UC013 property: " + property.Name); Fields(property.Value); }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Fields(item);
    }
}
