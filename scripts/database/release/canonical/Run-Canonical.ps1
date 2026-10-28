param(
 [ValidateSet('Preflight','Trial','Verify')][string]$Stage='Preflight',
 [string]$RunId,
 [string]$Server='DESKTOP-JI8U50T\SQLEXPRESS'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$evidence=Join-Path $root 'scripts/verification/CanonicalDatabase/evidence'
if($Server -cne 'DESKTOP-JI8U50T\SQLEXPRESS'){throw 'Unreviewed SQL instance'}
if($Stage -ne 'Preflight' -and $RunId -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$'){throw 'Explicit unique trial RunId required'}
function Open-Target([string]$name){
 if($name -cne 'master' -and $name -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}_(Fresh|Upgrade|AuthUpgrade)$'){throw 'Forbidden target'}
 $b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
 $b['Data Source']=$Server;$b['Initial Catalog']=$name;$b['Integrated Security']=$true;$b['TrustServerCertificate']=$true
 $b['Connect Timeout']=10;$b['Application Name']='HotelOps Canonical Independent Trial'
 $c=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
 $c.Open()
 $cmd=$c.CreateCommand();$cmd.CommandText='SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME()'
 $reader=$cmd.ExecuteReader()
 try{if(!$reader.Read() -or $reader.GetString(0) -cne $Server -or $reader.GetString(1) -cne $name){throw 'SQL identity mismatch'}}finally{$reader.Dispose()}
 return $c
}
function Execute($c,[string]$sql){
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql
 try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}
}
function Rows($c,[string]$sql){
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql
 $da=New-Object System.Data.SqlClient.SqlDataAdapter $cmd;$ds=New-Object System.Data.DataSet
 try{[void]$da.Fill($ds);foreach($table in $ds.Tables){foreach($row in $table.Rows){$obj=[ordered]@{};foreach($col in $table.Columns){$obj[$col.ColumnName]=if($row[$col] -is [DBNull]){$null}else{$row[$col]}};[pscustomobject]$obj}}}finally{$da.Dispose();$cmd.Dispose()}
}
function Run-Source($c,[string]$path,[string]$target){
 $sql=[IO.File]::ReadAllText((Join-Path $root $path))
 # Derive isolated copies in memory only. Guards remain enabled and target-specific.
 $sql=$sql.Replace('HotelOpsDB',$target).Replace('__REVIEWED_TARGET_DATABASE__',$target)
 foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $c $batch}}
}
function Save($value,[string]$name){$value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $evidence $name) -Encoding UTF8}
function Seed-Fingerprint($c){
 $data=[ordered]@{}
 foreach($table in @('branches','departments','reference_values')){$data[$table]=@(Rows $c "SELECT * FROM dbo.$table ORDER BY 1")}
 return ($data | ConvertTo-Json -Depth 12 -Compress)
}
function Snapshot($c){
 $queries=[ordered]@{
 columns="SELECT s.name schema_name,t.name table_name,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,c.collation_name,dc.definition default_definition FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.name"
 foreign_keys="SELECT OBJECT_SCHEMA_NAME(f.parent_object_id) schema_name,OBJECT_NAME(f.parent_object_id) table_name,f.name,f.delete_referential_action,f.update_referential_action,f.is_disabled,f.is_not_trusted,pc.name column_name,OBJECT_SCHEMA_NAME(f.referenced_object_id) referenced_schema,OBJECT_NAME(f.referenced_object_id) referenced_table,rc.name referenced_column,fc.constraint_column_id FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns pc ON pc.object_id=f.parent_object_id AND pc.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=fc.referenced_column_id ORDER BY table_name,f.name,fc.constraint_column_id"
 indexes="SELECT OBJECT_SCHEMA_NAME(i.object_id) schema_name,OBJECT_NAME(i.object_id) table_name,CASE WHEN i.is_primary_key=1 THEN 'PRIMARY_KEY' WHEN kc.is_system_named=1 THEN 'SYSTEM_UNIQUE' ELSE i.name END index_name,i.type_desc,i.is_unique,i.is_primary_key,i.is_unique_constraint,i.has_filter,i.filter_definition,i.is_disabled,c.name column_name,ic.key_ordinal,ic.is_descending_key,ic.is_included_column FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id LEFT JOIN sys.key_constraints kc ON kc.parent_object_id=i.object_id AND kc.unique_index_id=i.index_id WHERE t.is_ms_shipped=0 ORDER BY table_name,index_name,ic.key_ordinal,ic.index_column_id"
 checks="SELECT OBJECT_SCHEMA_NAME(parent_object_id) schema_name,OBJECT_NAME(parent_object_id) table_name,name,definition,is_disabled,is_not_trusted FROM sys.check_constraints ORDER BY table_name,name"
 }
 $result=[ordered]@{};foreach($key in $queries.Keys){$result[$key]=@(Rows $c $queries[$key])};return $result
}
$master=Open-Target 'master'
try{
 $preflight=@(Rows $master "SELECT CONVERT(nvarchar(128),@@SERVERNAME) server_name,CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) version,IS_SRVROLEMEMBER('sysadmin') is_sysadmin,HAS_PERMS_BY_NAME(NULL,'SERVER','CREATE ANY DATABASE') can_create_database")
 if($Stage -eq 'Preflight'){$preflight | ConvertTo-Json;return}
 $fresh=$RunId+'_Fresh';$upgrade=$RunId+'_Upgrade';$authUpgrade=$RunId+'_AuthUpgrade'
 if($Stage -eq 'Trial'){
  foreach($name in @($fresh,$upgrade,$authUpgrade)){
   if(@(Rows $master "SELECT name FROM sys.databases WHERE name=N'$name'").Count){throw "Refusing to overwrite existing database $name"}
  }
 }
 New-Item -ItemType Directory -Path $evidence -Force | Out-Null
 if($Stage -eq 'Trial' -and (Test-Path (Join-Path $evidence ($RunId+'.json')))){throw 'Existing trial evidence must not be overwritten'}
 $sources=@('scripts/database/release/01_HotelOps_Full_Schema.sql','scripts/database/release/02_HotelOps_Upgrade_Auth_To_RBAC.sql','scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql','scripts/database/release/04_HotelOps_Verification_ReadOnly.sql')
 $patches=@('database/patches/20261019_inventory_contract.sql','database/patches/20261020_material_request_contract.sql','database/patches/20261021_replenishment_contract.sql','database/patches/20261022_maintenance_material_usage_contract.sql')
 $allSources=$sources+$patches+@('scripts/database/release/canonical/00_Canonical_Reference_Contract.sql','scripts/database/release/canonical/01_Canonical_Seed.sql','scripts/database/release/canonical/02_Canonical_Verify.sql','scripts/database/release/canonical/Run-Canonical.ps1')
 $evidenceId=if($Stage -eq 'Trial'){$RunId}else{$RunId+'-verify-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')}
 $sourceHashes=@{};foreach($path in $allSources){ $sourceHashes[$path]=(Get-FileHash (Join-Path $root $path)).Hash }
 $report=[ordered]@{runId=$RunId;stage=$Stage;startedUtc=[DateTime]::UtcNow.ToString('o');preflight=$preflight;sourceHashes=$sourceHashes;mainDatabaseWritten=$false;targets=@();success=$false}
 $snapshots=@{}
 try{
 foreach($name in @($fresh,$upgrade,$authUpgrade)){
  if($Stage -eq 'Trial'){
   # Full installer owns CREATE DATABASE and retains the existing-database guard.
   Run-Source $master $sources[0] $name
  }
  $c=Open-Target $name
  try{
   if($Stage -eq 'Trial'){
    Run-Source $c $sources[2] $name
    if($name -eq $authUpgrade){
     # Synthetic AUTH-ready fixture: legacy aliases, no personnel or transactional data.
     Execute $c "UPDATE dbo.roles SET role_code=CASE role_code WHEN 'OPERATIONS_MANAGER' THEN 'GENERAL_MANAGER' WHEN 'HK_SUPERVISOR' THEN 'HK_MANAGER' WHEN 'MAINTENANCE_LEAD' THEN 'MAINT_MANAGER' WHEN 'MAINTENANCE_STAFF' THEN 'TECHNICIAN' WHEN 'STOREKEEPER' THEN 'WAREHOUSE_KEEPER' ELSE role_code END; DELETE FROM dbo.role_permissions;"
     Run-Source $c $sources[1] $name
    }
    # Existing-RBAC path does not rerun catalog normalization or 03 on a real clone.
    foreach($patch in $patches){Run-Source $c $patch $name}
    Run-Source $c 'scripts/database/release/canonical/00_Canonical_Reference_Contract.sql' $name
    Run-Source $c 'scripts/database/release/canonical/01_Canonical_Seed.sql' $name
    # Prove repeatability of canonical data seed (not one-time schema patches).
    $seedBefore=Seed-Fingerprint $c
    Run-Source $c 'scripts/database/release/canonical/01_Canonical_Seed.sql' $name
    if((Seed-Fingerprint $c) -cne $seedBefore){throw 'Canonical seed replay changed existing data'}
   }
   Run-Source $c $sources[3] $name
   Run-Source $c 'scripts/database/release/canonical/02_Canonical_Verify.sql' $name
   $summary=@(Rows $c "SELECT DB_NAME() database_name,(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) table_count,(SELECT COUNT(*) FROM sys.columns WHERE system_type_id=189) rowversion_count,(SELECT COUNT(*) FROM sys.foreign_keys) foreign_keys,(SELECT COUNT(*) FROM sys.check_constraints) check_constraints,(SELECT COUNT(*) FROM sys.default_constraints) defaults,(SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id WHERE t.is_ms_shipped=0 AND i.index_id>0) indexes,(SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) active_roles,(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) grants,(SELECT COUNT(*) FROM dbo.users) users,(SELECT COUNT(*) FROM dbo.user_roles) user_roles,(SELECT COUNT(*) FROM dbo.user_sessions) sessions,(SELECT COUNT(*) FROM dbo.branches WHERE is_active=1) active_branches,(SELECT COUNT(*) FROM dbo.departments WHERE is_active=1) active_departments,(SELECT COUNT(*) FROM dbo.reference_values) reference_values")
   $violations=@(Rows $c 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS;')
   if($violations.Count){throw 'CHECKCONSTRAINTS reported violations'}
   $integrity=@(Rows $c "DBCC CHECKDB (N'$name') WITH TABLERESULTS, NO_INFOMSGS;")
   if($integrity.Count){throw 'CHECKDB reported errors'}
   $snapshot=Snapshot $c;$snapshots[$name]=($snapshot | ConvertTo-Json -Depth 20 -Compress)
   Save $snapshot ($evidenceId+'_'+$name.Substring($RunId.Length+1)+'-schema.json')
   $report.targets+=@{name=$name;summary=$summary;checkdb='PASS';checkconstraints='PASS';seedReplay=$(if($Stage -eq 'Trial'){'PASS'}else{'Not Run'})}
  }finally{$c.Dispose()}
 }
 if($snapshots[$fresh] -cne $snapshots[$upgrade] -or $snapshots[$fresh] -cne $snapshots[$authUpgrade]){throw 'Fresh/upgrade schema comparison differs; inspect evidence'}
 foreach($path in $allSources){if((Get-FileHash (Join-Path $root $path)).Hash -cne $sourceHashes[$path]){throw 'Source changed during trial'}}
 $report.schemaComparison='PASS';$report.success=$true
 }catch{$report.error=$_.Exception.Message;throw}finally{$report.finishedUtc=[DateTime]::UtcNow.ToString('o');Save $report ($evidenceId+'.json')}
 $report | ConvertTo-Json -Depth 8
}finally{$master.Dispose()}
