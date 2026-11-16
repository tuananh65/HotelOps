param([switch]$Apply)
$ErrorActionPreference='Stop'
$reviewed=@(
    @{File='04-Fe04Closure.sql';Hash='B1EFE4DFD1D09FC3C6E5EF8B872A277B3392037C16207E45590D783E15B7B7E7';Marker='HotelOps.CleaningClosure.V1'},
    @{File='05-Fe04Cancellation.sql';Hash='7D61CD67E15BA9710CB30612470E5AA674EB9EECEDF4568B1346168DF1B2F8EF';Marker='HotelOps.CleaningCancellation.V1'},
    @{File='06-Fe04CancellationReadiness.sql';Hash='7E54344D7D50C1322BD8265860C1299F675F3055FE33D5720A4834F299E5E6D9';Marker='HotelOps.CleaningCancellationReadiness.V1'}
)
foreach($item in $reviewed){
    $path=Join-Path $PSScriptRoot $item.File
    if((Get-FileHash $path -Algorithm SHA256).Hash -ne $item.Hash){throw 'Reviewed Cleaning migration changed.'}
    $item.Sql=[IO.File]::ReadAllText($path)
    $item.Trigger=@([regex]::Split($item.Sql,'(?im)^\s*GO\s*\r?$')|Where-Object {$_.Trim() -match '^(?:CREATE|ALTER) TRIGGER dbo\.TR_dev3_closure_publication_source'})
    if($item.Trigger.Count -ne 1){throw 'Reviewed trigger boundary changed.'}
}
function Normalize([string]$value){return ($value.Trim().Replace("`r`n","`n") -creplace '^(?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+TRIGGER ','CREATE TRIGGER ')}
if(!$Apply){Write-Output 'Prepared Cleaning Cancellation and effective-readiness integration on approved development database.';return}
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
    Execute "SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:CleaningAssignment:Install:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51985,'Cleaning installation busy.',1;"
    for($i=1;$i -lt $reviewed.Count;$i++){
        $item=$reviewed[$i];$previous=$reviewed[$i-1]
        $existing=[string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='$($item.Marker)'")
        if($existing){if($existing -cne $item.Hash){throw 'Existing cancellation capability differs.'};continue}
        if([string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='$($previous.Marker)'") -cne $previous.Hash){throw 'Predecessor capability missing.'}
        $actual=[string](Scalar "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.TR_dev3_closure_publication_source'))")
        if((Normalize $actual) -cne (Normalize $previous.Trigger[0])){throw 'Source-proof trigger differs; no overwrite.'}
        foreach($batch in [regex]::Split($item.Sql,'(?im)^\s*GO\s*\r?$')){if($batch.Trim()){Execute $batch}}
        Execute "EXEC sys.sp_addextendedproperty @name=N'$($item.Marker)',@value=N'$($item.Hash)';"
    }
    Execute @'
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_dev3_cancellation_event','CK_dev3_readiness_start') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_dev3_closure_publication_source') AND is_disabled=0)
 THROW 51985,'Cancellation guards incomplete.',1;
'@
    $transaction.Commit()
    Write-Output 'APPLIED: Cleaning Cancellation/readiness contracts. Existing assignments, publications, grants and room states retained.'
}catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{if($transaction){$transaction.Dispose()};$connection.Dispose()}
