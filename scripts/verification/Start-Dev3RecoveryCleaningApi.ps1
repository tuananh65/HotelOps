param(
    [Parameter(Mandatory=$true)][string]$RecoveryReceipt,
    [ValidateRange(1024,65535)][int]$Port = 7148,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$receipt = Get-Content -LiteralPath $RecoveryReceipt -Raw | ConvertFrom-Json
if ($receipt.status -cne 'RECOVERY_VERIFIED' -or $receipt.developmentOwnershipConfirmed -ne $true -or
    $receipt.recoveryDatabase -cnotmatch '^HotelOps_DEV3_142A_Recovery_[0-9a-f]{8}$' -or
    [Guid]$receipt.recoveryGuid -eq [Guid]::Empty -or [string]::IsNullOrWhiteSpace($receipt.expectedServer) -or
    !(Test-Path -LiteralPath $receipt.backup -PathType Leaf)) {
    throw 'Verified disposable recovery receipt and retained backup required.'
}
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source'] = [string]$receipt.expectedServer
$builder['Initial Catalog'] = [string]$receipt.recoveryDatabase
$builder['Integrated Security'] = $true
$builder['Encrypt'] = $true
# Same encrypted, locally trusted SQL transport used by the recovery installer.
$builder['TrustServerCertificate'] = $true
$builder['Connect Timeout'] = 10
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Scalar([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 15
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
try {
    $connection.Open()
    if ([string](Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -cne $receipt.expectedServer -or
        [string](Scalar 'SELECT DB_NAME()') -cne $receipt.recoveryDatabase -or
        [Guid](Scalar 'SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()') -ne [Guid]$receipt.recoveryGuid) {
        throw 'Recovery instance/database/GUID mismatch.'
    }
    $markers = Scalar @'
SELECT COUNT(*) FROM sys.extended_properties WHERE class=0 AND (
 (name='HotelOps.CleaningCancellation.V1' AND CONVERT(nvarchar(128),value)='7D61CD67E15BA9710CB30612470E5AA674EB9EECEDF4568B1346168DF1B2F8EF') OR
 (name='HotelOps.CleaningCancellationReadiness.V1' AND CONVERT(nvarchar(128),value)='7E54344D7D50C1322BD8265860C1299F675F3055FE33D5720A4834F299E5E6D9'))
'@
    if ([int]$markers -ne 2) { throw 'Cancellation and readiness successor installation required.' }
    if ([int](Scalar @'
SELECT COUNT(*) FROM dbo.notification_delivery_intents i
JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
WHERE p.event_type='CLEANING_TASK_CANCELLED' AND i.status='PENDING'
'@) -ne 0) { throw 'Existing pending cancellation intents require review before starting this synthetic QA host.' }
} finally { $connection.Dispose() }
# The application still checks the full schema capability and recipient authorization per operation.
& dotnet dev-certs https --check
if ($LASTEXITCODE -ne 0) { throw 'Valid localhost HTTPS development certificate required.' }
Write-Output 'Recovery identity, cancellation markers, empty pending queue and HTTPS certificate checked.'
if ($ValidateOnly) { return }
$settings = @{
    'ConnectionStrings__HotelOpsDb' = $builder.ConnectionString
    'ASPNETCORE_ENVIRONMENT' = 'Development'
    'DOTNET_ENVIRONMENT' = 'Development'
    'ASPNETCORE_URLS' = "https://localhost:$Port"
    'CleaningCancellation__Capability' = 'VERIFIED_V1'
    'CleaningClosureNotifications__CancellationEnabled' = 'true'
    'CleaningClosureNotifications__Enabled' = 'false'
    'InspectionNotifications__Enabled' = 'false'
    'TaskNotifications__Enabled' = 'false'
    'MaterialApprovalNotifications__Enabled' = 'false'
    'TaskSeriesGeneration__Enabled' = 'false'
    'InternalRequestNotifications__Enabled' = 'false'
}
$previous = @{}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    Write-Output "Starting disposable cleaning API at https://localhost:$Port; use synthetic QA records only."
    & dotnet run --project (Join-Path $workspace 'src/HotelOps.Api/HotelOps.Api.csproj') --no-build --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw "Recovery API exited with code $LASTEXITCODE" }
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
