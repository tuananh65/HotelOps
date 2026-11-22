param([switch]$Apply)
$ErrorActionPreference='Stop'
$backupFile=Join-Path $PSScriptRoot '../../../artifacts/workday/backup-HotelOpsCompactDB.json'
$backup=Get-Content -LiteralPath $backupFile -Raw | ConvertFrom-Json
if(-not $backup.verified -or $backup.database -ne 'HotelOpsCompactDB' -or ([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes -gt 30){throw 'A fresh verified backup is required, including for transactional dry-run.'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
if($builder.InitialCatalog -ne 'HotelOpsCompactDB'){throw 'Unexpected database'}
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60
 $cmd.CommandText=Get-Content (Join-Path $PSScriptRoot 'fixed-operational-levels.sql') -Encoding UTF8 -Raw
 [void]$cmd.Parameters.Add('@apply',[System.Data.SqlDbType]::Bit);$cmd.Parameters['@apply'].Value=[bool]$Apply
 $reader=$cmd.ExecuteReader();$rows=@()
 do {while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$rows+=[pscustomobject]$row}} while($reader.NextResult())
 $reader.Close()
 $out=Join-Path $PSScriptRoot '../../../artifacts/fixed-operational-levels';[void](New-Item -ItemType Directory -Force $out)
 $mode=if($Apply){'applied'}else{'dry-run'}
 ConvertTo-Json -InputObject $rows | Set-Content (Join-Path $out ($mode+'.json')) -Encoding UTF8
 if($Apply){Copy-Item -LiteralPath $backupFile -Destination (Join-Path $out 'backup.json')}
 ConvertTo-Json -InputObject $rows
}finally{$connection.Dispose()}
