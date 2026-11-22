param(
    [Parameter(Mandatory=$true)][string]$Server,
    [ValidatePattern('^HotelOpsCompact[A-Za-z0-9_]{0,90}$')][string]$Database='HotelOpsCompactDB',
    [switch]$RebuildEmptyV1,
    [switch]$RollbackTrial
)
$ErrorActionPreference='Stop'
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$Server; $builder['Initial Catalog']='master'
$builder['Integrated Security']=$true; $builder['Encrypt']=$true
$builder['TrustServerCertificate']=$true; $builder['Connect Timeout']=10
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$transaction=$null
try {
 $connection.Open();$command=$connection.CreateCommand();$command.CommandTimeout=60
 $command.CommandText='SELECT DB_ID(@name)';[void]$command.Parameters.AddWithValue('@name',$Database)
 $exists=$command.ExecuteScalar() -isnot [DBNull];$command.Parameters.Clear()
 if(-not $exists){
  if($RollbackTrial){throw 'RollbackTrial requires an existing v1 target.'}
  $command.CommandText="CREATE DATABASE [$Database]";[void]$command.ExecuteNonQuery()
 } elseif(-not $RebuildEmptyV1){throw 'Existing database refused. Use RebuildEmptyV1 only for the empty v1 database created for this task.'}
 $connection.ChangeDatabase($Database)
 $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable);$command.Transaction=$transaction
 $command.CommandText="SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOpsCompact.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51300,'Cannot acquire schema lock.',1;"
 [void]$command.ExecuteNonQuery()
 if($exists){
  $command.CommandText="SELECT CONVERT(nvarchar(30),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema'"
  if($command.ExecuteScalar() -ne '1.0.0'){throw 'Only the known v1 schema may be replaced.'}
  $expected=Get-Content (Join-Path $PSScriptRoot '../compact/table-manifest.json') -Raw | ConvertFrom-Json
  $command.CommandText="SELECT name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY name"
  $reader=$command.ExecuteReader();$actual=@();while($reader.Read()){$actual+=$reader.GetString(0)};$reader.Dispose()
  if($actual.Count -ne 40 -or (Compare-Object ($expected.name|Sort-Object) $actual)){throw 'Unexpected source table manifest.'}
  foreach($table in $actual){
   # Actual row reads under exclusive locks, not approximate partition counts.
   $quoted=$table.Replace(']',']]')
   $command.CommandText="IF EXISTS(SELECT 1 FROM dbo.[$quoted] WITH(TABLOCKX,HOLDLOCK)) THROW 51301,'Database contains data. Rebuild refused; no rows will be deleted.',1;"
   [void]$command.ExecuteNonQuery()
  }
  $command.CommandText=@'
IF EXISTS(SELECT 1 FROM sys.procedures WHERE is_ms_shipped=0)
 OR EXISTS(SELECT 1 FROM sys.views WHERE is_ms_shipped=0 AND name NOT IN ('v_stock_balances','v_inventory_document_totals'))
 THROW 51302,'Unexpected custom objects. Rebuild refused.',1;
DECLARE @sql nvarchar(max)=N'';
SELECT @sql+=N'ALTER TABLE '+QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id))+N'.'+QUOTENAME(OBJECT_NAME(parent_object_id))+N' DROP CONSTRAINT '+QUOTENAME(name)+N';' FROM sys.foreign_keys;
EXEC sys.sp_executesql @sql;
DROP VIEW dbo.v_stock_balances,dbo.v_inventory_document_totals;
SET @sql=N'';
SELECT @sql+=N'DROP TABLE '+QUOTENAME(SCHEMA_NAME(schema_id))+N'.'+QUOTENAME(name)+N';' FROM sys.tables WHERE is_ms_shipped=0;
EXEC sys.sp_executesql @sql;
EXEC sys.sp_dropextendedproperty @name=N'HotelOpsCompactSchema';
'@
  [void]$command.ExecuteNonQuery()
 }
 foreach($file in @('01_Schema.sql','02_Integrity.sql','03_DocumentGuards.sql','04_Level2.sql')){
  foreach($batch in [regex]::Split([IO.File]::ReadAllText((Join-Path $PSScriptRoot $file)),'(?im)^\s*GO\s*\r?$')){
   if([string]::IsNullOrWhiteSpace($batch)){continue};$command.CommandText=$batch;[void]$command.ExecuteNonQuery()
  }
 }
 $command.CommandText="SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0";$count=[int]$command.ExecuteScalar()
 if($count -ne 37){throw "Expected 37 tables; found $count"}
 if($RollbackTrial){$transaction.Rollback();Write-Output 'PASS: v2 installed inside transaction, then rolled back to v1.'}
 else{$transaction.Commit();Write-Output "PASS: $Database now has schema 2.0.0 and 37 tables. No business data migrated or seeded."}
 $transaction.Dispose();$transaction=$null
} catch {
 if($null -ne $transaction){try{$transaction.Rollback()}catch{Write-Warning 'Rollback already completed or could not be confirmed.'}}
 throw
} finally {if($null -ne $transaction){$transaction.Dispose()};$connection.Dispose()}
