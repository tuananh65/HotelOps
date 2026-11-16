param([switch]$Apply)
$ErrorActionPreference='Stop'
$resumeHash='56B2F290C05D30F1E4DE0C01AC9E01FFA582455A7A8F6AD8EFBBDC8F0CDD5CD9'
$closureHash='B1EFE4DFD1D09FC3C6E5EF8B872A277B3392037C16207E45590D783E15B7B7E7'
$assignmentHash='FE7655A6B98EDA6D1921FB7C1157D18FE4E8B957083F2798C54B5C686AAFBACC'
$resumePath=Join-Path $PSScriptRoot '03-Fe04Resume.sql'
$closurePath=Join-Path $PSScriptRoot '04-Fe04Closure.sql'
$assignmentPath=Join-Path $PSScriptRoot '../release/fs013-candidate/07_ExecutableCandidate.sql'
foreach($item in @(@{Path=$resumePath;Hash=$resumeHash},@{Path=$closurePath;Hash=$closureHash},@{Path=$assignmentPath;Hash=$assignmentHash})){
    if((Get-FileHash $item.Path -Algorithm SHA256).Hash -ne $item.Hash){throw 'Reviewed source package changed.'}
}
$resume=[IO.File]::ReadAllText($resumePath)
$closure=[IO.File]::ReadAllText($closurePath)
# The existing assignment ledger is retained. Apply only its closure extension,
# attribution writer and source-proof trigger; keep the existing immutable triggers.
$start=$closure.IndexOf("IF COL_LENGTH('dbo.notification_publications','occurrence_key')")
$end=$closure.IndexOf('CREATE UNIQUE INDEX UX_fs009_publication_operation',$start)
$trigger=$closure.IndexOf('CREATE TRIGGER dbo.TR_dev3_closure_publication_source',$end)
if($start -lt 0 -or $end -lt 0 -or $trigger -lt 0){throw 'Reviewed closure boundaries changed.'}
$extension=$closure.Substring($start,$end-$start)
$closureTrigger=$closure.Substring($trigger)
$assignment=[IO.File]::ReadAllText($assignmentPath)
$guardStart=$assignment.IndexOf('CREATE OR ALTER TRIGGER dbo.TR_fs009_publication_source')
if($guardStart -lt 0){throw 'Reviewed assignment trigger missing.'}
$oldGuard=$assignment.Substring($guardStart)
$newGuard=$oldGuard.Replace('FROM inserted p WHERE NOT EXISTS(',"FROM inserted p WHERE p.event_type='CLEANING_TASK_ASSIGNED' AND NOT EXISTS(")
if($newGuard -eq $oldGuard){throw 'Assignment guard extension boundary changed.'}
function Normalize([string]$text){return ($text.Trim().Replace("`r`n","`n") -creplace '^(?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+TRIGGER ','CREATE TRIGGER ')}
if(!$Apply){Write-Output 'Prepared additive Cleaning Resume/Closure integration over the existing assignment ledger.';return}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
function Scalar([string]$sql){$c=$connection.CreateCommand();$c.Transaction=$transaction;$c.CommandTimeout=30;$c.CommandText=$sql;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$sql){$c=$connection.CreateCommand();$c.Transaction=$transaction;$c.CommandTimeout=30;$c.CommandText=$sql;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
try{
    $connection.Open()
    if([int](Scalar @'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'meita\MSSQLSERVER01'
 AND DB_NAME()=N'HotelOpsDB' AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E') THEN 1 ELSE 0 END
'@) -ne 1){throw 'Approved development identity required.'}
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    Execute "SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:CleaningAssignment:Install:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51985,'Cleaning installation busy.',1;"
    $existing=[string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningClosure.V1'")
    if($existing){
        if($existing -cne $closureHash){throw 'Existing closure contract differs.'}
        $transaction.Commit();Write-Output 'ALREADY_INSTALLED: retained closure and later workflow extensions.';return
    }
    Execute @'
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningAssignment.V1' AND CONVERT(nvarchar(20),value)='1')
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.notification_operation_reservations'),OBJECT_ID('dbo.notification_publications'),OBJECT_ID('dbo.notification_delivery_intents')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>6
 OR (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fs009_publication_immutable','TR_fs009_operation_immutable','TR_fs009_publication_source') AND is_disabled=0)<>3
 THROW 51985,'Assignment ledger prerequisites missing.',1;
'@
    $actual=[string](Scalar "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.TR_fs009_publication_source'))")
    if((Normalize $actual) -cne (Normalize $oldGuard)){throw 'Assignment source trigger differs; no overwrite.'}
    $resumeMarker=[string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningResume.V1'")
    if($resumeMarker -and $resumeMarker -cne $resumeHash){throw 'Existing Resume contract differs.'}
    if(!$resumeMarker){Execute $resume;Execute "EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningResume.V1',@value=N'$resumeHash';"}
    Execute $extension
    Execute $closureTrigger
    Execute $newGuard
    Execute "EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningClosure.V1',@value=N'$closureHash';"
    $transaction.Commit()
    Write-Output 'APPLIED: Cleaning Resume/Closure storage and provenance guards. Existing ledger/data retained; no business records or grants created.'
}catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{if($transaction){$transaction.Dispose()};$connection.Dispose()}
