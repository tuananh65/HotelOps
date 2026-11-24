param([Parameter(Mandatory=$true)][string]$RehearsalDatabase,[switch]$CommitProduction)
$ErrorActionPreference='Stop'
if($RehearsalDatabase -notmatch '^HotelOpsCompactWorkdayTestP0Prod[0-9]+$'){throw 'Explicit production rehearsal clone required'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=Join-Path $root 'artifacts/production-261124-p0';[void][IO.Directory]::CreateDirectory($out)
$scripts=@('p0-role-migration.sql','p0-supplies.sql','p0-store-policy.sql','p0-readiness.sql','p0-workflow-guards.sql','p0-requests.sql','p0-room-request-backfill.sql','p0-supply-defaults.sql','p0-finalize.sql')
$hashes=@{};foreach($file in $scripts){$hashes[$file]=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $file) -Algorithm SHA256).Hash}
$backup=Get-Content (Join-Path $root 'artifacts/workday/backup-HotelOpsCompactDB.json') -Raw|ConvertFrom-Json
if(!$backup.verified -or $backup.database -ne 'HotelOpsCompactDB' -or ([DateTime]::UtcNow-[DateTime]$backup.at).TotalHours -gt 2){throw 'Fresh verified Production backup required'}
$database=$RehearsalDatabase
if($CommitProduction){
 $proof=Get-Content (Join-Path $out 'migration-rehearsal.json') -Raw|ConvertFrom-Json
 if(!$proof.passed -or $proof.database -ne $RehearsalDatabase){throw 'Matching rehearsal proof required'}
 foreach($file in $scripts){if($proof.hashes.$file -ne $hashes[$file]){throw 'Migration changed after rehearsal'}}
 if(Get-NetTCPConnection -LocalPort 5769 -State Listen -ErrorAction SilentlyContinue){throw 'Stop the verified old Production API before migration'}
 $database='HotelOpsCompactDB'
}elseif($backup.restoredAs -ne $RehearsalDatabase){throw 'Rehearsal must come from the verified Production backup'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
if($builder.InitialCatalog -ne 'HotelOpsCompactDB'){throw 'Unexpected primary connection target'}
$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
try{
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120
 $cmd.CommandText=@'
SET XACT_ABORT ON; BEGIN TRANSACTION;
SELECT user_id,username,password_hash,security_stamp INTO #PreservedUsers FROM users;
SELECT * INTO #PreservedStock FROM stock_movements;
SELECT work_event_id INTO #PreservedWorkEvents FROM work_events;
SELECT audit_id INTO #PreservedAudit FROM audit_logs;
'@
 [void]$cmd.ExecuteNonQuery()
 foreach($file in $scripts){
  $source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $file))
  # Replace ONLY the TEST database-name admission. All business, schema, permission,
  # transaction and data-integrity checks remain byte-for-byte unchanged.
  $pattern="(?m)^IF DB_NAME\(\) NOT LIKE [^\r\n]+ THROW [^\r\n]+;\r?$"
  if([regex]::Matches($source,$pattern).Count -ne 1){throw "Expected exactly one reviewed target guard in $file"}
  $source=[regex]::Replace($source,$pattern,"IF DB_NAME()<>@ExpectedP0Database THROW 51900,'Exact authorized P0 deployment target required.',1;")
  foreach($batch in [regex]::Split($source,'(?m)^GO\s*\r?$')){
   if([string]::IsNullOrWhiteSpace($batch)){continue}
   $cmd=$connection.CreateCommand();$cmd.CommandTimeout=180;$cmd.CommandText=$batch
   if($batch.Contains('@ExpectedP0Database')){[void]$cmd.Parameters.AddWithValue('@ExpectedP0Database',$database)}
   $reader=$cmd.ExecuteReader();do{while($reader.Read()){} }while($reader.NextResult());$reader.Close()
  }
  Write-Output "Validated $file"
 }
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=180;$cmd.CommandText=@'
IF EXISTS(SELECT user_id,username,password_hash,security_stamp FROM #PreservedUsers EXCEPT SELECT user_id,username,password_hash,security_stamp FROM users)
 OR (SELECT COUNT(*) FROM users)<>(SELECT COUNT(*) FROM #PreservedUsers) THROW 51990,'User or credential preservation failed',1;
IF EXISTS(SELECT * FROM #PreservedStock EXCEPT SELECT * FROM stock_movements)
 OR (SELECT COUNT(*) FROM stock_movements)<>(SELECT COUNT(*) FROM #PreservedStock) THROW 51990,'Migration changed inventory ledger',1;
IF EXISTS(SELECT work_event_id FROM #PreservedWorkEvents EXCEPT SELECT work_event_id FROM work_events)
 OR EXISTS(SELECT audit_id FROM #PreservedAudit EXCEPT SELECT audit_id FROM audit_logs) THROW 51990,'Migration removed history',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=1) THROW 51990,'Database guards disabled or untrusted',1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
'@
 $reader=$cmd.ExecuteReader();$violations=0;do{while($reader.Read()){$violations++}}while($reader.NextResult());$reader.Close()
 if($violations){throw 'Constraint violation; migration rolled back'}
 $cmd.CommandText=if($CommitProduction){'COMMIT;'}else{'ROLLBACK;'};[void]$cmd.ExecuteNonQuery()
 $name=if($CommitProduction){'migration-production.json'}else{'migration-rehearsal.json'}
 @{passed=$true;database=$database;rehearsalDatabase=$RehearsalDatabase;committed=[bool]$CommitProduction;hashes=$hashes;backup=$backup.backup;at=[DateTime]::UtcNow.ToString('o');preservation='users/credentials/stock ledger/history verified'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $out $name) -Encoding UTF8
 Write-Output "PASS P0 migration validation; target=$database; committed=$CommitProduction"
}catch{
 try{$undo=$connection.CreateCommand();$undo.CommandText='IF @@TRANCOUNT>0 ROLLBACK;';[void]$undo.ExecuteNonQuery()}catch{}
 throw
}finally{$connection.Dispose()}
