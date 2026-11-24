param([string]$Database='HotelOpsCompactWorkdayTestP0261124')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompactWorkdayTestP0[0-9A-Za-z_]+$'){throw 'Isolated P0 clone required'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $queries=[ordered]@{
  schema="SELECT DB_NAME() database_name,(SELECT CONVERT(nvarchar(30),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema') version,(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys) foreign_keys,(SELECT COUNT(*) FROM sys.check_constraints) checks,(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) triggers"
  roles="SELECT r.code,r.is_active,COUNT(ur.user_role_id) active_assignments FROM roles r LEFT JOIN user_roles ur ON ur.role_id=r.role_id AND ur.revoked_at IS NULL AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) GROUP BY r.code,r.is_active ORDER BY r.code"
  grants="SELECT r.code role_code,p.code permission_code,rp.data_scope FROM roles r JOIN role_permissions rp ON rp.role_id=r.role_id JOIN permissions p ON p.permission_id=rp.permission_id ORDER BY r.code,p.code"
  warehouses="SELECT branch_id,warehouse_id,code,is_active FROM warehouses ORDER BY branch_id,warehouse_id"
  shifts="SELECT r.code role_code,a.status,CASE WHEN a.work_date<CONVERT(date,SYSDATETIME()) THEN 'PAST' ELSE 'CURRENT_FUTURE' END period,COUNT(*) quantity FROM workday_assignments a JOIN workday_shifts s ON s.shift_id=a.shift_id JOIN roles r ON r.role_id=s.role_id GROUP BY r.code,a.status,CASE WHEN a.work_date<CONVERT(date,SYSDATETIME()) THEN 'PAST' ELSE 'CURRENT_FUTURE' END"
  work="SELECT kind,status,COUNT(*) quantity FROM work_items GROUP BY kind,status"
  documents="SELECT kind,status,COUNT(*) quantity FROM inventory_documents GROUP BY kind,status"
  departments="SELECT branch_id,department_id,code,is_active FROM departments ORDER BY branch_id,code"
  catalog="SELECT name,type_desc FROM sys.objects WHERE type IN ('U','V','TR') AND is_ms_shipped=0 ORDER BY type,name"
 }
 $result=[ordered]@{database=$Database;at=[DateTime]::UtcNow.ToString('o')}
 foreach($entry in $queries.GetEnumerator()){
  $cmd=$connection.CreateCommand();$cmd.CommandText=$entry.Value;$cmd.CommandTimeout=60
  $reader=$cmd.ExecuteReader();$rows=@()
  while($reader.Read()){$row=[ordered]@{};for($i=0;$i-lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};$rows+=,$row}
  $reader.Dispose();$cmd.Dispose();$result[$entry.Key]=$rows
 }
 $out=Join-Path $PSScriptRoot '../../../artifacts/p0-workflow';[void][IO.Directory]::CreateDirectory($out)
 $result|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 (Join-Path $out 'preflight.json')
 $result.schema|ConvertTo-Json -Compress
 $result.roles|ConvertTo-Json -Compress
 Write-Output 'Read-only preflight saved to artifacts/p0-workflow/preflight.json'
}finally{$connection.Dispose()}
