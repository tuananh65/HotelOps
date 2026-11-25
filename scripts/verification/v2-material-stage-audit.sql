SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Exact TEST required',1;
SELECT TOP (30) w.work_id,w.status,e.event_type,e.work_event_id
FROM work_items w JOIN branches b ON b.branch_id=w.branch_id
JOIN work_events e ON e.work_id=w.work_id
WHERE b.code LIKE 'V2HK-%' AND w.kind='INCIDENT'
ORDER BY e.work_event_id DESC;
SELECT TOP (6) w.work_id,f.status workflow_status,f.request_type
FROM work_items w JOIN branches b ON b.branch_id=w.branch_id
JOIN request_workflows f ON f.work_id=w.work_id
WHERE b.code LIKE 'V2HK-%' AND w.kind='INCIDENT'
ORDER BY w.work_id DESC;
