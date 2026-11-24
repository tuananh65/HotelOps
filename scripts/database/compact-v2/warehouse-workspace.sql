-- User-authorized 2026-11-24: one named warehouse per branch and +100 base units/item.
-- Preserve warehouse IDs and all historical documents. No DDL, grants or account changes.
-- Run only after verified backup and clone validation. Fixed document numbers make retry safe.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestWarehouseLive261124')
 THROW 51980,'Wrong database for reviewed warehouse maintenance.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@now datetime2=SYSUTCDATETIME();
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps.WarehouseWorkspace.20261124',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51981,'Warehouse maintenance lock unavailable.',1;
 DECLARE @targets TABLE(branch_id bigint PRIMARY KEY,code varchar(60),warehouse_id bigint,new_name nvarchar(150),actor_id bigint);
 INSERT @targets(branch_id,code,warehouse_id,new_name)
 SELECT b.branch_id,b.code,w.warehouse_id,CASE b.code WHEN 'DVBV' THEN N'Kho Bùi Viện' ELSE N'Kho Cao Thắng' END
 FROM dbo.branches b JOIN dbo.warehouses w WITH(UPDLOCK,HOLDLOCK) ON w.branch_id=b.branch_id AND w.is_active=1
 WHERE b.code IN ('DVBV','DVCT') AND b.is_active=1;
 IF (SELECT COUNT(*) FROM @targets)<>2 THROW 51982,'Expected two branches with exactly one active warehouse each.',1;
 -- Attribute the warehouse document to its eligible custodian; audit explicitly records
 -- a system maintenance operation, not an interactive action performed by that employee.
 SELECT DISTINCT t.branch_id,u.user_id INTO #custodians FROM @targets t
 JOIN dbo.user_roles ur ON ur.branch_id=t.branch_id JOIN dbo.roles r ON r.role_id=ur.role_id
 JOIN dbo.users u ON u.user_id=ur.user_id JOIN dbo.role_permissions rp ON rp.role_id=r.role_id
 JOIN dbo.permissions p ON p.permission_id=rp.permission_id
 WHERE r.code='STOREKEEPER' AND r.is_active=1 AND p.code='STOCK_RECEIPT_POST' AND p.is_active=1 AND rp.data_scope IN ('BRANCH','ALL')
 AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
 AND ur.revoked_at IS NULL AND ur.assigned_at<=@now AND (ur.expires_at IS NULL OR ur.expires_at>@now)
 AND EXISTS(SELECT 1 FROM dbo.memberships m WHERE m.user_id=u.user_id AND m.branch_id=t.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=@now AND (m.valid_to IS NULL OR m.valid_to>@now));
 IF EXISTS(SELECT t.branch_id FROM @targets t LEFT JOIN #custodians c ON c.branch_id=t.branch_id GROUP BY t.branch_id HAVING COUNT(c.user_id)<>1)
 THROW 51983,'Exactly one eligible storekeeper is required for document custody.',1;
 UPDATE t SET actor_id=c.user_id FROM @targets t JOIN #custodians c ON c.branch_id=t.branch_id;
 SELECT i.item_id,i.base_unit_id INTO #items FROM dbo.inventory_items i WITH(HOLDLOCK) WHERE i.is_active=1;
 IF NOT EXISTS(SELECT 1 FROM #items) THROW 51984,'Empty active item catalog.',1;
 IF EXISTS(SELECT 1 FROM #items i LEFT JOIN dbo.reference_values u ON u.reference_id=i.base_unit_id AND u.kind='UOM' AND u.is_active=1 WHERE u.reference_id IS NULL)
 THROW 51984,'Invalid base unit.',1;
 SELECT t.warehouse_id,i.item_id,COALESCE(SUM(m.signed_base_qty),0) qty INTO #before FROM @targets t CROSS JOIN #items i
 LEFT JOIN dbo.stock_movements m WITH(UPDLOCK,HOLDLOCK) ON m.warehouse_id=t.warehouse_id AND m.item_id=i.item_id GROUP BY t.warehouse_id,i.item_id;
 DECLARE @added TABLE(warehouse_id bigint,item_id bigint,PRIMARY KEY(warehouse_id,item_id));
 INSERT dbo.audit_logs(branch_id,actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json)
 SELECT t.branch_id,NULL,NEWID(),'Warehouse',CONVERT(nvarchar(150),w.warehouse_id),'WAREHOUSE_RENAMED',
 (SELECT w.name AS name FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT t.new_name AS name,'user-request-20261124' AS [authorization] FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 FROM @targets t JOIN dbo.warehouses w ON w.warehouse_id=t.warehouse_id WHERE w.name<>t.new_name;
 UPDATE w SET name=t.new_name FROM dbo.warehouses w JOIN @targets t ON t.warehouse_id=w.warehouse_id WHERE w.name<>t.new_name;
 DECLARE @branch bigint,@warehouse bigint,@actor bigint,@code varchar(60),@doc bigint,@number varchar(60),@reason nvarchar(2000);
 SET @reason=N'Bổ sung 100 đơn vị cơ sở mỗi vật tư theo yêu cầu người dùng ngày 24/11/2026. Script vận hành thực hiện; chứng từ gắn thủ kho phụ trách, không phải thao tác đăng nhập của nhân viên.';
 DECLARE target_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT branch_id,warehouse_id,actor_id,code FROM @targets;
 OPEN target_cursor; FETCH NEXT FROM target_cursor INTO @branch,@warehouse,@actor,@code;
 WHILE @@FETCH_STATUS=0 BEGIN
  SET @number='WAREHOUSE-PLUS100-20261124-'+@code; SET @doc=NULL;
  SELECT @doc=document_id FROM dbo.inventory_documents WITH(UPDLOCK,HOLDLOCK) WHERE branch_id=@branch AND document_no=@number;
  IF @doc IS NOT NULL BEGIN
   IF NOT EXISTS(SELECT 1 FROM dbo.inventory_documents WHERE document_id=@doc AND kind='RECEIPT' AND status='POSTED' AND destination_warehouse_id=@warehouse)
    OR EXISTS(SELECT 1 FROM dbo.inventory_document_lines l LEFT JOIN dbo.stock_movements m ON m.line_id=l.line_id WHERE l.document_id=@doc AND (l.actual_qty<>100 OR l.conversion_factor<>1 OR m.movement_id IS NULL OR m.signed_base_qty<>100 OR m.warehouse_id<>@warehouse))
    OR NOT EXISTS(SELECT 1 FROM dbo.inventory_document_lines WHERE document_id=@doc)
   THROW 51985,'Existing operation receipt does not match reviewed maintenance.',1;
  END ELSE BEGIN
   INSERT dbo.inventory_documents(branch_id,document_no,kind,status,destination_warehouse_id,destination_branch_id,reason,created_by,created_at)
   VALUES(@branch,@number,'RECEIPT','DRAFT',@warehouse,@branch,@reason,@actor,@now);
   SET @doc=SCOPE_IDENTITY();
   INSERT dbo.inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,actual_qty,note)
   SELECT @branch,@doc,ROW_NUMBER() OVER(ORDER BY item_id),item_id,base_unit_id,1,100,@reason FROM #items;
   INSERT dbo.document_events(branch_id,document_id,event_type,actor_user_id,operation_key,reason)
   VALUES(@branch,@doc,'CREATED',@actor,NEWID(),@reason);
   UPDATE dbo.inventory_documents SET status='POSTED',posted_by=@actor,posted_at=@now WHERE document_id=@doc;
   INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by,occurred_at)
   OUTPUT inserted.warehouse_id,inserted.item_id INTO @added
   SELECT @branch,line_id,@warehouse,item_id,100,@actor,@now FROM dbo.inventory_document_lines WHERE document_id=@doc;
   INSERT dbo.document_events(branch_id,document_id,event_type,actor_user_id,operation_key,reason)
   VALUES(@branch,@doc,'POST',@actor,NEWID(),@reason);
   INSERT dbo.audit_logs(branch_id,actor_user_id,operation_key,entity_type,entity_key,action,after_json)
   VALUES(@branch,NULL,NEWID(),'InventoryDocument',CONVERT(nvarchar(150),@doc),'AUTHORIZED_STOCK_TOPUP',N'{"baseUnitsPerItem":100,"authorization":"user-request-20261124","executor":"maintenance-script"}');
  END
  FETCH NEXT FROM target_cursor INTO @branch,@warehouse,@actor,@code;
 END
 CLOSE target_cursor; DEALLOCATE target_cursor;
 IF EXISTS(SELECT 1 FROM #before b OUTER APPLY(SELECT SUM(m.signed_base_qty) qty FROM dbo.stock_movements m WHERE m.warehouse_id=b.warehouse_id AND m.item_id=b.item_id) a
 WHERE COALESCE(a.qty,0)<>b.qty+CASE WHEN EXISTS(SELECT 1 FROM @added x WHERE x.warehouse_id=b.warehouse_id AND x.item_id=b.item_id) THEN 100 ELSE 0 END)
 THROW 51986,'Stock delta reconciliation failed.',1;
 -- Close only resolved low-stock episodes, preserving notification history.
 UPDATE n SET expires_at=@now FROM dbo.notifications n JOIN dbo.inventory_policies p ON n.branch_id=p.branch_id AND n.event_key=p.low_stock_episode
 JOIN @targets t ON t.warehouse_id=p.warehouse_id WHERE n.notification_type='LOW_STOCK' AND n.expires_at IS NULL
 AND COALESCE((SELECT SUM(m.signed_base_qty) FROM dbo.stock_movements m WHERE m.warehouse_id=p.warehouse_id AND m.item_id=p.item_id),0)>=p.minimum_qty;
 UPDATE p SET low_stock_episode=NULL,alerted_at=NULL FROM dbo.inventory_policies p JOIN @targets t ON t.warehouse_id=p.warehouse_id
 WHERE p.low_stock_episode IS NOT NULL AND COALESCE((SELECT SUM(m.signed_base_qty) FROM dbo.stock_movements m WHERE m.warehouse_id=p.warehouse_id AND m.item_id=p.item_id),0)>=p.minimum_qty;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
