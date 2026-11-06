$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot '.private/processes.json')){throw 'Stop owned live services before permission probe.'}
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']='.\SQLEXPRESS';$builder['Initial Catalog']='HotelOps_Canonical_20261106_f110001_Fresh';$builder['Integrated Security']=$true;$builder['Encrypt']=$true;$builder['TrustServerCertificate']=$true
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(30000)
try {
 $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
 $command=$connection.CreateCommand();$command.CommandTimeout=20
 $command.CommandText=@'
IF DB_NAME()<>N'HotelOps_Canonical_20261106_f110001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))<>'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS011.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS011') THROW 51990,'Wrong isolated target',1;
IF USER_ID('fs011_deny_probe') IS NOT NULL THROW 51991,'Existing principal must not be overwritten',1;
BEGIN TRANSACTION;
BEGIN TRY
 CREATE USER fs011_deny_probe WITHOUT LOGIN;
 DENY INSERT,UPDATE,DELETE ON dbo.cleaning_assignments TO fs011_deny_probe;
 DECLARE @denied bit=0;
 EXECUTE AS USER='fs011_deny_probe';
 BEGIN TRY
  UPDATE dbo.cleaning_assignments SET reason=reason;
 END TRY
 BEGIN CATCH
  IF ERROR_NUMBER()=229 SET @denied=1;
 END CATCH;
 REVERT;
 IF @denied<>1 THROW 51992,'Restricted principal was not denied',1;
 ROLLBACK TRANSACTION;
 SELECT 1;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
'@
 try{$result=$command.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult();if([int]$result -ne 1){throw 'Permission probe failed'}}finally{$command.Dispose()}
 [ordered]@{level='COMPONENT_REAL_SQL';isolatedDenyUpdate='PASS_229';principalAndGrantTransaction='ROLLED_BACK';runtimePrincipalChanged=$false;sharedOrProductionPermissionsChanged=$false;limitation='Table DENY also blocks direct EF writes; signed-procedure or restricted owner facade needs accepted contract before integration'}|ConvertTo-Json
}finally{$connection.Dispose();$budget.Dispose()}
