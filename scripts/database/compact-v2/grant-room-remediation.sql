-- Explicit approval required for primary. Add only the three reviewed existing grants.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51400, 'Compact database required.', 1;
DECLARE @role bigint=(SELECT role_id FROM dbo.roles WHERE code='DEPT_MANAGER');
IF @role IS NULL THROW 51400, 'DEPT_MANAGER is missing.', 1;
IF (SELECT COUNT(*) FROM dbo.permissions WHERE code IN ('INCIDENT_VIEW','INCIDENT_TRIAGE','MR_CREATE'))<>3
 THROW 51400, 'Required existing permission missing.', 1;
IF EXISTS(SELECT 1 FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id
 WHERE rp.role_id=@role AND p.code IN ('INCIDENT_VIEW','INCIDENT_TRIAGE','MR_CREATE') AND rp.data_scope<>'BRANCH')
 THROW 51400, 'Conflicting grant scope; manual review required.', 1;
INSERT dbo.role_permissions(role_id,permission_id,data_scope)
 SELECT @role,p.permission_id,'BRANCH' FROM dbo.permissions p
 WHERE p.code IN ('INCIDENT_VIEW','INCIDENT_TRIAGE','MR_CREATE')
 AND NOT EXISTS(SELECT 1 FROM dbo.role_permissions rp WHERE rp.role_id=@role AND rp.permission_id=p.permission_id);
SELECT r.code role_code,p.code permission_code,rp.data_scope
 FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id
 WHERE rp.role_id=@role AND p.code IN ('INCIDENT_VIEW','INCIDENT_TRIAGE','MR_CREATE');
COMMIT;
