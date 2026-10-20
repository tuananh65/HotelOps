/* HotelOps RBAC read-only verification, SQL Server 2022+. Select expected DB manually. */
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.roles',N'U') IS NULL OR OBJECT_ID(N'dbo.role_permissions',N'U') IS NULL
    THROW 51800, 'Not in a database containing HotelOps RBAC.', 1;

SELECT DB_NAME() AS database_name,
       (SELECT COUNT(*) FROM dbo.roles) AS total_roles,
       (SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) AS active_roles,
       (SELECT COUNT(*) FROM dbo.permissions) AS total_permissions,
       (SELECT COUNT(*) FROM dbo.role_permissions) AS role_permission_grants;

SELECT r.role_code,r.is_active,COUNT(rp.permission_id) AS total_permissions,
       SUM(CASE WHEN rp.data_scope='ALL' THEN 1 ELSE 0 END) AS all_scope,
       SUM(CASE WHEN rp.data_scope='BRANCH' THEN 1 ELSE 0 END) AS branch_scope,
       SUM(CASE WHEN rp.data_scope='OWN' THEN 1 ELSE 0 END) AS own_scope,
       SUM(CASE WHEN rp.data_scope='DEPARTMENT' THEN 1 ELSE 0 END) AS department_scope
FROM dbo.roles r LEFT JOIN dbo.role_permissions rp ON rp.role_id=r.role_id
GROUP BY r.role_code,r.is_active ORDER BY r.role_code;

SELECT 'LEGACY_ACTIVE_ROLE' AS issue,r.role_code AS subject,CAST(NULL AS BIGINT) AS branch_id
FROM dbo.roles r WHERE r.role_code IN ('HK_INSPECTOR','STAFF') AND r.is_active=1
UNION ALL
SELECT 'NON_CANONICAL_ACTIVE_DEPARTMENT',d.department_code,d.branch_id
FROM dbo.departments d WHERE d.is_active=1 AND d.department_code NOT IN ('HK','MT','WH','FO');

SELECT ur.branch_id,r.role_code,COUNT(*) AS active_assignments
FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
JOIN dbo.users u ON u.user_id=ur.user_id
WHERE r.role_code IN ('OPERATIONS_MANAGER','DEPT_MANAGER') AND r.is_active=1
  AND ur.is_active=1 AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
  AND u.account_status='ACTIVE'
GROUP BY ur.branch_id,r.role_code ORDER BY ur.branch_id,r.role_code;

SELECT ur.user_id,u.full_name,ur.branch_id AS role_branch_id,u.primary_branch_id,
       r.role_code,u.account_status
FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
JOIN dbo.users u ON u.user_id=ur.user_id
WHERE ur.is_active=1 AND r.is_active=1 AND (
  ur.branch_id IS NULL AND r.role_code IN ('OPERATIONS_MANAGER','DEPT_MANAGER')
  OR ur.branch_id<>u.primary_branch_id AND r.role_code IN ('OPERATIONS_MANAGER','DEPT_MANAGER')
);

SELECT r.role_code,p.permission_code,rp.data_scope
FROM dbo.role_permissions rp JOIN dbo.roles r ON rp.role_id=r.role_id
JOIN dbo.permissions p ON p.permission_id=rp.permission_id
WHERE r.role_code='OWNER' ORDER BY p.permission_code;
