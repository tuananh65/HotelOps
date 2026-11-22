SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactApiTest%' THROW 51000,'Isolated test database required',1;
DECLARE @admin bigint=(SELECT user_id FROM users WHERE username='qa_admin'),@store bigint=(SELECT role_id FROM roles WHERE code='STOREKEEPER');
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT @store,p.permission_id,CASE WHEN p.code='ATTENDANCE_RECORD' THEN 'OWN' ELSE 'BRANCH' END FROM permissions p WHERE p.code IN
('ATTENDANCE_RECORD','INVENTORY_VIEW','ITEM_MASTER_MANAGE','MR_FULFILL','MR_VIEW','REPLENISHMENT_CREATE','REPLENISHMENT_VIEW','REPLENISHMENT_RECEIVE','REPLENISHMENT_CLOSE','STOCK_RECEIPT_POST','STOCK_ISSUE_POST','STOCK_RETURN_POST','STOCK_TRANSFER_POST','STOCK_ADJUST_POST','STOCK_REVERSE','STOCKTAKE_PERFORM')
AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=@store AND rp.permission_id=p.permission_id);
DECLARE @actors TABLE(username varchar(100),branch_id bigint);
INSERT @actors SELECT 'qa_store_a',branch_id FROM branches WHERE code='QA' UNION ALL SELECT 'qa_store_peer',branch_id FROM branches WHERE code='QA' UNION ALL SELECT 'qa_store_b',branch_id FROM branches WHERE code='OTHER';
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT t.username,t.username,t.username,u.password_hash,'ACTIVE','ACTIVE' FROM @actors t CROSS JOIN users u WHERE u.user_id=@admin;
INSERT memberships(user_id,branch_id,kind,recorded_by,valid_from) SELECT u.user_id,t.branch_id,'BRANCH_ACCESS',@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username;
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by,valid_from)
SELECT u.user_id,t.branch_id,d.department_id,'EMPLOYMENT',@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username JOIN departments d ON d.branch_id=t.branch_id AND d.code='HK';
INSERT user_roles(user_id,role_id,branch_id,assigned_by,assigned_at) SELECT u.user_id,@store,t.branch_id,@admin,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors t JOIN users u ON u.username=t.username;
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
SELECT d.branch_id,d.department_id,@store,N'Storekeeper shift',0,480,3 FROM departments d WHERE d.code='HK';
SELECT u.user_id,t.username,t.branch_id FROM @actors t JOIN users u ON u.username=t.username;
