-- Approved room-remediation requests go directly from Head to warehouse.
-- Ordinary documents still require an independent approver. Execute in a transaction after backup.
ALTER TABLE dbo.inventory_documents DROP CONSTRAINT CK_inventory_documents_approval;
ALTER TABLE dbo.inventory_documents WITH CHECK ADD CONSTRAINT CK_inventory_documents_approval CHECK (
 (approved_at IS NULL AND approved_by IS NULL) OR
 (approved_at IS NOT NULL AND approved_by IS NOT NULL AND
  (approved_by<>created_by OR (kind='MATERIAL_REQUEST' AND source_work_id IS NOT NULL AND receiving_room_id IS NOT NULL))));
GO
CREATE OR ALTER TRIGGER dbo.TR_inventory_documents_posted ON dbo.inventory_documents AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.inventory_document_lines l ON l.document_id=d.document_id
   JOIN dbo.stock_movements m ON m.line_id=l.line_id)
   THROW 51020,'Posted document with ledger entries is immutable; append receipt/reversal documents.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.document_id=i.document_id
   WHERE d.approved_by=d.created_by AND d.approved_at IS NOT NULL AND
    (i.created_by<>d.created_by OR ISNULL(i.approved_by,-1)<>d.approved_by
     OR ISNULL(i.source_work_id,-1)<>d.source_work_id OR ISNULL(i.receiving_room_id,-1)<>d.receiving_room_id
     OR i.branch_id<>d.branch_id OR i.kind<>d.kind OR i.approved_at IS NULL OR i.approved_at<>d.approved_at))
   THROW 51230,'Room remediation authorization is immutable.',1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.document_id=i.document_id
   WHERE i.approved_by=i.created_by AND i.approved_at IS NOT NULL
     AND (d.approved_by IS NULL OR d.approved_by<>i.approved_by OR d.created_by<>i.created_by)
     AND NOT EXISTS(
       SELECT 1 FROM dbo.work_items w
       JOIN dbo.work_events report ON report.work_id=w.work_id AND report.event_type='ROOM_ISSUE_REPORTED'
       WHERE w.work_id=i.source_work_id AND w.kind='INCIDENT' AND w.branch_id=i.branch_id
         AND w.room_id=i.receiving_room_id AND w.status='IN_PROGRESS'
         AND JSON_VALUE(report.snapshot_json,'$.Kind')='MISSING'
         AND (SELECT TOP(1) e.event_type FROM dbo.work_events e WHERE e.work_id=w.work_id AND e.event_type LIKE 'ROOM_ISSUE[_]%' ORDER BY e.work_event_id DESC)
             IN ('ROOM_ISSUE_APPROVED','ROOM_ISSUE_MATERIAL_REQUESTED')
         AND EXISTS(SELECT 1 FROM dbo.document_events e WHERE e.document_id=i.document_id AND e.actor_user_id=i.created_by AND e.event_type='ROOM_REMEDIATION_AUTHORIZED')
         AND EXISTS(SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
           WHERE ur.user_id=i.created_by AND ur.branch_id=i.branch_id AND r.code='DEPT_MANAGER' AND r.is_active=1
             AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()))
     )) THROW 51231,'Self approval is limited to authorized missing-room-object remediation.',1;
END;
GO
