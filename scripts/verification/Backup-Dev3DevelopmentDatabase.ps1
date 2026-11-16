param([Parameter(Mandatory=$true)][switch]$DevelopmentOwnershipConfirmed)
$ErrorActionPreference='Stop'
if(!$DevelopmentOwnershipConfirmed){throw 'Project Leader development-only ownership confirmation required'}
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source='HotelOpsDB';$expectedServer='meita\MSSQLSERVER01'
$expectedGuid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$run=[Guid]::NewGuid().ToString('N');$clone='HotelOps_DEV3_142A_Recovery_'+$run.Substring(0,8)
$directory=Join-Path $workspace ('artifacts/development-preflight-'+$run)
New-Item -ItemType Directory -Path $directory | Out-Null
$backup=Join-Path $directory 'HotelOpsDB-copy-only.bak'
$receiptPath=Join-Path $directory 'receipt.json'
$receipt=[ordered]@{source=$source;expectedServer=$expectedServer;sourceGuid=$expectedGuid;run=$run;
 developmentOwnershipConfirmed=$true;backup=$backup;recoveryDatabase=$clone;status='PREFLIGHT';startedUtc=[DateTime]::UtcNow.ToString('o')}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Connect Timeout=10')
function Query([string]$sql){$c=$connection.CreateCommand();$c.CommandText=$sql;$c.CommandTimeout=180;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function SaveReceipt{[IO.File]::WriteAllText($receiptPath,($receipt|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))}
function GuardSource{
 if([string](Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -cne $expectedServer -or
    [string](Query 'SELECT DB_NAME()') -cne $source -or
    [string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()') -ine $expectedGuid){throw 'Source identity mismatch'}
}
try{
 $connection.Open();$connection.ChangeDatabase($source);GuardSource
 if([int](Query 'SELECT COUNT(*) FROM sys.database_files') -ne 2){throw 'Review multi-file backup/restore mapping first'}
 $logicalData=[string](Query 'SELECT name FROM sys.database_files WHERE type=0')
 $logicalLog=[string](Query 'SELECT name FROM sys.database_files WHERE type=1')
 if($logicalData -notmatch '^[A-Za-z0-9_]+$' -or $logicalLog -notmatch '^[A-Za-z0-9_]+$'){throw 'Unexpected logical filenames'}
 $schema=[string](Query @"
 SELECT (SELECT (SELECT s.name AS [schema],t.name,c.name AS [column],ty.name AS [type],c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition AS [default]
 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
 LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.column_id FOR JSON PATH) AS columns_json,
 (SELECT OBJECT_SCHEMA_NAME(i.object_id) AS [schema],OBJECT_NAME(i.object_id) AS [table],i.name,i.is_unique,i.is_primary_key,i.filter_definition,c.name AS [column],ic.key_ordinal,ic.is_included_column
 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE OBJECTPROPERTY(i.object_id,'IsUserTable')=1 ORDER BY i.object_id,i.index_id,ic.index_column_id FOR JSON PATH) AS indexes_json,
 (SELECT name,OBJECT_NAME(parent_object_id) AS [table],definition,is_disabled,is_not_trusted FROM sys.check_constraints ORDER BY name FOR JSON PATH) AS checks_json,
 (SELECT f.name,OBJECT_NAME(f.parent_object_id) AS [table],COL_NAME(c.parent_object_id,c.parent_column_id) AS [column],OBJECT_NAME(f.referenced_object_id) AS referenced_table,
 COL_NAME(c.referenced_object_id,c.referenced_column_id) AS referenced_column,f.delete_referential_action_desc,f.update_referential_action_desc,f.is_disabled,f.is_not_trusted
 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id ORDER BY f.name,c.constraint_column_id FOR JSON PATH) AS foreign_keys_json,
 (SELECT OBJECT_SCHEMA_NAME(m.object_id) AS [schema],OBJECT_NAME(m.object_id) AS name,o.type_desc,m.definition FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id ORDER BY o.type_desc,o.name FOR JSON PATH) AS modules_json,
 (SELECT t.name,SUM(p.row_count) AS rows FROM sys.tables t JOIN sys.dm_db_partition_stats p ON p.object_id=t.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0 GROUP BY t.name ORDER BY t.name FOR JSON PATH) AS row_counts_json
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS metadata
"@)
 [void]($schema|ConvertFrom-Json)
 $schemaPath=Join-Path $directory 'baseline-schema.json';[IO.File]::WriteAllText($schemaPath,$schema,[Text.UTF8Encoding]::new($false))
 $receipt.schemaSha256=(Get-FileHash -LiteralPath $schemaPath -Algorithm SHA256).Hash
 $receipt.tables=[int](Query 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0')
 GuardSource;$connection.ChangeDatabase('master')
 if([int](Query "SELECT COUNT(*) FROM sys.databases WHERE name=N'$clone'") -ne 0){throw 'Recovery database already exists'}
 $dataDirectory=[string](Query "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))")
 $logDirectory=[string](Query "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))")
 if(!$dataDirectory -or !$logDirectory){throw 'Default SQL storage paths required'}
 $data=Join-Path $dataDirectory ($clone+'.mdf');$log=Join-Path $logDirectory ($clone+'.ldf')
 if((Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $data) -or (Test-Path -LiteralPath $log)){throw 'Never overwrite an existing backup or recovery file'}
 $escapedBackup=$backup.Replace("'","''");$escapedData=$data.Replace("'","''");$escapedLog=$log.Replace("'","''")
 SaveReceipt
 Query "BACKUP DATABASE [$source] TO DISK=N'$escapedBackup' WITH COPY_ONLY,CHECKSUM" | Out-Null
 $receipt.status='BACKUP_CREATED';SaveReceipt
 Query "RESTORE VERIFYONLY FROM DISK=N'$escapedBackup' WITH CHECKSUM" | Out-Null
 Query "RESTORE DATABASE [$clone] FROM DISK=N'$escapedBackup' WITH MOVE N'$logicalData' TO N'$escapedData',MOVE N'$logicalLog' TO N'$escapedLog',CHECKSUM" | Out-Null
 $connection.ChangeDatabase($clone)
 if([string](Query 'SELECT DB_NAME()') -cne $clone){throw 'Recovery target mismatch'}
 Query "DBCC CHECKDB ([$clone]) WITH NO_INFOMSGS,ALL_ERRORMSGS" | Out-Null
 if([int](Query 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0') -ne $receipt.tables){throw 'Restored table inventory mismatch'}
 $receipt.recoveryGuid=[string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()')
 if([Guid]$receipt.recoveryGuid -eq [Guid]::Empty){throw 'Recovery database identity unavailable'}
 $connection.ChangeDatabase($source);GuardSource
 $receipt.backupSha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
 $receipt.status='RECOVERY_VERIFIED';$receipt.completedUtc=[DateTime]::UtcNow.ToString('o');SaveReceipt
 [pscustomobject]@{status=$receipt.status;receipt=$receiptPath;sourceGuid=$expectedGuid;recoveryDatabase=$clone;schemaSha256=$receipt.schemaSha256}
}catch{$receipt.status='FAILED';$receipt.failure=$_.Exception.Message;SaveReceipt;throw}
finally{$connection.Dispose()}
# Retain backup and recovery clone. No source DDL/data changes, WITH REPLACE, DROP or cleanup.
