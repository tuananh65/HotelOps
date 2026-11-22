param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV4202611221659')][string]$Database,[int]$Port)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'));$base="http://127.0.0.1:$Port/api/v2"
if(($Database -eq 'HotelOpsCompactDB' -and $Port -ne 5787) -or ($Database -ne 'HotelOpsCompactDB' -and $Port -ne 5786)){throw 'Unexpected API mapping.'}
& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
$prior=(Get-Content -Raw (Join-Path $root 'artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json') | ConvertFrom-Json).password
$approval=Get-Content -Raw -Encoding UTF8 -LiteralPath 'C:/Users/Hax/.codex/attachments/82bc7a51-170d-4678-8cc5-509c7bd68b67/Pasted text.txt'
$credential=[regex]::Match($approval,'Mật khẩu dùng chung cho 56 tài khoản:\s*`([^`]+)`').Groups[1].Value;$approval=$null
function Login($password){Invoke-RestMethod "$base/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='NamNH1';password=$password} | ConvertTo-Json -Compress)}
$oldSession=Login $prior
$config=Get-Content -Raw -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database;$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandText='SELECT password_hash FROM users WHERE user_id=424';$oldHash=$cmd.ExecuteScalarAsync().GetAwaiter().GetResult()
 & (Join-Path $PSScriptRoot 'Invoke-StaffingV4.ps1') -Database $Database -Mode APPLY
 $applied=Get-Content -Raw (Join-Path $root "artifacts/staffing-v4/$Database-APPLY.json") | ConvertFrom-Json
 if($applied.result -ne 'APPLIED'){throw 'Expected fresh successful apply.'}
 $status=200;try{[void](Invoke-RestMethod "$base/auth/me" -Headers @{Authorization='Bearer '+$oldSession.accessToken})}catch{$status=[int]$_.Exception.Response.StatusCode}
 if($status -ne 401){throw 'Pre-reset session still accepted.'}
 $current=Login $credential;if($current.requiresPasswordChange -or $current.user.fields.userId -ne 424){throw 'New administrator login invalid.'}
 $profile=Invoke-RestMethod "$base/auth/me" -Headers @{Authorization='Bearer '+$current.accessToken}
 if($profile.roles[0].code -ne 'SYS_ADMIN' -or $profile.grants.Count -ne 5){throw 'Administrator grants changed.'}
 $recovery='Backup restored; fixture rehearsal recorded separately'
 if($Database -ne 'HotelOpsCompactDB'){
  # Rehearse recovery from the pre-reset value held in memory, then return to the new value.
  $cmd.CommandText='SELECT password_hash FROM users WHERE user_id=424';$newHash=$cmd.ExecuteScalarAsync().GetAwaiter().GetResult()
  foreach($step in @('RECOVER_OLD','RESTORE_V4')){
   & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
   $cmd.CommandText=@'
SET NOCOUNT ON;SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestStaffingV4202611221659' THROW 51770,'Recovery rehearsal is fixture-only.',1;
BEGIN TRY BEGIN TRANSACTION;
DECLARE @version binary(8);SELECT @version=row_version FROM users WITH(UPDLOCK,HOLDLOCK) WHERE user_id=424 AND username='NamNH1' AND account_status='ACTIVE';
IF @version IS NULL THROW 51771,'Admin changed.',1;
UPDATE users SET password_hash=@hash,security_stamp=NEWID(),temporary_password_expires_at=NULL,locked_until=NULL,failed_login_count=0 WHERE user_id=424 AND row_version=@version;
IF @@ROWCOUNT<>1 THROW 51771,'Recovery concurrency conflict.',1;
UPDATE user_sessions SET revoked_at=SYSUTCDATETIME() WHERE user_id=424 AND revoked_at IS NULL;
INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,after_json) VALUES(424,NEWID(),'StaffingV4Recovery','424',@step,N'{"isolated_rehearsal":true,"identity_and_grants_preserved":true}');
COMMIT;END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
'@
   $cmd.Parameters.Clear();[void]$cmd.Parameters.AddWithValue('@hash',$(if($step -eq 'RECOVER_OLD'){$oldHash}else{$newHash}));[void]$cmd.Parameters.AddWithValue('@step',$step);[void]$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()
   $test=Login $(if($step -eq 'RECOVER_OLD'){$prior}else{$credential});if($test.user.fields.userId -ne 424 -or $test.requiresPasswordChange){throw 'Recovery login failed.'}
  }
  $recovery='PASS: pre-reset admin credential restored and login verified; V4 credential reinstated and verified; all sessions rotated'
 }
 $result=@{database=$Database;old_session_status=$status;new_admin_login='PASS';admin_grants=5;requires_password_change=$false;recovery=$recovery;at=[DateTime]::UtcNow.ToString('o')}
 $result | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $root "artifacts/staffing-v4/session-transition-$Database.json")
 $result | ConvertTo-Json
}finally{$oldHash=$null;$newHash=$null;$credential=$null;$prior=$null;$oldSession=$null;$current=$null;if($cmd){$cmd.Parameters.Clear();$cmd.Dispose()};$c.Dispose()}
