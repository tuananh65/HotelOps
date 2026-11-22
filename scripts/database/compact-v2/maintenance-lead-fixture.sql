SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated database required',1;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='qa_admin'),@lead bigint,@staff bigint;
INSERT reference_values(kind,code,name,sort_order) VALUES('PRIORITY','HIGH',N'High',3),('SEVERITY','CRITICAL',N'Critical',4);
INSERT roles(code,name) VALUES('MAINTENANCE_LEAD',N'Maintenance Lead'); SET @lead=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT @lead,permission_id,CASE WHEN code='ATTENDANCE_RECORD' THEN 'OWN' ELSE 'BRANCH' END FROM permissions WHERE code IN
('ASSET_VIEW','ATTENDANCE_RECORD','DASHBOARD_VIEW','INCIDENT_CREATE','INCIDENT_ESCALATION_HANDLE','INCIDENT_MANAGE','INCIDENT_TRIAGE','INCIDENT_VIEW','MAINTENANCE_ASSIGN','MAINTENANCE_REVIEW','MAINTENANCE_VIEW','MR_VIEW','PM_MANAGE','ROOM_VIEW');
INSERT roles(code,name) VALUES('MAINTENANCE_STAFF',N'Maintenance Staff'); SET @staff=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT @staff,permission_id,CASE WHEN code='ATTENDANCE_RECORD' THEN 'OWN' ELSE 'BRANCH' END FROM permissions WHERE code IN
('ATTENDANCE_RECORD','MAINTENANCE_EXECUTE','MAINTENANCE_VIEW','INCIDENT_CREATE','INCIDENT_VIEW','ROOM_VIEW','ASSET_VIEW','MR_CREATE','MR_VIEW');
INSERT departments(branch_id,code,name) SELECT branch_id,'ENG',N'Engineering' FROM branches WHERE code IN ('QA','OTHER');
DECLARE @actors TABLE(username varchar(100),branch_id bigint,role_id bigint,active bit);
INSERT @actors SELECT 'qa_lead_a',branch_id,@lead,1 FROM branches WHERE code='QA' UNION ALL SELECT 'qa_lead_b',branch_id,@lead,1 FROM branches WHERE code='OTHER'
UNION ALL SELECT 'qa_tech_a',branch_id,@staff,1 FROM branches WHERE code='QA' UNION ALL SELECT 'qa_tech_peer',branch_id,@staff,1 FROM branches WHERE code='QA'
UNION ALL SELECT 'qa_tech_inactive',branch_id,@staff,0 FROM branches WHERE code='QA' UNION ALL SELECT 'qa_tech_b',branch_id,@staff,1 FROM branches WHERE code='OTHER';
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT t.username,t.username,t.username,u.password_hash,CASE WHEN t.active=1 THEN 'ACTIVE' ELSE 'INACTIVE' END,'ACTIVE' FROM @actors t CROSS JOIN users u WHERE u.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,recorded_by,valid_from) SELECT u.user_id,t.branch_id,'BRANCH_ACCESS',@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username;
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,valid_from) SELECT u.user_id,t.branch_id,d.department_id,'EMPLOYMENT',@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username JOIN departments d ON d.branch_id=t.branch_id AND d.code='ENG';
INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at) SELECT u.user_id,t.role_id,t.branch_id,@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username;
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT d.branch_id,d.department_id,r.role_id,CONCAT(r.name,' early'),0,480,4 FROM departments d CROSS JOIN roles r WHERE d.code='ENG' AND r.code IN ('MAINTENANCE_LEAD','MAINTENANCE_STAFF');
SELECT u.user_id,t.username,t.branch_id,t.role_id FROM @actors t JOIN users u ON u.username=t.username;
