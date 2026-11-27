using System.Text.Json;
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

    // This is an upper bound for an explicit physical report, not an inferred room stocktake.
    // Existing custody remains authoritative, including legacy reductions.
    internal static async Task<decimal> AssessedOutstanding(HotelOpsCompactDbContext db, long branch, long room, long? cleaning, long item, CancellationToken ct)
    {
        var rows = await (from r in db.SupplyReservations.AsNoTracking()
            join b in db.StaffSupplyBalances on r.BalanceId equals b.BalanceId
            join a in db.StaffSupplyAllocations on b.AllocationId equals a.AllocationId
            where r.WorkId == cleaning && r.RoomId == room && a.BranchId == branch && b.ItemId == item
                && r.Status != "RELEASED"
            select r).ToArrayAsync(ct);
        if (rows.Length == 0)
        {
            Output.Require(cleaning.HasValue && await db.WorkItems.AsNoTracking().AnyAsync(w =>
                w.WorkId == cleaning && w.BranchId == branch && w.RoomId == room
                && (w.Kind == "CLEANING" || w.Kind == "REWORK"), ct), "CLEANING_WORK_REQUIRED");
            // A saved confirmation must never reopen the norm if its reservation is missing.
            if (await db.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityType == "WorkItem"
                && a.EntityKey == cleaning!.Value.ToString() && a.Action == "SUPPLIES_CONFIRMED", ct)) return 0;
            // Do not add an item to a legacy custody set through the configuration fallback.
            Output.Require(!await db.SupplyReservations.AsNoTracking().AnyAsync(r =>
                r.WorkId == cleaning && r.Status != "RELEASED", ct), "CLEANING_SUPPLY_RESERVATION_REQUIRED");
            var capacity = await (from r in db.Rooms.AsNoTracking() join t in db.RoomTypes on r.RoomTypeId equals t.RoomTypeId
                where r.RoomId == room && r.BranchId == branch && r.IsActive && t.IsActive
                select t.Capacity).SingleOrDefaultAsync(ct);
            Output.Require(capacity > 0, "ROOM_SUPPLY_CAPACITY_INVALID");
            var owner = await db.WorkAssignments.AsNoTracking().Where(a => a.WorkId == cleaning && a.AssignmentStatus == "ACTIVE")
                .Select(a => (long?)a.AssigneeUserId).SingleOrDefaultAsync(ct);
            var allocation = await db.StaffSupplyAllocations.AsNoTracking().Where(a => a.UserId == owner
                && a.BranchId == branch && a.Status != "CLOSED"
                && db.WorkdayAssignments.Any(d => d.AssignmentId == a.AssignmentId && d.Status == "WORKING"))
                .Select(a => (long?)a.AllocationId).SingleOrDefaultAsync(ct);
            // Match the assessment preview: allocation snapshot first, current configuration only without allocation.
            var perGuest = allocation.HasValue
                ? await db.StaffSupplyBalances.AsNoTracking().Where(b => b.AllocationId == allocation && b.ItemId == item)
                    .Select(b => (decimal?)b.PerGuestQuantity).SingleOrDefaultAsync(ct)
                : await db.SupplyConfigurations.AsNoTracking().Where(c => c.BranchId == branch && c.ItemId == item
                    && c.IsCurrent && c.IsActive && c.SupplyGroup == "A" && c.UnitId != null)
                    .Select(c => (decimal?)c.PerGuestQuantity).SingleOrDefaultAsync(ct);
            Output.Require(perGuest.HasValue, "CLEANING_SUPPLY_RESERVATION_REQUIRED");
            var replenished = await (from l in db.StaffSupplyLedgers.AsNoTracking()
                join b in db.StaffSupplyBalances on l.BalanceId equals b.BalanceId
                join a in db.StaffSupplyAllocations on b.AllocationId equals a.AllocationId
                where l.WorkId == cleaning && l.Kind == "USE" && b.ItemId == item && a.BranchId == branch
                select (decimal?)l.Quantity).SumAsync(ct) ?? 0;
            // Callers separately subtract prior physical reports or net material-request commitments.
            return PhysicalReportOutstanding(SupplyMath.Required(capacity, perGuest!.Value), null, replenished);
        }
        Output.Require(rows.Length == 1, "CLEANING_SUPPLY_RESERVATION_REQUIRED");
        var row = rows.Single();
        var used = await db.StaffSupplyLedgers.AsNoTracking()
            .Where(l => l.ReservationId == row.ReservationId && l.BalanceId == row.BalanceId
                && l.WorkId == row.WorkId && l.Kind == "USE")
            .Select(l => (decimal?)l.Quantity).SumAsync(ct) ?? 0;
        return ReservationOutstanding(SupplyMath.Required(row.Capacity, row.PerGuestQuantity), row.Quantity, used);
    }

    public static decimal PhysicalReportOutstanding(decimal required, decimal? reserved, decimal consumed)
        => ReservationOutstanding(required, reserved ?? required, consumed);

    public static decimal ReservationOutstanding(decimal required, decimal reserved, decimal consumed)
    {
        Output.Require(required >= 0 && reserved >= 0 && reserved <= required && consumed >= 0
            && consumed <= reserved && SupplyMath.ValidQuantity(required)
            && SupplyMath.ValidQuantity(reserved) && SupplyMath.ValidQuantity(consumed), "SUPPLY_BALANCE_INCONSISTENT");
        return Outstanding(reserved, consumed, 0);
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
