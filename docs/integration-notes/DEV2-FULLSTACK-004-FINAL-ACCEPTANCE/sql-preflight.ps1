param([string]$SqlServer, [string]$OutputDirectory=$PSScriptRoot)
$ErrorActionPreference='Stop'
if (!(Test-Path -LiteralPath $OutputDirectory)) { [void](New-Item -ItemType Directory -Path $OutputDirectory) }
$targetDatabase='HotelOps_DEV2_FullStack_Trial'
if ($env:COMPUTERNAME -ne 'KIENPC') { throw 'Unexpected machine; no connection attempted' }
$instances=Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
$attempts=@(); $connection=$null; $identity=$null
foreach($property in $instances.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' }) {
    $server=if($property.Name -eq 'MSSQLSERVER'){'lpc:KIENPC'}else{'lpc:KIENPC\'+$property.Name}
    if ($SqlServer -and $server -cne $SqlServer) { continue }
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$server; $builder['Initial Catalog']=$targetDatabase; $builder['Integrated Security']=$true
    $builder['Encrypt']=$false; $builder['Connect Timeout']=5; $builder['Application Name']='DEV2_004_TRIAL_PREFLIGHT'
    $probe=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    try {
        $probe.Open()
        $command=$probe.CreateCommand(); $command.CommandText=@'
SELECT CONVERT(nvarchar(256),SERVERPROPERTY('ServerName')) server_name,
CONVERT(nvarchar(256),SERVERPROPERTY('MachineName')) machine_name,
DB_NAME() database_name, DB_ID() database_id,
(SELECT state_desc FROM sys.databases WHERE database_id=DB_ID()) database_state,
CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) product_version,
CONVERT(nvarchar(256),SERVERPROPERTY('InstanceDefaultBackupPath')) backup_path,
(SELECT CONVERT(nvarchar(36),database_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID()) database_guid
'@
        $reader=$command.ExecuteReader(); [void]$reader.Read(); $identity=[ordered]@{}
        for($i=0;$i -lt $reader.FieldCount;$i++){ $identity[$reader.GetName($i)]=$reader.GetValue($i) }
        $reader.Close()
        if($identity.database_name -cne $targetDatabase -or $identity.machine_name -cne 'KIENPC' -or $identity.database_state -ne 'ONLINE'){throw 'Target identity mismatch'}
        $connection=$probe; $attempts+=@{server=$server;status='CONNECTED_VERIFIED_TRIAL_ONLY'};break
    } catch [System.Data.SqlClient.SqlException] {
        $attempts+=@{server=$server;status='CONNECTION_FAILED';errorNumber=$_.Exception.Number;message=$_.Exception.Message};$probe.Dispose()
    }
}
$utf8=[Text.UTF8Encoding]::new($false)
if($null -eq $connection){[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SQL_ENVIRONMENT_PREFLIGHT.json'),(ConvertTo-Json -Depth 12 -InputObject @{status='BLOCKED_NO_VERIFIED_TRIAL_CONNECTION';database=$targetDatabase;attempts=$attempts;mutations=0}),$utf8);throw 'No verified trial connection; no writes performed'}
try {
    $schema=@{}
    $queries=@{
        columns="SELECT t.name table_name,c.name column_name,ty.name sql_type,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition default_definition FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id ORDER BY t.name,c.column_id"
        indexes="SELECT t.name table_name,i.name index_name,i.is_unique,i.is_primary_key,i.filter_definition,c.name column_name,ic.key_ordinal FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id ORDER BY t.name,i.index_id,ic.index_column_id"
        foreignKeys="SELECT fk.name,OBJECT_NAME(fk.parent_object_id) table_name,pc.name column_name,OBJECT_NAME(fk.referenced_object_id) referenced_table,rc.name referenced_column FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id"
        checks="SELECT OBJECT_NAME(parent_object_id) table_name,name,definition,is_disabled FROM sys.check_constraints ORDER BY table_name,name"
        rowCounts="SELECT t.name table_name,SUM(p.rows) row_count FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1) GROUP BY t.name ORDER BY t.name"
    }
    foreach($key in $queries.Keys){$cmd=$connection.CreateCommand();$cmd.CommandText=$queries[$key];$reader=$cmd.ExecuteReader();$rows=@();while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$value=$reader.GetValue($i);if($value -is [DBNull]){$value=$null};$row[$reader.GetName($i)]=$value};$rows+=,$row};$reader.Close();$schema[$key]=$rows}
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'SQL_SCHEMA_SNAPSHOT.json'),(ConvertTo-Json -Depth 12 -InputObject $schema),$utf8)
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'SQL_ENVIRONMENT_PREFLIGHT.json'),(ConvertTo-Json -Depth 12 -InputObject @{status='PASS';environment='DEDICATED_OWNER_AUTHORIZED_TRIAL';identity=$identity;transport='Windows integrated authentication over local shared memory (lpc), no TCP/TLS identity claim';attempts=$attempts;schemaInspection='PASS';mutations=0}),$utf8)
    Write-Output (ConvertTo-Json -Depth 8 -InputObject @{status='PASS';identity=$identity;tables=$schema.rowCounts.Count;columns=$schema.columns.Count;mutations=0})
} finally { $connection.Dispose() }
