$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Safety.ps1')
$now=[DateTimeOffset]::Parse('2026-11-09T12:00:00Z')
function Fixture {
 return [pscustomobject]@{server='DESKTOP-JI8U50T\SQLEXPRESS';database='HotelOps_ENVNOTIF001_Trial_20261109';
 owner='DEV1 ENV-001';schema_owner='DEV2';producers_enabled=$false;workers_enabled=$false;
 lease_start_utc='2026-11-09T11:00:00Z';lease_end_utc='2026-11-10T11:00:00Z';
 lease_id='3e67f753-4171-4846-b94d-621368b6a806';database_guid='a138fa48-cc14-4070-9ca4-793f1b60c5c1';
 prior_trigger_sha256=('A'*64)}
}
$null=Assert-NotificationTrialManifest (Fixture) 'DESKTOP-JI8U50T\hax' $now
$count=1
$cases=@(
 @('server','KIENPC'),@('database','HotelOpsDB'),@('database','HotelOps_ENV001_FE04_Trial_20261108'),
 @('owner','unknown'),@('schema_owner','DEV1'),@('producers_enabled',$true),@('workers_enabled',$true),
 @('producers_enabled','false'),@('lease_id',[guid]::Empty.ToString()),@('database_guid',[guid]::Empty.ToString()),
 @('prior_trigger_sha256','tampered'),@('lease_start_utc','2026-11-09T13:00:00Z'),
 @('lease_end_utc','2026-11-09T11:00:00Z'),@('lease_end_utc','2026-11-10T12:00:00Z')
)
foreach($case in $cases){
 $m=Fixture;$m.($case[0])=$case[1];$rejected=$false
 try{$null=Assert-NotificationTrialManifest $m 'DESKTOP-JI8U50T\hax' $now}catch{$rejected=$true}
 if(!$rejected){throw ('Unsafe manifest accepted: '+$case[0])};$count++
}
foreach($identity in @('DESKTOP-JI8U50T\CodexSandboxOffline','OTHER\hax')){
 $rejected=$false;try{$null=Assert-NotificationTrialManifest (Fixture) $identity $now}catch{$rejected=$true}
 if(!$rejected){throw 'Wrong Windows identity accepted'};$count++
}
$rejected=$false;try{$null=Assert-NotificationTrialManifest (Fixture) 'DESKTOP-JI8U50T\hax' ([DateTimeOffset]::Parse('2026-11-10T11:00:00Z'))}catch{$rejected=$true}
if(!$rejected){throw 'Lease-end boundary accepted'};$count++
foreach($f in Get-ChildItem $PSScriptRoot -Filter '*.ps1'){
 $tokens=$null;$errors=$null
 $null=[Management.Automation.Language.Parser]::ParseFile($f.FullName,[ref]$tokens,[ref]$errors)
 if($errors.Count){throw ($f.Name+': '+($errors.Message -join '; '))}
}
"PASS: $count manifest safety cases; all PowerShell files parse. No SQL opened."

