/* Supporting integrity rules; installed in the same transaction as 01_Schema.sql.
   All triggers handle sets, not only one inserted row. No 41st ledger/config table. */
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51000,'Compact database required.',1;
GO
CREATE TRIGGER dbo.TR_locations_hierarchy ON dbo.locations AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.locations p ON p.location_id=i.parent_location_id
   WHERE (i.kind='FLOOR' AND i.parent_location_id IS NOT NULL)
      OR (i.kind='AREA' AND i.parent_location_id IS NOT NULL AND p.kind<>'FLOOR'))
   THROW 51010,'A floor is a root; an area may only belong to a floor.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.locations child ON child.parent_location_id=i.location_id WHERE i.kind<>'FLOOR')
   THROW 51010,'A location with child areas must remain a floor.',1;
END;
GO
CREATE TRIGGER dbo.TR_rooms_cycle ON dbo.rooms AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.work_items w ON w.work_id=i.current_cleaning_work_id
           WHERE w.kind<>'CLEANING')
   THROW 51011,'The current room cycle must point to cleaning work for that room and turnover.',1;
END;
GO
CREATE TRIGGER dbo.TR_work_items_relations ON dbo.work_items AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.work_id=i.work_id
           WHERE i.kind<>d.kind OR i.branch_id<>d.branch_id)
   THROW 51012,'Work kind and historical branch are immutable.',1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.work_items p ON p.work_id=i.parent_work_id
   WHERE i.kind IN ('INSPECTION','REWORK') AND
   (p.work_id IS NULL OR p.kind<>'CLEANING' OR p.room_id<>i.room_id OR p.turnover_no<>i.turnover_no))
   THROW 51013,'Inspection/rework requires the cleaning parent in the same room turnover.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.work_items p ON p.work_id=i.parent_work_id WHERE p.kind=i.kind)
   THROW 51013,'Use dependency links for same-kind work; parent hierarchy cannot recurse by kind.',1;
 IF EXISTS(SELECT 1 FROM inserted p JOIN dbo.work_items c ON c.parent_work_id=p.work_id
   WHERE c.kind IN ('INSPECTION','REWORK') AND
   (p.kind<>'CLEANING' OR p.room_id IS NULL OR p.room_id<>c.room_id OR p.turnover_no IS NULL OR p.turnover_no<>c.turnover_no))
   THROW 51013,'Cannot invalidate child inspection/rework provenance.',1;
END;
GO
CREATE TRIGGER dbo.TR_findings_kind ON dbo.inspection_findings AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.work_items w ON w.work_id=i.inspection_work_id
   LEFT JOIN dbo.work_items r ON r.work_id=i.rework_work_id
   WHERE w.kind<>'INSPECTION' OR (i.rework_work_id IS NOT NULL AND
       (r.kind<>'REWORK' OR r.parent_work_id<>w.parent_work_id OR r.room_id<>w.room_id OR r.turnover_no<>w.turnover_no)))
   THROW 51014,'Finding must reference an inspection and rework from the same cleaning cycle.',1;
END;
GO
CREATE TRIGGER dbo.TR_checklist_template_immutable ON dbo.checklist_templates AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted WHERE published_at IS NOT NULL)
   THROW 51015,'Published checklist versions are immutable; create a new version.',1;
END;
GO
CREATE TRIGGER dbo.TR_checklist_items_immutable ON dbo.checklist_items AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM dbo.checklist_templates t JOIN
   (SELECT checklist_template_id FROM inserted UNION SELECT checklist_template_id FROM deleted) x
   ON x.checklist_template_id=t.checklist_template_id WHERE t.published_at IS NOT NULL)
   THROW 51015,'Items of a published checklist are immutable.',1;
END;
GO
CREATE TRIGGER dbo.TR_work_intervals_workday ON dbo.work_intervals AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.workdays w WITH(UPDLOCK,HOLDLOCK) ON w.workday_id=i.workday_id
   WHERE (i.closed_at IS NULL AND w.status<>'WORKING') OR w.started_at IS NULL
      OR i.effective_started_at<w.started_at OR (w.ended_at IS NOT NULL AND i.effective_ended_at>w.ended_at))
   THROW 51016,'Work intervals must be inside a started workday; open intervals need a working day.',1;
END;
GO
CREATE TRIGGER dbo.TR_workdays_intervals ON dbo.workdays AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted w JOIN dbo.work_intervals i WITH(UPDLOCK,HOLDLOCK) ON i.workday_id=w.workday_id
   WHERE (w.status<>'WORKING' AND i.closed_at IS NULL) OR w.started_at IS NULL
      OR i.effective_started_at<w.started_at OR (w.ended_at IS NOT NULL AND i.effective_ended_at>w.ended_at))
   THROW 51016,'Cannot end or shorten a workday around an open/outside interval.',1;
END;
GO
CREATE TRIGGER dbo.TR_stock_movements_immutable ON dbo.stock_movements AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 THROW 51017,'Stock ledger is append-only. Post a linked reversal.',1;
END;
GO
CREATE TRIGGER dbo.TR_stock_movements_posted ON dbo.stock_movements AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.inventory_document_lines l ON l.line_id=i.line_id
   JOIN dbo.inventory_documents d ON d.document_id=l.document_id
   WHERE d.posted_at IS NULL OR d.kind NOT IN ('RECEIPT','ISSUE','RETURN','TRANSFER','TRANSFER_RECEIPT','ADJUSTMENT','REVERSAL')
      OR (i.warehouse_id<>ISNULL(d.source_warehouse_id,-1) AND i.warehouse_id<>ISNULL(d.destination_warehouse_id,-1)))
   THROW 51018,'Ledger movements require a posted stock document and its warehouse.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.stock_movements r ON r.movement_id=i.reversal_of_movement_id
   WHERE i.warehouse_id<>r.warehouse_id OR i.item_id<>r.item_id OR i.signed_base_qty<>-r.signed_base_qty
      OR r.reversal_of_movement_id IS NOT NULL)
   THROW 51019,'A reversal must exactly negate the original item/warehouse movement once.',1;
END;
GO
CREATE TRIGGER dbo.TR_inventory_documents_posted ON dbo.inventory_documents AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.inventory_document_lines l ON l.document_id=d.document_id
   JOIN dbo.stock_movements m ON m.line_id=l.line_id)
   THROW 51020,'Posted document with ledger entries is immutable; append receipt/reversal documents.',1;
END;
GO
CREATE TRIGGER dbo.TR_inventory_lines_posted ON dbo.inventory_document_lines AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM
   (SELECT document_id FROM inserted UNION SELECT document_id FROM deleted) x
   JOIN dbo.inventory_document_lines l ON l.document_id=x.document_id
   JOIN dbo.stock_movements m ON m.line_id=l.line_id)
   THROW 51020,'Lines of a document with ledger entries are immutable.',1;
END;
GO
CREATE TRIGGER dbo.TR_work_events_immutable ON dbo.work_events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51021,'Work history is append-only.',1; END;
GO
CREATE TRIGGER dbo.TR_room_events_immutable ON dbo.room_events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51021,'Room history is append-only.',1; END;
GO
CREATE TRIGGER dbo.TR_document_events_immutable ON dbo.document_events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51021,'Document history is append-only.',1; END;
GO
CREATE TRIGGER dbo.TR_asset_events_immutable ON dbo.asset_events AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51021,'Asset history is append-only.',1; END;
GO
CREATE TRIGGER dbo.TR_audit_logs_immutable ON dbo.audit_logs AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51021,'Audit history is append-only.',1; END;
GO
CREATE VIEW dbo.v_stock_balances AS
 SELECT branch_id,warehouse_id,item_id,SUM(signed_base_qty) AS base_qty
 FROM dbo.stock_movements GROUP BY branch_id,warehouse_id,item_id;
GO
CREATE VIEW dbo.v_inventory_document_totals AS
 SELECT d.document_id,d.branch_id,d.kind,d.status,COUNT_BIG(l.line_id) AS line_count,
        COALESCE(SUM(l.base_qty),0) AS base_qty,
        COALESCE(SUM(l.base_qty*l.unit_cost),0) AS total_cost
 FROM dbo.inventory_documents d LEFT JOIN dbo.inventory_document_lines l ON l.document_id=d.document_id
 GROUP BY d.document_id,d.branch_id,d.kind,d.status;
GO
