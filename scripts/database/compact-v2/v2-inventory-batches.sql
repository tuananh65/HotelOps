SET XACT_ABORT ON;SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.item_lifecycle_profiles') IS NOT NULL THROW 51991,'Inventory foundation already exists; do not reseed balances.',1;
CREATE TABLE dbo.item_lifecycle_profiles(
 branch_id bigint NOT NULL,item_id bigint NOT NULL,classification varchar(20) NOT NULL,expiry_policy varchar(10) NOT NULL,
 updated_at datetime2 NOT NULL,updated_by bigint NULL,row_version rowversion NOT NULL,
 CONSTRAINT PK_item_lifecycle PRIMARY KEY(branch_id,item_id),
 CONSTRAINT FK_lifecycle_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
 CONSTRAINT FK_lifecycle_item FOREIGN KEY(item_id) REFERENCES dbo.inventory_items(item_id),
 CONSTRAINT FK_lifecycle_actor FOREIGN KEY(updated_by) REFERENCES dbo.users(user_id),
 CONSTRAINT CK_lifecycle_class CHECK(classification IN('CONSUMABLE','REUSABLE','ASSET','UNCLASSIFIED')),
 CONSTRAINT CK_lifecycle_expiry CHECK(expiry_policy IN('REQUIRED','NONE','UNKNOWN')));
CREATE TABLE dbo.stock_batches(
 batch_id bigint IDENTITY PRIMARY KEY,branch_id bigint NOT NULL,warehouse_id bigint NOT NULL,item_id bigint NOT NULL,
 lot_number nvarchar(100) NOT NULL,received_at datetime2 NULL,expires_on date NULL,received_quantity decimal(18,4) NOT NULL,
 warehouse_quantity decimal(18,4) NOT NULL,reserved_quantity decimal(18,4) NOT NULL,status varchar(20) NOT NULL,
 receipt_line_id bigint NULL,origin_batch_id bigint NULL,is_legacy bit NOT NULL,created_at datetime2 NOT NULL,row_version rowversion NOT NULL,
 CONSTRAINT FK_batch_profile FOREIGN KEY(branch_id,item_id) REFERENCES dbo.item_lifecycle_profiles(branch_id,item_id),
 CONSTRAINT FK_batch_warehouse FOREIGN KEY(warehouse_id) REFERENCES dbo.warehouses(warehouse_id),
 CONSTRAINT FK_batch_receipt FOREIGN KEY(receipt_line_id) REFERENCES dbo.inventory_document_lines(line_id),
 CONSTRAINT FK_batch_origin FOREIGN KEY(origin_batch_id) REFERENCES dbo.stock_batches(batch_id),
 CONSTRAINT CK_batch_quantity CHECK(received_quantity>=0 AND warehouse_quantity>=0 AND reserved_quantity>=0 AND reserved_quantity<=warehouse_quantity),
 CONSTRAINT CK_batch_status CHECK(status IN('LEGACY_REVIEW','READY','QUARANTINED','EXPIRED')),
 CONSTRAINT CK_batch_dates CHECK(status<>'READY' OR received_at IS NOT NULL));
CREATE INDEX IX_batch_allocate ON dbo.stock_batches(warehouse_id,item_id,status,expires_on,received_at,batch_id);
CREATE TABLE dbo.stock_batch_entries(
 entry_id bigint IDENTITY PRIMARY KEY,batch_id bigint NOT NULL,movement_id bigint NULL,ledger_id bigint NULL,
 kind varchar(20) NOT NULL,quantity decimal(18,4) NOT NULL,occurred_at datetime2 NOT NULL,
 CONSTRAINT FK_batchentry_batch FOREIGN KEY(batch_id) REFERENCES dbo.stock_batches(batch_id),
 CONSTRAINT FK_batchentry_movement FOREIGN KEY(movement_id) REFERENCES dbo.stock_movements(movement_id),
 CONSTRAINT FK_batchentry_ledger FOREIGN KEY(ledger_id) REFERENCES dbo.staff_supply_ledger(ledger_id),
 CONSTRAINT CK_batchentry_source CHECK((kind='WAREHOUSE' AND movement_id IS NOT NULL AND ledger_id IS NULL) OR (kind='STAFF_USE' AND ledger_id IS NOT NULL AND movement_id IS NULL)),
 CONSTRAINT CK_batchentry_qty CHECK(quantity<>0));
CREATE UNIQUE INDEX UX_batchentry_movement ON dbo.stock_batch_entries(movement_id,batch_id) WHERE movement_id IS NOT NULL;
CREATE UNIQUE INDEX UX_batchentry_ledger ON dbo.stock_batch_entries(ledger_id,batch_id) WHERE ledger_id IS NOT NULL;
CREATE TABLE dbo.staff_batch_balances(
 staff_batch_balance_id bigint IDENTITY PRIMARY KEY,balance_id bigint NOT NULL,batch_id bigint NOT NULL,quantity decimal(18,4) NOT NULL,row_version rowversion NOT NULL,
 CONSTRAINT FK_staffbatch_balance FOREIGN KEY(balance_id) REFERENCES dbo.staff_supply_balances(balance_id),
 CONSTRAINT FK_staffbatch_batch FOREIGN KEY(batch_id) REFERENCES dbo.stock_batches(batch_id),
 CONSTRAINT CK_staffbatch_qty CHECK(quantity>=0),CONSTRAINT UQ_staffbatch UNIQUE(balance_id,batch_id));

-- Only mapping accepted catalog identities; no classification from display-name heuristics.
IF NOT EXISTS(SELECT 1 FROM dbo.inventory_items WHERE item_id=213 AND code='DEMO-ITEM-3')
 OR NOT EXISTS(SELECT 1 FROM dbo.inventory_items WHERE item_id=222 AND code='TEST-DURABLE-KETTLE')
 THROW 51992,'Approved mapping identity does not match this baseline.',1;
INSERT dbo.item_lifecycle_profiles(branch_id,item_id,classification,expiry_policy,updated_at)
SELECT b.branch_id,i.item_id,CASE WHEN i.item_id IN(213,218) THEN 'REUSABLE' WHEN i.item_id=222 THEN 'ASSET'
 WHEN EXISTS(SELECT 1 FROM dbo.supply_configurations c WHERE c.branch_id=b.branch_id AND c.item_id=i.item_id AND c.is_current=1 AND c.supply_group='A') THEN 'CONSUMABLE'
 WHEN EXISTS(SELECT 1 FROM dbo.supply_configurations c WHERE c.branch_id=b.branch_id AND c.item_id=i.item_id AND c.is_current=1 AND c.supply_group='B') THEN 'REUSABLE' ELSE 'UNCLASSIFIED' END,
 CASE WHEN i.item_id IN(213,218,222,223,224,225) THEN 'NONE' ELSE 'UNKNOWN' END,SYSUTCDATETIME()
FROM dbo.branches b CROSS JOIN dbo.inventory_items i;
-- Opening batches preserve the aggregate ledger exactly; dates/expiry remain UNKNOWN.
INSERT dbo.stock_batches(branch_id,warehouse_id,item_id,lot_number,received_quantity,warehouse_quantity,reserved_quantity,status,is_legacy,created_at)
SELECT w.branch_id,m.warehouse_id,m.item_id,CONCAT('LEGACY-W-',m.warehouse_id,'-I-',m.item_id),SUM(m.signed_base_qty),SUM(m.signed_base_qty),0,'LEGACY_REVIEW',1,SYSUTCDATETIME()
FROM dbo.stock_movements m JOIN dbo.warehouses w ON w.warehouse_id=m.warehouse_id
JOIN dbo.item_lifecycle_profiles p ON p.branch_id=w.branch_id AND p.item_id=m.item_id AND p.classification='CONSUMABLE'
GROUP BY w.branch_id,m.warehouse_id,m.item_id HAVING SUM(m.signed_base_qty)>0;
INSERT dbo.stock_batches(branch_id,warehouse_id,item_id,lot_number,received_quantity,warehouse_quantity,reserved_quantity,status,is_legacy,created_at)
SELECT a.branch_id,b.warehouse_id,b.item_id,CONCAT('LEGACY-STAFF-',b.balance_id),b.initial_issued+b.additional_issued-b.used-b.returned,0,0,'LEGACY_REVIEW',1,SYSUTCDATETIME()
FROM dbo.staff_supply_balances b JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id
JOIN dbo.item_lifecycle_profiles p ON p.branch_id=a.branch_id AND p.item_id=b.item_id AND p.classification='CONSUMABLE'
WHERE b.initial_issued+b.additional_issued-b.used-b.returned>0;
INSERT dbo.staff_batch_balances(balance_id,batch_id,quantity)
SELECT b.balance_id,l.batch_id,l.received_quantity FROM dbo.stock_batches l JOIN dbo.staff_supply_balances b ON l.lot_number=CONCAT('LEGACY-STAFF-',b.balance_id) AND l.is_legacy=1;
-- New allocations must no longer treat approved reusable towels as consumables. Historical revisions remain.
COMMIT;
SELECT COUNT(*) tables FROM sys.tables WHERE is_ms_shipped=0;
SELECT COUNT(*) foreign_keys FROM sys.foreign_keys;
SELECT COUNT(*) checks FROM sys.check_constraints;
SELECT classification,COUNT(*) profiles FROM dbo.item_lifecycle_profiles GROUP BY classification;
