SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST required.',1;
IF (SELECT COUNT(*) FROM assets WHERE asset_code LIKE 'MIG222-%' AND condition_code='PENDING_INSPECTION' AND is_active=0 AND room_id IS NULL AND location_id IS NULL AND serial_number IS NULL AND retired_at IS NULL)<>200 THROW 51991,'Pending asset state mismatch.',1;
IF (SELECT COUNT(*) FROM audit_logs WHERE entity_type='Asset' AND action='MIGRATION_PENDING_INSPECTION' AND operation_key='4d6db533-8bf6-45c6-a9ac-9dfd5a1e7b20')<>200 THROW 51992,'Audit mapping mismatch.',1;
IF (SELECT COUNT(*) FROM stock_movements WHERE item_id=222)<>99 OR (SELECT SUM(signed_base_qty) FROM stock_movements WHERE item_id=222)<>103 THROW 51993,'Legacy ledger changed.',1;
IF EXISTS(SELECT 1 FROM assets WHERE asset_code LIKE 'MIG222-%' GROUP BY branch_id HAVING COUNT(*)<>100) THROW 51994,'Branch mapping mismatch.',1;
SELECT 'PASS' result,200 pending_assets,103 legacy_warehouse_units,97 legacy_room_issues,0 new_stock_movements;
SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys) foreign_keys,
 (SELECT COUNT(*) FROM sys.check_constraints) checks,(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) triggers;
DBCC CHECKCONSTRAINTS('dbo.assets') WITH ALL_CONSTRAINTS;
