param([switch]$ExecuteApprovedDeployment,[switch]$ResumeAfterReadOnlyHarnessFix)
$ErrorActionPreference='Stop'
if(!$ExecuteApprovedDeployment){throw 'Explicit Task03B execution switch required'}
. (Join-Path $PSScriptRoot 'Inspect-Task03B.ps1') -DefinitionsOnly
if(Test-Path (Join-Path $PSScriptRoot 'execution-evidence.json')){
    if(!$ResumeAfterReadOnlyHarnessFix){throw 'Execution receipt exists. No automatic replay/retry.'}
    $previous=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'execution-evidence.json') | ConvertFrom-Json
    if($previous.completedBatches.Count -ne 0 -or $previous.commitAcknowledged -or $null -ne $previous.backup -or $previous.transactionOnError.count -ne 0 -or $previous.error -notmatch 'object or column name is missing or empty') {throw 'Resume allowed only for the analyzed SELECT projection error before backup/upgrade'}
    foreach($p in $state.sourceBefore.PSObject.Properties){if($previous.errorSnapshot.($p.Name).hash -cne $p.Value.hash){throw 'Previous error snapshot does not match source baseline'}}
    $archive=Join-Path $PSScriptRoot 'read-only-harness-error.json'
    if(Test-Path $archive){throw 'Resume already attempted'}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'execution-evidence.json') -Destination $archive
}
$release=Join-Path $PSScriptRoot '../../../database/release'
$candidate=Join-Path $package 'REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql'
$expectedHash='C1FD3904871107F0284CDC8ADF5CFF8193972B408ECBCBC3F3EBE51AF29B3A64'
$expected=Get-Content -Raw -Encoding UTF8 (Join-Path $package 'EXPECTED_CHANGES_FOR_TASK03B.json') | ConvertFrom-Json
$receipt=[ordered]@{startedUtc=[DateTime]::UtcNow.ToString('o');status='PREFLIGHT';candidateSha256=$expectedHash;completedBatches=@();commitAcknowledged=$false;checks=@();maintenance=@();backup=$null}
function Receipt {Save-Json $receipt (Join-Path $PSScriptRoot 'execution-evidence.json')}
function Pass($message){$receipt.checks+= $message;Receipt;Write-Output ('PASS: '+$message)}
function HashRows($sql){return [string](Query $c ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',("+$sql+" FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h")).Tables[0].Rows[0].h}
function SameSet($a,$b,$label){if((@($a|Sort-Object) -join "`n") -cne (@($b|Sort-Object) -join "`n")){throw "Set mismatch: $label"}}
function Maintenance($phase){
    $processes=@(Get-CimInstance Win32_Process | Where-Object {($_.Name -match 'HotelOps|iisexpress|w3wp') -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -notmatch 'Microsoft.VisualStudio.ProjectSystem.Server.BuildHost.dll|VBCSCompiler.dll|Microsoft.CodeAnalysis.LanguageServer')})
    if($processes.Count){throw 'Application/unknown dotnet process present; writer quiescence not established'}
    $scheduled=@(Get-ScheduledTask | Where-Object {$_.State -ne 'Disabled' -and (($_.Actions.Execute -join ' ') -match 'dotnet|HotelOps|sqlcmd|powershell|pwsh' -or ($_.Actions.Arguments -join ' ') -match 'HotelOps|sqlcmd')})
    if($scheduled.Count){throw 'Potential scheduled writer present'}
    $services=@(Get-CimInstance Win32_Service | Where-Object {$_.State -eq 'Running' -and ($_.Name -match 'SQLAgent|W3SVC|HotelOps' -or $_.PathName -match 'HotelOps')})
    if($services.Count){throw 'Potential service writer present'}
    $other=Rows $c @'
SELECT s.session_id,s.program_name,s.status,s.database_id,s.open_transaction_count
FROM sys.dm_exec_sessions s WHERE s.is_user_process=1 AND s.session_id<>@@SPID
AND NOT (s.session_id IN (51,52) AND s.database_id=1 AND s.status='sleeping' AND s.open_transaction_count=0
 AND s.program_name IN ('SQLServerCEIP','Microsoft SQL Server Management Studio')
 AND NOT EXISTS(SELECT 1 FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id)
 AND NOT EXISTS(SELECT 1 FROM sys.dm_tran_session_transactions t WHERE t.session_id=s.session_id))
'@
    if($other.Count){throw 'New/active SQL session present; stop before next action'}
    $receipt.maintenance+= [ordered]@{phase=$phase;utc=[DateTime]::UtcNow.ToString('o');appWriters=0;potentialScheduledWriters=0;serviceWriters=0;unexpectedSqlSessions=0;note='Only previously observed sleeping SSMS/CEIP sessions in master allowed; no KILL or database mode change'}
    Receipt
}
function IdentityGate {
    $id=Identity $c
    foreach($n in @('server','database','databaseId','createStamp')){if($id[$n] -cne $state.sourceIdentity.$n){throw "Identity changed: $n"}}
    $health=(Query $c "SELECT state_desc,recovery_model_desc,is_read_only,IS_SRVROLEMEMBER('sysadmin') administrator FROM sys.databases WHERE database_id=DB_ID()").Tables[0].Rows[0]
    if($health.state_desc -ne 'ONLINE' -or $health.recovery_model_desc -ne 'SIMPLE' -or $health.is_read_only -or $health.administrator -ne 1){throw 'Unexpected database state or SQL permissions'}
}
function Integrity($phase){
    foreach($sql in @('DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS,NO_INFOMSGS','DBCC CHECKDB WITH NO_INFOMSGS,ALL_ERRORMSGS')){
        $ds=Query $c $sql;foreach($t in $ds.Tables){if($t.Rows.Count){throw "Integrity findings: $phase"}}
        Pass "$phase $sql"
    }
    $bad=(Query $c "SELECT name FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1 UNION ALL SELECT name FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1 UNION ALL SELECT name FROM sys.indexes WHERE is_unique=1 AND is_disabled=1").Tables[0]
    if($bad.Rows.Count){throw 'Disabled/untrusted constraint or unique index'}
    Pass "$phase trusted FK/CHECK and enabled unique indexes"
}
function GrantDiff($oldRows,$newRows){
    $aliases=@{GENERAL_MANAGER='OPERATIONS_MANAGER';HK_MANAGER='HK_SUPERVISOR';MAINT_MANAGER='MAINTENANCE_LEAD';TECHNICIAN='MAINTENANCE_STAFF';WAREHOUSE_KEEPER='STOREKEEPER'}
    $old=@{};$new=@{}
    foreach($g in $oldRows){$r=$g.role_code;if($aliases.ContainsKey($r)){$r=$aliases[$r]};$old[$r+'|'+$g.permission_code]=$g.data_scope}
    foreach($g in $newRows){$new[$g.role_code+'|'+$g.permission_code]=$g.data_scope}
    $add=@();$remove=@();$change=@()
    foreach($k in $new.Keys){if(!$old.ContainsKey($k)){$add+=$k}elseif($old[$k] -cne $new[$k]){$change+=[ordered]@{key=$k;before=$old[$k];after=$new[$k]}}}
    foreach($k in $old.Keys){if(!$new.ContainsKey($k)){$remove+=$k}}
    SameSet $add $expected.addedGrantKeys 'added grants'
    SameSet $remove $expected.removedGrantKeys 'removed grants'
    SameSet @($change|ForEach-Object {$_.key+'|'+$_.before+'|'+$_.after}) @($expected.changedScopes|ForEach-Object {$_.key+'|'+$_.before+'|'+$_.after}) 'scope changes'
    return [ordered]@{addedGrantKeys=@($add|Sort-Object);removedGrantKeys=@($remove|Sort-Object);changedScopes=$change}
}
$builder['Application Name']='HotelOps Task03B Approved Deployment'
$c=$null
try {
    Receipt
    if((git branch --show-current) -cne 'feature/BE1-authorization'){throw 'Wrong Git branch'}
    if((Get-FileHash $candidate).Hash -cne $expectedHash){throw 'Candidate digest mismatch'}
    foreach($p in $state.protectedFiles.PSObject.Properties){if((Get-FileHash -LiteralPath $p.Name).Hash -cne $p.Value){throw 'Protected file drift'}}
    $sql=Get-Content -Raw -Encoding UTF8 $candidate
    $batches=@([regex]::Split($sql,'(?im)^GO\s*\r?$')|Where-Object {$_.Trim()})
    if($batches.Count -ne 4 -or ([regex]::Matches($sql,"THROW 53005,").Count) -ne 288 -or ([regex]::Matches($sql,'    COMMIT TRANSACTION;').Count) -ne 1){throw 'Unexpected candidate batch/guard/transaction structure'}
    $c=Open-Db 'HotelOpsDB';IdentityGate;Maintenance 'before-final-preflight'
    $baseline=[ordered]@{};foreach($p in $state.sourceBefore.PSObject.Properties){$baseline[$p.Name]=$p.Value}
    $schema=[ordered]@{};foreach($p in $state.sourceSchema.PSObject.Properties){$schema[$p.Name]=$p.Value}
    $before=Snapshot $c;Assert-Same $baseline $before;Assert-Schema $schema (SchemaHashes $c)
    Pass 'Exact identity, 72 data fingerprints, 5 schema groups, 126 protected files and candidate digest'
    Integrity 'preflight'
    $grantSql='SELECT r.role_code,p.permission_code,rp.data_scope FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id ORDER BY r.role_code,p.permission_code'
    $oldGrants=Rows $c $grantSql
    $predicted=GrantDiff $oldGrants $expected.targetGrants
    Save-Json $predicted (Join-Path $PSScriptRoot 'predicted-grant-changes.json')
    Pass 'Exact predicted 18 added, 60 removed, 33 scope changes'
    # Expected projections are SELECT-only and retain every unapproved column, including credentials.
    $userColumns=(Query $c "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') ORDER BY column_id").Tables[0].Rows
    $userProjection=($userColumns|ForEach-Object {if($_.name -eq 'department_id'){'CASE WHEN user_id IN (4,19) THEN NULL ELSE department_id END AS department_id'}else{Identifier $_.name}}) -join ','
    $expectedUserHash=HashRows "SELECT $userProjection FROM dbo.users ORDER BY user_id"
    $maxAudit=[long](Query $c 'SELECT MAX(audit_id) n FROM dbo.audit_logs').Tables[0].Rows[0].n
    $oldAuditSql="SELECT * FROM dbo.audit_logs WHERE audit_id<=$maxAudit ORDER BY audit_id"
    $oldAuditHash=HashRows $oldAuditSql
    $maxUr=[long](Query $c 'SELECT MAX(user_role_id) n FROM dbo.user_roles').Tables[0].Rows[0].n
    $maxRole=[long](Query $c 'SELECT MAX(role_id) n FROM dbo.roles').Tables[0].Rows[0].n
    $maxPermission=[long](Query $c 'SELECT MAX(permission_id) n FROM dbo.permissions').Tables[0].Rows[0].n
    $rolePreserveSql="SELECT role_id,created_at FROM dbo.roles WHERE role_id<=$maxRole ORDER BY role_id"
    $rolePreserveHash=HashRows $rolePreserveSql
    $permissionPreserveSql="SELECT permission_id,permission_code,module_code,resource_code,action_code FROM dbo.permissions WHERE permission_id<=$maxPermission ORDER BY permission_id"
    $permissionPreserveHash=HashRows $permissionPreserveSql
    $oldUr=Rows $c 'SELECT ur.*,r.role_code FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id ORDER BY user_role_id'
    $inactiveIds=@($oldUr|Where-Object {($_.role_code -eq 'DEPT_MANAGER' -and $_.user_id -in @(5,17,18,23,20,25)) -or ($_.role_code -eq 'WAREHOUSE_KEEPER' -and $_.user_id -eq 19) -or ($_.role_code -eq 'HK_INSPECTOR' -and $_.user_id -in @(8,14))}|ForEach-Object {$_.user_role_id})
    if($inactiveIds.Count -ne 9){throw 'Unexpected personnel affected row count'}
    $omIds=@($oldUr|Where-Object {$_.role_code -eq 'GENERAL_MANAGER' -and $_.user_id -eq 1}|ForEach-Object {$_.user_role_id})
    if($omIds.Count -ne 1){throw 'Unexpected OM assignment count'}
    $urColumns=(Query $c "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.user_roles') ORDER BY column_id").Tables[0].Rows
    $urProjection=($urColumns|ForEach-Object {if($_.name -eq 'is_active'){"CAST(CASE WHEN user_role_id IN ($($inactiveIds -join ',')) THEN 0 ELSE is_active END AS bit) AS is_active"}elseif($_.name -eq 'branch_id'){"CASE WHEN user_role_id=$($omIds[0]) THEN CAST(1 AS bigint) ELSE branch_id END AS branch_id"}else{Identifier $_.name}}) -join ','
    $expectedUrHash=HashRows "SELECT $urProjection FROM dbo.user_roles ORDER BY user_role_id"
    $ubaColumns=(Query $c "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.user_branch_access') AND name<>'revoked_at' ORDER BY column_id").Tables[0].Rows
    $ubaProjection=($ubaColumns|ForEach-Object {Identifier $_.name}) -join ','
    $ubaPreserveSql="SELECT $ubaProjection FROM dbo.user_branch_access ORDER BY access_id"
    $ubaPreserveHash=HashRows $ubaPreserveSql
    $ubaOtherSql="SELECT access_id,revoked_at FROM dbo.user_branch_access WHERE NOT(user_id=1 AND branch_id=2 AND access_level='MANAGER') ORDER BY access_id"
    $ubaOtherHash=HashRows $ubaOtherSql
    Save-Json ([ordered]@{sourceSnapshot=$before;sourceSchema=$schema;expectedUserHash=$expectedUserHash;expectedExistingUserRoleHash=$expectedUrHash;oldAuditMax=$maxAudit;oldAuditHash=$oldAuditHash;maxExistingUserRoleId=$maxUr}) (Join-Path $PSScriptRoot 'preservation-baseline.json')
    Maintenance 'before-final-backup'
    $backupId='RBAC03B_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N')
    $backupPath=Join-Path $state.backupPath ($backupId+'.bak')
    # Server-side filesystem check works without changing Windows ACL. Unique name; no INIT/FORMAT/REPLACE.
    $exists=(Query $c ('SELECT file_exists FROM sys.dm_os_file_exists('+(Literal $backupPath)+')')).Tables[0].Rows[0].file_exists
    if($exists -ne 0){throw 'Backup path already exists'}
    $receipt.status='BACKUP';$receipt.backup=[ordered]@{identifier=$backupId;database='HotelOpsDB';location='SQL Server configured backup directory, outside repository';verifyOnly='NOT_RUN'};Receipt
    Execute $c ('BACKUP DATABASE [HotelOpsDB] TO DISK='+(Literal $backupPath)+' WITH COPY_ONLY,CHECKSUM,NOINIT,NAME='+(Literal $backupId))
    Execute $c ('RESTORE VERIFYONLY FROM DISK='+(Literal $backupPath)+' WITH CHECKSUM')
    $meta=Rows $c ("SELECT backup_set_id,CONVERT(varchar(36),backup_set_uuid) backup_set_uuid,database_name,server_name,CONVERT(varchar(23),backup_start_date,121) start_server_local,CONVERT(varchar(23),backup_finish_date,121) finish_server_local,backup_size,compressed_backup_size,is_copy_only,has_backup_checksums,is_damaged,recovery_model,first_lsn,last_lsn FROM msdb.dbo.backupset WHERE name="+(Literal $backupId))
    if($meta.Count -ne 1 -or $meta[0].database_name -cne 'HotelOpsDB' -or !$meta[0].is_copy_only -or !$meta[0].has_backup_checksums -or $meta[0].is_damaged){throw 'Backup metadata mismatch'}
    $files=(Query $c ('RESTORE FILELISTONLY FROM DISK='+(Literal $backupPath))).Tables[0]
    $logical=@($files.Rows|ForEach-Object {[ordered]@{name=$_.LogicalName;type=$_.Type;size=$_.Size}})
    if($logical.Count -ne 2 -or @($logical|Where-Object {$_.name -eq 'HotelOpsDB' -and $_.type -eq 'D'}).Count -ne 1 -or @($logical|Where-Object {$_.name -eq 'HotelOpsDB_log' -and $_.type -eq 'L'}).Count -ne 1){throw 'Restore logical file layout differs from rehearsed procedure'}
    $receipt.backup.verifyOnly='PASS';$receipt.backup.metadata=$meta;$receipt.backup.logicalFiles=$logical
    Save-Json $receipt.backup (Join-Path $PSScriptRoot 'backup-verification.json')
    Pass 'Fresh unique COPY_ONLY CHECKSUM backup, VERIFYONLY and logical file layout'
    IdentityGate;Maintenance 'immediately-before-candidate'
    Assert-Same $baseline (Snapshot $c);Assert-Schema $schema (SchemaHashes $c)
    if((Get-FileHash $candidate).Hash -cne $expectedHash){throw 'Candidate changed after backup'}
    $receipt.status='EXECUTING';$receipt.sqlStartUtc=[string](Query $c 'SELECT CONVERT(varchar(33),SYSUTCDATETIME(),126) utc').Tables[0].Rows[0].utc;Receipt
    Execute $c "EXEC sys.sp_set_session_context @key=N'HotelOps.RbacDeploymentApproval',@value=N'Task03B:RBAC03A_20261021_24e14e196c6a'"
    for($i=0;$i -lt $batches.Count;$i++){
        Maintenance ('before-batch-'+($i+1))
        Execute $c $batches[$i]
        $receipt.completedBatches+=($i+1);Receipt
        Write-Output ('PASS: reviewed main candidate batch '+($i+1))
    }
    $receipt.commitAcknowledged=$true;$receipt.sqlFinishUtc=[string](Query $c 'SELECT CONVERT(varchar(33),SYSUTCDATETIME(),126) utc').Tables[0].Rows[0].utc
    $tx=(Query $c 'SELECT @@TRANCOUNT n,XACT_STATE() s').Tables[0].Rows[0]
    if($tx.n -ne 0 -or $tx.s -ne 0){throw 'Unexpected transaction state after candidate'}
    $receipt.status='POST_VERIFICATION';Receipt
    Execute $c "EXEC sys.sp_set_session_context @key=N'HotelOps.RbacDeploymentApproval',@value=NULL"
    $post=Query $c (Get-Content -Raw -Encoding UTF8 (Join-Path $package 'postdeployment-readonly.sql'))
    $exact=0
    foreach($table in $post.Tables){if($table.Columns.Contains('missing') -and $table.Columns.Contains('extra')){foreach($r in $table.Rows){if($r.missing -ne 0 -or $r.extra -ne 0){throw 'Bidirectional catalog mismatch'};$exact++}}}
    if($exact -ne 3){throw 'Missing catalog result sets'}
    Print-Results $post | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'postdeployment-results.txt')
    Pass 'Bidirectional exact role metadata, permission metadata and 140 grant tuples'
    $standard=Query $c (Get-Content -Raw -Encoding UTF8 (Join-Path $release '04_HotelOps_Verification_ReadOnly.sql'))
    # The standard script contains a full_name column. Exclude names from persisted output.
    foreach($table in $standard.Tables){if($table.Columns.Contains('full_name')){$table.Columns.Remove('full_name')}}
    Print-Results $standard | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'release04-results.txt')
    $after=Snapshot $c
    $allowed=@($expected.changedTables|ForEach-Object {$_.table})
    Assert-Same $baseline $after $allowed;Assert-Schema $schema (SchemaHashes $c)
    foreach($t in $expected.changedTables){if($after[$t.table].count -ne $t.afterCount){throw 'Unexpected changed-table row count'}}
    if((HashRows 'SELECT * FROM dbo.users ORDER BY user_id') -cne $expectedUserHash){throw 'Unexpected user/authentication changes'}
    if((HashRows "SELECT * FROM dbo.user_roles WHERE user_role_id<=$maxUr ORDER BY user_role_id") -cne $expectedUrHash){throw 'Unexpected existing user_role changes'}
    foreach($pair in @(@($oldAuditSql,$oldAuditHash),@($ubaPreserveSql,$ubaPreserveHash),@($ubaOtherSql,$ubaOtherHash),@($rolePreserveSql,$rolePreserveHash),@($permissionPreserveSql,$permissionPreserveHash))){if((HashRows $pair[0]) -cne $pair[1]){throw 'Protected row fields/history changed'}}
    $personnel=Rows $c "SELECT ur.user_id,r.role_code,ur.branch_id,ur.is_active FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code IN ('OPERATIONS_MANAGER','DEPT_MANAGER','STOREKEEPER','HK_INSPECTOR','HK_SUPERVISOR') ORDER BY r.role_code,ur.branch_id,ur.user_id"
    SameSet @($personnel|ForEach-Object {$_.user_id.ToString()+'|'+$_.role_code+'|'+$_.branch_id+'|'+$_.is_active}) @($expected.personnel|ForEach-Object {$_.user_id.ToString()+'|'+$_.role_code+'|'+$_.branch_id+'|'+$_.is_active}) 'personnel'
    $revoked=(Query $c ("SELECT COUNT(*) n FROM dbo.user_branch_access WHERE user_id=1 AND branch_id=2 AND access_level='MANAGER' AND revoked_at BETWEEN "+(Literal $receipt.sqlStartUtc)+' AND '+(Literal $receipt.sqlFinishUtc))).Tables[0].Rows[0].n
    if($revoked -ne 1){throw 'OM access revocation mismatch'}
    $audits=Rows $c "SELECT audit_id,entity_type,entity_id,actor_user_id,action,ISJSON(old_values) valid_old,ISJSON(new_values) valid_new FROM dbo.audit_logs WHERE audit_id>$maxAudit ORDER BY entity_type"
    SameSet @($audits|ForEach-Object {$_.entity_type}) @('roles','permissions','role_permissions','users','user_roles','user_branch_access') 'six audit entity types'
    foreach($a in $audits){if($a.actor_user_id -ne $null -or $a.action -cne 'UPDATE' -or $a.entity_id -cne 'deployment:RBAC03A_20261021_24e14e196c6a' -or $a.valid_old -ne 1 -or $a.valid_new -ne 1){throw 'Migration audit mismatch'}}
    $actual=GrantDiff $oldGrants (Rows $c $grantSql)
    $actual.personnel=$personnel;$actual.newAuditRecords=$audits
    $actual.changedTables=@(foreach($k in $before.Keys){if($before[$k].hash -cne $after[$k].hash){[ordered]@{table=$k;beforeCount=$before[$k].count;afterCount=$after[$k].count}}})
    SameSet @($actual.changedTables|ForEach-Object {$_.table}) $allowed 'changed table set'
    Save-Json $actual (Join-Path $PSScriptRoot 'actual-changeset.json')
    Save-Json ([ordered]@{snapshot=$after;schema=SchemaHashes $c}) (Join-Path $PSScriptRoot 'postdeployment-fingerprints.json')
    Pass 'Exact personnel, 7 expected changed tables, 65 preserved tables, credentials/sessions, old audit history and 6 new audits'
    Integrity 'postdeployment'
    Maintenance 'after-verification'
    foreach($p in $state.protectedFiles.PSObject.Properties){if((Get-FileHash -LiteralPath $p.Name).Hash -cne $p.Value){throw 'Protected source/config/release changed'}}
    $receipt.counts=Rows $c "SELECT (SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) active_roles,(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) grants,(SELECT COUNT(*) FROM dbo.role_permissions WHERE data_scope='DEPARTMENT') department_grants"
    $receipt.scopes=Rows $c 'SELECT data_scope,COUNT(*) grants FROM dbo.role_permissions GROUP BY data_scope ORDER BY data_scope'
    $receipt.status='SUCCESS WITH NOTES';$receipt.finishedUtc=[DateTime]::UtcNow.ToString('o');Receipt
    Write-Output 'SUCCESS WITH NOTES: main COMMIT and core verification passed; HTTP authentication integration not run; no writer processes were stopped or started.'
} catch {
    $receipt.status=if($receipt.completedBatches.Count -gt 0 -or $receipt.commitAcknowledged){'FAILED / RECOVERY REQUIRED'}else{'BLOCKED'}
    $receipt.error=$_.Exception.Message
    if($c -and $c.State -eq 'Open'){
        try{$tx=(Query $c 'SELECT @@TRANCOUNT n,XACT_STATE() s').Tables[0].Rows[0];$receipt.transactionOnError=[ordered]@{count=$tx.n;state=$tx.s};if($tx.n -gt 0){Execute $c 'ROLLBACK TRANSACTION';$receipt.rollback='Performed on own connection only'};$receipt.errorSnapshot=Snapshot $c}catch{$receipt.errorVerification='Could not complete read-only error verification; manual inspection required'}
    }
    $receipt.finishedUtc=[DateTime]::UtcNow.ToString('o');Receipt
    throw
} finally {if($c){$c.Dispose()}}
