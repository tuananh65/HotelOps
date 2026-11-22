param(
    [Parameter(Mandatory = $true)][string]$Server,
    [ValidatePattern('^HotelOpsCompact[A-Za-z0-9_]{0,90}$')][string]$Database = 'HotelOpsCompactDB',
    [switch]$ResumeEmpty
)
$ErrorActionPreference = 'Stop'
# Never reuse, clear, drop, restore over, or modify an existing database.
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = $Server
$builder['Initial Catalog'] = 'master'
$builder['Integrated Security'] = $true
$builder['Encrypt'] = $true
$builder['TrustServerCertificate'] = $true
$builder['Connect Timeout'] = 10
$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$transaction = $null
$created = $false
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 60
    $command.CommandText = 'SELECT DB_ID(@name);'
    [void]$command.Parameters.AddWithValue('@name', $Database)
    $existing = $command.ExecuteScalar()
    if ($existing -isnot [DBNull]) {
        if (-not $ResumeEmpty) { throw "Database '$Database' already exists. No changes made." }
        $connection.ChangeDatabase($Database)
        $command.Parameters.Clear()
        $command.CommandText = 'SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped=0;'
        if ([int]$command.ExecuteScalar() -ne 0) { throw 'ResumeEmpty requires a completely empty database. No changes made.' }
    } else {
        $command.Parameters.Clear()
        # Identifier is constrained above, and quoted; no arbitrary SQL identifier input.
        $command.CommandText = "CREATE DATABASE [$Database];"
        [void]$command.ExecuteNonQuery()
        $created = $true
        $connection.ChangeDatabase($Database)
    }
    $transaction = $connection.BeginTransaction()
    $command.Transaction = $transaction
    foreach ($file in @('01_Schema.sql','02_Integrity.sql','03_DocumentGuards.sql')) {
        $sql = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $file))
        foreach ($batch in [regex]::Split($sql,'(?im)^\s*GO\s*\r?$')) {
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }
            $command.CommandText = $batch
            [void]$command.ExecuteNonQuery()
        }
    }
    $command.CommandText = 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;'
    $tableCount = [int]$command.ExecuteScalar()
    if ($tableCount -ne 40) { throw "Expected 40 tables; got $tableCount." }
    $transaction.Commit()
    $transaction.Dispose()
    $transaction = $null
    Write-Output "CREATED: $Database; tables: $tableCount; no source data or role grants copied."
} catch {
    if ($null -ne $transaction) {
        try { $transaction.Rollback() } catch { Write-Warning 'Transaction rollback could not be confirmed.' }
    }
    if ($created) { Write-Warning "New database '$Database' retained for inspection; no existing database changed." }
    throw
} finally {
    if ($null -ne $transaction) { $transaction.Dispose() }
    $connection.Dispose()
}
