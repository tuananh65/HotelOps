using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService(HotelOpsDbContext db)
{
    private const decimal QuantityMax = 999999999999999.999m;
    private static readonly string[] TerminalDocuments = ["POSTED", "REVERSED", "CANCELLED", "RECEIVED"];
    private static readonly string[] TerminalStocktakes = ["RECONCILED", "CANCELLED", "COMPLETED"];
    private static readonly string[] OpenStocktakes = ["IN_PROGRESS", "COUNTED", "PENDING_ADJUSTMENT"];

    private static ValidationException Invalid(string message) => new(message);
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
            sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(error => error.Number is 2601 or 2627 &&
                new[] { ("inventory_items", "UQ__inventor__4A67201E"), ("units_of_measure", "UQ__units_of__31E2176B"),
                    ("warehouses", "warehouses_index_19"), ("uom_conversions", "uom_conversions_index_20"),
                    ("stock_thresholds", "stock_thresholds_index_21") }
                .Any(key => error.Message.Contains("'dbo." + key.Item1 + "'", StringComparison.Ordinal) &&
                    error.Message.Contains("'" + key.Item2, StringComparison.Ordinal))))
        {
            throw Invalid("The Inventory code or reference combination already exists.");
        }
    }
    private static string Required(string? text, string field, int max = int.MaxValue)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max)
            throw Invalid($"{field} is required and must not exceed {max} characters.");
        return value;
    }
    private static string Reason(string? value) => Required(value, "Reason").Length >= 3
        ? value!.Trim() : throw Invalid("A meaningful reason of at least 3 characters is required.");
    private static decimal Qty(decimal value, bool signed = false)
    {
        if ((!signed && value < 0) || value > QuantityMax || value < -QuantityMax || decimal.Round(value, 3) != value)
            throw Invalid("Quantity must fit decimal(18,3) and be nonnegative unless a signed delta is requested.");
        return value;
    }
    private static decimal Base(decimal quantity, decimal factor)
    {
        try { return Qty(checked(quantity * factor)); }
        catch (OverflowException) { throw Invalid("Converted quantity exceeds decimal(18,3)."); }
    }
    private static void Version(byte[] actual, string? expected)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(expected ?? ""); }
        catch (FormatException) { throw Invalid("ExpectedRowVersion must be a Base64 rowversion."); }
        if (bytes.Length != 8 || !actual.SequenceEqual(bytes))
            throw Invalid("ExpectedRowVersion is invalid or stale. Reload the resource.");
    }
    private static (int Offset, int Size) Paging(InventoryPage page)
    {
        var offset = ((long)page.Page - 1) * page.PageSize;
        if (page.Page < 1 || page.PageSize is < 1 or > 100 || offset > int.MaxValue)
            throw Invalid("Page must be positive and PageSize must be between 1 and 100.");
        return ((int)offset, page.PageSize);
    }
    private async Task Gate(string resource, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Inventory lock requires a transaction.");
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@resource"; parameter.Value = resource;
        command.Parameters.Add(parameter);
        object? scalar;
        try { scalar = await command.ExecuteScalarAsync(ct); }
        catch (Microsoft.Data.SqlClient.SqlException) when (ct.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }
        var result = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        if (result < 0) throw new InvalidOperationException($"Inventory lock failed ({result}).");
    }
    // All Inventory writers cooperate through the catalog gate, then owner,
    // numeric warehouse, numeric stock-pair, and number-allocation gates.
    private async Task<IDbContextTransaction> Begin(long actor, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await Gate("HotelOps:Inventory:Catalog", ct);
            await Actor(actor, ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private async Task Actor(long id, CancellationToken ct)
    {
        // Temporary integration identity only; this does not authenticate a caller.
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.UserId == id, ct))
            throw Invalid("ActorUserId does not exist.");
    }
    private async Task<List<Warehouse>> Warehouses(IEnumerable<long> ids, bool active, CancellationToken ct)
    {
        var result = new List<Warehouse>();
        foreach (var id in ids.Distinct().Order())
        {
            await Gate($"HotelOps:Inventory:Warehouse:{id}", ct);
            var warehouse = await db.Warehouses.FromSqlInterpolated(
                $"SELECT * FROM dbo.warehouses WITH (UPDLOCK,HOLDLOCK) WHERE warehouse_id={id}")
                .SingleOrDefaultAsync(ct) ?? throw Invalid("Warehouse does not exist.");
            if (active && !warehouse.IsActive) throw Invalid("Warehouse must be active.");
            result.Add(warehouse);
        }
        return result;
    }
    private async Task StockGates(IEnumerable<(long Warehouse, long Item)> pairs, CancellationToken ct)
    {
        foreach (var pair in pairs.Distinct().OrderBy(x => x.Warehouse).ThenBy(x => x.Item))
            await Gate($"HotelOps:Inventory:Stock:{pair.Warehouse}:{pair.Item}", ct);
    }
    private async Task<decimal> Balance(long warehouse, long item, CancellationToken ct) =>
        await db.StockMovements.Where(x => x.WarehouseId == warehouse && x.ItemId == item)
            .SumAsync(x => (decimal?)x.SignedBaseQty, ct) ?? 0;

    private async Task<InventoryItem> Item(long id, CancellationToken ct)
    {
        var item = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == id, ct);
        if (item is null || !item.IsActive ||
            !await db.ItemCategories.AnyAsync(x => x.ItemCategoryId == item.CategoryId && x.IsActive, ct) ||
            !await db.UnitsOfMeasures.AnyAsync(x => x.UomId == item.BaseUomId && x.IsActive, ct))
            throw Invalid("Item, category and base UOM must be active.");
        return item;
    }
    private async Task<decimal> Factor(InventoryItem item, long uom, CancellationToken ct)
    {
        if (!await db.UnitsOfMeasures.AnyAsync(x => x.UomId == uom && x.IsActive, ct))
            throw Invalid("Selected UOM must be active.");
        if (uom == item.BaseUomId) return 1;
        return await db.UomConversions.Where(x => x.ItemId == item.ItemId && x.FromUomId == uom &&
            x.ToUomId == item.BaseUomId && x.IsActive).Select(x => (decimal?)x.ConversionFactor)
            .SingleOrDefaultAsync(ct) is > 0m and var factor ? factor :
            throw Invalid("An active item-specific direct conversion to base UOM is required.");
    }
    private async Task<string> Number(string prefix, DateTime now, bool stocktake, CancellationToken ct)
    {
        now = DateTime.UtcNow;
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        await Gate(stocktake ? $"HotelOps:Inventory:StocktakeNo:{date}" : $"HotelOps:Inventory:DocumentNo:{prefix}:{date}", ct);
        if (DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) != date)
            return await Number(prefix, DateTime.UtcNow, stocktake, ct);
        var head = $"{prefix}-{date}-";
        var numbers = stocktake
            ? await db.Stocktakes.Where(x => x.StocktakeNo.StartsWith(head)).Select(x => x.StocktakeNo).ToListAsync(ct)
            : await db.StockDocuments.Where(x => x.DocumentNo.StartsWith(head)).Select(x => x.DocumentNo).ToListAsync(ct);
        System.Numerics.BigInteger maximum = 0;
        foreach (var value in numbers)
        {
            var suffix = value[head.Length..];
            if (suffix.All(char.IsAsciiDigit) && System.Numerics.BigInteger.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                maximum = System.Numerics.BigInteger.Max(maximum, n);
        }
        var allocated = head + (maximum + 1).ToString("D3", CultureInfo.InvariantCulture);
        return allocated.Length <= 50 ? allocated : throw new InvalidOperationException("Inventory document number space is exhausted.");
    }
    private Dictionary<string, object?> Scalars(object entity) => db.Entry(entity).Properties
        .Where(x => x.Metadata.Name != "RowVersion").ToDictionary(x => x.Metadata.GetColumnName()!, x => x.CurrentValue);
    private async Task Audit(object entity, string table, long id, long actor, string action, string businessEvent,
        object? oldValues, IEnumerable<long?> branches, DateTime now, CancellationToken ct, string[]? extraFields = null)
    {
        db.ChangeTracker.DetectChanges();
        var fields = db.Entry(entity).Properties.Where(x => x.IsModified && x.Metadata.Name != "RowVersion" && !Equals(x.OriginalValue, x.CurrentValue))
            .Select(x => x.Metadata.GetColumnName()!).Concat(extraFields ?? []).Distinct().ToArray();
        var values = Scalars(entity);
        if (action == "CREATE") fields = values.Keys.Concat(extraFields ?? []).Distinct().ToArray();
        await Save(ct);
        id = Convert.ToInt64(db.Entry(entity).Property(db.Entry(entity).Metadata.FindPrimaryKey()!.Properties.Single().Name).CurrentValue, CultureInfo.InvariantCulture);
        values = Scalars(entity);
        foreach (var branch in branches.Distinct())
            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actor,
                BranchId = branch,
                EntityType = table,
                EntityId = id.ToString(CultureInfo.InvariantCulture),
                Action = action,
                OccurredAt = now,
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValues = JsonSerializer.Serialize(new
                {
                    businessEvent, values,
                    lines = entity is Stocktake stocktake ? stocktake.StocktakeLines.Select(Scalars).ToList() : null
                }),
                ChangedFields = JsonSerializer.Serialize(fields)
            });
        await Save(ct);
    }
}
