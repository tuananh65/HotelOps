param([switch]$Apply)
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$reviewed=@(
    @{Name='02_NotificationProposal.sql';Hash='A516545C1986561CAD8C011B7CB95BEEA45B52DC72142B4503455CB144FA2BAA';Boundary="THROW 51899,'FS013 isolated target/owner required.',1;"},
    @{Name='07_ExecutableCandidate.sql';Hash='FE7655A6B98EDA6D1921FB7C1157D18FE4E8B957083F2798C54B5C686AAFBACC';Boundary="THROW 51909,'FS013 isolated target required',1;"}
)
$packages=@()
foreach($item in $reviewed){
    $path=Join-Path $workspace ('scripts/database/release/fs013-candidate/'+$item.Name)
    if((Get-FileHash $path -Algorithm SHA256).Hash -ne $item.Hash){throw 'Reviewed assignment package changed.'}
    $sql=[IO.File]::ReadAllText($path)
    $start=$sql.IndexOf('IF DB_NAME()')
    $end=$sql.IndexOf($item.Boundary,$start)
    if($start -lt 0 -or $end -lt 0){throw 'Identity boundary changed.'}
    # Replace only the obsolete disposable identity preamble. All graph checks,
    # immutable triggers, uniqueness and the caller-owned transaction guard remain.
    $sql=$sql.Substring(0,$start)+$sql.Substring($end+$item.Boundary.Length)
    $packages+=,$sql
}
if(!$Apply){Write-Output 'Prepared Cleaning Assignment ledger/guard installation for approved HotelOpsDB; use -Apply.';return}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
try{
    $connection.Open()
    $command=$connection.CreateCommand()
    $command.CommandText=@'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'meita\MSSQLSERVER01'
 AND DB_NAME()=N'HotelOpsDB' AND EXISTS(SELECT 1 FROM sys.database_recovery_status
 WHERE database_id=DB_ID() AND database_guid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E') THEN 1 ELSE 0 END
'@
    try{if([int]$command.ExecuteScalar() -ne 1){throw 'Approved development identity required.'}}finally{$command.Dispose()}
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $command=$connection.CreateCommand();$command.Transaction=$transaction
    $command.CommandText=@'
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:CleaningAssignment:Install:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0 THROW 51985,'Assignment installation busy.',1;
SELECT COUNT(*) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningAssignment.V1';
'@
    try{$installed=[int]$command.ExecuteScalar()}finally{$command.Dispose()}
    if($installed -gt 0){
        # Later Cleaning workflows may extend these shared tables/triggers. A rerun
        # must not replace their definitions with the older assignment-only version.
        $transaction.Commit()
        Write-Output 'ALREADY_INSTALLED: retained current Cleaning Assignment schema and later workflow extensions.'
        return
    }
    foreach($sql in $packages){
        foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*\r?$')){
            if([string]::IsNullOrWhiteSpace($batch)){continue}
            $command=$connection.CreateCommand();$command.Transaction=$transaction;$command.CommandTimeout=30;$command.CommandText=$batch
            try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
        }
    }
    $command=$connection.CreateCommand();$command.Transaction=$transaction
    $command.CommandText=@'
IF OBJECT_ID('dbo.fs009_verify_assignment_guard','P') IS NULL
 OR (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fs009_publication_immutable','TR_fs009_operation_immutable','TR_fs009_publication_source') AND is_disabled=0)<>3
 THROW 51985,'Cleaning assignment guard installation incomplete.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningAssignment.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningAssignment.V1',@value=N'1';
'@
    try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
    $transaction.Commit()
    Write-Output 'APPLIED: Cleaning Assignment ledger and commit guard. No role grants, assignments or notifications created.'
}catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{if($transaction){$transaction.Dispose()};$connection.Dispose()}
