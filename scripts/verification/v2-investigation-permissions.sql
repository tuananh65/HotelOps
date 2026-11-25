IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'TEST required',1;
SELECT r.code role,p.code permission,rp.data_scope FROM dbo.roles r
JOIN dbo.role_permissions rp ON rp.role_id=r.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id
WHERE r.code IN('HOUSEKEEPING_SUPERVISOR','MAINTENANCE_SUPERVISOR','MAINTENANCE_STAFF')
AND p.code IN('INCIDENT_VIEW','INCIDENT_MANAGE','IR_PROCESS','MAINTENANCE_REVIEW') ORDER BY r.code,p.code;
