param([switch]$Apply)
$ErrorActionPreference='Stop'
if($Apply){
 $backup=Get-Content (Join-Path $PSScriptRoot '../../../artifacts/workday/backup-HotelOpsCompactDB.json') -Raw | ConvertFrom-Json
 if(-not $backup.verified -or $backup.database -ne 'HotelOpsCompactDB' -or ([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes -gt 30){throw 'A fresh verified backup is required.'}
}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
if($builder.InitialCatalog -ne 'HotelOpsCompactDB'){throw 'Unexpected database'}
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60
 $cmd.CommandText=Get-Content (Join-Path $PSScriptRoot 'role-cleanup-20261122.sql') -Encoding UTF8 -Raw
 [void]$cmd.Parameters.Add('@apply',[System.Data.SqlDbType]::Bit);$cmd.Parameters['@apply'].Value=[bool]$Apply
 $table=New-Object System.Data.DataTable
 $reader=$cmd.ExecuteReader();$table.Load($reader);$reader.Close()
 $rows=@();foreach($record in $table.Rows){$row=[ordered]@{};foreach($column in $table.Columns){$row[$column.ColumnName]=$record[$column.ColumnName]};$rows+=[pscustomobject]$row}
 $out=Join-Path $PSScriptRoot '../../../artifacts/role-cleanup';[void](New-Item -ItemType Directory -Force $out)
 $mode=if($Apply){'applied'}else{'dry-run'}
 $rows|ConvertTo-Json|Set-Content (Join-Path $out ($mode+'.json')) -Encoding UTF8
 $rows|ConvertTo-Json
 if($Apply){Copy-Item (Join-Path $PSScriptRoot '../../../artifacts/workday/backup-HotelOpsCompactDB.json') (Join-Path $out 'backup.json')}
}finally{$connection.Dispose()}
