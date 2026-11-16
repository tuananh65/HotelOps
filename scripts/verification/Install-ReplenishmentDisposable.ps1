param(
    [ValidateSet('Inspect','TrialRollback','Apply')][string]$Mode='Inspect',
    [Parameter(Mandatory=$true)][string]$EvidencePath,
    [long]$VerifiedBackupSetId=0,
    [string]$VerifiedRestoreDatabase=''
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$patch=Join-Path $root 'database/patches/20261021_replenishment_contract.sql'
$expectedHash='2CD673D0EE393FEB8FE0D7F340E5068F4858A853C2C9551B0D2394B27232B787'
if((Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash -cne $expectedHash){throw 'Reviewed source hash changed.'}
$source=[IO.File]::ReadAllText($patch)
$start=$source.IndexOf(' CREATE TABLE dbo.material_replenishments(',[StringComparison]::Ordinal)
$end=$source.IndexOf(' COMMIT;',[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){throw 'Reviewed DDL boundaries missing.'}
$ddl=$source.Substring($start,$end-$start)
if($ddl -match '(?im)^\s*(USE|DROP|DELETE|UPDATE|INSERT|ALTER|COMMIT|ROLLBACK|EXEC)\b'){throw 'Unexpected command in reviewed additive DDL.'}
$target='HotelOps_DEV3_142A_Recovery_2b221f40'
$server='meita\MSSQLSERVER01'
$guid='5270d07c-7c27-477b-a60e-86485add7fad'
$output=[IO.Path]::GetFullPath($EvidencePath)
if(Test-Path -LiteralPath $output){throw 'Evidence already exists; use a new path.'}
if(!(Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($output)))){throw 'Evidence parent must exist.'}
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source']=$server; $builder['Initial Catalog']=$target
$builder['Integrated Security']=$true; $builder['Connect Timeout']=5
$builder['Encrypt']=$true; $builder['TrustServerCertificate']=$false
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$transaction=$null
$receipt=[ordered]@{mode=$Mode;sourceSha256=$expectedHash;recordedUtc=[DateTime]::UtcNow.ToString('o');status='NOT_STARTED';mutationCommitted=$false}
function Table([string]$sql,[hashtable]$parameters=@{}) {
    $command=$connection.CreateCommand(); $command.CommandText=$sql; $command.CommandTimeout=30
    if($null -ne $transaction){$command.Transaction=$transaction}
    foreach($name in $parameters.Keys){[void]$command.Parameters.AddWithValue($name,$parameters[$name])}
    $table=[System.Data.DataTable]::new()
    try {$reader=$command.ExecuteReader(); try {$table.Load($reader)} finally {$reader.Dispose()}; return ,$table}
    finally {$command.Dispose()}
}
function Execute([string]$sql) {
    $command=$connection.CreateCommand(); $command.CommandText=$sql; $command.CommandTimeout=60
    if($null -ne $transaction){$command.Transaction=$transaction}
    try {[void]$command.ExecuteNonQuery()} finally {$command.Dispose()}
}
try {
    $connection.Open()
    $identity=Table "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) server_name,DB_NAME() database_name,CONVERT(varchar(36),database_guid) database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()"
    if($identity.Rows.Count -ne 1 -or $identity.Rows[0].server_name -cne $server -or $identity.Rows[0].database_name -cne $target -or $identity.Rows[0].database_guid.ToLowerInvariant() -cne $guid){throw 'Exact disposable identity rejected.'}
    $receipt.server=$server; $receipt.database=$target; $receipt.expectedGuid=$guid; $receipt.observedGuid=[string]$identity.Rows[0].database_guid
    $facts=Table @'
SELECT r.table_name,r.column_name,
 CONVERT(bit,CASE WHEN c.system_type_id=127 AND EXISTS(
 SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
 WHERE i.object_id=t.object_id AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
 AND ic.column_id=c.column_id AND ic.key_ordinal=1
 AND NOT EXISTS(SELECT 1 FROM sys.index_columns extra WHERE extra.object_id=i.object_id AND extra.index_id=i.index_id AND extra.key_ordinal>1))
 THEN 1 ELSE 0 END) compatible
FROM (VALUES('warehouses','warehouse_id'),('users','user_id'),('inventory_items','item_id'),
 ('units_of_measure','uom_id'),('stock_thresholds','threshold_id'),('material_request_lines','line_id'),('stock_document_lines','line_id'))r(table_name,column_name)
LEFT JOIN sys.tables t ON t.name=r.table_name AND t.schema_id=SCHEMA_ID('dbo')
LEFT JOIN sys.columns c ON c.object_id=t.object_id AND c.name=r.column_name
'@
    $receipt.dependencies=@($facts.Rows | ForEach-Object {[ordered]@{table=$_.table_name;column=$_.column_name;compatible=[bool]$_.compatible}})
    $existing=Table "SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN('material_replenishments','material_replenishment_lines','material_replenishment_receipt_lines') ORDER BY name"
    $receipt.existingTables=@($existing.Rows | ForEach-Object {$_.name})
    $compatible=@($facts.Rows | Where-Object {!$_.compatible}).Count -eq 0
    $receipt.schemaDependenciesCompatible=$compatible
    if($Mode -eq 'Inspect') {$receipt.status='READ_ONLY_INSPECTED'}
    else {
        if(!$compatible){throw 'Missing or incompatible FK dependencies; no schema mutation performed.'}
        if($existing.Rows.Count -ne 0){throw 'Existing/partial FE10 schema must be compared, never overwritten.'}
        if($VerifiedBackupSetId -le 0 -or !$VerifiedRestoreDatabase.StartsWith('HotelOps_DEV3_142A_',[StringComparison]::Ordinal) -or $VerifiedRestoreDatabase -eq $target){throw 'Separate disposable restore verification required.'}
        $backup=Table @'
SELECT b.backup_set_id FROM msdb.dbo.backupset b
WHERE b.backup_set_id=@backup AND b.database_name=DB_NAME() AND b.type='D'
 AND b.database_guid=(SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID())
 AND b.is_damaged=0 AND b.has_backup_checksums=1 AND b.backup_finish_date>=DATEADD(day,-1,GETDATE())
 AND EXISTS(SELECT 1 FROM msdb.dbo.restorehistory r WHERE r.backup_set_id=b.backup_set_id
 AND r.destination_database_name=@restore AND r.restore_type='D' AND r.restore_date>=b.backup_finish_date)
 AND EXISTS(SELECT 1 FROM sys.databases d WHERE d.name=@restore AND d.state=0)
'@ @{ '@backup'=$VerifiedBackupSetId; '@restore'=$VerifiedRestoreDatabase }
        if($backup.Rows.Count -ne 1){throw 'Recent checksum backup with completed disposable restore not proven.'}
        $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        Execute @'
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON;
SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET XACT_ABORT ON;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:Replenishment:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51902,'Schema lock unavailable.',1;
IF OBJECT_ID('dbo.material_replenishments','U') IS NOT NULL OR OBJECT_ID('dbo.material_replenishment_lines','U') IS NOT NULL OR OBJECT_ID('dbo.material_replenishment_receipt_lines','U') IS NOT NULL
 THROW 51902,'Existing FE10 schema detected under lock.',1;
'@
        Execute $ddl
        $created=Table "SELECT COUNT(*) count_value FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN('material_replenishments','material_replenishment_lines','material_replenishment_receipt_lines')"
        if([int]$created.Rows[0].count_value -ne 3){throw 'Expected three FE10 tables after DDL.'}
        if($Mode -eq 'Apply') {$transaction.Commit();$receipt.mutationCommitted=$true;$receipt.status='APPLIED'}
        else {$transaction.Rollback();$receipt.status='TRIAL_ROLLED_BACK'}
        $transaction.Dispose();$transaction=$null
        if($Mode -eq 'TrialRollback') {
            $remaining=Table "SELECT COUNT(*) count_value FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN('material_replenishments','material_replenishment_lines','material_replenishment_receipt_lines')"
            if([int]$remaining.Rows[0].count_value -ne 0){throw 'Rollback verification failed.'}
        }
    }
}
catch {
    if($null -ne $transaction){try {$transaction.Rollback()} finally {$transaction.Dispose();$transaction=$null}}
    $receipt.status='FAILED';$receipt.error=$_.Exception.Message
    throw
}
finally {
    $connection.Dispose()
    $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $output -Encoding UTF8
}
