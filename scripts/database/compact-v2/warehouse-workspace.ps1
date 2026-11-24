param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestWarehouseLive261124','HotelOpsCompactWorkdayTestWarehouse261124')][string]$Database='HotelOpsCompactWorkdayTestWarehouseLive261124',[switch]$Apply)
$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120
 if($Apply){
  $backupFile=Join-Path (Get-Location) ('artifacts/workday/backup-'+$Database+'.json')
  if(!(Test-Path $backupFile)){throw 'Backup evidence required for this exact database.'}
  $backup=Get-Content $backupFile -Raw | ConvertFrom-Json
  if(!$backup.verified -or $backup.database -ne $Database -or [DateTime]::Parse($backup.at).ToUniversalTime() -lt [DateTime]::UtcNow.AddHours(-4)){throw 'Recent verified backup for exact database required.'}
  $cmd.CommandText=Get-Content (Join-Path $PSScriptRoot 'warehouse-workspace.sql') -Raw -Encoding UTF8
  [void]$cmd.ExecuteNonQuery()
 }
 $cmd.CommandText=@'
SELECT b.code branch,w.warehouse_id,w.name,w.is_active,
 (SELECT COUNT(*) FROM dbo.inventory_policies p WHERE p.warehouse_id=w.warehouse_id) policy_count,
 (SELECT COUNT(*) FROM dbo.inventory_documents d WHERE d.destination_warehouse_id=w.warehouse_id AND d.document_no='WAREHOUSE-PLUS100-20261124-'+b.code) receipt_count,
 (SELECT COUNT(*) FROM dbo.stock_movements m JOIN dbo.inventory_document_lines l ON l.line_id=m.line_id JOIN dbo.inventory_documents d ON d.document_id=l.document_id WHERE m.warehouse_id=w.warehouse_id AND d.document_no='WAREHOUSE-PLUS100-20261124-'+b.code AND m.signed_base_qty=100) added_items
FROM dbo.branches b JOIN dbo.warehouses w ON w.branch_id=b.branch_id WHERE b.code IN ('DVBV','DVCT');
SELECT b.code branch,u.user_id,r.code role_code FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id JOIN dbo.branches b ON b.branch_id=ur.branch_id WHERE b.code IN ('DVBV','DVCT') AND r.code='STOREKEEPER' AND ur.revoked_at IS NULL AND u.account_status='ACTIVE';
SELECT COUNT(*) active_items FROM dbo.inventory_items WHERE is_active=1;
SELECT b.code branch,d.document_no,COUNT(m.movement_id) movement_count,SUM(m.signed_base_qty) added_base_units FROM dbo.inventory_documents d JOIN dbo.branches b ON b.branch_id=d.branch_id JOIN dbo.inventory_document_lines l ON l.document_id=d.document_id JOIN dbo.stock_movements m ON m.line_id=l.line_id WHERE d.document_no='WAREHOUSE-PLUS100-20261124-'+b.code GROUP BY b.code,d.document_no;
'@
 $adapter=New-Object System.Data.SqlClient.SqlDataAdapter($cmd);$data=New-Object System.Data.DataSet;[void]$adapter.Fill($data)
 foreach($table in $data.Tables){$table | Format-Table -AutoSize | Out-String | Write-Output}
 $evidence=@{database=$Database;applied=[bool]$Apply;at=[DateTime]::UtcNow.ToString('o');tables=@()}
 foreach($table in $data.Tables){$rows=@();foreach($row in $table.Rows){$value=@{};foreach($column in $table.Columns){$value[$column.ColumnName]=$row[$column.ColumnName]};$rows+=$value};$evidence.tables+=,@($rows)}
 $folder=Join-Path (Get-Location) 'artifacts/warehouse-validation';New-Item -ItemType Directory $folder -Force | Out-Null
 $evidence | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $folder ($Database+'-warehouse-data.json')) -Encoding UTF8
}finally{$connection.Dispose()}
