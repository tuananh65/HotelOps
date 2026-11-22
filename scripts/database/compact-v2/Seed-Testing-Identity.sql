/* Test identities only. Called after the empty-store bootstrap by Seed-Testing.ps1.
   No schema changes, no access to HotelOpsDB, no password literals. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51200,'Compact database required.',1;
IF (SELECT COUNT(*) FROM users)<>1 OR NOT EXISTS(SELECT 1 FROM users WHERE username='demo_admin')
 THROW 51201,'Expected only the dedicated demo bootstrap administrator.',1;
IF EXISTS(SELECT 1 FROM branches) THROW 51202,'Demo identity fixture requires empty branches.',1;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='demo_admin');
INSERT branches(code,name,address) VALUES
 ('DEMO-SG',N'Khách sạn mẫu Sài Gòn',N'Dữ liệu giả lập phục vụ kiểm thử'),
 ('DEMO-DN',N'Khách sạn mẫu Đà Nẵng',N'Dữ liệu giả lập phục vụ kiểm thử');
INSERT departments(branch_id,code,name)
 SELECT b.branch_id,d.code,d.name FROM branches b CROSS JOIN (VALUES
 ('HK',N'Buồng phòng'),('ENG',N'Kỹ thuật'),('FO',N'Lễ tân'),('STORE',N'Kho vật tư'))d(code,name);
DECLARE @roles TABLE(code varchar(60),name nvarchar(150),grants varchar(max));
INSERT @roles VALUES
 ('DEMO_MANAGER',N'Giám sát vận hành mẫu','ROOM_VIEW,ROOM_BLOCK_MANAGE,CLEANING_VIEW,CLEANING_ASSIGN,INSPECTION_VIEW,INSPECTION_PERFORM,DEFECT_RESOLVE,TASK_VIEW,TASK_MANAGE,TASK_EXECUTE,INCIDENT_VIEW,INCIDENT_CREATE,INCIDENT_TRIAGE,INCIDENT_MANAGE,INCIDENT_ESCALATION_HANDLE,MAINTENANCE_VIEW,MAINTENANCE_ASSIGN,MAINTENANCE_REVIEW,PM_MANAGE,IR_VIEW,IR_CREATE,IR_PROCESS,IR_OVERDUE_HANDLE,CHECKLIST_MANAGE,ASSET_VIEW,ASSET_MANAGE,ASSET_TRANSFER,ASSET_RETIRE,MR_VIEW,INVENTORY_VIEW,DASHBOARD_VIEW,REPORT_VIEW,AUDIT_VIEW'),
 ('RECEPTION',N'Lễ tân mẫu','ROOM_VIEW,ROOM_CHECKIN,ROOM_CHECKOUT,ROOM_OCCUPANCY_MOVE,INCIDENT_VIEW,INCIDENT_CREATE,IR_CREATE,IR_VIEW'),
 ('DEMO_HOUSEKEEPER',N'Nhân viên buồng phòng mẫu','ROOM_VIEW,CLEANING_VIEW,CLEANING_EXECUTE,TASK_VIEW,TASK_EXECUTE,MR_VIEW,MR_CREATE,IR_VIEW,IR_CREATE,IR_PROCESS,INCIDENT_VIEW,INCIDENT_CREATE'),
 ('DEMO_TECHNICIAN',N'Nhân viên kỹ thuật mẫu','ROOM_VIEW,ASSET_VIEW,MAINTENANCE_VIEW,MAINTENANCE_EXECUTE,INCIDENT_VIEW,INCIDENT_MANAGE,TASK_VIEW,TASK_EXECUTE,IR_VIEW,IR_PROCESS,MR_VIEW,MR_CREATE'),
 ('STOREKEEPER',N'Thủ kho mẫu','INVENTORY_VIEW,ITEM_MASTER_MANAGE,STOCK_RECEIPT_POST,STOCK_ISSUE_POST,STOCK_RETURN_POST,STOCK_TRANSFER_POST,STOCKTAKE_PERFORM,STOCK_ADJUST_POST,STOCK_REVERSE,MR_VIEW,MR_FULFILL,REPLENISHMENT_VIEW,REPLENISHMENT_CREATE,REPLENISHMENT_RECEIVE,REPLENISHMENT_CLOSE'),
 ('DEPT_MANAGER',N'Trưởng bộ phận mẫu','ROOM_VIEW,MR_VIEW,MR_APPROVE_L1,MR_APPROVE_FALLBACK,INVENTORY_VIEW,DASHBOARD_VIEW,REPORT_VIEW'),
 ('DEMO_AUDITOR',N'Người xem báo cáo mẫu','ROOM_VIEW,CLEANING_VIEW,INSPECTION_VIEW,TASK_VIEW,INCIDENT_VIEW,MAINTENANCE_VIEW,IR_VIEW,MR_VIEW,INVENTORY_VIEW,ASSET_VIEW,DASHBOARD_VIEW,REPORT_VIEW,AUDIT_VIEW');
IF EXISTS(SELECT 1 FROM @roles r CROSS APPLY STRING_SPLIT(r.grants,',') g WHERE NOT EXISTS(SELECT 1 FROM permissions p WHERE p.code=g.value))
 THROW 51203,'Fixture contains an unknown permission.',1;
INSERT roles(code,name,description) SELECT code,name,N'DEMO seed v1: test accounts only' FROM @roles;
INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT r.role_id,p.permission_id,'BRANCH' FROM @roles spec JOIN roles r ON r.code=spec.code
 CROSS APPLY STRING_SPLIT(spec.grants,',') g JOIN permissions p ON p.code=g.value;
-- The bootstrap administrator manages global shared catalogs, but receives no operational privileges.
INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT r.role_id,p.permission_id,'ALL' FROM roles r CROSS JOIN permissions p
 WHERE r.code='SYS_ADMIN' AND p.code IN ('ITEM_MASTER_MANAGE','ASSET_MANAGE')
 AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);
DECLARE @people TABLE(username nvarchar(100),full_name nvarchar(200),branch_code varchar(30),dept varchar(30),role_code varchar(60));
INSERT @people VALUES
 ('demo_manager',N'Giám sát Sài Gòn','DEMO-SG','HK','DEMO_MANAGER'),
 ('demo_reception',N'Lễ tân Sài Gòn','DEMO-SG','FO','RECEPTION'),
 ('demo_hk1',N'Buồng phòng mẫu 01','DEMO-SG','HK','DEMO_HOUSEKEEPER'),
 ('demo_hk2',N'Buồng phòng mẫu 02','DEMO-SG','HK','DEMO_HOUSEKEEPER'),
 ('demo_tech',N'Kỹ thuật Sài Gòn','DEMO-SG','ENG','DEMO_TECHNICIAN'),
 ('demo_store',N'Thủ kho Sài Gòn','DEMO-SG','STORE','STOREKEEPER'),
 ('demo_head',N'Trưởng bộ phận Sài Gòn','DEMO-SG','HK','DEPT_MANAGER'),
 ('demo_auditor',N'Kiểm soát viên Sài Gòn','DEMO-SG','FO','DEMO_AUDITOR'),
 ('demo_dn_manager',N'Giám sát Đà Nẵng','DEMO-DN','HK','DEMO_MANAGER'),
 ('demo_dn_hk',N'Buồng phòng Đà Nẵng','DEMO-DN','HK','DEMO_HOUSEKEEPER');
INSERT users(employee_code,username,full_name,email,password_hash,account_status,employment_status)
 SELECT UPPER(p.username),p.username,p.full_name,p.username+'@example.invalid',a.password_hash,'ACTIVE','ACTIVE'
 FROM @people p CROSS JOIN users a WHERE a.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,is_primary,recorded_by,provenance)
 SELECT u.user_id,b.branch_id,'BRANCH_ACCESS',1,@admin,N'DEMO seed v1'
 FROM @people p JOIN users u ON u.username=p.username JOIN branches b ON b.code=p.branch_code;
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,provenance)
 SELECT u.user_id,b.branch_id,d.department_id,'EMPLOYMENT',@admin,N'DEMO seed v1'
 FROM @people p JOIN users u ON u.username=p.username JOIN branches b ON b.code=p.branch_code
 JOIN departments d ON d.branch_id=b.branch_id AND d.code=p.dept;
INSERT user_roles(user_id,role_id,branch_id,assigned_by)
 SELECT u.user_id,r.role_id,b.branch_id,@admin FROM @people p JOIN users u ON u.username=p.username
 JOIN roles r ON r.code=p.role_code JOIN branches b ON b.code=p.branch_code;
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,provenance)
 SELECT u.user_id,b.branch_id,d.department_id,'DEPARTMENT_AUTHORITY',@admin,N'DEMO seed v1'
 FROM @people p JOIN users u ON u.username=p.username JOIN branches b ON b.code=p.branch_code
 JOIN departments d ON d.branch_id=b.branch_id
 WHERE (p.role_code='DEMO_MANAGER' AND d.code IN ('HK','ENG')) OR (p.role_code='DEPT_MANAGER' AND d.code IN ('HK','STORE'));
UPDATE d SET manager_user_id=u.user_id FROM departments d JOIN branches b ON b.branch_id=d.branch_id
 JOIN users u ON u.username='demo_head' WHERE b.code='DEMO-SG' AND d.code IN ('HK','STORE');
INSERT audit_logs(actor_user_id,entity_type,entity_key,action,operation_key)
 VALUES(@admin,'Seed','testing-v1','DEMO_IDENTITIES_CREATED',NEWID());
COMMIT;
