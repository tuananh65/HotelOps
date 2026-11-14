param(
    [Parameter(Mandatory=$true)][string]$Server,
    [Parameter(Mandatory=$true)][string]$ExpectedServer,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$schemaPath = Join-Path $root 'scripts/database/release/01_HotelOps_Full_Schema.sql'
$schema = [IO.File]::ReadAllText($schemaPath)
# Only recover the reviewed schema body. Database creation/selection is owned by this runner.
$marker = 'SET ANSI_NULLS ON;'
$offset = $schema.IndexOf($marker, [StringComparison]::Ordinal)
if ($offset -lt 0) { throw 'Reviewed schema body marker missing' }
$body = $schema.Substring($offset)
if ($body -match '(?im)^\s*(USE\s|CREATE\s+DATABASE|ALTER\s+DATABASE|DROP\s+DATABASE)' -or $body.Contains('HotelOpsDB')) {
    throw 'Schema body contains database targeting commands'
}
$run = [Guid]::NewGuid().ToString('N')
$database = 'HotelOps_DEV3_142A_' + [DateTime]::UtcNow.ToString('yyyyMMdd') + '_' + $run.Substring(0,8)
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source'] = $Server
$builder['Initial Catalog'] = 'master'
$builder['Integrated Security'] = $true
$builder['Connect Timeout'] = 5
# Local provisioning transport is recorded separately; this does not assert HTTPS/TLS acceptance.
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$transaction = $null
$receipt = [ordered]@{ task='DEV3-FASTTRACK-142A'; run=$run; database=$database;
    expectedServer=$ExpectedServer; schemaSha256=(Get-FileHash $schemaPath -Algorithm SHA256).Hash;
    status='NOT_CREATED'; createdUtc=[DateTime]::UtcNow.ToString('o') }
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$receiptPath = Join-Path $EvidenceDirectory 'isolated-provision.json'
if (Test-Path $receiptPath) { throw 'Existing provision receipt must not be overwritten' }
function Query([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 60
    if ($null -ne $transaction) { $command.Transaction = $transaction }
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
try {
    $connection.Open()
    $actual = [string](Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))")
    if ($actual -cne $ExpectedServer) { throw 'Server identity mismatch' }
    if ([string](Query 'SELECT DB_NAME()') -ne 'master') { throw 'Master preflight required' }
    if ([int](Query "SELECT COUNT(*) FROM sys.databases WHERE name=N'$database'") -ne 0) { throw 'Disposable name already exists' }
    $receipt.actualServer = $actual
    $receipt.transport = [string](Query 'SELECT net_transport FROM sys.dm_exec_connections WHERE session_id=@@SPID')
    $receipt.encrypted = [string](Query 'SELECT encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID')
    Query "CREATE DATABASE [$database]" | Out-Null
    $receipt.status = 'CREATED_EMPTY'
    $receipt.databaseGuid = [string](Query "SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID(N'$database')")
    $connection.ChangeDatabase($database)
    if ([string](Query 'SELECT DB_NAME()') -cne $database -or [int](Query 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0') -ne 0) {
        throw 'New empty target verification failed'
    }
    # Full schema installation is atomic. On failure keep the owned empty database and evidence.
    $transaction = $connection.BeginTransaction()
    Query "EXEC sys.sp_addextendedproperty @name=N'DEV3.Sprint142A.Run',@value=N'$run'" | Out-Null
    foreach ($batch in [regex]::Split($body, '(?im)^\s*GO\s*\r?$')) {
        if (-not [string]::IsNullOrWhiteSpace($batch)) { Query $batch | Out-Null }
    }
    $receipt.tables = [int](Query 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0')
    if ($receipt.tables -lt 1) { throw 'No schema tables installed' }
    $transaction.Commit()
    $transaction.Dispose()
    $transaction = $null
    # A newly created, untouched SQL database can initially report Guid.Empty.
    $receipt.databaseGuid = [string](Query 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()')
    if ([Guid]$receipt.databaseGuid -eq [Guid]::Empty) { throw 'Stable database GUID not yet available; runtime remains blocked' }
    $receipt.status = 'BASE_SCHEMA_INSTALLED_NO_FIXTURES'
    $receipt.runtimeAcceptance = 'NOT_RUN'
}
catch {
    if ($null -ne $transaction) { $transaction.Rollback(); $transaction.Dispose(); $transaction=$null }
    $receipt.error = $_.Exception.Message
    throw
}
finally {
    $connection.Dispose()
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
$receipt | ConvertTo-Json -Depth 5
