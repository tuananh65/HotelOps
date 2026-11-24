param([string]$Database='HotelOpsCompactWorkdayTestP0261124')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompactWorkdayTestP0[0-9A-Za-z_]+$'){throw 'Isolated P0 clone required'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try{
 $cmd=$connection.CreateCommand()
 $cmd.CommandText=@'
SELECT t.name table_name,c.name column_name FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
WHERE t.name IN ('roles','permissions','role_permissions','user_roles','memberships','user_sessions','workday_shifts','workday_assignments','work_assignments','work_intervals','work_events','audit_logs') ORDER BY t.name,c.column_id;
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID('user_roles'),OBJECT_ID('memberships'),OBJECT_ID('workday_assignments'),OBJECT_ID('work_assignments'));
'@
 $reader=$cmd.ExecuteReader();$columns=@{};$constraints=@()
 while($reader.Read()){$table=$reader.GetString(0);if(-not $columns.ContainsKey($table)){$columns[$table]=@()};$columns[$table]+=$reader.GetString(1)}
 [void]$reader.NextResult()
 while($reader.Read()){$constraints+=@{name=$reader.GetString(0);definition=$reader.GetString(1)}}
 $reader.Dispose()
 foreach($check in @('user_roles.assigned_at','user_roles.revoked_at','memberships.valid_from','memberships.valid_to','work_intervals.closed_at','workday_shifts.capacity','audit_logs.before_json')){
  $parts=$check.Split('.');if($columns[$parts[0]] -notcontains $parts[1]){throw ('Missing actual column '+$check)}
 }
 $out=Join-Path $PSScriptRoot '../../../artifacts/p0-workflow/role-schema-readonly.json'
 @{database=$Database;columns=$columns;constraints=$constraints}|ConvertTo-Json -Depth 6|Set-Content -Encoding UTF8 $out
 # PARSEONLY is compilation syntax checking; no statements in migration are executed.
 $cmd.CommandText='SET PARSEONLY ON;';[void]$cmd.ExecuteNonQuery()
 $cmd.CommandText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'p0-role-migration.sql'));[void]$cmd.ExecuteNonQuery()
 Write-Output 'PASS actual clone columns/constraints recorded; migration parsed without execution.'
}finally{$connection.Dispose()}
