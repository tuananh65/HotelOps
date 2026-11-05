$ErrorActionPreference='Stop'
Set-Location ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')))
$manifest=Get-Content (Join-Path $PSScriptRoot 'PRESERVATION_MANIFEST.json') -Raw | ConvertFrom-Json
if(@(git diff --cached --name-only).Count -gt 0){throw 'Root index must match audited empty preflight before initial staging.'}
$paths=@($manifest | Where-Object {$_.action -in @('STAGE_ORIGINAL','STAGE_DELETION')} | ForEach-Object path)
$paths+=@(Get-ChildItem -LiteralPath $PSScriptRoot -File -Recurse | ForEach-Object {$_.FullName.Substring((Get-Location).Path.Length+1).Replace('\','/')})
$pathFile=Join-Path $PSScriptRoot 'SELECTED_PATHS.txt'
[IO.File]::WriteAllLines($pathFile,@($paths|Sort-Object -Unique),[Text.UTF8Encoding]::new($false))
& git --literal-pathspecs add -f --pathspec-from-file=$pathFile
if($LASTEXITCODE -ne 0){throw 'Selective git add failed'}
foreach($m in @($manifest|Where-Object {$_.action -eq 'STAGE_SANITIZED_COPY'})){
 $blob=git hash-object -w --no-filters -- $m.stagedPath
 if($LASTEXITCODE -ne 0){throw 'Sanitized blob creation failed'}
 & git update-index --add --cacheinfo 100644 $blob $m.path
 if($LASTEXITCODE -ne 0){throw 'Sanitized selective index update failed'}
}
git add -f -- $pathFile
