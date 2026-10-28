using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed partial class MaterialRequestService
{
    public async Task<MaterialRequestDto?> Save(long? id, MaterialRequestWrite input, CancellationToken ct)
    {
        var discovered = id is null ? new MaterialRequest { BranchId = input.BranchId, RequesterId = input.ActorUserId, Status = "DRAFT" }
            : await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == id.Value, ct);
        if (discovered is null) return null;
        var needsCycle = discovered.Status is "PENDING_APPROVAL" or "APPROVED" || id is null && input.Submit;
        var discoveredCycle = id != null && needsCycle ? await db.MaterialRequestApprovalCycles.AsNoTracking()
            .Where(x => x.RequestId == id.Value).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct) : null;
        // Preserve the BASE explicit selector / latest-cycle edit fallback.
        var approver = input.AssignedApproverUserId ?? discoveredCycle?.AssignedApproverId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            long[] ids = needsCycle && approver is long candidate
                ? [input.ActorUserId, discovered.RequesterId, candidate] : [input.ActorUserId, discovered.RequesterId];
            var lockedUsers = await new UserEligibilityLock(db).LockAsync(ids, ct);
            UserEligibilityLock.RequireEligible(input.ActorUserId, lockedUsers);
            UserEligibilityLock.RequireEligible(discovered.RequesterId, lockedUsers);
            if (needsCycle && approver is long selected) UserEligibilityLock.RequireEligible(selected, lockedUsers);
            await Gate("HotelOps:Inventory:Catalog", ct);
            var r = id is null ? new MaterialRequest { BranchId = input.BranchId, RequesterId = input.ActorUserId, Status = "DRAFT", CreatedAt = DateTime.UtcNow } : await Locked(id.Value, input.ExpectedRowVersion, ct);
            if (r is null) return null;
            if (id != null) RecheckDiscovery(r, discovered);
            if (id != null && needsCycle)
            {
                var latestCycle = await Latest(r.RequestId, ct);
                if (discoveredCycle is null || latestCycle.ApprovalCycleId != discoveredCycle.ApprovalCycleId
                    || latestCycle.AssignedApproverId != discoveredCycle.AssignedApproverId || latestCycle.SubmittedBy != discoveredCycle.SubmittedBy
                    || latestCycle.Status != discoveredCycle.Status || latestCycle.RevisionNo != discoveredCycle.RevisionNo)
                    throw Invalid("Approval context changed during User lock acquisition. Reload.");
            }
            if (input.NeededAt is DateTime time && time.Kind != DateTimeKind.Utc) throw Invalid("NeededAt must use UTC (Z).");
            await Participant(input.ActorUserId, r.BranchId, ct);
            if (input.ActorUserId != r.RequesterId || r.BranchId != input.BranchId) throw Invalid("Requester identity and branch are immutable.");
            State(r, "DRAFT", "PENDING_APPROVAL", "NEEDS_INFORMATION", "APPROVED");
            if (await EverIssued(r, ct)) throw Invalid("Content/work attribution is immutable after first ISSUE.");
            var before = id is null ? null : Snapshot(r); var previousState = r.Status; var oldIncident = r.IncidentId;
            Apply(r, input);
            await ValidateContent(r, false, oldIncident != r.IncidentId || id is null, ct);
            var changed = before is null || JsonSerializer.Serialize(before) != JsonSerializer.Serialize(Snapshot(r));
            if (!changed) { await tx.CommitAsync(ct); return await Get(r.RequestId, ct); }
            if (id is null) { r.RequestNo = await Number(false, ct); db.MaterialRequests.Add(r); await db.SaveChangesAsync(ct); }
            if (needsCycle) await Cycle(r, input.ActorUserId, approver, lockedUsers, ct);
            if (id != null) r.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var currentCycle = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(x => x.RequestId == r.RequestId).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
            Audit(r, input.ActorUserId, id is null ? "CREATE" : "UPDATE", id is null ? "REQUEST_CREATED" : previousState == "APPROVED" ? "REQUEST_REAPPROVAL_REQUIRED" : "REQUEST_EDITED", before,
                new { CycleId = currentCycle?.ApprovalCycleId, Revision = currentCycle?.RevisionNo, AssignedApproverId = currentCycle?.AssignedApproverId, Submitted = r.Status == "PENDING_APPROVAL" });
            if (oldIncident != r.IncidentId && id != null) Audit(r, input.ActorUserId, "UPDATE", "WORK_LINK_CHANGED", before);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Get(r.RequestId, ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    private static void Apply(MaterialRequest r, MaterialRequestWrite input)
    {
        if (input.Lines is null || input.Lines.Count == 0 || input.Lines.Any(x => x is null) || input.Lines.Any(x => x.LineNo <= 0) || input.Lines.Select(x => x.LineNo).Distinct().Count() != input.Lines.Count || input.Lines.Where(x => x.RequestLineId != null).Select(x => x.RequestLineId).Distinct().Count() != input.Lines.Count(x => x.RequestLineId != null)) throw Invalid("Valid unique line numbers/identities are required.");
        var originals = r.MaterialRequestLines.ToArray();
        foreach (var l in originals) l.IsActive = false;
        foreach (var l in input.Lines)
        {
            MaterialRequestLine line;
            if (l.RequestLineId is long key)
            {
                line = originals.SingleOrDefault(x => x.LineId == key) ?? throw Invalid("RequestLineId does not belong to request.");
                if (line.ItemId != l.ItemId || line.UomId != l.UomId) throw Invalid("Do not reuse line identity for a different item/UOM; retire and use a new line number.");
            }
            else { line = new MaterialRequestLine { ItemId = l.ItemId, UomId = l.UomId }; r.MaterialRequestLines.Add(line); }
            if (originals.Any(x => x != line && x.LineNo == l.LineNo)) throw Invalid("Historical line number cannot be reused.");
            line.LineNo = l.LineNo; line.RequestedQty = Qty(l.RequestedQty); line.Note = l.Note?.Trim(); line.IsActive = true;
            // Approved quantities are invalidated only after meaningful content comparison.
        }
        r.DepartmentId = input.DepartmentId; r.SourceWarehouseId = input.SourceWarehouseId; r.Purpose = input.Purpose?.Trim() ?? "";
        r.NeededAt = input.NeededAt; r.PriorityId = input.PriorityId; r.ReceivingRoomId = input.ReceivingRoomId; r.ReceivingAreaId = input.ReceivingAreaId; r.IncidentId = input.IncidentId;
    }
    public async Task<MaterialRequestDto?> Command(long id, string command, MaterialCommand input, CancellationToken ct, MaterialApproval? approval = null)
    {
        var discovered = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == id, ct);
        if (discovered is null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Returning for information creates requester responsibility again.
            var needsRequester = command is "submit" or "cancel" or "request-information";
            var approver = command == "submit" ? input.AssignedApproverUserId : null;
            long[] ids = needsRequester ? [input.ActorUserId, discovered.RequesterId] : [input.ActorUserId];
            if (approver is long candidate) ids = [.. ids, candidate];
            var lockedUsers = await new UserEligibilityLock(db).LockAsync(ids, ct);
            UserEligibilityLock.RequireEligible(input.ActorUserId, lockedUsers);
            if (needsRequester) UserEligibilityLock.RequireEligible(discovered.RequesterId, lockedUsers);
            if (approver is long selected) UserEligibilityLock.RequireEligible(selected, lockedUsers);
            var r = await Locked(id, input.ExpectedRowVersion, ct); if (r is null) return null;
            RecheckDiscovery(r, discovered);
            await Participant(input.ActorUserId, r.BranchId, ct); var before = Snapshot(r);
            var action = "UPDATE"; var evt = "";
            if (command == "submit")
            {
                State(r, "DRAFT", "NEEDS_INFORMATION"); if (r.RequesterId != input.ActorUserId) throw Invalid("Only requester may submit.");
                var resubmit = r.Status == "NEEDS_INFORMATION";
                await Cycle(r, input.ActorUserId, approver, lockedUsers, ct); evt = resubmit ? "REQUEST_RESUBMITTED" : "REQUEST_SUBMITTED";
            }
            else if (command == "cancel")
            {
                State(r, "DRAFT", "PENDING_APPROVAL", "NEEDS_INFORMATION", "APPROVED");
                if (r.RequesterId != input.ActorUserId || await EverIssued(r, ct)) throw Invalid("Only requester may cancel before first ISSUE.");
                r.CancellationReason = Reason(input.Reason); r.Status = "CANCELLED"; action = "CANCEL"; evt = "REQUEST_CANCELLED";
                var cycle = await db.MaterialRequestApprovalCycles.Where(x => x.RequestId == id && x.Status == "PENDING").SingleOrDefaultAsync(ct);
                if (cycle != null) { cycle.Status = "SUPERSEDED"; cycle.ResolvedAt = DateTime.UtcNow; }
            }
            else
            {
                var cycle = await Latest(id, ct);
                if (cycle.AssignedApproverId != input.ActorUserId || input.ActorUserId == r.RequesterId || input.ActorUserId == cycle.SubmittedBy) throw Invalid("Current independent assigned approver is required.");
                if (command == "close-remainder")
                {
                    State(r, "PARTIALLY_ISSUED"); if (cycle.Status != "APPROVED" || !r.MaterialRequestLines.Any(x => x.IsActive && x.FulfilledQty > 0) || !r.MaterialRequestLines.Any(x => x.IsActive && x.ApprovedQty > x.FulfilledQty)) throw Invalid("Issued quantity and open approved remainder are required.");
                    r.RemainderCloseReason = Reason(input.Reason); r.RemainderClosedBy = input.ActorUserId; r.RemainderClosedAt = DateTime.UtcNow; r.Status = "REMAINDER_CLOSED"; evt = "REMAINDER_CLOSED";
                }
                else
                {
                    State(r, "PENDING_APPROVAL"); if (cycle.Status != "PENDING") throw Invalid("Cycle is already resolved.");
                    var decision = ""; string? note = input.Reason?.Trim();
                    if (command == "request-information") { note = Reason(note); r.Status = cycle.Status = "NEEDS_INFORMATION"; decision = "RETURNED"; evt = "REQUEST_INFORMATION_REQUESTED"; }
                    else if (command == "reject") { note = Reason(note); r.Status = cycle.Status = "REJECTED"; decision = "REJECTED"; action = "REJECT"; evt = "REQUEST_REJECTED"; }
                    else if (command == "approve")
                    {
                        var lines = r.MaterialRequestLines.Where(x => x.IsActive).ToArray(); var values = approval?.Lines;
                        if (values is null || values.Any(x => x is null) || values.Count != lines.Length || values.Select(x => x.RequestLineId).Distinct().Count() != values.Count || values.Any(x => !lines.Any(l => l.LineId == x.RequestLineId))) throw Invalid("Supply exactly one approval quantity per active line.");
                        foreach (var l in lines) { var q = Qty(values.Single(x => x.RequestLineId == l.LineId).ApprovedQty); if (q > l.RequestedQty) throw Invalid("ApprovedQty exceeds requested."); l.ApprovedQty = q; cycle.MaterialRequestApprovalLineSnapshots.Single(x => x.RequestLineId == l.LineId).ApprovedQty = q; }
                        var approved = lines.Any(x => x.ApprovedQty > 0); if (!approved) note = Reason(note);
                        r.Status = cycle.Status = approved ? "APPROVED" : "REJECTED"; decision = r.Status; action = approved ? "APPROVE" : "REJECT"; evt = approved ? "REQUEST_APPROVED" : "REQUEST_REJECTED";
                    }
                    else throw Invalid("Unknown command.");
                    cycle.ResolvedAt = DateTime.UtcNow;
                    db.MaterialRequestApprovals.Add(new() { RequestId = id, ApprovalCycleId = cycle.ApprovalCycleId, ApprovalLevel = 1, ApproverId = input.ActorUserId, Decision = decision, DecisionNote = note, DecidedAt = cycle.ResolvedAt.Value });
                }
            }
            r.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var currentCycle = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(x => x.RequestId == id).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
            Audit(r, input.ActorUserId, action, evt, before, new { CycleId = currentCycle?.ApprovalCycleId, Revision = currentCycle?.RevisionNo, AssignedApproverId = currentCycle?.AssignedApproverId });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Get(id, ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    public async Task<MaterialFulfillmentDto?> Issue(long id, MaterialIssue input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await new UserEligibilityLock(db).RequireEligibleAsync(input.ActorUserId, ct);
            var r = await Locked(id, input.ExpectedRowVersion, ct); if (r is null) return null;
            State(r, "APPROVED", "PARTIALLY_ISSUED"); await Participant(input.ActorUserId, r.BranchId, ct);
            var cycle = await Latest(id, ct);
            if (cycle.Status != "APPROVED" || cycle.AssignedApproverId == input.ActorUserId) throw Invalid("Issuer must differ from approving actor of approved cycle.");
            await Reconcile(r, ct);
            await ValidateContent(r, true, false, ct);
            var active = r.MaterialRequestLines.Where(x => x.IsActive).ToArray();
            if (active.Any(x => x.ApprovedQty is null || x.FulfilledQty > x.ApprovedQty)) throw Invalid("Unsafe current quantities.");
            IReadOnlyList<MaterialIssueLine> entries = input.Mode switch {
                "FullRemaining" when input.Lines is null || input.Lines.Count == 0 => active.Where(x => x.ApprovedQty > x.FulfilledQty).Select(x => new MaterialIssueLine(x.LineId, x.ApprovedQty!.Value - x.FulfilledQty)).ToArray(),
                "Partial" => input.Lines ?? [], _ => throw Invalid("Use FullRemaining without lines or Partial with explicit lines.") };
            if (entries.Count == 0 || entries.Any(x => x is null) || entries.Select(x => x.RequestLineId).Distinct().Count() != entries.Count) throw Invalid("A nonempty batch of distinct request lines is required.");
            foreach (var e in entries) { var l = active.SingleOrDefault(x => x.LineId == e.RequestLineId) ?? throw Invalid("Invalid request line."); if (Qty(e.Quantity) == 0 || e.Quantity > l.ApprovedQty - l.FulfilledQty) throw Invalid("Issue quantity exceeds remaining or is nonpositive."); }
            var before = Snapshot(r);
            var f = new MaterialFulfillment { RequestId = id, FulfillmentNo = await Number(true, ct), WarehouseId = r.SourceWarehouseId!.Value, FulfilledBy = input.ActorUserId, Status = "ISSUED", CreatedAt = DateTime.UtcNow };
            db.MaterialFulfillments.Add(f); await db.SaveChangesAsync(ct);
            foreach (var e in entries) { var l = active.Single(x => x.LineId == e.RequestLineId); f.MaterialFulfillmentLines.Add(new() { RequestLineId = l.LineId, ItemId = l.ItemId, UomId = l.UomId, IssuedQty = e.Quantity }); }
            await db.SaveChangesAsync(ct);
            var result = await posting.PostIssue(new(input.ActorUserId, f.FulfillmentId, f.WarehouseId, input.DocumentDate,
                f.MaterialFulfillmentLines.Select(x => new InventoryIssueLine(x.RequestLineId, x.ItemId, x.UomId, x.IssuedQty)).ToArray()), ct);
            f.StockDocumentId = result.DocumentId;
            f.FulfilledAt = await db.StockDocuments.Where(x => x.DocumentId == result.DocumentId).Select(x => x.PostedAt).SingleAsync(ct);
            foreach (var l in f.MaterialFulfillmentLines) { l.StockDocumentLineId = result.Lines.Single(x => x.RequestLineId == l.RequestLineId).StockDocumentLineId; active.Single(x => x.LineId == l.RequestLineId).FulfilledQty += l.IssuedQty; }
            r.Status = active.All(x => x.FulfilledQty == x.ApprovedQty) ? "FULLY_ISSUED" : "PARTIALLY_ISSUED"; r.UpdatedAt = f.FulfilledAt; if (r.Status == "FULLY_ISSUED") r.CompletedAt = f.FulfilledAt;
            var extra = new { f.FulfillmentId, f.FulfillmentNo, result.DocumentId, CycleId = cycle.ApprovalCycleId, Lines = result.Lines };
            Audit(r, input.ActorUserId, "CREATE", "FULFILLMENT_ISSUED", null, extra, "material_fulfillments", f.FulfillmentId,
                new { f.FulfillmentId, f.FulfillmentNo, f.RequestId, f.WarehouseId, f.FulfilledBy, f.Status, f.CreatedAt, f.FulfilledAt, f.StockDocumentId,
                    Lines = f.MaterialFulfillmentLines.Select(x => new { x.FulfillmentLineId, x.RequestLineId, x.ItemId, x.UomId, x.IssuedQty, x.StockDocumentLineId }).ToArray() });
            Audit(r, input.ActorUserId, "UPDATE", r.Status == "FULLY_ISSUED" ? "MATERIALS_FULLY_ISSUED" : "MATERIALS_PARTIALLY_ISSUED", before, extra);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Fulfillment(id, f.FulfillmentId, ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    private async Task Reconcile(MaterialRequest r, CancellationToken ct)
    {
        var batches = await db.MaterialFulfillments.AsNoTracking().Include(x => x.StockDocument).ThenInclude(x => x!.StockDocumentLines)
            .Include(x => x.MaterialFulfillmentLines).Where(x => x.RequestId == r.RequestId).ToListAsync(ct);
        foreach (var f in batches)
        {
            if (f.Status != "ISSUED" || f.StockDocument is not { DocumentType: "ISSUE", Status: "POSTED", ReferenceType: "MATERIAL_FULFILLMENT" } doc || doc.ReferenceId != f.FulfillmentId || f.WarehouseId != r.SourceWarehouseId || doc.SourceWarehouseId != f.WarehouseId || f.MaterialFulfillmentLines.Count != doc.StockDocumentLines.Count)
                throw Invalid("Fulfillment/ISSUE history cannot be reconciled safely.");
            foreach (var l in f.MaterialFulfillmentLines)
            {
                var requestLine = r.MaterialRequestLines.SingleOrDefault(x => x.LineId == l.RequestLineId);
                var issueLine = doc.StockDocumentLines.SingleOrDefault(x => x.LineId == l.StockDocumentLineId);
                if (requestLine is null || !requestLine.IsActive || issueLine is null || l.ItemId != requestLine.ItemId || l.UomId != requestLine.UomId || issueLine.ItemId != l.ItemId || issueLine.UomId != l.UomId || issueLine.ActualQty != l.IssuedQty || l.IssuedQty <= 0)
                    throw Invalid("Fulfillment line correlation is unsafe.");
            }
        }
        foreach (var line in r.MaterialRequestLines)
            if (line.FulfilledQty != batches.SelectMany(x => x.MaterialFulfillmentLines).Where(x => x.RequestLineId == line.LineId).Sum(x => x.IssuedQty)) throw Invalid("Cumulative fulfillment quantity is inconsistent.");
    }
}
