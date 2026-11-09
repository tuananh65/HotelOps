function Assert-NotificationTrialManifest {
 param([Parameter(Mandatory=$true)]$Manifest,[Parameter(Mandatory=$true)][string]$Identity,
       [DateTimeOffset]$Now=[DateTimeOffset]::UtcNow)
 $m=$Manifest
 if($m.server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $m.database -cne 'HotelOps_ENVNOTIF001_Trial_20261109' -or
  $m.owner -cne 'DEV1 ENV-001' -or $m.schema_owner -cne 'DEV2' -or
  $m.producers_enabled -isnot [bool] -or $m.producers_enabled -ne $false -or
  $m.workers_enabled -isnot [bool] -or $m.workers_enabled -ne $false -or
  $Identity -cne 'DESKTOP-JI8U50T\hax'){
  throw 'Exact target, owners, disabled producers/workers and hax identity required.'
 }
 $start=[DateTimeOffset]::Parse($m.lease_start_utc)
 $end=[DateTimeOffset]::Parse($m.lease_end_utc)
 if($end-$start -ne [TimeSpan]::FromHours(24) -or $end -le $Now -or $start -gt $Now -or
  [guid]$m.lease_id -eq [guid]::Empty -or [guid]$m.database_guid -eq [guid]::Empty -or
  $m.prior_trigger_sha256 -cnotmatch '^[A-F0-9]{64}$'){
  throw 'Valid provisioned lease, GUID, token and owner-reviewed prior trigger hash required.'
 }
 return [pscustomobject]@{Start=$start;End=$end}
}

