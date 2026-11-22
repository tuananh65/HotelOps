param(
 [ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV3202611221630')][string]$Database='HotelOpsCompactDB',
 [ValidateSet('PREFLIGHT','APPLY','FAIL_BEFORE_COMMIT')][string]$Mode='PREFLIGHT',
 [Guid]$BatchId='b5d5df03-3e1c-40f4-b60a-6ae335231630'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=Join-Path $root 'artifacts/staffing-v3-implementation';[void](New-Item -ItemType Directory -Force $out)
$manifest=Get-Content -LiteralPath (Join-Path $root 'docs/database/compact-v2/staffing-v3-dry-run/staffing-v3-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($source in $manifest.source_files){
 $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path 'C:/Users/Hax/Downloads' $source.file)).Hash
 if($hash -ne $source.sha256){throw 'Approved source file hash changed.'}
}
$targets=@(foreach($old in $manifest.existing_users){
 $branch=if($old.role_before.branch_id){($manifest.new_users | Where-Object {$_.branch_id -eq $old.role_before.branch_id} | Select-Object -First 1).branch_code}else{$null}
 $department=($old.memberships_preserve | Where-Object {$_.kind -eq 'EMPLOYMENT' -and -not $_.valid_to_utc} | Select-Object -First 1).department_code
 [pscustomobject]@{user_id=$old.user_id;employee_code=$old.identity_after.employee_code;username=$old.identity_after.username;full_name=$old.identity_after.full_name;email=$old.identity_after.email;role_code=$old.role_after_proposal.role_code;branch_code=$branch;department_code=$department;old_employee_code=$old.before.employee_code;old_username=$old.before.username;old_full_name=$old.before.full_name;old_email=$old.before.email;old_role_id=$old.role_before.role_id}
})+@(foreach($new in $manifest.new_users){[pscustomobject]@{user_id=$null;employee_code=$new.employee_code;username=$new.username;full_name=$new.full_name;email=$new.email;role_code=$new.role_code;branch_code=$new.branch_code;department_code=$new.department_code}})
if($targets.Count -ne 54){throw 'Expected exactly 54 targets.'}
if($Mode -ne 'PREFLIGHT'){
 & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
 Copy-Item -LiteralPath (Join-Path $root "artifacts/workday/backup-$Database.json") -Destination (Join-Path $out "backup-$Database-$Mode.json")
}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database
$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
function ReadJson($cmd){$r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$sb=New-Object Text.StringBuilder;try{while($r.ReadAsync().GetAwaiter().GetResult()){[void]$sb.Append($r.GetString(0))}}finally{$r.Dispose()};return ($sb.ToString() | ConvertFrom-Json)}
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandTimeout=120
 $cmd.CommandText=@'
SELECT
 (SELECT user_id,CONVERT(varchar(18),CONVERT(varbinary(8),row_version),1) AS row_version FROM users ORDER BY user_id FOR JSON PATH) AS users,
 (SELECT user_role_id,CONVERT(varchar(18),CONVERT(varbinary(8),row_version),1) AS row_version FROM user_roles ORDER BY user_role_id FOR JSON PATH) AS user_roles,
 (SELECT membership_id,CONVERT(varchar(18),CONVERT(varbinary(8),row_version),1) AS row_version FROM memberships ORDER BY membership_id FOR JSON PATH) AS memberships
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER
'@
 $live=ReadJson $cmd
 $roles=(Get-Content -LiteralPath (Join-Path $root 'artifacts/staffing-v3/users-snapshot.json') -Raw -Encoding UTF8 | ConvertFrom-Json).roles
 $grants=(Get-Content -LiteralPath (Join-Path $root 'artifacts/staffing-v3/diagnostics.json') -Raw -Encoding UTF8 | ConvertFrom-Json).role_grants
 $cmd.CommandText=Get-Content -LiteralPath (Join-Path $PSScriptRoot '09_StaffingV3.sql') -Raw -Encoding UTF8
 foreach($entry in @{Targets=$targets;ExpectedUsers=$live.users;ExpectedUserRoles=$live.user_roles;ExpectedMemberships=$live.memberships;ExpectedRoles=$roles;ExpectedGrants=$grants}.GetEnumerator()){
  [void]$cmd.Parameters.Add('@'+$entry.Key,[Data.SqlDbType]::NVarChar,-1);$cmd.Parameters['@'+$entry.Key].Value=ConvertTo-Json -InputObject @($entry.Value) -Depth 20 -Compress
 }
 [void]$cmd.Parameters.AddWithValue('@Mode',$Mode);[void]$cmd.Parameters.AddWithValue('@BatchId',$BatchId);[void]$cmd.Parameters.AddWithValue('@ActorId',[long]424)
 $r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@()
 try{do{while($r.ReadAsync().GetAwaiter().GetResult()){$row=[ordered]@{};for($i=0;$i -lt $r.FieldCount;$i++){$row[$r.GetName($i)]=if($r.IsDBNull($i)){$null}else{$r.GetValue($i)}};$rows+=[pscustomobject]$row}}while($r.NextResultAsync().GetAwaiter().GetResult())}finally{$r.Dispose()}
 $file=Join-Path $out "$Database-$Mode.json"
 [IO.File]::WriteAllText($file,(ConvertTo-Json -InputObject $rows -Depth 20),(New-Object Text.UTF8Encoding($false)))
 $rows | Select-Object * -ExcludeProperty before_json | ConvertTo-Json -Depth 5
}finally{$c.Dispose()}
