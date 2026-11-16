using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

internal static class SprintReportCyclesScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, long actor, long branch, long department,
        string username, string password, List<object> checks, CancellationToken ct)
    {
        // Caller verified the owned clone and supplies the admitted report actor.
        if (!db.Database.GetDbConnection().Database.StartsWith("HotelOps_DEV3_142A_CriteriaTrial_", StringComparison.Ordinal))
            throw new InvalidOperationException("Verified whole clone required");
        var code = "RPTC" + Guid.NewGuid().ToString("N")[..8];
        var type = new InternalRequestType { TypeCode = code, TypeName = "Synthetic cycle report", IsActive = true };
        db.InternalRequestTypes.Add(type); await db.SaveChangesAsync(ct);
        var at = DateTime.UtcNow.AddHours(-2);
        for (var cycleCount = 1; cycleCount <= 2; cycleCount++)
        {
            var end = at.AddMinutes(cycleCount == 1 ? 11 : 36);
            var request = new InternalRequest { RequestNo = code + cycleCount, BranchId = branch, RequestTypeId = type.RequestTypeId,
                RequesterId = actor, ReceivingDepartmentId = department, Subject = "Synthetic report cycles", Description = "Synthetic",
                Status = "CLOSED", CreatedAt = at, SubmittedAt = at, CompletedAt = end, ClosedAt = end };
            db.InternalRequests.Add(request); await db.SaveChangesAsync(ct);
            db.InternalRequestAssignments.Add(new() { InternalRequestId = request.InternalRequestId, AssigneeId = actor, AssignedBy = actor,
                AssignedAt = at.AddMinutes(1), EndedAt = at.AddMinutes(11), Status = "COMPLETED" });
            InternalRequestEvent Event(string kind, DateTime time, string old, string next) => new() {
                InternalRequestId = request.InternalRequestId, ActorId = actor, EventType = kind, OccurredAt = time, OldStatus = old, NewStatus = next };
            db.InternalRequestEvents.AddRange(Event("ACCEPT", at.AddSeconds(30), "PENDING_ACCEPTANCE", "ACCEPTED"),
                Event("ASSIGN", at.AddMinutes(1), "ACCEPTED", "IN_PROGRESS"), Event("COMPLETE", at.AddMinutes(11), "IN_PROGRESS", "CLOSED"));
            if (cycleCount == 2)
                db.InternalRequestEvents.AddRange(Event("REOPEN", at.AddMinutes(31), "CLOSED", "IN_PROGRESS"), Event("COMPLETE", end, "IN_PROGRESS", "CLOSED"));
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        var browser = await SprintReportsBrowser.RunAsync(username, password, branch, department, "request-cycles", ct, requestType: code);
        checks.Add(new { reportCyclesBrowser = browser });
        foreach (var item in browser.GetProperty("exports").EnumerateArray())
        {
            var snapshot = item.GetString();
            if (await db.AuditLogs.CountAsync(a => a.ActorUserId == actor && a.EntityType == "REPORT_EXPORT" && a.EntityId == snapshot, ct) != 3)
                throw new Exception("Cycle report CSV/XLSX/PDF export audit missing");
            if (!await db.AuditLogs.AnyAsync(a => a.ActorUserId == actor && a.EntityType == "REPORT_EXPORT" && a.EntityId == snapshot
                && a.NewValues != null && a.NewValues.Contains("\"format\":\"pdf\""), ct))
                throw new Exception("PDF export format audit missing");
            checks.Add(new { reportCyclesCheck = "CSV/XLSX/PDF snapshot export audits persisted" });
        }
    }
}
