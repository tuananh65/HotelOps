$ErrorActionPreference='Stop'
Set-Location ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')))
$handover='scripts/verification/HANDOVER_CODEX_A_20261105'
$candidates=@(git ls-files --modified --deleted; git ls-files --others --exclude-standard)
# Retain raw text evidence excluded by gitignore, but not compiled output or binary artifacts.
$evidence=@(rg --files --hidden --no-ignore scripts/verification -g '*.log' -g '*.trx' -g '!**/bin/**' -g '!**/obj/**' -g '!**/artifacts/**' -g '!**/HANDOVER_CODEX_A_20261105/**')
$candidates=@($candidates+$evidence | Sort-Object -Unique)
$manifest=@();$findings=@()
foreach($p in $candidates){
 if($p -like "$handover/*"){continue}
 $p=$p.Replace('\','/')
 $action='STAGE_ORIGINAL';$dest=$p;$reason='Codex A current work, task evidence, or unchanged reference excerpts; not ownership of referenced DEV2 code'
 if(!(Test-Path -LiteralPath $p)){ $manifest+=@{path=$p;action='STAGE_DELETION';reason='Approved template removal; see FallbackAuthorization/README.md'};continue }
 $i=Get-Item -LiteralPath $p
 if($p -match '(?i)\.(zip|pdf|dll|pdb|exe|pfx|pem|key|bak|mdf|ldf|nupkg|binlog)$|(^|/)(secrets\.json|\.env[^/]*|appsettings\.Development\.json)$'){
  $manifest+=@{path=$p;action='EXCLUDE_ORIGINAL';sha256=(Get-FileHash $p).Hash;bytes=$i.Length;reason='Binary duplicate package/test fixture or sensitive/build type; expanded text/provenance retained where available; original retained locally'};continue
 }
 $text=[IO.File]::ReadAllText((Join-Path (Get-Location) $p))
 # Never export apparent live credentials. Values are not written to scan output.
 $strong='gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|eyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}'
 $connection='(?i)(?:;\s*)(Password|Pwd)\s*=\s*[^;\r\n"'']{4,}'
 if($text -match $strong -or $text -match $connection){
   $findings+=@{path=$p;kind='POTENTIAL_SECRET_REQUIRES_REVIEW';lineNumbers=@(($text -split "`n" | Select-String -Pattern "$strong|$connection").LineNumber)}
   $manifest+=@{path=$p;action='QUARANTINE_REVIEW';sha256=(Get-FileHash $p).Hash;reason='Potential secret; no plaintext exported'};continue
 }
 if($text -match '(?i)DESKTOP-[A-Z0-9-]+'){
   $dest="$handover/sanitized/$p.txt";$action='STAGE_SANITIZED_COPY';$reason='Private local SQL host redacted; original kept locally and not staged. Archived text is not executable without deliberate local configuration.'
   $text=[regex]::Replace($text,'(?i)DESKTOP-[A-Z0-9-]+','REDACTED_LOCAL_SQL_HOST')
   $parent=Split-Path $dest -Parent;[IO.Directory]::CreateDirectory((Join-Path (Get-Location) $parent))|Out-Null
   [IO.File]::WriteAllText((Join-Path (Get-Location) $dest),$text,[Text.UTF8Encoding]::new($false))
 }
 $manifest+=@{path=$p;action=$action;stagedPath=$dest;sha256=(Get-FileHash $p).Hash;publishedSha256=(Get-FileHash $dest).Hash;bytes=$i.Length;reason=$reason}
}
ConvertTo-Json -InputObject $manifest -Depth 8 | Set-Content -Encoding utf8 "$handover/PRESERVATION_MANIFEST.json"
ConvertTo-Json -InputObject $findings -Depth 8 | Set-Content -Encoding utf8 "$handover/SECRET_REVIEW.json"
$manifest | ForEach-Object { [pscustomobject]$_ } | Group-Object action | Select-Object Name,Count | Format-Table
$findings | Select-Object path,kind,lineNumbers | Format-Table

