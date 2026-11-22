/* Balanced simplification: no workday/shift prerequisite, no role grants.
   Transitions and permission admission still belong to the backend. */
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51000,'Compact database required.',1;
GO
CREATE TRIGGER dbo.TR_work_intervals_assignment ON dbo.work_intervals AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.work_assignments a WITH(UPDLOCK,HOLDLOCK) ON a.assignment_id=i.assignment_id
   WHERE i.effective_started_at<a.assigned_at
      OR (i.closed_at IS NULL AND (a.assignment_status<>'ACTIVE' OR a.ended_at IS NOT NULL))
      OR (a.ended_at IS NOT NULL AND i.effective_ended_at>a.ended_at))
   THROW 51200,'Work time requires the matching assignment, never a workday.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.interval_id=i.interval_id
   WHERE i.work_id<>d.work_id OR i.assignment_id<>d.assignment_id OR i.user_id<>d.user_id OR i.branch_id<>d.branch_id
      OR d.closed_at IS NOT NULL OR i.effective_started_at<>d.effective_started_at OR i.opened_at<>d.opened_at)
   THROW 51201,'Time provenance and closed intervals are immutable; corrections belong in audited events.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.work_intervals w WITH(UPDLOCK,HOLDLOCK)
   ON w.user_id=i.user_id AND w.interval_id<>i.interval_id
   WHERE i.effective_started_at<COALESCE(w.effective_ended_at,CONVERT(datetime2,'9999-12-31'))
     AND w.effective_started_at<COALESCE(i.effective_ended_at,CONVERT(datetime2,'9999-12-31')))
   THROW 51202,'Work time intervals for the same person cannot overlap.',1;
END;
GO
CREATE TRIGGER dbo.TR_work_assignments_open_time ON dbo.work_assignments AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted a JOIN dbo.work_intervals i WITH(UPDLOCK,HOLDLOCK) ON i.assignment_id=a.assignment_id
   WHERE (a.assignment_status<>'ACTIVE' AND i.closed_at IS NULL) OR i.effective_started_at<a.assigned_at
      OR (a.ended_at IS NOT NULL AND i.effective_ended_at>a.ended_at))
   THROW 51203,'Close time intervals before reassigning or ending responsibility.',1;
END;
GO
CREATE TRIGGER dbo.TR_work_intervals_no_delete ON dbo.work_intervals AFTER DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51201,'Work time history cannot be deleted.',1; END;
GO
CREATE TRIGGER dbo.TR_work_items_review ON dbo.work_items AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.kind='INSPECTION' AND i.status='PASSED'
   AND (i.checklist_template_id IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.work_checklist_items c WHERE c.work_id=i.work_id)
    OR EXISTS(SELECT 1 FROM dbo.work_checklist_items c WHERE c.work_id=i.work_id AND c.required_snapshot=1 AND (c.outcome IS NULL OR c.outcome<>'PASS'))
    OR EXISTS(SELECT 1 FROM dbo.inspection_findings f JOIN dbo.work_items source ON source.work_id=f.inspection_work_id
       WHERE source.parent_work_id=i.parent_work_id AND f.status NOT IN ('RESOLVED','CANCELLED'))))
   THROW 51212,'Passed inspection requires completed required checks and resolved findings in this cycle.',1;
 IF EXISTS(SELECT 1 FROM inserted w JOIN dbo.work_assignments a ON a.assignee_user_id=w.verified_by
   WHERE (w.kind='INSPECTION' AND
     (a.work_id=w.parent_work_id OR a.work_id IN(SELECT r.work_id FROM dbo.work_items r WHERE r.parent_work_id=w.parent_work_id AND r.kind='REWORK')))
   OR (w.kind IN ('OPERATIONAL','MAINTENANCE') AND a.work_id=w.work_id)
   OR (w.kind='INCIDENT' AND a.work_id IN(SELECT m.work_id FROM dbo.work_items m WHERE m.parent_work_id=w.work_id AND m.kind='MAINTENANCE')))
   THROW 51204,'The verifier must be independent of all recorded performers.',1;
 IF EXISTS(SELECT 1 FROM inserted w JOIN dbo.work_intervals i ON i.work_id=w.work_id
   WHERE i.closed_at IS NULL AND w.status IN ('COMPLETED','CLOSED','CANCELLED','PENDING_INSPECTION','PENDING_REINSPECTION','PENDING_VERIFICATION','PASSED','FAILED'))
   THROW 51205,'Close execution time in the same transaction before submitting work.',1;
 IF EXISTS(SELECT 1 FROM inserted m LEFT JOIN dbo.work_items incident ON incident.work_id=m.parent_work_id
   WHERE m.kind='MAINTENANCE' AND m.parent_work_id IS NOT NULL AND incident.kind<>'INCIDENT')
   THROW 51206,'Maintenance parent must be its source incident.',1;
 IF EXISTS(SELECT 1 FROM inserted r JOIN dbo.work_links l ON l.source_work_id=r.work_id
   JOIN dbo.work_items target ON target.work_id=l.target_work_id
   WHERE r.kind='INTERNAL_REQUEST' AND r.status IN ('COMPLETED','CLOSED') AND l.relation='DEPENDS_ON'
     AND NOT ((target.kind='INSPECTION' AND target.status='PASSED')
       OR (target.kind IN ('OPERATIONAL','INCIDENT','MAINTENANCE','INTERNAL_REQUEST') AND target.status='CLOSED')
       OR (target.kind IN ('CLEANING','REWORK') AND target.status='COMPLETED')))
   THROW 51207,'Internal request cannot finish before required linked work is verified.',1;
END;
GO
CREATE TRIGGER dbo.TR_inspection_results_final ON dbo.work_checklist_items AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM (SELECT work_id FROM inserted UNION SELECT work_id FROM deleted) changed
   JOIN dbo.work_items i ON i.work_id=changed.work_id WHERE i.kind='INSPECTION' AND i.status IN ('PASSED','FAILED'))
   THROW 51213,'Final inspection evidence is immutable; create a reinspection.',1;
END;
GO
CREATE TRIGGER dbo.TR_work_events_handover ON dbo.work_events AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.event_type='HANDOVER_OFFERED'
   AND (i.recipient_user_id IS NULL OR i.recipient_user_id=i.actor_user_id OR NULLIF(LTRIM(RTRIM(i.reason)),'') IS NULL))
   THROW 51208,'Backlog handover requires another named recipient and context.',1;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.work_events offered ON offered.work_event_id=i.related_event_id
   WHERE i.event_type IN ('HANDOVER_ACCEPTED','HANDOVER_REJECTED') AND
   (offered.work_event_id IS NULL OR offered.event_type<>'HANDOVER_OFFERED' OR offered.recipient_user_id<>i.actor_user_id))
   THROW 51208,'Only the offered recipient can accept or reject a backlog handover.',1;
 IF EXISTS(SELECT i.related_event_id FROM dbo.work_events i WITH(UPDLOCK,HOLDLOCK)
   JOIN (SELECT DISTINCT related_event_id FROM inserted WHERE event_type IN ('HANDOVER_ACCEPTED','HANDOVER_REJECTED')) changed
   ON changed.related_event_id=i.related_event_id
   WHERE i.event_type IN ('HANDOVER_ACCEPTED','HANDOVER_REJECTED')
   GROUP BY i.related_event_id HAVING COUNT(*)>1)
   THROW 51208,'A handover offer can have only one decision.',1;
END;
GO
CREATE VIEW dbo.v_work_duration AS
 SELECT branch_id,work_id,user_id,
   SUM(CASE WHEN effective_ended_at IS NOT NULL THEN DATEDIFF_BIG(SECOND,effective_started_at,effective_ended_at) ELSE 0 END) AS completed_seconds,
   SUM(CASE WHEN effective_ended_at IS NULL THEN 1 ELSE 0 END) AS open_intervals
 FROM dbo.work_intervals GROUP BY branch_id,work_id,user_id;
GO
CREATE VIEW dbo.v_backlog_handovers AS
 SELECT offered.work_event_id,offered.branch_id,offered.work_id,offered.actor_user_id AS sender_user_id,
   offered.recipient_user_id,offered.reason,offered.occurred_at,
   decision.event_type AS decision,decision.occurred_at AS decided_at
 FROM dbo.work_events offered LEFT JOIN dbo.work_events decision ON decision.related_event_id=offered.work_event_id
   AND decision.event_type IN ('HANDOVER_ACCEPTED','HANDOVER_REJECTED')
 WHERE offered.event_type='HANDOVER_OFFERED';
GO
CREATE VIEW dbo.v_room_readiness AS
 SELECT room.room_id,room.branch_id,room.operational_status,room.turnover_no,
   CONVERT(bit,CASE WHEN room.operational_status='OCCUPIED' THEN 1 ELSE 0 END) AS is_occupied,
   CASE WHEN room.is_active=0 THEN 'INACTIVE'
    WHEN EXISTS(SELECT 1 FROM dbo.room_blocks b WHERE b.room_id=room.room_id AND b.blocked_at<=SYSUTCDATETIME()
      AND (b.released_at IS NULL OR b.released_at>SYSUTCDATETIME())) THEN 'BLOCKED'
    WHEN cleaning.work_id IS NULL THEN 'PENDING_CLEANING'
    WHEN cleaning.status='IN_PROGRESS' THEN 'CLEANING_IN_PROGRESS'
    WHEN cleaning.status='REWORK_REQUIRED' OR inspection.status='FAILED' OR EXISTS(
      SELECT 1 FROM dbo.work_items r WHERE r.parent_work_id=cleaning.work_id AND r.kind='REWORK'
      AND r.status NOT IN ('COMPLETED','CANCELLED')) THEN 'REWORK_REQUIRED'
    WHEN cleaning.status='COMPLETED' AND inspection.status='PASSED' AND inspection.verified_by IS NOT NULL THEN 'READY'
    WHEN cleaning.status='PENDING_INSPECTION' THEN 'PENDING_INSPECTION'
    ELSE 'PENDING_CLEANING' END AS readiness
 FROM dbo.rooms room LEFT JOIN dbo.work_items cleaning ON cleaning.work_id=room.current_cleaning_work_id
   AND cleaning.kind='CLEANING' AND cleaning.room_id=room.room_id AND cleaning.turnover_no=room.turnover_no
 OUTER APPLY(SELECT TOP(1) i.status,i.verified_by FROM dbo.work_items i
   WHERE i.parent_work_id=cleaning.work_id AND i.kind='INSPECTION' AND i.status<>'CANCELLED'
   ORDER BY i.created_at DESC,i.work_id DESC) inspection;
GO
CREATE TRIGGER dbo.TR_rooms_ready_admission ON dbo.rooms AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted old ON old.room_id=i.room_id
   JOIN dbo.v_room_readiness facts ON facts.room_id=i.room_id
   WHERE i.operational_status IN ('READY','AVAILABLE','OCCUPIED')
     AND (old.room_id IS NULL OR old.operational_status<>i.operational_status)
     AND facts.readiness<>'READY')
   THROW 51209,'Room release or arrival requires passed inspection in the current unblocked turnover.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted old ON old.room_id=i.room_id
   WHERE old.operational_status='OCCUPIED' AND i.operational_status<>'OCCUPIED'
   AND (i.operational_status<>'DIRTY' OR i.turnover_no<=old.turnover_no OR i.current_cleaning_work_id IS NOT NULL))
   THROW 51209,'Departure must leave DIRTY, advance turnover and invalidate the cleaning pointer.',1;
END;
GO
CREATE VIEW dbo.v_material_fulfillment AS
 SELECT request.document_id,request.branch_id,line.line_id,line.item_id,
   line.requested_qty,line.approved_qty,line.conversion_factor,
   COALESCE(issued.qty,0) AS issued_base_qty,
   CASE WHEN COALESCE(line.approved_qty,0)*line.conversion_factor>COALESCE(issued.qty,0)
     THEN COALESCE(line.approved_qty,0)*line.conversion_factor-COALESCE(issued.qty,0) ELSE 0 END AS missing_base_qty
 FROM dbo.inventory_documents request JOIN dbo.inventory_document_lines line ON line.document_id=request.document_id
 OUTER APPLY(SELECT SUM(-m.signed_base_qty) AS qty FROM dbo.inventory_document_lines issue_line
   JOIN dbo.inventory_documents issue ON issue.document_id=issue_line.document_id AND issue.kind='ISSUE'
   JOIN dbo.stock_movements m ON m.line_id=issue_line.line_id
   WHERE issue_line.source_line_id=line.line_id AND NOT EXISTS(
     SELECT 1 FROM dbo.stock_movements reversed WHERE reversed.reversal_of_movement_id=m.movement_id)) issued
 WHERE request.kind='MATERIAL_REQUEST';
GO
CREATE TRIGGER dbo.TR_material_issue_approval ON dbo.stock_movements AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted m JOIN dbo.inventory_document_lines line ON line.line_id=m.line_id
   JOIN dbo.inventory_documents issue ON issue.document_id=line.document_id
   LEFT JOIN dbo.inventory_document_lines source_line ON source_line.line_id=line.source_line_id
   LEFT JOIN dbo.inventory_documents request ON request.document_id=source_line.document_id
   WHERE issue.kind='ISSUE' AND (request.document_id IS NULL OR request.kind<>'MATERIAL_REQUEST'
     OR issue.source_document_id IS NULL OR issue.source_document_id<>request.document_id
     OR request.branch_id<>issue.branch_id OR request.approved_by IS NULL OR request.approved_at IS NULL
     OR request.status NOT IN ('APPROVED','PARTIALLY_ISSUED','FULLY_ISSUED','PARTIALLY_FULFILLED')
     OR request.approved_by=m.created_by OR source_line.approved_qty IS NULL))
   THROW 51210,'Issue requires approved material lines and a storekeeper different from the approver.',1;
 IF EXISTS(SELECT 1 FROM dbo.v_material_fulfillment f
   JOIN (SELECT DISTINCT l.source_line_id FROM inserted m JOIN dbo.inventory_document_lines l ON l.line_id=m.line_id) changed
   ON changed.source_line_id=f.line_id WHERE f.issued_base_qty>f.approved_qty*f.conversion_factor)
   THROW 51211,'Partial issues cannot exceed the approved material quantity.',1;
END;
GO
CREATE TRIGGER dbo.TR_material_approved_lines ON dbo.inventory_document_lines AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted source_line JOIN dbo.inventory_documents request ON request.document_id=source_line.document_id
   JOIN dbo.inventory_document_lines child ON child.source_line_id=source_line.line_id
   JOIN dbo.stock_movements movement ON movement.line_id=child.line_id
   WHERE request.kind='MATERIAL_REQUEST')
   THROW 51214,'Approved source quantities with fulfillment history are immutable; record a new revision/document.',1;
END;
GO
CREATE TRIGGER dbo.TR_findings_passed ON dbo.inspection_findings AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM (SELECT inspection_work_id FROM inserted UNION SELECT inspection_work_id FROM deleted) changed
   JOIN dbo.work_items i ON i.work_id=changed.inspection_work_id WHERE i.kind='INSPECTION' AND i.status='PASSED')
   THROW 51215,'Passed inspection findings are immutable; create a reinspection or incident.',1;
END;
GO
