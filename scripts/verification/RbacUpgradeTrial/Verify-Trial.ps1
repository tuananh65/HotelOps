param($clone,$state,[string]$root)
$ErrorActionPreference='Stop'
$trial=$state.trial
if ((Query $clone 'SELECT DB_NAME() db').Tables[0].Rows[0].db -cne $trial) { throw 'Wrong verification DB' }
$seed=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql')
$specs=@(
    @{name='roles'; start='INSERT INTO @SeedRoles'; end='DECLARE @SeedPermissions'; columns='role_code,role_name,description,is_system_role,is_active'; actual='SELECT role_code,role_name,description,is_system_role,is_active FROM dbo.roles'; count=12},
    @{name='permissions'; start='INSERT INTO @SeedPermissions'; end='DECLARE @SeedGrants'; columns='permission_code,permission_name,module_code,resource_code,action_code,description'; actual='SELECT permission_code,permission_name,module_code,resource_code,action_code,description FROM dbo.permissions'; count=62},
    @{name='grants'; start='INSERT INTO @SeedGrants'; end='    IF (SELECT COUNT(*) FROM @SeedRoles)'; columns='role_code,permission_code,data_scope'; actual='SELECT r.role_code,p.permission_code,rp.data_scope FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id'; count=140}
)
foreach ($spec in $specs) {
    $part=$seed.Substring($seed.IndexOf($spec.start)); $part=$part.Substring(0,$part.IndexOf($spec.end))
    $tuples=[regex]::Matches($part,'(?m)^\(.+\)(?=[,;]\r?$)')
    if ($tuples.Count -ne $spec.count) { throw ('Unexpected seed tuples: '+$spec.name) }
    $values=($tuples | ForEach-Object {$_.Value}) -join ','
    $sql="WITH expected AS (SELECT * FROM (VALUES $values) x($($spec.columns))), actual AS ($($spec.actual)) SELECT (SELECT COUNT(*) FROM (SELECT * FROM expected EXCEPT SELECT * FROM actual) x) missing,(SELECT COUNT(*) FROM (SELECT * FROM actual EXCEPT SELECT * FROM expected) x) extra"
    $r=(Query $clone $sql).Tables[0].Rows[0]
    if ($r.missing -ne 0 -or $r.extra -ne 0) { throw ('Catalog mismatch: '+$spec.name) }
    Write-Output ('PASS: exact bidirectional catalog comparison including metadata: '+$spec.name)
}
$assertions=@"
SELECT 'DH_BRANCH_1' issue WHERE (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.branch_id=1 AND ur.is_active=1)<>1
UNION ALL SELECT 'DH_BRANCH_2' WHERE (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.branch_id=2 AND ur.is_active=1)<>1
UNION ALL SELECT 'DH_IDENTITY' WHERE (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='DEPT_MANAGER' AND ur.is_active=1 AND ((ur.branch_id=1 AND ur.user_id=4) OR (ur.branch_id=2 AND ur.user_id=19)))<>2
UNION ALL SELECT 'USER5_STOREKEEPER' WHERE NOT EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='STOREKEEPER' AND ur.user_id=5 AND ur.branch_id=1 AND ur.is_active=1)
UNION ALL SELECT 'USER5_DH_OR_USER19_STOREKEEPER' WHERE EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE ur.is_active=1 AND ((ur.user_id=5 AND r.role_code='DEPT_MANAGER') OR (ur.user_id=19 AND r.role_code='STOREKEEPER')))
UNION ALL SELECT 'OM_ASSIGNMENT' WHERE (SELECT COUNT(*) FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='OPERATIONS_MANAGER' AND ur.is_active=1 AND ur.user_id=1 AND ur.branch_id=1)<>1
UNION ALL SELECT 'OM_OTHER_BRANCH' WHERE EXISTS (SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code='OPERATIONS_MANAGER' AND ur.is_active=1 AND (ur.branch_id IS NULL OR ur.branch_id<>1)) OR EXISTS (SELECT 1 FROM dbo.user_branch_access WHERE user_id=1 AND branch_id<>1 AND access_level='MANAGER' AND revoked_at IS NULL)
UNION ALL SELECT 'INVALID_SCOPE' WHERE EXISTS (SELECT 1 FROM dbo.role_permissions WHERE data_scope NOT IN ('ALL','BRANCH','OWN'))
"@
if ((Query $clone $assertions).Tables[0].Rows.Count -ne 0) { throw 'Personnel assertions failed' }
Write-Output 'PASS: approved DH/OM/Storekeeper assignments and scopes'
Print-Results (Query $clone "SELECT ur.user_id,r.role_code,ur.branch_id,ur.is_active FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.role_code IN ('OPERATIONS_MANAGER','DEPT_MANAGER','STOREKEEPER') ORDER BY r.role_code,ur.branch_id,ur.user_id; SELECT user_id,branch_id,access_level,revoked_at FROM dbo.user_branch_access WHERE user_id=1")
foreach ($sql in @('DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS', 'DBCC CHECKDB WITH NO_INFOMSGS, ALL_ERRORMSGS')) {
    $ds=Query $clone $sql
    foreach ($t in $ds.Tables) { if ($t.Rows.Count -gt 0) { throw ('Integrity returned findings: '+$sql) } }
    Write-Output ('PASS: '+$sql)
}
$disabled=(Query $clone "SELECT 'FK' kind,name FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1 UNION ALL SELECT 'CHECK',name FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1 UNION ALL SELECT 'INDEX',name FROM sys.indexes WHERE is_unique=1 AND is_disabled=1").Tables[0]
if ($disabled.Rows.Count -ne 0) { throw 'Disabled/untrusted constraints or unique indexes' }
Write-Output 'PASS: all FKs/CHECKs trusted and enabled; no disabled unique index'
Print-Results (Query $clone "SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys) foreign_keys,(SELECT COUNT(*) FROM sys.check_constraints) checks,(SELECT COUNT(*) FROM sys.default_constraints) defaults,(SELECT COUNT(*) FROM sys.indexes WHERE index_id>0 AND is_hypothetical=0) indexes")
# Report every direct FK reference to noncanonical departments; never deactivate them.
$fks=(Query $clone "SELECT OBJECT_SCHEMA_NAME(fkc.parent_object_id) schema_name,OBJECT_NAME(fkc.parent_object_id) table_name,COL_NAME(fkc.parent_object_id,fkc.parent_column_id) column_name FROM sys.foreign_key_columns fkc WHERE fkc.referenced_object_id=OBJECT_ID('dbo.departments')").Tables[0]
foreach ($fk in $fks.Rows) {
    $table=(Identifier $fk.schema_name)+'.'+(Identifier $fk.table_name)
    $col=Identifier $fk.column_name
    $ds=Query $clone ("SELECT '"+$fk.table_name+"' referencing_table,d.branch_id,d.department_code,COUNT_BIG(*) references_count FROM $table x JOIN dbo.departments d ON x.$col=d.department_id WHERE d.department_code NOT IN ('HK','MT','WH','FO') GROUP BY d.branch_id,d.department_code")
    Print-Results $ds
}
# Compare all CREATE TABLE column declarations to the full schema file without executing it.
$full=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'scripts/database/release/01_HotelOps_Full_Schema.sql')
$expected=@{}
foreach ($block in [regex]::Matches($full,'(?ms)CREATE TABLE \[dbo\]\.\[(?<table>[^]]+)\]\((?<body>.*?)^\)')) {
    foreach ($col in [regex]::Matches($block.Groups['body'].Value,'(?m)^\s*\[(?<col>[^]]+)\]\s+\[(?<type>[^]]+)\](?<args>\([^)]*\))?[^\r\n]*?(?<null>NOT NULL|NULL)')) {
        $key=$block.Groups['table'].Value+'.'+$col.Groups['col'].Value
        $expected[$key]=($col.Groups['type'].Value+$col.Groups['args'].Value+' '+$col.Groups['null'].Value).ToLowerInvariant()
    }
}
$actual=@{}
$cols=(Query $clone "SELECT t.name table_name,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.is_ms_shipped=0").Tables[0]
foreach ($col in $cols.Rows) {
    $type=$col.type_name
    if ($type -in @('varchar','nvarchar','char','nchar','varbinary','binary')) {
        $len=if ($col.max_length -eq -1) {'max'} elseif ($type -in @('nvarchar','nchar')) {[string]($col.max_length/2)} else {[string]$col.max_length}
        $type+='('+$len+')'
    } elseif ($type -in @('decimal','numeric')) {$type+='('+$col.precision+', '+$col.scale+')'}
    elseif ($type -in @('datetime2','datetimeoffset','time')) {$type+='('+$col.scale+')'}
    $nullability=if ($col.is_nullable) {'null'} else {'not null'}
    $actual[$col.table_name+'.'+$col.column_name]=($type+' '+$nullability).ToLowerInvariant()
}
$differences=@()
foreach ($key in $expected.Keys) { if (!$actual.ContainsKey($key) -or $expected[$key] -ne $actual[$key]) {$differences+= [ordered]@{column=$key; expected=$expected[$key]; actual=$actual[$key]}} }
foreach ($key in $actual.Keys) { if (!$expected.ContainsKey($key)) {$differences+= [ordered]@{column=$key;expected='absent';actual=$actual[$key]}} }
Save-Json $differences (Join-Path $PSScriptRoot 'schema-column-differences.json')
Write-Output ('Full Schema column comparison: expected '+$expected.Count+', actual '+$actual.Count+', differences '+$differences.Count)
if ($differences.Count -gt 0) {$differences | Select-Object -First 10 | ConvertTo-Json -Depth 4 | Write-Output}
$source=Open-Db 'HotelOpsDB'
try {
    $baseline=[ordered]@{}
    foreach ($p in $state.sourceBefore.PSObject.Properties) {$baseline[$p.Name]=$p.Value}
    $sourceAfter=Snapshot $source
    Save-Json $sourceAfter (Join-Path $PSScriptRoot 'source-after.json')
    Assert-Same $baseline $sourceAfter
    Write-Output ('PASS: source all '+$baseline.Count+' tables retain exact row counts and SHA256 fingerprints')
    Print-Results (Query $source "SELECT DB_NAME() db,(SELECT COUNT(*) FROM dbo.roles) roles,(SELECT COUNT(*) FROM dbo.roles WHERE is_active=1) active_roles,(SELECT COUNT(*) FROM dbo.permissions) permissions,(SELECT COUNT(*) FROM dbo.role_permissions) grants; SELECT data_scope,COUNT(*) grants FROM dbo.role_permissions GROUP BY data_scope")
    $sourceSchema=(Query $source "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT s.name schema_name,t.name table_name,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id ORDER BY s.name,t.name,c.column_id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) h").Tables[0].Rows[0].h
    $extra=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'extra-baseline.json') | ConvertFrom-Json
    if ($sourceSchema -cne $extra.schemaHash) { throw 'Source schema differs from pre-upgrade clone' }
    Write-Output 'PASS: source table/column schema matches pre-upgrade clone'
} finally {$source.Dispose()}
foreach ($p in $state.protected.PSObject.Properties) { if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $p.Name)).Hash -cne $p.Value) {throw ('Protected file changed: '+$p.Name)} }
Write-Output 'PASS: Task 01 files and standard release files unchanged'
Save-Json ([ordered]@{catalog='PASS'; personnel='PASS'; checkdb='PASS'; constraints='PASS'; source='PASS'; protectedFiles='PASS'; fullSchemaColumnDifferences=$differences.Count}) (Join-Path $PSScriptRoot 'verification-result.json')
