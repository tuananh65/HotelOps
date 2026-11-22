-- Approved V10 role mapping, data only. Run through Apply-V10Roles.ps1 (backup first).
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51420,'Compact database required.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>40 THROW 51421,'Expected 40 tables.',1;
IF EXISTS(SELECT 1 FROM audit_logs WHERE entity_type='RoleMapping' AND entity_key='V10-20261122' AND action='APPLY') RETURN;
BEGIN TRANSACTION;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='demo_admin'),@branch bigint=(SELECT branch_id FROM branches WHERE code='DEMO-SG');
IF @admin IS NULL OR @branch IS NULL THROW 51422,'Expected demo administrator and primary branch.',1;
IF EXISTS(SELECT 1 FROM workday_assignments a JOIN workday_shifts s ON a.shift_id=s.shift_id JOIN roles r ON r.role_id=s.role_id
 WHERE r.code='DEMO_MANAGER' AND a.status IN ('PLANNED','WORKING')) THROW 51423,'Resolve scheduled/active supervisor shifts before role remapping.',1;
DECLARE @spec TABLE(code varchar(60),name nvarchar(150),scope varchar(20),grants varchar(max));
INSERT @spec VALUES
('OPERATION_MANAGER',N'Quản lý vận hành','BRANCH','DASHBOARD_VIEW,ROOM_VIEW,ROOM_BLOCK_MANAGE,INCIDENT_VIEW,INCIDENT_CREATE,INCIDENT_TRIAGE,INCIDENT_MANAGE,INCIDENT_ESCALATION_HANDLE,MAINTENANCE_VIEW,ASSET_VIEW,ASSET_MANAGE,ASSET_TRANSFER,ASSET_RETIRE,INVENTORY_VIEW,IR_VIEW,IR_CREATE,IR_PROCESS,IR_OVERDUE_HANDLE,REPORT_VIEW,AUDIT_VIEW,TASK_VIEW,CLEANING_VIEW,INSPECTION_VIEW,MR_VIEW'),
('HOUSEKEEPING_SUPERVISOR',N'Giám sát buồng phòng','BRANCH','DASHBOARD_VIEW,ROOM_VIEW,CLEANING_VIEW,CLEANING_ASSIGN,INSPECTION_VIEW,INSPECTION_PERFORM,DEFECT_RESOLVE,INCIDENT_VIEW,INCIDENT_CREATE,CHECKLIST_MANAGE,ATTENDANCE_RECORD'),
('MAINTENANCE_LEAD',N'Trưởng nhóm bảo trì','BRANCH','DASHBOARD_VIEW,ROOM_VIEW,INCIDENT_VIEW,INCIDENT_CREATE,INCIDENT_TRIAGE,INCIDENT_MANAGE,INCIDENT_ESCALATION_HANDLE,MAINTENANCE_VIEW,MAINTENANCE_ASSIGN,MAINTENANCE_REVIEW,PM_MANAGE,ASSET_VIEW,MR_VIEW,ATTENDANCE_RECORD'),
('OWNER',N'Chủ sở hữu','ALL','DASHBOARD_VIEW,REPORT_VIEW,ROOM_VIEW,TASK_VIEW,CLEANING_VIEW,INSPECTION_VIEW,INCIDENT_VIEW,MAINTENANCE_VIEW,IR_VIEW,INVENTORY_VIEW,MR_VIEW,ASSET_VIEW');
IF EXISTS(SELECT 1 FROM roles r JOIN @spec s ON s.code=r.code) THROW 51424,'Role code already exists without applied mapping marker; inspect before proceeding.',1;
IF EXISTS(SELECT 1 FROM @spec s CROSS APPLY STRING_SPLIT(s.grants,',') g WHERE NOT EXISTS(SELECT 1 FROM permissions p WHERE p.code=g.value AND p.is_active=1)) THROW 51425,'Unknown/inactive permission.',1;
INSERT roles(code,name,description) SELECT code,name,N'V10 mapping approved 2026-11-22' FROM @spec;
INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT r.role_id,p.permission_id,CASE WHEN p.code='ATTENDANCE_RECORD' THEN 'OWN' ELSE s.scope END
 FROM @spec s JOIN roles r ON r.code=s.code CROSS APPLY STRING_SPLIT(s.grants,',') g JOIN permissions p ON p.code=g.value;
INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT r.role_id,p.permission_id,CASE WHEN r.code='SYS_ADMIN' THEN 'ALL' ELSE 'BRANCH' END FROM roles r CROSS JOIN permissions p
 WHERE ((r.code='SYS_ADMIN' AND p.code='AUDIT_VIEW') OR (r.code='DEPT_MANAGER' AND p.code IN ('TASK_VIEW','TASK_MANAGE','IR_VIEW','IR_CREATE','IR_PROCESS','IR_OVERDUE_HANDLE')))
 AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);
DECLARE @people TABLE(username nvarchar(100),name nvarchar(200),role varchar(60),dept varchar(30));
INSERT @people VALUES('demo_om',N'Quản lý vận hành Sài Gòn','OPERATION_MANAGER',NULL),('demo_hks',N'Giám sát buồng phòng Sài Gòn','HOUSEKEEPING_SUPERVISOR','HK'),('demo_maintenance_lead',N'Trưởng nhóm bảo trì Sài Gòn','MAINTENANCE_LEAD','ENG'),('demo_owner',N'Chủ sở hữu mẫu','OWNER',NULL);
IF EXISTS(SELECT 1 FROM users u JOIN @people p ON p.username=u.username) THROW 51426,'Target demo account already exists.',1;
INSERT users(employee_code,username,full_name,email,password_hash,account_status,employment_status)
 SELECT UPPER(p.username),p.username,p.name,p.username+'@example.invalid',u.password_hash,'ACTIVE','ACTIVE' FROM @people p JOIN users u ON u.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,is_primary,recorded_by,provenance)
 SELECT u.user_id,@branch,'BRANCH_ACCESS',1,@admin,N'V10 approved mapping' FROM @people p JOIN users u ON u.username=p.username;
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,provenance)
 SELECT u.user_id,@branch,d.department_id,'EMPLOYMENT',@admin,N'V10 approved mapping' FROM @people p JOIN users u ON u.username=p.username JOIN departments d ON d.branch_id=@branch AND d.code=p.dept;
INSERT user_roles(user_id,role_id,branch_id,assigned_by)
 SELECT u.user_id,r.role_id,CASE WHEN p.role='OWNER' THEN NULL ELSE @branch END,@admin FROM @people p JOIN users u ON u.username=p.username JOIN roles r ON r.code=p.role;
UPDATE s SET role_id=target.role_id FROM workday_shifts s JOIN roles old ON old.role_id=s.role_id JOIN departments d ON d.department_id=s.department_id
 JOIN roles target ON target.code=CASE WHEN d.code='HK' THEN 'HOUSEKEEPING_SUPERVISOR' WHEN d.code='ENG' THEN 'MAINTENANCE_LEAD' END WHERE old.code='DEMO_MANAGER';
INSERT audit_logs(actor_user_id,entity_type,entity_key,action,operation_key,after_json)
 VALUES(@admin,'RoleMapping','V10-20261122','APPLY',NEWID(),N'{"approved":true,"newRoles":4,"newUsers":4,"sharedItemsEditableByStorekeeper":true}');
COMMIT;
