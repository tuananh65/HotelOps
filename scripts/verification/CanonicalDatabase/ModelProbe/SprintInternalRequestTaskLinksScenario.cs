using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestTaskLinksScenario
{
    private sealed class Identity(CurrentUserInfo user) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo result) { result = user; return true; } }

    internal static async Task RunAsync(HotelOpsDbContext db, long actor, long request, List<object> checks, CancellationToken ct, string? username, string? password)
    {
        // Parent verified exact disposable identity, empty personnel and owns both branches.
        var original = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request, ct);
        var foreign = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.RequesterId == actor && r.BranchId != original.BranchId, ct);
        var credential = SessionCredential.Generate(); var now = DateTime.UtcNow;
        var session = new UserSession { UserId = actor, SessionKeyHash = SessionCredential.Hash(credential), CreatedAt = now, ExpiresAt = now.AddMinutes(20) };
        var tasks = new List<long>(); var grants = new List<long>(); var createdView = false; var installed = false; var trigger = false;
        RolePermission? targetGrantToRestore = null;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { taskLinkCheck = label }); }
        Task<byte[]> Version() => db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.internal_requests WHERE internal_request_id={request}").SingleAsync(ct);
        try
        {
            createdView = !await db.Permissions.AnyAsync(p => p.PermissionCode == "IR_VIEW", ct);
            foreach (var file in new[] { "InternalRequestDecisionIsolated.sql", "InternalRequestViewIsolated.sql", "InternalRequestTaskLinksIsolated.sql", "InternalRequestTaskLinksIsolated.sql" })
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/" + file, ct), ct);
            installed = true;
            var role = await db.Roles.SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
            foreach (var code in new[] { "IR_VIEW", "TASK_VIEW" })
            {
                var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == code, ct);
                var existing = await db.RolePermissions.AsNoTracking().SingleOrDefaultAsync(g => g.RoleId == role.RoleId && g.PermissionId == permission.PermissionId, ct);
                if (existing != null) { Check(existing.DataScope is "BRANCH" or "ALL", "existing target/read grant scope supported"); continue; }
                var grant = new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now };
                db.RolePermissions.Add(grant); await db.SaveChangesAsync(ct); grants.Add(grant.RolePermissionId); db.ChangeTracker.Clear();
            }
            db.UserSessions.Add(session); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var stored = await db.UserSessions.AsNoTracking().SingleAsync(s => s.SessionId == session.SessionId, ct);
            var ticket = SessionReference.FromProtectedTicket(actor, credential, false, DateTime.SpecifyKind(stored.CreatedAt, DateTimeKind.Utc));
            InternalRequestTaskLinks Service(HotelOpsDbContext context) => new(context, new(context), new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), new Identity(new(actor, session.SessionId, false)));
            var service = Service(db);
            for (var i = 0; i < 5; i++)
            {
                var task = new OperationalTask { TaskCode = "D3LINK" + Guid.NewGuid().ToString("N")[..12], Title = "Synthetic linked work " + i,
                    BranchId = i == 3 ? foreign.BranchId : original.BranchId, DepartmentId = (i == 3 ? foreign.ReceivingDepartmentId : original.ReceivingDepartmentId)!.Value,
                    Status = i == 4 ? "CANCELLED" : "IN_PROGRESS", CreatedBy = actor, CreatedAt = now };
                db.OperationalTasks.Add(task); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); tasks.Add(task.TaskId);
                if (i != 4) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},'IN_PROGRESS')", ct);
            }
            async Task Denied(long task, int status, string label, byte[]? version = null)
            {
                var denied = false;
                try { await service.LinkAsync(ticket, session.SessionId, request, version ?? await Version(), new(task, true), ct); }
                catch (InternalRequestReadException e) when (e.Status == status) { denied = true; }
                Check(denied, label);
            }
            var candidates = await service.ReadAsync(ticket, session.SessionId, request, true, 1, ct);
            Check(candidates.Items.Select(t => t.TaskId).Order().SequenceEqual(tasks.Take(3).Order()), "candidates include only visible same-branch open workflow tasks");
            await Denied(tasks[3], 404, "cross branch target denied"); await Denied(tasks[4], 409, "cancelled target denied");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}", ct);
            await Denied(tasks[0], 403, "IR_VIEW and IR_PROCESS without current department mapping deny linking");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}", ct);
            var targetGrant = await db.RolePermissions.AsNoTracking().SingleAsync(g => g.RoleId == role.RoleId && g.Permission.PermissionCode == "TASK_VIEW", ct);
            targetGrantToRestore = targetGrant;
            await db.RolePermissions.Where(g => g.RolePermissionId == targetGrant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct);
            await Denied(tasks[0], 404, "OWN target grant cannot borrow manager request authority");
            await db.RolePermissions.Where(g => g.RolePermissionId == targetGrant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, targetGrant.DataScope), ct);
            var before = await Version();
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.triggers WHERE name='TR_DEV3_IRTaskLinkFault'").SingleAsync(ct) != 0) throw new Exception("Preserve existing fault trigger");
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_IRTaskLinkFault ON dbo.internal_request_task_links AFTER INSERT AS THROW 51899,'Synthetic link failure',1;", ct); trigger = true;
            var fault = false;
            try { await service.LinkAsync(ticket, session.SessionId, request, before, new(tasks[0], true), ct); }
            catch (SqlException e) when (e.Number == 51899) { fault = true; }
            var afterFault = await Version();
            Check(fault && before.SequenceEqual(afterFault) && !await db.AuditLogs.AnyAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString(), ct)
                && (await service.ReadAsync(ticket, session.SessionId, request, false, 1, ct)).Total == 0, "link insert fault rolls back audit relationship and request version");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_IRTaskLinkFault", ct); trigger = false;
            await service.LinkAsync(ticket, session.SessionId, request, before, new(tasks[0], true), ct);
            await Denied(tasks[1], 412, "stale request version cannot add link", before);
            await Denied(tasks[0], 409, "duplicate link rejected without changing mandatory metadata");
            var raceVersion = await Version();
            async Task<int> Race()
            {
                await using var context = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                try { await Service(context).LinkAsync(ticket, session.SessionId, request, raceVersion, new(tasks[1], false), ct); return 200; }
                catch (InternalRequestReadException e) { return e.Status; }
            }
            Check((await Task.WhenAll(Race(), Race())).Order().SequenceEqual(new[] { 200, 412 }), "concurrent link commits once and stale contender fails");
            var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("frontend/scripts/sprintInternalRequestTaskLinksBrowser.mjs");
            using (var browser = Process.Start(start) ?? throw new Exception("Task links browser unavailable"))
            {
                var output = browser.StandardOutput.ReadToEndAsync(ct); var errors = browser.StandardError.ReadToEndAsync(ct);
                try
                {
                    await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, request, task = tasks[2] })); browser.StandardInput.Close();
                    await browser.WaitForExitAsync(ct); var result = JsonSerializer.Deserialize<JsonElement>(await output); _ = await errors;
                    if (browser.ExitCode != 0 || !result.GetProperty("success").GetBoolean()) throw new Exception("Task links browser failed at " + result.GetProperty("stage").GetString());
                    checks.Add(new { taskLinkBrowser = result });
                }
                finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
            }
            var links = await service.ReadAsync(ticket, session.SessionId, request, false, 1, ct);
            Check(links.Total == 3 && links.Items.Single(t => t.TaskId == tasks[0]).Mandatory == true && links.Items.Single(t => t.TaskId == tasks[1]).Mandatory == false
                && links.Items.Single(t => t.TaskId == tasks[2]).Mandatory == true, "mandatory and optional metadata persist with authenticated browser link");
            Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_task_links l JOIN dbo.audit_logs a ON a.audit_id=l.audit_id WHERE l.internal_request_id={request} AND a.actor_user_id={actor} AND a.entity_type='internal_requests'").SingleAsync(ct) == 3,
                "every link retains audit provenance");
            Check(await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.Status == original.Status && r.ClosedAt == null, ct), "linking never closes request");
            Check(await db.OperationalTasks.Where(t => tasks.Contains(t.TaskId)).AllAsync(t => t.SourceType == null && t.SourceId == null, ct), "existing task creation provenance is preserved");
            // Revoke exact target grant; IR_VIEW alone must not expose linked target metadata.
            await db.RolePermissions.Where(g => g.RolePermissionId == targetGrant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, "OWN"), ct);
            Check((await service.ReadAsync(ticket, session.SessionId, request, false, 1, ct)).Total == 0, "revoked branch target authority removes linked metadata");
            await db.RolePermissions.Where(g => g.RolePermissionId == targetGrant.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, targetGrant.DataScope), ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (trigger) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_IRTaskLinkFault", CancellationToken.None);
            if (installed) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.internal_request_task_links WHERE internal_request_id={request}", CancellationToken.None);
            foreach (var task in tasks) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.task_progress_versions WHERE task_id={task}", CancellationToken.None);
            await db.OperationalTasks.Where(t => tasks.Contains(t.TaskId)).ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditLogs.Where(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString()).ExecuteDeleteAsync(CancellationToken.None);
            if (targetGrantToRestore != null)
                await db.RolePermissions.Where(g => g.RolePermissionId == targetGrantToRestore.RolePermissionId).ExecuteUpdateAsync(s => s.SetProperty(g => g.DataScope, targetGrantToRestore.DataScope), CancellationToken.None);
            await db.RolePermissions.Where(g => grants.Contains(g.RolePermissionId)).ExecuteDeleteAsync(CancellationToken.None);
            if (createdView) await db.Permissions.Where(p => p.PermissionCode == "IR_VIEW").ExecuteDeleteAsync(CancellationToken.None);
            await db.UserSessions.Where(s => s.SessionId == session.SessionId && s.UserId == actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={original.BranchId} AND department_id={original.ReceivingDepartmentId}", CancellationToken.None);
            checks.Add(new { taskLinkCleanup = true });
        }
    }
}
