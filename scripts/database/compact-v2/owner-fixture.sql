SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated fixture required',1;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='qa_admin'),@a bigint=(SELECT branch_id FROM branches WHERE code='QA'),@role bigint;
INSERT roles(code,name) VALUES('OWNER',N'Owner');SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'ALL' FROM permissions WHERE code IN
('ASSET_VIEW','CLEANING_VIEW','DASHBOARD_VIEW','INCIDENT_VIEW','INSPECTION_VIEW','INVENTORY_VIEW','IR_VIEW','MAINTENANCE_VIEW','MR_VIEW','REPORT_VIEW','ROOM_VIEW','TASK_VIEW');
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT 'OWN_FIXTURE','qa_owner',N'Fixture Owner',password_hash,'ACTIVE','ACTIVE' FROM users WHERE user_id=@admin;
DECLARE @owner bigint=SCOPE_IDENTITY();
INSERT user_roles(user_id,role_id,assigned_by) VALUES(@owner,@role,@admin);
-- A mixed-scope reader proves ALL in one domain cannot broaden another domain.
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT 'READER_FIXTURE','qa_scope_reader',N'Fixture mixed reader',password_hash,'ACTIVE','ACTIVE' FROM users WHERE user_id=@admin;
DECLARE @reader bigint=SCOPE_IDENTITY();
INSERT roles(code,name) VALUES('QA_OVERVIEW_GLOBAL',N'Fixture global reporting');SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'ALL' FROM permissions WHERE code IN ('DASHBOARD_VIEW','REPORT_VIEW');
INSERT user_roles(user_id,role_id,assigned_by) VALUES(@reader,@role,@admin);
INSERT roles(code,name) VALUES('QA_ROOM_SCOPED',N'Fixture scoped room reader');SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'BRANCH' FROM permissions WHERE code='ROOM_VIEW';
INSERT user_roles(user_id,role_id,branch_id,assigned_by) VALUES(@reader,@role,@a,@admin);
INSERT memberships(user_id,branch_id,kind,recorded_by) VALUES(@reader,@a,'BRANCH_ACCESS',@admin);
SELECT @owner owner_id,@reader reader_id;
