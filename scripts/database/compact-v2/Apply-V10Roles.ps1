param([string]$Database='HotelOpsCompactWorkdayTestActor20261122')
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact[A-Za-z0-9_]*$'){throw 'Compact database required.'}
& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120
 $cmd.CommandText=Get-Content -LiteralPath (Join-Path $PSScriptRoot '07_V10Roles.sql') -Encoding utf8 -Raw
 [void]$cmd.ExecuteNonQuery()
 $cmd.CommandText="SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS tables,(SELECT COUNT(*) FROM roles) AS roles,(SELECT COUNT(*) FROM users) AS users,(SELECT COUNT(*) FROM workday_shifts) AS shifts FOR JSON PATH,WITHOUT_ARRAY_WRAPPER"
 Write-Output $cmd.ExecuteScalar()
} finally {$connection.Dispose()}
