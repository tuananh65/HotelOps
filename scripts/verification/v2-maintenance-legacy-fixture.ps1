param([ValidateSet('Seed')][string]$Action='Seed')
$ErrorActionPreference='Stop'
$database='HotelOpsCompactWorkdayTestV22611250010'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runId=[string]$env:HOTELOPS_V2_RUN_ID
$parsedRun=[Guid]::Empty
if(-not [Guid]::TryParse($runId,[ref]$parsedRun)){throw 'Valid run identity required.'}
$fixture=Get-Content (Join-Path $root 'artifacts/restructure-v2/housekeeping-private/fixture.json') -Raw|ConvertFrom-Json
if($fixture.runId -cne $runId -or $fixture.database -cne $database -or $fixture.prefix -cnotmatch '^V2HK-[a-f0-9]{10}$'){throw 'Owned TEST fixture required.'}
$inputData=$env:HOTELOPS_LEGACY_SEED|ConvertFrom-Json
if(-not $inputData -or $inputData.title -cnotmatch '^TEST legacy recover [a-f0-9-]{36}$' -or $inputData.paging -cnotmatch '^TEST paging [a-f0-9-]{36}$'){throw 'Synthetic titles required.'}
if(@($fixture.rooms|Where-Object roomId -eq $inputData.roomId).Count -ne 1 -or @($fixture.assets|Where-Object {$_.assetId -eq $inputData.assetId -and $_.roomId -eq $inputData.roomId}).Count -ne 1 -or [long]$inputData.cleaningWorkId -le 0){throw 'Owned room, asset and cleaning required.'}
$backup=Get-Content (Join-Path $root ('artifacts/workday/backup-'+$database+'.json')) -Raw|ConvertFrom-Json
$age=([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes
if($backup.verified -ne $true -or $backup.database -cne $database -or $age -lt 0 -or $age -gt 15 -or -not $backup.backup){throw 'Fresh verified TEST backup required.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.OpenAsync().GetAwaiter().GetResult()
 $verify=$connection.CreateCommand();$verify.CommandTimeout=120
 try {
  $verify.CommandText="IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Exact TEST database required',1; RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;"
  [void]$verify.Parameters.AddWithValue('@file',[string]$backup.backup)
  [void]$verify.ExecuteNonQueryAsync().GetAwaiter().GetResult()
 } finally {$verify.Dispose()}
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60
 try {
  $cmd.CommandText=Get-Content (Join-Path $PSScriptRoot 'v2-maintenance-legacy-fixture.sql') -Raw
  $parameters=@{branch=[long]$fixture.branch;prefix=[string]$fixture.prefix;runId=$runId;actor=[long]($fixture.actors|Where-Object alias -eq 'hk').user;supervisor=[long]($fixture.actors|Where-Object alias -eq 'supervisor').user;room=[long]$inputData.roomId;asset=[long]$inputData.assetId;cleaning=[long]$inputData.cleaningWorkId;title=[string]$inputData.title;paging=[string]$inputData.paging}
  foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue('@'+$key,$parameters[$key])}
  [string]$cmd.ExecuteScalarAsync().GetAwaiter().GetResult()
 } finally {$cmd.Dispose()}
} finally {$connection.Dispose()}
