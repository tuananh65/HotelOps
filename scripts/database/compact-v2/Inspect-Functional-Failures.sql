/* Read-only reproduction evidence. Run after Test-Backend.ps1 -Extended.
   No test writes are permitted on the primary Compact or legacy database. */
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51250,'Isolated functional fixture required.',1;
SELECT DB_NAME() AS [database],
 JSON_QUERY((SELECT d.document_id,d.kind,d.status,l.actual_qty AS dispatched_qty,
   (SELECT SUM(m.signed_base_qty) FROM stock_movements m
    JOIN inventory_document_lines received ON received.line_id=m.line_id
    JOIN inventory_documents receipt ON receipt.document_id=received.document_id
    WHERE received.source_line_id=l.line_id AND receipt.kind='TRANSFER_RECEIPT') AS received_qty
   FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id
   JOIN inventory_items item ON item.item_id=l.item_id
   WHERE d.kind='TRANSFER' AND item.code='F-TRANSFER' FOR JSON PATH)) AS transfer_status,
 JSON_QUERY((SELECT d.document_id,d.kind,d.status,d.closed_at,l.actual_qty AS requested_qty,
   (SELECT SUM(m.signed_base_qty) FROM stock_movements m
    JOIN inventory_document_lines received ON received.line_id=m.line_id
    JOIN inventory_documents receipt ON receipt.document_id=received.document_id
    WHERE received.source_line_id=l.line_id AND receipt.kind='RECEIPT') AS received_after_close_qty
   FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id
   JOIN inventory_items item ON item.item_id=l.item_id
   WHERE d.kind='REPLENISHMENT' AND item.code='F-CLOSED-REPLENISH' FOR JSON PATH)) AS closed_replenishment,
 (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS table_count,
 (SELECT COUNT(*) FROM sys.foreign_keys) AS fk_count,
 (SELECT COUNT(*) FROM sys.check_constraints) AS check_count,
 (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) AS trigger_count,
 (SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped=0) AS view_count
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
