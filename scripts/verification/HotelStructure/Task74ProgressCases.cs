using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task74ProgressCases
{
    internal static List<string> Receipts { get; } = [];
    private static async Task Deploy(HotelOpsDbContext db, string stage, bool expected, Action<bool,string> check, CancellationToken ct)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
        var receipt = Path.Combine(root, "obj", "task74-progress-" + stage + "-" + Guid.NewGuid().ToString("N") + ".local.json");
        var start = new ProcessStartInfo("powershell") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(root, "scripts/database/release/progress/Run-Progress.ps1"), "-Stage", stage, "-Database", db.Database.GetDbConnection().Database, "-Receipt", receipt }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); await File.WriteAllTextAsync(receipt + ".log", await stdout + await stderr, ct);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(receipt, ct)); Receipts.Add(Path.GetRelativePath(root, receipt).Replace('\\', '/'));
            check((process.ExitCode == 0 && json.RootElement.GetProperty("success").GetBoolean()) == expected, "Task74 progress migration " + stage + " expected=" + expected + " " + Path.GetFileName(receipt));
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        HttpClient http, Func<string, Task<CookieJar>> login, IReadOnlyDictionary<string, User> users,
        long branch, long foreign, string root, Action<bool,string> check, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var floor = new Floor { BranchId = branch, FloorCode = "T74_F", FloorName = "Progress floor", IsActive = true };
        var template = new InspectionChecklist { ChecklistCode = "T74_CK", ChecklistName = "Inspection foundation", IsActive = true, CreatedBy = users["admin"].UserId, CreatedAt = now };
        db.AddRange(floor, template); await db.SaveChangesAsync(ct);
        var version = new InspectionChecklistVersion { ChecklistId = template.ChecklistId, VersionNo = 1, Status = "DRAFT", EffectiveAt = now.AddDays(-1) };
        db.Add(version); await db.SaveChangesAsync(ct);
        db.Add(new InspectionChecklistItem { VersionId = version.VersionId, Criterion = "Inspection criterion distinct from cleaning", IsActive = true, IsRequired = true, SortOrder = 0 });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.inspection_checklist_versions SET status={"PUBLISHED"},published_by={users["admin"].UserId} WHERE version_id={version.VersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(type_code,type_name,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES({"T74_TYPE"},{"Progress type"},{true},{branch},{25},{template.ChecklistId})", ct);
        var roomTypeId = await db.BranchRoomTypes.Where(t => t.TypeCode == "T74_TYPE" && t.BranchId == branch).Select(t => t.RoomTypeId).SingleAsync(ct);
        var room = new Room { RoomTypeId = roomTypeId, BranchId = branch, FloorId = floor.FloorId, RoomNumber = "T74_ROOM", OperationalStatus = "CLEANING", IsActive = true };
        db.Add(room); await db.SaveChangesAsync(ct);
        var cleaningType = await db.ReferenceValues.Where(r => r.GroupCode == "CLEANING_TYPE" && r.IsActive).Select(r => r.ReferenceId).FirstAsync(ct);
        var department = await db.Departments.Where(d => d.BranchId == branch && d.IsActive).Select(d => d.DepartmentId).FirstAsync(ct);
        var cleaning = new CleaningTask { TaskNo = "T74_CLEAN", RoomId = room.RoomId, CleaningTypeId = cleaningType, Status = "IN_PROGRESS", ActualStartAt = now.AddMinutes(-10), CreatedBy = users["admin"].UserId, CreatedAt = now.AddHours(-1) };
        var task = new OperationalTask { TaskCode = "T74_TASK", BranchId = branch, DepartmentId = department, Title = "Progress task", Status = "IN_PROGRESS", ActualStartAt = now.AddMinutes(-10), CreatedBy = users["admin"].UserId, CreatedAt = now.AddHours(-1) };
        db.AddRange(cleaning, task); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"IN_PROGRESS"} WHERE cleaning_task_id={cleaning.CleaningTaskId}", ct);
        var beforeLegacy = await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.cleaning_tasks WHERE cleaning_task_id={cleaning.CleaningTaskId} FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.operational_tasks WHERE task_id={task.TaskId} FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER T74_ProgressDdlFault ON DATABASE FOR CREATE_INDEX AS BEGIN IF OBJECT_ID(N'dbo.task_completion_criteria') IS NOT NULL THROW 51749,'Task74 genuine post-table DDL fault',1; END", ct);
        try { await Deploy(db, "Apply", false, check, ct); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER T74_ProgressDdlFault ON DATABASE", ct); }
        check(!await ProgressSchema.InstalledAsync(db, ct) && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name='cleaning_item_definitions'").SingleAsync(ct) == 0, "Task74 genuine DDL fault rolls back all progress tables and marker");
        await Deploy(db, "Apply", true, check, ct); await Deploy(db, "Apply", true, check, ct); await Deploy(db, "Verify", true, check, ct);
        await Deploy(db, "Rollback", true, check, ct); check(!await ProgressSchema.InstalledAsync(db, ct), "Task74 empty rollback removes module");
        await Deploy(db, "Apply", true, check, ct);
        var afterLegacy = await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.cleaning_tasks WHERE cleaning_task_id={cleaning.CleaningTaskId} FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.operational_tasks WHERE task_id={task.TaskId} FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        check(beforeLegacy == afterLegacy, "Task74 populated legacy upgrade preserves every source task field and rowversion");
        var role = await db.Roles.Where(r => r.RoleCode == "HK_STAFF").Select(r => r.RoleId).SingleAsync(ct);
        db.Add(new UserRole { UserId = users["viewer"].UserId, RoleId = role, BranchId = branch, AssignedAt = now.AddHours(-1), IsActive = true });
        // Trial fixture role assignments only: SYS_ADMIN is not a business-permission bypass.
        foreach (var roleCode in new[] { "HK_SUPERVISOR", "HK_STAFF", "DEPT_MANAGER" })
        {
            var roleId = await db.Roles.Where(r => r.RoleCode == roleCode).Select(r => r.RoleId).SingleAsync(ct);
            foreach (var scope in new[] { branch, foreign })
                db.Add(new UserRole { UserId = users["admin"].UserId, RoleId = roleId, BranchId = scope, AssignedAt = now.AddHours(-1), IsActive = true });
        }
        foreach (var scope in new[] { branch, foreign })
            db.Add(new UserBranchAccess { UserId = users["admin"].UserId, BranchId = scope, AccessLevel = "STANDARD", GrantedAt = now.AddHours(-1) });
        db.Add(new CleaningAssignment { CleaningTaskId = cleaning.CleaningTaskId, AssigneeUserId = users["viewer"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-15), AssignmentStatus = "ACTIVE" });
        db.Add(new OperationalTaskAssignment { TaskId = task.TaskId, AssigneeId = users["viewer"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-15), AssignmentStatus = "ACTIVE" });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var admin = await login("admin"); var owner = await login("viewer"); var other = await login("other"); var denied = await login("denied");
        var anonymous = new CookieJar(); using (var response = await http.GetAsync("/api/auth/csrf", ct)) { anonymous.Read(response); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); anonymous.Token = json.RootElement.GetProperty("csrfToken").GetString(); }
        async Task<(int Status, JsonElement Body, string? Tag)> Send(HttpMethod method, string path, CookieJar jar, int expected, object? body = null, string? tag = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (jar.Cookies.Count != 0) request.Headers.Add("Cookie", string.Join("; ", jar.Cookies.Select(x => x.Key + "=" + x.Value)));
            if (csrf && jar.Token != null) request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, jar.Token);
            if (body != null) request.Content = JsonContent.Create(body);
            if (tag != null) request.Headers.TryAddWithoutValidation("If-Match", tag);
            using var response = await http.SendAsync(request, ct); var text = await response.Content.ReadAsStringAsync(ct); var status = (int)response.StatusCode;
            check(expected == 0 ? status is 200 or 412 : status == expected, $"Task74 {method} {path} expected {expected}, actual {status}");
            check(!text.Contains("SqlException") && !text.Contains("stackTrace") && !text.Contains("storageKey") && !text.Contains("fileUrl"), "Task74 no provider/private storage disclosure");
            if (response.Content.Headers.ContentType?.MediaType == "application/pdf") return (status, default, response.Headers.ETag?.Tag);
            using var json = JsonDocument.Parse(text); return (status, json.RootElement.Clone(), response.Headers.ETag?.Tag);
        }
        object Definitions(string label) => new { items = new[] { new { label, required = true }, new { label = label + " second required item", required = true } }, reason = "Explicit opt-in without legacy conversion" };
        var prefix = $"/api/branches/{branch}";
        var templatePath = prefix + $"/room-types/{roomTypeId}/cleaning-items";
        await Send(HttpMethod.Post, templatePath, owner, 403, Definitions("Clean surface"));
        await Send(HttpMethod.Post, templatePath, admin, 201, Definitions("Clean surface"));
        await Send(HttpMethod.Post, templatePath, admin, 409, Definitions("Another template"));
        await Send(HttpMethod.Post, prefix + $"/cleaning-tasks/{cleaning.CleaningTaskId}/progress-initialization", admin, 201, new { reason = "Explicit per-task cleaning item adoption" });
        await Send(HttpMethod.Post, prefix + $"/operational-tasks/{task.TaskId}/progress-initialization", admin, 201, Definitions("Completion criterion"));
        await Deploy(db, "Rollback", false, check, ct); await Deploy(db, "Verify", true, check, ct);
        check(await ProgressSchema.InstalledAsync(db, ct), "Task74 populated rollback refuses data loss and retains valid module");
        var pdf = new { name = "progress.pdf", mimeType = "application/pdf", base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.7\nPrivate Task74 evidence\n%%EOF")) };
        var foreignDepartment = await db.Departments.Where(d => d.BranchId == foreign && d.IsActive).Select(d => d.DepartmentId).FirstAsync(ct);
        var foreignTask = new OperationalTask { TaskCode = "T74_FOREIGN", BranchId = foreign, DepartmentId = foreignDepartment,
            Title = "Foreign branch criteria", Status = "IN_PROGRESS", ActualStartAt = now, CreatedBy = users["admin"].UserId, CreatedAt = now };
        db.Add(foreignTask); await db.SaveChangesAsync(ct);
        db.Add(new OperationalTaskAssignment { TaskId = foreignTask.TaskId, AssigneeId = users["admin"].UserId,
            AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-1), AssignmentStatus = "ACTIVE" });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var foreignPath = $"/api/branches/{foreign}/operational-tasks/{foreignTask.TaskId}/progress";
        await Send(HttpMethod.Post, $"/api/branches/{foreign}/operational-tasks/{foreignTask.TaskId}/progress-initialization", admin, 201, Definitions("Foreign criterion"));
        var foreignRead = await Send(HttpMethod.Get, foreignPath, admin, 200);
        var foreignCriterion = foreignRead.Body.GetProperty("items")[0].GetProperty("id").GetInt64();
        var foreignSaved = await Send(HttpMethod.Put, foreignPath, admin, 200,
            new { percent = 10, items = Array.Empty<object>(), note = "Foreign evidence", photos = new[] { pdf } }, foreignRead.Tag);
        var foreignAttachment = foreignSaved.Body.GetProperty("evidence")[0].GetProperty("id").GetInt64();
        var totals = new Dictionary<string,int>();
        foreach (var cleaningKind in new[] { true, false })
        {
            var uc = cleaningKind ? "UC032" : "UC109"; var checksAtStart = 0;
            void Verify(bool ok, string label) { check(ok, uc + " " + label); checksAtStart++; }
            var id = cleaningKind ? cleaning.CleaningTaskId : task.TaskId;
            var kind = cleaningKind ? "cleaning-tasks" : "operational-tasks";
            var path = prefix + $"/{kind}/{id}/progress";
            await Send(HttpMethod.Get, path, anonymous, 401); await Send(HttpMethod.Get, path, denied, 403);
            await Send(HttpMethod.Get, path.Replace($"branches/{branch}", $"branches/{foreign}"), owner, 403);
            await Send(HttpMethod.Get, prefix + $"/{kind}/{long.MaxValue}/progress", admin, 403); // OWN cannot resolve a missing resource.
            await Send(HttpMethod.Post, prefix + $"/{kind}/{long.MaxValue}/progress-initialization", admin, 404,
                cleaningKind ? new { reason = "Explicit missing-resource initialization probe" } : Definitions("Missing task criterion"));
            await Send(HttpMethod.Get, path, other, 403); // A global master-data grant is no execution bypass.
            var read = await Send(HttpMethod.Get, path, owner, 200);
            var item = read.Body.GetProperty("items")[0].GetProperty("id").GetInt64();
            Verify(read.Body.GetProperty("items")[0].GetProperty("label").GetString() == (cleaningKind ? "Clean surface" : "Completion criterion"), "uses distinct frozen cleaning/criterion model");
            object Save(int percent = 50, long? itemId = null, object[]? photos = null) => new { percent, items = new[] { new { id = itemId ?? item, done = cleaningKind ? (bool?)true : null, outcome = cleaningKind ? null : "PASS", note = "Observed progress" } }, note = "Actual task progress", photos = photos ?? Array.Empty<object>() };
            await Send(HttpMethod.Put, path, owner, 428, Save()); await Send(HttpMethod.Put, path, owner, 400, Save(), "*");
            await Send(HttpMethod.Put, path, owner, 400, Save(-1), read.Tag); await Send(HttpMethod.Put, path, owner, 400, Save(101), read.Tag);
            await Send(HttpMethod.Put, path, owner, 400, Save(itemId: long.MaxValue), read.Tag);
            if (!cleaningKind) await Send(HttpMethod.Put, path, owner, 400, Save(itemId: foreignCriterion), read.Tag);
            await Send(HttpMethod.Put, path, owner, 400, Save(), read.Tag, false);
            await Send(HttpMethod.Put, path, owner, 400, new { percent = 10, items = Array.Empty<object>(), arbitraryUrl = "https://example.invalid/evidence" }, read.Tag);
            await Send(HttpMethod.Put, path, owner, 400, Save(photos: [new { name = "../escape.pdf", mimeType = "application/pdf", base64 = pdf.base64 }]), read.Tag);
            await Send(HttpMethod.Put, path, owner, 400, Save(photos: [new { name = "x.pdf", mimeType = "application/pdf", base64 = "not-base64" }]), read.Tag);
            var fileCount = Directory.GetFiles(root).Length; var attachmentCount = await db.Attachments.CountAsync(ct);
            var source = cleaningKind ? "cleaning_tasks" : "operational_tasks";
            var faultSql = cleaningKind
                ? "ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT T74_ProgressAuditFault CHECK(entity_type<>'cleaning_tasks' OR changed_fields NOT LIKE '%evidence%')"
                : "ALTER TABLE dbo.audit_logs WITH NOCHECK ADD CONSTRAINT T74_ProgressAuditFault CHECK(entity_type<>'operational_tasks' OR changed_fields NOT LIKE '%evidence%')";
            await db.Database.ExecuteSqlRawAsync(faultSql, ct);
            try { await Send(HttpMethod.Put, path, owner, 500, Save(photos: [pdf]), read.Tag); }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT T74_ProgressAuditFault", ct); }
            var rolledBack = await Send(HttpMethod.Get, path, owner, 200);
            Verify(rolledBack.Tag == read.Tag && rolledBack.Body.GetProperty("percent").GetInt32() == 0 && Directory.GetFiles(root).Length == fileCount
                && await db.Attachments.CountAsync(ct) == attachmentCount, "audit fault rolls back criteria/log/progress/revision/evidence and removes owned file");
            // Initial grant is valid; real SQL AFTER INSERT wait crosses its expiry before commit-time gate.
            var pauseSql = cleaningKind
                ? "CREATE TRIGGER T74_ProgressAuditPause ON dbo.cleaning_task_items AFTER UPDATE AS BEGIN SET NOCOUNT ON; WAITFOR DELAY '00:00:04'; END"
                : "CREATE TRIGGER T74_ProgressAuditPause ON dbo.task_completion_criteria AFTER UPDATE AS BEGIN SET NOCOUNT ON; WAITFOR DELAY '00:00:04'; END";
            await db.Database.ExecuteSqlRawAsync(pauseSql, ct);
            await db.UserRoles.Where(r => r.UserId == users["viewer"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, now.AddHours(1)), ct);
            try
            {
                await db.UserRoles.Where(r => r.UserId == users["viewer"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, (DateTime?)DateTime.UtcNow.AddSeconds(2)), ct);
                await Send(HttpMethod.Put, path, owner, 403, Save(photos: [pdf]), read.Tag);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.T74_ProgressAuditPause", ct);
                await db.UserRoles.Where(r => r.UserId == users["viewer"].UserId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, (DateTime?)null), ct);
            }
            var expiredRollback = await Send(HttpMethod.Get, path, owner, 200);
            Verify(expiredRollback.Tag == read.Tag && expiredRollback.Body.GetProperty("percent").GetInt32() == 0 && Directory.GetFiles(root).Length == fileCount,
                "post-write permission expiry rejects commit and rolls back progress, revision and private file");
            var saved = await Send(HttpMethod.Put, path, owner, 200, Save(100, photos: [pdf]), read.Tag);
            Verify(saved.Body.GetProperty("percent").GetInt32() == 100 && saved.Body.GetProperty("state").GetString() == "IN_PROGRESS", "100 percent does not complete task");
            Verify(cleaningKind ? !saved.Body.GetProperty("items")[1].GetProperty("done").GetBoolean()
                : saved.Body.GetProperty("items")[1].GetProperty("outcome").ValueKind == JsonValueKind.Null,
                "missing required item remains explicit; percentage never fabricates completion");
            Verify(await db.AuditLogs.AnyAsync(a => a.EntityType == source && a.EntityId == id.ToString() && a.ActorUserId == users["viewer"].UserId
                && a.BranchId == branch && a.ChangedFields!.Contains("evidence"), ct), "atomic SQL audit has source, branch and server actor");
            await Send(HttpMethod.Put, path, owner, 412, Save(), read.Tag);
            var attachment = saved.Body.GetProperty("evidence")[0].GetProperty("id").GetInt64();
            await Send(HttpMethod.Get, path + "/evidence/" + attachment, owner, 200);
            await Send(HttpMethod.Get, path + "/evidence/" + attachment, other, 403);
            await Send(HttpMethod.Get, path + "/evidence/" + long.MaxValue, owner, 404);
            await Send(HttpMethod.Get, path + "/evidence/" + foreignAttachment, owner, 404);
            if (cleaningKind) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET instruction={"Legacy instruction changed"} WHERE cleaning_task_id={id}", ct);
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET description={"Legacy description changed"} WHERE task_id={id}", ct);
            await Send(HttpMethod.Put, path, owner, 412, Save(), saved.Tag);
            var legacyUpdated = await Send(HttpMethod.Get, path, owner, 200);
            Verify(legacyUpdated.Tag != saved.Tag, "legacy parent update advances progress rowversion");
            var edits = await Task.WhenAll(Send(HttpMethod.Put, path, owner, 0, Save(70), legacyUpdated.Tag), Send(HttpMethod.Put, path, owner, 0, Save(80), legacyUpdated.Tag));
            Verify(edits.Select(e => e.Status).Order().SequenceEqual(new[] { 200, 412 }), "real concurrent HTTPS updates: one commit and one stale write");
            var immutable = false;
            try
            {
                if (cleaningKind) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_task_items SET label={"Unexpected rewrite"} WHERE item_id={item}", ct);
                else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_completion_criteria SET label={"Unexpected rewrite"} WHERE criterion_id={item}", ct);
            }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 51741) { immutable = true; }
            Verify(immutable, "SQL rejects frozen item/criterion identity rewrite");
            var cancelBefore = await Send(HttpMethod.Get, path, owner, 200);
            await using (var heldDb = open(null))
            {
                await heldDb.Database.OpenConnectionAsync(ct);
                var blocker = await heldDb.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
                await using var held = await heldDb.Database.BeginTransactionAsync(ct);
                try
                {
                    if (cleaningKind) await heldDb.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.cleaning_progress_versions WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={id}").SingleAsync(ct);
                    else await heldDb.Database.SqlQuery<long>($"SELECT task_id AS [Value] FROM dbo.task_progress_versions WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}").SingleAsync(ct);
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    using var request = new HttpRequestMessage(HttpMethod.Put, path);
                    request.Headers.Add("Cookie", string.Join("; ", owner.Cookies.Select(x => x.Key + "=" + x.Value)));
                    request.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName, owner.Token);
                    request.Headers.TryAddWithoutValidation("If-Match", cancelBefore.Tag); request.Content = JsonContent.Create(Save(25, photos: [pdf]));
                    var pending = http.SendAsync(request, stop.Token); var api = 0;
                    for (var n = 0; n < 200 && !pending.IsCompleted; n++)
                    {
                        var waiting = await db.Database.SqlQuery<int>($"SELECT CONVERT(int,session_id) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={blocker} AND database_id=DB_ID() AND wait_type LIKE 'LCK_M_%'").ToArrayAsync(ct);
                        if (waiting.Length != 0) { api = waiting[0]; break; } await Task.Delay(30, ct);
                    }
                    Verify(api > 0, "SQL DMV confirms real HTTPS wait on progress revision lock");
                    stop.Cancel(); var canceled = false; try { using var response = await pending; } catch (OperationCanceledException) { canceled = true; }
                    var released = false;
                    for (var n = 0; n < 200; n++)
                    {
                        if (!await db.Database.SqlQuery<int>($"SELECT CONVERT(int,request_session_id) AS [Value] FROM sys.dm_tran_locks WHERE request_session_id={api} AND resource_database_id=DB_ID() AND request_owner_type='TRANSACTION'").AnyAsync(ct)) { released = true; break; }
                        await Task.Delay(30, ct);
                    }
                    Verify(canceled && released, "actual HTTPS cancellation releases transaction locks before blocker rollback");
                }
                finally { await held.RollbackAsync(CancellationToken.None); }
            }
            var cancelAfter = await Send(HttpMethod.Get, path, owner, 200);
            Verify(cancelAfter.Tag == cancelBefore.Tag, "canceled save preserves SQL revision and progress");
            if (cleaningKind)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(type_code,type_name,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES({"T74_REPLACEMENT"},{"Replacement room type"},{true},{branch},{30},{template.ChecklistId})", ct);
                var replacement = await db.BranchRoomTypes.Where(t => t.TypeCode == "T74_REPLACEMENT" && t.BranchId == branch).Select(t => t.RoomTypeId).SingleAsync(ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.rooms SET room_type_id={replacement} WHERE room_id={room.RoomId}", ct);
                try
                {
                    var afterTypeChange = await Send(HttpMethod.Get, path, owner, 200);
                    var stillWritable = await Send(HttpMethod.Put, path, owner, 200, Save(85), afterTypeChange.Tag);
                    Verify(stillWritable.Body.GetProperty("items")[0].GetProperty("label").GetString() == "Clean surface",
                        "RoomType change preserves writable original cleaning snapshots without importing new definitions");
                }
                finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.rooms SET room_type_id={roomTypeId} WHERE room_id={room.RoomId}", ct); }
            }
            // Preserve existing unique-current-assignment constraints; do not disable them to manufacture ambiguity.
            if (cleaningKind) db.Add(new CleaningAssignment { CleaningTaskId = id, AssigneeUserId = users["other"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-1), AssignmentStatus = "ACTIVE" });
            else db.Add(new OperationalTaskAssignment { TaskId = id, AssigneeId = users["other"].UserId, AssignedBy = users["admin"].UserId, AssignedAt = now.AddMinutes(-1), AssignmentStatus = "ACTIVE" });
            var duplicateOwnerRejected = false;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { duplicateOwnerRejected = true; }
            finally { db.ChangeTracker.Clear(); }
            if (duplicateOwnerRejected) Verify(true, "existing SQL unique-current-assignment constraint rejects second owner");
            else await Send(HttpMethod.Get, path, owner, 403);
            if (cleaningKind) await db.CleaningAssignments.Where(a => a.CleaningTaskId == id && a.AssigneeUserId == users["other"].UserId).ExecuteDeleteAsync(ct);
            else await db.OperationalTaskAssignments.Where(a => a.TaskId == id && a.AssigneeId == users["other"].UserId).ExecuteDeleteAsync(ct);
            if (cleaningKind) await db.CleaningAssignments.Where(a => a.CleaningTaskId == id && a.AssignmentStatus == "ACTIVE").ExecuteUpdateAsync(s => s.SetProperty(a => a.AssigneeUserId, users["other"].UserId), ct);
            else await db.OperationalTaskAssignments.Where(a => a.TaskId == id && a.AssignmentStatus == "ACTIVE").ExecuteUpdateAsync(s => s.SetProperty(a => a.AssigneeId, users["other"].UserId), ct);
            await Send(HttpMethod.Get, path, owner, 403);
            if (cleaningKind) await db.CleaningAssignments.Where(a => a.CleaningTaskId == id && a.AssignmentStatus == "ACTIVE").ExecuteUpdateAsync(s => s.SetProperty(a => a.AssigneeUserId, users["viewer"].UserId), ct);
            else await db.OperationalTaskAssignments.Where(a => a.TaskId == id && a.AssignmentStatus == "ACTIVE").ExecuteUpdateAsync(s => s.SetProperty(a => a.AssigneeId, users["viewer"].UserId), ct);
            if (!cleaningKind)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status={"PAUSED"} WHERE task_id={id}", ct);
                var paused = await Send(HttpMethod.Get, path, owner, 200);
                var updated = await Send(HttpMethod.Put, path, owner, 200, Save(90), paused.Tag);
                Verify(updated.Body.GetProperty("state").GetString() == "PAUSED", "approved stored PAUSED contract accepts progress without resuming or inventing work time");
            }
            var prematureCompletion = false;
            try
            {
                if (cleaningKind) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"COMPLETED"},status={"COMPLETED"} WHERE cleaning_task_id={id}", ct);
                else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status={"COMPLETED"},completed_at={DateTime.UtcNow} WHERE task_id={id}", ct);
            }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 51741) { prematureCompletion = true; }
            Verify(prematureCompletion, "SQL rejects premature completion while mandatory items/criteria are missing");
            if (cleaningKind) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"PENDING_INSPECTION"},status={"IN_PROGRESS"} WHERE cleaning_task_id={id}", ct);
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status={"CANCELLED"} WHERE task_id={id}", ct);
            await Send(HttpMethod.Get, path, owner, 409); totals[uc] = checksAtStart;
        }
        // New evidence cannot be attached to a different progress source by raw SQL.
        var otherProgress = await db.OperationalTaskProgresses.Where(p => p.TaskId == task.TaskId).Select(p => p.ProgressId).FirstAsync(ct);
        var cleaningAttachment = await db.Attachments.Where(a => a.EntityType == "CLEANING_TASK" && a.EntityId == cleaning.CleaningTaskId).Select(a => a.AttachmentId).FirstAsync(ct);
        var rejected = false;
        try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.progress_evidence_links SET cleaning_progress_id=NULL,task_progress_id={otherProgress} WHERE attachment_id={cleaningAttachment}", ct); }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 51741) { rejected = true; }
        check(rejected, "Task74 SQL rejects cross-source evidence relink");
        // One targeted shared regression after new writes; no duplicate full historical suite.
        var inspection = new RoomInspection { InspectionNo = "T74_INSPECT", RoomId = room.RoomId, CleaningTaskId = cleaning.CleaningTaskId,
            VersionId = version.VersionId, InspectorUserId = users["admin"].UserId, Status = "PENDING", CreatedAt = DateTime.UtcNow };
        db.Add(inspection); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"ASSIGNED"} WHERE inspection_id={inspection.InspectionId}", ct);
        var inspectionPath = prefix + $"/inspections/{inspection.InspectionId}";
        var inspectionRead = await Send(HttpMethod.Get, inspectionPath + "/execution", admin, 200);
        var started = await Send(HttpMethod.Post, inspectionPath + "/start", admin, 200, new { reason = "Regression independent inspection start" }, inspectionRead.Tag);
        var criterion = started.Body.GetProperty("criteria")[0].GetProperty("itemId").GetInt64();
        var inspectionSaved = await Send(HttpMethod.Put, inspectionPath + "/results", admin, 200,
            new { reason = "Regression private inspection evidence", results = new[] { new { itemId = criterion, outcome = "PASS", photos = new[] { pdf } } } }, started.Tag);
        await Send(HttpMethod.Post, inspectionPath + "/finish", admin, 200, new { reason = "Regression inspection finish" }, inspectionSaved.Tag);
        check(await db.Attachments.AnyAsync(a => a.EntityType == "INSPECTION" && a.EntityId == inspection.InspectionId, ct), "UC042 shared evidence helper preserves existing attachment source and persists private evidence");
        await Send(HttpMethod.Get, $"/api/rooms/{room.RoomId}/turnover-history?branchId={branch}", admin, 200);
        await Send(HttpMethod.Get, $"/api/inspection-history?branchId={branch}", admin, 200);
        await Send(HttpMethod.Get, $"/api/room-types?branchId={branch}", admin, 200);
        await Send(HttpMethod.Get, $"/api/rooms?branchId={branch}", admin, 200);
        check(await db.InspectionChecklistItems.CountAsync(i => i.VersionId == version.VersionId, ct) == 1,
            "UC039/040 consumer preserves immutable published inspection criteria distinct from progress items");
    }
}
