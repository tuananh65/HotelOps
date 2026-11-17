using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed record StockReturnApproveInput(string ExpectedRowVersion);

public sealed class StockReturnApprovalWorkflow(HotelOpsDbContext db, InsightAccess access,
    StockReturnApprovalAuthority authority, InventoryService inventory)
{
    public async Task<object> ReadAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await authority.RequireAsync(id, ct);
        await StockReturnApprovalStore.RequireStorageAsync(db, ct);
        var document = await inventory.GetDocument(id, ct) ?? throw new InsightException(404, "RETURN_UNAVAILABLE", "Return request unavailable.");
        var decision = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == id)
            .Select(d => new { d.ApprovedBy, d.ApprovedAt }).SingleAsync(ct);
        var valid = decision.ApprovedBy != null && await StockReturnApprovalStore.MatchesAsync(db, id, document.Status == "DRAFT", ct);
        await authority.RequireAsync(id, ct);
        await tx.CommitAsync(ct);
        return new { Document = document, decision.ApprovedBy, decision.ApprovedAt, ApprovalValid = valid,
            CanApprove = document.Status == "DRAFT" && decision.ApprovedBy == null && decision.ApprovedAt == null };
    }

    public async Task<object> ApproveAsync(long id, StockReturnApproveInput input, CancellationToken ct)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(input.ExpectedRowVersion ?? ""); }
        catch (FormatException) { throw new InsightException(400, "INVALID_VERSION", "A Base64 document rowversion is required."); }
        if (expected.Length != 8) throw new InsightException(400, "INVALID_VERSION", "A Base64 document rowversion is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await StockReturnApprovalStore.RequireStorageAsync(db, ct);
        await inventory.AcquireCatalog(ct);
        var document = await db.StockDocuments.FromSqlInterpolated($"SELECT * FROM dbo.stock_documents WITH(UPDLOCK,HOLDLOCK) WHERE document_id={id}")
            .SingleOrDefaultAsync(ct) ?? throw new InsightException(404, "RETURN_UNAVAILABLE", "Return request unavailable.");
        var branch = await authority.RequireAsync(id, ct);
        if (!document.RowVersion.SequenceEqual(expected)) throw new InsightException(412, "MSG04", "Return changed; reload before approving.");
        if (document.Status != "DRAFT" || document.ApprovedBy != null || document.ApprovedAt != null)
            throw new InsightException(409, "RETURN_NOT_PENDING_APPROVAL", "Only an unapproved pending return can be approved.");
        var snapshot = await StockReturnApprovalStore.SnapshotAsync(db, id, ct);
        var entityId = id.ToString(CultureInfo.InvariantCulture);
        if (await db.AuditLogs.CountAsync(a => a.EntityType == "stock_return_request_intents" && a.EntityId == entityId
            && a.Action == "CREATE" && a.OldValues == null && a.ActorUserId == document.CreatedBy && a.OccurredAt == document.CreatedAt
            && a.BranchId == branch && a.NewValues == snapshot, ct) != 1)
            throw new InsightException(409, "RETURN_REQUEST_PROVENANCE_REQUIRED", "A matching submitted return receipt is required; legacy drafts must be withdrawn and recreated.");
        if (await db.StockDocumentLines.AnyAsync(l => l.DocumentId == id
            && (l.StockReturnLineDetailStockDocumentLine == null
                || l.StockReturnLineDetailStockDocumentLine.OriginalIssueLine.Document.DocumentType != "ISSUE"
                || l.StockReturnLineDetailStockDocumentLine.OriginalIssueLine.Document.Status != "POSTED"
                || l.StockReturnLineDetailStockDocumentLine.OriginalIssueLine.ItemId != l.ItemId
                || !db.StockMovements.Any(m => m.DocumentLineId == l.StockReturnLineDetailStockDocumentLine.OriginalIssueLineId && m.SignedBaseQty < 0)), ct))
            throw new InsightException(409, "ORIGINAL_ISSUE_UNAVAILABLE", "Every returned line must refer to an effective posted issue of the same item.");
        var declared = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == id)
            .Select(l => new { Original = l.StockReturnLineDetailStockDocumentLine!.OriginalIssueLineId,
                Quantity = l.RequestedQty!.Value, l.ConversionFactor }).ToArrayAsync(ct);
        foreach (var group in declared.GroupBy(l => l.Original))
            await inventory.ValidateCapacity(group.Key, 0, group.Sum(l => inventory.HistoricalBase(l.Quantity, l.ConversionFactor)), id, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        document.ApprovedBy = access.UserId; document.ApprovedAt = now;
        var audit = new AuditLog { EntityType = "stock_return_approvals", EntityId = id.ToString(CultureInfo.InvariantCulture),
            BranchId = branch, ActorUserId = access.UserId, Action = "CREATE", OccurredAt = now, NewValues = snapshot };
        db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.stock_return_approvals(document_id,approved_by,approved_at,audit_id,approved_document_version,snapshot_json)
            VALUES({id},{access.UserId},{now},{audit.AuditId},{document.RowVersion},{snapshot})
            """, ct);
        await authority.RequireAsync(id, ct);
        if (!await StockReturnApprovalStore.MatchesAsync(db, id, true, ct))
            throw new InsightException(409, "RETURN_APPROVAL_CONFLICT", "Approval evidence does not match the current request.");
        var result = new { DocumentId = id, State = "APPROVED_PENDING_RECEIPT", ExpectedRowVersion = Convert.ToBase64String(document.RowVersion) };
        await tx.CommitAsync(ct); return result;
    }
}
