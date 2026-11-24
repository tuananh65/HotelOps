SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompactWorkdayTestP0%' THROW 51983,'Isolated P0 TEST required.',1;
-- Add routing metadata only. Existing incident, events, stock and room history are unchanged.
;WITH reports AS(
 SELECT w.work_id,w.branch_id,w.status work_status,JSON_VALUE(report.snapshot_json,'$.Kind') kind,latest.event_type stage
 FROM dbo.work_items w
 CROSS APPLY(SELECT TOP(1) e.snapshot_json FROM dbo.work_events e WHERE e.work_id=w.work_id AND e.event_type='ROOM_ISSUE_REPORTED' AND ISJSON(e.snapshot_json)=1 ORDER BY e.work_event_id)report
 CROSS APPLY(SELECT TOP(1) e.event_type FROM dbo.work_events e WHERE e.work_id=w.work_id AND e.event_type LIKE 'ROOM_ISSUE_%' ORDER BY e.work_event_id DESC)latest
 WHERE w.kind='INCIDENT' AND NOT EXISTS(SELECT 1 FROM dbo.request_workflows r WHERE r.work_id=w.work_id)
), routed AS(
 SELECT *,CASE WHEN work_status='CLOSED' THEN 'COMPLETED' WHEN work_status='CANCELLED' THEN 'CANCELLED'
  WHEN stage='ROOM_ISSUE_REJECTED' THEN 'REJECTED'
  WHEN stage IN('ROOM_ISSUE_INSPECTION_REQUESTED','ROOM_ISSUE_REMEDY_REPORTED') THEN 'PENDING_REINSPECTION'
  WHEN stage='ROOM_ISSUE_MATERIAL_REQUESTED' THEN 'PENDING_STOREKEEPER'
  WHEN stage='ROOM_ISSUE_REPAIR_REQUESTED' THEN 'PENDING_MAINTENANCE_SUPERVISOR'
  WHEN stage='ROOM_ISSUE_REPAIR_CREATED' THEN 'ACCEPTED'
  WHEN stage='ROOM_ISSUE_INSPECTION_FAILED' THEN 'REWORK_REQUIRED' ELSE 'PENDING_SUPERVISOR' END flow_status
 FROM reports WHERE kind IN('BROKEN','MISSING')
)
INSERT dbo.request_workflows(work_id,branch_id,request_type,status,queue_role_id,correlation_id,updated_at)
SELECT work_id,branch_id,CASE WHEN kind='BROKEN' THEN 'ROOM_EQUIPMENT_DAMAGE' ELSE 'ROOM_DURABLE_ITEM_MISSING' END,flow_status,
 CASE WHEN flow_status IN('COMPLETED','CANCELLED','REJECTED') THEN NULL ELSE
 (SELECT role_id FROM dbo.roles WHERE is_active=1 AND code=CASE WHEN flow_status='PENDING_STOREKEEPER' THEN 'STOREKEEPER'
  WHEN flow_status IN('PENDING_MAINTENANCE_SUPERVISOR','ACCEPTED') THEN 'MAINTENANCE_SUPERVISOR' ELSE 'HOUSEKEEPING_SUPERVISOR' END) END,
 NEWID(),SYSUTCDATETIME() FROM routed;
