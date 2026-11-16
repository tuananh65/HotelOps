param([switch]$Apply)
$ErrorActionPreference='Stop'
# The Project Leader confirmed this database is development-only. This installer
# admits that exact database identity, not any database matching a name pattern.
$server='meita\MSSQLSERVER01'
$database='HotelOpsDB'
$databaseGuid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$paths=@('scripts/verification/CanonicalDatabase/MaintenanceExecutionVersion.sql',
    'scripts/verification/CanonicalDatabase/MaintenanceAssignment.sql',
    'scripts/verification/CanonicalDatabase/MaintenanceDecline.sql',
    'scripts/verification/CanonicalDatabase/MaintenanceProgress.sql')
$hashes=@{
    'scripts/verification/CanonicalDatabase/MaintenanceExecutionVersion.sql'='9B21919002EBD8D7FDBB87DEACFCFDD3D4ABE83D840A2B96783058EDFDB73350'
    'scripts/verification/CanonicalDatabase/MaintenanceAssignment.sql'='B49EAD6DBCDAAB257702C31F99D64239DCFD4F5B778BE9581440D4A2E646E1AE'
    'scripts/verification/CanonicalDatabase/MaintenanceDecline.sql'='223B726F990CFB7348AB24263F203E34C50CD3656FD3924A04030BE954DF5806'
    'scripts/verification/CanonicalDatabase/MaintenanceProgress.sql'='5B5D13CFDB557D5621C128A29987D5B2C0387F3B4F1A2B4426EAB6A19A9C2A32'
}
$packages=@()
foreach($path in $paths){
    if((Get-FileHash (Join-Path $workspace $path) -Algorithm SHA256).Hash -ne $hashes[$path]){throw "Reviewed package changed: $path"}
    $sql=[IO.File]::ReadAllText((Join-Path $workspace $path))
    $old="N'`$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'"
    if(($sql.Split(@($old),[StringSplitOptions]::None)).Length -ne 2){throw 'Reviewed database guard boundary changed.'}
    # Replace only the disposable-name admission; retain exact server/GUID checks,
    # schema constraints, locking and all business/data integrity checks.
    $sql=$sql.Replace($old,"N'`$(ExpectedDatabase)' <> N'HotelOpsDB'")
    $sql=$sql.Replace('$(ExpectedServer)',$server).Replace('$(ExpectedDatabase)',$database)
    $sql=$sql.Replace('$(ExpectedDatabaseGuid)',$databaseGuid).Replace('$(ApplyChanges)','1')
    if($sql -match '(?im)^\s*(USE\s|CREATE\s+DATABASE|DROP\s|ALTER\s+DATABASE|TRUNCATE\s|DELETE\s)'){
        throw 'Unexpected destructive or cross-database statement in additive package.'
    }
    $packages+=@{Path=$path;Sql=$sql}
}
if(!$Apply){Write-Output 'Prepared Maintenance lifecycle installation. Use -Apply for the identity-guarded development database.';return}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
try{
    $connection.Open()
    $command=$connection.CreateCommand()
    $command.CommandText=@'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=@server
 AND DB_NAME()=@database AND EXISTS(SELECT 1 FROM sys.database_recovery_status
 WHERE database_id=DB_ID() AND database_guid=@guid) THEN 1 ELSE 0 END
'@
    [void]$command.Parameters.AddWithValue('@server',$server)
    [void]$command.Parameters.AddWithValue('@database',$database)
    [void]$command.Parameters.AddWithValue('@guid',[Guid]$databaseGuid)
    try{if([int]$command.ExecuteScalar() -ne 1){throw 'Development database identity mismatch.'}}finally{$command.Dispose()}
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    foreach($package in $packages){
        $command=$connection.CreateCommand();$command.Transaction=$transaction;$command.CommandTimeout=30
        $command.CommandText=$package.Sql
        try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
    }
    # Packages retain their inner transactions; this outer commit makes all four
    # installations atomic. On error the connection rolls back the whole batch.
    $transaction.Commit()
    Write-Output 'APPLIED: Maintenance concurrency, assignment, decline and progress storage on verified HotelOpsDB. No role grants or work rows seeded.'
}catch{
    if($transaction){try{$transaction.Rollback()}catch{}}
    throw
}finally{
    if($transaction){$transaction.Dispose()};$connection.Dispose()
}
