param([Parameter(Mandatory=$true)][string]$EnvironmentReceipt)
$ErrorActionPreference='Stop'
$e=Get-Content -LiteralPath $EnvironmentReceipt -Raw | ConvertFrom-Json
if(!$e.success -or !$e.createdByThisRun -or !$e.syntheticOnly -or $e.server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $e.database -cnotmatch '^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$'){throw 'Isolation receipt rejected'}
$c=New-Object System.Data.SqlClient.SqlConnection ('Data Source=lpc:.\SQLEXPRESS;Initial Catalog='+$e.database+';Integrated Security=true;Encrypt=false;Connect Timeout=5')
try{
 $c.Open();$q=$c.CreateCommand();$q.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner')";$reader=$q.ExecuteReader()
 if(!$reader.Read() -or $reader.GetString(0) -cne $e.database -or $reader.GetString(1) -cne $e.server -or $reader.GetString(2) -cne $e.owner){throw 'Owner mismatch'};$reader.Close()
 $tx=$c.BeginTransaction([System.Data.IsolationLevel]::Serializable);$q.Transaction=$tx
 try{
  $q.CommandText=@'
DECLARE @room bigint,@task bigint;
SELECT TOP(1) @room=room_id,@task=cleaning_task_id FROM dbo.room_inspections ORDER BY inspection_id;
IF EXISTS(SELECT 1 FROM dbo.cleaning_tasks WHERE task_no='FAST003_INVALID_CYCLE') THROW 51991,'No fixture replay',1;
INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,created_at)
 SELECT 'FAST003_INVALID_CYCLE',room_id,cleaning_type_id,'COMPLETED',created_by,SYSUTCDATETIME() FROM dbo.cleaning_tasks WHERE cleaning_task_id=@task;
DECLARE @other bigint=CONVERT(bigint,SCOPE_IDENTITY());
UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id=@other WHERE room_id=@room AND cleaning_task_id=@task;
IF @@ROWCOUNT<>1 THROW 51991,'Fixture cycle binding changed unexpectedly',1;
'@
  [void]$q.ExecuteNonQuery();$tx.Commit();Write-Output 'PASS owned negative current-cycle fixture committed; never production authority'
 }catch{$tx.Rollback();throw}
}finally{$c.Dispose()}
