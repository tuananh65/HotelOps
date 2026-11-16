param([switch]$Apply)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot '../../verification/CanonicalDatabase/MaintenanceMaterialUsage.sql'
$hash='6D252E84FA3422EA2988A396A35883B517F64AF46A0CCBE38BB34BFE8ADE45EE'
if((Get-FileHash $path -Algorithm SHA256).Hash -ne $hash){throw 'Reviewed material usage contract changed.'}
$sql=[IO.File]::ReadAllText($path)
$old="N'`$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'"
if(($sql.Split(@($old),[StringSplitOptions]::None)).Length -ne 2){throw 'Reviewed identity guard changed.'}
$sql=$sql.Replace($old,"N'`$(ExpectedDatabase)' <> N'HotelOpsDB'")
$sql=$sql.Replace('$(ExpectedServer)','meita\MSSQLSERVER01').Replace('$(ExpectedDatabase)','HotelOpsDB')
$sql=$sql.Replace('$(ExpectedDatabaseGuid)','BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E').Replace('$(ApplyChanges)','1')
if(!$Apply){Write-Output 'Prepared additive Maintenance Material Usage installation on approved DEV.';return}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
function Scalar([string]$text){$c=$connection.CreateCommand();$c.Transaction=$transaction;$c.CommandText=$text;$c.CommandTimeout=30;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$text){$c=$connection.CreateCommand();$c.Transaction=$transaction;$c.CommandText=$text;$c.CommandTimeout=30;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
try{
    $connection.Open()
    if([int](Scalar @'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'meita\MSSQLSERVER01'
 AND DB_NAME()=N'HotelOpsDB' AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E') THEN 1 ELSE 0 END
'@) -ne 1){throw 'Approved development identity required.'}
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    Execute "SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceMaterialUsage:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51844,'Material usage installation busy.',1;"
    $existing=[string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaintenanceMaterialUsage.V1'")
    if($existing){if($existing -cne $hash){throw 'Existing material usage package differs.'}}
    else{
        # The package refuses existing tables. No adoption or overwriting of unknown storage.
        Execute $sql
        Execute "EXEC sys.sp_addextendedproperty @name=N'HotelOps.MaintenanceMaterialUsage.V1',@value=N'$hash';"
    }
    Execute @'
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_material_usages')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>6
 OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_material_usages')
 AND name IN('CK_material_usages_quantity','CK_material_usages_base_factor') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_material_usages')
 AND name='UQ_material_usages_log_issue' AND is_unique=1 AND is_disabled=0)
 THROW 51844,'Material usage integrity guards missing.',1;
'@
    $transaction.Commit()
    Write-Output 'APPLIED: Maintenance usage table, six restrictive FKs, quantity constraints and indexes. No inventory, business rows or grants changed.'
}catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{if($transaction){$transaction.Dispose()};$connection.Dispose()}
