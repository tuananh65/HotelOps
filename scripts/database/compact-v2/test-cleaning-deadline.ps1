# Called while the assigned cleaning fixture is still unaccepted.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$detail=Call GET "/api/v2/work/$($clean.workId)" $null $head
$assigned=[datetime]$detail.assignments[0].fields.assignedAt
Check 'server deadline is exactly thirty minutes from assignment' ((Sql "SELECT DATEDIFF_BIG(microsecond,a.assigned_at,w.due_at) delta FROM work_items w JOIN work_assignments a ON a.work_id=w.work_id AND a.assignment_status='ACTIVE' WHERE w.work_id=$($clean.workId)").Rows[0].delta-eq 1800000000)
foreach($minutes in @(9,31)){
 $null=WorkAction $clean assign $head @{userId=$hkId;durationMinutes=$minutes;reason=$null} 400
}
$null=WorkAction $clean assign $head @{userId=$hkId;durationMinutes=$null;reason=$null} 400
Check 'assignment duration rejects missing and out-of-range values' $true
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INCIDENT';title='Staff forbidden incident';roomId=$room.roomId} $hk 403
Check 'HK staff incident creation denied by server' $true
$clean=(WorkAction $clean assign $head @{userId=$hkId;durationMinutes=10;reason=$null}).fields
$assignment=(Call GET "/api/v2/work/$($clean.workId)" $null $head).assignments|Where-Object {$_.fields.assignmentStatus-eq 'ACTIVE'}
Check 'reasonless reassignment gets new ten-minute deadline' ((Sql "SELECT DATEDIFF_BIG(microsecond,a.assigned_at,w.due_at) delta FROM work_items w JOIN work_assignments a ON a.work_id=w.work_id AND a.assignment_status='ACTIVE' WHERE w.work_id=$($clean.workId)").Rows[0].delta-eq 600000000)
# Simulate time only in the independent fixture. The worker must observe overdue without user interaction.
$null=Sql "UPDATE work_items SET due_at=DATEADD(second,-5,SYSUTCDATETIME()) WHERE work_id=$($clean.workId)"
$stopAt=[datetime]::UtcNow.AddSeconds(65)
do {
 $logs=(Sql "SELECT COUNT(*) n FROM audit_logs WHERE entity_type='Work' AND entity_key='$($clean.workId)' AND action='CLEANING_OVERDUE'").Rows[0].n
 if($logs-eq 1){break};Start-Sleep -Seconds 2
} while([datetime]::UtcNow-lt $stopAt)
Check 'background worker logs an unaccepted overdue job without completion' ($logs-eq 1)
$auditRow=(Sql "SELECT actor_user_id,after_json FROM audit_logs WHERE entity_type='Work' AND entity_key='$($clean.workId)' AND action='CLEANING_OVERDUE'").Rows[0]
Check 'overdue log identifies staff and assignment but system actor is null' ($auditRow.actor_user_id-is [DBNull] -and ($auditRow.after_json|ConvertFrom-Json).AssigneeUserId-eq $hkId)
$clean=(Call GET "/api/v2/work/$($clean.workId)" $null $hk).work.fields
$null=WorkAction $clean start $head @{} 403
$clean=(WorkAction $clean start $hk @{reason=$null}).fields
Check 'start atomically accepts and begins job without reason' ($clean.status-eq 'IN_PROGRESS')
$null=WorkAction $clean 'handover-offer' $hk @{userId=$peerId} 409
Check 'handover denied immediately after start' $true
$null=WorkAction $clean start $hk @{reason=$null} 409
Check 'repeat start cannot create another active timer' ((Sql "SELECT COUNT(*) n FROM work_intervals WHERE work_id=$($clean.workId) AND closed_at IS NULL").Rows[0].n-eq 1)
# Reassign before the base regression performs its own accept/start/complete sequence.
$clean=(WorkAction $clean assign $head @{userId=$hkId;durationMinutes=30;reason=$null}).fields
Check 'overdue observations are idempotent across later mutations' ((Sql "SELECT COUNT(*) n FROM audit_logs WHERE entity_key='$($clean.workId)' AND action='CLEANING_OVERDUE'").Rows[0].n-eq 1)
$clean=(WorkAction $clean accept $hk @{reason=$null}).fields
$null=WorkAction $clean 'handover-offer' $hk @{userId=$peerId} 409
Check 'accept alone also forbids handover' $true
$clean=(WorkAction $clean assign $head @{userId=$hkId;durationMinutes=30;reason=$null}).fields
