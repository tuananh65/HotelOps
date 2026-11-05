param([ValidateSet('Restore','Upgrade','Verify')][string]$Stage = 'Restore')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
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
$protected = @('src/HotelOps.Api/Program.cs','src/HotelOps.Api/Security/Authorization/ICurrentUserAccessor.cs','src/HotelOps.Api/Security/Authorization/CurrentUserAccessor.cs')
if ($Stage -eq 'Restore') {
    if (Test-Path -LiteralPath $statePath) { throw 'Trial state already exists; no automatic overwrite or restore' }
    $source = Open-Db 'HotelOpsDB'; $master = Open-Db 'master'
    try {
        $info = (Query $source "SELECT DB_NAME() db,CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) server_name,IS_SRVROLEMEMBER('sysadmin') sysadmin,CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) backup_path,CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) data_path,CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) log_path").Tables[0].Rows[0]
        if ($info.db -cne 'HotelOpsDB' -or $info.sysadmin -ne 1) { throw 'Source/restore permissions not established' }
        $suffix = '20261021_' + [guid]::NewGuid().ToString('N').Substring(0,12)
        $trial = 'HotelOps_RBAC_Upgrade_Trial_' + $suffix
        $backup = Join-Path $info.backup_path ($trial + '.bak')
        $data = Join-Path $info.data_path ($trial + '.mdf')
        $log = Join-Path $info.log_path ($trial + '_log.ldf')
        foreach ($path in @($backup,$data,$log)) { if (Test-Path -LiteralPath $path) { throw 'Generated file already exists' } }
        if ((Query $master ("SELECT DB_ID("+(Literal $trial)+") id")).Tables[0].Rows[0].id -isnot [DBNull]) { throw 'Trial DB exists' }
        $state = [ordered]@{server=[string]$info.server_name; source='HotelOpsDB'; trial=$trial; backup=$backup; data=$data; log=$log; sourceBefore=(Snapshot $source); protected=@{}}
        foreach ($file in ($protected + @(Get-ChildItem $release -File | ForEach-Object { 'scripts/database/release/' + $_.Name }))) { $state.protected[$file]=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $file)).Hash }
        Save-Json $state $statePath
        Execute $source ("BACKUP DATABASE [HotelOpsDB] TO DISK="+(Literal $backup)+" WITH COPY_ONLY,CHECKSUM,NOINIT,NAME="+(Literal $trial))
        Write-Output 'PASS: new COPY_ONLY CHECKSUM backup'
        Execute $master ("RESTORE VERIFYONLY FROM DISK="+(Literal $backup)+" WITH CHECKSUM")
        Write-Output 'PASS: RESTORE VERIFYONLY CHECKSUM'
        $files = (Query $master ("RESTORE FILELISTONLY FROM DISK="+(Literal $backup))).Tables[0]
        if ($files.Rows.Count -ne 2 -or @($files.Rows | Where-Object Type -eq 'D').Count -ne 1 -or @($files.Rows | Where-Object Type -eq 'L').Count -ne 1) { throw 'Unexpected backup file layout' }
        $moves = @()
        foreach ($f in $files.Rows) { $target = if ($f.Type -eq 'D') {$data} else {$log}; $moves += 'MOVE ' + (Literal $f.LogicalName) + ' TO ' + (Literal $target) }
        Execute $master ("IF DB_ID("+(Literal $trial)+") IS NOT NULL THROW 51900,'Trial exists',1; RESTORE DATABASE "+(Identifier $trial)+" FROM DISK="+(Literal $backup)+" WITH CHECKSUM,RECOVERY,"+($moves -join ','))
        $clone = Open-Db $trial
        try {
            Assert-Same $state.sourceBefore (Snapshot $clone)
            Print-Results (Query $clone "SELECT DB_NAME() database_name,name,physical_name FROM sys.database_files")
            Write-Output 'PASS: clone table counts and SHA256 content fingerprints equal source baseline'
        } finally { $clone.Dispose() }
        Write-Output ('Trial database: ' + $trial)
        Write-Output ('Backup: ' + $backup)
    } finally { $source.Dispose(); $master.Dispose() }
    exit
}
$state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
$trial = $state.trial
if ($trial -notmatch '^HotelOps_RBAC_Upgrade_Trial_20261021_[a-f0-9]{12}$' -or $trial -eq 'HotelOpsDB') { throw 'Invalid trial identity' }
$clone = Open-Db $trial
try {
    $identity=(Query $clone "SELECT DB_NAME() db,CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) server_name").Tables[0].Rows[0]
    if ($identity.db -cne $trial -or $identity.server_name -cne $state.server) { throw 'Wrong trial connection' }
    if ($Stage -eq 'Verify') {
        & (Join-Path $PSScriptRoot 'Verify-Trial.ps1') $clone $state $root
        exit
    }
    $catalog=(Query $clone 'SELECT (SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) grants').Tables[0].Rows[0]
    if ($catalog.roles -ne 11 -or $catalog.permissions -ne 47 -or $catalog.grants -ne 182) { throw 'Trial is not at original baseline; never rerun Upgrade on upgraded data' }
    $before = Snapshot $clone
    Save-Json $before (Join-Path $PSScriptRoot 'trial-before.json')
    $userColumns=((Query $clone "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name<>'department_id' ORDER BY column_id").Tables[0].Rows | ForEach-Object {Identifier $_.name}) -join ','
    $usersSql="SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $userColumns FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h"
    $userHash=(Query $clone $usersSql).Tables[0].Rows[0].h
    $schemaSql="SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT s.name schema_name,t.name table_name,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id ORDER BY s.name,t.name,c.column_id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h"
    $schemaHash=(Query $clone $schemaSql).Tables[0].Rows[0].h
    Save-Json ([ordered]@{usersExcludingDepartmentHash=$userHash; schemaHash=$schemaHash}) (Join-Path $PSScriptRoot 'extra-baseline.json')
    $text = Get-Content -Raw -Encoding UTF8 (Join-Path $release '02_HotelOps_Upgrade_Auth_To_RBAC.sql')
    # Only three exact executable target sites; preserve template comments and all guards.
    foreach ($old in @('USE [__REVIEWED_TARGET_DATABASE__];',"IF DB_NAME() <> N'__REVIEWED_TARGET_DATABASE__'","IF DB_NAME()<>N'__REVIEWED_TARGET_DATABASE__'")) {
        if (($text.Split(@($old),[StringSplitOptions]::None).Count-1) -ne 1) { throw 'Unexpected target occurrence' }
        $text=$text.Replace($old,$old.Replace('__REVIEWED_TARGET_DATABASE__',$trial))
    }
    $selection='DECLARE @SelectedDh TABLE(branch_id BIGINT PRIMARY KEY,user_id BIGINT NOT NULL);'
    if (!$text.Contains($selection)) { throw 'Selection anchor missing' }
    $text=$text.Replace($selection,$selection+"`r`n    INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);")
    # Both approved conflict resolutions participate in the existing main transaction.
    $prep=@"
    IF DB_NAME()<>N'$trial' THROW 51901,'Wrong trial DB',1;
    IF NOT EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE ur.user_id=19 AND ur.branch_id=2 AND ur.is_active=1 AND r.role_code='WAREHOUSE_KEEPER')
        THROW 51902,'Expected trial Storekeeper assignment missing',1;
    UPDATE ur SET is_active=0 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
      WHERE ur.user_id=19 AND ur.branch_id=2 AND r.role_code='WAREHOUSE_KEEPER' AND ur.is_active=1;
    UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME()
      WHERE user_id=1 AND branch_id=2 AND access_level='MANAGER' AND revoked_at IS NULL;
"@
    if (($text.Split(@('    BEGIN TRANSACTION;'),[StringSplitOptions]::None).Count-1) -ne 1) { throw 'Transaction anchor changed' }
    $text=$text.Replace('    BEGIN TRANSACTION;',"    BEGIN TRANSACTION;`r`n"+$prep)
    $stripped=[regex]::Replace($text,'(?s)/\*.*?\*/|--[^\r\n]*','')
    $stripped=[regex]::Replace($stripped,"N?'(?:[^']|'')*'","''")
    if ($stripped -match '(?i)\bEXEC(?:UTE)?\b|\bUSE\s+\[(?!HotelOps_RBAC_Upgrade_Trial_)|\bHotelOpsDB\b|\]\s*\.\s*\[') { throw 'Unexpected dynamic/cross-database reference' }
    if ($stripped -match '__REVIEWED_TARGET_DATABASE__') { throw 'Unresolved executable placeholder' }
    $commitPath=Join-Path $PSScriptRoot 'trial-upgrade.sql'
    $dryPath=Join-Path $PSScriptRoot 'trial-dry-run.sql'
    $text | Set-Content -Encoding UTF8 -LiteralPath $commitPath
    if (($text.Split(@('    COMMIT TRANSACTION;'),[StringSplitOptions]::None).Count-1) -ne 1) { throw 'Unexpected COMMIT count' }
    $dry=$text.Replace('    COMMIT TRANSACTION;','    ROLLBACK TRANSACTION;').Replace('RBAC UPGRADE COMMITTED.','RBAC TRIAL DRY RUN ROLLED BACK.')
    $dry | Set-Content -Encoding UTF8 -LiteralPath $dryPath
    foreach ($batch in [regex]::Split($dry,'(?im)^GO\s*\r?$')) { if ($batch.Trim()) { Execute $clone $batch } }
    Assert-Same $before (Snapshot $clone)
    Write-Output 'PASS: dry run completed and every table content fingerprint restored by rollback'
    foreach ($batch in [regex]::Split($text,'(?im)^GO\s*\r?$')) { if ($batch.Trim()) { Execute $clone $batch } }
    Write-Output 'PASS: Upgrade committed on trial only'
    Print-Results (Query $clone (Get-Content -Raw -Encoding UTF8 (Join-Path $release '04_HotelOps_Verification_ReadOnly.sql')))
    $after = Snapshot $clone
    Save-Json $after (Join-Path $PSScriptRoot 'trial-after.json')
    Assert-Same $before $after @('[dbo].[roles]','[dbo].[permissions]','[dbo].[role_permissions]','[dbo].[user_roles]','[dbo].[user_branch_access]','[dbo].[users]')
    Write-Output 'PASS: all other tables including SQL sessions retain exact counts and SHA256 fingerprints'
    if ((Query $clone $usersSql).Tables[0].Rows[0].h -cne $userHash) { throw 'User fields other than department changed' }
    if ((Query $clone $schemaSql).Tables[0].Rows[0].h -cne $schemaHash) { throw 'Unexpected table/column schema change' }
    Write-Output 'PASS: all user fields except department preserved; table/column schema unchanged'
} finally { $clone.Dispose() }
