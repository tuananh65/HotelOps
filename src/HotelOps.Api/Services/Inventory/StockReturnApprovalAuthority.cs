using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StockReturnApprovalAuthority(HotelOpsDbContext db, InsightAccess access,
    IPermissionService permissions, DashboardSessionGuard session)
{
    public async Task<long> RequireAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Approval transaction required.");
        await session.RequireAsync(ct);
        var document = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == id && d.DocumentType == "RETURN"
            && d.SourceWarehouseId == null && d.DestinationWarehouse != null && d.DestinationWarehouse.IsActive && d.DestinationWarehouse.Branch.IsActive)
            .Select(d => new { d.CreatedBy, d.DestinationWarehouse!.BranchId }).SingleOrDefaultAsync(ct)
            ?? throw new InsightException(404, "RETURN_UNAVAILABLE", "Return request unavailable.");
        if (document.CreatedBy == access.UserId) throw new InsightException(403, "SELF_APPROVAL_DENIED", "The requester cannot approve their own return.");
        InsightAccess.Require(await permissions.EvaluateBranchAsync("STOCK_RETURN_APPROVE", document.BranchId, ct));
        InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", document.BranchId, ct));
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.Users.AnyAsync(u => u.UserId == access.UserId && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null, ct))
            throw new InsightException(403, "ACCESS_DENIED", "Current active account required.");
        var origins = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == id)
            .Select(l => (long?)l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLine.Document.SourceWarehouse!.BranchId)
            .ToArrayAsync(ct);
        if (origins.Length == 0 || origins.Any(b => b == null)) throw new InsightException(409, "RETURN_LINEAGE_REQUIRED", "Original issue scope cannot be established.");
        foreach (var branch in origins.Select(b => b!.Value).Append(document.BranchId).Distinct())
        {
            InsightAccess.Require(await permissions.EvaluateBranchAsync("INVENTORY_VIEW", branch, ct));
            if (!await db.UserBranchAccesses.AnyAsync(a => a.UserId == access.UserId && a.BranchId == branch && a.Branch.IsActive
                && a.GrantedAt <= now && a.RevokedAt == null, ct)) throw new InsightException(403, "ACCESS_DENIED", "Current warehouse branch access required.");
        }
        return document.BranchId;
    }
}
