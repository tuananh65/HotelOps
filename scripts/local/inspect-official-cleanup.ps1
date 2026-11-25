$ErrorActionPreference='Stop'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']='HotelOpsCompactDB'
$db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try{
 $db.OpenAsync().GetAwaiter().GetResult();$cmd=$db.CreateCommand();$cmd.CommandText=@'
SELECT f.name,OBJECT_NAME(f.parent_object_id) child,pc.name child_column,OBJECT_NAME(f.referenced_object_id) parent,rc.name parent_column
FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id
JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
WHERE OBJECT_NAME(f.referenced_object_id) IN('inventory_documents','inventory_document_lines','stock_movements');
SELECT name,OBJECT_NAME(parent_id) parent,OBJECT_DEFINITION(object_id) definition FROM sys.triggers WHERE parent_id IN(OBJECT_ID('inventory_documents'),OBJECT_ID('inventory_document_lines'),OBJECT_ID('stock_movements'),OBJECT_ID('document_events'));
SELECT d.document_id,d.document_no,d.source_work_id,d.source_document_id,d.kind,d.status,d.reason FROM inventory_documents d WHERE d.document_id IN(440,441);
SELECT * FROM request_workflows WHERE document_id IN(440,441);
SELECT * FROM request_workflow_events WHERE related_document_id IN(440,441);
SELECT * FROM notifications WHERE document_id IN(440,441);
SELECT * FROM request_workflow_lines WHERE document_line_id IN(SELECT line_id FROM inventory_document_lines WHERE document_id IN(440,441));
SELECT * FROM staff_supply_ledger WHERE document_line_id IN(SELECT line_id FROM inventory_document_lines WHERE document_id BETWEEN 246 AND 441);
'@
 $r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$sets=@()
 do{$rows=@();while($r.Read()){$row=[ordered]@{};for($i=0;$i -lt $r.FieldCount;$i++){$v=$r.GetValue($i);$row[$r.GetName($i)]=if($v -is [DBNull]){$null}else{$v}};$rows+=,$row};$sets+=,@($rows)}while($r.NextResult());$r.Close()
 @{database='HotelOpsCompactDB';readOnly=$true;sets=$sets}|ConvertTo-Json -Depth 15|Set-Content (Join-Path $PSScriptRoot '../../artifacts/restructure-v2/official-cleanup-references.json') -Encoding UTF8
 for($i=0;$i -lt $sets.Count;$i++){Write-Output "set $i rows=$($sets[$i].Count)"}
}finally{$db.Dispose()}
