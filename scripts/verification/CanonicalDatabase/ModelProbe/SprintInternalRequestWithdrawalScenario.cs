using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintInternalRequestWithdrawalScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, long request, List<object> checks,
        CancellationToken ct, string username, string password)
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(new { withdrawalCheck = label }); }
        var source = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request, ct);
        var branch = source.BranchId; var department = source.ReceivingDepartmentId!.Value;
        var create = await db.Permissions.SingleAsync(p => p.PermissionCode == "IR_CREATE", ct);
        var view = await db.Permissions.SingleOrDefaultAsync(p => p.PermissionCode == "IR_VIEW", ct);
        if (view == null)
        {
            view = new() { PermissionCode = "IR_VIEW", PermissionName = "View internal request history", ModuleCode = create.ModuleCode,
                ResourceCode = create.ResourceCode, ActionCode = "VIEW", Description = "Approved scoped request read" };
            db.Permissions.Add(view); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        Check(view.ModuleCode == create.ModuleCode && view.ResourceCode == create.ResourceCode && view.ActionCode == "VIEW", "approved IR_VIEW catalog contract");
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
        var grant = await db.RolePermissions.SingleOrDefaultAsync(g => g.RoleId == role.RoleId && g.PermissionId == view.PermissionId, ct);
        if (grant == null) { grant = new() { RoleId = role.RoleId, PermissionId = view.PermissionId, DataScope = "BRANCH", GrantedAt = DateTime.UtcNow }; db.RolePermissions.Add(grant); }
        var name = "D3WITH" + Guid.NewGuid().ToString("N")[..8];
        var other = new User { EmployeeCode = name, Username = name, Email = name + "@example.invalid", FullName = "Synthetic different requester",
            PasswordHash = "Synthetic account cannot log in", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
        db.Users.Add(other); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "PENDING_ACCEPTANCE")
            .SetProperty(r => r.SubmittedAt, DateTime.UtcNow.AddMinutes(-5)), ct);
        var path = $"/api/internal-requests/{request}/withdrawal";
        async Task<(int Status, JsonElement Body)> Send(HttpMethod method, string? version = null, bool csrf = true, string reason = "No longer needed")
        {
            using var message = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post) message.Content = JsonContent.Create(new { reason });
            if (version != null) message.Headers.TryAddWithoutValidation("If-Match", '"' + version + '"');
            if (method == HttpMethod.Post && csrf)
            {
                var token = await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf", ct);
                message.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
            }
            using var response = await http.SendAsync(message, ct);
            var body = response.Content.Headers.ContentType?.MediaType?.Contains("json") == true
                ? await response.Content.ReadFromJsonAsync<JsonElement>(ct) : default;
            return ((int)response.StatusCode, body);
        }
        async Task<string> Version()
        {
            var result = await Send(HttpMethod.Get); Check(result.Status == 200, "public withdrawal context readable");
            return result.Body.GetProperty("version").GetString()!;
        }
        var version = await Version();
        Check((await Send(HttpMethod.Get)).Body.GetProperty("canWithdraw").GetBoolean(), "requester can withdraw Pending Acceptance");
        Check((await Send(HttpMethod.Post, version, false)).Status == 400 && (await Send(HttpMethod.Post)).Status == 428, "CSRF and If-Match required");
        Check((await Send(HttpMethod.Post, version, true, "   ")).Status == 400, "blank reason rejected");
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.RequesterId, other.UserId), ct);
        Check(!(await Send(HttpMethod.Get)).Body.GetProperty("canWithdraw").GetBoolean()
            && (await Send(HttpMethod.Post, await Version())).Status == 403, "mapped manager cannot withdraw someone else's request");
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.RequesterId, actor).SetProperty(r => r.Status, "ACCEPTED"), ct);
        Check((await Send(HttpMethod.Post, await Version())).Status == 409, "accepted request cannot be withdrawn");
        await db.InternalRequests.Where(r => r.InternalRequestId == request).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "PENDING_ACCEPTANCE"), ct);
        Check((await Send(HttpMethod.Post, version)).Status == 412, "stale version rejected");
        version = await Version();
        await db.UserBranchAccesses.Where(a => a.UserId == actor && a.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, DateTime.UtcNow), ct);
        Check((await Send(HttpMethod.Post, version)).Status == 404, "requester without current branch access denied");
        await db.UserBranchAccesses.Where(a => a.UserId == actor && a.BranchId == branch).ExecuteUpdateAsync(s => s.SetProperty(a => a.RevokedAt, (DateTime?)null), ct);
        var auditCount = await db.AuditLogs.CountAsync(ct); var eventCount = await db.InternalRequestEvents.CountAsync(ct);
        await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER dbo.TR_DEV3_WithdrawalFault ON dbo.internal_request_withdrawal_outbox AFTER INSERT AS THROW 51903,'Synthetic withdrawal outbox fault',1;", ct);
        try { Check((await Send(HttpMethod.Post, version)).Status >= 500, "outbox fault rejects withdrawal"); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_WithdrawalFault", CancellationToken.None); }
        Check(await Version() == version && await db.AuditLogs.CountAsync(ct) == auditCount && await db.InternalRequestEvents.CountAsync(ct) == eventCount
            && await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.Status == "PENDING_ACCEPTANCE" && r.CancellationReason == null, ct), "status reason event audit and revision roll back with outbox");
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintInternalRequestWithdrawalBrowser.mjs");
        using var browser = Process.Start(start) ?? throw new Exception("Withdrawal browser unavailable");
        var output = browser.StandardOutput.ReadToEndAsync(ct); var errors = browser.StandardError.ReadToEndAsync(ct);
        try
        {
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, request })); browser.StandardInput.Close(); await browser.WaitForExitAsync(ct);
            var proof = JsonSerializer.Deserialize<JsonElement>(await output); _ = await errors;
            if (browser.ExitCode != 0 || !proof.GetProperty("success").GetBoolean()) throw new Exception("Withdrawal browser failed at " + proof.GetProperty("stage").GetString());
            checks.Add(new { withdrawalBrowser = proof });
        }
        finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
        var saved = await db.InternalRequestEvents.AsNoTracking().SingleAsync(e => e.InternalRequestId == request && e.EventType == "WITHDRAW", ct);
        Check(saved.ActorId == actor && saved.NewStatus == "CANCELLED" && saved.EventNote == "No longer needed"
            && await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.Status == "CANCELLED" && r.CancellationReason == saved.EventNote, ct)
            && await db.AuditLogs.CountAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString()
                && a.ActorUserId == actor && a.NewValues != null && a.NewValues.Contains("WITHDRAW"), ct) == 1, "browser withdrawal persisted with reason event and audit");
        Check((await Send(HttpMethod.Post, version)).Status == 412 && await db.InternalRequestEvents.CountAsync(e => e.InternalRequestId == request && e.EventType == "WITHDRAW", ct) == 1, "retry cannot duplicate withdrawal");
        var delivery = new InternalRequestWithdrawalDelivery(db, TimeProvider.System, Options.Create(new NotificationCenterOptions()));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={department}", ct);
        Check(await delivery.DeliverAsync(saved.EventId, actor, ct) == "RECIPIENT_READ_DENIED", "revoked department mapping denies delivery");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch} AND department_id={department}", ct);
        Check(await delivery.DeliverAsync(saved.EventId, actor, ct) == "DELIVERED" && await delivery.DeliverAsync(saved.EventId, actor, ct) == "ALREADY_DELIVERED", "eligible mapped manager receives durable idempotent notification");
        var notice = await db.Notifications.SingleAsync(n => n.SourceType == "INTERNAL_REQUEST_WITHDRAWAL" && n.SourceId == saved.EventId, ct);
        using var resolved = await http.GetAsync($"/api/notifications/{notice.NotificationId}/source", ct);
        Check((int)resolved.StatusCode == 200, "notification source reopens authorized request");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch} AND department_id={department}", ct);
        using var denied = await http.GetAsync($"/api/notifications/{notice.NotificationId}/source", ct);
        Check((int)denied.StatusCode == 404, "notification source rechecks mapping after delivery");
        // A separate synthetic request verifies the actual public race without changing the completed browser receipt.
        var race = new InternalRequest { RequestNo = name + "RACE", BranchId = branch, RequestTypeId = source.RequestTypeId,
            RequesterId = actor, ReceivingDepartmentId = department, Subject = "Synthetic withdrawal race", Description = "Synthetic",
            Status = "PENDING_ACCEPTANCE", SubmittedAt = DateTime.UtcNow.AddMinutes(-1), CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        db.InternalRequests.Add(race); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        request = race.InternalRequestId; path = $"/api/internal-requests/{request}/withdrawal"; version = await Version();
        var competing = await Task.WhenAll(Send(HttpMethod.Post, version), Send(HttpMethod.Post, version));
        Check(competing.Select(r => r.Status).Order().SequenceEqual(new[] { 200, 412 })
            && await db.InternalRequestEvents.CountAsync(e => e.InternalRequestId == request && e.EventType == "WITHDRAW", ct) == 1,
            "concurrent public withdrawals yield one commit and one stale response");
    }
}
