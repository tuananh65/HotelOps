SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
-- Read-only audit. Run between workflow probes; never repair differences automatically.
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
DECLARE @checks TABLE(check_name varchar(80),checked_count bigint,mismatch_count bigint);
DECLARE @differences TABLE(check_name varchar(80),entity_id bigint,expected decimal(18,4),actual decimal(18,4));

;WITH ledger AS (
 SELECT m.branch_id,m.warehouse_id,m.item_id,SUM(m.signed_base_qty) quantity
 FROM dbo.stock_movements m JOIN dbo.item_lifecycle_profiles p ON p.branch_id=m.branch_id AND p.item_id=m.item_id
 WHERE p.classification='CONSUMABLE' GROUP BY m.branch_id,m.warehouse_id,m.item_id
), batches AS (
 SELECT b.branch_id,b.warehouse_id,b.item_id,SUM(b.warehouse_quantity) quantity
 FROM dbo.stock_batches b JOIN dbo.item_lifecycle_profiles p ON p.branch_id=b.branch_id AND p.item_id=b.item_id
 WHERE p.classification='CONSUMABLE' GROUP BY b.branch_id,b.warehouse_id,b.item_id
)
INSERT @differences SELECT 'consumable_warehouse',COALESCE(l.item_id,b.item_id),COALESCE(l.quantity,0),COALESCE(b.quantity,0)
FROM ledger l FULL JOIN batches b ON l.branch_id=b.branch_id AND l.warehouse_id=b.warehouse_id AND l.item_id=b.item_id;

INSERT @differences SELECT 'consumable_staff_batches',b.balance_id,
 b.initial_issued+b.additional_issued-b.used-b.returned-b.reclassified_quantity,
 COALESCE((SELECT SUM(h.quantity) FROM dbo.staff_batch_balances h WHERE h.balance_id=b.balance_id),0)
FROM dbo.staff_supply_balances b JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id
JOIN dbo.item_lifecycle_profiles p ON p.branch_id=a.branch_id AND p.item_id=b.item_id WHERE p.classification='CONSUMABLE';

INSERT @differences SELECT 'active_reservations',b.balance_id,b.reserved,
 COALESCE((SELECT SUM(r.quantity) FROM dbo.supply_reservations r WHERE r.balance_id=b.balance_id AND r.status='RESERVED'),0)
FROM dbo.staff_supply_balances b;

;WITH ledger AS (
 SELECT m.branch_id,m.warehouse_id,m.item_id,SUM(m.signed_base_qty) quantity
 FROM dbo.stock_movements m JOIN dbo.item_lifecycle_profiles p ON p.branch_id=m.branch_id AND p.item_id=m.item_id
 WHERE p.classification='REUSABLE' GROUP BY m.branch_id,m.warehouse_id,m.item_id
), custody AS (
 SELECT branch_id,warehouse_id,item_id,SUM(quantity) quantity FROM dbo.reusable_positions
 WHERE warehouse_id IS NOT NULL AND status NOT IN('IN_TRANSIT','WRITTEN_OFF','LEGACY_REVIEW')
 GROUP BY branch_id,warehouse_id,item_id
)
INSERT @differences SELECT 'reusable_warehouse',COALESCE(l.item_id,c.item_id),COALESCE(l.quantity,0),COALESCE(c.quantity,0)
FROM ledger l FULL JOIN custody c ON l.branch_id=c.branch_id AND l.warehouse_id=c.warehouse_id AND l.item_id=c.item_id;

-- Reconstruct each custody position from documented migration openings and all
-- subsequent movements. This includes staff, rooms, transit and written-off
-- positions; LEGACY_REVIEW remains an accounting check, never physical approval.
DECLARE @reusableMigrationAt datetime2=(SELECT MIN(occurred_at) FROM dbo.audit_logs
 WHERE entity_type='V2_REUSABLE_MIGRATION' AND entity_key='261125-v1' AND action='APPLIED');
IF @reusableMigrationAt IS NULL THROW 51991,'Reusable migration provenance required.',1;
DECLARE @opening TABLE(branch_id bigint,item_id bigint,location_key varchar(40),status varchar(20),quantity decimal(18,4));
INSERT @opening
SELECT m.branch_id,m.item_id,CONCAT('W:',m.warehouse_id),'READY',SUM(m.signed_base_qty)
FROM dbo.stock_movements m JOIN dbo.item_lifecycle_profiles p ON p.branch_id=m.branch_id AND p.item_id=m.item_id
WHERE p.classification='REUSABLE' AND m.occurred_at<=@reusableMigrationAt
GROUP BY m.branch_id,m.item_id,m.warehouse_id HAVING SUM(m.signed_base_qty)>0;
-- Settlement audit contains the pre-existing staff balance, not a balance
-- inferred by subtracting movements from the current position being checked.
INSERT @opening
SELECT j.branchId,j.itemId,CONCAT('U:',j.userId),'STAFF_HOLDING',SUM(j.quantity)
FROM dbo.audit_logs a CROSS APPLY OPENJSON(a.after_json) WITH
 (branchId bigint,itemId bigint,userId bigint,quantity decimal(18,4)) j
WHERE a.entity_type='StaffSupplyBalance' AND a.action='RECLASSIFIED_TO_REUSABLE'
GROUP BY j.branchId,j.itemId,j.userId;
INSERT @opening
SELECT w.branch_id,b.item_id,CONCAT('R:',r.room_id),'LEGACY_REVIEW',SUM(r.quantity)
FROM dbo.supply_reservations r JOIN dbo.staff_supply_balances b ON b.balance_id=r.balance_id
JOIN dbo.work_items w ON w.work_id=r.work_id JOIN dbo.item_lifecycle_profiles p ON p.branch_id=w.branch_id AND p.item_id=b.item_id
WHERE p.classification='REUSABLE' AND r.status='CONSUMED' AND r.resolved_at<=@reusableMigrationAt
GROUP BY w.branch_id,b.item_id,r.room_id;
;WITH movement AS (
 SELECT destination_position_id position_id,quantity FROM dbo.reusable_movements WHERE destination_position_id IS NOT NULL
 UNION ALL SELECT source_position_id,-quantity FROM dbo.reusable_movements WHERE source_position_id IS NOT NULL
), net AS (SELECT position_id,SUM(quantity) quantity FROM movement GROUP BY position_id)
INSERT @differences
SELECT CASE WHEN p.status='LEGACY_REVIEW' THEN 'reusable_legacy_position' ELSE 'reusable_position_balance' END,
 p.position_id,COALESCE(o.quantity,0)+COALESCE(n.quantity,0),p.quantity
FROM dbo.reusable_positions p LEFT JOIN @opening o ON o.branch_id=p.branch_id AND o.item_id=p.item_id
 AND o.location_key=p.location_key AND o.status=p.status LEFT JOIN net n ON n.position_id=p.position_id;
INSERT @checks SELECT 'reusable_opening_position_exists',COUNT_BIG(*),COALESCE(SUM(CASE WHEN p.position_id IS NULL THEN CONVERT(bigint,1) ELSE 0 END),0)
FROM @opening o LEFT JOIN dbo.reusable_positions p ON p.branch_id=o.branch_id AND p.item_id=o.item_id
 AND p.location_key=o.location_key AND p.status=o.status;

INSERT @differences SELECT 'mapped_stock_movement',m.movement_id,m.signed_base_qty,SUM(e.quantity)
FROM dbo.stock_movements m JOIN dbo.stock_batch_entries e ON e.movement_id=m.movement_id AND e.kind='WAREHOUSE'
GROUP BY m.movement_id,m.signed_base_qty;

-- Each posted shipment remains owned until its actual receipts are posted.
-- Keep pending discrepancies visible; do not classify them as losses.
DECLARE @transit TABLE(line_id bigint,item_id bigint,shipped decimal(18,4),received decimal(18,4));
INSERT @transit
SELECT l.line_id,l.item_id,
 COALESCE((SELECT -SUM(m.signed_base_qty) FROM dbo.stock_movements m
  WHERE m.line_id=l.line_id AND NOT EXISTS(SELECT 1 FROM dbo.stock_movements r WHERE r.reversal_of_movement_id=m.movement_id)),0),
 COALESCE((SELECT SUM(m.signed_base_qty) FROM dbo.inventory_document_lines rl
  JOIN dbo.inventory_documents rd ON rd.document_id=rl.document_id
  JOIN dbo.stock_movements m ON m.line_id=rl.line_id
  WHERE rl.source_line_id=l.line_id AND rd.source_document_id=d.document_id
   AND rd.kind='TRANSFER_RECEIPT' AND rd.posted_at IS NOT NULL
   AND NOT EXISTS(SELECT 1 FROM dbo.stock_movements r WHERE r.reversal_of_movement_id=m.movement_id)),0)
FROM dbo.inventory_document_lines l JOIN dbo.inventory_documents d ON d.document_id=l.document_id
WHERE d.kind='TRANSFER' AND d.posted_at IS NOT NULL;
INSERT @checks SELECT 'transfer_receipts_not_over_shipped',COUNT_BIG(*),
 COALESCE(SUM(CASE WHEN received<0 OR shipped<0 OR received>shipped THEN CONVERT(bigint,1) ELSE 0 END),0) FROM @transit;

-- Independent document/stock ledger balance for reusable goods still in transit.
;WITH sent AS (
 SELECT d.branch_id,d.source_warehouse_id warehouse_id,t.item_id,SUM(t.shipped-t.received) quantity
 FROM @transit t JOIN dbo.inventory_document_lines l ON l.line_id=t.line_id
 JOIN dbo.inventory_documents d ON d.document_id=l.document_id
 JOIN dbo.item_lifecycle_profiles pr ON pr.branch_id=d.branch_id AND pr.item_id=t.item_id
 WHERE pr.classification='REUSABLE' GROUP BY d.branch_id,d.source_warehouse_id,t.item_id
), held AS (
 SELECT branch_id,warehouse_id,item_id,SUM(quantity) quantity FROM dbo.reusable_positions
 WHERE status='IN_TRANSIT' GROUP BY branch_id,warehouse_id,item_id
)
INSERT @differences SELECT 'reusable_transit_document_balance',COALESCE(s.item_id,h.item_id),COALESCE(s.quantity,0),COALESCE(h.quantity,0)
FROM sent s FULL JOIN held h ON h.branch_id=s.branch_id AND h.warehouse_id=s.warehouse_id AND h.item_id=s.item_id;

-- Migration deliberately retained old movements without invented batch history.
-- Report these separately instead of silently dropping them through an INNER JOIN.
SELECT p.classification,COUNT_BIG(*) unmapped_movement_count,SUM(m.signed_base_qty) signed_quantity,
 MIN(m.occurred_at) earliest_movement,MAX(m.occurred_at) latest_movement
FROM dbo.stock_movements m JOIN dbo.item_lifecycle_profiles p ON p.branch_id=m.branch_id AND p.item_id=m.item_id
WHERE NOT EXISTS(SELECT 1 FROM dbo.stock_batch_entries e WHERE e.movement_id=m.movement_id AND e.kind='WAREHOUSE')
GROUP BY p.classification;
SELECT item_id,COUNT_BIG(*) shipment_lines,COALESCE(SUM(shipped),0) shipped_base_quantity,
 COALESCE(SUM(received),0) received_base_quantity,COALESCE(SUM(shipped-received),0) pending_base_quantity
FROM @transit GROUP BY item_id;
SELECT * FROM @transit WHERE received<0 OR shipped<0 OR received>shipped;
SELECT m.branch_id,m.item_id,COUNT_BIG(*) missing_lifecycle_movements
FROM dbo.stock_movements m WHERE NOT EXISTS(SELECT 1 FROM dbo.item_lifecycle_profiles p
 WHERE p.branch_id=m.branch_id AND p.item_id=m.item_id) GROUP BY m.branch_id,m.item_id;

INSERT @checks SELECT 'pending_asset_unique_provenance',COUNT_BIG(*),
 COALESCE(SUM(CASE WHEN evidence.mapping_count<>1 THEN CONVERT(bigint,1) ELSE 0 END),0)
FROM dbo.assets a CROSS APPLY(SELECT COUNT_BIG(*) mapping_count FROM dbo.audit_logs log
 WHERE log.entity_type='Asset' AND log.action='MIGRATION_PENDING_INSPECTION'
  AND log.entity_key=CONVERT(nvarchar(100),a.asset_id)) evidence
WHERE a.asset_code LIKE 'MIG222-%';

INSERT @checks SELECT check_name,COUNT_BIG(*),SUM(CASE WHEN expected<>actual THEN CONVERT(bigint,1) ELSE 0 END)
FROM @differences GROUP BY check_name;
INSERT @checks SELECT 'approved_writeoff_independent',COUNT_BIG(*),COALESCE(SUM(CASE WHEN decided_by=created_by OR decided_by IS NULL THEN CONVERT(bigint,1) ELSE 0 END),0)
FROM dbo.reusable_write_offs WHERE status='APPROVED';
INSERT @checks SELECT 'trusted_foreign_keys',COUNT_BIG(*),COALESCE(SUM(CASE WHEN is_disabled=1 OR is_not_trusted=1 THEN CONVERT(bigint,1) ELSE 0 END),0) FROM sys.foreign_keys;
INSERT @checks SELECT 'trusted_check_constraints',COUNT_BIG(*),COALESCE(SUM(CASE WHEN is_disabled=1 OR is_not_trusted=1 THEN CONVERT(bigint,1) ELSE 0 END),0) FROM sys.check_constraints;
SELECT * FROM @checks ORDER BY check_name;
SELECT TOP(100) * FROM @differences WHERE expected<>actual ORDER BY check_name,entity_id;
SELECT 'unverified_consumable_batches' category,COUNT_BIG(*) records,SUM(warehouse_quantity) quantity FROM dbo.stock_batches WHERE status='LEGACY_REVIEW'
UNION ALL SELECT 'unverified_reusable_positions',COUNT_BIG(*),SUM(quantity) FROM dbo.reusable_positions WHERE status='LEGACY_REVIEW';
COMMIT;
