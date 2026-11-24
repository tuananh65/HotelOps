param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestP0261124','HotelOpsCompactWorkdayTestWarehouse261124','HotelOpsCompactWorkdayTestRoomSupplies261124')][string]$Database='HotelOpsCompactDB',[switch]$Verify)
$ErrorActionPreference='Stop'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult()
 $command=$connection.CreateCommand();$command.CommandText=@'
SELECT b.code branch,rt.capacity,COUNT(*) rooms FROM rooms r JOIN room_types rt ON rt.room_type_id=r.room_type_id JOIN branches b ON b.branch_id=r.branch_id WHERE r.is_active=1 AND b.code IN('DVBV','DVCT') GROUP BY b.code,rt.capacity;
SELECT b.code branch,c.supply_group,c.item_id,i.name,c.per_guest_quantity,c.warehouse_id,i.base_unit_id,COALESCE((SELECT SUM(m.signed_base_qty) FROM stock_movements m WHERE m.item_id=c.item_id AND m.warehouse_id=c.warehouse_id),0) stock FROM supply_configurations c JOIN branches b ON b.branch_id=c.branch_id JOIN inventory_items i ON i.item_id=c.item_id WHERE c.is_current=1 AND c.is_active=1 AND c.supply_group IN('A','B') AND b.code IN('DVBV','DVCT') ORDER BY b.code,c.supply_group,i.name;
SELECT b.code branch,COUNT(DISTINCT d.receiving_room_id) rooms,COUNT(DISTINCT l.item_id) items FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id JOIN branches b ON b.branch_id=d.branch_id WHERE d.kind='ISSUE' AND d.posted_at IS NOT NULL AND d.receiving_room_id IS NOT NULL AND b.code IN('DVBV','DVCT') GROUP BY b.code;
'@
 if($Verify){$command.CommandText+=@'

SELECT d.branch_id,d.receiving_room_id room_id,l.item_id,SUM(-m.signed_base_qty) quantity INTO #room_items
FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id JOIN stock_movements m ON m.line_id=l.line_id
WHERE d.kind='ISSUE' AND d.posted_at IS NOT NULL AND d.receiving_room_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM stock_movements rev WHERE rev.reversal_of_movement_id=m.movement_id)
GROUP BY d.branch_id,d.receiving_room_id,l.item_id;
UPDATE ri SET quantity=ri.quantity-returns.quantity FROM #room_items ri CROSS APPLY(
 SELECT COALESCE(SUM(m.signed_base_qty),0) quantity FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id JOIN stock_movements m ON m.line_id=l.line_id
 JOIN inventory_document_lines src ON src.line_id=l.source_line_id JOIN inventory_documents original ON original.document_id=src.document_id
 WHERE d.kind='RETURN' AND d.posted_at IS NOT NULL AND original.receiving_room_id=ri.room_id AND original.branch_id=ri.branch_id AND l.item_id=ri.item_id
 AND NOT EXISTS(SELECT 1 FROM stock_movements rev WHERE rev.reversal_of_movement_id=m.movement_id)) returns;
SELECT b.code branch,r.room_id,r.code room,rt.capacity,i.item_id,i.name,c.supply_group,COALESCE(ri.quantity,0) quantity,
 CASE c.supply_group WHEN 'A' THEN rt.capacity*c.per_guest_quantity ELSE 1 END required
INTO #expected FROM branches b JOIN rooms r ON r.branch_id=b.branch_id JOIN room_types rt ON rt.room_type_id=r.room_type_id
JOIN supply_configurations c ON c.branch_id=b.branch_id AND c.is_current=1 AND c.is_active=1 JOIN inventory_items i ON i.item_id=c.item_id
LEFT JOIN #room_items ri ON ri.branch_id=b.branch_id AND ri.room_id=r.room_id AND ri.item_id=i.item_id
WHERE b.code IN('DVBV','DVCT') AND r.is_active=1 AND rt.is_active=1 AND (c.supply_group='A' OR c.supply_group='B' AND i.code LIKE 'TEST-DURABLE-%');
IF EXISTS(SELECT 1 FROM #expected WHERE quantity<required) THROW 51996,'Room contents are below the approved target.',1;
IF EXISTS(SELECT room_id FROM #expected GROUP BY room_id HAVING COUNT(*)<>12) THROW 51996,'Room must contain eight fill and four durable item IDs.',1;
IF EXISTS(SELECT m.warehouse_id,m.item_id FROM stock_movements m JOIN warehouses w ON w.warehouse_id=m.warehouse_id JOIN branches b ON b.branch_id=w.branch_id WHERE b.code IN('DVBV','DVCT') GROUP BY m.warehouse_id,m.item_id HAVING SUM(m.signed_base_qty)<0) THROW 51996,'Negative stock.',1;
IF EXISTS(SELECT 1 FROM inventory_documents d JOIN inventory_document_lines l ON l.document_id=d.document_id LEFT JOIN inventory_document_lines sl ON sl.line_id=l.source_line_id LEFT JOIN inventory_documents src ON src.document_id=sl.document_id WHERE d.document_no LIKE 'TEST-ROOM-261124-OUT-%' AND (src.document_id IS NULL OR src.document_id<>d.source_document_id OR src.receiving_room_id<>d.receiving_room_id OR sl.item_id<>l.item_id OR sl.approved_qty<>l.actual_qty OR src.approved_by=d.posted_by)) THROW 51996,'Invalid room material request provenance.',1;
IF EXISTS(SELECT 1 FROM supply_reservations r JOIN staff_supply_balances b ON b.balance_id=r.balance_id JOIN supply_configurations c ON c.configuration_id=b.configuration_id WHERE r.status='CONSUMED' AND (r.quantity<>r.capacity*r.per_guest_quantity OR c.supply_group<>'A')) THROW 51996,'Invalid fill consumption.',1;
SELECT branch,capacity,COUNT(DISTINCT room_id) rooms,COUNT(*) room_item_pairs,SUM(CASE WHEN supply_group='A' THEN quantity ELSE 0 END) fill_units,SUM(CASE WHEN supply_group='B' THEN quantity ELSE 0 END) durable_units FROM #expected GROUP BY branch,capacity ORDER BY branch,capacity;
SELECT branch,room_id,room,capacity,item_id,name,supply_group,quantity,required FROM #expected ORDER BY branch,room_id,supply_group,item_id;
'@}
 $reader=$command.ExecuteReaderAsync().GetAwaiter().GetResult();$sets=@()
 do{$rows=@();while($reader.ReadAsync().GetAwaiter().GetResult()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$rows+=,$row};$sets+=,@($rows)}while($reader.NextResultAsync().GetAwaiter().GetResult());$reader.Dispose()
 $result=@{database=$Database;at=[DateTime]::UtcNow.ToString('o');results=$sets}
 New-Item -ItemType Directory -Force artifacts/room-supplies | Out-Null
 $result | ConvertTo-Json -Depth 8 | Set-Content "artifacts/room-supplies/audit-$Database.json" -Encoding utf8
 $result | ConvertTo-Json -Depth 8 -Compress
} finally {$connection.Dispose()}
