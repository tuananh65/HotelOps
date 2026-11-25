SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
-- Read-only comparison. Never return password hashes, stamps, or session tokens.
DECLARE @now datetime2=SYSUTCDATETIME();
SELECT r.code,r.is_active,COUNT(DISTINCT CASE WHEN ur.revoked_at IS NULL AND ur.assigned_at<=@now
 AND (ur.expires_at IS NULL OR ur.expires_at>@now)
 AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL THEN ur.user_id END) active_users
FROM dbo.roles r LEFT JOIN dbo.user_roles ur ON ur.role_id=r.role_id
LEFT JOIN dbo.users u ON u.user_id=ur.user_id
WHERE r.code IN('DEPARTMENT_HEAD','DEPT_MANAGER','OPERATION_MANAGER','MAINTENANCE_STAFF','MAINTENANCE_SUPERVISOR','HK_STAFF')
GROUP BY r.code,r.is_active ORDER BY r.code;
SELECT r.code,p.code permission,rp.data_scope FROM dbo.role_permissions rp
JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id
WHERE r.code IN('DEPARTMENT_HEAD','DEPT_MANAGER','OPERATION_MANAGER','MAINTENANCE_STAFF','MAINTENANCE_SUPERVISOR')
ORDER BY r.code,p.code;
DECLARE @recovered nvarchar(max)=(SELECT TOP(1) after_json FROM dbo.audit_logs
 WHERE entity_type='V2_ROLE_RECOVERY' AND entity_key='261125-v1' AND action='APPLIED' ORDER BY occurred_at DESC);
IF @recovered IS NULL THROW 51991,'Recovery audit not found.',1;
SELECT old.branch_id,COUNT_BIG(*) recovered_users,
 SUM(CASE WHEN EXISTS_ROLE.user_id IS NOT NULL THEN 1 ELSE 0 END) active_maintenance_members,
 SUM(CASE WHEN u.username=source.username AND (u.password_hash=source.password_hash OR (u.password_hash IS NULL AND source.password_hash IS NULL)) THEN 1 ELSE 0 END) credentials_equal_source,
 SUM(CASE WHEN u.account_status=source.account_status AND u.employment_status=source.employment_status THEN 1 ELSE 0 END) account_status_equal_source
FROM OPENJSON(@recovered) WITH(user_id bigint,branch_id bigint) old
JOIN dbo.users u ON u.user_id=old.user_id
JOIN HotelOpsCompactDB.dbo.users source ON source.user_id=old.user_id
OUTER APPLY(SELECT TOP(1) ur.user_id FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
 WHERE ur.user_id=old.user_id AND ur.branch_id=old.branch_id AND r.code='MAINTENANCE_STAFF' AND r.is_active=1
 AND ur.revoked_at IS NULL AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now)) EXISTS_ROLE
GROUP BY old.branch_id;
SELECT entity_type,entity_key,action,occurred_at FROM dbo.audit_logs
WHERE entity_type IN('P0_ROLE_MIGRATION','V2_ROLE_RECOVERY') ORDER BY occurred_at;
