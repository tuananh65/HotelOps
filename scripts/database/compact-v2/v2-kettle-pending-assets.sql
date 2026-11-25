SET XACT_ABORT ON;SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @key uniqueidentifier='4d6db533-8bf6-45c6-a9ac-9dfd5a1e7b20',@now datetime2=SYSUTCDATETIME();
 IF EXISTS(SELECT 1 FROM audit_logs WHERE entity_type='V2_KETTLE_REGISTER' AND entity_key='222' AND action='PENDING_INSPECTION')
 BEGIN
  IF (SELECT COUNT(*) FROM assets WHERE asset_code LIKE 'MIG222-%')<>200 THROW 51991,'Existing pending register needs reconciliation.',1;
  IF EXISTS(SELECT 1 FROM assets WHERE asset_code LIKE 'MIG222-%' AND (is_active<>0 OR condition_code<>'PENDING_INSPECTION' OR room_id IS NOT NULL OR location_id IS NOT NULL OR retired_at IS NOT NULL))
    THROW 51991,'Pending assets changed after migration; manual reconciliation required.',1;
  IF (SELECT COUNT(*) FROM audit_logs WHERE operation_key=@key AND entity_type='Asset' AND action='MIGRATION_PENDING_INSPECTION')<>200
    OR EXISTS(SELECT 1 FROM assets a WHERE a.asset_code LIKE 'MIG222-%' AND NOT EXISTS(SELECT 1 FROM audit_logs l WHERE l.operation_key=@key AND l.entity_type='Asset' AND l.entity_key=CONVERT(nvarchar(100),a.asset_id) AND l.action='MIGRATION_PENDING_INSPECTION'))
    THROW 51991,'Pending asset audit mapping incomplete.',1;
  COMMIT;SELECT 'ALREADY_APPLIED' result;RETURN;
 END;
 IF NOT EXISTS(SELECT 1 FROM inventory_items WHERE item_id=222 AND code='TEST-DURABLE-KETTLE') THROW 51992,'Expected legacy kettle identity missing.',1;
 IF EXISTS(SELECT 1 FROM assets WHERE asset_code LIKE 'MIG222-%') THROW 51993,'Partial or conflicting migration found.',1;
 IF EXISTS(SELECT 1 FROM item_lifecycle_profiles WHERE item_id=222 AND classification<>'ASSET') THROW 51994,'Kettle classification must be ASSET.',1;
 CREATE TABLE #origins(branch_id bigint,warehouse_id bigint NULL,room_id bigint NULL,line_id bigint NULL,quantity int,source_code varchar(70));
 INSERT #origins
 SELECT m.branch_id,m.warehouse_id,NULL,NULL,CONVERT(int,SUM(m.signed_base_qty)),CONCAT('W',m.warehouse_id)
 FROM stock_movements m WHERE m.item_id=222 GROUP BY m.branch_id,m.warehouse_id;
 INSERT #origins
 SELECT d.branch_id,NULL,d.receiving_room_id,l.line_id,CONVERT(int,-m.signed_base_qty),CONCAT('L',l.line_id)
 FROM stock_movements m JOIN inventory_document_lines l ON m.line_id=l.line_id
 JOIN inventory_documents d ON d.document_id=l.document_id
 WHERE m.item_id=222 AND d.kind='ISSUE' AND d.posted_at IS NOT NULL AND d.receiving_room_id IS NOT NULL
 AND m.signed_base_qty<0 AND NOT EXISTS(SELECT 1 FROM stock_movements reversal WHERE reversal.reversal_of_movement_id=m.movement_id);
 IF (SELECT SUM(quantity) FROM #origins)<>200 OR (SELECT SUM(quantity) FROM #origins WHERE warehouse_id IS NOT NULL)<>103
    OR (SELECT SUM(quantity) FROM #origins WHERE room_id IS NOT NULL)<>97 OR EXISTS(SELECT 1 FROM #origins WHERE quantity<0)
    THROW 51995,'Legacy kettle totals changed; repeat audit before migration.',1;
 IF EXISTS(SELECT 1 FROM stock_movements WHERE item_id=222 AND signed_base_qty<>FLOOR(signed_base_qty)) THROW 51996,'Fractional asset quantity cannot be mapped.',1;
 ;WITH numbers AS(SELECT TOP(200) ROW_NUMBER() OVER(ORDER BY object_id) n FROM sys.all_objects)
 SELECT o.*,n.n,CONCAT('MIG222-',o.branch_id,'-',o.source_code,'-',n.n) asset_code INTO #units
 FROM #origins o JOIN numbers n ON n.n<=o.quantity;
 -- Book-location references remain audit evidence. Actual room/location, condition,
 -- serial, purchase date and custodian remain unverified and are not fabricated.
 INSERT assets(branch_id,asset_code,name,condition_code,is_active,created_at,asset_type_kind)
 SELECT branch_id,asset_code,N'Ấm đun — chờ kiểm kê','PENDING_INSPECTION',0,@now,'ASSET_TYPE' FROM #units;
 INSERT audit_logs(branch_id,operation_key,entity_type,entity_key,action,after_json,occurred_at)
 SELECT a.branch_id,@key,'Asset',CONVERT(nvarchar(100),a.asset_id),'MIGRATION_PENDING_INSPECTION',
  (SELECT 222 itemId,u.warehouse_id bookWarehouseId,u.room_id bookRoomId,u.line_id sourceLineId,u.n sourceUnit,
    1 registeredQuantity,'D11_OWNER_PENDING_PHYSICAL_COUNT' decision,'NOT_ADDITIONAL_STOCK' accountingTreatment FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@now
 FROM #units u JOIN assets a ON a.asset_code=u.asset_code AND a.branch_id=u.branch_id;
 IF @@ROWCOUNT<>200 THROW 51997,'Pending asset audit mapping incomplete.',1;
 INSERT audit_logs(operation_key,entity_type,entity_key,action,after_json,occurred_at)
 VALUES(@key,'V2_KETTLE_REGISTER','222','PENDING_INSPECTION',N'{"registered":200,"bookWarehouse":103,"bookRoomIssues":97,"stockMovementsUnchanged":true,"noPhysicalVerification":true}',@now);
 COMMIT;
 SELECT 'APPLIED' result,COUNT(*) pending_assets FROM assets WHERE asset_code LIKE 'MIG222-%' AND condition_code='PENDING_INSPECTION' AND is_active=0;
 SELECT branch_id,COUNT(*) pending_assets FROM assets WHERE asset_code LIKE 'MIG222-%' GROUP BY branch_id;
 SELECT branch_id,warehouse_id,SUM(signed_base_qty) retained_book_balance FROM stock_movements WHERE item_id=222 GROUP BY branch_id,warehouse_id;
END TRY
BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
