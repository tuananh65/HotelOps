param([Parameter(Mandatory=$true)][string]$EnvironmentReceipt,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$e=Get-Content -LiteralPath $EnvironmentReceipt -Raw|ConvertFrom-Json
if(!$e.success -or !$e.createdByThisRun -or !$e.syntheticOnly -or $e.server -cne 'DESKTOP-JI8U50T\SQLEXPRESS' -or $e.database -cnotmatch '^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$'){throw 'Owned isolation required'}
$c=[Data.SqlClient.SqlConnection]::new(('Data Source=lpc:.\SQLEXPRESS;Initial Catalog='+$e.database+';Integrated Security=true;Encrypt=false'))
try{$c.Open();$q=$c.CreateCommand();$q.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner')";$r=$q.ExecuteReader();if(!$r.Read() -or $r.GetString(0) -cne $e.database -or $r.GetString(1) -cne $e.server -or $r.GetString(2) -cne $e.owner){throw 'Actual identity rejected'};$r.Dispose()
 if($env:FE05_PREPARE_HTTP_FIXTURE -ceq 'YES'){
 $q.CommandText="IF (SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05.AssignmentDomain') <> 'FAST017_COMPONENT_NOT_ACCEPTED' THROW 51997,'Owned component marker missing',1; UPDATE i SET status='CANCELLED',workflow_status='CANCELLED' FROM dbo.room_inspections i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id JOIN dbo.rooms r ON r.room_id=i.room_id WHERE t.task_no='FAST_TASK_2' AND r.room_number='FAST2' AND i.workflow_status='ASSIGNED';";[void]$q.ExecuteNonQuery()
 }
 $q.CommandText="SELECT (SELECT COUNT(*) FROM dbo.room_inspections) AS inspections,(SELECT COUNT(*) FROM dbo.inspection_supervisor_bindings) AS bindings,(SELECT COUNT(*) FROM dbo.audit_logs) AS audits,(SELECT COUNT(*) FROM dbo.room_inspections WHERE workflow_status='ASSIGNED') AS assigned,(SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='room_inspections') AS inspectionAudits,(SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='inspection_supervisor_bindings') AS bindingAudits,(SELECT entity_type,action,COUNT(*) AS records FROM dbo.audit_logs GROUP BY entity_type,action FOR JSON PATH) AS auditKinds FOR JSON PATH,WITHOUT_ARRAY_WRAPPER"
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($Output),[string]$q.ExecuteScalar(),[Text.UTF8Encoding]::new($false))
}finally{$c.Dispose()}
