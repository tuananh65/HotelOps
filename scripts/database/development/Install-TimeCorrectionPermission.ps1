param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$server = 'meita\MSSQLSERVER01'
$database = 'HotelOpsDB'
$databaseGuid = 'BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$path = Join-Path $PSScriptRoot 'TimeCorrectionPermissionV1.sql'
if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne 'A3C6BEF5998EBDA3BF7212624B74DCB55450AFCA29D9CC8AE44BCA5F16ED6FC1') { throw 'Reviewed correction catalog package changed.' }
$commands = @([IO.File]::ReadAllText($path))
if (!$Apply) { Write-Output 'Prepared UC111 permission catalog. Use -Apply for the confirmed development database.'; return }
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
    # A fresh copy-only backup precedes mutation; no business records are changed.
    $backupDirectory = Join-Path $workspace ('artifacts/development-preflight-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $backupDirectory)
    $backupPath = Join-Path $backupDirectory 'HotelOpsDB-copy-only.bak'
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 300
    $command.CommandText = 'BACKUP DATABASE [HotelOpsDB] TO DISK=@backup WITH COPY_ONLY, CHECKSUM'
    [void]$command.Parameters.AddWithValue('@backup', $backupPath)
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
    Write-Output ('BACKUP: ' + $backupPath)
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
    Write-Output 'APPLIED: UC111 permission catalog. No grants or business records changed.'
} catch {
    if ($transaction) { try { $transaction.Rollback() } catch {} }
    throw
} finally {
    if ($transaction) { $transaction.Dispose() }
    $connection.Dispose()
}
