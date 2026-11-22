-- User-approved V2 normalization, 2026-11-22. Data only; IDs and schema preserved.
-- Parameters: @Apply bit. The wrapper backs up and verifies before @Apply=1.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestV2Data20261122')
 THROW 51500,'Unexpected database; refuse normalization.',1;
DECLARE @marker nvarchar(150)=N'V2-DATA-20261122';
IF EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type='DataNormalization' AND entity_key=@marker AND action='APPLY')
 BEGIN SELECT 'ALREADY_APPLIED' AS result; RETURN; END;
DECLARE @roles TABLE(old_code varchar(60),new_code varchar(60),new_name nvarchar(150));
INSERT @roles VALUES
 ('RECEPTION','RECEPTION',N'Lễ tân'),
 ('DEMO_HOUSEKEEPER','HK_STAFF',N'Nhân viên buồng phòng'),
 ('DEMO_TECHNICIAN','MAINTENANCE_STAFF',N'Nhân viên bảo trì'),
 ('STOREKEEPER','STOREKEEPER',N'Thủ kho'),
 ('DEPT_MANAGER','DEPT_MANAGER',N'Trưởng bộ phận');
DECLARE @branches TABLE(branch_id bigint,old_code varchar(30),new_code varchar(30),new_name nvarchar(150),new_address nvarchar(500),new_phone varchar(30));
INSERT @branches VALUES
 (413,'DEMO-SG','DVBV',N'Chi nhánh Bùi Viện',N'Bùi Viện','0987654321'),
 (414,'DEMO-DN','DVCT',N'Chi nhánh Cao Thắng',N'Cao Thắng','0987654312');
IF @Apply=0 BEGIN
 SELECT r.role_id,r.code AS old_code,m.new_code,r.name AS old_name,m.new_name FROM roles r JOIN @roles m ON m.old_code=r.code;
 SELECT b.branch_id,b.code AS old_code,m.new_code,b.name AS old_name,m.new_name,m.new_address,m.new_phone FROM branches b JOIN @branches m ON m.branch_id=b.branch_id;
 SELECT r.role_id,r.code,'DEACTIVATE; preserve grants and assigned users' AS change FROM roles r WHERE code IN ('DEMO_MANAGER','DEMO_AUDITOR');
 SELECT r.code,p.code AS revoke_permission,rp.data_scope FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='SYS_ADMIN' AND p.code IN ('ASSET_MANAGE','ITEM_MASTER_MANAGE');
 RETURN;
END;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource='HotelOps-V2-DataNormalization',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0 THROW 51501,'Cannot acquire normalization lock.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>40 OR (SELECT COUNT(*) FROM sys.foreign_keys)<>160 THROW 51502,'Schema differs from verified baseline.',1;
IF (SELECT COUNT(*) FROM roles)<>12 OR (SELECT COUNT(*) FROM roles WHERE is_active=1)<>12 OR (SELECT COUNT(*) FROM permissions)<>67 OR (SELECT COUNT(*) FROM role_permissions)<>180 THROW 51503,'Role/permission baseline changed.',1;
IF (SELECT COUNT(*) FROM branches)<>2 OR (SELECT COUNT(*) FROM departments)<>8 THROW 51504,'Branch/department baseline changed.',1;
IF (SELECT COUNT(*) FROM roles r JOIN @roles m ON r.code=m.old_code)<>5 THROW 51505,'Source role mapping missing.',1;
IF EXISTS(SELECT 1 FROM @roles m JOIN roles r ON r.code=m.new_code WHERE m.old_code<>m.new_code) THROW 51506,'Target role code already exists.',1;
IF (SELECT COUNT(*) FROM branches b JOIN @branches m ON b.branch_id=m.branch_id AND b.code=m.old_code)<>2 THROW 51507,'Source branch mapping changed.',1;
IF EXISTS(SELECT 1 FROM branches WHERE code IN ('DVBV','DVCT')) THROW 51508,'Target branch code already exists.',1;
IF (SELECT COUNT(*) FROM roles WHERE code IN ('DEMO_MANAGER','DEMO_AUDITOR') AND is_active=1)<>2 THROW 51509,'Demo role baseline changed.',1;
IF EXISTS(SELECT 1 FROM workday_assignments a JOIN user_roles ur ON ur.user_id=a.user_id JOIN roles r ON r.role_id=ur.role_id WHERE r.code IN ('DEMO_MANAGER','DEMO_AUDITOR') AND ur.revoked_at IS NULL AND a.status IN ('PLANNED','WORKING')) THROW 51510,'Demo actor has open shift assignments.',1;
IF EXISTS(SELECT 1 FROM work_assignments a JOIN user_roles ur ON ur.user_id=a.assignee_user_id JOIN roles r ON r.role_id=ur.role_id WHERE r.code IN ('DEMO_MANAGER','DEMO_AUDITOR') AND ur.revoked_at IS NULL AND a.assignment_status='ACTIVE') THROW 51511,'Demo actor has active work assignments.',1;
IF EXISTS(SELECT 1 FROM workday_shifts s JOIN roles r ON r.role_id=s.role_id WHERE r.code IN ('DEMO_MANAGER','DEMO_AUDITOR') AND s.is_active=1) THROW 51512,'Demo role still used by active shift definitions.',1;
IF NOT EXISTS(SELECT 1 FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id JOIN users u ON u.user_id=ur.user_id WHERE r.code='SYS_ADMIN' AND ur.branch_id IS NULL AND ur.revoked_at IS NULL AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL) THROW 51513,'No active global administrator.',1;
SELECT * INTO #oldRoles FROM roles;
SELECT * INTO #oldGrants FROM role_permissions;
SELECT * INTO #oldUsersRoles FROM user_roles;
SELECT * INTO #oldDepartments FROM departments;
SELECT * INTO #oldShifts FROM workday_shifts;
SELECT * INTO #oldBranches FROM branches;
DECLARE @before nvarchar(max)=(SELECT
 (SELECT role_id,code,name,is_active FROM roles ORDER BY role_id FOR JSON PATH) AS roles,
 (SELECT branch_id,code,name,address,phone FROM branches ORDER BY branch_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS branches,
 (SELECT rp.grant_id,rp.role_id,rp.permission_id,rp.data_scope FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='SYS_ADMIN' AND p.code IN ('ASSET_MANAGE','ITEM_MASTER_MANAGE') FOR JSON PATH) AS removed_grants
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
UPDATE r SET code=m.new_code,name=m.new_name FROM roles r JOIN @roles m ON r.code=m.old_code;
IF @@ROWCOUNT<>5 THROW 51514,'Unexpected normalized role count.',1;
UPDATE roles SET is_active=0 WHERE code IN ('DEMO_MANAGER','DEMO_AUDITOR');
IF @@ROWCOUNT<>2 THROW 51515,'Unexpected deactivated role count.',1;
UPDATE b SET code=m.new_code,name=m.new_name,address=m.new_address,phone=m.new_phone FROM branches b JOIN @branches m ON b.branch_id=m.branch_id;
IF @@ROWCOUNT<>2 THROW 51516,'Unexpected normalized branch count.',1;
DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='SYS_ADMIN' AND p.code IN ('ASSET_MANAGE','ITEM_MASTER_MANAGE') AND rp.data_scope='ALL';
IF @@ROWCOUNT<>2 THROW 51517,'Expected exactly two Admin grants revoked.',1;
IF (SELECT COUNT(*) FROM role_permissions)<>178 OR (SELECT COUNT(*) FROM roles WHERE is_active=1)<>10 THROW 51518,'Unexpected final counts.',1;
IF EXISTS(SELECT role_id FROM #oldRoles EXCEPT SELECT role_id FROM roles) OR EXISTS(SELECT role_id FROM roles EXCEPT SELECT role_id FROM #oldRoles) THROW 51519,'Role identity changed.',1;
IF EXISTS(SELECT * FROM #oldUsersRoles EXCEPT SELECT * FROM user_roles) OR EXISTS(SELECT * FROM user_roles EXCEPT SELECT * FROM #oldUsersRoles) THROW 51520,'User role assignments changed.',1;
IF EXISTS(SELECT * FROM #oldDepartments EXCEPT SELECT * FROM departments) OR EXISTS(SELECT * FROM departments EXCEPT SELECT * FROM #oldDepartments) THROW 51521,'Department data changed.',1;
IF EXISTS(SELECT * FROM #oldShifts EXCEPT SELECT * FROM workday_shifts) OR EXISTS(SELECT * FROM workday_shifts EXCEPT SELECT * FROM #oldShifts) THROW 51522,'Shift definitions changed.',1;
IF EXISTS(SELECT grant_id,role_id,permission_id,data_scope FROM role_permissions EXCEPT SELECT grant_id,role_id,permission_id,data_scope FROM #oldGrants) THROW 51523,'Unexpected grant added or modified.',1;
IF EXISTS(SELECT branch_id,timezone,is_active FROM branches EXCEPT SELECT branch_id,timezone,is_active FROM #oldBranches) THROW 51524,'Branch timezone or state changed.',1;
DECLARE @after nvarchar(max)=(SELECT
 (SELECT role_id,code,name,is_active FROM roles ORDER BY role_id FOR JSON PATH) AS roles,
 (SELECT branch_id,code,name,address,phone FROM branches ORDER BY branch_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS branches,
 N'User approved deactivation of two demo roles and revocation of two Admin grants; no reassignment' AS approval,
 N'HotelOps_Codex_Dieu_Phoi_Trien_Khai_Project2_V2.docx + owner-edited workbook + direct chat approval 2026-11-22' AS source
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json)
VALUES(NULL,NEWID(),'DataNormalization',@marker,'APPLY',@before,@after);
COMMIT;
SELECT 'APPLIED' AS result,(SELECT COUNT(*) FROM roles) AS roles,(SELECT COUNT(*) FROM roles WHERE is_active=1) AS active_roles,(SELECT COUNT(*) FROM permissions) AS permissions,(SELECT COUNT(*) FROM role_permissions) AS grants,(SELECT COUNT(*) FROM branches) AS branches,(SELECT COUNT(*) FROM departments) AS departments;
