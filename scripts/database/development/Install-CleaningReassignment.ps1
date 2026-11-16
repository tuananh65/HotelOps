param([switch]$Apply)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'CleaningReassignmentV1.sql'
$hash='034304E6B78CB4F39194F7E8811FCA835D0C3A0D22E6D8AB5F962E43D6DEF700'
if((Get-FileHash $path -Algorithm SHA256).Hash -ne $hash){throw 'Reviewed reassignment package changed.'}
if(!$Apply){Write-Output 'Prepared additive Cleaning Reassignment DEV installation.';return}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
function Scalar([string]$sql){$c=$connection.CreateCommand();$c.Transaction=$transaction;$c.CommandText=$sql;$c.CommandTimeout=30;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$sql){$c=$connection.CreateCommand();$c.Transaction=$transaction;$c.CommandText=$sql;$c.CommandTimeout=30;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
try{
    $connection.Open()
    if([int](Scalar @'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'meita\MSSQLSERVER01'
 AND DB_NAME()=N'HotelOpsDB' AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E') THEN 1 ELSE 0 END
'@) -ne 1){throw 'Approved development identity required.'}
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    Execute "SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:CleaningAssignment:Install:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51985,'Cleaning installation busy.',1;"
    $existing=[string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningReassignment.Package'")
    if($existing){if($existing -cne $hash){throw 'Existing reassignment package differs.'}}
    else{
        Execute @'
IF OBJECT_ID('dbo.workdays','U') IS NULL OR OBJECT_ID('dbo.work_intervals','U') IS NULL
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningCancellationReadiness.V1')
 OR OBJECT_ID('dbo.fs013_reassignment_operations') IS NOT NULL
 OR OBJECT_ID('dbo.fs013_reassignment_intents') IS NOT NULL
 OR OBJECT_ID('dbo.fs013_reassign_with_evidence') IS NOT NULL
 OR OBJECT_ID('dbo.fs013_verify_reassignment') IS NOT NULL
 THROW 51985,'Prerequisite missing or existing reassignment storage; no overwrite.',1;
'@
        foreach($batch in [regex]::Split([IO.File]::ReadAllText($path),'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch}}
        Execute "EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningReassignment.V1',@value=N'1'; EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningReassignment.Package',@value=N'$hash';"
    }
    Execute @'
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.fs013_reassignment_operations'),OBJECT_ID('dbo.fs013_reassignment_intents')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>8
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_cleaning_reassignment_immutable') AND is_disabled=0)
 THROW 51985,'Reassignment integrity guards missing.',1;
'@
    $transaction.Commit()
    Write-Output 'APPLIED: Reassignment receipts, durable intents and transaction procedures. No business rows or grants changed.'
}catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{if($transaction){$transaction.Dispose()};$connection.Dispose()}
