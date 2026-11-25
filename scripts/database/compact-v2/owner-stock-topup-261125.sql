-- Owner-authorized operational receipt, 25 November 2026. Not a test fixture.
-- Expiry dates are internal inventory deadlines chosen with owner permission,
-- NOT manufacturer-certified expiry. Existing unknown/legacy batches stay unchanged.
SET NOCOUNT ON; SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestTopup2611252300') THROW 51990,'Unexpected database.',1;
BEGIN TRANSACTION;
DECLARE @key uniqueidentifier='299d9be0-9324-432b-a407-b536b9b572a5',@now datetime2=SYSUTCDATETIME();
IF EXISTS(SELECT 1 FROM audit_logs WHERE operation_key=@key AND action='OWNER_STOCK_TOPUP') BEGIN COMMIT; SELECT 'ALREADY_APPLIED' result; RETURN; END;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps.StaffSupplies',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0 THROW 51990,'Supply lock unavailable.',1;
SELECT c.branch_id,c.warehouse_id,c.item_id,i.base_unit_id,p.classification,i.name,
 CONVERT(date,CASE WHEN i.name=N'Nước uống' THEN '20270525' WHEN i.name IN(N'Giấy vệ sinh',N'Bàn chải đánh răng') OR p.classification='REUSABLE' THEN '20281125' ELSE '20271125' END) deadline
INTO #items FROM supply_configurations c JOIN inventory_items i ON i.item_id=c.item_id
 JOIN warehouses w ON w.warehouse_id=c.warehouse_id AND w.branch_id=c.branch_id AND w.is_active=1
 JOIN item_lifecycle_profiles p ON p.branch_id=c.branch_id AND p.item_id=c.item_id
WHERE c.is_current=1 AND c.is_active=1 AND c.supply_group='A' AND i.is_active=1;
IF (SELECT COUNT(*) FROM #items)<>16 OR (SELECT COUNT(DISTINCT warehouse_id) FROM #items)<>2
 OR EXISTS(SELECT warehouse_id FROM #items GROUP BY warehouse_id HAVING COUNT(*)<>8)
 OR EXISTS(SELECT 1 FROM #items WHERE classification NOT IN('CONSUMABLE','REUSABLE')) THROW 51990,'Expected two warehouses with eight configured cleaning items each.',1;
DECLARE @branch bigint,@warehouse bigint,@actor bigint,@doc bigint,@item bigint,@unit bigint,@line bigint,@movement bigint,@batch bigint,@position bigint,@class varchar(20),@deadline date,@number int;
DECLARE warehouses CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT branch_id,warehouse_id FROM #items ORDER BY warehouse_id;
OPEN warehouses; FETCH NEXT FROM warehouses INTO @branch,@warehouse;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @actor=NULL;
 SELECT TOP(1) @actor=u.user_id FROM users u JOIN user_roles ur ON ur.user_id=u.user_id JOIN roles r ON r.role_id=ur.role_id
 WHERE ur.branch_id=@branch AND r.code='STOREKEEPER' AND r.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<=@now
 AND (ur.expires_at IS NULL OR ur.expires_at>@now) AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL ORDER BY u.user_id;
 IF @actor IS NULL THROW 51990,'Active storekeeper required for receipt provenance.',1;
 INSERT inventory_documents(branch_id,document_no,kind,status,destination_warehouse_id,destination_branch_id,created_by,created_at,reason)
 VALUES(@branch,CONCAT('OWNER-261125-',@warehouse),'RECEIPT','DRAFT',@warehouse,@branch,@actor,@now,N'Nhập 100 đơn vị mỗi vật tư dọn phòng theo yêu cầu Project Owner ngày 25/11/2026; thực hiện bằng script, không phải thao tác đăng nhập của thủ kho. Hạn quản lý nội bộ do Owner cho phép tự đặt, không xác nhận hạn nhà sản xuất.');
 SET @doc=SCOPE_IDENTITY(); SET @number=0;
 DECLARE items CURSOR LOCAL FAST_FORWARD FOR SELECT item_id,base_unit_id,classification,deadline FROM #items WHERE warehouse_id=@warehouse ORDER BY item_id;
 OPEN items; FETCH NEXT FROM items INTO @item,@unit,@class,@deadline;
 WHILE @@FETCH_STATUS=0 BEGIN
  SET @number+=1;
  INSERT inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,unit_kind,conversion_factor,actual_qty,note)
  VALUES(@branch,@doc,@number,@item,@unit,'UOM',1,100,CONCAT(N'Hạn quản lý nội bộ: ',CONVERT(varchar(10),@deadline,23),N'; Owner-authorized estimate; not manufacturer expiry.'));
  FETCH NEXT FROM items INTO @item,@unit,@class,@deadline;
 END; CLOSE items; DEALLOCATE items;
 UPDATE inventory_documents SET status='POSTED',posted_by=@actor,posted_at=@now WHERE document_id=@doc;
 DECLARE lines CURSOR LOCAL FAST_FORWARD FOR SELECT l.line_id,l.item_id,i.classification,i.deadline FROM inventory_document_lines l JOIN #items i ON i.item_id=l.item_id AND i.warehouse_id=@warehouse WHERE l.document_id=@doc ORDER BY l.line_no;
 OPEN lines; FETCH NEXT FROM lines INTO @line,@item,@class,@deadline;
 WHILE @@FETCH_STATUS=0 BEGIN
  INSERT stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by,occurred_at) VALUES(@branch,@line,@warehouse,@item,100,@actor,@now);
  SET @movement=SCOPE_IDENTITY();
  IF @class='CONSUMABLE' BEGIN
   UPDATE item_lifecycle_profiles SET expiry_policy='REQUIRED',updated_at=@now,updated_by=@actor WHERE branch_id=@branch AND item_id=@item;
   INSERT stock_batches(branch_id,warehouse_id,item_id,lot_number,received_at,expires_on,received_quantity,warehouse_quantity,reserved_quantity,status,receipt_line_id,is_legacy,created_at)
   VALUES(@branch,@warehouse,@item,CONCAT('OWNER-INTERNAL-261125-',@line),@now,@deadline,100,100,0,'READY',@line,0,@now);
   SET @batch=SCOPE_IDENTITY();
   INSERT stock_batch_entries(batch_id,movement_id,kind,quantity,occurred_at) VALUES(@batch,@movement,'WAREHOUSE',100,@now);
  END ELSE BEGIN
   SET @position=NULL;
   SELECT @position=position_id FROM reusable_positions WHERE branch_id=@branch AND item_id=@item AND warehouse_id=@warehouse AND status='READY';
   IF @position IS NULL BEGIN
    INSERT reusable_positions(branch_id,item_id,warehouse_id,location_key,status,quantity) VALUES(@branch,@item,@warehouse,CONCAT('W:',@warehouse),'READY',0);
    SET @position=SCOPE_IDENTITY();
   END;
   UPDATE reusable_positions SET quantity=quantity+100 WHERE position_id=@position;
   INSERT reusable_movements(branch_id,item_id,destination_position_id,quantity,kind,document_line_id,actor_user_id,operation_key,reason,occurred_at)
   VALUES(@branch,@item,@position,100,'RECEIPT',@line,@actor,NEWID(),CONCAT(N'Owner-authorized receipt; internal inspection deadline ',CONVERT(varchar(10),@deadline,23),N'; not manufacturer expiry.'),@now);
  END;
  FETCH NEXT FROM lines INTO @line,@item,@class,@deadline;
 END; CLOSE lines; DEALLOCATE lines;
 INSERT document_events(branch_id,document_id,event_type,revision_no,actor_user_id,operation_key,reason,occurred_at)
 VALUES(@branch,@doc,'POSTED',1,@actor,NEWID(),N'Owner-authorized scripted operational receipt: 8 x 100 base units.',@now);
 FETCH NEXT FROM warehouses INTO @branch,@warehouse;
END; CLOSE warehouses; DEALLOCATE warehouses;
IF EXISTS(SELECT 1 FROM #items i WHERE (SELECT SUM(signed_base_qty) FROM stock_movements m WHERE m.item_id=i.item_id AND m.warehouse_id=i.warehouse_id)<0) THROW 51990,'Negative stock.',1;
IF EXISTS(SELECT 1 FROM #items i WHERE i.classification='CONSUMABLE' AND
 (SELECT SUM(signed_base_qty) FROM stock_movements m WHERE m.item_id=i.item_id AND m.warehouse_id=i.warehouse_id)<>
 (SELECT SUM(warehouse_quantity) FROM stock_batches b WHERE b.item_id=i.item_id AND b.warehouse_id=i.warehouse_id)) THROW 51990,'Batch ledger mismatch.',1;
INSERT audit_logs(entity_type,entity_key,action,operation_key,occurred_at,after_json)
VALUES('InventoryReceipt','OWNER-261125','OWNER_STOCK_TOPUP',@key,@now,(SELECT *,100 quantity,N'Owner-authorized internal deadline, not manufacturer expiry' provenance FROM #items FOR JSON PATH));
COMMIT;
SELECT d.document_id,d.document_no,d.status,w.name warehouse,i.name item,l.actual_qty,l.note FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id JOIN inventory_items i ON i.item_id=l.item_id JOIN warehouses w ON w.warehouse_id=d.destination_warehouse_id WHERE d.document_no LIKE 'OWNER-261125-%' ORDER BY d.document_id,l.line_no;
