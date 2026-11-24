-- Authorized local demonstration data, 24 November 2026. No schema/grant changes.
-- @DevelopFill102 may be enabled only for the explicitly approved Develop exception.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestP0261124','HotelOpsCompactWorkdayTestRoomSupplies261124')
 THROW 51990,'Unexpected room-supplies database.',1;
IF @DevelopFill102=1 AND DB_NAME()<>'HotelOpsCompactWorkdayTestP0261124'
 THROW 51990,'The 102-unit exception is restricted to Develop.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@now datetime2=SYSUTCDATETIME();
 EXEC @lock=sys.sp_getapplock @Resource='HotelOps.StaffSupplies',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51991,'Supply lock unavailable.',1;
 DECLARE @reason nvarchar(2000)=N'DỮ LIỆU KIỂM THỬ được người dùng duyệt 24/11/2026: bổ sung đồ fill và đồ dùng bền cho phòng; không phải xác nhận nhận hàng thực tế.';
 SELECT b.branch_id,b.code,w.warehouse_id INTO #branches FROM branches b
 JOIN warehouses w WITH(UPDLOCK,HOLDLOCK) ON w.branch_id=b.branch_id AND w.is_active=1
 WHERE b.code IN('DVBV','DVCT') AND b.is_active=1;
 IF (SELECT COUNT(*) FROM #branches)<>2 OR (SELECT COUNT(DISTINCT branch_id) FROM #branches)<>2
 THROW 51991,'Expected one active warehouse for each target branch.',1;
 SELECT DISTINCT b.branch_id,u.user_id,r.code INTO #actors FROM #branches b
 JOIN user_roles ur ON ur.branch_id=b.branch_id JOIN roles r ON r.role_id=ur.role_id JOIN users u ON u.user_id=ur.user_id
 WHERE r.code IN('STOREKEEPER','OPERATION_MANAGER','HOUSEKEEPING_SUPERVISOR') AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<=@now
 AND (ur.expires_at IS NULL OR ur.expires_at>@now) AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
 AND EXISTS(SELECT 1 FROM memberships m WHERE m.user_id=u.user_id AND m.branch_id=b.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=@now AND (m.valid_to IS NULL OR m.valid_to>@now))
 AND EXISTS(SELECT 1 FROM role_permissions rp JOIN permissions p ON p.permission_id=rp.permission_id WHERE rp.role_id=r.role_id AND p.is_active=1 AND rp.data_scope IN('BRANCH','ALL') AND p.code=CASE r.code WHEN 'STOREKEEPER' THEN 'STOCK_ISSUE_POST' WHEN 'HOUSEKEEPING_SUPERVISOR' THEN 'IR_PROCESS' ELSE 'ROOM_SUPPLY_CONFIG' END);
 IF EXISTS(SELECT b.branch_id FROM #branches b LEFT JOIN #actors a ON a.branch_id=b.branch_id GROUP BY b.branch_id HAVING COUNT(DISTINCT a.code)<>3)
 THROW 51991,'Eligible storekeeper, housekeeping supervisor and operations manager required.',1;
 DECLARE @unit bigint=(SELECT MIN(reference_id) FROM reference_values WHERE kind='UOM' AND is_active=1 AND name=N'Cái' AND base_unit_id IS NULL);
 IF @unit IS NULL THROW 51992,'Existing base unit Cai required.',1;
 DECLARE @catalog TABLE(code varchar(60),name nvarchar(150));
 INSERT @catalog VALUES('TEST-DURABLE-KETTLE',N'Ấm đun'),('TEST-DURABLE-GLASS',N'Ly uống nước'),('TEST-DURABLE-HANGER',N'Móc áo'),('TEST-DURABLE-BIN',N'Thùng rác');
 IF EXISTS(SELECT 1 FROM @catalog c JOIN inventory_items i ON i.code=c.code WHERE i.name<>c.name OR i.base_unit_id<>@unit OR i.is_active=0)
 THROW 51992,'Existing test catalog conflicts with this operation.',1;
 INSERT inventory_items(code,name,base_unit_id,description)
 SELECT c.code,c.name,@unit,@reason FROM @catalog c WHERE NOT EXISTS(SELECT 1 FROM inventory_items i WITH(UPDLOCK,HOLDLOCK) WHERE i.code=c.code);
 SELECT i.item_id,i.base_unit_id INTO #durables FROM inventory_items i JOIN @catalog c ON c.code=i.code;
 INSERT supply_configurations(branch_id,item_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,is_current,updated_by,updated_at)
 SELECT b.branch_id,i.item_id,'B',b.warehouse_id,i.base_unit_id,0,0,0,1,1,1,(SELECT MIN(user_id) FROM #actors a WHERE a.branch_id=b.branch_id AND a.code='OPERATION_MANAGER'),@now
 FROM #branches b CROSS JOIN #durables i WHERE NOT EXISTS(SELECT 1 FROM supply_configurations c WHERE c.branch_id=b.branch_id AND c.item_id=i.item_id AND c.is_current=1);
 IF EXISTS(SELECT 1 FROM #branches b CROSS JOIN #durables i JOIN supply_configurations c ON c.item_id=i.item_id AND c.branch_id=b.branch_id AND c.is_current=1 WHERE c.supply_group<>'B' OR c.is_active=0 OR c.warehouse_id<>b.warehouse_id)
 THROW 51992,'Durable configuration conflicts with this operation.',1;
 INSERT inventory_policies(branch_id,warehouse_id,item_id,minimum_qty,target_qty)
 SELECT b.branch_id,b.warehouse_id,i.item_id,0,0 FROM #branches b CROSS JOIN #durables i
 WHERE NOT EXISTS(SELECT 1 FROM inventory_policies p WHERE p.branch_id=b.branch_id AND p.warehouse_id=b.warehouse_id AND p.item_id=i.item_id);
 SELECT c.branch_id,c.item_id,i.base_unit_id,c.supply_group,c.per_guest_quantity,b.warehouse_id,b.code,
 CAST(CASE WHEN @DevelopFill102=1 AND b.code='DVBV' AND c.supply_group='A' THEN 102 ELSE 100 END AS decimal(18,4)) receipt_qty
 INTO #items FROM supply_configurations c JOIN #branches b ON b.branch_id=c.branch_id JOIN inventory_items i ON i.item_id=c.item_id
 WHERE c.is_current=1 AND c.is_active=1 AND i.is_active=1 AND (c.supply_group='A' OR c.supply_group='B' AND EXISTS(SELECT 1 FROM #durables d WHERE d.item_id=c.item_id));
 IF EXISTS(SELECT b.branch_id FROM #branches b LEFT JOIN #items i ON i.branch_id=b.branch_id GROUP BY b.branch_id HAVING SUM(CASE WHEN i.supply_group='A' THEN 1 ELSE 0 END)<>8 OR SUM(CASE WHEN i.supply_group='B' THEN 1 ELSE 0 END)<>4)
 THROW 51992,'Expected eight configured fill items and four approved durable items.',1;
 SELECT r.branch_id,r.room_id,rt.capacity INTO #rooms FROM rooms r JOIN #branches b ON b.branch_id=r.branch_id JOIN room_types rt ON rt.room_type_id=r.room_type_id AND rt.branch_id=r.branch_id
 WHERE r.is_active=1 AND rt.is_active=1;
 IF EXISTS(SELECT 1 FROM #rooms WHERE capacity<=0) OR NOT EXISTS(SELECT 1 FROM #rooms) THROW 51992,'Invalid active room capacity.',1;
 -- Room possession is a room/item pair, never a name match or a warehouse-wide quantity.
 SELECT d.receiving_room_id room_id,l.item_id,l.line_id,-m.signed_base_qty quantity INTO #issued FROM inventory_documents d
 JOIN inventory_document_lines l ON l.document_id=d.document_id JOIN stock_movements m ON m.line_id=l.line_id
 JOIN #rooms r ON r.room_id=d.receiving_room_id AND r.branch_id=d.branch_id
 WHERE d.kind='ISSUE' AND d.posted_at IS NOT NULL AND m.signed_base_qty<0 AND NOT EXISTS(SELECT 1 FROM stock_movements rev WHERE rev.reversal_of_movement_id=m.movement_id);
 SELECT issued.room_id,issued.item_id,SUM(m.signed_base_qty) quantity INTO #returned FROM #issued issued
 JOIN inventory_document_lines l ON l.source_line_id=issued.line_id JOIN inventory_documents d ON d.document_id=l.document_id JOIN stock_movements m ON m.line_id=l.line_id
 WHERE d.kind='RETURN' AND d.posted_at IS NOT NULL AND NOT EXISTS(SELECT 1 FROM stock_movements rev WHERE rev.reversal_of_movement_id=m.movement_id)
 GROUP BY issued.room_id,issued.item_id;
 SELECT r.branch_id,r.room_id,i.item_id,i.base_unit_id,i.warehouse_id,
 CAST(CASE i.supply_group WHEN 'A' THEN r.capacity*i.per_guest_quantity ELSE 1 END AS decimal(18,4)) target_qty,
 COALESCE((SELECT SUM(x.quantity) FROM #issued x WHERE x.room_id=r.room_id AND x.item_id=i.item_id),0)-COALESCE((SELECT x.quantity FROM #returned x WHERE x.room_id=r.room_id AND x.item_id=i.item_id),0) existing_qty
 INTO #desired FROM #rooms r JOIN #items i ON i.branch_id=r.branch_id;
 SELECT i.warehouse_id,i.item_id,COALESCE(SUM(m.signed_base_qty),0) quantity INTO #before FROM #items i
 LEFT JOIN stock_movements m WITH(UPDLOCK,HOLDLOCK) ON m.warehouse_id=i.warehouse_id AND m.item_id=i.item_id GROUP BY i.warehouse_id,i.item_id;
 CREATE TABLE #new_movements(warehouse_id bigint,item_id bigint,quantity decimal(18,4));
 CREATE TABLE #documents(branch_id bigint,warehouse_id bigint,room_id bigint NULL,document_no varchar(60),kind varchar(30));
 INSERT #documents SELECT branch_id,warehouse_id,NULL,'TEST-ROOM-261124-IN-'+code,'RECEIPT' FROM #branches;
 INSERT #documents SELECT r.branch_id,b.warehouse_id,r.room_id,'TEST-ROOM-261124-OUT-'+CONVERT(varchar(20),r.room_id),'ISSUE' FROM #rooms r JOIN #branches b ON b.branch_id=r.branch_id;
 DECLARE @branch bigint,@warehouse bigint,@room bigint,@number varchar(60),@kind varchar(30),@actor bigint,@doc bigint,@request bigint,@approver bigint,@created int=0;
 DECLARE doc_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT branch_id,warehouse_id,room_id,document_no,kind FROM #documents ORDER BY CASE kind WHEN 'RECEIPT' THEN 0 ELSE 1 END,branch_id,room_id;
 OPEN doc_cursor;FETCH NEXT FROM doc_cursor INTO @branch,@warehouse,@room,@number,@kind;
 WHILE @@FETCH_STATUS=0 BEGIN
  SET @actor=(SELECT MIN(user_id) FROM #actors WHERE branch_id=@branch AND code='STOREKEEPER');
  SET @doc=(SELECT document_id FROM inventory_documents WITH(UPDLOCK,HOLDLOCK) WHERE branch_id=@branch AND document_no=@number);
  IF @doc IS NOT NULL BEGIN
   IF NOT EXISTS(SELECT 1 FROM inventory_documents WHERE document_id=@doc AND kind=@kind AND status='POSTED' AND ((@kind='RECEIPT' AND destination_warehouse_id=@warehouse) OR (@kind='ISSUE' AND receiving_room_id=@room AND source_warehouse_id=@warehouse)))
    THROW 51993,'Existing document conflicts with this operation.',1;
   IF EXISTS(SELECT 1 FROM inventory_document_lines l LEFT JOIN stock_movements m ON m.line_id=l.line_id WHERE l.document_id=@doc AND (m.movement_id IS NULL OR m.warehouse_id<>@warehouse OR m.item_id<>l.item_id OR m.signed_base_qty<>CASE @kind WHEN 'RECEIPT' THEN l.actual_qty ELSE -l.actual_qty END))
    THROW 51993,'Existing posted document has inconsistent movements.',1;
  END ELSE IF @kind='RECEIPT' OR EXISTS(SELECT 1 FROM #desired WHERE room_id=@room AND target_qty>existing_qty) BEGIN
   IF @kind='ISSUE' AND EXISTS(SELECT 1 FROM #desired d WHERE d.room_id=@room AND d.target_qty>d.existing_qty AND COALESCE((SELECT SUM(m.signed_base_qty) FROM stock_movements m WITH(UPDLOCK,HOLDLOCK) WHERE m.warehouse_id=@warehouse AND m.item_id=d.item_id),0)<d.target_qty-d.existing_qty)
    THROW 51994,'Insufficient warehouse stock; entire operation rolled back.',1;
   SET @request=NULL;
   IF @kind='ISSUE' BEGIN
    SET @approver=(SELECT MIN(user_id) FROM #actors WHERE branch_id=@branch AND code='HOUSEKEEPING_SUPERVISOR' AND user_id<>@actor);
    IF @approver IS NULL THROW 51993,'Independent supervisor required for synthetic approved request.',1;
    -- Synthetic approvals belong only to this explicitly authorized data fixture.
    -- The audit actor stays NULL and the reason identifies the maintenance executor.
    INSERT inventory_documents(branch_id,document_no,kind,status,source_warehouse_id,receiving_room_id,reason,created_by,created_at)
    VALUES(@branch,'TEST-ROOM-261124-MR-'+CONVERT(varchar(20),@room),'MATERIAL_REQUEST','DRAFT',@warehouse,@room,@reason,@actor,@now);
    SET @request=SCOPE_IDENTITY();SET @created+=1;
    INSERT inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,requested_qty,approved_qty,actual_qty,note)
    SELECT @branch,@request,ROW_NUMBER() OVER(ORDER BY item_id),item_id,base_unit_id,1,target_qty-existing_qty,target_qty-existing_qty,target_qty-existing_qty,@reason FROM #desired WHERE room_id=@room AND target_qty>existing_qty;
    UPDATE inventory_documents SET status='APPROVED',approved_by=@approver,approved_at=@now WHERE document_id=@request;
    INSERT document_events(branch_id,document_id,event_type,actor_user_id,operation_key,reason)
    VALUES(@branch,@request,'CREATED',@actor,NEWID(),@reason),(@branch,@request,'APPROVE',@approver,NEWID(),@reason);
    INSERT audit_logs(branch_id,actor_user_id,operation_key,entity_type,entity_key,action,after_json)
    VALUES(@branch,NULL,NEWID(),'InventoryDocument',CONVERT(nvarchar(150),@request),'AUTHORIZED_ROOM_TEST_DATA',N'{"authorization":"user-approved-20261124-room-supplies","executor":"maintenance-script","synthetic":true}');
   END
   INSERT inventory_documents(branch_id,document_no,kind,status,source_warehouse_id,destination_warehouse_id,destination_branch_id,receiving_room_id,source_document_id,reason,created_by,created_at)
   VALUES(@branch,@number,@kind,'DRAFT',CASE WHEN @kind='ISSUE' THEN @warehouse END,CASE WHEN @kind='RECEIPT' THEN @warehouse END,CASE WHEN @kind='RECEIPT' THEN @branch END,@room,@request,@reason,@actor,@now);
   SET @doc=SCOPE_IDENTITY();SET @created+=1;
   IF @kind='RECEIPT'
    INSERT inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,actual_qty,note)
    SELECT @branch,@doc,ROW_NUMBER() OVER(ORDER BY item_id),item_id,base_unit_id,1,receipt_qty,@reason FROM #items WHERE branch_id=@branch;
   ELSE
    INSERT inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,actual_qty,source_line_id,note)
    SELECT @branch,@doc,line_no,item_id,unit_id,1,approved_qty,line_id,@reason FROM inventory_document_lines WHERE document_id=@request;
   INSERT document_events(branch_id,document_id,event_type,actor_user_id,operation_key,reason) VALUES(@branch,@doc,'CREATED',@actor,NEWID(),@reason);
   UPDATE inventory_documents SET status='POSTED',posted_by=@actor,posted_at=@now WHERE document_id=@doc;
   INSERT stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by,occurred_at)
   OUTPUT inserted.warehouse_id,inserted.item_id,inserted.signed_base_qty INTO #new_movements
   SELECT @branch,line_id,@warehouse,item_id,CASE @kind WHEN 'RECEIPT' THEN actual_qty ELSE -actual_qty END,@actor,@now FROM inventory_document_lines WHERE document_id=@doc;
   IF @request IS NOT NULL UPDATE inventory_documents SET status='FULLY_ISSUED' WHERE document_id=@request;
   INSERT document_events(branch_id,document_id,event_type,actor_user_id,operation_key,reason) VALUES(@branch,@doc,'POST',@actor,NEWID(),@reason);
   INSERT audit_logs(branch_id,actor_user_id,operation_key,entity_type,entity_key,action,after_json)
   VALUES(@branch,NULL,NEWID(),'InventoryDocument',CONVERT(nvarchar(150),@doc),'AUTHORIZED_ROOM_TEST_DATA',N'{"authorization":"user-approved-20261124-room-supplies","executor":"maintenance-script","synthetic":true}');
  END
  FETCH NEXT FROM doc_cursor INTO @branch,@warehouse,@room,@number,@kind;
 END
 CLOSE doc_cursor;DEALLOCATE doc_cursor;
 IF EXISTS(SELECT 1 FROM #before b OUTER APPLY(SELECT SUM(m.signed_base_qty) qty FROM stock_movements m WHERE m.warehouse_id=b.warehouse_id AND m.item_id=b.item_id) current_stock
 OUTER APPLY(SELECT SUM(n.quantity) qty FROM #new_movements n WHERE n.warehouse_id=b.warehouse_id AND n.item_id=b.item_id) delta
 WHERE COALESCE(current_stock.qty,0)<>b.quantity+COALESCE(delta.qty,0) OR current_stock.qty<0)
 THROW 51995,'Warehouse reconciliation failed.',1;
 -- Resolve existing low-stock episodes only when their actual threshold is satisfied.
 UPDATE n SET expires_at=@now FROM notifications n JOIN inventory_policies p ON p.branch_id=n.branch_id AND p.low_stock_episode=n.event_key
 JOIN #branches b ON b.warehouse_id=p.warehouse_id WHERE n.notification_type='LOW_STOCK' AND n.expires_at IS NULL
 AND COALESCE((SELECT SUM(signed_base_qty) FROM stock_movements m WHERE m.warehouse_id=p.warehouse_id AND m.item_id=p.item_id),0)>=p.minimum_qty;
 UPDATE p SET low_stock_episode=NULL,alerted_at=NULL FROM inventory_policies p JOIN #branches b ON b.warehouse_id=p.warehouse_id
 WHERE p.low_stock_episode IS NOT NULL AND COALESCE((SELECT SUM(signed_base_qty) FROM stock_movements m WHERE m.warehouse_id=p.warehouse_id AND m.item_id=p.item_id),0)>=p.minimum_qty;
 COMMIT;
 SELECT @created documents_created,(SELECT COUNT(*) FROM #rooms) rooms,(SELECT COUNT(*) FROM #desired) room_item_pairs;
 SELECT b.code branch,i.supply_group,i.item_id,(SELECT name FROM inventory_items WHERE item_id=i.item_id) item,
 COALESCE((SELECT SUM(m.signed_base_qty) FROM stock_movements m WHERE m.warehouse_id=i.warehouse_id AND m.item_id=i.item_id),0) warehouse_stock
 FROM #items i JOIN #branches b ON b.branch_id=i.branch_id ORDER BY b.code,i.supply_group,i.item_id;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
