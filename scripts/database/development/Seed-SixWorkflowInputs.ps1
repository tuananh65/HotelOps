param([Parameter(Mandatory=$true)][string]$RecoveryReceipt,
 [ValidateSet('Recovery','Development')][string]$Target='Recovery',[switch]$RollbackTrial)
$ErrorActionPreference='Stop'
$r=Get-Content -LiteralPath $RecoveryReceipt -Raw|ConvertFrom-Json
if($r.status -cne 'RECOVERY_VERIFIED' -or !$r.developmentOwnershipConfirmed -or $r.source -cne 'HotelOpsDB') {throw 'Verified approved recovery receipt required'}
if((Get-FileHash -LiteralPath $r.backup -Algorithm SHA256).Hash -cne $r.backupSha256){throw 'Backup changed'}
$directory=Split-Path -Parent (Resolve-Path -LiteralPath $RecoveryReceipt)
$sqlPath=Join-Path $PSScriptRoot 'Seed-SixWorkflowInputs.sql'
$hash=(Get-FileHash $sqlPath -Algorithm SHA256).Hash
$trialPath=Join-Path $directory 'six-workflow-seed-recovery.json'
if($Target -eq 'Development'){
 $trial=Get-Content $trialPath -Raw|ConvertFrom-Json
 if($trial.status -ne 'APPLIED_VERIFIED' -or $trial.sqlSha256 -cne $hash -or $trial.database -cne $r.recoveryDatabase){throw 'Matching successful seed trial required'}
}
$database=if($Target -eq 'Recovery'){$r.recoveryDatabase}else{$r.source}
$guid=if($Target -eq 'Recovery'){$r.recoveryGuid}else{$r.sourceGuid}
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source']='localhost';$builder['Initial Catalog']=$database;$builder['Integrated Security']=$true;$builder['Encrypt']=$true;$builder['TrustServerCertificate']=$true
$c=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$tx=$null
$report=[ordered]@{database=$database;sqlSha256=$hash;status='STARTED';startedUtc=[DateTime]::UtcNow.ToString('o')}
try{
 $c.OpenAsync().GetAwaiter().GetResult()
 $q=$c.CreateCommand();$q.CommandText="SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))"
 try{if([string]$q.ExecuteScalarAsync().GetAwaiter().GetResult() -cne $r.expectedServer){throw 'Server mismatch'}}finally{$q.Dispose()}
 $tx=$c.BeginTransaction([System.Data.IsolationLevel]::Serializable)
 $q=$c.CreateCommand();$q.Transaction=$tx;$q.CommandTimeout=120;$q.CommandText=[IO.File]::ReadAllText($sqlPath)
 [void]$q.Parameters.AddWithValue('@ExpectedDatabase',$database)
 [void]$q.Parameters.AddWithValue('@ExpectedGuid',[Guid]$guid)
 $rows=@()
 try{
  $reader=$q.ExecuteReaderAsync().GetAwaiter().GetResult()
  try{do{while($reader.ReadAsync().GetAwaiter().GetResult()){
   $row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$rows+=$row
  }}while($reader.NextResultAsync().GetAwaiter().GetResult())}finally{$reader.Dispose()}
 }finally{$q.Dispose()}
 $q=$c.CreateCommand();$q.Transaction=$tx;$q.CommandText="IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) THROW 51994,'Integrity constraints disabled',1; DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;"
 try{
  $check=$q.ExecuteReaderAsync().GetAwaiter().GetResult()
  try{do{if($check.ReadAsync().GetAwaiter().GetResult()){throw 'DBCC reported a constraint violation'}}while($check.NextResultAsync().GetAwaiter().GetResult())}finally{$check.Dispose()}
 }finally{$q.Dispose()}
 if($RollbackTrial){$tx.Rollback();$report.status='ROLLBACK_VERIFIED'}else{$tx.Commit();$report.status='APPLIED_VERIFIED'}
 $report.records=$rows;$report.completedUtc=[DateTime]::UtcNow.ToString('o')
}catch{if($tx -and $tx.Connection){$tx.Rollback()};$report.status='FAILED';$report.failure=$_.Exception.Message;throw}
finally{
 if($tx){$tx.Dispose()};$c.Dispose()
 $suffix=if($RollbackTrial){'rollback'}else{$Target.ToLower()}
 $report|ConvertTo-Json -Depth 8|Set-Content (Join-Path $directory "six-workflow-seed-$suffix.json") -Encoding UTF8
 $report|ConvertTo-Json -Depth 8
}
