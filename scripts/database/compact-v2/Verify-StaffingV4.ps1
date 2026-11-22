param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV4202611221659')][string]$Database='HotelOpsCompactDB',[switch]$Before)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$config=Get-Content -Raw -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database
$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandTimeout=120
 $cmd.CommandText=@'
SELECT DB_NAME() database_name,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) sql_instance,SYSUTCDATETIME() checked_at,
 (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys) foreign_keys,
 (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) invalid_foreign_keys,
 (SELECT COUNT(*) FROM users) users,(SELECT COUNT(*) FROM users WHERE account_status='ACTIVE') active_accounts,(SELECT COUNT(*) FROM users WHERE account_status='INACTIVE') inactive_accounts,
 (SELECT COUNT(*) FROM users WHERE password_hash IS NOT NULL) credentialed_users,
 (SELECT COUNT(*) FROM users WHERE temporary_password_expires_at IS NOT NULL) restricted_users,
 (SELECT COUNT(*) FROM users WHERE locked_until>SYSUTCDATETIME()) locked_users,
 (SELECT COUNT(*) FROM roles WHERE is_active=1) active_roles,(SELECT COUNT(*) FROM permissions) permissions,(SELECT COUNT(*) FROM role_permissions) role_permissions,
 (SELECT COUNT(*) FROM user_roles) role_assignments,(SELECT COUNT(*) FROM memberships) memberships,
 (SELECT COUNT(*) FROM workday_assignments) workday_assignments,
 (SELECT COUNT(*) FROM user_sessions WHERE revoked_at IS NULL AND expires_at>SYSUTCDATETIME()) active_sessions,
 (SELECT COUNT(*) FROM user_sessions s JOIN users u ON u.user_id=s.user_id WHERE s.revoked_at IS NULL AND s.security_stamp<>u.security_stamp) stale_unrevoked_sessions,
 (SELECT COUNT(*) FROM (SELECT employee_code FROM users GROUP BY employee_code HAVING COUNT(*)>1)d) duplicate_employee_codes,
 (SELECT COUNT(*) FROM (SELECT username FROM users GROUP BY username HAVING COUNT(*)>1)d) duplicate_usernames,
 (SELECT COUNT(*) FROM (SELECT email FROM users WHERE email IS NOT NULL GROUP BY email HAVING COUNT(*)>1)d) duplicate_emails,
 (SELECT COUNT(*) FROM users u WHERE (SELECT COUNT(*) FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.user_id=u.user_id AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND r.is_active=1)<>1) invalid_role_accounts,
 (SELECT COUNT(*) FROM user_roles ur WHERE ur.revoked_at IS NULL AND ur.branch_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM memberships m WHERE m.user_id=ur.user_id AND m.branch_id=ur.branch_id AND m.kind='BRANCH_ACCESS' AND m.valid_from<=SYSUTCDATETIME() AND (m.valid_to IS NULL OR m.valid_to>SYSUTCDATETIME()))) invalid_branch_assignments,
 (SELECT u.user_id,u.employee_code,u.username,u.full_name,u.email,u.account_status,u.employment_status,r.code role_code,ur.branch_id FROM users u JOIN user_roles ur ON ur.user_id=u.user_id AND ur.revoked_at IS NULL JOIN roles r ON r.role_id=ur.role_id ORDER BY u.user_id FOR JSON PATH,INCLUDE_NULL_VALUES) roster,
 (SELECT r.role_id,r.code role_code,p.code permission_code,p.is_active permission_active,r.is_active role_active,rp.data_scope FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id ORDER BY r.role_id,p.code FOR JSON PATH) grants,
 (SELECT m.membership_id,m.user_id,b.code branch_code,d.code department_code,m.kind,m.valid_from,m.valid_to FROM memberships m JOIN branches b ON b.branch_id=m.branch_id LEFT JOIN departments d ON d.department_id=m.department_id WHERE m.user_id IN(SELECT user_id FROM users WHERE employee_code IN('DH002','EMP050')) FOR JSON PATH,INCLUDE_NULL_VALUES) new_memberships,
 (SELECT audit_id,actor_user_id,CONVERT(varchar(36),operation_key) batch_id,action,occurred_at,after_json FROM audit_logs WHERE entity_type='StaffingV4' ORDER BY audit_id FOR JSON PATH) batch_audit
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
'@
 $r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$sb=New-Object Text.StringBuilder;try{while($r.ReadAsync().GetAwaiter().GetResult()){[void]$sb.Append($r.GetString(0))}}finally{$r.Dispose()};$result=$sb.ToString() | ConvertFrom-Json
 $expected=@{tables=40;foreign_keys=160;invalid_foreign_keys=0;active_roles=10;permissions=67;role_permissions=178;duplicate_employee_codes=0;duplicate_usernames=0;duplicate_emails=0;invalid_role_accounts=0;invalid_branch_assignments=0}
 if($Before){$expected.users=54;$expected.active_accounts=15;$expected.inactive_accounts=39;$expected.credentialed_users=15;$expected.role_assignments=57;$expected.memberships=110}
 else{$expected.users=56;$expected.active_accounts=56;$expected.inactive_accounts=0;$expected.credentialed_users=56;$expected.role_assignments=59;$expected.memberships=114;$expected.restricted_users=0;$expected.locked_users=0;if($Database -eq 'HotelOpsCompactDB'){$expected.stale_unrevoked_sessions=0}}
 foreach($entry in $expected.GetEnumerator()){if($result.($entry.Key) -ne $entry.Value){throw "Validation failed: $($entry.Key)"}}
 $old=(Get-Content -Raw -Encoding UTF8 (Join-Path $root 'artifacts/staffing-v3-implementation/validation-HotelOpsCompactDB.json') | ConvertFrom-Json).roster
 foreach($u in $old){$n=@($result.roster | Where-Object user_id -eq $u.user_id);if($n.Count -ne 1){throw 'Missing existing user'};foreach($field in @('employee_code','username','full_name','email','role_code','branch_id')){if($n[0].$field -cne $u.$field){throw "Existing user drift: $($u.user_id) $field"}}}
 $original=(Get-Content -Raw -Encoding UTF8 (Join-Path $root 'artifacts/staffing-v3/diagnostics.json') | ConvertFrom-Json).role_grants
 function GrantKey($g){"$($g.role_id)|$($g.role_code)|$($g.permission_code)|$($g.permission_active)|$($g.role_active)|$($g.data_scope)"}
 if(Compare-Object @($original | ForEach-Object {GrantKey $_}) @($result.grants | ForEach-Object {GrantKey $_})){throw 'Grant mapping changed.'}
 if(-not $Before){
  if($result.batch_audit.Count -ne 3){throw 'Expected three V4 audit stages.'}
  foreach($a in $result.batch_audit){if([Guid]$a.batch_id -ne [Guid]'c7046ec2-244c-4eaa-8799-b9e946422659'){throw 'Unexpected V4 batch identifier.'}}
  foreach($code in @('DH002','EMP050')){$u=$result.roster | Where-Object employee_code -eq $code;if($u.branch_id -ne 414){throw 'New account branch mismatch'};if(@($result.new_memberships | Where-Object user_id -eq $u.user_id).Count -ne 2){throw 'New memberships mismatch'}}
 }
 $cmd.CommandText='DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS';$r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$violations=0;try{do{while($r.ReadAsync().GetAwaiter().GetResult()){$violations++}}while($r.NextResultAsync().GetAwaiter().GetResult())}finally{$r.Dispose()};if($violations){throw 'Constraint violations found.'}
 $result | Add-Member constraint_violations $violations;$result | Add-Member result 'PASS'
 $phase=if($Before){'before'}else{'after'}
 [IO.File]::WriteAllText((Join-Path $root "artifacts/staffing-v4/validation-$Database-$phase.json"),($result | ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
 $result | Select-Object * -ExcludeProperty roster,grants,new_memberships,batch_audit | ConvertTo-Json
}finally{$c.Dispose()}
