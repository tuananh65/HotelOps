using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task72ChecklistCases
{
    internal static List<string> Receipts { get; } = [];
    internal static async Task Deploy(HotelOpsDbContext db, string stage, bool expected, Action<bool,string> check, CancellationToken ct)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
        var receipt = Path.Combine(root, "obj", "task72-checklist-" + stage + "-" + Guid.NewGuid().ToString("N") + ".local.json");
        var start = new ProcessStartInfo("powershell") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(root, "scripts/database/release/checklists/Run-Checklists.ps1"), "-Stage", stage, "-Database", db.Database.GetDbConnection().Database, "-Receipt", receipt }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct); await File.WriteAllTextAsync(receipt + ".log", await stdout + await stderr, ct);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(receipt, ct));
        Receipts.Add(Path.GetRelativePath(root, receipt).Replace('\\', '/'));
        check((process.ExitCode == 0 && json.RootElement.GetProperty("success").GetBoolean()) == expected, "actual checklist deployment " + stage + " expected success=" + expected + " receipt=" + Path.GetFileName(receipt));
    }

    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open, HttpClient http,
        Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> users, long branch, long foreign, Action<bool,string> check, CancellationToken ct)
    {
        if (await RoomTypeSchema.ReadAsync(db, ct) == RoomTypeContract.Legacy) await Task69DeploymentCases.DeployAsync(db, "Apply", true, check, ct);
        if (await ChecklistSchema.StateAsync(db, ct) == 1) await Deploy(db, "Rollback", true, check, ct);
        check(await ChecklistSchema.StateAsync(db, ct) == 0, "checklist upgrade trial begins on compatible legacy schema");
        var actor = users["viewer"].UserId;
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "HK_SUPERVISOR", ct);
        var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == "CHECKLIST_MANAGE", ct);
        db.Add(new UserRole { UserId = actor, RoleId = role.RoleId, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var supervisor = await login("viewer"); var admin = await login("admin"); var denied = await login("denied");
        var anonymous = new CookieJar();
        using (var response = await http.GetAsync("/api/auth/csrf", ct))
        {
            anonymous.Read(response); using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); anonymous.Token = body.RootElement.GetProperty("csrfToken").GetString();
        }
        async Task<(JsonElement Body, string? Tag)> Send(HttpMethod method, string path, CookieJar? jar, int expected, object? body = null, string? tag = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (jar != null) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            if (csrf && jar?.Token != null) request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct); var text = await response.Content.ReadAsStringAsync(ct);
            check((int)response.StatusCode == expected, method + " " + path + " expected=" + expected + " actual=" + (int)response.StatusCode + (expected != (int)response.StatusCode ? " " + text : ""));
            check(!text.Contains("SqlException") && !text.Contains("stackTrace", StringComparison.OrdinalIgnoreCase) && !text.Contains("passwordHash", StringComparison.OrdinalIgnoreCase), "checklist HTTP conceals provider/credential details");
            if (expected is 200 or 201) check(response.Headers.CacheControl?.NoStore == true && (!path.Contains("inspection-checklists") || response.Headers.ETag != null), "success no-store; checklist response has strong revision ETag");
            using var json = JsonDocument.Parse(text); return (json.RootElement.Clone(), response.Headers.ETag?.Tag);
        }
        var path = $"/api/branches/{branch}/inspection-checklists";
        await Send(HttpMethod.Get, path + "/1", supervisor, 403);
        check(!await db.RolePermissions.AnyAsync(p => p.RoleId == role.RoleId && p.PermissionId == permission.PermissionId, ct), "HK_SUPERVISOR does not receive implicit checklist authority");
        var grant = new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = DateTime.UtcNow };
        db.Add(grant); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await Send(HttpMethod.Get, path + "/1", supervisor, 503);
        var legacy = new InspectionChecklist { ChecklistCode = "T72_LEGACY", ChecklistName = "Preserved legacy template", IsActive = true };
        var legacyVersion = new InspectionChecklistVersion { Checklist = legacy, VersionNo = 1, Status = "PUBLISHED" };
        db.Add(new InspectionChecklistItem { Version = legacyVersion, Criterion = "Legacy criterion", IsRequired = true, IsActive = true });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER T72_ChecklistDdlFault ON DATABASE FOR CREATE_INDEX AS BEGIN IF COL_LENGTH(N'dbo.inspection_checklists',N'branch_id') IS NOT NULL THROW 51729,'Task72 post-column DDL fault',1; END", ct);
        try { await Deploy(db, "Apply", false, check, ct); check(await ChecklistSchema.StateAsync(db, ct) == 0, "mid-DDL failure atomically rolls back extension columns/bridge/FKs/marker"); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER T72_ChecklistDdlFault ON DATABASE", ct); }
        await Deploy(db, "Apply", true, check, ct); await Deploy(db, "Apply", true, check, ct); await Deploy(db, "Verify", true, check, ct);
        check(await db.ChecklistScopes.AsNoTracking().Where(c => c.ChecklistId == legacy.ChecklistId).Select(c => c.BranchId).SingleAsync(ct) == null
            && await db.InspectionChecklistItems.AnyAsync(i => i.VersionId == legacyVersion.VersionId && i.Criterion == "Legacy criterion", ct), "populated legacy upgrade preserves ownership/content without backfill");
        await Deploy(db, "Rollback", true, check, ct); check(await ChecklistSchema.StateAsync(db, ct) == 0, "empty-extension rollback restores legacy schema");
        await Deploy(db, "Apply", true, check, ct);
        await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER dbo.TR_checklist_items_immutable_v1 ON dbo.inspection_checklist_items", ct);
        try { await Deploy(db, "Verify", false, check, ct); await Send(HttpMethod.Get, path + "/1", supervisor, 503); }
        finally { await db.Database.ExecuteSqlRawAsync("ENABLE TRIGGER dbo.TR_checklist_items_immutable_v1 ON dbo.inspection_checklist_items", ct); }
        async Task<long> Type(string code, long b) => (await Send(HttpMethod.Post, "/api/room-types", admin, 201,
            new { branchId = b, code, name = code, standardCleaningMinutes = 30, defaultChecklistId = legacy.ChecklistId })).Body.GetProperty("id").GetInt64();
        var type1 = await Type("T72_TYPE1", branch); var type2 = await Type("T72_TYPE2", branch); var otherType = await Type("T72_FOREIGN", foreign);
        object[] Criteria(string text = "No dust") => [new { text, group = "Bedroom", mandatory = true, passCondition = "Clean surfaces", description = "Visible surfaces" }];
        object Create(string code, long[]? types = null, object[]? criteria = null) => new { code, name = "Room quality", roomTypeIds = types ?? [type1, type2], criteria = criteria ?? Criteria() };
        var transition = new { reason = "Reviewed inspection standard", confirmation = true };
        await Send(HttpMethod.Post, path, anonymous, 401, Create("T72_AUTH")); await Send(HttpMethod.Post, path, denied, 403, Create("T72_AUTH"));
        await Send(HttpMethod.Post, path, admin, 403, Create("T72_AUTH"));
        await Send(HttpMethod.Post, $"/api/branches/{foreign}/inspection-checklists", supervisor, 403, Create("T72_AUTH", [otherType]));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", []));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", [type1, type1]));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", [otherType]));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", [long.MaxValue]));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", criteria: []));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", criteria: Criteria(" ")));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_BAD", criteria: [new { text = "Missing explicit mandatory flag", group = "Bedroom" }]));
        await db.RoomTypes.Where(t => t.RoomTypeId == type2).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), ct);
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_INACTIVE_TYPE"));
        await db.RoomTypes.Where(t => t.RoomTypeId == type2).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, true), ct);
        await Send(HttpMethod.Post, path, supervisor, 400, Create(new string('X', 41)));
        await Send(HttpMethod.Post, path, supervisor, 400, Create("T72_CSRF"), csrf: false);
        await Fault(() => Send(HttpMethod.Post, path, supervisor, 500, Create("T72_ROLLBACK")));
        check(!await db.InspectionChecklists.AnyAsync(c => c.ChecklistCode == "T72_ROLLBACK", ct), "UC039 audit failure leaves no checklist/version/items/applicability");
        var created = await Send(HttpMethod.Post, path, supervisor, 201, Create(" t72_main "));
        var id = created.Body.GetProperty("id").GetInt64(); var route = path + "/" + id;
        check(created.Body.GetProperty("code").GetString() == "T72_MAIN" && created.Body.GetProperty("roomTypeIds").GetArrayLength() == 2
            && created.Body.GetProperty("versions")[0].GetProperty("status").GetString() == "DRAFT"
            && created.Body.GetProperty("versions")[0].GetProperty("criteria")[0].GetProperty("group").GetString() == "Bedroom", "UC039 version1 Draft/group/mandatory/applicable multi-types persisted through HTTP");
        await Send(HttpMethod.Post, path, supervisor, 409, Create("T72_MAIN"));
        await Send(HttpMethod.Get, path + "/" + long.MaxValue, supervisor, 404);
        await Send(HttpMethod.Get, path + "/" + legacy.ChecklistId, supervisor, 404);
        await Send(HttpMethod.Get, $"/api/branches/{foreign}/inspection-checklists/{id}", supervisor, 403);
        await Send(HttpMethod.Post, route + "/publish", supervisor, 428, transition);
        await Send(HttpMethod.Post, route + "/publish", anonymous, 401, transition, created.Tag);
        await Send(HttpMethod.Post, route + "/publish", denied, 403, transition, created.Tag);
        await Send(HttpMethod.Post, $"/api/branches/{foreign}/inspection-checklists/{id}/publish", supervisor, 403, transition, created.Tag);
        await Send(HttpMethod.Post, route + "/publish", supervisor, 400, transition, "*");
        await Send(HttpMethod.Post, route + "/publish", supervisor, 400, new { reason = "Confirm", confirmation = false }, created.Tag);
        await db.RoomTypes.Where(t => t.RoomTypeId == type2).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), ct);
        await Send(HttpMethod.Post, route + "/publish", supervisor, 400, transition, created.Tag);
        await db.RoomTypes.Where(t => t.RoomTypeId == type2).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, true), ct);
        await Fault(() => Send(HttpMethod.Post, route + "/publish", supervisor, 500, transition, created.Tag));
        check(await db.InspectionChecklistVersions.AnyAsync(v => v.ChecklistId == id && v.Status == "DRAFT", ct), "UC039 publish audit failure restores Draft and no partial publication");
        var published = await Send(HttpMethod.Post, route + "/publish", supervisor, 200, transition, created.Tag);
        var version1 = published.Body.GetProperty("versions")[0].GetProperty("id").GetInt64();
        var item1 = published.Body.GetProperty("versions")[0].GetProperty("criteria")[0].GetProperty("id").GetInt64();
        check(await db.InspectionChecklistVersions.AnyAsync(v => v.VersionId == version1 && v.Status == "PUBLISHED" && v.PublishedBy == actor && v.EffectiveAt <= DateTime.UtcNow, ct), "UC039 effective publication and server actor persisted");
        var second = await Send(HttpMethod.Post, path, supervisor, 201, Create("T72_OVERLAP", [type1]));
        check(second.Body.GetProperty("roomTypeIds")[0].GetInt64() == type1, "multiple checklists may apply to one same-branch RoomType");
        var secondId = second.Body.GetProperty("id").GetInt64();
        await Reject($"UPDATE dbo.inspection_checklist_versions SET status={"PUBLISHED"},effective_at={DateTime.UtcNow.AddDays(1)},published_by={actor} WHERE checklist_id={secondId}", "future-effective publication rejected by SQL");
        await db.InspectionChecklistItems.Where(i => i.Version.ChecklistId == secondId).ExecuteUpdateAsync(s => s.SetProperty(i => i.IsActive, false), ct);
        var noActive = await Send(HttpMethod.Get, path + "/" + secondId, supervisor, 200);
        await Send(HttpMethod.Post, path + "/" + secondId + "/publish", supervisor, 400, transition, noActive.Tag);
        await db.InspectionChecklistItems.Where(i => i.Version.ChecklistId == secondId).ExecuteUpdateAsync(s => s.SetProperty(i => i.IsActive, true), ct);
        await Reject($"UPDATE dbo.inspection_checklist_items SET criterion={"Tampered"} WHERE item_id={item1}", "published criteria SQL immutability");
        await Reject($"DELETE dbo.inspection_checklist_items WHERE item_id={item1}", "published criterion deletion denied");
        await Reject($"UPDATE dbo.inspection_checklist_versions SET effective_at={DateTime.UtcNow} WHERE version_id={version1}", "published metadata SQL immutability");
        await Reject($"UPDATE dbo.inspection_checklists SET branch_id={foreign} WHERE checklist_id={id}", "ownership cannot move branch");
        await Reject($"INSERT dbo.inspection_checklist_room_types(checklist_id,room_type_id) VALUES({id},{otherType})", "cross-branch applicability denied by SQL");
        var floor = new Floor { BranchId = branch, FloorCode = "T72_INSPECT", FloorName = "Trial", IsActive = true }; db.Add(floor); await db.SaveChangesAsync(ct);
        var room = new Room { BranchId = branch, FloorId = floor.FloorId, RoomTypeId = type1, RoomNumber = "T72_INSPECT", OperationalStatus = "AVAILABLE", IsActive = true }; db.Add(room); await db.SaveChangesAsync(ct);
        var assigned = new RoomInspection { InspectionNo = "T72_ASSIGNED", RoomId = room.RoomId, VersionId = version1, InspectorUserId = actor, Status = "PENDING", CreatedAt = DateTime.UtcNow };
        db.Add(assigned); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var used = await Send(HttpMethod.Get, route, supervisor, 200);
        await Send(HttpMethod.Put, path + "/" + long.MaxValue, supervisor, 404, new { criteria = Criteria(), reason = "Missing resource" }, used.Tag);
        await Send(HttpMethod.Put, $"/api/branches/{foreign}/inspection-checklists/{id}", supervisor, 403, new { criteria = Criteria(), reason = "Foreign branch" }, used.Tag);
        check(used.Body.GetProperty("versions")[0].GetProperty("usedByInspection").GetBoolean(), "UC040 edit shows actual historical inspection use");
        object Update(string text) => new { criteria = Criteria(text), reason = "Revise inspection standard" };
        var rowVersionBefore = await db.ChecklistScopes.AsNoTracking().Where(c => c.ChecklistId == id).Select(c => c.RowVersion).SingleAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklists SET checklist_name=checklist_name WHERE checklist_id={id}", ct);
        await Send(HttpMethod.Put, route, supervisor, 412, Update("Same content, changed SQL revision"), used.Tag);
        var rowVersionAfter = await db.ChecklistScopes.AsNoTracking().Where(c => c.ChecklistId == id).Select(c => c.RowVersion).SingleAsync(ct);
        check(!rowVersionBefore.SequenceEqual(rowVersionAfter), "GBR005 actual SQL rowversion catches same-content external write without relying on audit/state hash");
        used = await Send(HttpMethod.Get, route, supervisor, 200);
        await Send(HttpMethod.Put, route, denied, 403, Update("Denied"), used.Tag);
        await Send(HttpMethod.Put, route, supervisor, 400, new { criteria = Array.Empty<object>(), reason = "Invalid" }, used.Tag);
        await Send(HttpMethod.Put, route, supervisor, 428, Update("Missing tag"));
        await Send(HttpMethod.Put, route, supervisor, 412, Update("Stale"), created.Tag);
        await Fault(() => Send(HttpMethod.Put, route, supervisor, 500, Update("Faulted draft"), used.Tag));
        check(await db.InspectionChecklistVersions.CountAsync(v => v.ChecklistId == id, ct) == 1, "UC040 audit fault rolls back new Draft/items/version number");
        var draft2 = await Send(HttpMethod.Put, route, supervisor, 200, Update("New standard"), used.Tag);
        check(draft2.Body.GetProperty("versions").GetArrayLength() == 2 && draft2.Body.GetProperty("versions")[0].GetProperty("criteria")[0].GetProperty("text").GetString() == "No dust", "UC040 creates version2 Draft and preserves published criteria");
        var savedDraft = await Send(HttpMethod.Put, route, supervisor, 200, Update("Updated Draft"), draft2.Tag);
        check(savedDraft.Body.GetProperty("versions").GetArrayLength() == 2 && savedDraft.Body.GetProperty("versions")[1].GetProperty("criteria")[0].GetProperty("text").GetString() == "Updated Draft", "UC040 editing existing Draft does not create extra version");
        await Fault(() => Send(HttpMethod.Post, route + "/publish", supervisor, 500, transition, savedDraft.Tag));
        check(await db.InspectionChecklistVersions.CountAsync(v => v.ChecklistId == id && v.Status == "PUBLISHED", ct) == 1
            && await db.InspectionChecklistVersions.AnyAsync(v => v.VersionId == version1 && v.Status == "PUBLISHED", ct), "UC040 failed republish rolls back previous retirement/new publication together");
        var published2 = await Send(HttpMethod.Post, route + "/publish", supervisor, 200, transition, savedDraft.Tag);
        var version2 = published2.Body.GetProperty("versions")[1].GetProperty("id").GetInt64();
        check(await db.RoomInspections.AnyAsync(i => i.InspectionId == assigned.InspectionId && i.VersionId == version1, ct)
            && await db.InspectionChecklistVersions.AnyAsync(v => v.VersionId == version1 && v.Status == "RETIRED", ct)
            && await db.InspectionChecklistVersions.AnyAsync(v => v.VersionId == version2 && v.Status == "PUBLISHED", ct), "UC040 atomic replacement retains already-assigned version and retires only lifecycle");
        await Reject($"INSERT dbo.room_inspections(inspection_no,room_id,version_id,inspector_user_id,status,created_at) VALUES({"T72_RETIRED_NEW"},{room.RoomId},{version1},{actor},{"PENDING"},{DateTime.UtcNow})", "new inspection cannot select retired version");
        await db.RoomInspections.Where(i => i.InspectionId == assigned.InspectionId).ExecuteUpdateAsync(s => s.SetProperty(i => i.SummaryNote, "Existing assignment retained"), ct);
        check(await db.InspectionChecklistItems.AnyAsync(i => i.ItemId == item1 && i.Criterion == "No dust", ct), "existing assigned inspection can progress without rewriting historical criterion");
        var nextInspection = new RoomInspection { InspectionNo = "T72_NEW_VERSION", RoomId = room.RoomId, VersionId = version2, InspectorUserId = actor, Status = "PENDING", CreatedAt = DateTime.UtcNow };
        db.Add(nextInspection); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        check(await db.RoomInspections.AnyAsync(i => i.InspectionId == nextInspection.InspectionId && i.VersionId == version2, ct), "new inspection accepts latest effective Published version");
        var oldHistory = await Send(HttpMethod.Get, $"/api/inspection-history/{assigned.InspectionId}?branchId={branch}", supervisor, 200);
        var newHistory = await Send(HttpMethod.Get, $"/api/inspection-history/{nextInspection.InspectionId}?branchId={branch}", supervisor, 200);
        check(oldHistory.Body.GetProperty("inspection").GetProperty("versionId").GetInt64() == version1
            && oldHistory.Body.GetProperty("criteria")[0].GetProperty("criterion").GetString() == "No dust"
            && oldHistory.Body.GetProperty("criteria")[0].GetProperty("group").GetString() == "Bedroom"
            && newHistory.Body.GetProperty("criteria")[0].GetProperty("criterion").GetString() == "Updated Draft", "UC048 real HTTPS historical criteria/group remain on assigned version while new inspection uses version2");
        published2 = await Send(HttpMethod.Get, route, supervisor, 200);
        await Consumer(type1, id, 200); await Consumer(type2, id, 200); await Consumer(otherType, id, 400, foreign);
        var type3 = await Type("T72_NOT_APPLICABLE", branch); await Consumer(type3, id, 400);
        await Fault(() => Send(HttpMethod.Post, route + "/retire", supervisor, 500, transition, published2.Tag));
        await Send(HttpMethod.Post, route + "/retire", denied, 403, transition, published2.Tag);
        await Send(HttpMethod.Post, route + "/retire", supervisor, 400, new { reason = "Confirm", confirmation = false }, published2.Tag);
        check(await db.InspectionChecklistVersions.AnyAsync(v => v.VersionId == version2 && v.Status == "PUBLISHED", ct), "retirement audit fault rolls back lifecycle");
        var retired = await Send(HttpMethod.Post, route + "/retire", supervisor, 200, transition, published2.Tag);
        await Consumer(type1, id, 400);
        await Send(HttpMethod.Post, route + "/retire", supervisor, 409, transition, retired.Tag);
        await Deploy(db, "Rollback", false, check, ct); await Deploy(db, "Verify", true, check, ct);
        check(await ChecklistSchema.StateAsync(db, ct) == 1 && await db.RoomInspections.AnyAsync(i => i.InspectionId == assigned.InspectionId, ct), "populated rollback refusal preserves extension/history");
        var race = await Task.WhenAll(Send(HttpMethod.Post, path, supervisor, 201, Create("T72_RACE_ONE")), Send(HttpMethod.Post, path, supervisor, 201, Create("T72_RACE_TWO")));
        check(race.Length == 2, "parallel independent creates complete without shared namespace conflict");
        var edit = await Send(HttpMethod.Put, route, supervisor, 200, Update("Concurrent draft baseline"), retired.Tag);
        async Task<int> RaceUpdate(string text)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, route); Headers(request); request.Headers.TryAddWithoutValidation("If-Match", edit.Tag); request.Content = JsonContent.Create(Update(text));
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        var outcomes = await Task.WhenAll(RaceUpdate("First concurrent standard"), RaceUpdate("Second concurrent standard"));
        check(outcomes.Order().SequenceEqual(new[] {200,412}) && await db.InspectionChecklistVersions.CountAsync(v => v.ChecklistId == id && v.Status == "DRAFT", ct) == 1, "UC040 same-version concurrent updates commit once, stale loser412, one Draft");
        var last = await Send(HttpMethod.Get, route, supervisor, 200);
        await Send(HttpMethod.Post, route + "/publish", supervisor, 200, transition, last.Tag);
        async Task<int> DuplicateCreate()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path); Headers(request); request.Content = JsonContent.Create(Create("T72_DUPLICATE_RACE"));
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        check((await Task.WhenAll(DuplicateCreate(), DuplicateCreate())).Order().SequenceEqual(new[] {201,409})
            && await db.InspectionChecklists.CountAsync(c => c.ChecklistCode == "T72_DUPLICATE_RACE", ct) == 1, "UC039 concurrent normalized duplicate creates commit once");
        foreach (var isCreate in new[] { true, false })
        {
            var before = await Send(HttpMethod.Get, route, supervisor, 200);
            await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
            var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var held = await holder.Database.BeginTransactionAsync(ct);
            await holder.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; IF @r<0 THROW 51722,'Fixture lock unavailable',1;", ct);
            using var request = new HttpRequestMessage(isCreate ? HttpMethod.Post : HttpMethod.Put, isCreate ? path : route); Headers(request);
            if (!isCreate) request.Headers.TryAddWithoutValidation("If-Match", before.Tag);
            request.Content = JsonContent.Create(isCreate ? Create("T72_RETAINED_CREATE") : Update("Retained update"));
            var pending = http.SendAsync(request, ct); var api = await Wait(owner, pending);
            await using var revoker = open(null);
            async Task Revoke()
            {
                await using var transaction = await revoker.Database.BeginTransactionAsync(ct);
                await new UserEligibilityLock(revoker).LockAsync([actor], ct);
                await revoker.RolePermissions.Where(p => p.RolePermissionId == grant.RolePermissionId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            }
            var revoke = Revoke(); await Wait(api, revoke);
            check(!revoke.IsCompleted, "checklist retained User orders compliant grant revocation after transaction " + isCreate);
            await held.CommitAsync(ct); using var response = await pending;
            check((int)response.StatusCode == (isCreate ? 201 : 200), "checklist authoritative write commits before waiting revocation " + isCreate);
            await revoke; await Send(HttpMethod.Get, route, supervisor, 403);
            grant = new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = DateTime.UtcNow };
            db.Add(grant); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        foreach (var isCreate in new[] {true,false})
        {
            var before = await Send(HttpMethod.Get, route, supervisor, 200); var auditBefore = await db.AuditLogs.CountAsync(ct);
            await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
            var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var held = await holder.Database.BeginTransactionAsync(ct);
            await holder.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; IF @r<0 THROW 51722,'Fixture lock unavailable',1;", ct);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var request = new HttpRequestMessage(isCreate ? HttpMethod.Post : HttpMethod.Put, isCreate ? path : route); Headers(request);
            if (!isCreate) request.Headers.TryAddWithoutValidation("If-Match", before.Tag);
            request.Content = JsonContent.Create(isCreate ? Create("T72_CANCELED_CREATE") : Update("Canceled update"));
            var pending = http.SendAsync(request, stop.Token); var api = await Wait(owner, pending); stop.Cancel(); var canceled = false;
            try { using var response = await pending; } catch (OperationCanceledException) { canceled = true; }
            var released = false;
            for (var i = 0; i < 200; i++)
            {
                if (!await db.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)) { released = true; break; }
                await Task.Delay(30, ct);
            }
            check(canceled && released, "actual HTTPS checklist cancellation releases SQL transaction before blocker release " + isCreate);
            await held.RollbackAsync(ct);
            var after = await Send(HttpMethod.Get, route, supervisor, 200);
            check(after.Tag == before.Tag && await db.AuditLogs.CountAsync(ct) == auditBefore && !await db.InspectionChecklists.AnyAsync(c => c.ChecklistCode == "T72_CANCELED_CREATE", ct), "canceled checklist write has no aggregate/audit/revision mutation " + isCreate);
        }
        var publishBaseline = await Send(HttpMethod.Get, route, supervisor, 200);
        async Task<int> PublishRace()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route + "/publish"); Headers(request); request.Headers.TryAddWithoutValidation("If-Match", publishBaseline.Tag); request.Content = JsonContent.Create(transition);
            using var response = await http.SendAsync(request, ct); return (int)response.StatusCode;
        }
        var publicationRace = await Task.WhenAll(PublishRace(), PublishRace());
        check(publicationRace.Order().SequenceEqual(new[] {200,412})
            && await db.InspectionChecklistVersions.CountAsync(v => v.ChecklistId == id && v.Status == "PUBLISHED", ct) == 1, "concurrent publication commits one replacement and rejects stale retry; actual=" + string.Join(',', publicationRace));
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "inspection_checklists" && a.EntityId == id.ToString()).ToArrayAsync(ct);
        check(audits.Count(a => a.Action == "CREATE") == 1 && audits.All(a => a.ActorUserId == actor && a.BranchId == branch && !string.IsNullOrEmpty(a.ChangedFields)), "atomic audits bind actor/branch/entity and changed fields");
        await db.RolePermissions.Where(p => p.RolePermissionId == grant.RolePermissionId).ExecuteDeleteAsync(ct);
        await Send(HttpMethod.Get, route, supervisor, 403); await Send(HttpMethod.Put, route, supervisor, 403, Update("Revoked"), last.Tag);
        check(!await db.RolePermissions.AnyAsync(p => p.RoleId == role.RoleId && p.PermissionId == permission.PermissionId, ct), "trial-only explicit grant removed; deployment never implicitly grants HK_SUPERVISOR");

        void Headers(HttpRequestMessage request)
        {
            request.Headers.Add("Cookie", string.Join("; ", supervisor.Cookies.Select(x => x.Key + "=" + x.Value)));
            request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, supervisor.Token);
        }
        async Task<int> Wait(int blocker, Task pending)
        {
            for (var i = 0; i < 150 && !pending.IsCompleted; i++)
            {
                var ids = await db.Database.SqlQuery<int>($"SELECT CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={blocker} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                if (ids.Length > 0) { check(true, "checklist actual DMV wait blocker=" + blocker + " waiter=" + ids[0]); return ids[0]; }
                await Task.Delay(20, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("Checklist SQL contention was not observed.");
        }
        async Task Consumer(long typeId, long checklistId, int expected, long? b = null)
        {
            var typePath = $"/api/room-types/{typeId}?branchId={b ?? branch}";
            var read = await Send(HttpMethod.Get, typePath, admin, 200);
            await Send(HttpMethod.Put, typePath, admin, expected, new { name = "Consumer compatibility", standardCleaningMinutes = 35, defaultChecklistId = checklistId }, read.Tag);
        }
        async Task Reject(FormattableString sql, string label)
        {
            var refused = false;
            try { await db.Database.ExecuteSqlInterpolatedAsync(sql, ct); }
            catch (SqlException error) when (error.Errors.Cast<SqlError>().Any(e => e.Number == 51721)) { refused = true; }
            check(refused, label);
        }
        async Task Fault(Func<Task> operation)
        {
            var before = await db.AuditLogs.CountAsync(ct);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER T72_ChecklistAuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='inspection_checklists') THROW 51728,'Task72 disposable audit fault',1; END", ct);
            try { await operation(); check(await db.AuditLogs.CountAsync(ct) == before, "failed checklist transaction persists no audit"); }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.T72_ChecklistAuditFault", ct); }
        }
    }
}
