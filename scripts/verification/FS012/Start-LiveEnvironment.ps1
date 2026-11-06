[CmdletBinding()]
param([switch]$StartApprovedTrial,[switch]$DisableCandidate)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$target='HotelOps_Canonical_20261106_f120001_Fresh'
$private=Join-Path $PSScriptRoot '.private'
$processFile=Join-Path $private 'processes.json'
if(!$StartApprovedTrial){[ordered]@{mode='PLAN_ONLY';target=$target;browser='https://localhost:5173';viteLoopback='http://127.0.0.1:5189';api='https://localhost:7443';privateKeyExport=$false;trustStoreChange=$false;requiresApprovedSyntheticFixture=$true}|ConvertTo-Json;return}
if(Test-Path -LiteralPath $processFile){throw 'Existing process receipt: inspect/stop owned processes first; never kill by port.'}
$fixture=Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $private 'fixture.json')|ConvertFrom-Json
if($fixture.database -cne $target -or $fixture.owner -cne 'DEV1_FS012' -or !$fixture.sqlCertificateException){throw 'Approved fixture/SQL certificate scope mismatch'}
$listeners=[Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
if(@($listeners|Where-Object {$_.Port -in @(5173,5189,7443)}).Count){throw 'Required port occupied; do not reuse or kill unrelated processes.'}
$helper=Join-Path $PSScriptRoot 'bin/Debug/net10.0/FS012.Verification.dll'
$api=Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll'
if(!(Test-Path $helper) -or !(Test-Path $api)){throw 'Build helper and backend first'}
$candidateMode=if($DisableCandidate){'DISABLED'}else{'FS012_PROVISIONAL'}
$owned=New-Object Collections.Generic.List[object]
function StartOwned([string]$name,[string]$exe,[string]$arguments,[string]$working,[hashtable]$variables){
 $prior=@{};foreach($key in $variables.Keys){$prior[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,[string]$variables[$key],'Process')}
 try{
  $process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $working -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $private ($name+'.stdout.log')) -RedirectStandardError (Join-Path $private ($name+'.stderr.log'))
  $owned.Add([pscustomobject]@{name=$name;id=$process.Id;startTimeUtcTicks=$process.StartTime.ToUniversalTime().Ticks})
  [ordered]@{owner='DEV1_FS012';database=$target;processes=@($owned.ToArray())}|ConvertTo-Json -Depth 4|Set-Content -Encoding utf8 -LiteralPath $processFile
 }finally{foreach($key in $prior.Keys){[Environment]::SetEnvironmentVariable($key,$prior[$key],'Process')}}
}
try{
 $public=Join-Path $private 'localhost-public.pem'
 $publicBefore=if(Test-Path -LiteralPath $public){[IO.File]::GetLastWriteTimeUtc($public)}else{[DateTime]::MinValue}
 StartOwned 'gateway' 'dotnet' ('"'+$helper+'" gateway --approved-local-gateway') $root @{DOTNET_ENVIRONMENT='Development';ASPNETCORE_ENVIRONMENT='Development'}
 $public=Join-Path $private 'localhost-public.pem'
 for($i=0;$i -lt 100;$i++){if((Test-Path -LiteralPath $public) -and [IO.File]::GetLastWriteTimeUtc($public) -gt $publicBefore -and [IO.File]::ReadAllText($public).Contains('-----END CERTIFICATE-----')){break};Start-Sleep -Milliseconds 100}
 if(!(Test-Path -LiteralPath $public) -or [IO.File]::GetLastWriteTimeUtc($public) -le $publicBefore){throw 'Fresh atomic public certificate export unavailable; private key export forbidden.'}
 $b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
 $b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']=$target;$b['Integrated Security']=$true;$b['Encrypt']=$true;$b['TrustServerCertificate']=$true
 StartOwned 'api' 'dotnet' ('"'+$api+'" --environment Development --urls https://localhost:7443') (Join-Path $root 'src/HotelOps.Api') @{DOTNET_ENVIRONMENT='Development';ASPNETCORE_ENVIRONMENT='Development';ConnectionStrings__HotelOpsDb=$b.ConnectionString;Logging__LogLevel__Default='Warning';Evidence__PrivateRoot=(Join-Path $private 'evidence');CleaningAssignment__Candidate=$candidateMode}
 $vite=Join-Path $root 'frontend/node_modules/vite/bin/vite.js'
 StartOwned 'vite' 'node' ('"'+$vite+'" --host 127.0.0.1 --port 5189') (Join-Path $root 'frontend') @{API_PROXY_TARGET='https://localhost:7443';NODE_EXTRA_CA_CERTS=$public;DEV_HTTPS_CERT='';DEV_HTTPS_KEY='';VITE_DEV1_API_BASE_URL='/api'}
 Add-Type -AssemblyName System.Net.Http
 $client=New-Object Net.Http.HttpClient;$client.Timeout=[TimeSpan]::FromSeconds(2)
 $ready=$false
 try{for($i=0;$i -lt 60;$i++){try{$response=$client.GetAsync('https://localhost:5173/api/auth/csrf').GetAwaiter().GetResult();try{if([int]$response.StatusCode -eq 200){$ready=$true;break}}finally{$response.Dispose()}}catch{};Start-Sleep -Milliseconds 250}}finally{$client.Dispose()}
 if(!$ready){throw 'Live HTTPS/proxy/backend startup not ready. Inspect private logs locally; do not publish tokens/provider payloads.'}
 [ordered]@{status='LIVE_SERVICES_READY';target=$target;browser='https://localhost:5173';routeChain='TLS store-key gateway -> Vite loopback proxy -> HTTPS API -> approved SQL trial';privateKeyExport=$false;trustStoreChange=$false;authenticationVerified=$false;processReceipt='FS012/.private/processes.json'}|ConvertTo-Json
}catch{
 # Stop only processes this invocation created and whose start identity still matches.
 foreach($entry in $owned){try{$p=Get-Process -Id $entry.id -ErrorAction Stop;if($p.StartTime.ToUniversalTime().Ticks -eq $entry.startTimeUtcTicks){Stop-Process -Id $entry.id -ErrorAction Stop}}catch{}}
 if(Test-Path -LiteralPath $processFile){Remove-Item -LiteralPath $processFile}
 throw
}
