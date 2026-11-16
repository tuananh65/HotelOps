param([switch]$Apply)
$ErrorActionPreference='Stop'
# The Project Leader confirmed this database is development-only. This installer
# admits that exact database identity, not any database matching a name pattern.
$server='meita\MSSQLSERVER01'
$database='HotelOpsDB'
$databaseGuid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$paths=@('scripts/database/development/IncidentCategoryRecovery.sql',
    'scripts/verification/CanonicalDatabase/IncidentSla.sql',
    'scripts/verification/CanonicalDatabase/IncidentTriage.sql',
    'scripts/verification/CanonicalDatabase/IncidentClassification.sql',
    'scripts/verification/CanonicalDatabase/IncidentAffectedRooms.sql',
    'scripts/verification/CanonicalDatabase/IncidentEscalation.sql',
    'scripts/verification/CanonicalDatabase/IncidentReopenCycles.sql')
$hashes=@{
    'scripts/database/development/IncidentCategoryRecovery.sql'='E24E3497A0DB6F27AE1E0049874583F21D7E3054BB10DE7C1795CE1D4C10468B'
    'scripts/verification/CanonicalDatabase/IncidentSla.sql'='5DA3343D291DD9F3DA15818D4C2FEC1A5F494F77C1E787388BAC7C50077F2111'
    'scripts/verification/CanonicalDatabase/IncidentTriage.sql'='708E135236B5BE943D89CCFA5BDB353AF8F0374EC379BAA18B08C6447701125E'
    'scripts/verification/CanonicalDatabase/IncidentClassification.sql'='92DDF064637E9390582A14803468620F886455A0B4AF5AEB4B33DA0AE0BDD220'
    'scripts/verification/CanonicalDatabase/IncidentAffectedRooms.sql'='DC1F56355B6DB3FBBF7965CB11D5C0D5F7FF54706E67EF77901591066D65C51D'
    'scripts/verification/CanonicalDatabase/IncidentEscalation.sql'='DC06F3AF24080AFEF58CCF1AF3A38DFD94A3D2D183CD2FAB8C27D48763EF6B88'
    'scripts/verification/CanonicalDatabase/IncidentReopenCycles.sql'='C4DD26A8549E3C13E2287F236CB8330B0E31DEBB27E79158C0C881B5B9F8EB6C'
}
$packages=@()
foreach($path in $paths){
    if((Get-FileHash (Join-Path $workspace $path) -Algorithm SHA256).Hash -ne $hashes[$path]){throw "Reviewed package changed: $path"}
    $sql=[IO.File]::ReadAllText((Join-Path $workspace $path))
    if($path -eq 'scripts/database/development/IncidentCategoryRecovery.sql'){
        # This pinned predecessor uses fixed recovery identity and an Apply bit.
        # Admit only the same approved source identity checked by this installer.
        $sql=$sql.Replace("N'HotelOps_DEV3_142A_Recovery_2b221f40'","N'HotelOpsDB'")
        $sql=$sql.Replace('5270D07C-7C27-477B-A60E-86485ADD7FAD',$databaseGuid)
        $sql=$sql.Replace('DECLARE @Apply bit = 0;','DECLARE @Apply bit = 1;')
        $packages+=@{Path=$path;Sql=$sql}
        continue
    }
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
if(!$Apply){Write-Output 'Prepared Incident lifecycle installation. Use -Apply for the identity-guarded development database.';return}
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
    # Packages retain their inner transactions; this outer commit makes all reviewed
    # installations atomic. On error the connection rolls back the whole batch.
    $transaction.Commit()
    Write-Output 'APPLIED: Incident category, SLA, triage, classification, affected rooms, escalation and reopen storage on verified HotelOpsDB. No grants, SLA values or incident rows seeded.'
}catch{
    if($transaction){try{$transaction.Rollback()}catch{}}
    throw
}finally{
    if($transaction){$transaction.Dispose()};$connection.Dispose()
}
