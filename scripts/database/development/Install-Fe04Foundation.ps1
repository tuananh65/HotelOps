param([Parameter(Mandatory=$true)][string]$RecoveryReceipt,
 [Parameter(Mandatory=$true)][ValidateSet('Recovery','Development')][string]$Target,
 [switch]$RollbackTrial)
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$receipt=Get-Content -LiteralPath $RecoveryReceipt -Raw | ConvertFrom-Json
$sourceGuid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
if($receipt.status -cne 'RECOVERY_VERIFIED' -or !$receipt.developmentOwnershipConfirmed -or
 $receipt.expectedServer -cne 'meita\MSSQLSERVER01' -or $receipt.source -cne 'HotelOpsDB' -or
 $receipt.sourceGuid -ine $sourceGuid -or $receipt.recoveryDatabase -cnotmatch '^HotelOps_DEV3_142A_Recovery_[a-f0-9]{8}$' -or
 [Guid]$receipt.recoveryGuid -eq [Guid]::Empty){throw 'Verified development recovery receipt required'}
if((Get-FileHash -LiteralPath $receipt.backup -Algorithm SHA256).Hash -cne $receipt.backupSha256){throw 'Backup integrity mismatch'}
$directory=Split-Path -Parent (Resolve-Path -LiteralPath $RecoveryReceipt).Path
$schemaPath=Join-Path $directory 'baseline-schema.json'
if((Get-FileHash -LiteralPath $schemaPath -Algorithm SHA256).Hash -cne $receipt.schemaSha256){throw 'Baseline schema mismatch'}
$manifestPath=Join-Path $PSScriptRoot 'fe04-foundation-v1.json'
$manifestHash=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$packages=@()
$verification=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'fe04-foundation-verification-v1.json') -Raw|ConvertFrom-Json
foreach($entry in $verification){if((Get-FileHash -LiteralPath (Join-Path $workspace $entry.path) -Algorithm SHA256).Hash -cne $entry.sha256){throw 'Verification source checksum drift'}}
foreach($entry in $manifest){
 $path=Join-Path $workspace $entry.path
 if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $entry.sha256){throw "Source package changed: $($entry.id)"}
 $text=[IO.File]::ReadAllText($path);$start=$text.IndexOf($entry.start,[StringComparison]::Ordinal)
 if($start -lt 0){throw 'Reviewed package boundary missing'}
 # Replace only historical disposable admission. Keep all business/schema checks and triggers.
 $body=$text.Substring($start)
 if($body -match '(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s+DATABASE|ALTER\s+DATABASE)'){throw 'Cross-database package command denied'}
 $packages+=@{entry=$entry;body=$body}
}
$trialPath=Join-Path $directory 'fe04-foundation-recovery.json'
if($Target -eq 'Development'){
 if($RollbackTrial){throw 'Rollback trials run only against the recovery clone'}
 $trial=Get-Content -LiteralPath $trialPath -Raw|ConvertFrom-Json
 if($trial.status -cne 'APPLIED_VERIFIED' -or $trial.manifestHash -cne $manifestHash -or $trial.database -cne $receipt.recoveryDatabase){throw 'Matching successful recovery trial required'}
}
$database=if($Target -eq 'Recovery'){$receipt.recoveryDatabase}else{$receipt.source}
$expectedGuid=if($Target -eq 'Recovery'){$receipt.recoveryGuid}else{$sourceGuid}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
function Query([string]$sql){$c=$connection.CreateCommand();$c.CommandText=$sql;$c.Transaction=$transaction;$c.CommandTimeout=60;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$sql){$c=$connection.CreateCommand();$c.CommandText=$sql;$c.Transaction=$transaction;$c.CommandTimeout=60;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
$report=[ordered]@{database=$database;manifestHash=$manifestHash;backupSha256=$receipt.backupSha256;status='STARTED';rollbackTrial=[bool]$RollbackTrial;startedUtc=[DateTime]::UtcNow.ToString('o')}
try{
 $connection.Open();$connection.ChangeDatabase($database)
 if([string](Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -cne $receipt.expectedServer -or
 [string](Query 'SELECT DB_NAME()') -cne $database -or
 [string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()') -ine $expectedGuid){throw 'Target identity mismatch'}
 $beforeCounts=[string](Query "SELECT (SELECT t.name,SUM(p.row_count) AS rows FROM sys.tables t JOIN sys.dm_db_partition_stats p ON p.object_id=t.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0 GROUP BY t.name ORDER BY t.name FOR JSON PATH) AS data")|ConvertFrom-Json
 $transaction=$connection.BeginTransaction()
 Execute "SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:Development:Migrations',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51990,'Migration lock unavailable',1;"
 Execute "IF OBJECT_ID('dbo.dev3_schema_migrations','U') IS NULL CREATE TABLE dbo.dev3_schema_migrations(migration_id varchar(80) NOT NULL PRIMARY KEY,source_hash char(64) NOT NULL,backup_sha256 char(64) NOT NULL,applied_at datetime2(7) NOT NULL);"
 $existing=[string](Query "SELECT source_hash FROM dbo.dev3_schema_migrations WHERE migration_id='FE04_FOUNDATION_V1'")
 if($existing -and $existing -cne $manifestHash){throw 'Migration checksum drift'}
 if(!$existing){
  Execute "IF COL_LENGTH('dbo.users','row_version') IS NULL BEGIN IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND system_type_id=189) THROW 51990,'Other user version exists',1; ALTER TABLE dbo.users ADD row_version rowversion NOT NULL; END; IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version' AND system_type_id=189 AND is_nullable=0) THROW 51990,'User version incompatible',1;"
  foreach($package in $packages){
   foreach($batch in [regex]::Split($package.body,'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch}}
   if($package.entry.marker){Execute ("EXEC sys.sp_addextendedproperty @name=N'"+$package.entry.marker+"',@value=N'"+$package.entry.sha256+"';")}
  }
  Execute "INSERT dbo.dev3_schema_migrations VALUES('FE04_FOUNDATION_V1','$manifestHash','$($receipt.backupSha256)',SYSUTCDATETIME());"
 }
 foreach($package in $packages){
  if($package.entry.marker -and [string](Query ("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'"+$package.entry.marker+"'")) -cne $package.entry.sha256){throw 'Capability checksum mismatch'}
  foreach($batch in [regex]::Split($package.body,'(?im)^\s*GO\s*\r?$')){
   if($batch.Trim() -match '^CREATE TRIGGER dbo\.([A-Za-z0-9_]+)'){
    $name=$Matches[1];$actual=[string](Query "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.$name'))")
    if($actual.Trim().Replace("`r`n","`n") -cne $batch.Trim().Replace("`r`n","`n")){throw "Trigger definition mismatch: $name"}
   }
  }
 }
 foreach($entry in $verification){Execute ([IO.File]::ReadAllText((Join-Path $workspace $entry.path)))}
 if([int](Query "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1") -gt 0 -or
    [int](Query "SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1") -gt 0){throw 'Untrusted or disabled constraint requires review'}
 foreach($row in $beforeCounts){if($row.name -cnotmatch '^[A-Za-z0-9_]+$'){throw 'Unsupported table identifier'};if([long](Query "SELECT COUNT_BIG(*) FROM dbo.[$($row.name)]") -ne [long]$row.rows){throw "Existing row count changed: $($row.name)"}}
 if($RollbackTrial){$transaction.Rollback();$report.status='ROLLBACK_VERIFIED'}else{$transaction.Commit();$report.status='APPLIED_VERIFIED'}
 $transaction.Dispose();$transaction=$null
 $report.tables=[int](Query 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0')
 $report.completedUtc=[DateTime]::UtcNow.ToString('o')
}catch{if($transaction -and $transaction.Connection){$transaction.Rollback()};$report.status='FAILED';$report.failure=$_.Exception.Message;throw}
finally{
 if($transaction){$transaction.Dispose()};$connection.Dispose()
 $output=if($RollbackTrial){Join-Path $directory 'fe04-foundation-rollback.json'}elseif($Target -eq 'Recovery'){$trialPath}else{Join-Path $directory 'fe04-foundation-development.json'}
 [IO.File]::WriteAllText($output,($report|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false));$report|ConvertTo-Json
}
