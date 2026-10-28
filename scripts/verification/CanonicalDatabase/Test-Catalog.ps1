param([Parameter(Mandatory=$true)][string]$RunId)
$ErrorActionPreference='Stop'
if($RunId -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$'){throw 'Invalid trial RunId'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$source=[IO.File]::ReadAllText((Join-Path $root 'scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql'))
$start=$source.IndexOf('DECLARE @SeedRoles TABLE')
$end=$source.IndexOf('    IF (SELECT COUNT(*) FROM @SeedRoles)')
if($start -lt 0 -or $end -le $start){throw 'Catalog source layout changed; review extraction'}
# Extract only reviewed table-variable declarations/static INSERTs, never live catalog writes.
$definitions=$source.Substring($start,$end-$start)
$checks=@'
IF EXISTS(SELECT role_code,is_active,is_system_role FROM dbo.roles EXCEPT SELECT role_code,is_active,is_system_role FROM @SeedRoles)
 OR EXISTS(SELECT role_code,is_active,is_system_role FROM @SeedRoles EXCEPT SELECT role_code,is_active,is_system_role FROM dbo.roles)
 THROW 51940,'Role catalog differs from release 03',1;
IF EXISTS(SELECT permission_code,module_code,resource_code,action_code FROM dbo.permissions EXCEPT SELECT permission_code,module_code,resource_code,action_code FROM @SeedPermissions)
 OR EXISTS(SELECT permission_code,module_code,resource_code,action_code FROM @SeedPermissions EXCEPT SELECT permission_code,module_code,resource_code,action_code FROM dbo.permissions)
 THROW 51941,'Permission catalog differs from release 03',1;
IF EXISTS(SELECT r.role_code,p.permission_code,g.data_scope FROM dbo.role_permissions g JOIN dbo.roles r ON r.role_id=g.role_id JOIN dbo.permissions p ON p.permission_id=g.permission_id EXCEPT SELECT role_code,permission_code,data_scope FROM @SeedGrants)
 OR EXISTS(SELECT role_code,permission_code,data_scope FROM @SeedGrants EXCEPT SELECT r.role_code,p.permission_code,g.data_scope FROM dbo.role_permissions g JOIN dbo.roles r ON r.role_id=g.role_id JOIN dbo.permissions p ON p.permission_id=g.permission_id)
 THROW 51942,'Grant catalog differs from release 03',1;
'@
$results=[ordered]@{}
foreach($suffix in @('Fresh','Upgrade','AuthUpgrade')){
 $target=$RunId+'_'+$suffix
 $c=New-Object System.Data.SqlClient.SqlConnection "Data Source=DESKTOP-JI8U50T\SQLEXPRESS;Initial Catalog=$target;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
 try{
  $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText='SELECT CONVERT(nvarchar(128),@@SERVERNAME)+''|''+DB_NAME()'
  if($cmd.ExecuteScalar() -cne ('DESKTOP-JI8U50T\SQLEXPRESS|'+$target)){throw 'Identity mismatch'}
  $cmd.CommandText=$definitions+"`n"+$checks;[void]$cmd.ExecuteNonQuery();$cmd.Dispose()
  $results[$target]='PASS: exact role/permission/grant set matches release 03'
 }finally{$c.Dispose()}
}
$path=Join-Path $PSScriptRoot ('evidence/'+$RunId+'-catalog.json')
if(Test-Path $path){throw 'Catalog evidence already exists'}
$results | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding UTF8
$results | ConvertTo-Json
