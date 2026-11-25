param([Parameter(Mandatory=$true)][ValidatePattern('^HotelOpsCompactWorkdayTestOfficial[0-9]+$')][string]$Database)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=Join-Path $root 'artifacts/official-v2-upgrade'
if(Get-NetTCPConnection -LocalPort 5092 -State Listen -ErrorAction SilentlyContinue){throw 'Rehearsal port occupied; existing process preserved.'}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$publish=Join-Path $root '.artifacts/runtime-production/api'
$vars=@{ConnectionStrings__HotelOpsDb=$builder.ConnectionString;ASPNETCORE_ENVIRONMENT='Development';DOTNET_ENVIRONMENT='Development';Compact__WorkerEnabled='false';Compact__CleaningOverdueEnabled='false';Compact__AutoEndShiftsEnabled='false';Email__Enabled='false';AI__Enabled='false';AllowedHosts='localhost;127.0.0.1';Logging__LogLevel__Default='Warning'}
$saved=@{};$process=$null
try{
 foreach($key in $vars.Keys){$saved[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,[string]$vars[$key],'Process')}
 $process=Start-Process dotnet -ArgumentList @(('"'+(Join-Path $publish 'HotelOps.Api.dll')+'"'),'--urls','http://127.0.0.1:5092') -WorkingDirectory $publish -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'rehearsal-api.log') -RedirectStandardError (Join-Path $out 'rehearsal-api.err.log')
 $ready=$false
 for($i=0;$i -lt 80;$i++){
  if($process.HasExited){throw 'Rehearsal API failed SchemaGate/startup; inspect log.'}
  try{$health=Invoke-RestMethod 'http://127.0.0.1:5092/health' -TimeoutSec 2;if($health.status -eq 'ok' -and $health.tables -eq 57){$ready=$true;break}}catch{}
  Start-Sleep -Milliseconds 250
 }
 if(!$ready){throw 'Rehearsal health timeout.'}
 @{passed=$true;database=$Database;schemaGateAllMappings=$true;tables=$health.tables;workersEnabled=$false;at=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $out ($Database+'-runtime.json')) -Encoding UTF8
 Write-Output 'PASS: actual API startup, complete EF SchemaGate and health; rehearsal workers disabled.'
}finally{
 if($process -and !$process.HasExited){$process.Kill();[void]$process.WaitForExit(10000)}
 foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key],'Process')}
}
