param([Parameter(Mandatory=$true)][string]$Database)
$ErrorActionPreference='Stop'
if($Database -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}_Fresh$'){throw 'Independent trial database name required'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$server='DESKTOP-JI8U50T\SQLEXPRESS'
$c=New-Object System.Data.SqlClient.SqlConnection "Data Source=$server;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=HotelOps UC141 Trial Setup"
function Execute([string]$sql){$cmd=$c.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
try{
 $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText='SELECT CONVERT(nvarchar(128),@@SERVERNAME)'
 if($cmd.ExecuteScalar() -cne $server){throw 'Wrong SQL instance'}
 $cmd.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=N'$Database'"
 if($cmd.ExecuteScalar() -ne 0){throw 'Refusing to overwrite existing database'};$cmd.Dispose()
 $sources=@('scripts/database/release/01_HotelOps_Full_Schema.sql','scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql',
 'database/patches/20261019_inventory_contract.sql','database/patches/20261020_material_request_contract.sql',
 'database/patches/20261021_replenishment_contract.sql','database/patches/20261022_maintenance_material_usage_contract.sql',
 'scripts/database/release/canonical/00_Canonical_Reference_Contract.sql','scripts/database/release/canonical/01_Canonical_Seed.sql',
 'scripts/database/release/canonical/02_Canonical_Verify.sql')
 foreach($source in $sources){
  $sql=[IO.File]::ReadAllText((Join-Path $root $source)).Replace('HotelOpsDB',$Database)
  foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch}}
 }
 Execute "IF DB_NAME()<>N'$Database' THROW 51960,'Wrong UC141 target',1; EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserDirectory.Trial',@value=N'Task17';"
 Write-Output "PASS: isolated UC141 database created: $Database; original SQL sources unchanged; HotelOpsDB not written."
}finally{$c.Dispose()}
