param([switch]$DefinitionsOnly)
$ErrorActionPreference='Stop'
$package=Join-Path $PSScriptRoot '../Task03A'
$state=Get-Content -Raw -Encoding UTF8 (Join-Path $package 'deployment-state.json') | ConvertFrom-Json
$started=[DateTime]::UtcNow.ToString('o')
# Import only reviewed helper declarations; never execute Task03A orchestration.
$tokens=$null;$parseErrors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $package 'Prepare-Task03A.ps1'),[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw 'Task03A helper parse failed'}
$names=@('Open-Db','Query','Execute','Literal','Identifier','Snapshot','Save-Json','Identity','SchemaHashes','Rows','Print-Results','Assert-Same','Assert-Schema')
foreach($fn in $ast.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst]},$false)){
    if($fn.Name -in $names){. ([scriptblock]::Create($fn.Extent.Text))}
}
$cfg=Get-Content -Raw -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') | ConvertFrom-Json
$connection=$cfg.'ConnectionStrings:HotelOpsDb'
if(!$connection){$connection=$cfg.ConnectionStrings.HotelOpsDb}
if($env:ConnectionStrings__HotelOpsDb){$connection=$env:ConnectionStrings__HotelOpsDb}
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connection
if($builder.InitialCatalog -cne 'HotelOpsDB'){throw 'Configured database differs from approved target'}
$builder['Connect Timeout']=10
$builder['Application Name']='HotelOps Task03B ReadOnly Preflight'
if($DefinitionsOnly){return}
$processes=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -match 'dotnet|HotelOps|iisexpress|w3wp|sqlagent'} | ForEach-Object {
    [ordered]@{pid=$_.ProcessId;parentPid=$_.ParentProcessId;name=$_.Name;isHotelOps=($_.CommandLine -match 'HotelOps');isLanguageServer=($_.CommandLine -match 'LanguageServer|Roslyn|ServiceHub');isWatch=($_.CommandLine -match '\bwatch\b')}
})
$tasks=@(Get-ScheduledTask | Where-Object {($_.Actions.Execute -join ' ') -match 'dotnet|HotelOps|sqlcmd|powershell|pwsh' -or ($_.TaskName -match 'HotelOps|SQL')} | Select-Object TaskName,TaskPath,State)
$services=@(Get-Service | Where-Object {$_.Name -match 'SQLAgent|W3SVC|WAS|HotelOps'} | Select-Object Name,@{n='status';e={$_.Status.ToString()}})
$c=Open-Db 'HotelOpsDB'
try {
    $id=Identity $c
    foreach($name in @('server','database','databaseId','createStamp')){if($id[$name] -cne $state.sourceIdentity.$name){throw "Identity mismatch: $name"}}
    $engine=Rows $c "SELECT @@SERVERNAME instance_name,DB_NAME() database_name,state_desc,recovery_model_desc,user_access_desc,IS_SRVROLEMEMBER('sysadmin') is_sysadmin,HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER STATE') view_server_state FROM sys.databases WHERE database_id=DB_ID()"
    $sessions=Rows $c @'
SELECT s.session_id,s.host_name,s.program_name,s.status,s.database_id,DB_NAME(s.database_id) database_name,s.open_transaction_count,
       r.command,r.status request_status,r.database_id request_database_id,r.blocking_session_id,r.writes request_writes,
       CASE WHEN EXISTS(SELECT 1 FROM sys.dm_tran_session_transactions st JOIN sys.dm_tran_database_transactions dt ON dt.transaction_id=st.transaction_id WHERE st.session_id=s.session_id AND dt.database_id=DB_ID()) THEN 1 ELSE 0 END has_target_transaction
FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id=s.session_id
WHERE s.is_user_process=1 AND s.session_id<>@@SPID ORDER BY s.session_id
'@
    $jobs=Rows $c 'SELECT enabled,COUNT(*) job_count FROM msdb.dbo.sysjobs GROUP BY enabled'
    $before=Snapshot $c
    $schema=SchemaHashes $c
    $drift=@(foreach($p in $state.sourceBefore.PSObject.Properties){if(!$before.Contains($p.Name) -or $before[$p.Name].hash -cne $p.Value.hash -or $before[$p.Name].count -ne $p.Value.count){[ordered]@{table=$p.Name;expectedCount=$p.Value.count;actual=$before[$p.Name]}}})
    $schemaDrift=@(foreach($p in $state.sourceSchema.PSObject.Properties){if($schema[$p.Name] -cne $p.Value){$p.Name}})
    $fileDrift=@(foreach($p in $state.protectedFiles.PSObject.Properties){if((Get-FileHash -LiteralPath $p.Name).Hash -cne $p.Value){$p.Name}})
    $counts=Rows $c "SELECT (SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) active_roles,(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) grants,(SELECT COUNT(*) FROM dbo.role_permissions WHERE data_scope='DEPARTMENT') department_grants,(SELECT COUNT(*) FROM dbo.user_sessions) sessions,(SELECT COUNT(*) FROM dbo.audit_logs) audits"
    $personnel=Rows $c "SELECT ur.user_id,r.role_code,ur.branch_id,ur.is_active FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id ORDER BY ur.user_role_id"
    $report=[ordered]@{startedUtc=$started;finishedUtc=[DateTime]::UtcNow.ToString('o');identity=$id;engine=$engine;processes=$processes;scheduledTasks=$tasks;services=$services;otherSqlSessions=$sessions;sqlJobs=$jobs;counts=$counts;personnel=$personnel;tableCount=$before.Count;dataDrift=$drift;schemaDrift=$schemaDrift;protectedFileDrift=$fileDrift;candidateSha256=(Get-FileHash (Join-Path $package 'REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql')).Hash;sourceSnapshot=$before;sourceSchema=$schema;candidateExecuted=$false;backupExecuted=$false}
    Save-Json $report (Join-Path $PSScriptRoot 'preflight-evidence.json')
    $report.Remove('sourceSnapshot');$report.Remove('sourceSchema')
    ConvertTo-Json -InputObject $report -Depth 8
} finally {$c.Dispose()}
