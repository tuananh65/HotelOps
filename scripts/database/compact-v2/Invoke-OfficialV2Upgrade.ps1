[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][ValidatePattern('^HotelOpsCompactWorkdayTestOfficial[0-9]+$')][string]$RehearsalDatabase,
 [switch]$CommitRehearsal,
 [switch]$CommitProduction,
 [string]$BackupMetadataPath
)
$ErrorActionPreference='Stop'
if($CommitRehearsal -and $CommitProduction){throw 'Choose rehearsal or production, never both.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=Join-Path $root 'artifacts/official-v2-upgrade'
[void][IO.Directory]::CreateDirectory($out)
$manifestPath=Join-Path $root 'docs/restructure-v2/rollout-manifest.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8|ConvertFrom-Json
if($manifest.formatVersion -ne 1 -or $manifest.steps.Count -ne 11 -or $manifest.sourceBaseline.tables -ne 49){throw 'Unexpected reviewed migration manifest.'}
$database=if($CommitProduction){'HotelOpsCompactDB'}else{$RehearsalDatabase}
if(!$BackupMetadataPath){$BackupMetadataPath=Join-Path $root 'artifacts/workday/backup-HotelOpsCompactDB.json'}
$backup=Get-Content -LiteralPath $BackupMetadataPath -Raw -Encoding UTF8|ConvertFrom-Json
$age=([DateTime]::UtcNow-([DateTimeOffset]::Parse($backup.at)).UtcDateTime).TotalHours
if(!$backup.verified -or $age -lt 0 -or $age -gt 2 -or !$backup.backup){throw 'Fresh CHECKSUM/VERIFYONLY backup metadata (within two hours) required.'}
if($CommitProduction){
 if($backup.database -ne 'HotelOpsCompactDB'){throw 'Production backup must belong to HotelOpsCompactDB.'}
 if(Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue){throw 'Stop local Production on 5080 before applying the official migration.'}
}elseif($backup.database -ne $RehearsalDatabase -and !($backup.database -eq 'HotelOpsCompactDB' -and $backup.restoredAs -eq $RehearsalDatabase)){
 throw 'Backup must be of the exact clone or the official backup restored into that clone.'
}
$hashes=[ordered]@{};$steps=@();$seen=@{}
foreach($step in $manifest.steps){
 if($step.order -ne ($steps.Count+1) -or $seen.ContainsKey($step.id)){throw 'Invalid migration order.'}
 foreach($dependency in $step.dependsOn){if(!$seen.ContainsKey($dependency)){throw "Missing earlier dependency: $dependency"}}
 $path=[IO.Path]::GetFullPath((Join-Path $root $step.path))
 if(!$path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($path) -ne '.sql'){throw 'Migration must be a repository SQL file.'}
 $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
 if($hash -ne $step.sha256){throw "Reviewed migration hash changed: $($step.id)"}
 $source=[IO.File]::ReadAllText($path)
 # Replace only the reviewed database admission, never data, schema or quantity guards.
 $guard="(?m)^IF\s+DB_NAME\(\)\s*<>\s*'HotelOpsCompactWorkdayTestV22611250010'\s+THROW[^\r\n]+;\r?$"
 if([regex]::Matches($source,$guard).Count -ne 1){throw "Expected one exact TEST admission: $($step.id)"}
 $source=[regex]::Replace($source,$guard,"IF DB_NAME()<>@ExpectedV2Database THROW 51990,'Exact authorized V2 target required.',1;")
 $hashes[$step.id]=$hash;$steps+=@{id=$step.id;source=$source};$seen[$step.id]=$true
}
# Owner 25/11: remove only the proven room fixture plus explicitly confirmed
# MR440/ISSUE441. The historical manifest stays intact; reviewed cleanup and the
# zero-ownership kettle replacement are separate, hashed inputs to the rehearsal.
$cleanupPath=Join-Path $PSScriptRoot 'official-cleanup-room-test.sql'
$kettlePath=Join-Path $PSScriptRoot 'official-kettle-after-cleanup.sql'
foreach($entry in @(@{key='officialCleanup';path=$cleanupPath},@{key='officialKettleAfterCleanup';path=$kettlePath})){
 $hashes[$entry.key]=(Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash.ToLowerInvariant()
}
$cleanupSource=[IO.File]::ReadAllText($cleanupPath)
$kettleSource=[IO.File]::ReadAllText($kettlePath)
if(!$cleanupSource.Contains('@ExpectedV2Database') -or !$kettleSource.Contains('@ExpectedV2Database')){throw 'Official cleanup and kettle replacement require exact target guards.'}
$kettleStep=@($steps|Where-Object {$_.id -eq 'kettle-register'})
if($kettleStep.Count -ne 1){throw 'Expected one reviewed kettle step.'}
$kettleStep[0].source=$kettleSource
$schemaGate=Join-Path $root $manifest.schemaGate.path
$hashes['schemaGate']=(Get-FileHash -LiteralPath $schemaGate -Algorithm SHA256).Hash.ToLowerInvariant()
if($hashes['schemaGate'] -ne $manifest.schemaGate.sha256){throw 'SchemaGate changed since manifest review.'}
$hashes['runner']=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashes['manifest']=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$proofPath=Join-Path $out ($RehearsalDatabase+'-rehearsal.json')
$proof=$null
if($CommitProduction){
 $proof=Get-Content -LiteralPath $proofPath -Raw -Encoding UTF8|ConvertFrom-Json
 if(!$proof.passed -or !$proof.committed -or $proof.database -ne $RehearsalDatabase){throw 'Matching committed rehearsal proof required.'}
 foreach($key in $hashes.Keys){if($proof.hashes.$key -ne $hashes[$key]){throw "Rehearsed code changed: $key"}}
}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function NewCommand([string]$sql){
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=180;$cmd.CommandText=$sql
 if($sql.Contains('@ExpectedV2Database')){[void]$cmd.Parameters.AddWithValue('@ExpectedV2Database',$database)}
 return $cmd
}
function Execute([string]$sql){
 $cmd=NewCommand $sql
 try{[void]$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}
}
function Scalar([string]$sql){
 $cmd=NewCommand $sql
 try{return $cmd.ExecuteScalarAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}
}
function Drain([string]$sql){
 $cmd=NewCommand $sql;$reader=$null;$count=0
 try{
  $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult()
  do{while($reader.ReadAsync().GetAwaiter().GetResult()){$count++}}while($reader.NextResultAsync().GetAwaiter().GetResult())
 }finally{if($reader){$reader.Dispose()};$cmd.Dispose()}
 return $count
}
try{
 $connection.OpenAsync().GetAwaiter().GetResult()
 if((Scalar 'SELECT DB_NAME()') -ne $database){throw 'Unexpected connection target.'}
 $verify=NewCommand 'RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;'
 try{[void]$verify.Parameters.AddWithValue('@file',[string]$backup.backup);[void]$verify.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$verify.Dispose()}
 Execute @'
SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps.OfficialV2Upgrade',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0 THROW 51990,'Migration lock unavailable.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>49
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(nvarchar(20),value)='3.0.0')
 THROW 51990,'Fresh 49-table Compact 3.0.0 baseline required; reseed refused.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1) THROW 51990,'Baseline guards are not trusted/enabled.',1;
SELECT user_id,username,password_hash INTO #PreservedUsers FROM dbo.users;
SELECT * INTO #PreservedItems FROM dbo.inventory_items;
SELECT work_event_id INTO #PreservedWorkEvents FROM dbo.work_events;
SELECT audit_id INTO #PreservedAudit FROM dbo.audit_logs;
'@
 # Hash ordered scalar data without writing credentials or row data to artifacts.
 # Any drift between rehearsal and the official baseline blocks the commit.
 $baseline=[string](Scalar @'
SET NOCOUNT ON;
DECLARE @table nvarchar(517),@object int,@cols nvarchar(max),@order nvarchar(max),@sql nvarchar(max),@json nvarchar(max),@aggregate nvarchar(max)=N'';
DECLARE tables_cursor CURSOR LOCAL FAST_FORWARD FOR
 SELECT QUOTENAME(SCHEMA_NAME(schema_id))+N'.'+QUOTENAME(name),object_id FROM sys.tables WHERE is_ms_shipped=0 ORDER BY schema_id,name;
OPEN tables_cursor;FETCH NEXT FROM tables_cursor INTO @table,@object;
WHILE @@FETCH_STATUS=0 BEGIN
 SELECT @cols=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),N',') WITHIN GROUP(ORDER BY column_id)
 FROM sys.columns WHERE object_id=@object AND system_type_id<>189;
 SELECT @order=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),N',') WITHIN GROUP(ORDER BY ic.key_ordinal)
 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
 JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE i.object_id=@object AND i.is_primary_key=1 AND ic.key_ordinal>0;
 IF @order IS NULL THROW 51990,'Cannot fingerprint a table without a primary key.',1;
 SET @sql=N'IF NOT EXISTS(SELECT 1 FROM '+@table+N') SET @result=N''[]''; ELSE SELECT @result=(SELECT '+@cols+N' FROM '+@table+N' ORDER BY '+@order+N' FOR JSON PATH,INCLUDE_NULL_VALUES);';
 EXEC sys.sp_executesql @sql,N'@result nvarchar(max) OUTPUT',@result=@json OUTPUT;
 IF @json IS NULL BEGIN DECLARE @failure nvarchar(2048)=CONCAT('Null table fingerprint for ',@table,'; migration refused.'); THROW 51990,@failure,1; END;
 SET @aggregate+=@table+N':'+CONVERT(varchar(64),HASHBYTES('SHA2_256',@json),2)+N';';
 FETCH NEXT FROM tables_cursor INTO @table,@object;
END;
CLOSE tables_cursor;DEALLOCATE tables_cursor;
SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',@aggregate),2);
'@)
 if($baseline -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Missing or invalid baseline fingerprint; migration refused.'}
 if($CommitProduction -and $proof.baselineFingerprint -ne $baseline){throw 'Official data changed or differs from rehearsed pre-cleanup baseline. Rehearse a fresh clone.'}
 foreach($batch in [regex]::Split($cleanupSource,'(?im)^\s*GO\s*\r?$')){
  if($batch.Trim()){[void](Drain $batch)}
 }
 if([int](Scalar 'SELECT @@TRANCOUNT') -ne 1){throw 'Cleanup did not preserve the outer transaction.'}
 Execute @'
IF EXISTS(SELECT user_id,username,password_hash FROM #PreservedUsers EXCEPT SELECT user_id,username,password_hash FROM dbo.users)
 OR (SELECT COUNT(*) FROM dbo.users)<>(SELECT COUNT(*) FROM #PreservedUsers) THROW 51990,'Cleanup changed credentials.',1;
IF EXISTS(SELECT * FROM #PreservedItems EXCEPT SELECT * FROM dbo.inventory_items)
 OR (SELECT COUNT(*) FROM dbo.inventory_items)<>(SELECT COUNT(*) FROM #PreservedItems) THROW 51990,'Cleanup changed item catalog.',1;
SELECT * INTO #PreservedStock FROM dbo.stock_movements;
SELECT * INTO #PreservedDocuments FROM dbo.inventory_documents;
SELECT * INTO #PreservedLines FROM dbo.inventory_document_lines;
'@
 Write-Output 'Validated authorized room-test cleanup'
 foreach($step in $steps){
  foreach($batch in [regex]::Split($step.source,'(?im)^\s*GO\s*\r?$')){
   if($batch.Trim()){[void](Drain $batch)}
  }
  if([int](Scalar 'SELECT @@TRANCOUNT') -ne 1){throw "Outer transaction was not preserved by $($step.id)."}
  Write-Output "Validated $($step.id)"
 }
 Execute @'
IF EXISTS(SELECT user_id,username,password_hash FROM #PreservedUsers EXCEPT SELECT user_id,username,password_hash FROM dbo.users)
 OR (SELECT COUNT(*) FROM dbo.users)<>(SELECT COUNT(*) FROM #PreservedUsers) THROW 51990,'Credentials changed.',1;
IF EXISTS(SELECT * FROM #PreservedStock EXCEPT SELECT * FROM dbo.stock_movements)
 OR (SELECT COUNT(*) FROM dbo.stock_movements)<>(SELECT COUNT(*) FROM #PreservedStock) THROW 51990,'Stock ledger changed.',1;
IF EXISTS(SELECT * FROM #PreservedDocuments EXCEPT SELECT * FROM dbo.inventory_documents)
 OR (SELECT COUNT(*) FROM dbo.inventory_documents)<>(SELECT COUNT(*) FROM #PreservedDocuments)
 OR EXISTS(SELECT * FROM #PreservedLines EXCEPT SELECT * FROM dbo.inventory_document_lines)
 OR (SELECT COUNT(*) FROM dbo.inventory_document_lines)<>(SELECT COUNT(*) FROM #PreservedLines) THROW 51990,'Inventory documents changed.',1;
IF EXISTS(SELECT * FROM #PreservedItems EXCEPT SELECT * FROM dbo.inventory_items)
 OR (SELECT COUNT(*) FROM dbo.inventory_items)<>(SELECT COUNT(*) FROM #PreservedItems) THROW 51990,'Item catalog changed.',1;
IF EXISTS(SELECT work_event_id FROM #PreservedWorkEvents EXCEPT SELECT work_event_id FROM dbo.work_events)
 OR EXISTS(SELECT audit_id FROM #PreservedAudit EXCEPT SELECT audit_id FROM dbo.audit_logs) THROW 51990,'Original history removed.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>57
 OR (SELECT COUNT(*) FROM sys.foreign_keys)<>234
 OR (SELECT COUNT(*) FROM sys.check_constraints)<>116
 OR (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1)<>38
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(nvarchar(20),value)='3.0.0')
 THROW 51990,'Final V2 schema counts/property mismatch.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1) THROW 51990,'Final guards are not trusted/enabled.',1;
'@
 if((Drain 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;') -ne 0){throw 'Constraint violations; migration rolled back.'}
 $commit=[bool]($CommitProduction -or $CommitRehearsal)
 Execute $(if($commit){'COMMIT;'}else{'ROLLBACK;'})
 $result=@{passed=$true;database=$database;committed=$commit;baselineFingerprint=$baseline;hashes=$hashes;backup=$backup.backup;at=[DateTime]::UtcNow.ToString('o');cleanup='Owner-confirmed room fixture and MR440/ISSUE441; same transaction as upgrade';kettleRegister='Zero synthetic ownership; no MIG222 assets recreated';limits='Schema counts and preservation checks passed. Full EF SchemaGate, API/UI and business reconciliation must be checked before cutover.'}
 $file=if($CommitProduction){Join-Path $out 'production.json'}elseif($CommitRehearsal){$proofPath}else{Join-Path $out ($RehearsalDatabase+'-dry-run.json')}
 $result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $file -Encoding UTF8
 Write-Output "PASS V2 migration; target=$database; committed=$commit"
}catch{
 if($connection.State -eq 'Open'){try{Execute 'IF @@TRANCOUNT>0 ROLLBACK;'}catch{Write-Warning 'Rollback command failed; disposing the connection.'}}
 throw
}finally{$connection.Dispose()}
