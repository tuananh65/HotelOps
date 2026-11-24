param([string]$Database='HotelOpsCompactDB',[string]$Server='.\SQLEXPRESS')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact[A-Za-z0-9_]*$'){throw 'Invalid database scope'}
& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database -Server $Server
$connection=New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
$connection.Open();$transaction=$connection.BeginTransaction()
try {
 $command=$connection.CreateCommand();$command.Transaction=$transaction;$command.CommandTimeout=60
 $command.CommandText=Get-Content (Join-Path $PSScriptRoot 'add-department-description.sql') -Raw -Encoding UTF8
 [void]$command.ExecuteNonQuery();$transaction.Commit()
 Write-Output "PASS: nullable department description added on $Database; backup verified."
} catch {$transaction.Rollback();throw} finally {$transaction.Dispose();$connection.Dispose()}
