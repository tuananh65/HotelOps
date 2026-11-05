param([ValidateSet('Inspect','Execute')][string]$Stage = 'Inspect')
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

$trial='HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282'
$clone=Open-Db $trial
try {
    $id=(Query $clone "SELECT DB_NAME() db,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) server_name").Tables[0].Rows[0]
    if($id.db -cne $trial -or $id.server_name -cne 'DESKTOP-JI8U50T\SQLEXPRESS'){throw 'Wrong trial target'}
    if($Stage -eq 'Inspect') {
        $output=@(Print-Results (Query $clone (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'preflight.sql'))))
        $output | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'preflight-results.txt')
        $output | Write-Output
        $fks=(Query $clone "SELECT fk.name fk_name,OBJECT_SCHEMA_NAME(fkc.parent_object_id) schema_name,OBJECT_NAME(fkc.parent_object_id) table_name,COL_NAME(fkc.parent_object_id,fkc.parent_column_id) column_name FROM sys.foreign_key_columns fkc JOIN sys.foreign_keys fk ON fk.object_id=fkc.constraint_object_id WHERE fkc.referenced_object_id=OBJECT_ID('dbo.users') ORDER BY table_name,column_name").Tables[0]
        $refs=@()
        foreach($fk in $fks.Rows) {
            if($fk.table_name -in @('users','user_sessions','user_roles','user_branch_access')){continue}
            $table=(Identifier $fk.schema_name)+'.'+(Identifier $fk.table_name)
            $column=Identifier $fk.column_name
            $cols=(Query $clone ("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID("+(Literal $table)+") ORDER BY column_id")).Tables[0].Rows | ForEach-Object {$_.name}
            $safe=$cols | Where-Object {$_ -match '(^|_)(id|status|at|date)$|^is_active$' -and $_ -notmatch 'password|token|credential|secret'}
            $projection=($safe|ForEach-Object {Identifier $_}) -join ','
            $ds=Query $clone "SELECT $projection FROM $table WHERE $column IN (11,17,18,20)"
            if($ds.Tables[0].Rows.Count){$refs+=('User FK: '+$fk.fk_name);$refs+=Print-Results $ds}
        }
        $refs | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'user-reference-results.txt')
        $refs | Write-Output
        exit
    }
    $evidence=Join-Path $PSScriptRoot 'result.json'
    if(Test-Path -LiteralPath $evidence){throw 'Execution evidence exists; do not replay this completed partial trial'}
    $source=Open-Db 'HotelOpsDB'
    try {
        $before=Snapshot $clone
        $sourceBefore=Snapshot $source
        Save-Json $before (Join-Path $PSScriptRoot 'trial-before.json')
        Save-Json $sourceBefore (Join-Path $PSScriptRoot 'source-before.json')
        $protected=@{}
        foreach($file in @('src/HotelOps.Api/Program.cs','src/HotelOps.Api/Security/Authorization/ICurrentUserAccessor.cs','src/HotelOps.Api/Security/Authorization/CurrentUserAccessor.cs','src/HotelOps.Api/appsettings.json','src/HotelOps.Api/appsettings.Development.json','scripts/verification/RbacUpgradeTrial/Task02B/department-cleanup.sql')+@(Get-ChildItem $release -File|ForEach-Object {'scripts/database/release/'+$_.Name})) {
            $protected[$file]=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $file)).Hash
        }
        $rows=(Query $clone "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) path,IS_SRVROLEMEMBER('sysadmin') sysadmin").Tables[0].Rows[0]
        if($rows.sysadmin -ne 1){throw 'Backup permission not established'}
        $backup=Join-Path $rows.path ('HotelOps_Task02C_'+[guid]::NewGuid().ToString('N')+'.bak')
        if(Test-Path -LiteralPath $backup){throw 'Backup path exists'}
        Execute $clone ("BACKUP DATABASE "+(Identifier $trial)+" TO DISK="+(Literal $backup)+" WITH COPY_ONLY,CHECKSUM,NOINIT")
        Execute $clone ("RESTORE VERIFYONLY FROM DISK="+(Literal $backup)+" WITH CHECKSUM")
        Save-Json ([ordered]@{database=$trial;backup=$backup;verifyOnly='PASS';copyOnly=$true}) (Join-Path $PSScriptRoot 'recovery-point.json')
        Write-Output 'PASS: current trial COPY_ONLY CHECKSUM backup and VERIFYONLY, outside repository'

        $unchangedColumns=@{}
        foreach($table in @('warehouses','internal_request_types','work_categories')) {
            $columns=((Query $clone ("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo."+$table+"') AND name<>'is_active' ORDER BY column_id")).Tables[0].Rows|ForEach-Object {Identifier $_.name}) -join ','
            $pk=(Query $clone ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo."+$table+"') AND i.is_primary_key=1 ORDER BY ic.key_ordinal")).Tables[0].Rows[0].name
            $q="SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $columns FROM dbo.$table ORDER BY "+(Identifier $pk)+" FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h"
            $unchangedColumns[$table]=@{sql=$q;hash=(Query $clone $q).Tables[0].Rows[0].h}
        }
        $auditBefore=(Query $clone 'SELECT COALESCE(MAX(audit_id),0) max_id,COUNT_BIG(*) n FROM dbo.audit_logs').Tables[0].Rows[0]
        $auditMax=[long]$auditBefore.max_id
        $auditOldSql="SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.audit_logs WHERE audit_id<=$auditMax ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h"
        $auditOldHash=(Query $clone $auditOldSql).Tables[0].Rows[0].h
        $sql=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'resolve-safe-dependencies.sql')
        if(($sql.Split(@('DECLARE @Commit bit=0;'),[StringSplitOptions]::None).Count-1) -ne 1){throw 'Dry-run flag changed'}
        $dry=Query $clone $sql
        Print-Results $dry | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'dry-run-results.txt')
        if($dry.Tables[0].Rows.Count -ne 8){throw 'Expected exactly eight safe retirement candidates'}
        Assert-Same $before (Snapshot $clone)
        Write-Output 'PASS: eight safe deactivations and audit rows dry-run; all table fingerprints restored by ROLLBACK'
        $commit=Query $clone $sql.Replace('DECLARE @Commit bit=0;','DECLARE @Commit bit=1;')
        Print-Results $commit | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'commit-results.txt')
        if($commit.Tables[0].Rows.Count -ne 8){throw 'Unexpected committed row scope'}
        Write-Output 'PASS: COMMIT two empty warehouses, three request types, three work categories; eight audit records'
        $after=Snapshot $clone
        Assert-Same $before $after @('[dbo].[warehouses]','[dbo].[internal_request_types]','[dbo].[work_categories]','[dbo].[audit_logs]')
        foreach($table in $unchangedColumns.Keys) {
            if((Query $clone $unchangedColumns[$table].sql).Tables[0].Rows[0].h -cne $unchangedColumns[$table].hash){throw 'Unexpected nonflag field changed'}
            if($before['[dbo].['+$table+']'].count -ne $after['[dbo].['+$table+']'].count){throw 'Row count changed'}
        }
        if((Query $clone $auditOldSql).Tables[0].Rows[0].h -cne $auditOldHash){throw 'Existing audit history changed'}
        if($after['[dbo].[audit_logs]'].count -ne ([long]$auditBefore.n+8)){throw 'Audit append count mismatch'}
        Save-Json $after (Join-Path $PSScriptRoot 'trial-after.json')
        $gate=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'scripts/verification/RbacUpgradeTrial/Task02B/department-cleanup.sql')
        # Read the unchanged preflight and rollback path to report remaining blockers.
        Print-Results (Query $clone $gate) | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'department-dry-run-results.txt')
        $blocked=$false
        try {Execute $clone $gate.Replace('DECLARE @Commit bit=0;','DECLARE @Commit bit=1;')}
        catch {if($_.Exception.InnerException -is [System.Data.SqlClient.SqlException] -and $_.Exception.InnerException.Number -eq 52005){$blocked=$true}else{throw}}
        if(!$blocked){throw 'Unexpected department COMMIT success'}
        Assert-Same $after (Snapshot $clone)
        Write-Output 'BLOCKED: unchanged SQL 52005 guard still rejects department retirement; no department change committed'
        $parent=Split-Path $PSScriptRoot -Parent
        $state=Get-Content -Raw -LiteralPath (Join-Path $parent 'trial-state.json')|ConvertFrom-Json
        $verification=& (Join-Path $parent 'Verify-Trial.ps1') $clone $state $root
        $verification | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'verification-results.txt')
        $verification | Select-String -Pattern 'PASS:|Full Schema column comparison' | Write-Output
        Print-Results (Query $clone (Get-Content -Raw -Encoding UTF8 (Join-Path $release '04_HotelOps_Verification_ReadOnly.sql'))) | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot 'release-verification-results.txt')
        $sourceAfter=Snapshot $source
        Assert-Same $sourceBefore $sourceAfter
        Save-Json $sourceAfter (Join-Path $PSScriptRoot 'source-after.json')
        foreach($file in $protected.Keys){if((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $file)).Hash -cne $protected[$file]){throw ('Protected file changed: '+$file)}}
        Save-Json $protected (Join-Path $PSScriptRoot 'protected-file-hashes.json')
        Save-Json ([ordered]@{safeSubsetDryRun='PASS_ROLLBACK';safeSubsetCommit='PASS_8_FLAGS_8_AUDITS';departments='BLOCKED_SQL_52005';rbac='PASS';integrity='PASS';source='UNCHANGED';usersAndSessions='UNCHANGED';verdict='BLOCKED_PARTIAL_IMPLEMENTATION'}) $evidence
    } finally {$source.Dispose()}
} finally {$clone.Dispose()}
