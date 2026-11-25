-- V2: group B may hold a reusable shift allocation, without consumable per-guest usage.
-- Preserve all revisions and balances. Apply only after backup and the inventory lifecycle migration.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Reviewed isolated V2 TEST only',1;
IF OBJECT_ID('dbo.item_lifecycle_profiles') IS NULL THROW 51999,'Lifecycle foundation required',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.supply_configurations DROP CONSTRAINT CK_supply_config_group;
ALTER TABLE dbo.supply_configurations WITH CHECK ADD CONSTRAINT CK_supply_config_group CHECK(
 supply_group IN('A','B','C') AND initial_quantity>=0 AND minimum_stock>=0 AND per_guest_quantity>=0 AND revision>0
 AND (supply_group='A' OR per_guest_quantity=0)
 AND (supply_group<>'C' OR initial_quantity=0)
 AND (supply_group<>'A' OR is_active=0 OR (warehouse_id IS NOT NULL AND initial_quantity>0 AND per_guest_quantity>0)));
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_configuration_immutable ON dbo.supply_configurations AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT configuration_id,branch_id,item_id,asset_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,updated_by,updated_at FROM deleted
 EXCEPT SELECT configuration_id,branch_id,item_id,asset_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,updated_by,updated_at FROM inserted)
 OR EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.configuration_id=d.configuration_id WHERE d.is_current=0 OR i.is_current<>0)
 THROW 51502,''Supply configuration revisions are immutable.'',1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.supply_group=''B'' AND i.initial_quantity>0 AND i.is_current=1
 AND NOT EXISTS(SELECT 1 FROM dbo.item_lifecycle_profiles p WHERE p.branch_id=i.branch_id AND p.item_id=i.item_id AND p.classification=''REUSABLE''))
 THROW 51502,''Only reusable items can have a group B shift allocation.'',1;
END;');
COMMIT;
SELECT 'APPLIED' result,(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) businessTables,
 (SELECT COUNT(*) FROM sys.check_constraints) checks,(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) triggers;
