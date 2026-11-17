using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    public async Task<object?> ReversalPreview(long id, long actor, CancellationToken ct)
    {
        await using var tx = await Begin(actor, ct, async token => { await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([actor], token); }, System.Data.IsolationLevel.Serializable);
        var original = await Document(id, ct);
        if (original == null) return null;
        await RequireReversal(actor, original, ct);
        string? blocker = null;
        var effects = new List<object>();
        try
        {
            var movements = await ReversalSource(original, null, ct);
            await ReverseIssueRequest(original, actor, ct);
            await Warehouses(WarehouseIds(original), true, ct);
            foreach (var group in movements.GroupBy(m => new { m.WarehouseId, m.ItemId }))
            {
                var balance = await Balance(group.Key.WarehouseId, group.Key.ItemId, ct);
                var delta = -group.Sum(m => m.SignedBaseQty);
                effects.Add(new { group.Key.WarehouseId, group.Key.ItemId, currentQty = balance.ToString(CultureInfo.InvariantCulture),
                    reversalQty = delta.ToString(CultureInfo.InvariantCulture), resultingQty = (balance + delta).ToString(CultureInfo.InvariantCulture) });
                if (balance + delta < 0) blocker = "MSG16: Reversal would make stock negative.";
            }
        }
        catch (ValidationException e) { blocker = e.Message; }
        var reversals = await db.StockDocuments.AsNoTracking().Where(d => d.ReversalOfDocumentId == id)
            .Select(d => new { d.DocumentId, d.DocumentNo, d.Status }).ToArrayAsync(ct);
        await RequireReversal(actor, original, ct);
        await tx.CommitAsync(ct);
        return new { original.DocumentId, original.DocumentNo, original.DocumentType, original.Status,
            rowVersion = Convert.ToBase64String(original.RowVersion), canReverse = blocker == null, blocker, effects, reversals };
    }
}
