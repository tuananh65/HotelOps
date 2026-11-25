-- Synthetic baseline only: all stock, shift start, work and inspection mutations use HTTP.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Exact V2 TEST required',1;
IF @prefix NOT LIKE 'V2HK-%' THROW 51999,'Fixture prefix required',1;
BEGIN TRANSACTION;
INSERT branches(code,name,timezone) VALUES(@prefix,N'V2 TEST housekeeping','Asia/Ho_Chi_Minh');
DECLARE @b bigint=SCOPE_IDENTITY();
INSERT departments(branch_id,code,name) SELECT @b,code,code FROM(VALUES('HK'),('STORE'),('OPS'),('TECH'),('FRONT'))x(code);
DECLARE @hk bigint=(SELECT department_id FROM departments WHERE branch_id=@b AND code='HK');
INSERT locations(branch_id,kind,code,name,floor_number) VALUES(@b,'FLOOR','TEST_F1','TEST floor',1);
DECLARE @loc bigint=SCOPE_IDENTITY();
INSERT room_types(branch_id,code,name,capacity) VALUES(@b,'TEST_TYPE','TEST capacity four',4);
DECLARE @type bigint=SCOPE_IDENTITY();
INSERT rooms(branch_id,location_id,room_type_id,department_id,code,operational_status)
 SELECT @b,@loc,@type,@hk,code,'DIRTY' FROM(VALUES('TEST101'),('TEST102'))x(code);
INSERT room_types(branch_id,code,name,capacity) VALUES(@b,'TEST_LARGE','TEST capacity twelve',12);
DECLARE @largeType bigint=SCOPE_IDENTITY();
INSERT rooms(branch_id,location_id,room_type_id,department_id,code,operational_status)
 VALUES(@b,@loc,@largeType,@hk,'TEST103','DIRTY');
INSERT warehouses(branch_id,code,name,department_id) SELECT @b,'TEST_MAIN','TEST warehouse',department_id FROM departments WHERE branch_id=@b AND code='STORE';
DECLARE @warehouse bigint=SCOPE_IDENTITY(),@unit bigint=(SELECT TOP(1) reference_id FROM reference_values WHERE kind='UOM' AND is_active=1 ORDER BY reference_id);
DECLARE @actors TABLE(alias varchar(20),role varchar(60),dept varchar(20));
INSERT @actors VALUES('hk','HK_STAFF','HK'),('peer','HK_STAFF','HK'),('supervisor','HOUSEKEEPING_SUPERVISOR','HK'),('store','STOREKEEPER','STORE'),('manager','OPERATION_MANAGER','OPS');
INSERT @actors VALUES('lead','MAINTENANCE_SUPERVISOR','TECH'),('technician','MAINTENANCE_STAFF','TECH');
INSERT @actors VALUES('reception','RECEPTION','FRONT');
INSERT users(employee_code,username,full_name,account_status,employment_status,phone)
 SELECT @prefix+'-'+alias,@prefix+'-'+alias,'TEST '+alias,'ACTIVE','ACTIVE',CASE WHEN alias='hk' THEN '0900000001' ELSE NULL END FROM @actors;
INSERT memberships(user_id,branch_id,kind,valid_from,provenance)
 SELECT u.user_id,@b,'BRANCH_ACCESS',DATEADD(day,-1,SYSUTCDATETIME()),'Synthetic V2 TEST' FROM @actors a JOIN users u ON u.username=@prefix+'-'+a.alias;
INSERT memberships(user_id,branch_id,department_id,kind,valid_from,provenance)
 SELECT u.user_id,@b,d.department_id,'EMPLOYMENT',DATEADD(day,-1,SYSUTCDATETIME()),'Synthetic V2 TEST' FROM @actors a JOIN users u ON u.username=@prefix+'-'+a.alias JOIN departments d ON d.branch_id=@b AND d.code=a.dept;
INSERT user_roles(user_id,role_id,branch_id,assigned_at)
 SELECT u.user_id,r.role_id,@b,DATEADD(day,-1,SYSUTCDATETIME()) FROM @actors a JOIN users u ON u.username=@prefix+'-'+a.alias JOIN roles r ON r.code=a.role;
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity,is_active)
 SELECT DISTINCT @b,d.department_id,r.role_id,'TEST full-day',0,1440,5,1 FROM @actors a JOIN departments d ON d.branch_id=@b AND d.code=a.dept JOIN roles r ON r.code=a.role;
INSERT workday_assignments(shift_id,user_id,work_date,status,created_by)
 SELECT s.shift_id,u.user_id,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),'PLANNED',u.user_id FROM @actors a JOIN users u ON u.username=@prefix+'-'+a.alias JOIN roles r ON r.code=a.role JOIN workday_shifts s ON s.branch_id=@b AND s.role_id=r.role_id;
INSERT inventory_items(code,name,base_unit_id,description) VALUES(@prefix+'-fill','TEST FEFO consumable',@unit,'Synthetic TEST only'),(@prefix+'-towel','TEST reusable towel',@unit,'Synthetic TEST only');
DECLARE @supervisor bigint=(SELECT user_id FROM users WHERE username=@prefix+'-supervisor');
INSERT checklist_templates(branch_id,code,version_no,name,kind,created_by,is_active) VALUES(@b,'TEST_CLEAN',1,'TEST required cleaning','CLEANING',@supervisor,1);
DECLARE @template bigint=SCOPE_IDENTITY();
INSERT checklist_items(checklist_template_id,line_no,description,is_required,weight) VALUES(@template,1,'TEST physical cleaning check',1,1);
UPDATE checklist_templates SET published_at=SYSUTCDATETIME() WHERE checklist_template_id=@template;
INSERT checklist_templates(branch_id,code,version_no,name,kind,created_by,is_active) VALUES(@b,'TEST_REPAIR',1,'TEST required repair','MAINTENANCE',@supervisor,1);
DECLARE @repairTemplate bigint=SCOPE_IDENTITY();
INSERT checklist_items(checklist_template_id,line_no,description,is_required,weight) VALUES(@repairTemplate,1,'TEST physical repair check',1,1);
UPDATE checklist_templates SET published_at=SYSUTCDATETIME() WHERE checklist_template_id=@repairTemplate;
INSERT assets(branch_id,asset_code,name,room_id,condition_code)
 SELECT @b,@prefix+'-'+code,'TEST air conditioner',room_id,'GOOD' FROM rooms WHERE branch_id=@b;
COMMIT;
SELECT @b branch,@warehouse warehouse,@unit unit,@template checklist,@repairTemplate repairChecklist,
 JSON_QUERY((SELECT asset_id assetId,room_id roomId FROM assets WHERE branch_id=@b FOR JSON PATH)) assets,
 JSON_QUERY((SELECT a.alias,u.user_id [user],wa.assignment_id assignment FROM @actors a JOIN users u ON u.username=@prefix+'-'+a.alias JOIN workday_assignments wa ON wa.user_id=u.user_id FOR JSON PATH)) actors,
 JSON_QUERY((SELECT room_id roomId,code FROM rooms WHERE branch_id=@b ORDER BY code FOR JSON PATH)) rooms,
 JSON_QUERY((SELECT item_id itemId,code,base_unit_id unitId FROM inventory_items WHERE code IN(@prefix+'-fill',@prefix+'-towel') FOR JSON PATH)) items
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
