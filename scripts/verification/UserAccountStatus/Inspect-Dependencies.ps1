param([Parameter(Mandatory=$true)][string]$Database,
      [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
if ($Database -cnotmatch '^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$' -or (Test-Path -LiteralPath $OutputPath)) {
    throw 'Owned trial and NEW local output required.'
}
$server = 'DESKTOP-JI8U50T\SQLEXPRESS'
$connection = New-Object System.Data.SqlClient.SqlConnection "Data Source=$server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
try {
    $connection.Open()
    $guard = $connection.CreateCommand()
    $guard.CommandText = "SELECT CASE WHEN DB_NAME()=@db AND CONVERT(nvarchar(128),@@SERVERNAME)=@server AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128),value)='Task17') THEN 1 ELSE 0 END"
    [void]$guard.Parameters.AddWithValue('@db',$Database)
    [void]$guard.Parameters.AddWithValue('@server',$server)
    try { if ($guard.ExecuteScalar() -ne 1) { throw 'Trial guard failed.' } } finally { $guard.Dispose() }
    $command = $connection.CreateCommand()
    # Metadata only: no user, password, session or operational payload is selected.
    $command.CommandText = @'
SELECT t.name AS table_name,c.name AS column_name,f.name AS foreign_key,
       rc.name AS referenced_column,f.is_disabled,f.is_not_trusted
FROM sys.foreign_key_columns fc
JOIN sys.foreign_keys f ON f.object_id=fc.constraint_object_id
JOIN sys.tables t ON t.object_id=fc.parent_object_id
JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=fc.parent_column_id
JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
WHERE fc.referenced_object_id=OBJECT_ID('dbo.users')
ORDER BY t.name,c.name;
SELECT t.name AS table_name,c.name AS constraint_name,c.definition,c.is_disabled,c.is_not_trusted
FROM sys.check_constraints c JOIN sys.tables t ON t.object_id=c.parent_object_id
WHERE t.object_id IN(SELECT parent_object_id FROM sys.foreign_key_columns WHERE referenced_object_id=OBJECT_ID('dbo.users'))
   OR t.name IN('users','cleaning_tasks','operational_tasks','internal_requests','material_requests','asset_handovers','material_request_approval_cycles')
ORDER BY t.name,c.name;
'@
    $reader = $command.ExecuteReader()
    $fks = @(); $constraints = @()
    try {
        while ($reader.Read()) {
            $fks += [ordered]@{table=$reader.GetString(0);column=$reader.GetString(1);foreignKey=$reader.GetString(2);referencedColumn=$reader.GetString(3);disabled=$reader.GetBoolean(4);untrusted=$reader.GetBoolean(5)}
        }
        [void]$reader.NextResult()
        while ($reader.Read()) {
            $constraints += [ordered]@{table=$reader.GetString(0);constraint=$reader.GetString(1);definition=$reader.GetString(2);disabled=$reader.GetBoolean(3);untrusted=$reader.GetBoolean(4)}
        }
    } finally { $reader.Dispose(); $command.Dispose() }
    [ordered]@{database=$Database;metadataOnly=$true;userForeignKeys=$fks;checkConstraints=$constraints;openWorkPredicateApproved=$false} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    Write-Output "PASS: metadata-only inventory: $($fks.Count) user foreign keys, $($constraints.Count) CHECK constraints. Open-work semantics NOT VERIFIED."
} finally { $connection.Dispose() }
