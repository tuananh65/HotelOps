using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.MaterialRequests;
internal sealed partial class MaterialApprovalNotificationDelivery
{
    internal const string ReceiptWaitingKind = "STOCK_RECEIPT_AVAILABLE";
    internal const string ReceiptWaitingSource = "STOCK_RECEIPT_WAITING_REQUEST";
    private static IQueryable<NoticeEvent> ReceiptWaitingEvents(HotelOpsDbContext db) => db.Database.SqlQueryRaw<NoticeEvent>("""
        SELECT a.audit_id AS Id,r.request_id AS RequestId,r.requester_id AS RequesterId,r.branch_id AS BranchId,
          a.actor_user_id AS ActorId,a.occurred_at AS At
        FROM dbo.audit_logs a
        CROSS APPLY (SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END AS payload) j
        JOIN dbo.stock_documents d ON d.document_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.documentId'))
        JOIN dbo.material_requests r ON r.request_id=TRY_CONVERT(bigint,a.entity_id)
        JOIN dbo.warehouses w ON w.warehouse_id=d.destination_warehouse_id
        WHERE a.entity_type='stock_receipt_waiting_requests' AND a.action='CREATE'
          AND a.actor_user_id IS NOT NULL AND a.branch_id=r.branch_id AND w.branch_id=r.branch_id
          AND r.request_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.requestId'))
          AND r.requester_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.requesterId'))
          AND w.warehouse_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.warehouseId'))
          AND r.source_warehouse_id=w.warehouse_id
          AND d.document_type='RECEIPT' AND d.status='POSTED' AND d.source_warehouse_id IS NULL
          AND d.reversal_of_document_id IS NULL AND d.cancelled_at IS NULL
          AND d.posted_at=a.occurred_at AND d.posted_by=a.actor_user_id
          AND NOT EXISTS(SELECT 1 FROM dbo.stock_documents reversal WHERE reversal.reversal_of_document_id=d.document_id AND reversal.status='POSTED')
          AND EXISTS(SELECT 1 FROM OPENJSON(j.payload,'$.shortages') WITH(itemId bigint '$.ItemId',remainingBase decimal(38,3) '$.remainingBase',availableBefore decimal(38,3) '$.availableBefore') shortage
            JOIN dbo.stock_document_lines l ON l.document_id=d.document_id AND l.item_id=shortage.itemId
            JOIN dbo.stock_movements m ON m.document_line_id=l.line_id AND m.warehouse_id=w.warehouse_id AND m.item_id=l.item_id
            WHERE shortage.remainingBase>shortage.availableBefore AND l.actual_qty>0 AND l.base_qty>0
              AND m.signed_base_qty=l.base_qty AND m.created_by=d.posted_by AND m.occurred_at=d.posted_at)
        """);
    internal static async Task<long?> ReceiptWaitingTargetAsync(HotelOpsDbContext db, long id, long user, long? actor, DateTime at, CancellationToken ct)
    {
        var target = await DecisionTargetAsync(db, id, user, ct, NoticeType.ReceiptWaiting);
        return target != null && await ReceiptWaitingEvents(db).AnyAsync(e => e.Id == id && e.ActorId == actor && e.At == at, ct) ? target : null;
    }
}
