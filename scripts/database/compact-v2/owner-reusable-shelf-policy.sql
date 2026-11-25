-- Owner-authorized INTERNAL inventory review deadline, not manufacturer expiry.
-- Back up with CHECKSUM and VERIFYONLY before execution.
SET XACT_ABORT ON;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestTopup2611252300') THROW 51992,'Unexpected database.',1;
BEGIN TRANSACTION;
INSERT audit_logs(branch_id,entity_type,entity_key,action,operation_key,occurred_at,after_json)
SELECT p.branch_id,'ReusableShelfLife',CONCAT(p.branch_id,':',p.item_id),'POLICY_SET',NEWID(),SYSUTCDATETIME(),
 (SELECT '2028-11-25' deadline,N'Owner-authorized internal review deadline; not manufacturer expiry; per branch/item, not per unit or lot.' provenance FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
FROM item_lifecycle_profiles p JOIN inventory_items i ON i.item_id=p.item_id AND i.is_active=1
WHERE p.classification='REUSABLE'
 AND EXISTS(SELECT 1 FROM supply_configurations c WHERE c.branch_id=p.branch_id AND c.item_id=p.item_id AND c.is_current=1 AND c.is_active=1)
 AND NOT EXISTS(SELECT 1 FROM audit_logs a WHERE a.entity_type='ReusableShelfLife' AND a.entity_key=CONCAT(p.branch_id,':',p.item_id) AND a.action='POLICY_SET');
COMMIT;
SELECT COUNT(*) policies FROM audit_logs WHERE entity_type='ReusableShelfLife' AND action='POLICY_SET';
