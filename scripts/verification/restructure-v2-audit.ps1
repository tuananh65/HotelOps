param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestP0261124')][string]$Database='HotelOpsCompactDB')
$ErrorActionPreference='Stop'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult()
 $command=$connection.CreateCommand();$command.CommandTimeout=90
 $command.CommandText=@'
SET NOCOUNT ON;
SELECT DB_NAME() database_name,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) server_name,CONVERT(nvarchar(60),SERVERPROPERTY('ProductVersion')) product_version,CONVERT(nvarchar(100),SERVERPROPERTY('Edition')) edition;
SELECT name,CONVERT(nvarchar(100),value) value FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema';
SELECT t.name,SUM(p.rows) row_count FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0 GROUP BY t.name ORDER BY t.name;
SELECT r.role_id,r.code,r.is_active,COUNT(DISTINCT CASE WHEN ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) THEN ur.user_id END) current_users FROM dbo.roles r LEFT JOIN dbo.user_roles ur ON ur.role_id=r.role_id GROUP BY r.role_id,r.code,r.is_active ORDER BY r.code;
SELECT r.code role_code,p.code permission_code,rp.data_scope FROM dbo.roles r JOIN dbo.role_permissions rp ON rp.role_id=r.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id ORDER BY r.code,p.code;
SELECT t.name table_name,c.name column_name,ty.name type_name,c.max_length,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.is_ms_shipped=0 ORDER BY t.name,c.column_id;
SELECT name,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1;
SELECT name FROM sys.triggers WHERE parent_class=1 AND is_disabled=1;
SELECT kind,status,COUNT(*) quantity FROM dbo.work_items GROUP BY kind,status;
SELECT kind,status,COUNT(*) quantity FROM dbo.inventory_documents GROUP BY kind,status;
SELECT b.code branch_code,c.supply_group,COUNT(*) configurations FROM dbo.supply_configurations c JOIN dbo.branches b ON b.branch_id=c.branch_id WHERE c.is_current=1 GROUP BY b.code,c.supply_group;
SELECT branch_id,warehouse_id,item_id,SUM(signed_base_qty) balance FROM dbo.stock_movements GROUP BY branch_id,warehouse_id,item_id ORDER BY branch_id,warehouse_id,item_id;
SELECT r.code old_role,ur.user_id,ur.branch_id,ur.assigned_at,ur.revoked_at FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code IN('MAINTENANCE_STAFF','MAINTENANCE_LEAD','DEPT_MANAGER') ORDER BY r.code,ur.user_id;
'@
 $reader=$command.ExecuteReaderAsync().GetAwaiter().GetResult();$sets=@()
 do {$rows=@();while($reader.ReadAsync().GetAwaiter().GetResult()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$value=$reader.GetValue($i);$row[$reader.GetName($i)]=if($value -is [DBNull]){$null}else{$value}};$rows+=,$row};$sets+=,@($rows)}while($reader.NextResultAsync().GetAwaiter().GetResult())
 $reader.Dispose()
 $result=@{database=$Database;at=[DateTime]::UtcNow.ToString('o');readOnly=$true;sets=$sets}
 New-Item -ItemType Directory -Force artifacts/restructure-v2 | Out-Null
 $result | ConvertTo-Json -Depth 12 | Set-Content "artifacts/restructure-v2/audit-$Database.json" -Encoding utf8
 [pscustomobject]@{database=$Database;resultSets=$sets.Count;tables=$sets[2].Count;roles=$sets[3].Count;invalidForeignKeys=$sets[6].Count;invalidChecks=$sets[7].Count;disabledTriggers=$sets[8].Count} | ConvertTo-Json -Compress
} finally {$connection.Dispose()}
