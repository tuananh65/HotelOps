SET NOCOUNT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'
 THROW 51910,'Isolated canonical target required.',1;
IF (SELECT COUNT(*) FROM dbo.roles)<>12 OR (SELECT COUNT(*) FROM dbo.roles WHERE is_active=1)<>10
 OR (SELECT COUNT(*) FROM dbo.permissions)<>62 OR (SELECT COUNT(*) FROM dbo.role_permissions)<>140
 OR EXISTS(SELECT 1 FROM dbo.role_permissions WHERE data_scope='DEPARTMENT')
 THROW 51911,'RBAC catalog counts differ.',1;
IF (SELECT COUNT(*) FROM dbo.branches)<>2 OR (SELECT COUNT(*) FROM dbo.branches WHERE is_active=1 AND branch_code IN('BR001','BR002'))<>2
 OR (SELECT COUNT(*) FROM dbo.departments)<>8
 OR EXISTS(SELECT 1 FROM dbo.branches b WHERE (SELECT COUNT(*) FROM dbo.departments d WHERE d.branch_id=b.branch_id AND d.is_active=1 AND d.department_code IN('HK','MT','WH','FO'))<>4)
 THROW 51912,'Canonical branch/department mismatch.',1;
IF (SELECT COUNT(*) FROM dbo.reference_values WHERE is_active=1 AND group_code IN('PRIORITY','SEVERITY','CLEANING_TYPE','BLOCKER'))<>15
 THROW 51913,'Reference catalog mismatch.',1;
IF (SELECT COUNT(*) FROM dbo.reference_values WHERE group_code='PRIORITY' AND is_active=1 AND value_code IN('LOW','NORMAL','HIGH','CRITICAL'))<>4
 OR EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='PRIORITY' AND value_code NOT IN('LOW','NORMAL','HIGH','CRITICAL'))
 THROW 51917,'SRS GBR-085 priority contract mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='SEVERITY' AND value_code='CRITICAL' AND is_active=1)
 THROW 51918,'SEVERITY CRITICAL missing.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>82
 OR (SELECT COUNT(*) FROM sys.columns WHERE system_type_id=189)<>9
 THROW 51919,'Combined schema table/rowversion count mismatch.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51914,'Disabled or untrusted constraint.',1;
IF COL_LENGTH('dbo.users','failed_login_count') IS NULL OR COL_LENGTH('dbo.users','locked_until') IS NULL
 OR COL_LENGTH('dbo.users','temporary_password_expires_at') IS NULL OR COL_LENGTH('dbo.user_sessions','session_key_hash') IS NULL
 THROW 51915,'Authentication prerequisite missing.',1;
IF EXISTS(SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
 WHERE ur.is_active=1 AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND r.role_code='OPERATIONS_MANAGER'
 GROUP BY ur.user_id HAVING COUNT(DISTINCT ur.branch_id)>1)
 THROW 51916,'OM assigned to multiple branches.',1;
SELECT DB_NAME() database_name,(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,
 (SELECT COUNT(*) FROM sys.columns WHERE system_type_id=189) rowversions,
 (SELECT COUNT(*) FROM dbo.users) users,(SELECT COUNT(*) FROM dbo.user_roles) user_roles,
 (SELECT COUNT(*) FROM dbo.user_sessions) sessions,(SELECT COUNT(*) FROM dbo.reference_values) reference_values;
