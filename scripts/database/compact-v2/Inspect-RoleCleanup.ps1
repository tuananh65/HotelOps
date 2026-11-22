$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
if($builder.InitialCatalog -ne 'HotelOpsCompactDB'){throw 'Unexpected database'}
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $queries=@{
 audit="SELECT audit_id,CONVERT(varchar(36),operation_key) operation_key,occurred_at,ISJSON(before_json) before_valid,ISJSON(after_json) after_valid, (SELECT COUNT(*) FROM OPENJSON(before_json,'$.roles')) before_roles,(SELECT COUNT(*) FROM OPENJSON(after_json,'$.roles')) after_roles FROM dbo.audit_logs WHERE action='ROLE_CLEANUP' AND entity_key='inactive-role-cleanup-20261122'";
 verification="SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) invalid_foreign_keys,(SELECT COUNT(*) FROM dbo.audit_logs WHERE action='ROLE_CLEANUP' AND entity_key='inactive-role-cleanup-20261122') cleanup_audits; SELECT audit_id,CONVERT(varchar(36),operation_key) operation_key,occurred_at,ISJSON(before_json) before_valid,ISJSON(after_json) after_valid FROM dbo.audit_logs WHERE action='ROLE_CLEANUP' AND entity_key='inactive-role-cleanup-20261122'";
 roles='SELECT r.role_id,r.code,r.name,r.description,r.is_active,(SELECT COUNT(*) FROM dbo.user_roles u WHERE u.role_id=r.role_id) assignments,(SELECT COUNT(*) FROM dbo.user_roles u WHERE u.role_id=r.role_id AND u.revoked_at IS NULL) unrevoked,(SELECT COUNT(*) FROM dbo.role_permissions p WHERE p.role_id=r.role_id) grants FROM dbo.roles r ORDER BY r.role_id';
 references="SELECT fk.name,OBJECT_SCHEMA_NAME(fk.parent_object_id) schema_name,OBJECT_NAME(fk.parent_object_id) table_name,c.name column_name,fk.delete_referential_action_desc FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=fk.object_id JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id WHERE fk.referenced_object_id=OBJECT_ID('dbo.roles')";
 triggers="SELECT name,OBJECT_DEFINITION(object_id) definition FROM sys.triggers WHERE parent_id IN(OBJECT_ID('dbo.roles'),OBJECT_ID('dbo.role_permissions'),OBJECT_ID('dbo.user_roles'))";
 }
 foreach($key in $queries.Keys){
  $cmd=$connection.CreateCommand();$cmd.CommandText=$queries[$key];$reader=$cmd.ExecuteReader();$rows=@()
  while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};$rows+= [pscustomobject]$row};$reader.Close()
  @{section=$key;rows=$rows}|ConvertTo-Json -Depth 5
 }
}finally{$connection.Dispose()}
