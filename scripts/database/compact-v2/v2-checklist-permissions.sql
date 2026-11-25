SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated TEST required.',1;
BEGIN TRANSACTION;
INSERT dbo.role_permissions(role_id,permission_id,data_scope)
SELECT r.role_id,p.permission_id,'BRANCH' FROM dbo.roles r CROSS JOIN dbo.permissions p
WHERE r.code IN('HOUSEKEEPING_SUPERVISOR','MAINTENANCE_SUPERVISOR') AND p.code='CHECKLIST_MANAGE'
AND NOT EXISTS(SELECT 1 FROM dbo.role_permissions rp WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);
IF NOT EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type='V2_CHECKLIST_PERMISSIONS' AND entity_key='261125-v1')
INSERT dbo.audit_logs(entity_type,entity_key,action,operation_key,occurred_at,after_json)
VALUES('V2_CHECKLIST_PERMISSIONS','261125-v1','APPLIED',NEWID(),SYSUTCDATETIME(),'{"HK":"CLEANING,INSPECTION","MaintenanceSupervisor":"MAINTENANCE","scope":"BRANCH","kindEnforcedByService":true}');
COMMIT;
