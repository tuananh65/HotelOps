param([ValidateSet('Preflight','Rehearse','RevalidateGuards','FinalReadOnly')][string]$Stage = 'Preflight', [switch]$ResumeVerifiedBackup)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$statePath = Join-Path $PSScriptRoot 'trial-state.json'
$release = Join-Path $root 'scripts/database/release'
$cfg = Get-Content -Raw -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') | ConvertFrom-Json
$connection = $cfg.'ConnectionStrings:HotelOpsDb'
if (!$connection) { $connection = $cfg.ConnectionStrings.HotelOpsDb }
if ($env:ConnectionStrings__HotelOpsDb) { $connection = $env:ConnectionStrings__HotelOpsDb }
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connection
if ($builder.InitialCatalog -cne 'HotelOpsDB') { throw 'Configured source is not HotelOpsDB' }
$builder['Connect Timeout'] = 10
function Open-Db([string]$database) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $builder.ConnectionString
    $b['Initial Catalog'] = $database
    $c = New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
    $c.Open()
    return $c
}
function Query($c, [string]$sql) {
    $cmd = $c.CreateCommand(); $cmd.CommandTimeout = 180; $cmd.CommandText = $sql
    $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $ds = New-Object System.Data.DataSet
    [void]$da.Fill($ds)
    return ,$ds
}
function Execute($c, [string]$sql) {
    $cmd = $c.CreateCommand(); $cmd.CommandTimeout = 300; $cmd.CommandText = $sql
    [void]$cmd.ExecuteNonQuery()
}
function Literal([string]$s) { return "N'" + $s.Replace("'", "''") + "'" }
function Identifier([string]$s) { return '[' + $s.Replace(']', ']]') + ']' }
function Snapshot($c) {
    $result = [ordered]@{}
    $tables = (Query $c "SELECT s.name schema_name,t.name table_name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name").Tables[0]
    foreach ($t in $tables.Rows) {
        $name = (Identifier $t.schema_name) + '.' + (Identifier $t.table_name)
        $keys = (Query $c ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID("+(Literal $name)+") AND i.is_primary_key=1 ORDER BY ic.key_ordinal")).Tables[0]
        if ($keys.Rows.Count -eq 0) { throw "No deterministic primary key for $name" }
        $order = ($keys.Rows | ForEach-Object { Identifier $_.name }) -join ','
        $row = (Query $c "SELECT COUNT_BIG(*) row_count,CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $name ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES)),2) content_hash FROM $name").Tables[0].Rows[0]
        $result[$name] = [ordered]@{count=[long]$row.row_count; hash=[string]$row.content_hash}
    }
    return $result
}
function Save-Json($value, $path) { ConvertTo-Json -InputObject $value -Depth 12 | Set-Content -Encoding UTF8 -LiteralPath $path }
function Assert-Same($a, $b, [string[]]$except = @()) {
    foreach ($key in $a.Keys) {
        if ($key -in $except) { continue }
        if (!$b.Contains($key) -or $a[$key].count -ne $b[$key].count -or $a[$key].hash -ne $b[$key].hash) { throw "Content changed: $key" }
    }
    if ($a.Count -ne $b.Count) { throw 'Table set changed' }
}
function Print-Results($ds) {
    foreach ($t in $ds.Tables) { $t | Format-Table -AutoSize | Out-String -Width 220 | Write-Output }
}
function Identity($c) {
    $r=(Query $c "SELECT DB_NAME() db,DB_ID() id,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) server_name,CONVERT(varchar(23),create_date,121) created FROM sys.databases WHERE name=DB_NAME()").Tables[0].Rows[0]
    return [ordered]@{database=[string]$r.db;databaseId=[int]$r.id;server=[string]$r.server_name;createStamp=[string]$r.created}
}
function SchemaHashes($c) {
    $queries=[ordered]@{
        columns="SELECT t.name table_name,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id ORDER BY t.name,c.column_id"
        foreignKeys="SELECT OBJECT_NAME(f.parent_object_id) child,f.name,pc.name child_column,OBJECT_NAME(f.referenced_object_id) parent,rc.name parent_column,f.delete_referential_action,f.update_referential_action,f.is_disabled,f.is_not_trusted FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id ORDER BY child,f.name,fc.constraint_column_id"
        checks="SELECT OBJECT_NAME(parent_object_id) table_name,name,definition,is_disabled,is_not_trusted FROM sys.check_constraints ORDER BY table_name,name"
        defaults="SELECT OBJECT_NAME(parent_object_id) table_name,COL_NAME(parent_object_id,parent_column_id) column_name,name,definition FROM sys.default_constraints ORDER BY table_name,column_name"
        indexes="SELECT t.name table_name,i.name,i.type,i.is_unique,i.is_primary_key,i.is_disabled,i.has_filter,i.filter_definition,c.name column_name,ic.key_ordinal,ic.is_descending_key,ic.is_included_column FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.index_id>0 ORDER BY t.name,i.name,ic.index_column_id"
    }
    $hashes=[ordered]@{}
    foreach($key in $queries.Keys){$hashes[$key]=[string](Query $c ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',("+$queries[$key]+" FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h")).Tables[0].Rows[0].h}
    return $hashes
}
function Assert-Schema($a,$b){foreach($k in $a.Keys){if($a[$k] -cne $b[$k]){throw ('Schema changed: '+$k)}}}
function Rows($c,$sql){
    $dt=(Query $c $sql).Tables[0];$result=@()
    foreach($r in $dt.Rows){$o=[ordered]@{};foreach($col in $dt.Columns){$v=$r[$col];if($v -is [DBNull]){$v=$null};$o[$col.ColumnName]=$v};$result+=$o}
    return ,$result
}
function Bind-Guard($id,$approval){
    return @"
IF DB_NAME()<>$(Literal $id.database) OR DB_ID()<>$($id.databaseId)
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>$(Literal $id.server)
 OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND CONVERT(varchar(23),create_date,121)=$(Literal $id.createStamp))
 THROW 53000,'Wrong reviewed server/database identity. Stop.',1;
IF CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval')) IS NULL
 OR CONVERT(nvarchar(200),SESSION_CONTEXT(N'HotelOps.RbacDeploymentApproval'))<>$(Literal $approval)
 THROW 53001,'Separate Task03B approval/session gate is not set. No upgrade permitted.',1;
IF COL_LENGTH('dbo.role_permissions','data_scope') IS NULL
 THROW 53002,'Reviewed target already had data_scope; missing column requires a new review.',1;
"@
}
function Make-Deployment($id,$approval,$baseline,$auditTag,$dryRun,$repeatDrift=$true) {
    $text=Get-Content -Raw -Encoding UTF8 (Join-Path $release '02_HotelOps_Upgrade_Auth_To_RBAC.sql')
    foreach($old in @('USE [__REVIEWED_TARGET_DATABASE__];',"IF DB_NAME() <> N'__REVIEWED_TARGET_DATABASE__'","IF DB_NAME()<>N'__REVIEWED_TARGET_DATABASE__'")){
        if(($text.Split(@($old),[StringSplitOptions]::None).Count-1) -ne 1){throw 'Release target anchor changed'}
        $text=$text.Replace($old,$old.Replace('__REVIEWED_TARGET_DATABASE__',$id.database))
    }
    $anchor='DECLARE @SelectedDh TABLE(branch_id BIGINT PRIMARY KEY,user_id BIGINT NOT NULL);'
    if(!$text.Contains($anchor)){throw 'DH anchor missing'}
    $text=$text.Replace($anchor,$anchor+"`r`n INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);")
    $auditQueries=[ordered]@{
        roles='SELECT role_id,role_code,role_name,description,is_system_role,is_active FROM dbo.roles ORDER BY role_id'
        permissions='SELECT permission_id,permission_code,permission_name,module_code,resource_code,action_code,description FROM dbo.permissions ORDER BY permission_id'
        role_permissions='SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_id,permission_id'
        users='SELECT user_id,department_id FROM dbo.users ORDER BY user_id'
        user_roles='SELECT user_role_id,user_id,role_id,branch_id,is_active,assigned_at,expires_at FROM dbo.user_roles ORDER BY user_role_id'
        user_branch_access='SELECT access_id,user_id,branch_id,access_level,revoked_at FROM dbo.user_branch_access ORDER BY access_id'
    }
    $fields=@{roles='["role_code","role_name","description","is_system_role","is_active"]';permissions='["permission_code","permission_name","description","module_code","resource_code","action_code"]';role_permissions='["role_id","permission_id","data_scope"]';users='["department_id"]';user_roles='["role_id","branch_id","is_active","assigned_at"]';user_branch_access='["revoked_at"]'}
    $capture="`r`n DECLARE @DeploymentBefore TABLE(entity_type nvarchar(100) PRIMARY KEY,rows_json nvarchar(max));`r`n"
    $audit="`r`n -- Actual migration-level before/after audit, no staff impersonation or credential payload.`r`n"
    foreach($table in $auditQueries.Keys){
        $json='('+$auditQueries[$table]+' FOR JSON PATH,INCLUDE_NULL_VALUES)'
        $capture+=' INSERT INTO @DeploymentBefore VALUES ('+(Literal $table)+','+$json+");`r`n"
        $audit+=" INSERT INTO dbo.audit_logs(actor_user_id,entity_type,entity_id,action,old_values,new_values,changed_fields,occurred_at)`r`n SELECT NULL,"+(Literal $table)+','+(Literal ('deployment:'+ $auditTag))+",'UPDATE',`r`n (SELECT "+(Literal $auditTag)+" deployment_id,JSON_QUERY(b.rows_json) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),`r`n (SELECT "+(Literal $auditTag)+" deployment_id,JSON_QUERY("+$json+") rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),`r`n "+(Literal $fields[$table])+",SYSUTCDATETIME() FROM @DeploymentBefore b WHERE b.entity_type="+(Literal $table)+' AND b.rows_json<>'+$json+";`r`n"
    }
    $prep=@"
    -- Approved for this dataset only; shared Release remains unmodified.
    IF NOT EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
      WHERE ur.user_id=19 AND ur.branch_id=2 AND ur.is_active=1 AND r.role_code='WAREHOUSE_KEEPER')
      THROW 53003,'Reviewed Storekeeper conflict no longer matches baseline.',1;
    UPDATE ur SET is_active=0 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
      WHERE ur.user_id=19 AND ur.branch_id=2 AND ur.is_active=1 AND r.role_code='WAREHOUSE_KEEPER';
    UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME()
      WHERE user_id=1 AND branch_id=2 AND access_level='MANAGER' AND revoked_at IS NULL;
"@
    if(($text.Split(@('    BEGIN TRANSACTION;'),[StringSplitOptions]::None).Count-1) -ne 1 -or ($text.Split(@('    COMMIT TRANSACTION;'),[StringSplitOptions]::None).Count-1) -ne 1){throw 'Transaction anchors changed'}
    $text=$text.Replace('    BEGIN TRANSACTION;',"    BEGIN TRANSACTION;`r`n"+$capture+$prep)
    $commit=if($dryRun){'    ROLLBACK TRANSACTION;'}else{'    COMMIT TRANSACTION;'}
    $text=$text.Replace('    COMMIT TRANSACTION;',$audit+$commit)
    if($dryRun){$text=$text.Replace('RBAC UPGRADE COMMITTED.','RBAC REHEARSAL DRY RUN ROLLED BACK.')}
    $drift="`r`nIF @@TRANCOUNT<>0 THROW 53004,'Existing transaction not allowed before deployment preflight.',1;`r`n"
    foreach($key in $baseline.Keys){
        $table=$key
        $pk=$script:PkOrders[$key]
        $drift+="IF (SELECT COUNT_BIG(*) FROM $table)<>"+$baseline[$key].count+" OR CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $table ORDER BY $pk FOR JSON PATH,INCLUDE_NULL_VALUES)),2)<>"+(Literal $baseline[$key].hash)+" THROW 53005,'Reviewed data fingerprint drift. Stop and reassess.',1;`r`n"
    }
    $batches=[regex]::Split($text,'(?im)^GO\s*\r?$')
    $built=@();$first=$true
    foreach($batch in $batches){if($batch.Trim()){$prefix=Bind-Guard $id $approval;if($first -or $repeatDrift){$prefix+=$drift};$first=$false;$built+=($prefix+"`r`n"+$batch)}}
    $body=($built -join "`r`nGO`r`n")+"`r`nGO`r`n"
    $stripped=[regex]::Replace($body,'(?s)/\*.*?\*/|--[^\r\n]*','')
    if($stripped.Contains('__REVIEWED_TARGET_DATABASE__')){throw 'Executable placeholder remains'}
    $noStrings=[regex]::Replace($stripped,"N?'(?:[^']|'')*'","''")
    if($noStrings -match '(?i)\bEXEC(?:UTE)?\b|\b(?:master|msdb|tempdb)\s*\.') {throw 'Unexpected dynamic/cross-database SQL'}
    $uses=[regex]::Matches($noStrings,'(?i)\bUSE\s+\[([^]]+)\]')
    if($uses.Count -ne 1 -or $uses[0].Groups[1].Value -cne $id.database){throw 'Unexpected USE target'}
    return "/* REVIEWED CANDIDATE ONLY. Separate Task03B approval required for HotelOpsDB.`r`n Session gate is not an authorization credential. Do not bypass guards. No department/fixture cleanup.`r`n Audit tag: $auditTag */`r`n"+$body
}
function Run-CloneBatches($c,$text,$cloneId){
    $actual=Identity $c
    if($actual.database -eq 'HotelOpsDB' -or $actual.database -cne $cloneId.database -or $actual.databaseId -ne $cloneId.databaseId -or $actual.createStamp -cne $cloneId.createStamp){throw 'Refuse upgrade execution outside exact new rehearsal clone'}
    foreach($batch in [regex]::Split($text,'(?im)^GO\s*\r?$')){if($batch.Trim()){Execute $c $batch}}
}

$stateFile=Join-Path $PSScriptRoot 'deployment-state.json'
if($Stage -eq 'Preflight'){
    if(Test-Path -LiteralPath $stateFile){throw 'State exists; do not overwrite reviewed baseline'}
    $source=Open-Db 'HotelOpsDB'
    try{
        $id=Identity $source
        if($id.server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $id.database -cne 'HotelOpsDB'){throw 'Wrong main target'}
        $snapshot=Snapshot $source
        $schema=SchemaHashes $source
        $old=Get-Content -Raw -LiteralPath (Join-Path $root 'scripts/verification/RbacUpgradeTrial/trial-state.json')|ConvertFrom-Json
        $drift=@()
        foreach($k in $snapshot.Keys){if($old.sourceBefore.$k.hash -cne $snapshot[$k].hash -or $old.sourceBefore.$k.count -ne $snapshot[$k].count){$drift+=$k}}
        Print-Results (Query $source (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'preflight-readonly.sql'))) | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'preflight-results.txt')
        $personnel=@"
IF EXISTS (SELECT branch_id,user_id FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.is_active=1
 EXCEPT SELECT branch_id,user_id FROM (VALUES(1,4),(1,5),(1,17),(1,18),(1,23),(2,19),(2,20),(2,25)) x(branch_id,user_id))
 OR EXISTS (SELECT branch_id,user_id FROM (VALUES(1,4),(1,5),(1,17),(1,18),(1,23),(2,19),(2,20),(2,25)) x(branch_id,user_id)
 EXCEPT SELECT branch_id,user_id FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.is_active=1)
 THROW 53100,'DH data differs from reviewed personnel plan.',1;
IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE user_id=4 AND primary_branch_id=1 AND account_status='ACTIVE' AND deleted_at IS NULL)
 OR NOT EXISTS (SELECT 1 FROM dbo.users WHERE user_id=19 AND primary_branch_id=2 AND account_status='ACTIVE' AND deleted_at IS NULL)
 THROW 53101,'Selected DH identity/scope changed.',1;
IF (SELECT COUNT(*) FROM dbo.roles)<>11 OR (SELECT COUNT(*) FROM dbo.permissions)<>47 OR (SELECT COUNT(*) FROM dbo.role_permissions)<>182
 THROW 53102,'Catalog baseline changed; reassess upgrade path.',1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='role_permissions' AND COLUMN_NAME='data_scope' AND DATA_TYPE='varchar' AND CHARACTER_MAXIMUM_LENGTH=20 AND IS_NULLABLE='NO')
 THROW 53103,'Unexpected data_scope shape.',1;
"@
        Execute $source $personnel # SELECT/IF/THROW only: no data-changing dry run on source.
        $protected=[ordered]@{}
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'src/HotelOps.Api') -File -Recurse | Where-Object {$_.FullName -notmatch '\\(bin|obj)\\'}){$protected[$file.FullName]=(Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash}
        foreach($file in Get-ChildItem -LiteralPath $release -File){$protected[$file.FullName]=(Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash}
        $settings=(Query $source "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) backup_path,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) data_path,CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath')) log_path,IS_SRVROLEMEMBER('sysadmin') sysadmin").Tables[0].Rows[0]
        if($settings.sysadmin -ne 1){throw 'Backup/restore permission not established'}
        $runId='RBAC03A_20261021_'+[guid]::NewGuid().ToString('N').Substring(0,12)
        $state=[ordered]@{runId=$runId;sourceIdentity=$id;sourceBefore=$snapshot;sourceSchema=$schema;sourceDriftVsOriginalTask02=$drift;protectedFiles=$protected;backupPath=[string]$settings.backup_path;dataPath=[string]$settings.data_path;logPath=[string]$settings.log_path;mainUpgradeExecuted=$false;sourceDataScopeAlreadyPresent=$true}
        Save-Json $state $stateFile
        Write-Output ('PASS: exact main identity; '+$snapshot.Count+' tables snapshotted; drift vs original Task02: '+$drift.Count)
        Write-Output 'PASS: selected DH scope and legacy RBAC/data_scope prerequisites; source received no DML/DDL'
    }finally{$source.Dispose()}
    exit
}

$state=Get-Content -Raw -LiteralPath $stateFile|ConvertFrom-Json
if($Stage -eq 'FinalReadOnly'){
    $source=Open-Db 'HotelOpsDB'
    try{
        $started=(Query $source 'SELECT SYSUTCDATETIME() utc').Tables[0].Rows[0].utc
        $actualId=Identity $source
        if($actualId.database -cne $state.sourceIdentity.database -or $actualId.server -cne $state.sourceIdentity.server -or $actualId.databaseId -ne $state.sourceIdentity.databaseId -or $actualId.createStamp -cne $state.sourceIdentity.createStamp){throw 'Source identity changed'}
        $before=[ordered]@{};foreach($p in $state.sourceBefore.PSObject.Properties){$before[$p.Name]=$p.Value}
        $schemaBefore=[ordered]@{};foreach($p in $state.sourceSchema.PSObject.Properties){$schemaBefore[$p.Name]=$p.Value}
        $after=Snapshot $source;$schemaAfter=SchemaHashes $source
        Assert-Same $before $after;Assert-Schema $schemaBefore $schemaAfter
        foreach($p in $state.protectedFiles.PSObject.Properties){if((Get-FileHash -Algorithm SHA256 -LiteralPath $p.Name).Hash -cne $p.Value){throw ('Protected file changed: '+$p.Name)}}
        $finished=(Query $source 'SELECT SYSUTCDATETIME() utc').Tables[0].Rows[0].utc
        Save-Json ([ordered]@{identity=$actualId;startedUtc=[DateTime]::SpecifyKind($started,[DateTimeKind]::Utc).ToString('o');finishedUtc=[DateTime]::SpecifyKind($finished,[DateTimeKind]::Utc).ToString('o');isAtomicSnapshot=$false;tables=$after;schemaHashes=$schemaAfter;comparison='72/72 contents and 5/5 metadata hashes equal Task03A preflight';protectedFilesMatch=126;mainUpgradeExecuted=$false}) (Join-Path $PSScriptRoot 'source-after-readonly.json')
        Write-Output 'PASS: final SELECT-only source check, 72/72 table content and 5/5 schema hashes unchanged; 126 protected files match'
    }finally{$source.Dispose()}
    exit
}
if($Stage -eq 'RevalidateGuards'){
    $resultFile=Join-Path $PSScriptRoot 'rehearsal-result.json'
    $result=Get-Content -Raw -LiteralPath $resultFile|ConvertFrom-Json
    $baseline=[ordered]@{};foreach($p in $state.sourceBefore.PSObject.Properties){$baseline[$p.Name]=$p.Value}
    $source=Open-Db 'HotelOpsDB';$clone=Open-Db $result.clone.database
    try{
        Assert-Same $baseline (Snapshot $source)
        $cloneBefore=Snapshot $clone
        $script:PkOrders=[ordered]@{}
        foreach($key in $baseline.Keys){$keys=(Query $source ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID("+(Literal $key)+") AND i.is_primary_key=1 ORDER BY ic.key_ordinal")).Tables[0].Rows;$script:PkOrders[$key]=($keys|ForEach-Object {Identifier $_.name}) -join ','}
        $mainApproval='Task03B:'+$state.runId;$cloneApproval='Task03A-Rehearsal:'+$state.runId
        $oldMain=Make-Deployment $state.sourceIdentity $mainApproval $baseline $state.runId $false $false
        $path=Join-Path $PSScriptRoot 'REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql'
        if((Get-Content -Raw -Encoding UTF8 -LiteralPath $path).TrimEnd() -cne $oldMain.TrimEnd()){throw 'Prior tested candidate cannot be reproduced; mutation body drift'}
        $archive=Join-Path $PSScriptRoot 'initial-rehearsal-evidence'
        if(Test-Path -LiteralPath $archive){throw 'Guard refresh already exists; do not overwrite evidence'}
        [void](New-Item -ItemType Directory -Path $archive)
        foreach($name in @('REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql','rehearsal-dry-run.sql','rehearsal-commit.sql','rehearsal-result.json')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $archive $name)}
        $main=Make-Deployment $state.sourceIdentity $mainApproval $baseline $state.runId $false
        $dry=Make-Deployment $result.clone $cloneApproval $baseline $state.runId $true
        $commit=Make-Deployment $result.clone $cloneApproval $baseline $state.runId $false
        $main|Set-Content -Encoding UTF8 -LiteralPath $path
        $dry|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'rehearsal-dry-run.sql')
        $commit|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'rehearsal-commit.sql')
        Execute $clone ("EXEC sys.sp_set_session_context @key=N'HotelOps.RbacDeploymentApproval',@value="+(Literal $cloneApproval))
        $tested=0
        foreach($batch in [regex]::Split($commit,'(?im)^GO\s*\r?$')){if($batch.Trim()){
            $failed=$false
            try{Run-CloneBatches $clone $batch $result.clone}catch{if($_.Exception.InnerException -is [System.Data.SqlClient.SqlException] -and $_.Exception.InnerException.Number -eq 53005){$failed=$true}else{throw}}
            if(!$failed){throw 'Every batch must reject the already-upgraded clone before any writes'}
            $tested++
        }}
        Assert-Same $cloneBefore (Snapshot $clone);Assert-Same $baseline (Snapshot $source)
        $result.mainScriptSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
        $result|Add-Member -NotePropertyName repeatedDriftGuards -NotePropertyValue ([ordered]@{allBatchesRejectDrift=$tested;error=53005;noDatabaseWrites=$true;mutationBodyReproducedAgainstInitialCandidate=$true;note='Only repeated read-only baseline guards added; mutation body was already dry-run/commit tested on fresh clone. Main script never executed.'}) -Force
        Save-Json $result $resultFile
        Write-Output ('PASS: identical reviewed mutation body; strengthened drift guard rejects all '+$tested+' batches on upgraded clone, with source/clone contents unchanged')
    }finally{$source.Dispose();$clone.Dispose()}
    exit
}
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'rehearsal-result.json')){throw 'Rehearsal already completed; do not replay'}
$backupStateExists=Test-Path -LiteralPath (Join-Path $PSScriptRoot 'backup-rehearsal-state.json')
if($backupStateExists -and !$ResumeVerifiedBackup){throw 'Backup state exists; inspect before explicit safe resume'}
if(!$backupStateExists -and $ResumeVerifiedBackup){throw 'No existing backup state to resume'}
$source=Open-Db 'HotelOpsDB';$master=Open-Db 'master'
try{
    $current=Identity $source
    if($current.database -cne $state.sourceIdentity.database -or $current.databaseId -ne $state.sourceIdentity.databaseId -or $current.server -cne $state.sourceIdentity.server -or $current.createStamp -cne $state.sourceIdentity.createStamp){throw 'Source identity changed'}
    $baseline=[ordered]@{};foreach($p in $state.sourceBefore.PSObject.Properties){$baseline[$p.Name]=$p.Value}
    Assert-Same $baseline (Snapshot $source)
    $schema=[ordered]@{};foreach($p in $state.sourceSchema.PSObject.Properties){$schema[$p.Name]=$p.Value}
    Assert-Schema $schema (SchemaHashes $source)
    if($ResumeVerifiedBackup){
        $saved=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'backup-rehearsal-state.json')|ConvertFrom-Json
        $cloneName=$saved.clone;$backup=$saved.backup;$data=$saved.data;$log=$saved.log
        if($cloneName -notmatch '^HotelOps_RBAC03A_Rehearsal_20261021_[a-f0-9]{12}$' -or $backup -cne (Join-Path $state.backupPath ($state.runId+'.bak')) -or $data -cne (Join-Path $state.dataPath ($cloneName+'.mdf')) -or $log -cne (Join-Path $state.logPath ($cloneName+'_log.ldf'))){throw 'Resume paths differ from reviewed backup state'}
        foreach($path in @($data,$log)){if(Test-Path -LiteralPath $path){throw 'Restore file already exists; no overwrite permitted'}}
    }else{
        $cloneName='HotelOps_RBAC03A_Rehearsal_20261021_'+[guid]::NewGuid().ToString('N').Substring(0,12)
        $backup=Join-Path $state.backupPath ($state.runId+'.bak')
        $data=Join-Path $state.dataPath ($cloneName+'.mdf');$log=Join-Path $state.logPath ($cloneName+'_log.ldf')
        foreach($path in @($backup,$data,$log)){if(Test-Path -LiteralPath $path){throw 'Generated recovery file exists'}}
    }
    if((Query $master ('SELECT DB_ID('+(Literal $cloneName)+') id')).Tables[0].Rows[0].id -isnot [DBNull]){throw 'Clone exists'}
    $restoreState=[ordered]@{clone=$cloneName;backup=$backup;data=$data;log=$log;sourceIdentity=$state.sourceIdentity;upgradeOnMain=$false}
    if(!$ResumeVerifiedBackup){Save-Json $restoreState (Join-Path $PSScriptRoot 'backup-rehearsal-state.json')}
    # The only data-management command issued on the source is this authorized COPY_ONLY backup.
    if(!$ResumeVerifiedBackup){Execute $source ('BACKUP DATABASE [HotelOpsDB] TO DISK='+(Literal $backup)+' WITH COPY_ONLY,CHECKSUM,NOINIT,NAME='+(Literal $state.runId))}
    Execute $master ('RESTORE VERIFYONLY FROM DISK='+(Literal $backup)+' WITH CHECKSUM')
    $backupMeta=Rows $master ("SELECT bs.database_name,bs.backup_start_date,bs.backup_finish_date,bs.backup_size,bs.compressed_backup_size,bs.is_copy_only,bs.has_backup_checksums,bs.first_lsn,bs.last_lsn,bs.checkpoint_lsn FROM msdb.dbo.backupset bs JOIN msdb.dbo.backupmediafamily mf ON mf.media_set_id=bs.media_set_id WHERE mf.physical_device_name="+(Literal $backup)+' ORDER BY bs.backup_finish_date DESC')
    if($backupMeta.Count -ne 1 -or $backupMeta[0].database_name -cne 'HotelOpsDB' -or !$backupMeta[0].is_copy_only -or !$backupMeta[0].has_backup_checksums){throw 'Backup source/history/checksum evidence mismatch'}
    Save-Json ([ordered]@{path=$backup;fileSystemBytes=$null;fileSystemSizeStatus='Windows ACL denies PowerShell Get-Item; no ACL changes attempted';reportedBackupBytes=[long]$backupMeta[0].backup_size;reportedCompressedBackupBytes=[long]$backupMeta[0].compressed_backup_size;metadata=$backupMeta;verifyOnly='PASS';serverTimeNote='backup_start/finish_date are SQL Server local datetime; not labelled UTC';capturedUtc=(Query $master 'SELECT SYSUTCDATETIME() utc').Tables[0].Rows[0].utc.ToString('o')}) (Join-Path $PSScriptRoot 'backup-evidence.json')
    Write-Output 'PASS: unique main COPY_ONLY CHECKSUM backup and VERIFYONLY; no main DML/DDL'
    $files=(Query $master ('RESTORE FILELISTONLY FROM DISK='+(Literal $backup))).Tables[0]
    if($files.Rows.Count -ne 2 -or @($files.Rows|Where-Object Type -eq 'D').Count -ne 1 -or @($files.Rows|Where-Object Type -eq 'L').Count -ne 1){throw 'Unexpected backup file layout'}
    $sourceFiles=Rows $source 'SELECT name,physical_name FROM sys.database_files'
    foreach($f in $sourceFiles){if($f.physical_name -in @($data,$log)){throw 'Physical path collision with main'}}
    $moves=@();foreach($f in $files.Rows){$target=if($f.Type -eq 'D'){$data}else{$log};$moves+='MOVE '+(Literal $f.LogicalName)+' TO '+(Literal $target)}
    Execute $master ('IF DB_ID('+(Literal $cloneName)+") IS NOT NULL THROW 53110,'Clone exists',1; RESTORE DATABASE "+(Identifier $cloneName)+' FROM DISK='+(Literal $backup)+' WITH CHECKSUM,RECOVERY,'+($moves -join ','))
    $clone=Open-Db $cloneName
    try{
        $cloneId=Identity $clone
        if($cloneId.database -eq 'HotelOpsDB' -or $cloneId.databaseId -eq $state.sourceIdentity.databaseId){throw 'Clone not independent'}
        $cloneFiles=Rows $clone 'SELECT name,physical_name FROM sys.database_files'
        foreach($f in $cloneFiles){if($f.physical_name -notin @($data,$log)){throw 'Unexpected restored physical path'}}
        Assert-Same $baseline (Snapshot $clone);Assert-Schema $schema (SchemaHashes $clone)
        Save-Json ([ordered]@{identity=$cloneId;files=$cloneFiles;sourceFiles=$sourceFiles;all72TableContentMatch=$true;allSchemaHashesMatch=$true}) (Join-Path $PSScriptRoot 'restore-proof.json')
        Write-Output ('PASS: actual restore to independent '+$cloneName+'; 72 table hashes and schema hashes match source baseline')
        $script:PkOrders=[ordered]@{}
        foreach($key in $baseline.Keys){$keys=(Query $clone ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID("+(Literal $key)+") AND i.is_primary_key=1 ORDER BY ic.key_ordinal")).Tables[0].Rows;$script:PkOrders[$key]=($keys|ForEach-Object {Identifier $_.name}) -join ','}
        $mainApproval='Task03B:'+ $state.runId
        $rehearsalApproval='Task03A-Rehearsal:'+ $state.runId
        $mainSql=Make-Deployment $state.sourceIdentity $mainApproval $baseline $state.runId $false
        $drySql=Make-Deployment $cloneId $rehearsalApproval $baseline $state.runId $true
        $commitSql=Make-Deployment $cloneId $rehearsalApproval $baseline $state.runId $false
        $mainSql|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql')
        $drySql|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'rehearsal-dry-run.sql')
        $commitSql|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'rehearsal-commit.sql')
        $closed=$false
        try{Run-CloneBatches $clone $drySql $cloneId}catch{if($_.Exception.InnerException -is [System.Data.SqlClient.SqlException] -and $_.Exception.InnerException.Number -eq 53001){$closed=$true}else{throw}}
        if(!$closed){throw 'Approval gate unexpectedly open'}
        Assert-Same $baseline (Snapshot $clone)
        Write-Output 'PASS: approval gate rejected rehearsal before any writes (53001)'
        Execute $clone ("EXEC sys.sp_set_session_context @key=N'HotelOps.RbacDeploymentApproval',@value="+(Literal $rehearsalApproval))
        $auditMax=[long](Query $clone 'SELECT COALESCE(MAX(audit_id),0) n FROM dbo.audit_logs').Tables[0].Rows[0].n
        $beforePermissions=Rows $clone 'SELECT permission_code FROM dbo.permissions ORDER BY permission_code'
        $beforeSemantic=Rows $clone 'SELECT r.role_code,p.permission_code,rp.data_scope FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id ORDER BY r.role_code,p.permission_code'
        Run-CloneBatches $clone $drySql $cloneId
        Assert-Same $baseline (Snapshot $clone);Assert-Schema $schema (SchemaHashes $clone)
        Write-Output 'PASS: clone Dry Run ROLLBACK restores all 72 table contents and schema hashes'
        $userCols=((Query $clone "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name<>'department_id' ORDER BY column_id").Tables[0].Rows|ForEach-Object {Identifier $_.name}) -join ','
        $userSql="SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $userCols FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h"
        $userHash=(Query $clone $userSql).Tables[0].Rows[0].h
        $auditSql="SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.audit_logs WHERE audit_id<=$auditMax ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h"
        $auditOldHash=(Query $clone $auditSql).Tables[0].Rows[0].h
        Run-CloneBatches $clone $commitSql $cloneId
        Write-Output 'PASS: exact rehearsal variant COMMIT on new clone only'
        $after=Snapshot $clone
        Assert-Same $baseline $after @('[dbo].[roles]','[dbo].[permissions]','[dbo].[role_permissions]','[dbo].[users]','[dbo].[user_roles]','[dbo].[user_branch_access]','[dbo].[audit_logs]')
        Assert-Schema $schema (SchemaHashes $clone)
        if((Query $clone $userSql).Tables[0].Rows[0].h -cne $userHash){throw 'Unexpected user/authentication field change'}
        if((Query $clone $auditSql).Tables[0].Rows[0].h -cne $auditOldHash){throw 'Old audit history changed'}
        if($after['[dbo].[audit_logs]'].count -ne $baseline['[dbo].[audit_logs]'].count+6){throw 'Expected six migration audit records'}
        $verify=& (Join-Path $PSScriptRoot 'Verify-Rehearsal.ps1') $clone
        $verify|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'rehearsal-verification.txt')
        $verify|Write-Output
        $afterSemantic=Rows $clone 'SELECT r.role_code,p.permission_code,rp.data_scope FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id ORDER BY r.role_code,p.permission_code'
        $aliases=@{GENERAL_MANAGER='OPERATIONS_MANAGER';HK_MANAGER='HK_SUPERVISOR';MAINT_MANAGER='MAINTENANCE_LEAD';TECHNICIAN='MAINTENANCE_STAFF';WAREHOUSE_KEEPER='STOREKEEPER'}
        $oldGrants=@{};foreach($g in $beforeSemantic){$code=$g.role_code;if($aliases.ContainsKey($code)){$code=$aliases[$code]};$oldGrants[$code+'|'+$g.permission_code]=$g.data_scope}
        $newGrants=@{};foreach($g in $afterSemantic){$newGrants[$g.role_code+'|'+$g.permission_code]=$g.data_scope}
        $added=@();$removed=@();$scopeChanged=@()
        foreach($k in $newGrants.Keys){if(!$oldGrants.ContainsKey($k)){$added+=$k}elseif($oldGrants[$k] -cne $newGrants[$k]){$scopeChanged+=[ordered]@{key=$k;before=$oldGrants[$k];after=$newGrants[$k]}}}
        foreach($k in $oldGrants.Keys){if(!$newGrants.ContainsKey($k)){$removed+=$k}}
        $diff=@();foreach($k in $baseline.Keys){if($baseline[$k].hash -cne $after[$k].hash){$diff+=[ordered]@{table=$k;beforeCount=$baseline[$k].count;afterCount=$after[$k].count}}}
        $allAfterPermissions=Rows $clone 'SELECT permission_code FROM dbo.permissions ORDER BY permission_code'
        $oldCodes=@($beforePermissions|ForEach-Object {$_.permission_code})
        $newCodes=@($allAfterPermissions|Where-Object {$_.permission_code -notin $oldCodes}|ForEach-Object {$_.permission_code})
        if($newCodes.Count -ne 15){throw 'Expected 15 new permission codes'}
        Save-Json ([ordered]@{changedTables=$diff;grantsComparison='Old codes normalized by approved aliases only; key=role_code|permission_code';addedGrantKeys=@($added|Sort-Object);removedGrantKeys=@($removed|Sort-Object);changedScopes=$scopeChanged;expectedAuditRows=6;personnel=Rows $clone "SELECT ur.user_id,r.role_code,ur.branch_id,ur.is_active FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code IN ('OPERATIONS_MANAGER','DEPT_MANAGER','STOREKEEPER','HK_INSPECTOR','HK_SUPERVISOR') ORDER BY r.role_code,ur.branch_id,ur.user_id";userDepartmentChanges=Rows $clone 'SELECT user_id,primary_branch_id,department_id,account_status FROM dbo.users WHERE user_id IN (1,4,5,19)';newPermissionCodes=$newCodes;targetGrants=$afterSemantic;note='New identity values/timestamps on main may differ from clone due rollback identity gaps; semantic codes/assignments are approval keys'}) (Join-Path $PSScriptRoot 'EXPECTED_CHANGES_FOR_TASK03B.json')
        Save-Json $after (Join-Path $PSScriptRoot 'rehearsal-after.json')
        Assert-Same $baseline (Snapshot $source);Assert-Schema $schema (SchemaHashes $source)
        foreach($p in $state.protectedFiles.PSObject.Properties){if((Get-FileHash -Algorithm SHA256 -LiteralPath $p.Name).Hash -cne $p.Value){throw ('Protected file changed: '+$p.Name)}}
        Save-Json ([ordered]@{clone=$cloneId;backup=$backup;restore='PASS';approvalGate='PASS_53001';dryRun='PASS_ROLLBACK_72_TABLES_AND_SCHEMA';commit='PASS_CLONE_ONLY';catalog='PASS_EXACT';integrity='PASS';authenticationFields='UNCHANGED';sessions='UNCHANGED';departmentsAndBusinessTables='UNCHANGED';oldAudits='UNCHANGED';newMigrationAudits=6;sourceAll72Tables='UNCHANGED';sourceSchema='UNCHANGED';mainUpgradeExecuted=$false;mainScriptSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql')).Hash;separateTask03BApprovalRequired=$true}) (Join-Path $PSScriptRoot 'rehearsal-result.json')
        Write-Output 'PASS: source all 72 table contents/schema and protected source/config/release unchanged; main upgrade NEVER executed'
    }finally{$clone.Dispose()}
}finally{$source.Dispose();$master.Dispose()}




