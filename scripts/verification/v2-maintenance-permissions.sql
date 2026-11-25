IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'TEST required.',1;
SELECT r.code role,p.code permission,rp.data_scope FROM dbo.roles r JOIN dbo.role_permissions rp ON rp.role_id=r.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id
WHERE r.code IN('MAINTENANCE_SUPERVISOR','MAINTENANCE_STAFF','HOUSEKEEPING_SUPERVISOR','OPERATION_MANAGER') AND (p.code LIKE '%CHECKLIST%' OR p.code LIKE '%TEMPLATE%' OR p.code='MAINTENANCE_REVIEW');
SELECT code FROM dbo.permissions WHERE code LIKE '%CHECKLIST%' OR code LIKE '%TEMPLATE%' OR code='MAINTENANCE_REVIEW';
