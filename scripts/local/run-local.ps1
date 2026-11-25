param(
 [ValidateSet('Start','Stop','Status')][string]$Action='Status',
 [ValidateSet('All','Development','Production')][string]$Environment='All',
 [string]$DevelopmentDatabase='HotelOpsCompactWorkdayTestLocalDevV2261125',
 [string]$ProductionDatabase='HotelOpsCompactDB'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$state=Join-Path $root 'artifacts/local-runtime'
[void][IO.Directory]::CreateDirectory($state)
$publish=Join-Path $root '.artifacts/runtime-production/api'
$targets=@(
 @{name='Development';database=$DevelopmentDatabase;url='http://127.0.0.1:5081';port=5081;web='http://127.0.0.1:5173'},
 @{name='Production';database=$ProductionDatabase;url='https://localhost:5080';port=5080;web='https://localhost:5080'}
)|Where-Object {$Environment -eq 'All' -or $_.name -eq $Environment}
foreach($target in $targets){
 $officialProduction=$target.name -eq 'Production' -and $target.database -eq 'HotelOpsCompactDB'
 if(!$officialProduction -and $target.database -notmatch '^HotelOpsCompactWorkdayTestLocal(Dev|Production)[A-Za-z0-9_]+$'){throw 'Only isolated local databases or the authorized official Production database are accepted.'}
}
function OwnedProcess($record){
 $p=Get-Process -Id $record.pid -ErrorAction SilentlyContinue
 if($p -and $p.StartTime.ToUniversalTime().ToString('o') -ne $record.startedAt){throw 'PID ownership changed; process preserved.'}
 return $p
}
function StartOwned([string]$name,[string]$file,[string[]]$arguments,[string]$working,[hashtable]$variables,[hashtable]$metadata){
 $path=Join-Path $state ($name+'.json')
 if(Test-Path -LiteralPath $path){
  $record=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
  if(OwnedProcess $record){throw "$name is already running; use Status instead."}
  Remove-Item -LiteralPath $path
 }
 $saved=@{}
 try{
  foreach($key in $variables.Keys){$saved[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,[string]$variables[$key],'Process')}
  $p=Start-Process -FilePath $file -ArgumentList $arguments -WorkingDirectory $working -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $state ($name+'.log')) -RedirectStandardError (Join-Path $state ($name+'.err.log'))
  $metadata.pid=$p.Id;$metadata.startedAt=$p.StartTime.ToUniversalTime().ToString('o');$metadata.name=$name
  $metadata|ConvertTo-Json|Set-Content -LiteralPath $path -Encoding UTF8
  return $p
 }finally{foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key],'Process')}}
}
function StopOwned([string]$name){
 $path=Join-Path $state ($name+'.json')
 if(!(Test-Path -LiteralPath $path)){return}
 $record=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 $p=OwnedProcess $record
 if($p){$p.Kill();if(!$p.WaitForExit(10000)){throw "$name did not exit."}}
 Remove-Item -LiteralPath $path
 Write-Output "Stopped $name"
}
function AssertFree([int]$port){
 if(@(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue).Count){throw "Port $port is occupied; existing process preserved."}
}
function AssertDatabase([string]$connectionString,[string]$expected){
 $db=[System.Data.SqlClient.SqlConnection]::new($connectionString)
 try{
  $db.OpenAsync().GetAwaiter().GetResult()
  $cmd=$db.CreateCommand();$cmd.CommandText="SELECT CASE WHEN DB_NAME()=@name AND (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)=57 THEN 1 ELSE 0 END"
  [void]$cmd.Parameters.AddWithValue('@name',$expected)
  if($cmd.ExecuteScalar() -ne 1){throw 'Runtime database name/schema mismatch. No migration is performed by this launcher.'}
 }finally{$db.Dispose()}
}
if($Action -eq 'Stop'){
 if($Environment -in @('All','Development')){StopOwned 'Development-Web'}
 foreach($target in $targets){StopOwned ($target.name+'-Api')}
 exit 0
}
if($Action -eq 'Status'){
 foreach($target in $targets){
  $path=Join-Path $state ($target.name+'-Api.json');$alive=$false;$health=$null
  if(Test-Path -LiteralPath $path){$record=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json;$alive=[bool](OwnedProcess $record)}
  if($alive){try{$health=Invoke-RestMethod ($target.url+'/health') -TimeoutSec 5}catch{}}
  [pscustomobject]@{environment=$target.name;running=$alive;healthy=$health.status -eq 'ok';tables=$health.tables;url=$target.web;database=$(if($alive){$record.database}else{$target.database})}
 }
 exit 0
}
if(!(Test-Path (Join-Path $publish 'HotelOps.Api.dll'))){throw 'Publish Release API first; see docs/deployment/local-development-production.md.'}
if($targets.name -contains 'Production' -and !(Test-Path (Join-Path $publish 'wwwroot/index.html'))){throw 'Built frontend must be copied into publish/wwwroot before Production starts.'}
foreach($target in $targets){AssertFree $target.port}
if($targets.name -contains 'Development'){AssertFree 5173}
$secretPath=Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json'
$settings=Get-Content -LiteralPath $secretPath -Raw|ConvertFrom-Json
$started=@()
try{
 foreach($target in $targets){
  $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$target.database
  AssertDatabase $builder.ConnectionString $target.database
  $variables=@{
   ConnectionStrings__HotelOpsDb=$builder.ConnectionString
   ASPNETCORE_ENVIRONMENT=$target.name
   DOTNET_ENVIRONMENT=$target.name
   ASPNETCORE_WEBROOT=(Join-Path $publish 'wwwroot')
   AllowedHosts='localhost;127.0.0.1'
   Compact__WorkerEnabled='true'
   Compact__CleaningOverdueEnabled='true'
   Compact__AutoEndShiftsEnabled='true'
   Email__Enabled='false'
   Email__AllowLiveDelivery='false'
   Email__DataProtectionKeysPath=(Join-Path $state ($target.name+'-keys'))
   Email__PublicBaseUrl=$target.web
   AI__Enabled='false'
  }
  $name=$target.name+'-Api'
  $p=StartOwned $name 'dotnet' @(('"'+(Join-Path $publish 'HotelOps.Api.dll')+'"'),'--urls',$target.url) $publish $variables @{database=$target.database;url=$target.url;environment=$target.name}
  $started+=,$name;$ready=$false
  for($i=0;$i -lt 80;$i++){
   if($p.HasExited){throw "$name exited; inspect artifacts/local-runtime logs."}
   try{$health=Invoke-RestMethod ($target.url+'/health') -TimeoutSec 2;if($health.status -eq 'ok' -and $health.tables -eq 57){$ready=$true;break}}catch{}
   Start-Sleep -Milliseconds 250
  }
  if(!$ready){throw "$name did not become healthy."}
  Write-Output "Ready $($target.name): $($target.url)"
 }
 if($targets.name -contains 'Development'){
  $vite=Join-Path $root 'frontend/node_modules/vite/bin/vite.js'
  [void](StartOwned 'Development-Web' 'node' @(('"'+$vite+'"'),'--host','127.0.0.1','--port','5173','--strictPort') (Join-Path $root 'frontend') @{API_PROXY_TARGET='http://127.0.0.1:5081'} @{url='http://127.0.0.1:5173';environment='Development'})
  $started+=,'Development-Web'
  Write-Output 'Development web: http://127.0.0.1:5173'
 }
 Write-Output 'Production web: https://localhost:5080 (when Production was selected).'
}catch{
 foreach($name in @($started)[($started.Count-1)..0]){if($name){try{StopOwned $name}catch{Write-Warning 'An owned process needs inspection.'}}}
 throw
}
