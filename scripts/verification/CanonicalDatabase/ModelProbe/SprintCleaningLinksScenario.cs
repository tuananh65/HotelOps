using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintCleaningLinksScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long request, string username, string password, List<object> checks, CancellationToken ct)
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { cleaningLinkCheck = label }); }
        if (!db.Database.GetDbConnection().Database.StartsWith("HotelOps_DEV3_142A_CriteriaTrial_", StringComparison.Ordinal)) throw new Exception("Owned clone required");
        var source = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request, ct);
        var branch = source.BranchId; var now = DateTime.UtcNow;
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
        var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == "CLEANING_VIEW", ct);
        var grant = await db.RolePermissions.SingleOrDefaultAsync(g => g.RoleId == role.RoleId && g.PermissionId == permission.PermissionId, ct);
        if (grant == null) { grant = new() { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now }; db.RolePermissions.Add(grant); }
        else grant.DataScope = "BRANCH";
        var type = new RoomType { TypeCode = "D3LINK" + Guid.NewGuid().ToString("N")[..8], TypeName = "Synthetic link type", IsActive = true, StandardCapacity = 1 };
        var floor = new Floor { BranchId = branch, FloorCode = "LINK", FloorName = "Synthetic link floor", IsActive = true };
        var otherBranch = await db.Branches.Where(b => b.BranchId != branch).Select(b => b.BranchId).SingleAsync(ct);
        var otherFloor = new Floor { BranchId = otherBranch, FloorCode = "LINK", FloorName = "Foreign link floor", IsActive = true };
        db.AddRange(type, floor, otherFloor); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var cleaningType = await db.ReferenceValues.Where(r => r.GroupCode == "CLEANING_TYPE" && r.IsActive).Select(r => r.ReferenceId).FirstOrDefaultAsync(ct);
        if (cleaningType == 0)
        {
            var reference = new ReferenceValue { GroupCode = "CLEANING_TYPE", ValueCode = "D3LINK" + Guid.NewGuid().ToString("N")[..8], DisplayName = "Synthetic cleaning type", IsActive = true, SortOrder = 0 };
            db.ReferenceValues.Add(reference); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); cleaningType = reference.ReferenceId;
        }
        var tasks = new List<CleaningTask>();
        for (var i = 0; i < 4; i++)
        {
            var room = new Room { BranchId = i == 3 ? otherBranch : branch, FloorId = i == 3 ? otherFloor.FloorId : floor.FloorId,
                RoomTypeId = type.RoomTypeId, RoomNumber = "LINK" + i, OperationalStatus = "DIRTY", IsActive = true };
            db.Rooms.Add(room); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var task = new CleaningTask { RoomId = room.RoomId, CleaningTypeId = cleaningType, TaskNo = "LINK-" + Guid.NewGuid().ToString("N"),
                Instruction = "Synthetic cleaning link " + i, Status = "PENDING", CreatedBy = actor, CreatedAt = now };
            db.CleaningTasks.Add(task); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); tasks.Add(task);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_ASSIGNMENT' WHERE cleaning_task_id={task.CleaningTaskId}", ct);
        }
        var path = $"/api/internal-requests/{request}/linked-cleaning";
        async Task<JsonElement> Read(bool candidates = false) => await http.GetFromJsonAsync<JsonElement>(path + (candidates ? "?candidates=true" : ""), ct);
        async Task<int> Link(long task, string version)
        {
            var csrf = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
            using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { taskId = task, mandatory = true }) };
            message.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("csrfToken").GetString()); message.Headers.TryAddWithoutValidation("If-Match", '"' + version + '"');
            using var response = await http.SendAsync(message, ct); return (int)response.StatusCode;
        }
        var view = await Read(true); var version = view.GetProperty("version").GetString()!;
        Check(view.GetProperty("total").GetInt32() == 3, "candidate list excludes foreign branch");
        Check(await Link(tasks[3].CleaningTaskId, version) == 404, "foreign cleaning task rejected");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='COMPLETED',workflow_status='COMPLETED' WHERE cleaning_task_id={tasks[0].CleaningTaskId}", ct);
        Check(await Link(tasks[0].CleaningTaskId, version) == 409, "completed cleaning task rejected");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='PENDING',workflow_status='PENDING_ASSIGNMENT' WHERE cleaning_task_id={tasks[0].CleaningTaskId}", ct);
        await db.RolePermissions.Where(g => g.RolePermissionId == grant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct);
        Check((await Read(true)).GetProperty("total").GetInt32() == 0 && await Link(tasks[0].CleaningTaskId, version) == 404, "source processing authority does not bypass target OWN scope");
        await db.RolePermissions.Where(g => g.RolePermissionId == grant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "BRANCH"), ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND department_id={source.ReceivingDepartmentId}", ct);
        Check(await Link(tasks[0].CleaningTaskId, version) == 403, "missing current receiving department mapping denies link");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND department_id={source.ReceivingDepartmentId}", ct);
        await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER dbo.TR_DEV3_CleaningLinkFault ON dbo.internal_request_cleaning_links AFTER INSERT AS THROW 51901,'Synthetic cleaning link failure',1;", ct);
        var beforeAudit = await db.AuditLogs.CountAsync(ct);
        try { Check(await Link(tasks[0].CleaningTaskId, version) >= 500, "injected link failure rejects mutation"); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_CleaningLinkFault", CancellationToken.None); }
        Check((await Read()).GetProperty("version").GetString() == version && await db.AuditLogs.CountAsync(ct) == beforeAudit,
            "link failure rolls back audit and request revision");
        var race = await Task.WhenAll(Link(tasks[0].CleaningTaskId, version), Link(tasks[1].CleaningTaskId, version));
        Check(race.Order().SequenceEqual(new[] { 200, 412 }), "concurrent cleaning links commit once and reject stale contender");
        var linked = (await Read()).GetProperty("items")[0].GetProperty("taskId").GetInt64();
        Check(await Link(linked, (await Read()).GetProperty("version").GetString()!) == 409, "duplicate cleaning link rejected");
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintCleaningLinkBrowser.mjs");
        using var browser = Process.Start(start) ?? throw new Exception("Cleaning link browser unavailable");
        var output = browser.StandardOutput.ReadToEndAsync(ct); var errors = browser.StandardError.ReadToEndAsync(ct);
        try {
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, request, task = tasks[2].CleaningTaskId })); browser.StandardInput.Close();
            await browser.WaitForExitAsync(ct); var proof = JsonSerializer.Deserialize<JsonElement>(await output); _ = await errors;
            if (browser.ExitCode != 0 || !proof.GetProperty("success").GetBoolean()) throw new Exception("Cleaning link browser failed at " + proof.GetProperty("stage").GetString());
            checks.Add(new { cleaningLinkBrowser = proof });
        } finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_cleaning_links l JOIN dbo.audit_logs a ON a.audit_id=l.audit_id WHERE l.internal_request_id={request} AND l.cleaning_task_id={tasks[2].CleaningTaskId} AND l.is_mandatory=0 AND a.actor_user_id={actor}").SingleAsync(ct) == 1,
            "browser optional cleaning link and audit persist atomically");
        Check(await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.Status == "IN_PROGRESS" && r.ClosedAt == null, ct), "cleaning link does not close request");
    }
}
