using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StockReturnRequestAuthority(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session)
{
    public async Task<long> WarehouseAsync(long actor, long warehouse, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Return transaction required.");
        await session.RequireAsync(ct);
        if (actor != access.UserId) throw new InsightException(403, "ACCESS_DENIED", "Current requester required.");
        var branch = await db.Warehouses.AsNoTracking().Where(w => w.WarehouseId == warehouse && w.IsActive && w.Branch.IsActive)
            .Select(w => (long?)w.BranchId).SingleOrDefaultAsync(ct)
            ?? throw new InsightException(404, "WAREHOUSE_UNAVAILABLE", "Return warehouse unavailable.");
        if (!await StockReturnReadAccess.RequesterAsync(db, actor, branch, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current return request role, grant and scope required.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.Users.AnyAsync(u => u.UserId == actor && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AnyAsync(a => a.UserId == actor && a.BranchId == branch && a.GrantedAt <= now && a.RevokedAt == null, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Active requester and current branch access required.");
        return branch;
    }

    public async Task RequireOwnAsync(long actor, StockDocument document, CancellationToken ct)
    {
        if (document.DocumentType != "RETURN" || document.CreatedBy != actor || document.SourceWarehouseId != null)
            throw new InsightException(403, "RETURN_OWNER_REQUIRED", "Only the return requester may access this request.");
        var branch = await WarehouseAsync(actor, document.DestinationWarehouseId ?? 0, ct);
        var origins = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == document.DocumentId)
            .Select(l => (long?)l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLine.Document.SourceWarehouse!.BranchId).ToArrayAsync(ct);
        if (origins.Length == 0 || origins.Any(origin => origin != branch))
            throw new InsightException(403, "RETURN_LINE_SCOPE", "Original issue scope must match the requester's return branch.");
    }

    public async Task<StockDocumentLine[]> IssuedAsync(long actor, long issue, CancellationToken ct)
    {
        var document = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == issue && d.DocumentType == "ISSUE" && d.Status == "POSTED", ct)
            ?? throw new InsightException(404, "ISSUE_UNAVAILABLE", "A posted issue is required.");
        var branch = await WarehouseAsync(actor, document.SourceWarehouseId ?? 0, ct);
        var batches = await db.MaterialFulfillments.AsNoTracking().Where(f => f.StockDocumentId == issue && f.WarehouseId == document.SourceWarehouseId
                && f.Request.BranchId == branch && f.Status == "ISSUED" && f.FulfilledAt != null)
            .Select(f => new { f.FulfillmentId, f.ReceivedBy, f.ReceivedAt, f.Request.DepartmentId }).ToArrayAsync(ct);
        if (batches.Length != 1) throw new InsightException(409, "ISSUE_PROVENANCE_REQUIRED", "Material issue provenance is unavailable.");
        var batch = batches[0];
        if (batch.ReceivedBy != actor || batch.ReceivedAt == null)
        {
            if (!await WorkdayMembershipStore.InstalledAsync(db, ct))
                throw new InsightException(503, "MEMBERSHIP_STORAGE_REQUIRED", "Explicit department membership is unavailable.");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var memberships = await WorkdayMembershipStore.ReadAsync(db, actor, branch, now, ct);
            if (!memberships.Any(m => m.DepartmentId == batch.DepartmentId))
                throw new InsightException(403, "ISSUE_RECIPIENT_REQUIRED", "Only the recorded recipient or a current member of the receiving department may request this return.");
        }
        // An issuer-entered receiver name is not authenticated recipient evidence.
        return await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == issue
            && db.MaterialFulfillmentLines.Any(f => f.FulfillmentId == batch.FulfillmentId && f.StockDocumentLineId == l.LineId && f.ItemId == l.ItemId && f.UomId == l.UomId)
            && db.StockMovements.Any(m => m.DocumentLineId == l.LineId && m.SignedBaseQty < 0))
            .OrderBy(l => l.LineNo).ToArrayAsync(ct);
    }

    public async Task ValidateAsync(long actor, InventoryDraft input, CancellationToken ct)
    {
        await WarehouseAsync(actor, input.DestinationWarehouseId ?? 0, ct);
        if (input.SourceWarehouseId != null || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            throw new InsightException(400, "RETURN_REASON_REQUIRED", "Enter a return reason of at most 1000 characters.");
        var originals = input.Lines.Select(l => l.OriginalIssueLineId).ToArray();
        if (originals.Any(id => id == null) || originals.Distinct().Count() != originals.Length)
            throw new InsightException(400, "RETURN_LINES_INVALID", "Select distinct original issue lines.");
        var sourceLines = await db.StockDocumentLines.AsNoTracking().Where(l => originals.Contains(l.LineId)).ToArrayAsync(ct);
        if (sourceLines.Length != originals.Length) throw new InsightException(404, "ISSUE_UNAVAILABLE", "Original issue unavailable.");
        foreach (var issue in sourceLines.Select(l => l.DocumentId).Distinct())
        {
            var eligible = await IssuedAsync(actor, issue, ct);
            if (!await db.StockDocuments.AnyAsync(d => d.DocumentId == issue && d.SourceWarehouseId == input.DestinationWarehouseId, ct)
                || sourceLines.Where(l => l.DocumentId == issue).Any(l => !eligible.Any(e => e.LineId == l.LineId)))
                throw new InsightException(403, "RETURN_LINE_SCOPE", "Return must refer to eligible materials from the receiving warehouse.");
        }
        foreach (var line in input.Lines)
        {
            var original = sourceLines.Single(l => l.LineId == line.OriginalIssueLineId);
            if (line.ItemId != original.ItemId || line.UomId != original.UomId)
                throw new InsightException(400, "RETURN_UNIT_MISMATCH", "Use the original issued item and unit.");
        }
    }
}
