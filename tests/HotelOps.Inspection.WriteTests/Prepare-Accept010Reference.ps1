param([Parameter(Mandatory=$true)][string]$ZipPath)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$destination=[IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/fast014/accept010/package'))
$pin='E5EA9DE23BC671C7088BA9325733EC22B2D474326E877A179E9F6D6CE8505EE2'
if((Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash -cne $pin){throw 'Exact ACCEPT010 ZIP SHA256 required'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ZipPath))
try {
 foreach($entry in $zip.Entries) {
  $relative=$entry.FullName.Replace('\','/');if($relative.EndsWith('/')){continue}
  $target=[IO.Path]::GetFullPath([IO.Path]::Combine($destination,$relative))
  if(!$target.StartsWith($destination+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive path escapes reference cache'}
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
  $stream=$entry.Open();$memory=[IO.MemoryStream]::new();try{$stream.CopyTo($memory);$bytes=$memory.ToArray()}finally{$stream.Dispose();$memory.Dispose()}
  if([IO.File]::Exists($target)) {
   $sha=[Security.Cryptography.SHA256]::Create();try{$expected=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','')}finally{$sha.Dispose()}
   if((Get-FileHash -LiteralPath $target).Hash -cne $expected){throw 'Existing reference differs: no overwrite'}
  }else{[IO.File]::WriteAllBytes($target,$bytes)}
 }
}finally{$zip.Dispose()}
$metadata=Join-Path $destination 'docs/integration-notes/DEV2-FE05-N01-ACCEPT-010'
$package=Get-Content -LiteralPath (Join-Path $metadata 'PACKAGE_SHA256.json') -Raw|ConvertFrom-Json
foreach($file in $package.files){if((Get-FileHash -LiteralPath (Join-Path $destination $file.path)).Hash -ine $file.sha256){throw ('Package hash mismatch: '+$file.path)}}
$allowlist=Get-Content -LiteralPath (Join-Path $metadata 'SOURCE_CHANGE_ALLOWLIST.json') -Raw|ConvertFrom-Json
$canonical=[Text.StringBuilder]::new()
foreach($file in $allowlist.files){if((Get-FileHash -LiteralPath (Join-Path $destination $file.path)).Hash -ine $file.sha256){throw ('Allowlist mismatch: '+$file.path)};[void]$canonical.Append($file.path).Append([char]0).Append($file.sha256).Append([char]10)}
$sha=[Security.Cryptography.SHA256]::Create();try{$identity=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical.ToString())))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
if($identity -cne '9ca9572711d6cc34c532e9530edd4c602bae615215e3a94a3d12eaf80b91fc1e'){throw 'Source identity mismatch'}
Write-Output ('PASS exact ZIP, '+$package.files.Count+' package hashes, '+$allowlist.files.Count+' allowlist hashes; source remains LOCAL_ONLY_UNCOMMITTED')
Write-Output ('Accept010Inputs='+$destination)
