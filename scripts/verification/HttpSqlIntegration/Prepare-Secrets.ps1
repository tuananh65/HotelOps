$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Integration-Environment.ps1')
$config=Get-IntegrationConfig
$folder=Join-Path $env:APPDATA 'Microsoft/UserSecrets/HotelOps.Authorization.Integration.Task06'
$file=Join-Path $folder 'secrets.json'
if(Test-Path -LiteralPath $file){
    $existing=Get-Content -Raw -LiteralPath $file|ConvertFrom-Json
    foreach($name in @('assigned','former','none','restricted','inactive','mixed','branchb','revoked')){if(!$existing."Passwords:$name"){throw 'Existing credential store incomplete; do not regenerate credentials for existing fixtures'}}
    Write-Output 'Integration secret store already exists; preserved';exit 0
}
$secrets=@{'ConnectionStrings:Integration'=$config.ConnectionString;'Integration:ExpectedServer'=$config.ExpectedServer}
$rng=[System.Security.Cryptography.RandomNumberGenerator]::Create()
try {foreach($name in @('assigned','former','none','restricted','inactive','mixed','branchb','revoked')){
    $bytes=New-Object byte[] 32;$rng.GetBytes($bytes)
    $secrets["Passwords:$name"]='It06!'+[Convert]::ToBase64String($bytes)
}}finally{$rng.Dispose()}
[void](New-Item -ItemType Directory -Path $folder -Force)
$secrets|ConvertTo-Json|Set-Content -LiteralPath $file -Encoding UTF8
Write-Output 'PASS: independent random credentials saved only in Integration User Secrets'
