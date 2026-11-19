SET XACT_ABORT ON;
DECLARE @now datetime2(7)=SYSUTCDATETIME(),@user bigint,@branch bigint,@department bigint,@admin bigint;
SELECT @user=u.user_id,@branch=u.primary_branch_id,@department=u.department_id
FROM dbo.users u WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.departments d ON d.department_id=u.department_id AND d.branch_id=u.primary_branch_id AND d.is_active=1
JOIN dbo.branches b ON b.branch_id=u.primary_branch_id AND b.is_active=1
WHERE u.username='huy.dinh' AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
AND d.department_code='FO'
AND EXISTS(SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
 WHERE ur.user_id=u.user_id AND ur.branch_id=u.primary_branch_id AND ur.is_active=1 AND r.is_active=1 AND r.role_code='RECEPTION'
 AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now))
AND EXISTS(SELECT 1 FROM dbo.user_branch_access a WHERE a.user_id=u.user_id AND a.branch_id=u.primary_branch_id AND a.revoked_at IS NULL AND a.granted_at<=@now);
IF @user IS NULL THROW 51991,'Existing active Reception branch and FO department required.',1;
SELECT @admin=u.user_id FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id
WHERE u.username='sysadmin' AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
AND ur.is_active=1 AND r.is_active=1 AND r.role_code='SYS_ADMIN' AND ur.branch_id IS NULL AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now);
IF @admin IS NULL THROW 51991,'Local administrative audit principal required.',1;
IF EXISTS(SELECT 1 FROM dbo.employee_department_memberships WHERE user_id=@user AND (department_id<>@department OR branch_id<>@branch OR revoked_at IS NOT NULL OR granted_at>@now))
 THROW 51991,'Conflicting or revoked membership; no overwrite.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.employee_department_memberships WHERE user_id=@user AND branch_id=@branch AND department_id=@department)
BEGIN
 INSERT dbo.employee_department_memberships(user_id,branch_id,department_id,granted_at,recorded_by,provenance)
 VALUES(@user,@branch,@department,@now,@admin,N'Huy access repair: mirror existing active FO primary department and branch; user requested Reception workflow repair.');
 INSERT dbo.audit_logs(branch_id,entity_type,entity_id,action,new_values,request_id,user_agent,occurred_at)
 SELECT @branch,'employee_department_memberships',CONVERT(nvarchar(30),@user),'CREATE',
 (SELECT @user AS userId,@branch AS branchId,@department AS departmentId,@now AS grantedAt FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 'huy-dinh-membership-fix-20261120',N'User-requested local repair; administrative recorder, no interactive impersonation.',@now;
END;
