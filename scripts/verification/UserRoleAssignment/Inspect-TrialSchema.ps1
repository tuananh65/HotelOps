param(
    [Parameter(Mandatory=$true)][string]$Database,
    [Parameter(Mandatory=$true)][string]$Receipt,
    [Parameter(Mandatory=$true)][string]$ExpectedServer
)
$ErrorActionPreference = 'Stop'
if ($Database -cnotmatch '^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}_Fresh$') {
    throw 'Disposable canonical trial required; no production target accepted'
}
if (Test-Path -LiteralPath $Receipt) { throw 'Receipt already exists; preserve artifacts' }
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = $ExpectedServer
$builder['Initial Catalog'] = $Database
$builder['Integrated Security'] = $true
$builder['TrustServerCertificate'] = $true
$builder['Connect Timeout'] = 10
$builder['Application Name'] = 'HotelOps Task44 read-only schema inspection'
$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$budget = New-Object System.Threading.CancellationTokenSource
$budget.CancelAfter([TimeSpan]::FromMinutes(2))
function ReadJson([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 30
    $command.CommandText = $sql
    try { return $command.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult() | ConvertFrom-Json }
    finally { $command.Dispose() }
}
try {
    $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
    $identity = ReadJson "SELECT (SELECT DB_NAME() AS databaseName,CONVERT(nvarchar(128),@@SERVERNAME) AS serverName,(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial') AS marker FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)"
    if ($identity.databaseName -cne $Database -or $identity.serverName -cne $ExpectedServer -or $identity.marker -cne 'Task17') {
        throw 'Disposable trial identity rejected'
    }
    $columns = ReadJson @'
SELECT (SELECT OBJECT_NAME(c.object_id) AS tableName,c.name AS columnName,TYPE_NAME(c.user_type_id) AS typeName,c.is_nullable AS nullable
FROM sys.columns c WHERE c.object_id IN (OBJECT_ID('dbo.user_roles'),OBJECT_ID('dbo.roles'),OBJECT_ID('dbo.role_permissions'),OBJECT_ID('dbo.user_branch_access'))
OR (c.object_id=OBJECT_ID('dbo.users') AND c.name='row_version') ORDER BY tableName,c.column_id FOR JSON PATH)
'@
    $indexes = ReadJson @'
SELECT (SELECT OBJECT_NAME(i.object_id) AS tableName,i.name AS indexName,i.is_unique AS isUnique,i.has_filter AS hasFilter,i.filter_definition AS filterDefinition,
JSON_QUERY((SELECT c.name AS columnName,ic.key_ordinal AS ordinal FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 ORDER BY ic.key_ordinal FOR JSON PATH)) AS keys
FROM sys.indexes i WHERE i.object_id IN (OBJECT_ID('dbo.user_roles'),OBJECT_ID('dbo.user_branch_access')) AND i.is_unique=1 ORDER BY tableName,indexName FOR JSON PATH)
'@
    $foreignKeys = ReadJson @'
SELECT (SELECT OBJECT_NAME(f.parent_object_id) AS tableName,f.name AS constraintName,OBJECT_NAME(f.referenced_object_id) AS referencedTable,f.is_disabled AS disabled,f.is_not_trusted AS untrusted,
JSON_QUERY((SELECT pc.name AS parentColumn,rc.name AS referencedColumn FROM sys.foreign_key_columns fc JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id
JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id FOR JSON PATH)) AS columns
FROM sys.foreign_keys f WHERE f.parent_object_id IN (OBJECT_ID('dbo.user_roles'),OBJECT_ID('dbo.user_branch_access'),OBJECT_ID('dbo.role_permissions')) ORDER BY tableName,constraintName FOR JSON PATH)
'@
    $checks = ReadJson @'
SELECT (SELECT OBJECT_NAME(parent_object_id) AS tableName,name AS constraintName,definition,is_disabled AS disabled FROM sys.check_constraints
WHERE parent_object_id IN (OBJECT_ID('dbo.user_roles'),OBJECT_ID('dbo.user_branch_access'),OBJECT_ID('dbo.roles'),OBJECT_ID('dbo.role_permissions')) ORDER BY tableName,name FOR JSON PATH)
'@
    $triggers = ReadJson @'
SELECT (SELECT OBJECT_NAME(parent_id) AS tableName,name,is_disabled AS disabled FROM sys.triggers
WHERE parent_id IN (OBJECT_ID('dbo.user_roles'),OBJECT_ID('dbo.user_branch_access'),OBJECT_ID('dbo.roles'),OBJECT_ID('dbo.role_permissions')) ORDER BY tableName,name FOR JSON PATH)
'@
    $catalog = ReadJson @'
SELECT (SELECT r.role_code AS roleCode,r.is_system_role AS isSystemRole,r.is_active AS isActive,
JSON_QUERY((SELECT p.permission_code AS permissionCode,rp.data_scope AS dataScope FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id
WHERE rp.role_id=r.role_id ORDER BY p.permission_code,rp.data_scope FOR JSON PATH)) AS grants
FROM dbo.roles r ORDER BY r.role_code FOR JSON PATH)
'@
    $result = [ordered]@{ success=$true; scope='Read-only disposable SQL metadata/catalog; no UC010 mutation or authorization certification';
        identity=$identity; columns=@($columns | Where-Object { $null -ne $_ }); uniqueIndexes=@($indexes | Where-Object { $null -ne $_ });
        foreignKeys=@($foreignKeys | Where-Object { $null -ne $_ }); checkConstraints=@($checks | Where-Object { $null -ne $_ });
        triggers=@($triggers | Where-Object { $null -ne $_ }); roles=@($catalog | Where-Object { $null -ne $_ }) }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Receipt -Encoding UTF8
    Write-Output ('PASS: identity-guarded SELECT-only schema/catalog inspection; roles=' + @($catalog).Count)
}
finally { $connection.Dispose(); $budget.Dispose() }
