using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Replenishments;

// Transactional persistence rules shared by FE-10 and Inventory's linked RECEIPT reversal.
internal static class ReplenishmentRules
{
    internal static ValidationException Invalid(string message) => new(message);
    internal static async Task Gate(HotelOpsDbContext db, string resource, CancellationToken ct)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction() ?? throw new InvalidOperationException("Transaction required.");
        command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;";
        var p = command.CreateParameter(); p.ParameterName = "@resource"; p.Value = resource; command.Parameters.Add(p);
        object? result;
        try { result = await command.ExecuteScalarAsync(ct); }
        catch (Microsoft.Data.SqlClient.SqlException) when (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); throw; }
        if (Convert.ToInt32(result, CultureInfo.InvariantCulture) < 0) { ct.ThrowIfCancellationRequested(); throw new InvalidOperationException("Replenishment application lock failed."); }
    }
    internal static async Task<MaterialReplenishment?> Owner(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        await Gate(db, $"HotelOps:Replenishment:Owner:{id}", ct);
        return await db.MaterialReplenishments.FromSqlInterpolated($"SELECT * FROM dbo.material_replenishments WITH (UPDLOCK,HOLDLOCK) WHERE replenishment_id={id}")
            .Include(x => x.Warehouse).Include(x => x.MaterialReplenishmentLines).ThenInclude(x => x.MaterialReplenishmentReceiptLines).SingleOrDefaultAsync(ct);
    }
    internal static async Task Claims(HotelOpsDbContext db, IEnumerable<MaterialReplenishmentLine> lines, CancellationToken ct)
    {
        foreach (var p in lines.Select(x => (x.WarehouseId, x.ItemId)).Distinct().OrderBy(x => x.WarehouseId).ThenBy(x => x.ItemId))
            await Gate(db, $"HotelOps:Replenishment:Shortage:{p.WarehouseId}:{p.ItemId}", ct);
    }
    internal static void Version(byte[] actual, string? input)
    {
        byte[] value; try { value = Convert.FromBase64String(input ?? ""); } catch (FormatException) { throw Invalid("Malformed ExpectedRowVersion."); }
        if (value.Length != 8 || !value.SequenceEqual(actual)) throw Invalid("Missing or stale ExpectedRowVersion.");
    }
    internal static decimal Quantity(decimal value)
    {
        if (value <= 0 || value > 999999999999999.999m || decimal.Round(value, 3) != value) throw Invalid("Quantity must be positive exact decimal(18,3).");
        return value;
    }
    internal static async Task Actor(HotelOpsDbContext db, long actor, long branch, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == actor, ct);
        if (u is null || u.DeletedAt != null || u.AccountStatus != "ACTIVE" || u.EmploymentStatus != "ACTIVE") throw Invalid("An active business actor is required.");
        if (u.PrimaryBranchId != branch && !await db.UserBranchAccesses.AnyAsync(x => x.UserId == actor && x.BranchId == branch && x.RevokedAt == null, ct)) throw Invalid("Business actor must be branch-related. This is not authorization.");
    }
    internal static object Snapshot(MaterialReplenishment r) => new
    {
        r.ReplenishmentId, r.ReplenishmentNo, r.WarehouseId, BranchId = r.Warehouse.BranchId, r.RequestedBy, r.SupplierName, r.ExpectedOn, r.Status,
        r.ClosedAt, r.ClosedBy, r.CloseReason, r.CreatedAt, r.UpdatedAt,
        Lines = r.MaterialReplenishmentLines.OrderBy(x => x.LineNo).Select(x => new { x.ReplenishmentLineId, x.LineNo, x.ItemId, x.BaseUomId, x.SourceType, x.SourceThresholdId,
            x.SourceMaterialRequestLineId, x.RequestedBaseQty, GrossCreditedBaseQty = x.MaterialReplenishmentReceiptLines.Sum(l => l.CreditedBaseQty), NetReceivedBaseQty = x.ReceivedBaseQty, RemainingBaseQty = x.RequestedBaseQty - x.ReceivedBaseQty, x.IsOpenCoverage, x.Note }).ToArray()
    };
    internal static void Audit(HotelOpsDbContext db, MaterialReplenishment r, long actor, string evt, object? old, object? extra = null)
    {
        var after = Snapshot(r); var prior = old is null ? null : JsonSerializer.Serialize(old);
        using var a = JsonDocument.Parse(JsonSerializer.Serialize(after)); using var b = JsonDocument.Parse(prior ?? "{}");
        var changed = a.RootElement.EnumerateObject().Where(x => !b.RootElement.TryGetProperty(x.Name, out var p) || p.GetRawText() != x.Value.GetRawText()).Select(x => x.Name).ToArray();
        var occurredAt = DateTime.UtcNow;
        db.AuditLogs.Add(new() { EntityType = "material_replenishments", EntityId = r.ReplenishmentId.ToString(CultureInfo.InvariantCulture), ActorUserId = actor, BranchId = r.Warehouse.BranchId,
            Action = old is null ? "CREATE" : "UPDATE", OccurredAt = occurredAt, OldValues = prior, NewValues = JsonSerializer.Serialize(new { BusinessEvent = evt, ActorUserId = actor, OccurredAt = occurredAt, State = after, Extra = extra }), ChangedFields = JsonSerializer.Serialize(changed) });
    }
    internal static async Task<List<MaterialReplenishmentReceiptLine>> Links(HotelOpsDbContext db, long root, CancellationToken ct) =>
        await db.MaterialReplenishmentReceiptLines.AsNoTracking().Include(x => x.StockDocumentLine).ThenInclude(x => x.Document)
            .Where(x => x.ReplenishmentLine.ReplenishmentId == root).ToListAsync(ct);
    internal static async Task ProveReceipt(HotelOpsDbContext db, StockDocumentLine line, long warehouse, long item, long baseUom, CancellationToken ct)
    {
        var doc = line.Document;
        if (doc.DocumentType != "RECEIPT" || doc.DestinationWarehouseId != warehouse || line.ItemId != item || line.BaseQty <= 0 || line.ActualQty <= 0 || line.ConversionFactor <= 0
            || line.ActualQty * line.ConversionFactor != line.BaseQty || !await db.InventoryItems.AnyAsync(x => x.ItemId == item && x.BaseUomId == baseUom, ct)) throw Invalid("Unsafe receipt line identity/base quantity.");
        var movements = await db.StockMovements.AsNoTracking().Where(x => x.DocumentLineId == line.LineId).ToListAsync(ct);
        if (movements.Count != 1 || movements[0].WarehouseId != warehouse || movements[0].ItemId != item || movements[0].SignedBaseQty != line.BaseQty) throw Invalid("Receipt movement evidence is ambiguous.");
        if (doc.Status == "POSTED")
        {
            if (await db.StockDocuments.AnyAsync(x => x.ReversalOfDocumentId == doc.DocumentId && x.Status == "POSTED", ct)) throw Invalid("Receipt reversal evidence conflicts with POSTED status.");
        }
        else if (doc.Status == "REVERSED")
        {
            var reversals = await db.StockDocuments.AsNoTracking().Where(x => x.ReversalOfDocumentId == doc.DocumentId && x.DocumentType == "REVERSAL" && x.Status == "POSTED").Select(x => x.DocumentId).ToListAsync(ct);
            if (reversals.Count != 1) throw Invalid("Receipt reversal lineage is ambiguous.");
            // Whole-document correction: verify offsets by warehouse/item, never infer line correlation by order.
            var all = await db.StockMovements.AsNoTracking().Where(x => x.DocumentLine.DocumentId == doc.DocumentId || x.DocumentLine.DocumentId == reversals[0]).ToListAsync(ct);
            if (all.GroupBy(x => (x.WarehouseId, x.ItemId)).Any(g => g.Sum(x => x.SignedBaseQty) != 0)) throw Invalid("Receipt reversal offset evidence does not reconcile.");
        }
        else throw Invalid("Receipt is not effective or reversed with proven offsets.");
    }
    internal static async Task Reconcile(HotelOpsDbContext db, MaterialReplenishment r, CancellationToken ct)
    {
        var links = await Links(db, r.ReplenishmentId, ct);
        foreach (var link in links)
        {
            var l = r.MaterialReplenishmentLines.Single(x => x.ReplenishmentLineId == link.ReplenishmentLineId);
            await ProveReceipt(db, link.StockDocumentLine, r.WarehouseId, l.ItemId, l.BaseUomId, ct);
            if (link.CreditedBaseQty != link.StockDocumentLine.BaseQty) throw Invalid("Receipt credit snapshot mismatch.");
        }
        foreach (var l in r.MaterialReplenishmentLines)
        {
            var net = links.Where(x => x.ReplenishmentLineId == l.ReplenishmentLineId && x.StockDocumentLine.Document.Status == "POSTED").Sum(x => x.CreditedBaseQty);
            if (l.ReceivedBaseQty != net || l.IsOpenCoverage != (r.Status != "CLOSED" && net < l.RequestedBaseQty)) throw Invalid("Received cache/coverage does not reconcile.");
        }
        var status = DerivedStatus(r);
        if (r.Status != "CLOSED" && status != r.Status) throw Invalid("Replenishment state does not reconcile.");
    }
    private static string DerivedStatus(MaterialReplenishment r) => r.MaterialReplenishmentLines.All(x => x.ReceivedBaseQty == x.RequestedBaseQty) ? "FULLY_RECEIVED"
        : r.MaterialReplenishmentLines.Any(x => x.ReceivedBaseQty > 0) ? "PARTIALLY_RECEIVED" : "REQUESTED";
    internal static async Task Refresh(HotelOpsDbContext db, MaterialReplenishment r, long? reversing, CancellationToken ct)
    {
        var links = await Links(db, r.ReplenishmentId, ct);
        foreach (var l in r.MaterialReplenishmentLines)
        {
            l.ReceivedBaseQty = links.Where(x => x.ReplenishmentLineId == l.ReplenishmentLineId && x.StockDocumentLine.Document.Status == "POSTED" && x.StockDocumentLine.DocumentId != reversing).Sum(x => x.CreditedBaseQty);
            if (l.ReceivedBaseQty > l.RequestedBaseQty) throw Invalid("Receipt exceeds remaining replenishment quantity.");
            l.IsOpenCoverage = r.Status != "CLOSED" && l.ReceivedBaseQty < l.RequestedBaseQty;
        }
        if (r.Status != "CLOSED") r.Status = DerivedStatus(r);
        r.UpdatedAt = DateTime.UtcNow;
    }
    internal static async Task NoConflictingClaims(HotelOpsDbContext db, IReadOnlyList<MaterialReplenishment> roots, CancellationToken ct)
    {
        var ids = roots.Select(x => x.ReplenishmentId).ToArray();
        var lines = roots.SelectMany(x => x.MaterialReplenishmentLines).Where(x => x.IsOpenCoverage).ToArray();
        if (lines.GroupBy(x => (x.WarehouseId, x.ItemId)).Any(g => g.Count() > 1)) throw Invalid("Correction would reopen competing coverage claims.");
        foreach (var l in lines)
            if (await db.MaterialReplenishmentLines.AnyAsync(x => !ids.Contains(x.ReplenishmentId) && x.WarehouseId == l.WarehouseId && x.ItemId == l.ItemId && x.IsOpenCoverage, ct)) throw Invalid("Another replenishment owns active Warehouse/Item coverage.");
    }
    internal static async Task ReverseLinkedReceipt(HotelOpsDbContext db, StockDocument original, long actor, CancellationToken ct)
    {
        if (original.DocumentType != "RECEIPT") return;
        var ids = await db.MaterialReplenishmentReceiptLines.Where(x => x.StockDocumentLine.DocumentId == original.DocumentId).Select(x => x.ReplenishmentLine.ReplenishmentId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var roots = new List<MaterialReplenishment>(); var before = new Dictionary<long, object>();
        foreach (var id in ids)
        {
            var r = (await Owner(db, id, ct))!; await Actor(db, actor, r.Warehouse.BranchId, ct); await Reconcile(db, r, ct);
            roots.Add(r); before.Add(id, Snapshot(r));
        }
        await Claims(db, roots.SelectMany(x => x.MaterialReplenishmentLines), ct);
        foreach (var r in roots) await Refresh(db, r, original.DocumentId, ct);
        await NoConflictingClaims(db, roots, ct);
        foreach (var r in roots) Audit(db, r, actor, "RECEIPT_REVERSED_REPLENISHMENT_RECALCULATED", before[r.ReplenishmentId], new { OriginalReceiptDocumentId = original.DocumentId });
    }
}
