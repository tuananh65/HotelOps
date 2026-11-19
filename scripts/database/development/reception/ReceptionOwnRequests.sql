-- Approved by user: RECEPTION may read their own IR requests.
-- Caller owns SERIALIZABLE transaction and backup/rollback verification.
SET XACT_ABORT ON;
DECLARE @now datetime2(7)=SYSUTCDATETIME(), @role bigint;
SELECT @role=role_id FROM dbo.roles WITH(UPDLOCK,HOLDLOCK)
WHERE role_code='RECEPTION' AND is_active=1;
IF @role IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id
 WHERE u.username='huy.dinh' AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
 AND ur.role_id=@role AND ur.is_active=1 AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now))
 THROW 51991,'Active Huy reception assignment required.',1;
IF (SELECT COUNT(*) FROM dbo.permissions WHERE permission_code IN('IR_VIEW'))<>1
 THROW 51991,'Existing permission catalog required.',1;
IF EXISTS(SELECT 1 FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id
 WHERE rp.role_id=@role AND p.permission_code IN('IR_VIEW') AND rp.data_scope<>'OWN')
 THROW 51991,'Conflicting scope; do not overwrite.',1;
DECLARE @before nvarchar(max)=(SELECT p.permission_code,rp.data_scope FROM dbo.role_permissions rp
 JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE role_id=@role ORDER BY p.permission_code FOR JSON PATH);
INSERT dbo.role_permissions(role_id,permission_id,data_scope,granted_at)
 SELECT @role,p.permission_id,'OWN',@now FROM dbo.permissions p WHERE p.permission_code IN('IR_VIEW')
 AND NOT EXISTS(SELECT 1 FROM dbo.role_permissions WHERE role_id=@role AND permission_id=p.permission_id);
DECLARE @after nvarchar(max)=(SELECT p.permission_code,rp.data_scope FROM dbo.role_permissions rp
 JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE role_id=@role ORDER BY p.permission_code FOR JSON PATH);
IF @before<>@after INSERT dbo.audit_logs(entity_type,entity_id,action,old_values,new_values,changed_fields,request_id,user_agent,occurred_at)
 VALUES('roles',CONVERT(nvarchar(30),@role),'UPDATE',@before,@after,N'["role_permissions"]',
 'huy-dinh-access-fix-20261120',N'User-approved local repair; add independent IR_VIEW OWN. No interactive administrator impersonation.',@now);
