SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated test database required',1;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='qa_admin'),@staff bigint,@head bigint,@other bigint=(SELECT branch_id FROM branches WHERE code='OTHER');
INSERT roles(code,name) VALUES('HK_STAFF',N'Housekeeping Staff'); SET @staff=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT @staff,permission_id,CASE WHEN code='ATTENDANCE_RECORD' THEN 'OWN' ELSE 'BRANCH' END FROM permissions WHERE code IN
('ATTENDANCE_RECORD','CLEANING_EXECUTE','CLEANING_VIEW','INCIDENT_CREATE','INCIDENT_VIEW','IR_CREATE','IR_PROCESS','IR_VIEW','MR_CREATE','MR_VIEW','ROOM_VIEW','TASK_EXECUTE','TASK_VIEW');
INSERT roles(code,name) VALUES('HOUSEKEEPING_SUPERVISOR',N'Housekeeping Supervisor'); SET @head=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT @head,permission_id,CASE WHEN code='ATTENDANCE_RECORD' THEN 'OWN' ELSE 'BRANCH' END FROM permissions WHERE code IN
('ATTENDANCE_RECORD','CHECKLIST_MANAGE','CLEANING_ASSIGN','CLEANING_VIEW','DASHBOARD_VIEW','DEFECT_RESOLVE','INCIDENT_CREATE','INCIDENT_VIEW','INSPECTION_PERFORM','INSPECTION_VIEW','ROOM_VIEW');
INSERT roles(code,name) VALUES('QA_INSPECT_NO_DEFECT',N'Fixture inspection without defect resolution');
DECLARE @noDefect bigint=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @noDefect,permission_id,'BRANCH' FROM permissions WHERE code IN ('INSPECTION_PERFORM','INSPECTION_VIEW');
INSERT departments(branch_id,code,name) VALUES(@other,'HK',N'Housekeeping');
DECLARE @actors TABLE(username varchar(100),branch_id bigint,role_id bigint);
INSERT @actors SELECT 'qa_hk_a',branch_id,@staff FROM branches WHERE code='QA' UNION ALL SELECT 'qa_hk_peer',branch_id,@staff FROM branches WHERE code='QA'
UNION ALL SELECT 'qa_hk_b',@other,@staff UNION ALL SELECT 'qa_hks_b',@other,@head UNION ALL SELECT 'qa_hks_a',branch_id,@head FROM branches WHERE code='QA'
UNION ALL SELECT 'qa_inspect_no_defect',branch_id,@noDefect FROM branches WHERE code='QA';
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT t.username,t.username,t.username,u.password_hash,'ACTIVE','ACTIVE' FROM @actors t CROSS JOIN users u WHERE u.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,recorded_by,valid_from) SELECT u.user_id,t.branch_id,'BRANCH_ACCESS',@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username;
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,valid_from) SELECT u.user_id,t.branch_id,d.department_id,'EMPLOYMENT',@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username JOIN departments d ON d.branch_id=t.branch_id AND d.code='HK';
INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at) SELECT u.user_id,t.role_id,t.branch_id,@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username;
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT d.branch_id,d.department_id,@staff,N'Housekeeping morning',0,480,4 FROM departments d WHERE d.code='HK';
SELECT u.user_id,t.username,t.branch_id,t.role_id FROM @actors t JOIN users u ON u.username=t.username;
