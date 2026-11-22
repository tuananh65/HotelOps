-- Synthetic fixtures only. Never run against the primary Compact database.
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51510,'Isolated test database required',1;
SET NOCOUNT ON;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='qa_admin'),
 @a bigint=(SELECT branch_id FROM branches WHERE code='QA'),@b bigint=(SELECT branch_id FROM branches WHERE code='OTHER'),
 @dh bigint=(SELECT role_id FROM roles WHERE code='DEPT_MANAGER'),@workerRole bigint,@hk bigint,@fo bigint,@ct bigint;
INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT @dh,permission_id,'BRANCH' FROM permissions p WHERE code IN
 ('DASHBOARD_VIEW','SHIFT_MANAGE','TASK_MANAGE','TASK_VIEW','IR_CREATE','IR_VIEW','IR_PROCESS','IR_OVERDUE_HANDLE','MR_VIEW','MR_APPROVE_L1','MR_APPROVE_FALLBACK','REPORT_VIEW','ROOM_VIEW','INVENTORY_VIEW')
 AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=@dh AND rp.permission_id=p.permission_id);
INSERT roles(code,name) VALUES('QA_SHIFT',N'Fixture staff');SET @workerRole=SCOPE_IDENTITY();
INSERT role_permissions(role_id,permission_id,data_scope) SELECT @workerRole,permission_id,'OWN' FROM permissions WHERE code='ATTENDANCE_RECORD';
SET @hk=(SELECT department_id FROM departments WHERE branch_id=@a AND code='HK');
INSERT departments(branch_id,code,name) VALUES(@a,'FO',N'Front office');SET @fo=SCOPE_IDENTITY();
INSERT departments(branch_id,code,name) VALUES(@b,'HK',N'Other housekeeping');SET @ct=SCOPE_IDENTITY();
DECLARE @people TABLE(username varchar(100),branch_id bigint,department_id bigint,head bit,inactive bit);
INSERT @people VALUES('qa_head_a',@a,@hk,1,0),('qa_head_b',@b,@ct,1,0),('qa_fallback',@a,@fo,1,0),
 ('qa_staff_a',@a,@hk,0,0),('qa_staff_a2',@a,@hk,0,0),('qa_staff_fo',@a,@fo,0,0),('qa_staff_b',@b,@ct,0,0),('qa_inactive',@a,@hk,0,1);
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
 SELECT p.username,p.username,p.username,u.password_hash,CASE WHEN p.inactive=1 THEN 'INACTIVE' ELSE 'ACTIVE' END,'ACTIVE' FROM @people p CROSS JOIN users u WHERE u.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,valid_from,recorded_by)
 SELECT u.user_id,p.branch_id,'BRANCH_ACCESS',DATEADD(day,-2,SYSUTCDATETIME()),@admin FROM @people p JOIN users u ON u.username=p.username;
INSERT memberships(user_id,branch_id,department_id,kind,valid_from,recorded_by)
 SELECT u.user_id,p.branch_id,p.department_id,'EMPLOYMENT',DATEADD(day,-2,SYSUTCDATETIME()),@admin FROM @people p JOIN users u ON u.username=p.username;
INSERT memberships(user_id,branch_id,department_id,kind,valid_from,recorded_by)
 SELECT u.user_id,p.branch_id,p.department_id,'DEPARTMENT_AUTHORITY',DATEADD(day,-2,SYSUTCDATETIME()),@admin FROM @people p JOIN users u ON u.username=p.username WHERE p.head=1;
INSERT user_roles(user_id,role_id,branch_id,assigned_at,assigned_by)
 SELECT u.user_id,CASE WHEN p.head=1 THEN @dh ELSE @workerRole END,p.branch_id,DATEADD(day,-2,SYSUTCDATETIME()),@admin FROM @people p JOIN users u ON u.username=p.username;
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity) VALUES
 (@a,@hk,@workerRole,N'HK morning',480,960,1),(@a,@hk,@workerRole,N'HK overlapping',600,1080,2),
 (@a,@fo,@workerRole,N'FO morning',480,960,2),(@b,@ct,@workerRole,N'Other HK',480,960,2);
DECLARE @unit bigint,@item bigint,@warehouse bigint;
INSERT reference_values(kind,code,name) VALUES('UOM','QA-EA',N'Piece');SET @unit=SCOPE_IDENTITY();
INSERT inventory_items(code,name,base_unit_id) VALUES('QA-ITEM',N'Test item',@unit);SET @item=SCOPE_IDENTITY();
INSERT warehouses(branch_id,department_id,code,name) VALUES(@a,@fo,'QA-WH',N'Test warehouse');SET @warehouse=SCOPE_IDENTITY();
INSERT inventory_documents(branch_id,document_no,kind,status,department_id,source_warehouse_id,created_by)
 SELECT @a,'QA-L1','MATERIAL_REQUEST','PENDING_APPROVAL',@hk,@warehouse,user_id FROM users WHERE username='qa_staff_a';
INSERT inventory_documents(branch_id,document_no,kind,status,department_id,source_warehouse_id,created_by)
 SELECT @a,'QA-FALLBACK','MATERIAL_REQUEST','PENDING_APPROVAL',@hk,@warehouse,user_id FROM users WHERE username='qa_head_a';
INSERT inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,requested_qty)
 SELECT @a,document_id,1,@item,@unit,1,2 FROM inventory_documents WHERE document_no IN ('QA-L1','QA-FALLBACK');
SELECT u.user_id,u.username,p.branch_id,p.department_id FROM @people p JOIN users u ON u.username=p.username;
