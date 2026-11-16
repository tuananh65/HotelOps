using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.MaterialRequests;

// Evidence only: the caller applies the approved GBR-152 terminal-state mapping.
// This grants no target read access and performs no mutations.
internal sealed record MaterialCompletionEvidence(string? Status, bool FullyIssuedProven,
    bool RemainderClosureProven, string? Failure)
{
    internal static async Task<MaterialCompletionEvidence> ReadAsync(
        HotelOpsDbContext db, long requestId, long branchId, CancellationToken ct)
    {
        if (requestId <= 0 || branchId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Material evidence requires the closure transaction.");
        var available = await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID(N'dbo.material_request_approval_cycles',N'U') IS NOT NULL
              AND OBJECT_ID(N'dbo.material_request_approval_line_snapshots',N'U') IS NOT NULL
              AND COL_LENGTH(N'dbo.material_requests',N'remainder_closed_at') IS NOT NULL
              AND COL_LENGTH(N'dbo.material_requests',N'remainder_closed_by') IS NOT NULL
              AND COL_LENGTH(N'dbo.material_requests',N'remainder_close_reason') IS NOT NULL
              AND COL_LENGTH(N'dbo.material_requests',N'row_version')=8
              AND COL_LENGTH(N'dbo.material_request_approvals',N'approval_cycle_id')=8
              AND COL_LENGTH(N'dbo.material_request_lines',N'is_active')=1
              AND COL_LENGTH(N'dbo.material_fulfillment_lines',N'stock_document_line_id')=8
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
        if (!available) return new(null, false, false, "MATERIAL_COMPLETION_SCHEMA_REQUIRED");
        var request = await db.MaterialRequests.AsNoTracking().Include(r => r.MaterialRequestLines)
            .SingleOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (request is null) return new(null, false, false, "MATERIAL_REQUEST_MISSING");
        MaterialCompletionEvidence Missing(string reason) => new(request.Status, false, false, reason);
        if (request.BranchId != branchId || !await db.Departments.AsNoTracking()
            .AnyAsync(d => d.DepartmentId == request.DepartmentId && d.BranchId == branchId, ct))
            return Missing("MATERIAL_BRANCH_MISMATCH");
        if (request.Status is not ("FULLY_ISSUED" or "REMAINDER_CLOSED"))
            return Missing("MATERIAL_TERMINAL_EVIDENCE_UNAVAILABLE");
        if (request.SourceWarehouseId is not {} warehouse || !await db.Warehouses.AsNoTracking()
            .AnyAsync(w => w.WarehouseId == warehouse && w.BranchId == branchId, ct))
            return Missing("MATERIAL_WAREHOUSE_PROVENANCE_INVALID");

        var cycle = await db.MaterialRequestApprovalCycles.AsNoTracking()
            .Include(c => c.MaterialRequestApproval).Include(c => c.MaterialRequestApprovalLineSnapshots)
            .Where(c => c.RequestId == requestId).OrderByDescending(c => c.RevisionNo)
            .ThenByDescending(c => c.ApprovalCycleId).FirstOrDefaultAsync(ct);
        if (cycle is not { Status: "APPROVED", ResolvedAt: not null }
            || cycle.MaterialRequestApproval is not { Decision: "APPROVED" } approval
            || approval.RequestId != requestId || approval.ApprovalCycleId != cycle.ApprovalCycleId
            || approval.ApproverId != cycle.AssignedApproverId || approval.ApproverId == request.RequesterId
            || approval.ApproverId == cycle.SubmittedBy || approval.DecidedAt != cycle.ResolvedAt)
            return Missing("MATERIAL_APPROVAL_PROVENANCE_INVALID");
        var active = request.MaterialRequestLines.Where(l => l.IsActive).ToArray();
        var snapshots = cycle.MaterialRequestApprovalLineSnapshots;
        if (active.Length == 0 || snapshots.Count != active.Length
            || snapshots.Select(s => s.RequestLineId).Distinct().Count() != snapshots.Count
            || active.Any(l => l.ApprovedQty is null || l.ApprovedQty < 0 || l.FulfilledQty < 0
                || l.FulfilledQty > l.ApprovedQty || !snapshots.Any(s => s.RequestLineId == l.LineId
                    && s.ItemId == l.ItemId && s.UomId == l.UomId && s.ApprovedQty == l.ApprovedQty
                    && s.RequestedQty == l.RequestedQty && s.LineNo == l.LineNo)))
            return Missing("MATERIAL_APPROVED_QUANTITIES_INVALID");
        var batches = await db.MaterialFulfillments.AsNoTracking()
            .Include(f => f.StockDocument).ThenInclude(d => d!.StockDocumentLines)
            .Include(f => f.MaterialFulfillmentLines).Where(f => f.RequestId == requestId).ToListAsync(ct);
        if (batches.Count == 0) return Missing("MATERIAL_ISSUE_EVIDENCE_MISSING");
        try { MaterialRequestService.ValidateFulfillments(request, batches); }
        catch (ValidationException) { return Missing("MATERIAL_ISSUE_EVIDENCE_INVALID"); }
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (batches.Any(f => f.FulfilledAt is null || f.StockDocument!.PostedAt != f.FulfilledAt
            || f.FulfilledAt < cycle.ResolvedAt || f.FulfilledAt > now)) return Missing("MATERIAL_ISSUE_TIME_INVALID");
        var latestIssue = batches.Max(f => f.FulfilledAt!.Value);
        if (request.Status == "FULLY_ISSUED")
            return active.All(l => l.FulfilledQty == l.ApprovedQty) && request.CompletedAt == latestIssue
                ? new(request.Status, true, false, null) : Missing("MATERIAL_FULL_ISSUE_PROOF_INVALID");
        if (request.RemainderClosedAt is not {} closed || closed < latestIssue || closed > now
            || request.RemainderClosedBy != cycle.AssignedApproverId
            || string.IsNullOrWhiteSpace(request.RemainderCloseReason)
            || request.RemainderCloseReason.Trim().Length < 10
            || !active.Any(l => l.FulfilledQty > 0) || !active.Any(l => l.FulfilledQty < l.ApprovedQty))
            return Missing("MATERIAL_REMAINDER_CLOSURE_PROOF_INVALID");
        var key = requestId.ToString(CultureInfo.InvariantCulture);
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "material_requests"
            && a.EntityId == key && a.BranchId == branchId && a.Action == "UPDATE"
            && a.ActorUserId == request.RemainderClosedBy && a.OccurredAt >= closed && a.OccurredAt <= now
            && a.NewValues != null && a.NewValues.Contains("REMAINDER_CLOSED"))
            .Select(a => a.NewValues!).ToArrayAsync(ct);
        if (!audits.Any(raw => MatchesRemainderAudit(raw, request.RemainderClosedBy!.Value,
            closed, request.RemainderCloseReason))) return Missing("MATERIAL_REMAINDER_AUDIT_MISSING");
        return new(request.Status, false, true, null);
    }

    private static bool MatchesRemainderAudit(string raw, long actor, DateTime closed, string reason)
    {
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("BusinessEvent", out var evt) || evt.ValueKind != JsonValueKind.String
                || evt.GetString() != "REMAINDER_CLOSED" || !root.TryGetProperty("State", out var state)
                || state.ValueKind != JsonValueKind.Object) return false;
            return state.TryGetProperty("Status", out var status) && status.ValueKind == JsonValueKind.String
                && status.GetString() == "REMAINDER_CLOSED"
                && state.TryGetProperty("RemainderClosedBy", out var who) && who.ValueKind == JsonValueKind.Number
                && who.TryGetInt64(out var user) && user == actor
                && state.TryGetProperty("RemainderClosedAt", out var when) && when.ValueKind == JsonValueKind.String
                && when.TryGetDateTime(out var at) && at == closed
                && state.TryGetProperty("RemainderCloseReason", out var why) && why.ValueKind == JsonValueKind.String
                && why.GetString() == reason;
        }
        catch (JsonException) { return false; }
    }
}
