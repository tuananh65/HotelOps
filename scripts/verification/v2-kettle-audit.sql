SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST required.',1;
SELECT t.name table_name,c.name column_name,TYPE_NAME(c.user_type_id) type,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id WHERE t.name IN('assets','asset_events','inventory_documents','inventory_document_lines','stock_movements','locations','warehouses') ORDER BY t.name,c.column_id;
SELECT i.item_id,i.code,i.name,p.branch_id,p.classification,p.expiry_policy FROM inventory_items i LEFT JOIN item_lifecycle_profiles p ON p.item_id=i.item_id WHERE i.item_id IN(213,218,222);
SELECT m.branch_id,m.warehouse_id,SUM(m.signed_base_qty) balance FROM stock_movements m WHERE m.item_id=222 GROUP BY m.branch_id,m.warehouse_id;
SELECT d.document_id,d.kind,d.status,d.branch_id,d.receiving_room_id,l.line_id,l.source_line_id,l.item_id,l.actual_qty,m.signed_base_qty,m.reversal_of_movement_id FROM inventory_documents d JOIN inventory_document_lines l ON d.document_id=l.document_id LEFT JOIN stock_movements m ON m.line_id=l.line_id WHERE l.item_id=222 ORDER BY d.document_id;
SELECT asset_id,branch_id,asset_code,name,room_id,location_id,condition_code,is_active FROM assets WHERE name LIKE N'%ấm%' OR asset_code LIKE '%KETTLE%';
SELECT * FROM warehouses;
SELECT location_id,branch_id,code,name,kind,parent_location_id,is_active FROM locations WHERE kind<>'ROOM';


