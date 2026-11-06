$ErrorActionPreference='Stop'
$target='HotelOps_Canonical_20261106_f120001_Fresh'
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']='.\SQLEXPRESS';$builder['Initial Catalog']=$target;$builder['Integrated Security']=$true;$builder['Encrypt']=$true;$builder['TrustServerCertificate']=$true
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(30000)
try{
 $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
 $command=$connection.CreateCommand()
 $command.CommandText=@'
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f120001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))<>N'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS012.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS012') THROW 51940,'Wrong trial',1;
SELECT (SELECT DB_NAME() AS databaseName,IS_SRVROLEMEMBER('sysadmin') AS verificationHostIsSysadmin,
 (SELECT COUNT(*) FROM sys.check_constraints WHERE name LIKE 'CK_FS012%Fault%') AS residualFaultConstraintCount,
 JSON_QUERY((SELECT name,type_desc,authentication_type_desc FROM sys.database_principals WHERE name='fs012_app' FOR JSON PATH)) AS principals,
 JSON_QUERY((SELECT p.permission_name,p.state_desc,OBJECT_NAME(p.major_id) AS objectName,COL_NAME(p.major_id,p.minor_id) AS columnName FROM sys.database_permissions p WHERE grantee_principal_id=USER_ID('fs012_app') ORDER BY p.major_id,p.minor_id,p.permission_name FOR JSON PATH)) AS permissions,
 JSON_QUERY((SELECT name,is_disabled FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.cleaning_assignments') FOR JSON PATH)) AS assignmentTriggers,
 JSON_QUERY((SELECT name,is_disabled FROM sys.check_constraints WHERE name LIKE 'CK_FS012%Fault%' FOR JSON PATH)) AS residualFaultConstraints,
 JSON_QUERY((SELECT p.name,sm.execute_as_principal_id,CONVERT(varchar(64),HASHBYTES('SHA2_256',sm.definition),2) AS definitionSqlUnicodeSHA256 FROM sys.procedures p JOIN sys.sql_modules sm ON sm.object_id=p.object_id WHERE p.name LIKE 'fs012%' FOR JSON PATH)) AS modules
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
'@
 try{
  $result=[string]$command.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult();$parsed=$result|ConvertFrom-Json
  $command.CommandText="EXECUTE AS USER='fs012_app'; BEGIN TRY SELECT (SELECT p.name,HAS_PERMS_BY_NAME(QUOTENAME(SCHEMA_NAME(p.schema_id))+'.'+QUOTENAME(p.name),'OBJECT','EXECUTE') AS effectiveExecute FROM sys.procedures p WHERE SCHEMA_NAME(p.schema_id)='dbo' ORDER BY p.name FOR JSON PATH); REVERT; END TRY BEGIN CATCH REVERT; THROW; END CATCH;"
  $effective=[string]$command.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()
  $parsed|Add-Member -NotePropertyName effectiveDboProcedureExecute -NotePropertyValue @($effective|ConvertFrom-Json|ForEach-Object {$_})
  $parsed|ConvertTo-Json -Depth 8
  if($parsed.residualFaultConstraintCount -ne 0){throw 'Residual fault constraints remain'}
 }finally{$command.Dispose()}
}finally{$connection.Dispose();$budget.Dispose()}
