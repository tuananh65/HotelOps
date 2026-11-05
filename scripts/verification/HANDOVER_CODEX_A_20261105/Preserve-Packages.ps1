$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
Set-Location $root
$m=Get-Content (Join-Path $PSScriptRoot 'PRESERVATION_MANIFEST.json') -Raw | ConvertFrom-Json
$entries=@();$seen=@{}
foreach($z in @($m|Where-Object {$_.path -like '*.zip'})){
 $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $root $z.path))
 try{foreach($e in $zip.Entries){if(!$e.Name){continue};$stream=$e.Open();$memory=[IO.MemoryStream]::new();try{$stream.CopyTo($memory);$bytes=$memory.ToArray()}finally{$stream.Dispose();$memory.Dispose()}
 $hash=([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes))).Replace('-','');$dest=$null;$action='BINARY_INVENTORY_ONLY'
 if($e.Name -match '\.(md|json|txt|csv|cs|ps1|sql|patch|diff|csproj|targets|proposal|fragment)$'){
  $text=[Text.Encoding]::UTF8.GetString($bytes)
  if($text -match 'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|(?i);\s*(Password|Pwd)\s*=\s*[^;\r\n"'']{4,}'){$action='SENSITIVE_QUARANTINE'}
  else{$action='PRESERVED_TEXT';$dest='package-archive/'+$hash+'.txt';if(!$seen.ContainsKey($hash)){$full=Join-Path $PSScriptRoot $dest;[IO.Directory]::CreateDirectory((Split-Path $full -Parent))|Out-Null;$text=[regex]::Replace($text,'(?i)DESKTOP-[A-Z0-9-]+','REDACTED_LOCAL_SQL_HOST');[IO.File]::WriteAllText($full,$text,[Text.UTF8Encoding]::new($false));$seen[$hash]=$true}}
 }
 $entries+=@{zip=$z.path;zipSha256=$z.sha256;entry=$e.FullName;originalSha256=$hash;bytes=$bytes.Length;action=$action;archivePath=$dest}
 }}finally{$zip.Dispose()}
}
ConvertTo-Json -InputObject $entries -Depth 8 | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot 'ZIP_INVENTORY.json')
# Retain harmless 42-byte historical test fixture as encoded evidence, without binary packaging.
$pdf='scripts/verification/Task85/PhaseB/private-evidence/history.pdf'
@{path=$pdf;sha256=(Get-FileHash $pdf).Hash;bytes=(Get-Item $pdf).Length;fixtureNotDatabaseDump=$true;base64=[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $pdf)))} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot 'HISTORICAL_PDF_FIXTURE.json')
$entries | ForEach-Object {[pscustomobject]$_} | Group-Object action | Select-Object Name,Count | ConvertTo-Json
