param([string]$EnvRoot='D:\SEP490\Projects\HotelOps-Integration\obj\env-fe03-001')
$ErrorActionPreference='Stop'
. (Join-Path $EnvRoot 'Common.ps1')
Assert-Hax
$m=Get-Content (Join-Path $EnvRoot 'manifest.json') -Raw|ConvertFrom-Json
$sql=Open-Sql $TrialDatabase
try{Assert-Trial $sql $m -Lease}finally{$sql.Dispose()}
$cred=Import-Clixml (Join-Path $SecretDirectory 'Allowed.credential.clixml')
try{
 $env:FE03_LIVE_USER=$cred.UserName;$env:FE03_LIVE_PASSWORD=$cred.GetNetworkCredential().Password
 node (Join-Path $PSScriptRoot 'Read-LiveBrowser.mjs')
 if($LASTEXITCODE -ne 0){throw 'Live browser failed; see redacted check receipt'}
}finally{Remove-Item Env:FE03_LIVE_USER,Env:FE03_LIVE_PASSWORD -ErrorAction SilentlyContinue;$cred=$null}
