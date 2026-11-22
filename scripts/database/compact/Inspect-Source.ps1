param(
    [Parameter(Mandatory = $true)][string]$Server,
    [string]$Database = 'HotelOpsDB',
    [string]$OutputPath = 'artifacts/compact-db/source-inventory.json'
)
$ErrorActionPreference = 'Stop'
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = $Server
$builder['Initial Catalog'] = $Database
$builder['Integrated Security'] = $true
$builder['Encrypt'] = $true
$builder['TrustServerCertificate'] = $true
$builder['Connect Timeout'] = 10
$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 30
    $command.CommandText = @'
SET NOCOUNT ON;
SELECT
  DB_NAME() AS database_name,
  CONVERT(varchar(40),SERVERPROPERTY('ProductVersion')) AS server_version,
  (SELECT SCHEMA_NAME(t.schema_id) AS schema_name,t.name,
    (SELECT c.name,ty.name AS data_type,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity
     FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
     WHERE c.object_id=t.object_id ORDER BY c.column_id FOR JSON PATH) AS columns,
    (SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints
     WHERE parent_object_id=t.object_id ORDER BY name FOR JSON PATH) AS checks,
    (SELECT fk.name,OBJECT_NAME(fk.referenced_object_id) AS target_table,
       COL_NAME(fc.parent_object_id,fc.parent_column_id) AS source_column,
       COL_NAME(fc.referenced_object_id,fc.referenced_column_id) AS target_column,
       fk.is_disabled,fk.is_not_trusted
     FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fk.object_id=fc.constraint_object_id
     WHERE fk.parent_object_id=t.object_id ORDER BY fk.name,fc.constraint_column_id FOR JSON PATH) AS foreign_keys
   FROM sys.tables t WHERE t.is_ms_shipped=0 ORDER BY t.name FOR JSON PATH) AS tables,
  (SELECT type_desc,COUNT(*) AS object_count FROM sys.objects
   WHERE is_ms_shipped=0 AND type IN ('V','P','TR','FN','IF','TF') GROUP BY type_desc FOR JSON PATH) AS objects
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
'@
    $reader = $command.ExecuteReader()
    $json = New-Object System.Text.StringBuilder
    while ($reader.Read()) { [void]$json.Append($reader.GetString(0)) }
    $reader.Dispose()
    $parsed = $json.ToString() | ConvertFrom-Json
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),($parsed | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    Write-Output ("Inventory: {0}; tables: {1}; output: {2}" -f $parsed.database_name,$parsed.tables.Count,$OutputPath)
} finally { $connection.Dispose() }
