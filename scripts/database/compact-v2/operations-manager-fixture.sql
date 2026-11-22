SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated test database required',1;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='qa_admin'), @role bigint;
INSERT reference_values(kind,code,name) VALUES('PRIORITY','QA-OM-HIGH',N'High');
INSERT roles(code,name) VALUES('OPERATION_MANAGER',N'Operations Manager'); SET @role=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @role,permission_id,'BRANCH' FROM permissions WHERE code IN
('ASSET_MANAGE','ASSET_RETIRE','ASSET_TRANSFER','ASSET_VIEW','AUDIT_VIEW','CLEANING_VIEW','DASHBOARD_VIEW','INCIDENT_CREATE','INCIDENT_ESCALATION_HANDLE','INCIDENT_MANAGE','INCIDENT_TRIAGE','INCIDENT_VIEW','INSPECTION_VIEW','INVENTORY_VIEW','IR_CREATE','IR_OVERDUE_HANDLE','IR_PROCESS','IR_VIEW','MAINTENANCE_VIEW','MR_VIEW','REPORT_VIEW','ROOM_BLOCK_MANAGE','ROOM_VIEW','TASK_VIEW');
DECLARE @users TABLE(username varchar(100),branch_id bigint);
INSERT @users SELECT 'qa_om_a',branch_id FROM branches WHERE code='QA' UNION ALL SELECT 'qa_om_b',branch_id FROM branches WHERE code='OTHER';
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT u.username,u.username,u.username,a.password_hash,'ACTIVE','ACTIVE' FROM @users u CROSS JOIN users a WHERE a.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,recorded_by) SELECT u.user_id,t.branch_id,'BRANCH_ACCESS',@admin FROM @users t JOIN users u ON u.username=t.username;
INSERT user_roles(user_id,role_id,branch_id,assigned_by) SELECT u.user_id,@role,t.branch_id,@admin FROM @users t JOIN users u ON u.username=t.username;
SELECT u.user_id,u.username,t.branch_id FROM @users t JOIN users u ON u.username=t.username;
