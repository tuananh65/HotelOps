[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RepositoryRoot,[switch]$Execute)
$ErrorActionPreference='Stop'
$repository=(Resolve-Path -LiteralPath $RepositoryRoot).Path
$manifestPath=Join-Path $PSScriptRoot 'SOURCE_MANIFEST.json'
if(!(Test-Path -LiteralPath $manifestPath)){throw 'Run the packaged SETUP_TESTER.ps1 beside SOURCE_MANIFEST.json, not this source template.'}
$manifest=Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath|ConvertFrom-Json
foreach($entry in $manifest.files){
 $target=[IO.Path]::GetFullPath((Join-Path $repository $entry.path))
 if(!$target.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path escapes repository'}
 if(!(Test-Path -LiteralPath $target)){throw ('Missing source: '+$entry.path)}
 if($entry.normalization -eq 'TEXT_LF'){
  $text=[IO.File]::ReadAllText($target).Replace("`r`n","`n").TrimStart([char]0xFEFF)
  $hasher=[Security.Cryptography.SHA256]::Create()
  try{$actual=([BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-','')}finally{$hasher.Dispose()}
 }else{$actual=(Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash}
 if($actual -ine $entry.sha256){throw ('Source mismatch: '+$entry.path+'; checkout tested commit before setup')}
}
Write-Output ('TESTED_SOURCE_HASHES_VERIFIED '+$manifest.files.Count)
& (Join-Path $repository 'scripts/verification/FS011/New-TesterTrial.ps1') -RepositoryRoot $repository -Execute:$Execute
