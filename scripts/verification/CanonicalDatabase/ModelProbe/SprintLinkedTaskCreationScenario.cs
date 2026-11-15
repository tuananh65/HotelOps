using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintLinkedTaskCreationScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long request, List<object> checks, CancellationToken ct, string username, string password)
    {
        // Entry is exclusively through SprintAuthProbe's validated clone receipt.
        // Immutable criteria/definitions/audit remain until verified whole-clone drop.
        if (!db.Database.GetDbConnection().Database.StartsWith("HotelOps_DEV3_142A_CriteriaTrial_", StringComparison.Ordinal))
            throw new Exception("Owned clone required for immutable task creation");
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { linkedCreationCheck = label }); }
        var row = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request, ct);
        var branch = row.BranchId; var department = row.ReceivingDepartmentId!.Value;
        var now = DateTime.UtcNow;
        var createPermission = await db.Permissions.SingleAsync(p => p.PermissionCode == "IR_CREATE", ct);
        var viewPermission = await db.Permissions.SingleOrDefaultAsync(p => p.PermissionCode == "IR_VIEW", ct);
        if (viewPermission == null)
        {
            viewPermission = new Permission { PermissionCode = "IR_VIEW", PermissionName = "View internal request history", ModuleCode = createPermission.ModuleCode,
                ResourceCode = createPermission.ResourceCode, ActionCode = "VIEW", Description = "UC123 read only; no intake or processing authority." };
            db.Permissions.Add(viewPermission); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        Check(viewPermission.ModuleCode == createPermission.ModuleCode && viewPermission.ResourceCode == createPermission.ResourceCode && viewPermission.ActionCode == "VIEW", "approved IR_VIEW catalog contract preserved on clone");
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
        foreach (var code in new[] { "IR_VIEW", "TASK_VIEW", "TASK_MANAGE" })
        {
            var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == code, ct);
            var grant = await db.RolePermissions.SingleOrDefaultAsync(g => g.RoleId == role.RoleId && g.PermissionId == permission.PermissionId, ct);
            if (grant == null) db.RolePermissions.Add(new() { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now });
            else Check(grant.DataScope is "BRANCH" or "ALL", "existing " + code + " scope compatible");
        }
        var category = new WorkCategory { CategoryCode = "D3LINK" + Guid.NewGuid().ToString("N")[..8], CategoryName = "Synthetic operational work", IsActive = true };
        db.WorkCategories.Add(category); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_category_processes(category_id,process_code) VALUES({category.CategoryId},'OPERATIONAL')", ct);
        var priority = await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(r => r.GroupCode == "PRIORITY" && r.ValueCode == "NORMAL", ct);
        if (priority == null)
        {
            priority = new ReferenceValue { GroupCode = "PRIORITY", ValueCode = "NORMAL", DisplayName = "Synthetic normal priority", IsActive = true, SortOrder = 1 };
            db.ReferenceValues.Add(priority); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        Check(priority.IsActive, "approved NORMAL priority active; no existing catalog entry overwritten");
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.PriorityId, priority.ReferenceId).SetProperty(r => r.DueAt, now.AddDays(1)), ct);
        var root = $"/api/branches/{branch}/operational-tasks";
        async Task<(int Status, JsonElement Body)> Send(HttpMethod method, string path, object? body = null, string? version = null, bool csrf = true)
        {
            using var message = new HttpRequestMessage(method, path);
            if (body != null) message.Content = JsonContent.Create(body);
            if (version != null) message.Headers.TryAddWithoutValidation("If-Match", '"' + version + '"');
            if (method != HttpMethod.Get && csrf)
            {
                var token = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
                message.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
            }
            using var response = await http.SendAsync(message, ct);
            JsonElement data = default;
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json") == true) data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return ((int)response.StatusCode, data);
        }
        async Task<TaskRequestContext> Context()
        {
            var response = await Send(HttpMethod.Get, root + "/request-context/" + request);
            Check(response.Status == 200, "public source prefill authorized");
            return response.Body.Deserialize<TaskRequestContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        CreateOperationalTaskRequest Input(TaskRequestContext source, bool mandatory = true) => new(Guid.NewGuid(), source.DepartmentId, category.CategoryId, source.Title,
            source.Description, "Synthetic completed outcome", [new ProgressDefinition("Outcome confirmed", true)], priority.ReferenceId,
            null, source.DueAt, 30, source.RoomId, source.AreaId, request, new(source.Version, mandatory));
        var context = await Context(); var input = Input(context);
        Check(context.BranchId == branch && context.DepartmentId == department && context.Title == row.Subject && context.Description == row.Description && context.PriorityId == priority.ReferenceId, "prefill comes from authorized source request");
        Check((await Send(HttpMethod.Post, root, input, context.Version, false)).Status == 400 && (await Send(HttpMethod.Post, root, input)).Status == 428, "linked create retains CSRF and source If-Match");
        Check((await Send(HttpMethod.Post, root, input with { RequestLink = null }, context.Version)).Status == 400, "linked create cannot omit completion metadata");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={department}", ct);
        Check((await Send(HttpMethod.Post, root, input, context.Version)).Status == 403 && (await Send(HttpMethod.Get, root + "/request-context/" + request)).Status == 403, "target task management cannot bypass missing source department mapping");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch} AND department_id={department}", ct);
        var manage = await db.RolePermissions.AsNoTracking().SingleAsync(g => g.RoleId == role.RoleId && g.Permission.PermissionCode == "TASK_MANAGE", ct);
        await db.RolePermissions.Where(g => g.RolePermissionId == manage.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct);
        Check((await Send(HttpMethod.Post, root, input, context.Version)).Status == 403, "source IR_PROCESS cannot bypass target TASK_MANAGE");
        await db.RolePermissions.Where(g => g.RolePermissionId == manage.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, manage.DataScope), ct);
        var otherBranch = await db.Branches.Where(b => b.BranchId != branch).Select(b => b.BranchId).SingleAsync(ct);
        Check((await Send(HttpMethod.Post, $"/api/branches/{otherBranch}/operational-tasks", input, context.Version)).Status == 403, "cross branch linked creation denied");
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAt, DateTime.UtcNow), ct);
        Check((await Send(HttpMethod.Post, root, input, context.Version)).Status == 412 && !await db.OperationalTasks.AnyAsync(ct), "stale source version creates no task");
        context = await Context(); input = Input(context);
        if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.triggers WHERE name='TR_DEV3_LinkedCreateFault'").SingleAsync(ct) != 0) throw new Exception("Preserve existing fault trigger");
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_LinkedCreateFault ON dbo.internal_request_task_links AFTER INSERT AS THROW 51900,'Synthetic linked creation fault',1;", ct);
            Check((await Send(HttpMethod.Post, root, input, context.Version)).Status >= 500, "injected link failure denies public creation");
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_LinkedCreateFault", CancellationToken.None); }
        Check(!await db.OperationalTasks.AnyAsync(ct) && !await db.AuditLogs.AnyAsync(a => a.EntityType == "operational_tasks" || a.EntityType == "internal_requests", ct)
            && (await Context()).Version == context.Version, "task definition criteria link audit and source revision roll back together");
        var created = await Send(HttpMethod.Post, root, input, context.Version);
        Check(created.Status == 201, "public linked task creation succeeds"); var task = created.Body.GetProperty("taskId").GetInt64();
        var retry = await Send(HttpMethod.Post, root, input, context.Version);
        Check(retry.Status == 201 && retry.Body.GetProperty("taskId").GetInt64() == task && await db.OperationalTasks.CountAsync(ct) == 1, "same-key retry returns original committed task despite advanced source version");
        Check((await Send(HttpMethod.Post, root, input with { Title = "Different definition" }, context.Version)).Status == 409, "same key changed definition cannot replace task or link");
        Check(await db.OperationalTasks.AnyAsync(t => t.TaskId == task && t.SourceType == "INTERNAL_REQUEST" && t.SourceId == request && t.Status == "TODO", ct), "created task retains explicit source provenance and pending physical state");
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_task_links l JOIN dbo.audit_logs a ON a.audit_id=l.audit_id WHERE l.internal_request_id={request} AND l.task_id={task} AND l.is_mandatory=1 AND a.actor_user_id={actor}").SingleAsync(ct) == 1, "created mandatory link and audit committed exactly once");
        context = await Context(); var first = Input(context, false); var second = Input(context, false);
        var race = await Task.WhenAll(Send(HttpMethod.Post, root, first, context.Version), Send(HttpMethod.Post, root, second, context.Version));
        Check(race.Select(r => r.Status).Order().SequenceEqual(new[] { 201, 412 }) && await db.OperationalTasks.CountAsync(ct) == 2, "concurrent different keys on one source version commit only one task");
        var nextDepartment = await db.Departments.Where(d => d.BranchId == branch && d.DepartmentId != department).Select(d => d.DepartmentId).SingleAsync(ct);
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.ReceivingDepartmentId, nextDepartment), ct);
        Check((await Send(HttpMethod.Post, root, input, input.RequestLink!.Version)).Status == 403, "idempotent replay rechecks current receiving department after route");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({actor},{branch},{nextDepartment},SYSUTCDATETIME())", ct);
        context = await Context();
        // Legacy provenance is visible without inventing Required/Optional classification.
        var legacyIds = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var targetBranch = i == 1 ? otherBranch : branch;
            var targetDepartment = await db.Departments.Where(d => d.BranchId == targetBranch).Select(d => d.DepartmentId).FirstAsync(ct);
            var legacy = new OperationalTask { TaskCode = "D3LEGACY" + Guid.NewGuid().ToString("N")[..10], Title = "Synthetic historical task",
                BranchId = targetBranch, DepartmentId = targetDepartment, SourceType = i == 2 ? "MAINTENANCE" : "INTERNAL_REQUEST",
                SourceId = request, Status = "COMPLETED", CompletedAt = now, CreatedAt = now, CreatedBy = actor };
            db.OperationalTasks.Add(legacy); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); legacyIds.Add(legacy.TaskId);
        }
        var linkPath = $"/api/internal-requests/{request}/linked-tasks";
        var legacyView = await http.GetFromJsonAsync<JsonElement>(linkPath, ct);
        var legacyRows = legacyView.GetProperty("items").EnumerateArray().ToArray();
        Check(legacyView.GetProperty("total").GetInt32() == 3 && legacyRows.Count(x => x.GetProperty("taskId").GetInt64() == task) == 1,
            "source and explicit link overlap returns one row with correct total");
        Check(legacyRows.Single(x => x.GetProperty("taskId").GetInt64() == legacyIds[0]).GetProperty("mandatory").ValueKind == JsonValueKind.Null
            && !legacyRows.Any(x => legacyIds.Skip(1).Contains(x.GetProperty("taskId").GetInt64())),
            "legacy completed task is unclassified; foreign branch and wrong source type excluded");
        var taskViewGrant = await db.RolePermissions.AsNoTracking().SingleAsync(g => g.RoleId == role.RoleId && g.Permission.PermissionCode == "TASK_VIEW", ct);
        try
        {
            await db.RolePermissions.Where(g => g.RolePermissionId == taskViewGrant.RolePermissionId).ExecuteUpdateAsync(u => u.SetProperty(g => g.DataScope, "OWN"), ct);
            var hidden = await http.GetFromJsonAsync<JsonElement>(linkPath, ct);
            Check(hidden.GetProperty("total").GetInt32() == 0 && hidden.GetProperty("items").GetArrayLength() == 0,
                "IR_VIEW does not expose legacy or explicit tasks after independent TASK_VIEW scope revocation");
        }
        finally { await db.RolePermissions.Where(g => g.RolePermissionId == taskViewGrant.RolePermissionId).ExecuteUpdateAsync(u => u.SetProperty(g => g.DataScope, taskViewGrant.DataScope), CancellationToken.None); }
        // Synthetic historical metadata on the already identity-verified whole clone.
        // No file payload/download claim: this checks the public evidence-list query.
        var evidenceEvent = new InternalRequestEvent { InternalRequestId = request, EventType = "PROGRESS", OldStatus = "IN_PROGRESS", NewStatus = "IN_PROGRESS", ActorId = actor, OccurredAt = now };
        db.InternalRequestEvents.Add(evidenceEvent); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        for (var i = 0; i < 101; i++) db.Attachments.Add(new Attachment {
            EntityType = "INTERNAL_REQUEST_PROGRESS", EntityId = evidenceEvent.EventId,
            FileName = "synthetic.png", OriginalFileName = $"historical-{i}.png", MimeType = "image/png", FileSizeBytes = 8,
            StorageKey = Guid.NewGuid().ToString("N") + ".png", UploadedBy = actor, UploadedAt = now });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintLinkedTaskCreationBrowser.mjs");
        using var browser = Process.Start(start) ?? throw new Exception("Linked creation browser unavailable");
        var output = browser.StandardOutput.ReadToEndAsync(ct); var errors = browser.StandardError.ReadToEndAsync(ct);
        try
        {
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, request, branch, category = category.CategoryId, context.Title, context.Description, context.DepartmentId, context.PriorityId })); browser.StandardInput.Close();
            await browser.WaitForExitAsync(ct); var proof = JsonSerializer.Deserialize<JsonElement>(await output); _ = await errors;
            if (browser.ExitCode != 0 || !proof.GetProperty("success").GetBoolean()) throw new Exception("Linked task browser failed at " + proof.GetProperty("stage").GetString());
            checks.Add(new { linkedCreationBrowser = proof });
            var savedDeadline = DateTimeOffset.Parse(proof.GetProperty("deadline").GetString()!, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime;
            Check(await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.DueAt == savedDeadline && r.Status == "IN_PROGRESS", ct)
                && await db.AuditLogs.AnyAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString()
                    && a.NewValues != null && a.NewValues.Contains("Synthetic revised completion forecast"), ct), "manager deadline change and reason persisted with audit through public UI");
            var browserTask = proof.GetProperty("task").GetInt64();
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_task_links l JOIN dbo.operational_tasks t ON t.task_id=l.task_id WHERE l.internal_request_id={request} AND l.task_id={browserTask} AND l.is_mandatory=1 AND t.source_type='INTERNAL_REQUEST' AND t.source_id={request}").SingleAsync(ct) == 1,
                "browser created task and required link persist in SQL");
            var optionalTask = proof.GetProperty("optionalTask").GetInt64();
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_task_links l JOIN dbo.operational_tasks t ON t.task_id=l.task_id WHERE l.internal_request_id={request} AND l.task_id={optionalTask} AND l.is_mandatory=0 AND t.source_type='INTERNAL_REQUEST' AND t.source_id={request}").SingleAsync(ct) == 1,
                "Tasks source selector creates optional request link without inferring mandatory status");
        }
        finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
        Check(await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.Status == "IN_PROGRESS" && r.ClosedAt == null, ct), "linked creation never closes the request");
        var evidencePath = $"/api/internal-requests/{request}/progress";
        var latestEvidence = await http.GetFromJsonAsync<JsonElement>(evidencePath, ct);
        var latestRows = latestEvidence.GetProperty("evidence").EnumerateArray().ToArray();
        Check(latestRows.Length == 100 && latestEvidence.GetProperty("evidenceCount").GetInt32() == 101, "public evidence first page is bounded with exact scoped total");
        var cursor = latestRows[^1].GetProperty("id").GetInt64();
        var olderEvidence = await http.GetFromJsonAsync<JsonElement>($"{evidencePath}?evidenceBefore={cursor}", ct);
        var olderRows = olderEvidence.GetProperty("evidence").EnumerateArray().ToArray();
        Check(olderRows.Length == 1 && olderRows[0].GetProperty("id").GetInt64() < cursor
            && olderEvidence.GetProperty("version").GetString() == latestEvidence.GetProperty("version").GetString(), "public evidence cursor retrieves historical file without duplicate or version mutation");
        using var invalidCursor = await http.GetAsync($"{evidencePath}?evidenceBefore=0", ct);
        Check((int)invalidCursor.StatusCode == 400, "invalid evidence cursor rejected");
        await SprintCleaningLinksScenario.RunAsync(db, http, actor, request, username, password, checks, ct);
        // Continue the same owned request as a real HK_STAFF handler with no manager mapping.
        var staffRole = await db.Roles.SingleAsync(r => r.RoleCode == "HK_STAFF" && r.IsActive, ct);
        foreach (var code in new[] { "IR_VIEW", "IR_PROCESS" })
        {
            var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == code, ct);
            var grant = await db.RolePermissions.SingleOrDefaultAsync(g => g.RoleId == staffRole.RoleId && g.PermissionId == permission.PermissionId, ct);
            if (grant == null) db.RolePermissions.Add(new() { RoleId = staffRole.RoleId, PermissionId = permission.PermissionId, DataScope = "OWN", GrantedAt = now });
            else Check(grant.DataScope is "OWN" or "BRANCH", "existing handler scope compatible");
        }
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.UserRoles.Where(r => r.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleId, staffRole.RoleId), ct);
        await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, nextDepartment), ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor}", ct);
        db.InternalRequestAssignments.Add(new() { InternalRequestId = request, AssigneeId = actor, AssignedBy = actor, AssignedAt = now, Status = "ACTIVE" });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var handlerStart = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        handlerStart.ArgumentList.Add("frontend/scripts/sprintInternalRequestHandlerBrowser.mjs");
        using var handlerBrowser = Process.Start(handlerStart) ?? throw new Exception("Handler browser unavailable");
        var handlerOutput = handlerBrowser.StandardOutput.ReadToEndAsync(ct); var handlerErrors = handlerBrowser.StandardError.ReadToEndAsync(ct);
        try
        {
            await handlerBrowser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, request })); handlerBrowser.StandardInput.Close();
            await handlerBrowser.WaitForExitAsync(ct); var handlerProof = JsonSerializer.Deserialize<JsonElement>(await handlerOutput); _ = await handlerErrors;
            if (handlerBrowser.ExitCode != 0 || !handlerProof.GetProperty("success").GetBoolean()) throw new Exception("Handler browser failed at " + handlerProof.GetProperty("stage").GetString());
            checks.Add(new { handlerBrowser = handlerProof });
        }
        finally { if (!handlerBrowser.HasExited) handlerBrowser.Kill(entireProcessTree: true); }
        Check(await db.Database.SqlQuery<int>($"SELECT progress_percent AS Value FROM dbo.internal_request_progress WHERE internal_request_id={request} AND result_note='Synthetic handler result'").SingleAsync(ct) == 75
            && await db.AuditLogs.AnyAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString() && a.NewValues != null && a.NewValues.Contains("Synthetic handler result"), ct), "public handler progress result and audit persisted");
        await db.InternalRequestAssignments.Where(a => a.InternalRequestId == request && a.Status == "ACTIVE")
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "REASSIGNED").SetProperty(a => a.EndedAt, DateTime.UtcNow), ct);
        var endedView = await http.GetFromJsonAsync<JsonElement>(evidencePath, ct);
        Check(!endedView.GetProperty("canUpdate").GetBoolean(), "former handler has no current update action");
        Check((await Send(HttpMethod.Post, evidencePath, new { percent = 80, note = "Former handler" }, endedView.GetProperty("version").GetString())).Status == 403,
            "former handler public mutation denied despite retained request read access");
    }
}
