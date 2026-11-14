param([Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [ValidateRange(0,7)][int]$StartAt=0)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$database='HotelOps_DEV3_142A_20261114_cc78c13f'
$run='cc78c13ffc124efcad67b3d804baa84c'
$files=@(
 'database/patches/20261019_inventory_contract.sql',
 'database/patches/20261020_material_request_contract.sql',
 'database/patches/20261021_replenishment_contract.sql',
 'database/patches/20261022_maintenance_material_usage_contract.sql',
 'scripts/database/release/uc017/05_UC017_Branch_RoomTypes.sql',
 'scripts/database/release/checklists/01_Checklists.sql',
 'scripts/database/release/housekeeping/01_Housekeeping.sql',
 'scripts/database/release/progress/01_Progress.sql'
)
$guard=@'
SET NOCOUNT ON; SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run'
   AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51992,'Exact sprint-owned database required',1;
IF EXISTS(SELECT 1 FROM dbo.users) OR EXISTS(SELECT 1 FROM dbo.branches)
 THROW 51992,'Empty synthetic personnel/branch prerequisite required',1;
BEGIN TRANSACTION;
GO
'@
$receipt=Join-Path $EvidenceDirectory 'schema-prerequisites.json'
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
if(Test-Path $receipt){throw 'Do not overwrite or replay prior installation; inspect exact state first'}
$results=[Collections.Generic.List[object]]::new()
$utf8=[Text.UTF8Encoding]::new($false)
foreach($relative in $files[$StartAt..($files.Length-1)]){
 $source=Join-Path $root $relative
 $body=[IO.File]::ReadAllText($source)
 if($body -match '(?im)^\s*(USE\s|CREATE\s+DATABASE|ALTER\s+DATABASE|DROP\s+DATABASE)'){throw 'Unexpected database-level operation'}
 # Preserve schema and business checks. Replace only historical target/ownership bindings.
 $body=$body.Replace('HotelOpsDB',$database).Replace('HotelOps.UserDirectory.Trial','DEV3.Sprint142A.Run').Replace('Task17',$run)
 foreach($pattern in @('HotelOps[_]Canonical[_]%[_]AuthUpgrade','HotelOps[_]Canonical[_]%[_]Upgrade','HotelOps[_]Canonical[_]%[_]Fresh','HotelOps[_]Canonical[_]%')){
  $body=$body.Replace($pattern,$database)
 }
 $postflight=''
 $package = switch -Wildcard ($relative) {
  '*/checklists/*' {'Checklists'}
  '*/housekeeping/*' {'Housekeeping'}
  '*/progress/*' {'Progress'}
 }
 if($package){
  # Match the repository package runner: verify metadata before publishing its exact source hash.
  $verify=Join-Path (Split-Path $source) '02_Verify.sql'
  $postflight="`nGO`n"+[IO.File]::ReadAllText($verify)+"`nGO`nEXEC sys.sp_addextendedproperty @name=N'HotelOps.$package.V1',@value=N'"+(Get-FileHash $source -Algorithm SHA256).Hash+"';`n"
 }
 $derived=$guard+"`n"+$body+$postflight+"`nGO`nIF @@TRANCOUNT<>1 THROW 51992,'Unexpected transaction depth',1;`nCOMMIT TRANSACTION;`n"
 $temp=Join-Path ([IO.Path]::GetTempPath()) ('dev3-142a-'+[Guid]::NewGuid().ToString('N')+'.sql')
 [IO.File]::WriteAllText($temp,$derived,$utf8)
 $log=Join-Path $EvidenceDirectory ('schema-'+[IO.Path]::GetFileNameWithoutExtension($relative)+'.log.txt')
 try {
  & sqlcmd -S localhost -E -C -b -d $database -i $temp -o $log
  $code=$LASTEXITCODE
  $results.Add([ordered]@{source=$relative; sourceSha256=(Get-FileHash $source -Algorithm SHA256).Hash;
    derivedSha256=(Get-FileHash $temp -Algorithm SHA256).Hash;exitCode=$code;log=[IO.Path]::GetFileName($log)})
  [IO.File]::WriteAllText($receipt,($results|ConvertTo-Json -Depth 5),$utf8)
  if($code -ne 0){throw "Installation failed at $relative; this script's transaction rolled back; earlier scripts retained"}
 } finally {Remove-Item -LiteralPath $temp}
}
'Reviewed prerequisite installation complete; run model and runtime checks separately.'
