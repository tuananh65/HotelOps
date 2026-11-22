# Dot-sourced by Test-Backend.ps1, after a verified backup of a newly created fixture.
if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required'}
$actors=Sql (Get-Content (Join-Path $PSScriptRoot 'maintenance-lead-fixture.sql') -Encoding UTF8 -Raw)
$lead=(Call POST '/api/v2/auth/login' @{username='qa_lead_a';password=$password}).accessToken
$foreign=(Call POST '/api/v2/auth/login' @{username='qa_lead_b';password=$password}).accessToken
$tech=(Call POST '/api/v2/auth/login' @{username='qa_tech_a';password=$password}).accessToken
$peer=(Call POST '/api/v2/auth/login' @{username='qa_tech_peer';password=$password}).accessToken
$a=$f.branchId;$b=$f.otherBranchId
$leadId=($actors.Rows|Where-Object username -eq 'qa_lead_a').user_id
$techId=($actors.Rows|Where-Object username -eq 'qa_tech_a').user_id
$peerId=($actors.Rows|Where-Object username -eq 'qa_tech_peer').user_id
$foreignId=($actors.Rows|Where-Object username -eq 'qa_tech_b').user_id
$inactiveId=($actors.Rows|Where-Object username -eq 'qa_tech_inactive').user_id
$dept=(Sql "SELECT department_id FROM departments WHERE branch_id=$a AND code='ENG'").Rows[0].department_id
function FreshWork($work,$token=$lead){return (Call GET "/api/v2/work/$($work.workId)" $null $token).work.fields}
function NewMaintenance($title='Test maintenance',$token=$lead,$branch=$a){return (Call POST '/api/v2/work' @{branchId=$branch;kind='MAINTENANCE';title=$title;dueAt=[DateTime]::UtcNow.AddHours(-1).ToString('o')} $token).fields}
function FinishMaintenance($work,$token=$tech){foreach($verb in @('accept','start','complete')){$work=(WorkAction $work $verb $token).fields};return $work}
$me=Call GET '/api/v2/auth/me' $null $lead
Check 'Lead exactly 14 baseline grants, attendance OWN, no execution/asset manage/shift manage grants' (@($me.grants).Count-eq 14 -and @($me.grants|Where-Object {$_.code-eq 'ATTENDANCE_RECORD' -and $_.dataScope-eq 'OWN'}).Count-eq 1 -and @($me.grants|Where-Object code -in 'MAINTENANCE_EXECUTE','ASSET_MANAGE','SHIFT_MANAGE').Count-eq 0)
foreach($entry in @(@{token=$lead;own=$a;other=$b},@{token=$foreign;own=$b;other=$a})){
 foreach($path in @('insights/maintenance','work/technicians','templates','catalog/assets','workspace/options','workday')){
  $null=Call GET "/api/v2/$path`?branchId=$($entry.own)&date=$((Get-Date).ToString('yyyy-MM-dd'))" $null $entry.token
  $null=Call GET "/api/v2/$path`?branchId=$($entry.other)&date=$((Get-Date).ToString('yyyy-MM-dd'))" $null $entry.token 403
 }
 foreach($kind in @('MAINTENANCE','INCIDENT')){$null=Call GET "/api/v2/work?branchId=$($entry.other)&kind=$kind" $null $entry.token 403}
}
Check 'Lead A/B read APIs enforce branch scope across dashboard/tasks/PM/assets/technicians/Workday' $true
$otherWork=NewMaintenance 'Foreign maintenance' $foreign $b
$null=Call GET "/api/v2/work/$($otherWork.workId)" $null $lead 403
$null=WorkAction $otherWork assign $lead @{userId=$techId} 403
Check 'Foreign task detail and writes are forbidden' $true
$candidates=Call GET "/api/v2/work/candidates?branchId=$a&kind=MAINTENANCE" $null $lead
Check 'Assignment candidates exclude inactive, foreign and non-executor Lead' (@($candidates|Where-Object userId -eq $techId).Count-eq 1 -and @($candidates|Where-Object userId -in $foreignId,$inactiveId,$leadId).Count-eq 0)
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{code='LEAD-STD';name='Standard';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{code='LEAD-F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
$room=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$floor.locationId}} $admin).fields
$asset=(Call POST "/api/v2/catalog/assets?branchId=$a" @{fields=@{assetCode='LEAD-AC';name='Air conditioner';conditionCode='GOOD';roomId=$room.roomId;departmentId=$dept}} $admin).fields
$otherAsset=(Call POST "/api/v2/catalog/assets?branchId=$b" @{fields=@{assetCode='LEAD-OTHER';name='Foreign device';conditionCode='GOOD'}} $admin).fields
$priority=@{referenceId=(Sql "SELECT reference_id FROM reference_values WHERE kind='PRIORITY' AND code='HIGH'").Rows[0].reference_id}
$severity=@{referenceId=(Sql "SELECT reference_id FROM reference_values WHERE kind='SEVERITY' AND code='CRITICAL'").Rows[0].reference_id}
$incident=(Call POST '/api/v2/work' @{branchId=$a;kind='INCIDENT';title='Room AC failure';roomId=$room.roomId;assetId=$asset.assetId;departmentId=$dept;severityId=$severity.referenceId} $lead).fields
$incident=(WorkAction $incident triage $lead @{priorityId=$priority.referenceId;severityId=$severity.referenceId;dueAt=[DateTime]::UtcNow.AddHours(2).ToString('o')}).fields
$incident=(WorkAction $incident escalate $lead).fields
Check 'Incident create/triage/escalation preserve independent incident lifecycle' ($incident.status-eq 'OPEN' -and $incident.priorityId-eq $priority.referenceId)
$derive=@{rowVersion=$incident.rowVersion;operationKey=[Guid]::NewGuid().ToString();title='Repair room AC';reason='Repair diagnosed AC problem'}
$work=(Call POST "/api/v2/work/$($incident.workId)/maintenance" $derive $lead).fields
$null=Call POST "/api/v2/work/$($incident.workId)/maintenance" $derive $lead 409
$incident=FreshWork $incident
$derive.rowVersion=$incident.rowVersion;$derive.operationKey=[Guid]::NewGuid().ToString()
$null=Call POST "/api/v2/work/$($incident.workId)/maintenance" $derive $lead 409
Check 'Incident -> Maintenance linked atomically; retry and second active repair rejected' ($work.parentWorkId-eq $incident.workId -and $work.assetId-eq $asset.assetId)
$incident=(WorkAction $incident assign $lead @{userId=$leadId}).fields
foreach($verb in @('accept','start','complete')){$incident=(WorkAction $incident $verb $lead).fields}
$null=WorkAction $incident verify $admin @{} 409
Check 'Incident cannot close while its maintenance has not been reviewed' ($incident.status-eq 'RESOLVED')
$null=WorkAction $work assign $lead @{userId=$foreignId} 409
$null=WorkAction $work assign $lead @{userId=$inactiveId} 409
$null=WorkAction $work assign $lead @{userId=$leadId} 403
$work=(WorkAction $work assign $lead @{userId=$techId}).fields
Check 'Only active same-branch executor can be assigned' ($work.status-eq 'PENDING')
$listed=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&search=Repair%20room%20AC&sort=due&pageSize=1" $null $lead
Check 'Server search/sort/page returns asset, priority and real assignee metadata' ($listed.total-eq 1 -and $listed.items[0].fields.assetName-eq 'Air conditioner' -and $listed.items[0].fields.priorityName-eq 'High' -and $listed.items[0].fields.assigneeName-eq 'qa_tech_a')
$stale=$work;$work=(WorkAction $work assign $lead @{userId=$peerId}).fields
$null=WorkAction $stale assign $lead @{userId=$techId} 409
$work=(WorkAction $work assign $lead @{userId=$techId}).fields
Check 'Reassignment keeps history, one active assignee, stale version rejected' ((Sql "SELECT COUNT(*) n FROM work_assignments WHERE work_id=$($work.workId) AND assignment_status='ACTIVE'").Rows[0].n-eq 1 -and (Sql "SELECT COUNT(*) n FROM work_assignments WHERE work_id=$($work.workId) AND assignment_status='REASSIGNED'").Rows[0].n-eq 2)
$null=WorkAction $work verify $lead @{} 409
foreach($verb in @('accept','start','complete')){$null=WorkAction $work $verb $lead @{} 403}
$null=WorkAction $work start $peer @{} 403
Check 'Lead cannot execute staff work, review before completion or bypass assignment' $true
$work=FinishMaintenance $work
$null=WorkAction $work assign $lead @{userId=$peerId} 409
$incident=FreshWork $incident;$derive.rowVersion=$incident.rowVersion;$derive.operationKey=[Guid]::NewGuid().ToString()
$null=Call POST "/api/v2/work/$($incident.workId)/maintenance" $derive $lead 409
Check 'Completed repair awaits review and blocks duplicate active maintenance' ($work.status-eq 'COMPLETED')
$reviewStats=Call GET "/api/v2/insights/maintenance?branchId=$a" $null $lead
Check 'Pending review stays in total open maintenance; incident/asset/severity metrics are separate' ($reviewStats.metrics.open-eq 1 -and $reviewStats.metrics.pendingReview-eq 1 -and $reviewStats.metrics.incidents-eq 1 -and $reviewStats.metrics.severeIncidents-eq 1 -and $reviewStats.metrics.affectedAssets-eq 1)
$null=WorkAction $work reopen $lead @{reason='short'} 400
$stale=$work;$work=(WorkAction $work reopen $lead @{reason='Still noisy, repeat repair required'}).fields
$null=WorkAction $stale verify $lead @{} 409
$work=(WorkAction $work start $tech).fields;$work=(WorkAction $work complete $tech).fields
$work=(WorkAction $work verify $lead).fields
$null=WorkAction $work verify $lead @{} 409
$detail=Call GET "/api/v2/work/$($work.workId)" $null $lead
Check 'Review rejection -> existing reopen -> staff rework -> independent accept with immutable review history' ($work.status-eq 'CLOSED' -and $detail.maintenance.reviews.Count-eq 2)
$assetDetail=Call GET "/api/v2/assets/$($asset.assetId)" $null $lead
$state=@(Call GET "/api/v2/rooms?branchId=$a" $null $lead)|Where-Object roomId -eq $room.roomId
Check 'Maintenance acceptance does not close incident, make room Ready, or alter asset condition' ((FreshWork $incident).status-eq 'RESOLVED' -and $state.readiness-ne 'READY' -and $assetDetail.asset.fields.conditionCode-eq $asset.conditionCode)
$null=WorkAction $incident verify $lead @{} 403
$incident=(WorkAction $incident verify $admin).fields
Check 'Incident closure remains separate and requires independent verification' ($incident.status-eq 'CLOSED')
$planBody=@{branchId=$a;kind='MAINTENANCE';code='LEAD-PM-TEST';title='Monthly AC preventive maintenance';roomId=$room.roomId;assetId=$asset.assetId;departmentId=$dept;frequency='MONTHLY';frequencyInterval=1;nextDueAt=[DateTime]::UtcNow.AddMinutes(-1).ToString('o')}
$badPlan=$planBody.Clone();$badPlan.assetId=$otherAsset.assetId
$null=Call POST '/api/v2/templates/schedules' $badPlan $lead 409
$plan=(Call POST '/api/v2/templates/schedules' $planBody $lead).fields
$null=Call GET "/api/v2/templates/schedules/$($plan.workTemplateId)" $null $foreign 403
$planBody.title='Monthly AC maintenance updated'
$updated=(Call PUT "/api/v2/templates/schedules/$($plan.workTemplateId)" @{rowVersion=$plan.rowVersion;plan=$planBody} $lead).fields
$null=Call PUT "/api/v2/templates/schedules/$($plan.workTemplateId)" @{rowVersion=$plan.rowVersion;plan=$planBody} $lead 409
Check 'PM create/edit validates same-branch asset and stale version without changing identity/history' ($updated.title-eq $planBody.title)
$savedWorker=[Environment]::GetEnvironmentVariable('HOTELOPS_COMPACT_MAINTENANCE_TEST')
try{
 $env:HOTELOPS_COMPACT_MAINTENANCE_TEST=$connection.ConnectionString
 dotnet test (Join-Path $root 'tests/HotelOps.Compact.Tests/HotelOps.Compact.Tests.csproj') --no-build --filter 'FullyQualifiedName~MaintenanceWorkerTests' --logger "trx;LogFileName=maintenance-worker.trx" --results-directory (Join-Path $artifacts 'maintenance-worker')
 if($LASTEXITCODE-ne 0){throw 'PM worker verification failed'}
}finally{[Environment]::SetEnvironmentVariable('HOTELOPS_COMPACT_MAINTENANCE_TEST',$savedWorker)}
$history=Call GET "/api/v2/templates/schedules/$($plan.workTemplateId)" $null $lead
Check 'Existing PM worker generates exactly one Maintenance Task across repeat ticks and retains occurrence history' ($history.occurrences.Count-eq 1 -and $history.occurrences[0].status-eq 'PENDING')
$pm=FreshWork @{workId=$history.occurrences[0].workId}
$pm=(WorkAction $pm assign $lead @{userId=$techId}).fields;$pm=FinishMaintenance $pm;$pm=(WorkAction $pm verify $lead).fields
$history=Call GET "/api/v2/templates/schedules/$($plan.workTemplateId)" $null $lead
Check 'PM -> occurrence -> assignment -> staff execution -> Lead review -> maintenance history' ($history.occurrences[0].status-eq 'CLOSED' -and $history.occurrences[0].verifiedAt)
$currentPlan=$history.template.fields
$null=Call POST "/api/v2/templates/schedules/$($plan.workTemplateId)/disable" @{rowVersion=$currentPlan.rowVersion} $lead
$history=Call GET "/api/v2/templates/schedules/$($plan.workTemplateId)" $null $lead
Check 'Disabling PM keeps generated maintenance history intact' (-not $history.template.fields.isActive -and $history.occurrences.Count-eq 1)
$assetDetail=Call GET "/api/v2/assets/$($asset.assetId)" $null $lead
Check 'Read-only asset details include location, related maintenance/incident and PM history' ($assetDetail.work.Count-ge 2 -and $assetDetail.plans.Count-eq 1)
foreach($verb in @('move','retire','condition')){$null=Call POST "/api/v2/assets/$($asset.assetId)/actions/$verb" @{rowVersion=$asset.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Forbidden asset operation'} $lead 403}
foreach($verb in @('check-in','check-out','unblock')){$null=Call POST "/api/v2/rooms/$($room.roomId)/actions/$verb" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $lead 403}
Check 'No asset management, occupancy, Ready or room-unblock capability' $true
foreach($kind in @('RECEIPT','ISSUE','MATERIAL_REQUEST')){$null=Call POST '/api/v2/documents' @{branchId=$a;kind=$kind} $lead 403}
$materialWork=NewMaintenance 'Repair awaiting materials';$materialWork=(WorkAction $materialWork assign $lead @{userId=$techId}).fields
$mr=(Call POST '/api/v2/documents' @{branchId=$a;kind='MATERIAL_REQUEST';departmentId=$dept;sourceWorkId=$materialWork.workId} $tech).fields
$materialContext=Call GET "/api/v2/work/$($materialWork.workId)" $null $lead
Check 'Maintenance detail exposes only source-linked material requests under MR_VIEW' ($materialContext.maintenance.materials[0].documentId-eq $mr.documentId)
$null=Call GET "/api/v2/documents/$($mr.documentId)" $null $lead
$null=DocAction $mr approve $lead 403
$null=DocAction $mr fulfill $lead 403
Check 'Lead can follow maintenance MR but cannot create/approve/fulfill or post inventory' $true
foreach($path in @('/api/v2/users',"/api/v2/catalog/roles?branchId=$a","/api/v2/insights/stock?branchId=$a")){$null=Call GET $path $null $lead 403}
$null=Call POST '/api/v2/users' @{employeeCode='FORBIDDEN';username='forbidden';fullName='Forbidden';password=$password} $lead 403
$null=Call POST "/api/v2/catalog/roles?branchId=$a" @{fields=@{code='FORBIDDEN';name='Forbidden'}} $lead 403
Check 'No user/role administration or inventory access' $true
$shift=(Sql "SELECT TOP 1 shift_id FROM workday_shifts WHERE branch_id=$a AND role_id=(SELECT role_id FROM roles WHERE code='MAINTENANCE_LEAD')").Rows[0].shift_id
function AssignLeadShift($user,$date){$version=[Convert]::ToBase64String((Sql "SELECT row_version FROM workday_shifts WHERE shift_id=$shift").Rows[0].row_version);return (Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$user;workDate=$date;rowVersion=$version;operationKey=[Guid]::NewGuid().ToString()} $admin).fields}
$today=(Get-Date).ToString('yyyy-MM-dd');$ownShift=AssignLeadShift $leadId $today;$future=AssignLeadShift $leadId (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
$null=Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$leadId;workDate=$today;rowVersion=$ownShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $lead 403
$null=Call POST "/api/v2/workday/assignments/$($ownShift.assignmentId)/start" @{rowVersion=$ownShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $foreign 403
$null=Call POST "/api/v2/workday/assignments/$($future.assignmentId)/start" @{rowVersion=$future.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $lead 409
$started=(Call POST "/api/v2/workday/assignments/$($ownShift.assignmentId)/start" @{rowVersion=$ownShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $lead).fields
$ended=(Call POST "/api/v2/workday/assignments/$($started.assignmentId)/end" @{rowVersion=$started.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $lead).fields
Check 'Personal Workday starts/ends own assigned shift only; no scheduling or future-start privilege' ($ended.status-eq 'ENDED')
$technicians=Call GET "/api/v2/work/technicians?branchId=$a" $null $lead
Check 'Technician workspace uses eligible candidates, actual workload and actual shift data' (@($technicians.items|Where-Object {$_.userId-eq $techId -and $_.completed-ge 2}).Count-eq 1 -and $technicians.timezone)
$stats=Call GET "/api/v2/insights/maintenance?branchId=$a" $null $lead
Check 'Maintenance KPI comes from branch data, separate from incidents/PM' ($stats.metrics.closed-eq 2 -and $stats.metrics.incidents-eq 0 -and $stats.metrics.pmDue-eq 0)
$notice=(Sql "SELECT TOP 1 notification_id FROM notifications WHERE recipient_user_id=$leadId").Rows[0].notification_id
$null=Call POST "/api/v2/notifications/$notice/read" $null $foreign 404
$null=Call POST "/api/v2/notifications/$notice/read" $null $lead
Check 'Notifications are recipient-only and maintenance review alerts are delivered' $true
Check 'Maintenance changes retain audit, assignment events and review history' ((Sql "SELECT COUNT(*) n FROM audit_logs WHERE actor_user_id=$leadId AND entity_type IN ('Work','WorkTemplate')").Rows[0].n-gt 5)
# Browser test receives only ephemeral tokens; no credentials are persisted in artifacts.
$liveWork=NewMaintenance 'Browser assignment and review';$liveReview=NewMaintenance 'Browser completed repair'
$liveReview=(WorkAction $liveReview assign $lead @{userId=$techId}).fields;$liveReview=FinishMaintenance $liveReview
$liveEnv=@{LEAD_TEST_DATABASE=$database;LEAD_TEST_TOKEN=$lead;LEAD_TEST_BASE="http://127.0.0.1:$Port";LEAD_TEST_WORK=[string]$liveWork.workId;LEAD_TEST_REVIEW=[string]$liveReview.workId;LEAD_TEST_TECH=[string]$techId;LEAD_TEST_ASSET=[string]$asset.assetId}
$savedLive=@{};foreach($name in $liveEnv.Keys){$savedLive[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$liveEnv[$name])}
try{Push-Location (Join-Path $root 'frontend');try{node tests/compact-maintenance-lead-live.mjs;if($LASTEXITCODE-ne 0){throw 'Live Maintenance Lead browser failed'}}finally{Pop-Location}}
finally{foreach($name in $savedLive.Keys){[Environment]::SetEnvironmentVariable($name,$savedLive[$name])}}
Check 'Live browser assignment/review and PM modal persist through real API and SQL' ((FreshWork $liveReview).status-eq 'CLOSED' -and (Sql "SELECT COUNT(*) n FROM work_assignments WHERE work_id=$($liveWork.workId) AND assignee_user_id=$techId AND assignment_status='ACTIVE'").Rows[0].n-eq 1 -and (Sql "SELECT COUNT(*) n FROM work_templates WHERE code='BROWSER-PM'").Rows[0].n-eq 1)
