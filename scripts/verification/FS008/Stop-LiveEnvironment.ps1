[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot '.private/processes.json'
if(!(Test-Path -LiteralPath $path)){Write-Output 'NO_FS008_PROCESS_RECEIPT';return}
$receipt=Get-Content -Raw -Encoding utf8 -LiteralPath $path|ConvertFrom-Json
if($receipt.owner -cne 'DEV1_FS008' -or $receipt.database -cne 'HotelOps_Canonical_20261106_220000_f080001_Fresh'){throw 'Process receipt scope mismatch'}
$stopped=0;$absent=0;$mismatch=$false
foreach($entry in $receipt.processes){
 try{$process=Get-Process -Id $entry.id -ErrorAction Stop}catch{$absent++;continue}
 if($process.StartTime.ToUniversalTime().Ticks -ne $entry.startTimeUtcTicks){$mismatch=$true;continue}
 Stop-Process -Id $entry.id;$stopped++
}
if($mismatch){throw 'PID reuse/identity mismatch; unrelated process not stopped; receipt retained'}
Remove-Item -LiteralPath $path
[ordered]@{status='OWNED_SERVICES_STOPPED';stopped=$stopped;alreadyExited=$absent;databaseDropped=$false;privateFilesDeleted=$false}|ConvertTo-Json
