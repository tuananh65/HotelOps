param([ValidatePattern('^HotelOpsCompact(DB|WorkdayTestOfficial[0-9]+)$')][string]$Database='HotelOpsCompactDB')
$ErrorActionPreference='Stop'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try{
 $db.OpenAsync().GetAwaiter().GetResult();$q=$db.CreateCommand();$q.CommandTimeout=90;$q.CommandText=@'
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>57 THROW 51990,'Table count mismatch',1;
IF EXISTS(SELECT 1 FROM inventory_documents WHERE document_no LIKE 'TEST-ROOM-261124-%' OR document_id IN(440,441)) THROW 51990,'Test documents remain',1;
IF EXISTS(SELECT 1 FROM stock_movements GROUP BY warehouse_id,item_id HAVING SUM(signed_base_qty)<0) THROW 51990,'Negative retained stock',1;
IF EXISTS(SELECT 1 FROM stock_movements WHERE item_id=222) OR EXISTS(SELECT 1 FROM assets WHERE asset_code LIKE 'MIG222-%') THROW 51990,'Synthetic kettle ownership remains',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1) THROW 51990,'Disabled/untrusted database guards',1;
IF NOT EXISTS(SELECT 1 FROM audit_logs WHERE operation_key='be99df91-a554-482a-b90c-ce71b5de6066' AND ISJSON(before_json)=1 AND (SELECT COUNT(*) FROM OPENJSON(before_json,'$.documents'))=196) THROW 51990,'Full cleanup archive missing',1;
IF EXISTS(SELECT user_id,username,password_hash FROM HotelOpsCompactWorkdayTestOfficial2611252122.dbo.users EXCEPT SELECT user_id,username,password_hash FROM users) THROW 51990,'Credentials changed',1;
SELECT (SELECT COUNT(*) FROM users) users_count,(SELECT COUNT(*) FROM inventory_items) item_count,(SELECT COUNT(*) FROM inventory_documents) retained_documents,
 (SELECT COUNT(*) FROM stock_movements) retained_movements,(SELECT COUNT(*) FROM work_items) retained_work,
 (SELECT COUNT(*) FROM user_roles ur JOIN roles r ON r.role_id=ur.role_id WHERE r.code='MAINTENANCE_STAFF' AND ur.revoked_at IS NULL AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())) maintenance_staff,
 (SELECT COUNT(*) FROM OPENJSON((SELECT before_json FROM audit_logs WHERE operation_key='be99df91-a554-482a-b90c-ce71b5de6066'),'$.documents')) removed_documents;
'@
 $reader=$q.ExecuteReaderAsync().GetAwaiter().GetResult();$result=[ordered]@{database=$Database;at=[DateTime]::UtcNow.ToString('o');tables=57;credentialsPreserved=$true;trustedGuards=$true;nonnegativeStock=$true}
 while($reader.Read()){for($i=0;$i -lt $reader.FieldCount;$i++){$result[$reader.GetName($i)]=$reader.GetValue($i)}};$reader.Close()
 $result|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot "../../artifacts/official-v2-upgrade/$Database-verification.json") -Encoding UTF8
 $result|ConvertTo-Json -Compress
}finally{$db.Dispose()}
