-- Read-only preflight. Missing objects are blockers, not permission to create them.
SET NOCOUNT ON;
IF DB_NAME() <> N'HotelOpsDB' THROW 51000, 'Run this preflight against HotelOpsDB.', 1;
SELECT DB_NAME() AS database_name, CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS server_name;
SELECT v.workflow, v.required_object,
       CASE WHEN OBJECT_ID(v.required_object, 'U') IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS result
FROM (VALUES
  ('WF5 material approval', 'dbo.material_request_approval_cycles'),
  ('WF5 material approval', 'dbo.material_request_approval_line_snapshots'),
  ('WF4 maintenance materials', 'dbo.maintenance_material_usages'),
  ('WF6 workday', 'dbo.workdays'),
  ('WF6 workday', 'dbo.work_intervals')
) v(workflow, required_object);
SELECT v.workflow, v.table_name, v.column_name,
       CASE WHEN COL_LENGTH(v.table_name, v.column_name) IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS result
FROM (VALUES
  ('WF2 cleaning', 'dbo.cleaning_tasks', 'row_version'),
  ('WF2 cleaning', 'dbo.cleaning_tasks', 'workflow_status'),
  ('WF3 inspection', 'dbo.room_inspections', 'row_version'),
  ('WF4 maintenance', 'dbo.maintenance_works', 'row_version'),
  ('WF5 material request', 'dbo.material_requests', 'row_version')
) v(workflow, table_name, column_name);
SELECT v.marker,
       CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties p WHERE p.class=0 AND p.name=v.marker)
       THEN 'PRESENT_NOT_YET_VERIFIED' ELSE 'MISSING' END AS result
FROM (VALUES ('HotelOps.Checklists.V1'), ('HotelOps.Housekeeping.V1'), ('HotelOps.Progress.V1')) v(marker);
SELECT 'rooms' AS entity, COUNT_BIG(*) AS records FROM dbo.rooms
UNION ALL SELECT 'cleaning_tasks', COUNT_BIG(*) FROM dbo.cleaning_tasks
UNION ALL SELECT 'room_inspections', COUNT_BIG(*) FROM dbo.room_inspections
UNION ALL SELECT 'incidents', COUNT_BIG(*) FROM dbo.incidents
UNION ALL SELECT 'maintenance_works', COUNT_BIG(*) FROM dbo.maintenance_works
UNION ALL SELECT 'material_requests', COUNT_BIG(*) FROM dbo.material_requests
UNION ALL SELECT 'shift_assignments', COUNT_BIG(*) FROM dbo.shift_assignments;
SELECT MIN(shift_date) AS earliest_shift, MAX(shift_date) AS latest_shift FROM dbo.work_shifts;
-- PRESENT does not certify the complete schema, permissions, or runtime workflow.
