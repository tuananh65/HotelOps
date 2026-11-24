param([switch]$RestoreBaseline)
$ErrorActionPreference='Stop'
$baseline='HotelOpsCompactWorkdayTestP0Baseline261124'
$target='HotelOpsCompactWorkdayTestP0261124'
$root=Join-Path $PSScriptRoot '../..'
$metadata=Get-Content (Join-Path $root 'artifacts/workday/backup-HotelOpsCompactDB.json') -Raw | ConvertFrom-Json
if(!$metadata.verified -or $metadata.database -ne 'HotelOpsCompactDB' -or $metadata.restoredAs -ne $target){throw 'Unexpected verified backup provenance.'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']='master'
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=180
 if($RestoreBaseline){
  $cmd.CommandText='SELECT DB_ID(@name)';[void]$cmd.Parameters.AddWithValue('@name',$baseline)
  if($cmd.ExecuteScalar() -isnot [DBNull]){throw 'Baseline already exists; restore refused. Run audit without -RestoreBaseline.'}
  $cmd.Parameters.Clear();[void]$cmd.Parameters.AddWithValue('@file',$metadata.backup)
  $cmd.CommandText='RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM';[void]$cmd.ExecuteNonQuery()
  $cmd.CommandText='RESTORE FILELISTONLY FROM DISK=@file';$r=$cmd.ExecuteReader();$files=@()
  while($r.Read()){$files+=@{name=[string]$r['LogicalName'];type=[string]$r['Type']}};$r.Close()
  $cmd.CommandText="SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))";$directory=[string]$cmd.ExecuteScalar()
  $moves=@();$index=0
  foreach($file in $files){$index++;$suffix=if($file.type -eq 'L'){'.ldf'}else{'.mdf'};$path=Join-Path $directory ($baseline+'_'+$index+$suffix);$moves+="MOVE N'"+$file.name.Replace("'","''")+"' TO N'"+$path.Replace("'","''")+"'"}
  $cmd.CommandText="IF DB_ID(N'$baseline') IS NOT NULL THROW 51995,'Baseline exists; overwrite refused.',1; RESTORE DATABASE [$baseline] FROM DISK=@file WITH "+($moves -join ',')+',CHECKSUM'
  [void]$cmd.ExecuteNonQuery()
  $cmd.Parameters.Clear();$cmd.CommandText="ALTER DATABASE [$baseline] SET READ_ONLY";[void]$cmd.ExecuteNonQuery()
 }
 $cmd.Parameters.Clear()
 $cmd.CommandText=@"
SET NOCOUNT ON;
IF DB_ID(N'$baseline') IS NULL OR DB_ID(N'$target') IS NULL THROW 51996,'Both isolated TEST databases required.',1;
IF DATABASEPROPERTYEX(N'$baseline','Updateability')<>'READ_ONLY' THROW 51997,'Baseline must be read only.',1;
CREATE TABLE #counts(tableName sysname,baselineRows bigint,targetRows bigint,missingPrimaryKeys bigint,changedOriginalRows bigint);
DECLARE @table sysname,@join nvarchar(max),@cols nvarchar(max),@sql nvarchar(max);
DECLARE tables CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM [$baseline].sys.tables WHERE is_ms_shipped=0 ORDER BY name;
OPEN tables;FETCH NEXT FROM tables INTO @table;
WHILE @@FETCH_STATUS=0
BEGIN
 SELECT @join=STRING_AGG(CONVERT(nvarchar(max),'b.'+QUOTENAME(c.name)+'=t.'+QUOTENAME(c.name)),' AND ')
 FROM [$baseline].sys.tables tb JOIN [$baseline].sys.indexes i ON i.object_id=tb.object_id AND i.is_primary_key=1
 JOIN [$baseline].sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
 JOIN [$baseline].sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE tb.name=@table;
 SELECT @cols=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',')
 FROM [$baseline].sys.tables tb JOIN [$baseline].sys.columns c ON c.object_id=tb.object_id
 WHERE tb.name=@table AND c.system_type_id NOT IN (34,35,99,189,241)
 AND EXISTS(SELECT 1 FROM [$target].sys.tables tt JOIN [$target].sys.columns tc ON tc.object_id=tt.object_id WHERE tt.name=tb.name AND tc.name=c.name);
 IF @join IS NULL THROW 51998,'Baseline table has no primary key; audit cannot prove retention.',1;
 SET @sql=N'INSERT #counts SELECT @name,(SELECT COUNT_BIG(*) FROM [$baseline].dbo.'+QUOTENAME(@table)+'),(SELECT COUNT_BIG(*) FROM [$target].dbo.'+QUOTENAME(@table)+'),(SELECT COUNT_BIG(*) FROM [$baseline].dbo.'+QUOTENAME(@table)+' b WHERE NOT EXISTS(SELECT 1 FROM [$target].dbo.'+QUOTENAME(@table)+' t WHERE '+@join+')),(SELECT COUNT_BIG(*) FROM (SELECT '+@cols+' FROM [$baseline].dbo.'+QUOTENAME(@table)+' EXCEPT SELECT '+@cols+' FROM [$target].dbo.'+QUOTENAME(@table)+') changed)';
 EXEC sys.sp_executesql @sql,N'@name sysname',@table;
 FETCH NEXT FROM tables INTO @table;
END;
CLOSE tables;DEALLOCATE tables;
SELECT (SELECT COUNT_BIG(*) FROM [$baseline].dbo.users) originalUsers,
 (SELECT COUNT_BIG(*) FROM [$target].dbo.users) currentUsers,
 (SELECT COUNT_BIG(*) FROM [$target].dbo.users t WHERE NOT EXISTS(SELECT 1 FROM [$baseline].dbo.users b WHERE b.user_id=t.user_id)) addedUsers,
 JSON_QUERY((SELECT * FROM #counts ORDER BY tableName FOR JSON PATH)) tables
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
"@
 $r=$cmd.ExecuteReader();$json='';while($r.Read()){$json+=$r.GetString(0)};$r.Close();$result=$json|ConvertFrom-Json
 # Compare password material only inside SQL. No hashes, salts, credentials or row contents leave SQL.
 $cmd.CommandText="SELECT c.name FROM [$baseline].sys.columns c JOIN [$baseline].sys.tables t ON t.object_id=c.object_id WHERE t.name='users' AND (c.name LIKE '%password%' OR c.name LIKE '%salt%')"
 $r=$cmd.ExecuteReader();$passwordColumns=@();while($r.Read()){$passwordColumns+=[string]$r[0]};$r.Close()
 if($passwordColumns.Count -eq 0){throw 'No credential columns found; refusing unsupported password preservation claim.'}
 $checks=($passwordColumns|ForEach-Object {"EXISTS(SELECT b.[$_] EXCEPT SELECT t.[$_])"}) -join ' OR '
 $cmd.CommandText="SELECT COUNT_BIG(*) FROM [$baseline].dbo.users b JOIN [$target].dbo.users t ON t.user_id=b.user_id WHERE $checks"
 $result|Add-Member changedOriginalCredentials ([long]$cmd.ExecuteScalar())
 $cmd.CommandText=@"
SELECT COUNT_BIG(*) FROM [$baseline].dbo.role_permissions b
WHERE NOT EXISTS(SELECT 1 FROM [$target].dbo.audit_logs a
 CROSS APPLY OPENJSON(a.before_json,'$.grants') WITH(role_id bigint,permission_id bigint,data_scope varchar(30)) g
 WHERE a.entity_type='P0_ROLE_MIGRATION' AND a.entity_key='261124-v1' AND a.action='APPLIED'
 AND g.role_id=b.role_id AND g.permission_id=b.permission_id AND g.data_scope=b.data_scope);
"@
 $result|Add-Member originalGrantsMissingFromMigrationArchive ([long]$cmd.ExecuteScalar())
 $result|Add-Member baselineDatabase $baseline
 $result|Add-Member targetDatabase $target
 $result|Add-Member sourceBackupVerifiedAt $metadata.at
 $result|Add-Member capturedAt ([DateTime]::UtcNow.ToString('o'))
 # Role grants are deliberately replaced during approved RBAC migration; their original values must survive in the audit snapshot.
 $missing=($result.tables|Where-Object tableName -ne 'role_permissions'|Measure-Object missingPrimaryKeys -Sum).Sum
 $result|Add-Member verdict $(if($missing -eq 0 -and $result.changedOriginalCredentials -eq 0 -and $result.originalGrantsMissingFromMigrationArchive -eq 0){'RETENTION_VERIFIED_CONTENT_CHANGES_REVIEW_REQUIRED'}else{'FAILED'})
 $out=Join-Path $root 'artifacts/p0-workflow/preservation-audit.json'
 $result|ConvertTo-Json -Depth 8|Set-Content $out -Encoding UTF8
 $result|ConvertTo-Json -Depth 8
}finally{$connection.Dispose()}
