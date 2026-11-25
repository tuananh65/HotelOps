SET XACT_ABORT ON; SET NOCOUNT ON;
IF DB_NAME()<>@ExpectedV2Database OR (DB_NAME()<>'HotelOpsCompactDB' AND DB_NAME() NOT LIKE 'HotelOpsCompactWorkdayTestOfficial%')
 THROW 51990,'Exact authorized official/rehearsal database required.',1;
IF @@TRANCOUNT<>1 THROW 51990,'Official kettle check requires the outer upgrade transaction.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.inventory_items WHERE item_id=222 AND code='TEST-DURABLE-KETTLE')
 THROW 51992,'Expected preserved kettle catalog identity missing.',1;
IF EXISTS(SELECT 1 FROM dbo.stock_movements WHERE item_id=222)
 OR EXISTS(SELECT 1 FROM dbo.inventory_document_lines WHERE item_id=222)
 THROW 51995,'Kettle transactions remain after authorized cleanup; ownership needs reconciliation.',1;
IF EXISTS(SELECT 1 FROM dbo.assets WHERE asset_code LIKE 'MIG222-%')
 OR EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type='V2_KETTLE_REGISTER' AND entity_key='222' AND action='PENDING_INSPECTION')
 THROW 51993,'A previous synthetic kettle register exists; do not silently discard registered assets.',1;
IF EXISTS(SELECT 1 FROM dbo.item_lifecycle_profiles WHERE item_id=222 AND classification<>'ASSET')
 OR (SELECT COUNT(*) FROM dbo.item_lifecycle_profiles WHERE item_id=222 AND classification='ASSET')<>(SELECT COUNT(*) FROM dbo.branches)
 THROW 51994,'Preserved kettle catalog must be classified as ASSET in every branch.',1;
IF EXISTS(SELECT 1 FROM dbo.staff_supply_balances WHERE item_id=222 AND (initial_issued<>0 OR additional_issued<>0 OR used<>0 OR returned<>0 OR reserved<>0))
 OR EXISTS(SELECT 1 FROM dbo.reusable_positions WHERE item_id=222 AND quantity<>0)
 OR EXISTS(SELECT 1 FROM dbo.stock_batches WHERE item_id=222 AND (received_quantity<>0 OR warehouse_quantity<>0 OR reserved_quantity<>0))
 THROW 51995,'Nonzero kettle ownership/custody remains; physical reconciliation required.',1;
INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,after_json,occurred_at)
VALUES(NEWID(),'V2_KETTLE_REGISTER','222','NO_SYNTHETIC_OWNERSHIP',
 N'{"registered":0,"ownerDecision":"2026-11-25-delete-confirmed-test-including-MR440-ISSUE441","syntheticStockRemoved":true,"catalogPreserved":true,"physicalOwnershipNotInvented":true,"previous200UnitTestRegisterNotCreated":true}',SYSUTCDATETIME());
