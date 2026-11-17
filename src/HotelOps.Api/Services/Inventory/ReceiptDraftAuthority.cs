using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed record ReceiptSource(long DocumentId, string SourceLabel, string? ReferenceNumber, long? ReplenishmentId);

public sealed class ReceiptDraftAuthority(HotelOpsDbContext db, InsightAccess access, IPermissionService permissions, DashboardSessionGuard session)
{
    public async Task RequireAsync(long actor, long warehouse, CancellationToken ct)
    {
        await session.RequireAsync(ct);
        if (actor != access.UserId) throw new InsightException(403, "ACCESS_DENIED", "Receipt actor must be the current user.");
        var branch = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouse && w.Branch.IsActive)
            .Select(w => (long?)w.BranchId).SingleOrDefaultAsync(ct) ?? throw new InsightException(403, "ACCESS_DENIED", "Warehouse branch unavailable.");
        InsightAccess.Require(await permissions.EvaluateBranchAsync("STOCK_RECEIPT_POST", branch, ct));
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", branch, ct));
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == actor && a.BranchId == branch && a.GrantedAt <= now && a.RevokedAt == null, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "STOREKEEPER"
                && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
                && r.User.DeletedAt == null && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE", ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current Storekeeper role and warehouse branch access required.");
    }

    public async Task ValidateAsync(long actor, InventoryDraft input, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Receipt transaction required.");
        var source = input.SourceLabel?.Trim(); var reference = input.ReferenceNumber?.Trim();
        if (string.IsNullOrEmpty(source) || source.Length > 200 || source.Any(char.IsControl) || reference?.Length > 100 || reference?.Any(char.IsControl) == true)
            throw new InsightException(400, "INVALID_RECEIPT_SOURCE", "Supplier/internal source is required (maximum 200 characters); reference maximum 100 characters.");
        await InstalledAsync(ct);
        await RequireAsync(actor, input.DestinationWarehouseId ?? 0, ct);
        if (input.ReplenishmentId is {} id)
        {
            var targetBranch = await db.Warehouses.Where(w => w.WarehouseId == input.DestinationWarehouseId).Select(w => w.BranchId).SingleAsync(ct);
            InsightAccess.Require(await permissions.EvaluateBranchAsync("REPLENISHMENT_VIEW", targetBranch, ct));
            var root = await db.MaterialReplenishments.AsNoTracking().SingleOrDefaultAsync(r => r.ReplenishmentId == id, ct);
            if (root == null || root.WarehouseId != input.DestinationWarehouseId || root.Status is not ("REQUESTED" or "PARTIALLY_RECEIVED"))
                throw new InsightException(400, "REPLENISHMENT_MISMATCH", "MSG03: Linked replenishment must be open in the receiving warehouse.");
            var branch = await db.Warehouses.Where(w => w.WarehouseId == root.WarehouseId).Select(w => w.BranchId).SingleAsync(ct);
            InsightAccess.Require(await permissions.EvaluateBranchAsync("REPLENISHMENT_VIEW", branch, ct));
            var items = input.Lines.Select(l => l.ItemId).Distinct().ToArray();
            if (await db.MaterialReplenishmentLines.Where(l => l.ReplenishmentId == id && l.WarehouseId == root.WarehouseId && items.Contains(l.ItemId))
                .Select(l => l.ItemId).Distinct().CountAsync(ct) != items.Length)
                throw new InsightException(400, "REPLENISHMENT_MISMATCH", "MSG03: Every receipt item must belong to the linked replenishment.");
        }
    }

    internal async Task RequireExistingLinkAsync(long document, long warehouse, CancellationToken ct)
    {
        if ((await SourceAsync(document, ct))?.ReplenishmentId == null) return;
        var branch = await db.Warehouses.Where(w => w.WarehouseId == warehouse).Select(w => w.BranchId).SingleAsync(ct);
        InsightAccess.Require(await permissions.EvaluateBranchAsync("REPLENISHMENT_VIEW", branch, ct));
    }

    internal async Task InstalledAsync(CancellationToken ct)
    {
        if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.stock_receipt_sources','U') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReceiptSources.V1') THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1)
            throw new InsightException(503, "RECEIPT_SCHEMA_REQUIRED", "Versioned receipt source storage is required.");
    }

    internal Task<ReceiptSource?> SourceAsync(long document, CancellationToken ct) => db.Database.SqlQuery<ReceiptSource>($"SELECT document_id AS DocumentId,source_label AS SourceLabel,reference_number AS ReferenceNumber,replenishment_id AS ReplenishmentId FROM dbo.stock_receipt_sources WHERE document_id={document}").SingleOrDefaultAsync(ct);

    internal async Task SaveSourceAsync(StockDocument document, long actor, InventoryDraft input, CancellationToken ct)
    {
        var before = await SourceAsync(document.DocumentId, ct);
        var after = new ReceiptSource(document.DocumentId, input.SourceLabel!.Trim(), string.IsNullOrWhiteSpace(input.ReferenceNumber) ? null : input.ReferenceNumber.Trim(), input.ReplenishmentId);
        if (before == after) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.stock_receipt_sources SET source_label={after.SourceLabel},reference_number={after.ReferenceNumber},replenishment_id={after.ReplenishmentId} WHERE document_id={document.DocumentId}; IF @@ROWCOUNT=0 INSERT dbo.stock_receipt_sources(document_id,source_label,reference_number,replenishment_id) VALUES({document.DocumentId},{after.SourceLabel},{after.ReferenceNumber},{after.ReplenishmentId});", ct);
        var branch = await db.Warehouses.Where(w => w.WarehouseId == document.DestinationWarehouseId).Select(w => w.BranchId).SingleAsync(ct);
        db.AuditLogs.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "stock_receipt_sources", EntityId = document.DocumentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action = before == null ? "CREATE" : "UPDATE", OccurredAt = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),
            OldValues = JsonSerializer.Serialize(before), NewValues = JsonSerializer.Serialize(after), ChangedFields = "[\"source_label\",\"reference_number\",\"replenishment_id\"]" });
        await db.SaveChangesAsync(ct);
    }
}
