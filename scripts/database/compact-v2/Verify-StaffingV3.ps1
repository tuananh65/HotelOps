param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV3202611221630')][string]$Database='HotelOpsCompactDB')
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database
$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandTimeout=120
 $cmd.CommandText=@'
SELECT DB_NAME() AS database_name,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS sql_instance,CONVERT(varchar(33),SYSUTCDATETIME(),126)+'Z' AS checked_at,
 (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS tables,(SELECT COUNT(*) FROM sys.foreign_keys) AS foreign_keys,
 (SELECT COUNT(*) FROM users) AS users,(SELECT COUNT(*) FROM users WHERE account_status='ACTIVE') AS active_accounts,(SELECT COUNT(*) FROM users WHERE account_status='INACTIVE') AS inactive_accounts,
 (SELECT COUNT(*) FROM user_roles) AS role_assignments,(SELECT COUNT(*) FROM user_roles WHERE revoked_at IS NOT NULL) AS revoked_role_assignments,(SELECT COUNT(*) FROM memberships) AS memberships,
 (SELECT COUNT(*) FROM roles WHERE is_active=1) AS active_roles,(SELECT COUNT(*) FROM permissions) AS permissions,(SELECT COUNT(*) FROM role_permissions) AS role_permissions,
 (SELECT COUNT(*) FROM workday_assignments) AS workday_assignments,
 (SELECT COUNT(*) FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.revoked_at IS NULL AND r.is_active=0) AS unrevoked_inactive_roles,
 (SELECT COUNT(*) FROM (SELECT employee_code FROM users GROUP BY employee_code HAVING COUNT(*)>1) d) AS duplicate_employee_codes,
 (SELECT COUNT(*) FROM (SELECT username FROM users GROUP BY username HAVING COUNT(*)>1) d) AS duplicate_usernames,
 (SELECT COUNT(*) FROM (SELECT email FROM users WHERE email IS NOT NULL GROUP BY email HAVING COUNT(*)>1) d) AS duplicate_emails,
 (SELECT COUNT(*) FROM users WHERE user_id NOT BETWEEN 424 AND 438 AND(password_hash IS NOT NULL OR account_status<>'INACTIVE' OR employment_status<>'ACTIVE')) AS incorrectly_provisioned,
 (SELECT COUNT(*) FROM users u WHERE u.user_id NOT BETWEEN 424 AND 438 AND ((SELECT COUNT(*) FROM memberships m WHERE m.user_id=u.user_id AND m.kind='BRANCH_ACCESS' AND m.valid_to IS NULL AND m.is_primary=1)<>1 OR (SELECT COUNT(*) FROM memberships m WHERE m.user_id=u.user_id AND m.kind='EMPLOYMENT' AND m.valid_to IS NULL)<>1 OR (SELECT COUNT(*) FROM user_roles ur WHERE ur.user_id=u.user_id AND ur.revoked_at IS NULL)<>1)) AS missing_new_links,
 (SELECT COUNT(*) FROM users u JOIN memberships m ON m.user_id=u.user_id JOIN user_roles ur ON ur.user_id=u.user_id AND ur.revoked_at IS NULL WHERE u.user_id NOT BETWEEN 424 AND 438 AND m.branch_id<>ur.branch_id) AS cross_branch_new_links,
 (SELECT COUNT(*) FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE ur.user_id=438 AND r.code='OWNER' AND ur.branch_id IS NULL AND ur.revoked_at IS NULL) AS owner_global_assignment,
 (SELECT COUNT(*) FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id WHERE r.code='OWNER' AND rp.data_scope='ALL') AS owner_all_grants,
 (SELECT u.user_id,u.employee_code,u.username,u.full_name,u.email,u.account_status,u.employment_status,r.code AS role_code,ur.branch_id FROM users u JOIN user_roles ur ON ur.user_id=u.user_id AND ur.revoked_at IS NULL JOIN roles r ON r.role_id=ur.role_id ORDER BY u.user_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS roster,
 (SELECT b.code AS branch_code,r.code AS role_code,COUNT(*) AS staff_count,SUM(CASE WHEN u.account_status='ACTIVE' THEN 1 ELSE 0 END) AS active_count FROM users u JOIN user_roles ur ON ur.user_id=u.user_id AND ur.revoked_at IS NULL JOIN roles r ON r.role_id=ur.role_id JOIN branches b ON b.branch_id=ur.branch_id GROUP BY b.code,r.code ORDER BY b.code,r.code FOR JSON PATH) AS staffing,
 (SELECT audit_id,actor_user_id,CONVERT(varchar(36),operation_key) AS batch_id,occurred_at,JSON_VALUE(after_json,'$.validation') AS validation FROM audit_logs WHERE entity_type='StaffingV3' AND action='APPLY' FOR JSON PATH) AS batch_audit
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER
'@
 $r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$sb=New-Object Text.StringBuilder;while($r.ReadAsync().GetAwaiter().GetResult()){[void]$sb.Append($r.GetString(0))};$r.Dispose();$result=$sb.ToString() | ConvertFrom-Json
 $expected=@{tables=40;foreign_keys=160;users=54;active_accounts=15;inactive_accounts=39;role_assignments=57;revoked_role_assignments=3;memberships=110;active_roles=10;permissions=67;role_permissions=178;unrevoked_inactive_roles=0;duplicate_employee_codes=0;duplicate_usernames=0;duplicate_emails=0;incorrectly_provisioned=0;missing_new_links=0;cross_branch_new_links=0;owner_global_assignment=1;owner_all_grants=12}
 foreach($item in $expected.GetEnumerator()){if($result.($item.Key) -ne $item.Value){throw "Validation failed: $($item.Key)"}}
 if($result.batch_audit.Count -ne 1 -or [Guid]$result.batch_audit[0].batch_id -ne [Guid]'b5d5df03-3e1c-40f4-b60a-6ae335231630'){throw 'Invalid batch audit.'}
 $manifest=Get-Content -LiteralPath (Join-Path $root 'docs/database/compact-v2/staffing-v3-dry-run/staffing-v3-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 foreach($target in @($manifest.existing_users | ForEach-Object {$_.identity_after})+@($manifest.new_users)){
  $match=@($result.roster | Where-Object {$_.employee_code -ceq $target.employee_code})
  if($match.Count -ne 1 -or $match[0].username -cne $target.username -or $match[0].full_name -cne $target.full_name -or $match[0].email -cne $target.email){throw 'Roster identity mismatch.'}
 }
 $cmd.CommandText='DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS';$r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$violations=0
 do{while($r.ReadAsync().GetAwaiter().GetResult()){$violations++}}while($r.NextResultAsync().GetAwaiter().GetResult());$r.Dispose();if($violations){throw "Constraint violations: $violations"}
 $result | Add-Member -NotePropertyName constraint_violations -NotePropertyValue $violations
 $result | Add-Member -NotePropertyName result -NotePropertyValue 'PASS'
 [IO.File]::WriteAllText((Join-Path $root "artifacts/staffing-v3-implementation/validation-$Database.json"),($result | ConvertTo-Json -Depth 15),(New-Object Text.UTF8Encoding($false)))
 $result | Select-Object * -ExcludeProperty roster,staffing | ConvertTo-Json -Depth 8
}finally{$c.Dispose()}
