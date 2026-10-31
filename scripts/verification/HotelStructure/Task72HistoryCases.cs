using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HotelOps.Api.Services.Users;

internal static class Task72HistoryCases
{
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open, HttpClient http, Func<string, Task<CookieJar>> login,
        IReadOnlyDictionary<string, User> users, long branch, long foreign, string evidenceRoot, Action<bool,string> check, CancellationToken ct)
    {
        var role = await db.Roles.SingleAsync(x => x.RoleCode == "HK_SUPERVISOR", ct);
        db.Add(new UserRole { UserId = users["viewer"].UserId, RoleId = role.RoleId, BranchId = branch, IsActive = true, AssignedAt = DateTime.UtcNow.AddHours(-1) });
        var type = await db.ReferenceValues.Where(x => x.GroupCode == "CLEANING_TYPE").Select(x => x.ReferenceId).FirstAsync(ct);
        var roomType = new RoomType { TypeCode = "T72_TYPE", TypeName = "Trial type", IsActive = true };
        var floor = new Floor { BranchId = branch, FloorCode = "T72_F", FloorName = "Trial floor", IsActive = true };
        var foreignFloor = new Floor { BranchId = foreign, FloorCode = "T72_F", FloorName = "Foreign floor", IsActive = true };
        db.AddRange(roomType, floor, foreignFloor); await db.SaveChangesAsync(ct);
        var room = new Room { BranchId = branch, FloorId = floor.FloorId, RoomTypeId = roomType.RoomTypeId, RoomNumber = "T72_ROOM", OperationalStatus = "AVAILABLE", IsActive = true };
        var otherRoom = new Room { BranchId = foreign, FloorId = foreignFloor.FloorId, RoomTypeId = roomType.RoomTypeId, RoomNumber = "T72_FOREIGN", OperationalStatus = "AVAILABLE", IsActive = true };
        db.AddRange(room, otherRoom); await db.SaveChangesAsync(ct);
        var template = new InspectionChecklist { ChecklistCode = "T72_HISTORY", ChecklistName = "Historical checklist", IsActive = true };
        var version = new InspectionChecklistVersion { Checklist = template, VersionNo = 1, Status = "PUBLISHED", EffectiveAt = DateTime.UtcNow.AddDays(-5) };
        var item = new InspectionChecklistItem { Version = version, Criterion = "Historical criterion", IsRequired = true, IsActive = false, SortOrder = 1, PassCondition = "No dust" };
        var unanswered = new InspectionChecklistItem { Version = version, Criterion = "Unanswered historical criterion", IsRequired = false, IsActive = true, SortOrder = 2 };
        db.AddRange(item, unanswered);
        var now = DateTime.UtcNow.AddDays(-1);
        CleaningTask Cycle(Room r, string number, string status, DateTime at) => new() { TaskNo = number, RoomId = r.RoomId, CleaningTypeId = type, CreatedBy = users["admin"].UserId, CreatedAt = at, Status = status };
        var cycle = Cycle(room, "T72_CYCLE", "COMPLETED", now);
        var cancelled = Cycle(room, "T72_CANCELLED", "CANCELLED", now.AddHours(-2));
        var foreignCycle = Cycle(otherRoom, "T72_FOREIGN", "COMPLETED", now);
        db.AddRange(cycle, cancelled, foreignCycle); await db.SaveChangesAsync(ct);
        RoomInspection Inspection(Room r, CleaningTask t, string number, string result, DateTime at) => new() {
            InspectionNo = number, RoomId = r.RoomId, CleaningTaskId = t.CleaningTaskId, VersionId = version.VersionId,
            InspectorUserId = users["viewer"].UserId, Status = "COMPLETED", Result = result, CreatedAt = at, InspectedAt = at, SummaryNote = "Historical summary" };
        var failed = Inspection(room, cycle, "T72_FAIL", "FAIL", now.AddHours(1));
        var passed = Inspection(room, cycle, "T72_PASS", "PASS", now.AddHours(2));
        var foreignInspection = Inspection(otherRoom, foreignCycle, "T72_FOREIGN", "FAIL", now.AddHours(1));
        var corrupt = Inspection(otherRoom, cycle, "T72_MISMATCH", "PASS", now.AddHours(3));
        db.AddRange(failed, passed, foreignInspection, corrupt); await db.SaveChangesAsync(ct);
        var defect = new InspectionDefect { InspectionId = failed.InspectionId, ChecklistItemId = item.ItemId, Description = "Dust", Status = "OPEN" };
        db.AddRange(defect,
            new InspectionResult { InspectionId = failed.InspectionId, ChecklistItemId = item.ItemId, Outcome = "FAIL", Note = "Historical result", InspectedAt = now },
            new CleaningAssignment { CleaningTaskId = cycle.CleaningTaskId, AssigneeUserId = users["other"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddHours(-1), EndedAt = now.AddHours(1), AssignmentStatus = "REASSIGNED", Reason = "Shift ended" },
            new CleaningAssignment { CleaningTaskId = cycle.CleaningTaskId, AssigneeUserId = users["admin"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddHours(1), AssignmentStatus = "COMPLETED" },
            new CleaningProgressLog { CleaningTaskId = cycle.CleaningTaskId, ProgressPercent = 25, Status = "BLOCKED", Note = "Blocked by missing supplies", LoggedBy = users["other"].UserId, LoggedAt = now },
            new CleaningProgressLog { CleaningTaskId = cycle.CleaningTaskId, ProgressPercent = 100, Status = "COMPLETED", LoggedBy = users["admin"].UserId, LoggedAt = now.AddHours(1) },
            new CleaningRework { CleaningTaskId = cycle.CleaningTaskId, InspectionId = failed.InspectionId, Reason = "Remove dust", Status = "COMPLETED", RequestedAt = now.AddHours(1), CompletedAt = now.AddHours(2) });
        await db.SaveChangesAsync(ct);
        var fileBytes = Encoding.ASCII.GetBytes("%PDF-1.7\nTask72 historical evidence\n%%EOF");
        await File.WriteAllBytesAsync(Path.Combine(evidenceRoot, "history.pdf"), fileBytes, ct);
        Attachment FileRow(string entity, long id, string key, DateTime? deleted = null) => new() { EntityType = entity, EntityId = id, FileName = "history.pdf", OriginalFileName = "history.pdf", StorageKey = key,
            FileUrl = "https://private.invalid/never-expose", MimeType = "application/pdf", FileSizeBytes = fileBytes.Length, UploadedBy = users["admin"].UserId, UploadedAt = now, DeletedAt = deleted };
        var attachment = FileRow("INSPECTION", failed.InspectionId, "history.pdf");
        var foreignAttachment = FileRow("INSPECTION", foreignInspection.InspectionId, "history.pdf");
        var traversal = FileRow("INSPECTION", failed.InspectionId, "../history.pdf");
        var deleted = FileRow("INSPECTION", failed.InspectionId, "history.pdf", now);
        db.AddRange(attachment, foreignAttachment, traversal, deleted); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var jar = await login("viewer"); var denied = await login("denied"); var admin = await login("admin");
        async Task<(int Status, byte[] Bytes, JsonElement Json)> Get(string path, CookieJar? cookies, int expected, string label, bool binary = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (cookies != null) request.Headers.Add("Cookie", string.Join("; ", cookies.Cookies.Select(x => x.Key + "=" + x.Value)));
            using var response = await http.SendAsync(request, ct); var bytes = await response.Content.ReadAsByteArrayAsync(ct); var text = Encoding.UTF8.GetString(bytes);
            check((int)response.StatusCode == expected, label + " HTTP " + (int)response.StatusCode + (expected == (int)response.StatusCode ? "" : ": " + text));
            check(!text.Contains("storageKey", StringComparison.OrdinalIgnoreCase) && !text.Contains("private.invalid") && !text.Contains("SqlException") && !text.Contains("stackTrace", StringComparison.OrdinalIgnoreCase), label + " private/provider fields hidden");
            if (expected == 200) check(response.Headers.CacheControl?.NoStore == true, label + " no-store");
            if (binary) { check(response.Content.Headers.ContentType?.MediaType == "application/pdf" && response.Headers.Contains("X-Content-Type-Options"), label + " private PDF and nosniff"); return ((int)response.StatusCode, bytes, default); }
            using var json = JsonDocument.Parse(text); return ((int)response.StatusCode, bytes, json.RootElement.Clone());
        }
        var list = $"/api/inspection-history?branchId={branch}";
        var detail = $"/api/inspection-history/{failed.InspectionId}?branchId={branch}";
        var cycles = $"/api/rooms/{room.RoomId}/turnover-history?branchId={branch}";
        var cycleDetail = $"/api/rooms/{room.RoomId}/turnover-history/{cycle.CleaningTaskId}?branchId={branch}";
        var auditBefore = await db.AuditLogs.CountAsync(ct);
        var roomStatus = await db.Rooms.Where(x => x.RoomId == room.RoomId).Select(x => x.OperationalStatus).SingleAsync(ct);
        foreach (var path in new[] { list, detail, cycles, cycleDetail })
        {
            await Get(path, null, 401, "unauthenticated " + path);
            await Get(path, denied, 403, "no grant " + path);
            await Get(path, admin, 403, "SYS_ADMIN has no implicit history permission " + path);
            await Get(path.Replace($"branchId={branch}", $"branchId={foreign}"), jar, 403, "foreign explicit branch " + path);
        }
        var all = (await Get("/api/inspection-history", jar, 200, "UC048 SQL scope without branch parameter")).Json;
        check(all.GetProperty("items").EnumerateArray().All(x => x.GetProperty("branchId").GetInt64() == branch), "UC048 scoped list never leaks foreign or mismatched inspections");
        var first = (await Get(list + "&pageSize=1", jar, 200, "UC048 newest-first page")).Json;
        check(first.GetProperty("items")[0].GetProperty("id").GetInt64() == passed.InspectionId && first.GetProperty("total").GetInt32() == 2, "UC048 stable count and newest historical inspection");
        foreach (var filter in new[] { "result=FAIL", $"inspectorId={users["viewer"].UserId}", $"housekeeperId={users["other"].UserId}", "search=T72_FAIL", $"roomId={room.RoomId}", "from=" + now.ToUniversalTime().ToString("O"), "to=" + now.AddHours(4).ToUniversalTime().ToString("O") })
            await Get(list + "&" + filter, jar, 200, "UC048 filter " + filter);
        foreach (var filter in new[] { "result=UNKNOWN", "page=0", "pageSize=101", "inspectorId=0", "roomId=-1", "from=2026-10-31T00:00:00", "from=2026-10-31T00:00:00Z&to=2026-01-01T00:00:00Z" })
            await Get(list + "&" + filter, jar, 400, "UC048 invalid " + filter);
        await Get($"/api/inspection-history/{foreignInspection.InspectionId}?branchId={branch}", jar, 404, "UC048 foreign resource hidden");
        await Get($"/api/inspection-history/{corrupt.InspectionId}?branchId={branch}", jar, 404, "UC048 inconsistent source hidden");
        await Get($"/api/inspection-history/{long.MaxValue}?branchId={branch}", jar, 404, "UC048 missing inspection");
        var d = (await Get(detail, jar, 200, "UC048 persisted exact-version detail")).Json;
        var emptyInspections = (await Get(list + "&search=NO_MATCH_T72", jar, 200, "UC048 empty search")).Json;
        check(emptyInspections.GetProperty("total").GetInt32() == 0 && emptyInspections.GetProperty("items").GetArrayLength() == 0
            && emptyInspections.GetProperty("filters").GetProperty("search").GetString() == "NO_MATCH_T72", "UC048 empty result echoes applied filters");
        var onlyFailed = (await Get(list + "&result=FAIL", jar, 200, "UC048 result semantics")).Json;
        check(onlyFailed.GetProperty("total").GetInt32() == 1 && onlyFailed.GetProperty("items")[0].GetProperty("id").GetInt64() == failed.InspectionId, "UC048 result filter returns exactly matching inspection");
        check(d.GetProperty("criteria").GetArrayLength() == 2 && d.GetProperty("criteria")[0].GetProperty("criterion").GetString() == "Historical criterion"
            && d.GetProperty("criteria")[1].GetProperty("outcome").ValueKind == JsonValueKind.Null, "UC048 inactive historical criterion retained and unanswered result not fabricated");
        check(d.GetProperty("housekeepers").GetArrayLength() == 2 && d.GetProperty("defects").GetArrayLength() == 1 && d.GetProperty("reworks").GetArrayLength() == 1
            && d.GetProperty("cycleInspections").GetArrayLength() == 2, "UC048 assignments, defects, rework and reinspection chain");
        check(d.GetProperty("evidence").EnumerateArray().All(x => x.GetProperty("id").GetInt64() != deleted.AttachmentId), "UC048 deleted evidence excluded");
        var c = (await Get(cycles, jar, 200, "UC038 list")).Json;
        check(c.GetProperty("total").GetInt32() == 2 && c.GetProperty("items")[0].GetProperty("result").GetString() == "PASS", "UC038 cleaning cycles and latest consistent inspection result");
        var cd = (await Get(cycleDetail, jar, 200, "UC038 detail")).Json;
        var emptyCycles = (await Get(cycles + "&result=FAIL", jar, 200, "UC038 empty result")).Json;
        check(emptyCycles.GetProperty("total").GetInt32() == 0 && emptyCycles.GetProperty("items").GetArrayLength() == 0
            && emptyCycles.GetProperty("filters").GetProperty("result").GetString() == "FAIL", "UC038 latest result filter and empty applied filters");
        check(cd.GetProperty("progress").GetArrayLength() == 2 && cd.GetProperty("progress")[0].GetProperty("status").GetString() == "BLOCKED"
            && cd.GetProperty("inspections").GetArrayLength() == 2 && cd.GetProperty("reworks").GetArrayLength() == 1, "UC038 full chronology with blocker, linked inspections and rework");
        foreach (var filter in new[] { "result=PASS", "result=FAIL", "page=2&pageSize=1", "from=" + now.AddHours(-1).ToString("O") })
            await Get(cycles + "&" + filter, jar, 200, "UC038 filter " + filter);
        foreach (var filter in new[] { "result=UNKNOWN", "page=0", "pageSize=101", "from=invalid", "inspectorId=1", "roomId=9999" }) await Get(cycles + "&" + filter, jar, 400, "UC038 invalid " + filter);
        await Get($"/api/rooms/{room.RoomId}/turnover-history/{long.MaxValue}?branchId={branch}", jar, 404, "UC038 missing cycle");
        await Get($"/api/rooms/{otherRoom.RoomId}/turnover-history?branchId={branch}", jar, 404, "UC038 foreign room hidden");
        await Get($"/api/rooms/{long.MaxValue}/turnover-history?branchId={branch}", jar, 404, "UC038 missing room");
        foreach (var path in new[] { $"/api/inspection-history/{failed.InspectionId}/evidence/", $"/api/rooms/{room.RoomId}/turnover-history/{cycle.CleaningTaskId}/evidence/" })
        {
            var valid = path + attachment.AttachmentId + $"?branchId={branch}";
            var download = await Get(valid, jar, 200, "source-authorized evidence " + path, true);
            check(download.Bytes.SequenceEqual(fileBytes), "SQL attachment resolves exact private bytes");
            await Get(valid, null, 401, "evidence unauthenticated"); await Get(valid, denied, 403, "evidence forbidden");
            await Get(path + foreignAttachment.AttachmentId + $"?branchId={branch}", jar, 404, "unrelated source attachment denied");
            await Get(path + deleted.AttachmentId + $"?branchId={branch}", jar, 404, "deleted evidence denied");
            await Get(path + traversal.AttachmentId + $"?branchId={branch}", jar, 404, "path traversal denied");
        }
        await db.Rooms.Where(x => x.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        await Get(cycles, jar, 200, "UC038 inactive room historical access"); await Get(detail, jar, 200, "UC048 inactive room historical access");
        await db.Rooms.Where(x => x.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
        check(await db.AuditLogs.CountAsync(ct) == auditBefore && await db.Rooms.Where(x => x.RoomId == room.RoomId).Select(x => x.OperationalStatus).SingleAsync(ct) == roomStatus,
            "both read workflows persist no operational mutations or fabricated audits");
        foreach (var route in new[] { detail, cycles })
        {
            await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
            var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var transaction = await holder.Database.BeginTransactionAsync(ct);
            await holder.Rooms.Where(x => x.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Description, "Concurrent authoritative value"), ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            var pending = http.SendAsync(request, ct);
            var api = await Wait(owner, pending);
            await using var revoker = open(null);
            async Task Revoke()
            {
                await using var tx = await revoker.Database.BeginTransactionAsync(ct);
                await new UserEligibilityLock(revoker).LockAsync([users["viewer"].UserId], ct);
                await revoker.UserRoles.Where(x => x.UserId == users["viewer"].UserId && x.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
                await tx.CommitAsync(ct);
            }
            var revoke = Revoke();
            await Wait(api, revoke);
            check(!revoke.IsCompleted, "history retained actor lock orders compliant SQL permission revocation after read " + route);
            await transaction.CommitAsync(ct);
            using var response = await pending;
            check((int)response.StatusCode == 200, "history read completes consistently before waiting revocation " + route);
            await revoke;
            await Get(route, jar, 403, "next history request observes committed concurrent revocation");
            await db.UserRoles.Where(x => x.UserId == users["viewer"].UserId && x.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
        }
        foreach (var route in new[] { detail, cycles })
        {
            await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
            var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
            await using var tx = await holder.Database.BeginTransactionAsync(ct);
            await holder.Rooms.Where(x => x.RoomId == room.RoomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Description, "Uncommitted cancellation fixture"), ct);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            var pending = http.SendAsync(request, stop.Token); var api = await Wait(owner, pending);
            stop.Cancel(); var cancelledRequest = false;
            try { using var response = await pending; } catch (OperationCanceledException) { cancelledRequest = true; }
            var released = false;
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (!await db.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)) { released = true; break; }
                await Task.Delay(40, ct);
            }
            check(cancelledRequest && released, "actual HTTPS cancellation releases history SQL transaction before blocker rollback " + route);
            await tx.RollbackAsync(ct);
            await Get(route, jar, 200, "history retry after cancellation rollback");
        }
        async Task<int> Wait(int owner, Task pending)
        {
            for (var attempt = 0; attempt < 200 && !pending.IsCompleted; attempt++)
            {
                var waiter = await db.Database.SqlQuery<int>($"SELECT CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                if (waiter.Length > 0) { check(true, "real SQL DMV lock wait observed; blocker=" + owner + " waiter=" + waiter[0]); return waiter[0]; }
                await Task.Delay(40, ct);
            }
            if (pending.IsFaulted) await pending;
            throw new InvalidOperationException("Expected history SQL contention was not observed.");
        }
        await db.UserRoles.Where(x => x.UserId == users["viewer"].UserId && x.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        await Get(list, jar, 403, "UC048 SQL role revocation effective for existing cookie"); await Get(cycles, jar, 403, "UC038 SQL role revocation effective for existing cookie");
    }
}
