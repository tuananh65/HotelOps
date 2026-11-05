$ErrorActionPreference='Stop'
Set-Location ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')))
$paths=Get-Content (Join-Path $PSScriptRoot 'IGNORED_SCRATCH_PATHS.json') -Raw | ConvertFrom-Json
$rows=@();$seen=@{}
$known=@{}
foreach($ref in @('HEAD','cf8fb020362f8f64192e7f2f14a10547cebe5b91')){
 foreach($line in (git ls-tree -r $ref)){if($line -match '^\d+ blob ([a-f0-9]+)\t(.+)$'){$known[$Matches[1]]="$ref`:$($Matches[2])"}}
}
foreach($raw in $paths){
 $p=$raw.Replace('\','/');$bytes=[IO.File]::ReadAllBytes((Join-Path (Get-Location) $p));$sha=[Security.Cryptography.SHA256]::Create();$hash=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','');$sha.Dispose()
 $header=[Text.Encoding]::UTF8.GetBytes("blob $($bytes.Length)`0");$gitBytes=New-Object byte[] ($header.Length+$bytes.Length);[Array]::Copy($header,$gitBytes,$header.Length);[Array]::Copy($bytes,0,$gitBytes,$header.Length,$bytes.Length);$sha1=[Security.Cryptography.SHA1]::Create();$blob=([BitConverter]::ToString($sha1.ComputeHash($gitBytes))).Replace('-','').ToLowerInvariant();$sha1.Dispose()
 $owner='CODEX_A_ARCHIVAL_REFERENCE';$action='ARCHIVE_UNAPPROVED_WIP';$dest=$null
 if($p -match '/(Services|DTOs)/(Incidents|Maintenance|Assets|Inventory|MaterialRequests|Replenishments)/|/tests/HotelOps\.(Inventory|IncidentMaintenance|MaterialRequests|Replenishment)\.|/Controllers/(Incidents|MaintenanceWorks|Assets|Inventory|MaterialRequests|Replenishments)Controller'){$owner='DEV2_REFERENCE_COPY_NOT_CLAIMED';$action='INVENTORY_ONLY_OTHER_OWNER'}
 $text=[Text.Encoding]::UTF8.GetString($bytes)
 if($text -match 'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|(?i)"(password|token|credential|connectionString)"\s*:\s*"[^"\r\n]{6,}"|;\s*(Password|Pwd)\s*=\s*[^;\r\n"'']{4,}'){$action='EXCLUDE_POTENTIAL_SENSITIVE_SCRATCH'}
 if($action -eq 'ARCHIVE_UNAPPROVED_WIP'){
   $dest='scratch-archive/'+$hash+'.txt'
   if(!$seen.ContainsKey($hash)){
      $text=[regex]::Replace($text,'(?i)DESKTOP-[A-Z0-9-]+','REDACTED_LOCAL_SQL_HOST')
      $full=Join-Path $PSScriptRoot $dest;[IO.Directory]::CreateDirectory((Split-Path $full -Parent))|Out-Null
      [IO.File]::WriteAllText($full,$text,[Text.UTF8Encoding]::new($false));$seen[$hash]=$true
   }
 }
 $rows+=@{originalPath=$p;originalSha256=$hash;gitBlob=$blob;knownGitLocation=$known[$blob];bytes=$bytes.Length;owner=$owner;action=$action;archivePath=$dest;executable=$false}
}
ConvertTo-Json -InputObject $rows -Depth 8 | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot 'SCRATCH_SOURCE_INVENTORY.json')
$rows | ForEach-Object {[pscustomobject]$_} | Group-Object action | Select-Object Name,Count | ConvertTo-Json
Write-Output "Unique archived text blobs: $($seen.Count)"
