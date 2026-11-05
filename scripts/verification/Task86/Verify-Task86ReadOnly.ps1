param([Parameter(Mandatory=$true)][string]$Receipt)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Receipt){throw 'Unused receipt required'}
$task86Target='HotelOps_Canonical_20261104_223000_c01286a1_Fresh'
$task86Connection=New-Object System.Data.SqlClient.SqlConnection "Data Source=REDACTED_LOCAL_SQL_HOST\SQLEXPRESS;Initial Catalog=$task86Target;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=HotelOps Task86 readonly oracle"
try{
 $task86Connection.Open();$task86Identity=$task86Connection.CreateCommand()
 $task86Identity.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial')"
 $task86Reader=$task86Identity.ExecuteReader()
 if(!$task86Reader.Read() -or $task86Reader.GetString(0) -cne 'REDACTED_LOCAL_SQL_HOST\SQLEXPRESS' -or $task86Reader.GetString(1) -cne $task86Target -or $task86Reader.IsDBNull(2) -or $task86Reader.GetString(2) -cne 'Task17'){throw 'Trial identity mismatch'}
 $task86Reader.Dispose();$task86Identity.Dispose()
 $task86Query=$task86Connection.CreateCommand()
 $task86Query.CommandText=@'
SELECT 'branch_rowversion_metadata' AS [name],COUNT_BIG(*) AS actual,CONVERT(bigint,1) AS expected FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.branches') AND name=N'row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0
UNION ALL SELECT 'exactly_one_branch_rowversion',COUNT_BIG(*),1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.branches') AND system_type_id=189
UNION ALL SELECT 'new_branch_code_active_timezone',COUNT_BIG(*),1 FROM dbo.branches WHERE branch_code='T86_NEW' AND is_active=1 AND timezone='Asia/Ho_Chi_Minh' AND DATALENGTH(row_version)=8
UNION ALL SELECT 'race_single_persisted_branch',COUNT_BIG(*),1 FROM dbo.branches WHERE branch_code='T86_RACE'
UNION ALL SELECT 'failed_branches_absent',COUNT_BIG(*),0 FROM dbo.branches WHERE branch_code IN('T86_FAIL_CREATE','T86_BAD','T86_CSRF')
UNION ALL SELECT 'create_audit_exact_actor_branch_json',COUNT_BIG(*),1 FROM dbo.audit_logs a JOIN dbo.branches b ON a.entity_id=CONVERT(nvarchar(30),b.branch_id) JOIN dbo.users u ON u.user_id=a.actor_user_id WHERE a.entity_type='branches' AND a.action='CREATE' AND b.branch_code='T86_NEW' AND a.branch_id=b.branch_id AND u.username='t86.admin' AND JSON_VALUE(a.new_values,'$.Code')='T86_NEW' AND a.occurred_at>=CONVERT(datetime2,'2026-11-04T15:10:46') AND a.occurred_at<=SYSUTCDATETIME()
UNION ALL SELECT 'exactly_two_committed_updates',COUNT_BIG(*),2 FROM dbo.audit_logs a JOIN dbo.branches b ON a.entity_id=CONVERT(nvarchar(30),b.branch_id) WHERE a.entity_type='branches' AND a.action='UPDATE' AND b.branch_code='T86_NEW'
UNION ALL SELECT 'update_before_after_fields_present',COUNT_BIG(*),2 FROM dbo.audit_logs a JOIN dbo.branches b ON a.entity_id=CONVERT(nvarchar(30),b.branch_id) WHERE a.entity_type='branches' AND a.action='UPDATE' AND b.branch_code='T86_NEW' AND JSON_VALUE(a.old_values,'$.Name') IS NOT NULL AND JSON_VALUE(a.new_values,'$.entity.Name') IS NOT NULL AND JSON_VALUE(a.new_values,'$.reason') IS NOT NULL AND ISJSON(a.changed_fields)=1 AND a.branch_id=b.branch_id
UNION ALL SELECT 'audit_failure_marker_absent',COUNT_BIG(*),0 FROM dbo.audit_logs WHERE entity_type='branches' AND (JSON_VALUE(new_values,'$.Code')='T86_FAIL_CREATE' OR JSON_VALUE(new_values,'$.entity.Name')='T86_FAIL_UPDATE')
UNION ALL SELECT 'trial_fault_constraint_removed',COUNT_BIG(*),0 FROM sys.check_constraints WHERE object_id=OBJECT_ID(N'dbo.CK_Task86_BranchAuditFailure')
UNION ALL SELECT 'role_count',COUNT_BIG(*),12 FROM dbo.roles
UNION ALL SELECT 'permission_count',COUNT_BIG(*),62 FROM dbo.permissions
UNION ALL SELECT 'grant_count',COUNT_BIG(*),140 FROM dbo.role_permissions
'@
 $task86Rows=@();$task86Reader=$task86Query.ExecuteReader()
 while($task86Reader.Read()){$task86Actual=$task86Reader.GetInt64(1);$task86Expected=$task86Reader.GetInt64(2);$task86Rows+=[ordered]@{name=$task86Reader.GetString(0);actual=$task86Actual;expected=$task86Expected;pass=($task86Actual -eq $task86Expected)}}
 $task86Reader.Dispose();$task86Query.Dispose()
 $task86Failures=@($task86Rows | Where-Object pass -eq $false).Count
 [ordered]@{recordedUtc=[DateTime]::UtcNow.ToString('o');target=$task86Target;scope='READ_ONLY_SQL_METADATA_AND_PERSISTED_BUSINESS_AUDIT_ORACLE';checks=$task86Rows.Count;passed=($task86Rows.Count-$task86Failures);failed=$task86Failures;cases=$task86Rows;writes=0} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $Receipt -Encoding UTF8
 Write-Output "READ_ONLY_ORACLE checks=$($task86Rows.Count) failures=$task86Failures"
 if($task86Failures){exit 1}
}finally{$task86Connection.Dispose()}
