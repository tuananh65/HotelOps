param([switch]$Apply)
$ErrorActionPreference='Stop'
# The Project Leader confirmed this database is development-only. This installer
# admits that exact database identity, not any database matching a name pattern.
$server='meita\MSSQLSERVER01'
$database='HotelOpsDB'
$databaseGuid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$paths=@('scripts/verification/CanonicalDatabase/UnscheduledWorkday.sql',
    'scripts/database/development/EmployeeMembershipHistoryV1.sql')
$packages=@()
foreach($path in $paths){
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
if(!$Apply){Write-Output 'Prepared additive employee membership/history installation. Use -Apply for the identity-guarded development database.';return}
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
    # Packages retain their inner transactions; this outer commit makes the two
    # installations atomic. On error the connection rolls back the whole batch.
    $transaction.Commit()
    Write-Output 'APPLIED: Employee membership and period history on verified HotelOpsDB. No employee memberships or role grants seeded.'
}catch{
    if($transaction){try{$transaction.Rollback()}catch{}}
    throw
}finally{
    if($transaction){$transaction.Dispose()};$connection.Dispose()
}
