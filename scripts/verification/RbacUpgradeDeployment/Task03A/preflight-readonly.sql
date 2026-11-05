SELECT DB_NAME() database_name,DB_ID() database_id,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) server_name,
compatibility_level,create_date FROM sys.databases WHERE name=DB_NAME();
SELECT (SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) active_roles,
(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) grants;
SELECT role_id,role_code,is_active FROM dbo.roles ORDER BY role_id;
SELECT data_scope,COUNT(*) grant_count FROM dbo.role_permissions GROUP BY data_scope;
SELECT ur.user_role_id,ur.user_id,r.role_code,ur.branch_id,ur.is_active,ur.expires_at,u.primary_branch_id,u.department_id,u.account_status
FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id JOIN dbo.users u ON u.user_id=ur.user_id
WHERE r.role_code IN ('GENERAL_MANAGER','OPERATIONS_MANAGER','DEPT_MANAGER','WAREHOUSE_KEEPER','STOREKEEPER','HK_MANAGER','HK_SUPERVISOR','HK_INSPECTOR')
ORDER BY r.role_code,ur.branch_id,ur.user_id;
SELECT uba.access_id,uba.user_id,uba.branch_id,uba.access_level,uba.revoked_at FROM dbo.user_branch_access uba
WHERE uba.user_id IN (1,4,5,17,18,19,20,23,25) ORDER BY uba.user_id,uba.branch_id;
SELECT account_status,COUNT(*) user_count FROM dbo.users GROUP BY account_status;
SELECT user_id,COUNT(*) session_count,SUM(CASE WHEN revoked_at IS NULL THEN 1 ELSE 0 END) unrevoked FROM dbo.user_sessions GROUP BY user_id;
SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,CHARACTER_MAXIMUM_LENGTH,IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_NAME='users' AND COLUMN_NAME IN ('failed_login_count','locked_until','temporary_password_expires_at'))
OR (TABLE_NAME='user_sessions' AND COLUMN_NAME='session_key_hash') OR (TABLE_NAME='role_permissions' AND COLUMN_NAME='data_scope');
SELECT department_id,branch_id,department_code,is_active FROM dbo.departments ORDER BY branch_id,department_code;
SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.role_permissions');
