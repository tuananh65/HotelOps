-- TEST-only metadata bootstrap. No stock movements, receipts or balances are created.
-- Run after role consolidation, p0-supplies.sql and p0-store-policy.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompactWorkdayTestP0%' THROW 51970,'Isolated P0 TEST database required.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@now datetime2=SYSUTCDATETIME();
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps.P0.SupplyDefaults',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
 IF @lock<0 THROW 51971,'Supply defaults migration already running.',1;
 IF OBJECT_ID('dbo.supply_configurations') IS NULL THROW 51972,'Supply schema required.',1;
 IF EXISTS(SELECT b.branch_id FROM dbo.branches b LEFT JOIN dbo.warehouses w ON w.branch_id=b.branch_id AND w.is_active=1 WHERE b.is_active=1 GROUP BY b.branch_id HAVING COUNT(w.warehouse_id)<>1)
  THROW 51973,'Each active branch requires exactly one active warehouse.',1;

 -- Prefer the original OM (not the newly converted department-head assignment).
 -- More than one original eligible OM is ambiguous and requires explicit mapping.
 SELECT DISTINCT ur.branch_id,ur.user_id INTO #actors
 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id JOIN dbo.users u ON u.user_id=ur.user_id
 JOIN dbo.branches b ON b.branch_id=ur.branch_id AND b.is_active=1
 WHERE r.code='OPERATION_MANAGER' AND r.is_active=1 AND u.account_status='ACTIVE'
 AND ur.revoked_at IS NULL AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now)
 AND EXISTS(SELECT 1 FROM dbo.memberships m WHERE m.user_id=ur.user_id AND m.branch_id=ur.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=@now AND (m.valid_to IS NULL OR m.valid_to>@now))
 AND NOT EXISTS(SELECT 1 FROM dbo.user_roles old JOIN dbo.roles old_role ON old_role.role_id=old.role_id WHERE old.user_id=ur.user_id AND old.branch_id=ur.branch_id AND old_role.code='DEPT_MANAGER' AND old.revoked_at=ur.assigned_at);
 IF EXISTS(SELECT b.branch_id FROM dbo.branches b LEFT JOIN #actors a ON a.branch_id=b.branch_id WHERE b.is_active=1 GROUP BY b.branch_id HAVING COUNT(a.user_id)<>1)
  THROW 51974,'Original OM attribution is ambiguous or missing; provide reviewed branch mapping.',1;

 DECLARE @defaults TABLE(code varchar(60) PRIMARY KEY,name nvarchar(150),unit_code varchar(60),unit_name nvarchar(150),existing_code varchar(60) NULL,item_id bigint NULL);
 INSERT @defaults(code,name,unit_code,unit_name,existing_code) VALUES
 ('P0-FILL-TOOTHBRUSH',N'Bàn chải đánh răng','P0-EACH',N'Cái',NULL),
 ('P0-FILL-TOOTHPASTE',N'Kem đánh răng','P0-TUBE',N'Tuýp',NULL),
 ('P0-FILL-SHAMPOO',N'Dầu gội','P0-BOTTLE',N'Chai','DEMO-ITEM-2'),
 ('P0-FILL-BODYWASH',N'Sữa tắm','P0-BOTTLE',N'Chai',NULL),
 ('P0-FILL-WATER',N'Nước uống','P0-BOTTLE',N'Chai','DEMO-ITEM-4'),
 ('P0-FILL-TOILETPAPER',N'Giấy vệ sinh','P0-ROLL',N'Cuộn',NULL),
 ('P0-FILL-BATHTOWEL',N'Khăn tắm','P0-EACH',N'Cái','DEMO-ITEM-3'),
 ('P0-FILL-FACETOWEL',N'Khăn mặt','P0-EACH',N'Cái',NULL);

 -- The inspected legacy EA unit is discrete (cái/chai), never ml/litre or a carton.
 -- Match both exact business name and the inspected unit, never fuzzy names.
 UPDATE d SET item_id=i.item_id FROM @defaults d JOIN dbo.inventory_items i ON i.code=d.existing_code AND i.name=d.name
 JOIN dbo.reference_values u ON u.reference_id=i.base_unit_id
 WHERE i.is_active=1 AND u.kind='UOM' AND u.code='DEMO-EA' AND u.name=N'Cái / chai' AND u.is_active=1 AND u.base_unit_id IS NULL AND u.conversion_factor IS NULL;
 IF EXISTS(SELECT 1 FROM @defaults d JOIN dbo.inventory_items i ON i.code=d.existing_code WHERE d.item_id IS NULL)
  THROW 51975,'Existing fill item meaning/unit/status differs from inspected mapping.',1;
 IF EXISTS(SELECT 1 FROM @defaults d JOIN dbo.inventory_items i ON i.name=d.name WHERE d.item_id IS NULL AND i.code<>d.code)
  THROW 51975,'A same-name catalog item needs reviewed unit mapping; duplicate item was not created.',1;
 -- Respect deactivated canonical metadata rather than silently re-enabling it.
 IF EXISTS(SELECT 1 FROM @defaults d JOIN dbo.inventory_items i ON i.code=d.code JOIN dbo.reference_values u ON u.reference_id=i.base_unit_id
  WHERE d.item_id IS NULL AND (i.name<>d.name OR i.is_active=0 OR u.kind<>'UOM' OR u.code<>d.unit_code OR u.name<>d.unit_name OR u.is_active=0 OR u.base_unit_id IS NOT NULL))
  THROW 51976,'Canonical default item has a conflicting or inactive definition.',1;
 IF EXISTS(SELECT 1 FROM @defaults d JOIN dbo.reference_values u ON u.kind='UOM' AND u.code=d.unit_code WHERE u.name<>d.unit_name OR u.is_active=0 OR u.base_unit_id IS NOT NULL OR u.conversion_factor IS NOT NULL)
  THROW 51976,'Canonical base unit has a conflicting or inactive definition.',1;
 INSERT dbo.reference_values(kind,code,name,sort_order,is_active)
 SELECT DISTINCT 'UOM',d.unit_code,d.unit_name,0,1 FROM @defaults d
 WHERE d.item_id IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.reference_values u WHERE u.kind='UOM' AND u.code=d.unit_code);
 IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE kind='ITEM_CATEGORY' AND code='P0-FILL')
  INSERT dbo.reference_values(kind,code,name,sort_order,is_active) VALUES('ITEM_CATEGORY','P0-FILL',N'Đồ bổ sung đầu ca',0,1);
 IF EXISTS(SELECT 1 FROM dbo.reference_values WHERE kind='ITEM_CATEGORY' AND code='P0-FILL' AND is_active=0)
  THROW 51976,'Default item category is disabled.',1;
 INSERT dbo.inventory_items(code,name,category_id,base_unit_id,description,is_active)
 SELECT d.code,d.name,c.reference_id,u.reference_id,N'Danh mục đồ fill P0; đơn vị cơ sở từng món, không tự tạo tồn kho.',1
 FROM @defaults d JOIN dbo.reference_values u ON u.kind='UOM' AND u.code=d.unit_code
 JOIN dbo.reference_values c ON c.kind='ITEM_CATEGORY' AND c.code='P0-FILL'
 WHERE d.item_id IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.inventory_items i WHERE i.code=d.code);
 UPDATE d SET item_id=i.item_id FROM @defaults d JOIN dbo.inventory_items i ON i.code=d.code WHERE d.item_id IS NULL;
 IF EXISTS(SELECT 1 FROM @defaults WHERE item_id IS NULL) THROW 51977,'Incomplete fill catalog mapping.',1;

 DECLARE @new TABLE(configuration_id bigint,branch_id bigint,item_id bigint,warehouse_id bigint);
 INSERT dbo.supply_configurations(branch_id,item_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,is_current,updated_by,updated_at)
 OUTPUT inserted.configuration_id,inserted.branch_id,inserted.item_id,inserted.warehouse_id INTO @new
 SELECT b.branch_id,d.item_id,'A',w.warehouse_id,i.base_unit_id,10,50,1,1,1,1,a.user_id,@now
 FROM dbo.branches b JOIN dbo.warehouses w ON w.branch_id=b.branch_id AND w.is_active=1 JOIN #actors a ON a.branch_id=b.branch_id
 CROSS JOIN @defaults d JOIN dbo.inventory_items i ON i.item_id=d.item_id
 WHERE b.is_active=1 AND NOT EXISTS(SELECT 1 FROM dbo.supply_configurations s WHERE s.branch_id=b.branch_id AND s.item_id=d.item_id);
 -- Existing current/revised/disabled supply configuration is never overwritten.
 UPDATE p SET minimum_qty=50,target_qty=CASE WHEN p.target_qty<50 THEN 50 ELSE p.target_qty END
 FROM dbo.inventory_policies p JOIN @new n ON n.branch_id=p.branch_id AND n.item_id=p.item_id AND n.warehouse_id=p.warehouse_id;
 INSERT dbo.inventory_policies(branch_id,warehouse_id,item_id,minimum_qty,target_qty)
 SELECT n.branch_id,n.warehouse_id,n.item_id,50,50 FROM @new n
 WHERE NOT EXISTS(SELECT 1 FROM dbo.inventory_policies p WHERE p.branch_id=n.branch_id AND p.warehouse_id=n.warehouse_id AND p.item_id=n.item_id);

 -- Exact room asset type observed in TEST: air conditioners require external repair.
 -- No generic durable classification is guessed from names or historical issues.
 INSERT dbo.supply_configurations(branch_id,asset_id,supply_group,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,is_current,updated_by,updated_at)
 SELECT asset.branch_id,asset.asset_id,'C',0,0,0,1,1,1,a.user_id,@now
 FROM dbo.assets asset JOIN #actors a ON a.branch_id=asset.branch_id
 JOIN dbo.reference_values t ON t.reference_id=asset.asset_type_id AND t.kind='ASSET_TYPE' AND t.name=N'Điều hòa'
 WHERE asset.room_id IS NOT NULL AND asset.is_active=1 AND asset.retired_at IS NULL
 AND NOT EXISTS(SELECT 1 FROM dbo.supply_configurations s WHERE s.branch_id=asset.branch_id AND s.asset_id=asset.asset_id);

 -- Seed absent room-report definitions only; preserve intentional disable/name edits.
 INSERT dbo.reference_values(kind,code,name,settings_json,sort_order,is_active)
 SELECT 'WORK_CATEGORY',CONCAT('ROOM_ISSUE_',b.branch_id,'_',v.kind),v.name,
  CONCAT('{"branchId":',b.branch_id,',"roomIssueKind":"',v.kind,'"}'),v.sort_order,1
 FROM dbo.branches b CROSS JOIN (VALUES('BROKEN',N'Hỏng đồ vật',0),('MISSING',N'Thiếu đồ vật',1))v(kind,name,sort_order)
 WHERE b.is_active=1 AND NOT EXISTS(SELECT 1 FROM dbo.reference_values r WHERE r.kind='WORK_CATEGORY' AND r.code=CONCAT('ROOM_ISSUE_',b.branch_id,'_',v.kind));
 IF EXISTS(SELECT 1 FROM @new)
  INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,after_json,occurred_at)
  VALUES(NEWID(),'P0_SUPPLY_DEFAULTS','261124-v1','BOOTSTRAP_METADATA',
   (SELECT n.configuration_id,n.branch_id,n.item_id,n.warehouse_id,10 initial_quantity,50 minimum_stock,1 per_guest_quantity FROM @new n FOR JSON PATH),@now);
 SELECT d.code requested_code,i.code mapped_code,i.name,u.code base_unit_code,u.name base_unit_name FROM @defaults d JOIN dbo.inventory_items i ON i.item_id=d.item_id JOIN dbo.reference_values u ON u.reference_id=i.base_unit_id ORDER BY d.code;
 SELECT COUNT(*) inserted_fill_configurations FROM @new;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
