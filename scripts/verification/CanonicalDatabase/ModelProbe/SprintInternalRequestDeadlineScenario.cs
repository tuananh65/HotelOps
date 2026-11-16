using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintInternalRequestDeadlineScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, long request,
        Func<HttpMethod, string, object?, string?, bool, Task<(int Status, JsonElement Body)>> send,
        List<object> checks, CancellationToken ct)
    {
        // Called only by the identity-verified whole-clone HTTPS workflow.
        var path = $"/api/internal-requests/{request}/progress";
        var initial = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == request, ct);
        var created = DateTime.SpecifyKind(initial.CreatedAt, DateTimeKind.Utc);
        var before = await send(HttpMethod.Get, path, null, null, true);
        void Check(bool passed, string label)
        {
            if (!passed) throw new Exception(label);
            checks.Add(new { internalRequestDeadlineCheck = label });
        }
        Check(before.Status == 200, "manager reads current progress before deadline revision");
        var version = before.Body.GetProperty("version").GetString()!;
        var percent = before.Body.GetProperty("percent").GetInt32();
        var events = await db.InternalRequestEvents.CountAsync(e => e.InternalRequestId == request, ct);
        var audits = await db.AuditLogs.CountAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString(), ct);
        foreach (var invalid in new[] { created.AddSeconds(-1), created })
        {
            var response = await send(HttpMethod.Post, path,
                new { percent = 13, deadline = invalid, deadlineReason = "Synthetic invalid origin" }, version, true);
            var after = await send(HttpMethod.Get, path, null, null, true);
            Check(response.Status == 400 && after.Status == 200
                && after.Body.GetProperty("version").GetString() == version
                && after.Body.GetProperty("percent").GetInt32() == percent
                && await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.DueAt == initial.DueAt && r.CreatedAt == initial.CreatedAt, ct)
                && await db.InternalRequestEvents.CountAsync(e => e.InternalRequestId == request, ct) == events
                && await db.AuditLogs.CountAsync(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString(), ct) == audits,
                invalid == created ? "deadline equal to creation rejected atomically over HTTPS" : "deadline before creation rejected atomically over HTTPS");
        }
        // SQL time, not UpdatedAt or this probe's wall clock, establishes an already overdue window.
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var overdue = created.AddSeconds(1);
        Check(now > overdue, "fixture has an elapsed valid deadline after official creation");
        foreach (var deadline in new[] { overdue, DateTime.SpecifyKind(now.AddDays(2), DateTimeKind.Utc) })
        {
            var priorDeadline = await db.InternalRequests.Where(r => r.InternalRequestId == request).Select(r => r.DueAt).SingleAsync(ct);
            var response = await send(HttpMethod.Post, path,
                new { percent, deadline, deadlineReason = "Synthetic approved window revision" }, version, true);
            Check(response.Status == 200, deadline == overdue ? "valid already overdue deadline accepted" : "extended deadline accepted");
            var stale = await send(HttpMethod.Post, path,
                new { percent, deadline = created, deadlineReason = "Synthetic stale revision" }, version, true);
            Check(stale.Status == 412, "stale version remains rejected before deadline validation");
            version = response.Body.GetProperty("version").GetString()!;
            var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "internal_requests" && a.EntityId == request.ToString())
                .OrderByDescending(a => a.AuditId).FirstAsync(ct);
            using var oldValues = JsonDocument.Parse(audit.OldValues!);
            using var newValues = JsonDocument.Parse(audit.NewValues!);
            Check(await db.InternalRequests.AnyAsync(r => r.InternalRequestId == request && r.DueAt == deadline
                    && r.CreatedAt == initial.CreatedAt && r.Status == "IN_PROGRESS", ct)
                && oldValues.RootElement.GetProperty("deadline").GetDateTime() == priorDeadline
                && newValues.RootElement.GetProperty("deadline").GetDateTime() == deadline
                && newValues.RootElement.GetProperty("DeadlineReason").GetString() == "Synthetic approved window revision",
                "revision preserves official creation and audits previous/current deadline plus reason");
        }
        var pending = new InternalRequest {
            RequestNo = "D3DEADLINE" + Guid.NewGuid().ToString("N"), BranchId = initial.BranchId,
            RequestTypeId = initial.RequestTypeId, RequesterId = initial.RequesterId,
            RequesterDepartmentId = initial.RequesterDepartmentId, ReceivingDepartmentId = initial.ReceivingDepartmentId,
            Subject = "Synthetic acceptance deadline", Description = "Approved official creation boundary",
            Status = "PENDING_ACCEPTANCE", CreatedAt = created, SubmittedAt = created, DueAt = now.AddDays(1)
        };
        db.InternalRequests.Add(pending);
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var decisionPath = $"/api/internal-requests/{pending.InternalRequestId}/decision";
        var decision = await send(HttpMethod.Get, decisionPath, null, null, true);
        Check(decision.Status == 200, "mapped manager can read synthetic pending decision");
        var decisionVersion = decision.Body.GetProperty("version").GetString()!;
        foreach (var invalid in new[] { created.AddSeconds(-1), created })
        {
            var response = await send(HttpMethod.Post, decisionPath,
                new { accept = true, reason = "Synthetic invalid acceptance deadline", deadline = invalid }, decisionVersion, true);
            var after = await send(HttpMethod.Get, decisionPath, null, null, true);
            Check(response.Status == 400 && after.Status == 200
                && after.Body.GetProperty("version").GetString() == decisionVersion
                && await db.InternalRequests.AnyAsync(r => r.InternalRequestId == pending.InternalRequestId
                    && r.Status == "PENDING_ACCEPTANCE" && r.DueAt == pending.DueAt, ct)
                && !await db.InternalRequestEvents.AnyAsync(e => e.InternalRequestId == pending.InternalRequestId, ct)
                && !await db.AuditLogs.AnyAsync(a => a.EntityType == "internal_requests" && a.EntityId == pending.InternalRequestId.ToString(), ct),
                invalid == created ? "Accept rejects equal creation deadline without mutation" : "Accept rejects pre-creation deadline without mutation");
        }
        var accepted = await send(HttpMethod.Post, decisionPath,
            new { accept = true, reason = "Synthetic valid acceptance deadline", deadline = overdue }, decisionVersion, true);
        Check(accepted.Status == 200 && await db.InternalRequests.AnyAsync(r => r.InternalRequestId == pending.InternalRequestId
            && r.Status == "ACCEPTED" && r.DueAt == overdue && r.CreatedAt == created, ct)
            && await db.AuditLogs.AnyAsync(a => a.EntityType == "internal_requests" && a.EntityId == pending.InternalRequestId.ToString(), ct),
            "Accept preserves valid overdue deadline and official creation with audit");
    }
}
