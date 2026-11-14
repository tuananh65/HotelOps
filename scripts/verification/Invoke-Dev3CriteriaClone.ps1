param([Parameter(Mandatory=$true)][ValidateSet('Create','Cleanup')][string]$Mode,
      [Parameter(Mandatory=$true)][string]$ReceiptPath)
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source='HotelOps_DEV3_142A_20261114_cc78c13f'
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
function Query([string]$sql){
 $c=$connection.CreateCommand();$c.CommandText=$sql;$c.CommandTimeout=120
 try{return $c.ExecuteScalar()}finally{$c.Dispose()}
}
try{
 $connection.Open()
 if([string](Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -cne 'meita'){throw 'Instance mismatch'}
 if($Mode -eq 'Create'){
  if(Test-Path -LiteralPath $ReceiptPath){throw 'Receipt already exists'}
  $connection.ChangeDatabase($source)
  if([string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()') -ine '1D5A9B53-7D19-435A-96AB-9FDFF447B6E0' -or
    [string](Query "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'") -cne 'cc78c13ffc124efcad67b3d804baa84c'){throw 'Source ownership mismatch'}
  if([int](Query 'SELECT (SELECT COUNT(*) FROM dbo.users)+(SELECT COUNT(*) FROM dbo.branches)+(SELECT COUNT(*) FROM dbo.operational_tasks)+(SELECT COUNT(*) FROM dbo.attachments)+(SELECT COUNT(*) FROM dbo.audit_logs)') -ne 0){throw 'Empty synthetic source required'}
  if([int](Query 'SELECT COUNT(*) FROM sys.database_files') -ne 2){throw 'Exactly two source files required'}
  $logicalData=[string](Query 'SELECT name FROM sys.database_files WHERE type=0')
  $logicalLog=[string](Query 'SELECT name FROM sys.database_files WHERE type=1')
  if($logicalData -notmatch '^[A-Za-z0-9_]+$' -or $logicalLog -notmatch '^[A-Za-z0-9_]+$'){throw 'Unexpected logical filenames'}
  $run=[Guid]::NewGuid().ToString('N');$database='HotelOps_DEV3_142A_CriteriaTrial_'+$run.Substring(0,8)
  $directory=Join-Path $workspace ('TestResults/criteria-clone-'+$run)
  New-Item -ItemType Directory -Path $directory | Out-Null
  $backup=Join-Path $directory 'owned-source.bak'
  $receipt=[ordered]@{database=$database;run=$run;source=$source;backup=$backup;status='PREPARING'}
  [IO.File]::WriteAllText($ReceiptPath,($receipt|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
  $connection.ChangeDatabase('master')
  $dataDirectory=[string](Query "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))")
  if(!$dataDirectory -or $dataDirectory.Contains("'")){throw 'Unsupported SQL data directory'}
  if([int](Query "SELECT COUNT(*) FROM sys.databases WHERE name='$database'") -ne 0){throw 'Clone already exists'}
  $data=Join-Path $dataDirectory ($database+'.mdf');$log=Join-Path $dataDirectory ($database+'.ldf')
  $escapedBackup=$backup.Replace("'","''")
  Query "BACKUP DATABASE [$source] TO DISK=N'$escapedBackup' WITH COPY_ONLY, CHECKSUM" | Out-Null
  Query "RESTORE VERIFYONLY FROM DISK=N'$escapedBackup' WITH CHECKSUM" | Out-Null
  Query "RESTORE DATABASE [$database] FROM DISK=N'$escapedBackup' WITH MOVE N'$logicalData' TO N'$data', MOVE N'$logicalLog' TO N'$log', CHECKSUM" | Out-Null
  $connection.ChangeDatabase($database)
  Query "EXEC sys.sp_updateextendedproperty @name=N'DEV3.Sprint142A.Run',@value=N'$run'" | Out-Null
  $connection.Close();$connection.Open();$connection.ChangeDatabase($database)
  Query 'CHECKPOINT' | Out-Null
  $receipt.databaseGuid=[string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()')
  if([Guid]$receipt.databaseGuid -eq [Guid]::Empty){throw 'Stable clone GUID not available; do not run fixtures'}
  $receipt.status='CLONE_READY';$receipt.dataFile=$data;$receipt.logFile=$log
  [IO.File]::WriteAllText($ReceiptPath,($receipt|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
  $receipt|ConvertTo-Json
 }else{
  $receipt=Get-Content -LiteralPath $ReceiptPath -Raw|ConvertFrom-Json
  if($receipt.database -notmatch '^HotelOps_DEV3_142A_CriteriaTrial_[a-f0-9]{8}$' -or $receipt.run -notmatch '^[a-f0-9]{32}$' -or $receipt.status -ne 'CLONE_READY'){throw 'Invalid clone receipt'}
  $database=$receipt.database
  $connection.ChangeDatabase($database)
  if([string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()') -ine $receipt.databaseGuid -or
     [string](Query "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'") -cne $receipt.run){throw 'Clone identity mismatch'}
  if([int](Query 'SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID() AND session_id<>@@SPID') -ne 0){throw 'Stop owned API/connections before cleanup'}
  $connection.ChangeDatabase('master');Query "DROP DATABASE [$database]" | Out-Null
  $expected=Join-Path $workspace ('TestResults/criteria-clone-'+$receipt.run+'/owned-source.bak')
  if([IO.Path]::GetFullPath($receipt.backup) -cne [IO.Path]::GetFullPath($expected)){throw 'Backup path mismatch'}
  Remove-Item -LiteralPath $expected
  $receipt.status='DROPPED_AND_BACKUP_REMOVED'
  [IO.File]::WriteAllText(($ReceiptPath+'.cleanup.json'),($receipt|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
  $receipt|ConvertTo-Json
 }
}finally{$connection.Dispose()}
