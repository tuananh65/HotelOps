-- Applied only after creating and backing up the isolated synthetic Lead/Staff fixture.
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated fixture required',1;
DECLARE @role bigint=(SELECT role_id FROM roles WHERE code='MAINTENANCE_STAFF');
DELETE FROM role_permissions WHERE role_id=@role;
-- Match the actual role 5 read-only audit; do not copy hypothetical baseline permissions.
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT @role,permission_id,CASE WHEN code='ATTENDANCE_RECORD' THEN 'OWN' ELSE 'BRANCH' END FROM permissions WHERE code IN
('ASSET_VIEW','ATTENDANCE_RECORD','INCIDENT_MANAGE','INCIDENT_VIEW','IR_PROCESS','IR_VIEW','MAINTENANCE_EXECUTE','MAINTENANCE_VIEW','MR_CREATE','MR_VIEW','ROOM_VIEW','TASK_EXECUTE','TASK_VIEW');
SELECT @role role_id;
