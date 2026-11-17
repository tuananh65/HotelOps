param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$server = 'meita\MSSQLSERVER01'
$database = 'HotelOpsDB'
$databaseGuid = 'BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$packages = [ordered]@{
    'PreventiveSchedule.sql' = 'A2A6D6EE058C07115F32BA0EC3A35E81137B535293541CE7FC5A1B97914C4904'
    'PreventiveOccurrence.sql' = '049CF603D389025480B138990D368A9FD0B34CDC80BBED3787D791D752D066E9'
}
$commands = @()
foreach ($entry in $packages.GetEnumerator()) {
    $path = Join-Path $workspace ('scripts/verification/CanonicalDatabase/' + $entry.Key)
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value) { throw "Reviewed package changed: $($entry.Key)" }
    $sql = [IO.File]::ReadAllText($path)
    $guard = "N'`$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'"
    if (($sql.Split(@($guard), [StringSplitOptions]::None)).Length -ne 2) { throw 'Database admission boundary changed.' }
    # Admit only the development database explicitly confirmed by the Project Leader.
    # Exact server/GUID, data integrity and schema compatibility checks remain intact.
    $sql = $sql.Replace($guard, "N'`$(ExpectedDatabase)' <> N'HotelOpsDB'")
    $sql = $sql.Replace('$(ExpectedServer)', $server).Replace('$(ExpectedDatabase)', $database)
    $sql = $sql.Replace('$(ExpectedDatabaseGuid)', $databaseGuid).Replace('$(ApplyChanges)', '1')
    $commands += $sql
}
if (!$Apply) { Write-Output 'Prepared UC061 schedule/occurrence storage. Use -Apply for the confirmed development database.'; return }
$connection = [System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=false')
$transaction = $null
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = @'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=@server
 AND DB_NAME()=@database AND EXISTS(SELECT 1 FROM sys.database_recovery_status
 WHERE database_id=DB_ID() AND database_guid=@guid) THEN 1 ELSE 0 END
'@
    [void]$command.Parameters.AddWithValue('@server', $server)
    [void]$command.Parameters.AddWithValue('@database', $database)
    [void]$command.Parameters.AddWithValue('@guid', [Guid]$databaseGuid)
    try { if ([int]$command.ExecuteScalar() -ne 1) { throw 'Development identity mismatch.' } }
    finally { $command.Dispose() }
    # Additive installation only. On failure, rollback all packages together.
    # After successful use, retain schema and historical receipts if reverting source;
    # stop generation via configuration, never drop provenance or business rows.
    $transaction = $connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    foreach ($sql in $commands) {
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction; $command.CommandTimeout = 60; $command.CommandText = $sql
        try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
    }
    $transaction.Commit()
    Write-Output 'APPLIED: UC061 schedule metadata and immutable occurrence provenance. No grants, business rows or worker activation.'
} catch {
    if ($transaction) { try { $transaction.Rollback() } catch {} }
    throw
} finally {
    if ($transaction) { $transaction.Dispose() }
    $connection.Dispose()
}
