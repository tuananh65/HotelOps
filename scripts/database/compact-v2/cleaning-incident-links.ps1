param([Parameter(Mandatory=$true)][ValidateSet('HotelOpsCompactWorkdayTestWarehouse261124','HotelOpsCompactWorkdayTestP0261124','HotelOpsCompactDB')][string]$Database)
$ErrorActionPreference='Stop'
$backup=Get-Content "artifacts/workday/backup-$Database.json" -Raw -Encoding utf8 | ConvertFrom-Json
if(-not $backup.verified){throw 'Verified backup required'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try{$connection.Open();$command=$connection.CreateCommand();$command.CommandTimeout=90;$command.CommandText=Get-Content (Join-Path $PSScriptRoot 'cleaning-incident-links.sql') -Raw -Encoding utf8;$added=$command.ExecuteScalar();Write-Output "$Database : recovered $added recorded incident links"}finally{$connection.Dispose()}
