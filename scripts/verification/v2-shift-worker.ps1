param([ValidateSet('Start','Stop')][string]$Action)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$directory=Join-Path $root 'artifacts/restructure-v2/housekeeping-private'
$path=Join-Path $directory 'shift-worker.json'
$runId=[string]$env:HOTELOPS_V2_RUN_ID
if(-not $runId){throw 'Run identity required.'}
if($Action -eq 'Stop'){
 if(Test-Path $path){
  $owned=Get-Content $path -Raw|ConvertFrom-Json
  if($owned.runId -ne $runId){throw 'Worker belongs to another run.'}
  $process=Get-Process -Id $owned.pid -ErrorAction SilentlyContinue
  if($process){
   if($process.StartTime.ToUniversalTime().ToString('o') -ne $owned.created){throw 'PID reuse guard.'}
   Stop-Process -Id $process.Id -Force
  }
  Remove-Item -LiteralPath $path
 }
 '{"stopped":true}';exit 0
}
if(Test-Path $path){throw 'Owned worker metadata exists.'}
$fixture=Get-Content (Join-Path $directory 'fixture.json') -Raw|ConvertFrom-Json
if($fixture.runId -ne $runId -or $fixture.database -ne 'HotelOpsCompactWorkdayTestV22611250010'){throw 'Owned TEST fixture required.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$fixture.database
$build=if($env:HOTELOPS_V2_TEST_BUILD){[IO.Path]::GetFullPath($env:HOTELOPS_V2_TEST_BUILD)}else{Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0'}
if(-not $build.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path (Join-Path $build 'HotelOps.Api.dll'))){throw 'Existing workspace assembly required.'}
$env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Compact__WorkerEnabled='false'
$env:Compact__CleaningOverdueEnabled='false'
$env:Compact__AutoEndShiftsEnabled='true'
$env:Email__Enabled='false'
$env:AI__Enabled='false'
# Ephemeral loopback port; no HTTP requests are made to trigger shift expiration.
$process=Start-Process dotnet -ArgumentList @((Join-Path $build 'HotelOps.Api.dll'),'--urls','http://127.0.0.1:0') -WorkingDirectory (Join-Path $root 'src/HotelOps.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'shift-worker.log') -RedirectStandardError (Join-Path $directory 'shift-worker.err.log')
@{pid=$process.Id;created=$process.StartTime.ToUniversalTime().ToString('o');runId=$runId}|ConvertTo-Json|Set-Content $path
'{"started":true}'
