param([Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$database='HotelOps_DEV3_142A_20261114_cc78c13f'
$run='cc78c13ffc124efcad67b3d804baa84c'
$guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0'
$utf8=[Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$receipt=Join-Path $EvidenceDirectory 'candidate-schema.json'
if(Test-Path $receipt){throw 'Prior installation receipt exists; no automatic replay'}
$guard=@'
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON;
SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
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
$results=[Collections.Generic.List[object]]::new()
$files=@(
 'scripts/verification/ScheduledWorkdayTrialHost/candidate/01-install-candidate.sql',
 'scripts/database/release/fs013-candidate/01_Install.sql',
 'scripts/database/release/fs013-candidate/05_VerifyLifecycle.sql',
 'scripts/database/release/fs013-candidate/02_NotificationProposal.sql',
 'scripts/database/release/fs013-candidate/03_VerifyNotificationProposal.sql'
)
foreach($relative in $files){
 $source=Join-Path $root $relative
 $body=[IO.File]::ReadAllText($source)
 if($body -match '(?im)^\s*(USE\s|CREATE\s+DATABASE|ALTER\s+DATABASE|DROP\s+DATABASE)'){throw 'Unexpected database targeting command'}
 if($relative -like '*/fs013-candidate/*'){
  # Replace exactly the historical three-line identity header with the GUID/run-bound guard above.
  $lines=$body -split '\r?\n'
  if($lines[0] -notlike '-- Development-only exact-target*' -or $lines[1] -notlike "IF DB_NAME()<>N'HotelOps_Canonical_20261106_f130001_Fresh'*" -or $lines[2] -notlike " THROW 51899,*"){throw 'Historical identity header changed'}
  $body=$lines[3..($lines.Length-1)] -join "`n"
 }else{
  $start=[DateTime]::UtcNow.AddMinutes(-1)
  $end=$start.AddHours(1)
  $variables=@{TrialServer='meita';TrialDatabase=$database;TrialDatabaseGuid=$guid;LeaseId=$run;
    LeaseStartsUtc=$start.ToString('yyyy-MM-ddTHH:mm:ss.fffffff');LeaseEndsUtc=$end.ToString('yyyy-MM-ddTHH:mm:ss.fffffff')}
  foreach($name in $variables.Keys){$body=$body.Replace(('$('+ $name +')'),$variables[$name])}
  $body=$body.Replace('HotelOps[_]UC139[_]Trial[_]%',$database).Replace('HotelOps.UC139.Trial','DEV3.Sprint142A.Run')
  if($body.Contains('$(')){throw 'Unresolved SQLCMD variable'}
 }
 $derived=$guard+"`n"+$body+"`nGO`nIF @@TRANCOUNT<>1 THROW 51992,'Unexpected transaction depth',1;`nCOMMIT;`n"
 $temp=Join-Path ([IO.Path]::GetTempPath()) ('dev3-142a-'+[Guid]::NewGuid().ToString('N')+'.sql')
 [IO.File]::WriteAllText($temp,$derived,$utf8)
 $log=Join-Path $EvidenceDirectory ([IO.Path]::GetFileNameWithoutExtension($relative)+'.log.txt')
 try{
  & sqlcmd -S localhost -E -C -b -d $database -i $temp -o $log
  $code=$LASTEXITCODE
  $results.Add([ordered]@{source=$relative;sourceSha256=(Get-FileHash $source -Algorithm SHA256).Hash;
    derivedSha256=(Get-FileHash $temp -Algorithm SHA256).Hash;exitCode=$code;log=[IO.Path]::GetFileName($log)})
  [IO.File]::WriteAllText($receipt,($results|ConvertTo-Json -Depth 5),$utf8)
  if($code -ne 0){throw "Candidate schema failed: $relative; transaction rolled back, earlier steps retained"}
 }finally{Remove-Item -LiteralPath $temp}
}
'Candidate schema installed and checked; production writer admission remains unchanged.'
