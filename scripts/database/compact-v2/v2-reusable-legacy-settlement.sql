SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST required.',1;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.staff_supply_balances','reclassified_quantity') IS NULL
 ALTER TABLE dbo.staff_supply_balances ADD reclassified_quantity decimal(18,4) NOT NULL CONSTRAINT DF_supply_balance_reclassified DEFAULT 0;
EXEC(N'
IF NOT EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type=''V2_REUSABLE_SETTLEMENT'' AND entity_key=''261125-v1'')
BEGIN
 IF EXISTS(SELECT a.branch_id,b.item_id,a.user_id FROM dbo.staff_supply_balances b
 JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id
 JOIN dbo.item_lifecycle_profiles p ON p.branch_id=a.branch_id AND p.item_id=b.item_id AND p.classification=''REUSABLE''
 GROUP BY a.branch_id,b.item_id,a.user_id
 HAVING SUM(b.initial_issued+b.additional_issued-b.used-b.returned-b.reclassified_quantity)>
 COALESCE((SELECT SUM(r.quantity) FROM dbo.reusable_positions r WHERE r.branch_id=a.branch_id AND r.item_id=b.item_id AND r.user_id=a.user_id AND r.status=''STAFF_HOLDING''),0))
 THROW 51994,''Legacy custody has moved: reconcile before settling old balances.'',1;
 INSERT dbo.audit_logs(entity_type,entity_key,action,operation_key,occurred_at,after_json)
 SELECT ''StaffSupplyBalance'',CONVERT(varchar(30),b.balance_id),''RECLASSIFIED_TO_REUSABLE'',NEWID(),SYSUTCDATETIME(),
 (SELECT b.balance_id balanceId,b.item_id itemId,a.branch_id branchId,a.user_id userId,r.position_id reusablePositionId,
 b.initial_issued+b.additional_issued-b.used-b.returned quantity,''Transfer of accounting authority; no physical issue or return'' explanation FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.staff_supply_balances b JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id
 JOIN dbo.item_lifecycle_profiles p ON p.branch_id=a.branch_id AND p.item_id=b.item_id AND p.classification=''REUSABLE''
 JOIN dbo.reusable_positions r ON r.branch_id=a.branch_id AND r.item_id=b.item_id AND r.user_id=a.user_id AND r.status=''STAFF_HOLDING''
 WHERE b.initial_issued+b.additional_issued-b.used-b.returned>0;
 UPDATE b SET reclassified_quantity=b.initial_issued+b.additional_issued-b.used-b.returned
 FROM dbo.staff_supply_balances b JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id
 JOIN dbo.item_lifecycle_profiles p ON p.branch_id=a.branch_id AND p.item_id=b.item_id AND p.classification=''REUSABLE'';
 INSERT dbo.audit_logs(entity_type,entity_key,action,operation_key,occurred_at,after_json)
 VALUES(''V2_REUSABLE_SETTLEMENT'',''261125-v1'',''APPLIED'',NEWID(),SYSUTCDATETIME(),''{"oldDocumentsPreserved":true,"physicalQuantityUnchanged":true}'');
END;');
-- Extend the existing CHECK rather than disable it. Obsolete reserved holds are released
-- by the next authorized work operation; they do not represent additional physical stock.
DECLARE @definition nvarchar(max)=(SELECT definition FROM sys.check_constraints WHERE name='CK_supply_balance_nonnegative');
IF @definition NOT LIKE '%reclassified_quantity%'
BEGIN
 EXEC(N'ALTER TABLE dbo.staff_supply_balances DROP CONSTRAINT CK_supply_balance_nonnegative; ALTER TABLE dbo.staff_supply_balances WITH CHECK ADD CONSTRAINT CK_supply_balance_nonnegative CHECK(('+@definition+N') AND reclassified_quantity>=0 AND reclassified_quantity<=initial_issued+additional_issued-used-returned);');
END;
DECLARE @guard nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_supply_balance_scope'));
IF @guard NOT LIKE '%i.reclassified_quantity%'
BEGIN
 SET @guard=REPLACE(@guard,'i.returned<d.returned)','i.returned<d.returned OR i.reclassified_quantity<>d.reclassified_quantity)');
 IF @guard NOT LIKE '%i.reclassified_quantity%' THROW 51995,'Unexpected balance guard baseline.',1;
 SET @guard=REPLACE(@guard,'CREATE OR ALTER TRIGGER','ALTER TRIGGER');
 SET @guard=REPLACE(@guard,'CREATE   TRIGGER','ALTER TRIGGER');
 SET @guard=REPLACE(@guard,'CREATE TRIGGER','ALTER TRIGGER');
 EXEC sp_executesql @guard;
END;
COMMIT;
EXEC(N'SELECT COUNT(*) converted_balances,SUM(reclassified_quantity) transferred_quantity FROM dbo.staff_supply_balances WHERE reclassified_quantity>0;');
