/* Additional compact storage invariants. Included in the initial installer.
   Transactional posting/approval authorization still belongs to the adapted services. */
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51000,'Compact database required.',1;
GO
ALTER TABLE dbo.inventory_documents ADD CONSTRAINT CK_inventory_documents_kind_status CHECK (
 (kind='MATERIAL_REQUEST' AND status IN ('DRAFT','PENDING_APPROVAL','NEEDS_INFORMATION','APPROVED','PARTIALLY_ISSUED','FULLY_ISSUED','REMAINDER_CLOSED','REJECTED','CANCELLED','SUBMITTED','RETURNED','PARTIALLY_FULFILLED','COMPLETED')) OR
 (kind='REPLENISHMENT' AND status IN ('REQUESTED','PARTIALLY_RECEIVED','FULLY_RECEIVED','CLOSED')) OR
 (kind='TRANSFER_REQUEST' AND status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')) OR
 (kind='STOCKTAKE' AND status IN ('IN_PROGRESS','COUNTED','PENDING_ADJUSTMENT','RECONCILED','CANCELLED')) OR
 (kind='TRANSFER' AND status IN ('DRAFT','IN_TRANSIT','PARTIALLY_RECEIVED','RECEIVED','CANCELLED')) OR
 (kind IN ('RECEIPT','ISSUE','RETURN','TRANSFER_RECEIPT','ADJUSTMENT','REVERSAL') AND status IN ('DRAFT','POSTED','REVERSED','CANCELLED'))
);
ALTER TABLE dbo.inventory_documents ADD CONSTRAINT CK_inventory_documents_posted_state CHECK (
 (kind IN ('MATERIAL_REQUEST','REPLENISHMENT','TRANSFER_REQUEST','STOCKTAKE') AND posted_at IS NULL) OR
 (kind='TRANSFER' AND ((status IN ('DRAFT','CANCELLED') AND posted_at IS NULL) OR (status IN ('IN_TRANSIT','PARTIALLY_RECEIVED','RECEIVED') AND posted_at IS NOT NULL))) OR
 (kind IN ('RECEIPT','ISSUE','RETURN','TRANSFER_RECEIPT','ADJUSTMENT','REVERSAL') AND
  ((status IN ('POSTED','REVERSED') AND posted_at IS NOT NULL) OR (status IN ('DRAFT','CANCELLED') AND posted_at IS NULL)))
);
GO
CREATE TRIGGER dbo.TR_stock_movements_quantity ON dbo.stock_movements AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inventory_document_lines l ON l.line_id=i.line_id
   JOIN dbo.inventory_documents d ON d.document_id=l.document_id
   WHERE (d.kind IN ('RECEIPT','RETURN','TRANSFER_RECEIPT') AND
            (i.signed_base_qty<>l.base_qty OR i.warehouse_id<>ISNULL(d.destination_warehouse_id,-1)))
      OR (d.kind IN ('ISSUE','TRANSFER') AND
            (i.signed_base_qty<>-l.base_qty OR i.warehouse_id<>ISNULL(d.source_warehouse_id,-1)))
      OR (d.kind='ADJUSTMENT' AND ABS(i.signed_base_qty)<>l.base_qty)
      OR (d.kind='REVERSAL' AND i.reversal_of_movement_id IS NULL)
      OR (d.kind<>'REVERSAL' AND i.reversal_of_movement_id IS NOT NULL))
   THROW 51022,'Movement sign, quantity, warehouse or reversal linkage does not match its document.',1;
 IF EXISTS(SELECT 1 FROM inserted i
   JOIN dbo.stock_movements r ON r.movement_id=i.reversal_of_movement_id
   JOIN dbo.inventory_document_lines l ON l.line_id=i.line_id
   JOIN dbo.inventory_document_lines original ON original.line_id=r.line_id
   JOIN dbo.inventory_documents d ON d.document_id=l.document_id
   WHERE l.source_line_id IS NULL OR l.source_line_id<>r.line_id OR l.base_qty<>ABS(r.signed_base_qty)
      OR d.source_document_id IS NULL OR d.source_document_id<>original.document_id)
   THROW 51022,'Reversal provenance must point to the exact original document and line.',1;
 -- Hold item/warehouse key ranges until commit. A deadlock must be retried by the caller.
 IF EXISTS(SELECT 1 FROM dbo.stock_movements m WITH(UPDLOCK,HOLDLOCK)
   JOIN (SELECT DISTINCT warehouse_id,item_id FROM inserted) affected
   ON affected.warehouse_id=m.warehouse_id AND affected.item_id=m.item_id
   GROUP BY m.warehouse_id,m.item_id HAVING SUM(m.signed_base_qty)<0)
   THROW 51023,'Posting would make warehouse stock negative.',1;
END;
GO
