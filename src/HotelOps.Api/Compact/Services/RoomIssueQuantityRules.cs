using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// Read-only evidence checks; callers hold the supply lock in their Serializable workflow transaction.
public static class RoomIssueQuantityRules
{
    public static void RequireWithin(decimal quantity, decimal remaining, string code)
        => Output.Require(quantity > 0 && SupplyMath.ValidQuantity(quantity) && quantity <= Math.Max(0, remaining), code);

    public static decimal Outstanding(decimal missing, decimal replenished, decimal committed)
        => Math.Max(0, missing - replenished - committed);

    public static decimal Commitment(string status, decimal requested, decimal issued, decimal returned)
    {
        var net = Math.Max(0, issued - returned);
        var pending = status is "REJECTED" or "CANCELLED" or "REMAINDER_CLOSED" or "CLOSED" or "FULLY_ISSUED"
            ? 0 : Math.Max(0, requested - issued);
        return net + pending;
    }

    internal sealed record Report(string Kind, long? ItemId, decimal Quantity, long? CleaningWorkId);

    // Book evidence is only an upper bound, never a manufactured physical stocktake.
    internal static async Task<decimal> RecordedRoomQuantity(HotelOpsCompactDbContext db,long branch,long room,long item,CancellationToken ct)
    {
        var issued=await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where d.BranchId==branch&&d.ReceivingRoomId==room&&d.Kind=="ISSUE"&&d.PostedAt!=null&&l.ItemId==item
                &&m.SignedBaseQty<0&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select new{l.LineId,Quantity=-m.SignedBaseQty}).ToArrayAsync(ct);
        var ids=issued.Select(i=>i.LineId).ToArray();
        var returned=await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where d.BranchId==branch&&d.Kind=="RETURN"&&d.PostedAt!=null&&l.SourceLineId!=null&&ids.Contains(l.SourceLineId.Value)
                &&m.ReversalOfMovementId==null&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;
        return Math.Max(0,issued.Sum(i=>i.Quantity)-returned);
    }

    internal static async Task<decimal> AssessedOutstanding(HotelOpsCompactDbContext db, long branch, long room, long? cleaning, long item, CancellationToken ct)
    {
        var rows = await (from r in db.SupplyReservations.AsNoTracking()
            join b in db.StaffSupplyBalances on r.BalanceId equals b.BalanceId
            join a in db.StaffSupplyAllocations on b.AllocationId equals a.AllocationId
            where r.WorkId == cleaning && r.RoomId == room && a.BranchId == branch && b.ItemId == item
                && r.Status != "RELEASED" && r.AssessedAt != null
            select r).ToArrayAsync(ct);
        Output.Require(rows.Length == 1, "ROOM_SUPPLY_ASSESSMENT_REQUIRED");
        var row = rows.Single();
        var snapshot = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "WorkItem" && a.EntityKey == row.WorkId.ToString() && a.Action == "SUPPLIES_ASSESSED")
            .OrderByDescending(a => a.AuditId).Select(a => a.AfterJson).FirstOrDefaultAsync(ct);
        var assessment = snapshot == null ? null : JsonSerializer.Deserialize<AssessRoomSupplyLine[]>(snapshot,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.SingleOrDefault(a => a.ReservationId == row.ReservationId);
        Output.Require(assessment != null, "ROOM_SUPPLY_ASSESSMENT_REQUIRED");
        var missing = RoomSupplyAssessmentRules.Validate(SupplyMath.Required(row.Capacity, row.PerGuestQuantity), assessment!);
        Output.Require(missing == row.Quantity && row.UsableQuantity == assessment!.UsableQuantity, "SUPPLY_BALANCE_INCONSISTENT");
        return Outstanding(missing, assessment!.ReplenishedQuantity!.Value, 0);
    }

    internal static async Task<(long WorkId, Report Data)[]> Reports(HotelOpsCompactDbContext db, long branch, long room, long? cleaning, long item, CancellationToken ct)
    {
        var rows = await (from w in db.WorkItems.AsNoTracking() join e in db.WorkEvents on w.WorkId equals e.WorkId
            where w.BranchId == branch && w.RoomId == room && w.Status != "CANCELLED"
                && e.EventType == "ROOM_ISSUE_REPORTED"
            select new { w.WorkId, e.SnapshotJson }).ToArrayAsync(ct);
        return rows.Select(r => (r.WorkId, Data: JsonSerializer.Deserialize<Report>(r.SnapshotJson!)!))
            .Where(r => r.Data.Kind == "MISSING" && r.Data.ItemId == item && r.Data.CleaningWorkId == cleaning).ToArray();
    }

    // Count commitments as well as actual net deliveries, so a second request cannot double-allocate.
    // Closed/rejected/cancelled requests release only their unissued remainder, never posted deliveries.
    internal static async Task<decimal> Committed(HotelOpsCompactDbContext db, long[] works, long item, CancellationToken ct)
    {
        var lines = await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            where d.SourceWorkId != null && works.Contains(d.SourceWorkId.Value) && d.Kind == "MATERIAL_REQUEST" && l.ItemId == item
            select new { l.LineId, d.Status, Quantity = (l.ApprovedQty ?? l.RequestedQty ?? l.ActualQty) * l.ConversionFactor }).ToArrayAsync(ct);
        var ids = lines.Select(l => l.LineId).ToArray();
        var issued = await (from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where l.SourceLineId != null && ids.Contains(l.SourceLineId.Value) && d.Kind == "ISSUE" && d.PostedAt != null
                && m.SignedBaseQty < 0 && !db.StockMovements.Any(r => r.ReversalOfMovementId == m.MovementId)
            select new { l.LineId, l.SourceLineId, Quantity = -m.SignedBaseQty }).ToArrayAsync(ct);
        var issuedIds = issued.Select(l => l.LineId).ToArray();
        var returned = await (from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where l.SourceLineId != null && issuedIds.Contains(l.SourceLineId.Value) && d.Kind == "RETURN" && d.PostedAt != null
                && m.ReversalOfMovementId == null && !db.StockMovements.Any(r => r.ReversalOfMovementId == m.MovementId)
            select new { l.SourceLineId, m.SignedBaseQty }).ToArrayAsync(ct);
        return lines.Sum(line => {
            var deliveries = issued.Where(i => i.SourceLineId == line.LineId).ToArray();
            var gross = deliveries.Sum(i => i.Quantity);
            var returns = deliveries.Sum(i => returned.Where(r => r.SourceLineId == i.LineId).Sum(r => r.SignedBaseQty));
            return Commitment(line.Status, line.Quantity, gross, returns);
        });
    }
}
