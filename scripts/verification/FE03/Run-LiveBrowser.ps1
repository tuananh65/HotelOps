param([string]$EnvRoot='D:\SEP490\Projects\HotelOps-Integration\obj\env-fe03-001', [string]$ExpectedApplicationSha='6a0c9248008203c245c347d9d6831279835ee544')
$ErrorActionPreference='Stop'
. (Join-Path $EnvRoot 'Common.ps1')
Assert-Hax
$m=Get-Content (Join-Path $EnvRoot 'manifest.json') -Raw|ConvertFrom-Json
if($m.source_commit -cne $ExpectedApplicationSha){throw 'BLOCKED_RUNTIME_VERSION'}
if($ExpectedApplicationSha -ceq '717715fa0f9fa51ba467592f1dd01ea06e8ce568'){
 . (Join-Path $PSScriptRoot 'HistoryAttribution.Assertions.ps1')
 $contract=Convert-HistoryHandoffContract (Get-Content (Join-Path $EnvRoot 'ENV-CONTRACT.json') -Raw|ConvertFrom-Json)
 Assert-HistoryRuntimeContract $contract $m $ExpectedApplicationSha
}
$dll=Join-Path $EnvRoot 'runtime/backend/HotelOps.Api.dll'
if((Get-FileHash $dll).Hash -cne $m.runtime_binary_sha256 -or [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion -cne ('1.0.0+'+$ExpectedApplicationSha)){throw 'Runtime binary identity mismatch'}
$sql=Open-Sql $TrialDatabase
try{Assert-Trial $sql $m -Lease}finally{$sql.Dispose()}
$savedConfig=@{}
foreach($key in @('FE03_LIVE_BASE','FE03_LIVE_SHA','FE03_LIVE_LEASE')){$savedConfig[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
$env:FE03_LIVE_BASE=$m.https_base_url;$env:FE03_LIVE_SHA=$ExpectedApplicationSha;$env:FE03_LIVE_LEASE=$m.lease_end_utc
$cred=Import-Clixml (Join-Path $SecretDirectory 'Allowed.credential.clixml')
try{
 $env:FE03_LIVE_USER=$cred.UserName;$env:FE03_LIVE_PASSWORD=$cred.GetNetworkCredential().Password
 node (Join-Path $PSScriptRoot 'Read-LiveBrowser.mjs')
 if($LASTEXITCODE -ne 0){throw 'Live browser failed; see redacted check receipt'}
}finally{foreach($key in $savedConfig.Keys){[Environment]::SetEnvironmentVariable($key,$savedConfig[$key],'Process')};Remove-Item Env:FE03_LIVE_USER,Env:FE03_LIVE_PASSWORD -ErrorAction SilentlyContinue;$cred=$null}
