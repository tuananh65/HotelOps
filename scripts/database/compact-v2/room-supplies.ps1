param([Parameter(Mandatory=$true)][ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestP0261124','HotelOpsCompactWorkdayTestRoomSupplies261124')][string]$Database,[switch]$DevelopFill102)
$ErrorActionPreference='Stop'
$backup=Get-Content "artifacts/workday/backup-$Database.json" -Raw -Encoding utf8 | ConvertFrom-Json
if(-not $backup.verified -or $backup.database -ne $Database -or ([DateTime]::UtcNow-[DateTime]$backup.at).TotalHours -gt 4){throw 'A fresh verified backup of this database is required.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult();$command=$connection.CreateCommand();$command.CommandTimeout=120
 $command.CommandText=Get-Content (Join-Path $PSScriptRoot 'room-supplies.sql') -Raw -Encoding utf8
 [void]$command.Parameters.AddWithValue('@DevelopFill102',[bool]$DevelopFill102)
 $reader=$command.ExecuteReaderAsync().GetAwaiter().GetResult();$sets=@()
 do {$rows=@();while($reader.ReadAsync().GetAwaiter().GetResult()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$rows+=,$row};$sets+=,@($rows)}while($reader.NextResultAsync().GetAwaiter().GetResult());$reader.Dispose()
 New-Item -ItemType Directory -Force artifacts/room-supplies | Out-Null
 $result=@{database=$Database;at=[DateTime]::UtcNow.ToString('o');backup=$backup.backup;results=$sets}
 $result | ConvertTo-Json -Depth 8 | Set-Content "artifacts/room-supplies/applied-$Database-$(Get-Date -Format yyyyMMddHHmmss).json" -Encoding utf8
 $sets[0] | ConvertTo-Json -Compress
} finally {$connection.Dispose()}
