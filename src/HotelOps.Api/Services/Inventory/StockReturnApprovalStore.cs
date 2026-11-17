using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

internal static class StockReturnApprovalStore
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
          AND name='HotelOps.StockReturnApproval.V1' AND CONVERT(nvarchar(10),value)='1')
          AND COL_LENGTH('dbo.stock_return_approvals','document_id')=8
          AND COL_LENGTH('dbo.stock_return_approvals','approved_document_version')=8
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_stock_return_approvals_immutable') AND is_disabled=0)
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.stock_return_approvals')
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
          THEN 1 ELSE 0 END AS Value
        """).AnyAsync(v => v == 1, ct);

    internal static async Task RequireStorageAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct)) throw new InsightException(503, "RETURN_APPROVAL_STORAGE_REQUIRED", "Install the guarded return approval schema before approval or posting.");
    }

    internal static async Task<string> SnapshotAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        var document = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == id && d.DocumentType == "RETURN")
            .Select(d => new { d.DocumentId, d.DocumentNo, d.DocumentDate, d.SourceWarehouseId, d.DestinationWarehouseId,
                d.CreatedBy, d.CreatedAt, d.Reason, d.Note, d.ReferenceType, d.ReferenceId }).SingleOrDefaultAsync(ct)
            ?? throw new InsightException(404, "RETURN_UNAVAILABLE", "Return request unavailable.");
        var lines = await db.StockDocumentLines.AsNoTracking().Where(l => l.DocumentId == id).OrderBy(l => l.LineId)
            .Select(l => new { l.LineId, l.LineNo, l.ItemId, l.UomId, l.RequestedQty, l.ConversionFactor,
                OriginalIssueLineId = l.StockReturnLineDetailStockDocumentLine == null ? (long?)null : l.StockReturnLineDetailStockDocumentLine.OriginalIssueLineId })
            .ToArrayAsync(ct);
        if (document.SourceWarehouseId != null || document.DestinationWarehouseId == null || lines.Length == 0
            || lines.Any(l => l.OriginalIssueLineId == null || l.RequestedQty is not > 0 || l.ConversionFactor <= 0))
            throw new InsightException(409, "RETURN_LINEAGE_REQUIRED", "Return request requires valid original issue lines and positive declared quantities.");
        return JsonSerializer.Serialize(new { schemaVersion = 1, document, lines });
    }

    internal static async Task<bool> MatchesAsync(HotelOpsDbContext db, long id, bool requireVersion, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct)) return false;
        var snapshot = await SnapshotAsync(db, id, ct);
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.stock_return_approvals p WITH(HOLDLOCK)
            JOIN dbo.stock_documents d WITH(HOLDLOCK) ON d.document_id=p.document_id
            JOIN dbo.audit_logs a WITH(HOLDLOCK) ON a.audit_id=p.audit_id
            JOIN dbo.warehouses w WITH(HOLDLOCK) ON w.warehouse_id=d.destination_warehouse_id
            WHERE p.document_id={id} AND d.document_type='RETURN' AND d.created_by<>p.approved_by
              AND d.approved_by=p.approved_by AND d.approved_at=p.approved_at
              AND p.snapshot_json={snapshot} AND ({requireVersion}=0 OR d.row_version=p.approved_document_version)
              AND a.entity_type='stock_return_approvals' AND a.entity_id=CONVERT(varchar(30),p.document_id)
              AND a.action='CREATE' AND a.actor_user_id=p.approved_by AND a.occurred_at=p.approved_at
              AND a.branch_id=w.branch_id AND a.old_values IS NULL AND a.new_values=p.snapshot_json
            """).AnyAsync(count => count == 1, ct);
    }

    internal static async Task RequireForPostingAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Return posting transaction required.");
        await RequireStorageAsync(db, ct);
        if (!await MatchesAsync(db, id, true, ct))
            throw new InsightException(409, "RETURN_INDEPENDENT_APPROVAL_REQUIRED", "The unchanged return request requires a proven approval by a different user before stock can be posted.");
    }
}
