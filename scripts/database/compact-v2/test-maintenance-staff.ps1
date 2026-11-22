if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required'}
$actors=Sql (Get-Content (Join-Path $PSScriptRoot 'maintenance-lead-fixture.sql') -Encoding UTF8 -Raw)
$null=Sql (Get-Content (Join-Path $PSScriptRoot 'maintenance-staff-fixture.sql') -Encoding UTF8 -Raw)
$staff=(Call POST '/api/v2/auth/login' @{username='qa_tech_a';password=$password}).accessToken
$peer=(Call POST '/api/v2/auth/login' @{username='qa_tech_peer';password=$password}).accessToken
$foreign=(Call POST '/api/v2/auth/login' @{username='qa_tech_b';password=$password}).accessToken
$lead=(Call POST '/api/v2/auth/login' @{username='qa_lead_a';password=$password}).accessToken
$a=$f.branchId;$b=$f.otherBranchId
$staffId=($actors.Rows|Where-Object username -eq 'qa_tech_a').user_id
$peerId=($actors.Rows|Where-Object username -eq 'qa_tech_peer').user_id
$foreignId=($actors.Rows|Where-Object username -eq 'qa_tech_b').user_id
$dept=(Sql "SELECT department_id FROM departments WHERE branch_id=$a AND code='ENG'").Rows[0].department_id
function FreshWork($work,$token=$staff){return (Call GET "/api/v2/work/$($work.workId)" $null $token).work.fields}
function NewMaintenance($title='Staff repair',$branch=$a){return (Call POST '/api/v2/work' @{branchId=$branch;kind='MAINTENANCE';title=$title;dueAt=[DateTime]::UtcNow.AddHours(-1).ToString('o')} $admin).fields}
Add-Type -AssemblyName System.Net.Http
function UploadTechEvidence($workId,$token,$expected=200){
 $client=New-Object Net.Http.HttpClient
 $client.DefaultRequestHeaders.Authorization=New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer',$token)
 $multipart=New-Object Net.Http.MultipartFormDataContent
 try{
  $multipart.Add((New-Object Net.Http.StringContent([string]$a)),'branchId')
  $multipart.Add((New-Object Net.Http.StringContent([string]$workId)),'workId')
  $bytes=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=')
  $content=New-Object Net.Http.ByteArrayContent(,$bytes);$content.Headers.ContentType=New-Object Net.Http.Headers.MediaTypeHeaderValue('image/png')
  $multipart.Add($content,'file','repair.png')
  $response=$client.PostAsync("http://127.0.0.1:$Port/api/v2/evidence",$multipart).GetAwaiter().GetResult()
  $json=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
  if([int]$response.StatusCode-ne $expected){throw "Evidence upload expected $expected got $($response.StatusCode): $json"}
  return $json|ConvertFrom-Json
 }finally{$multipart.Dispose();$client.Dispose()}
}
$me=Call GET '/api/v2/auth/me' $null $staff
Check 'Actual Staff baseline 13 grants; attendance OWN; no hypothetical create or manager grants' ($me.grants.Count-eq 13 -and @($me.grants|Where-Object {$_.code-eq 'ATTENDANCE_RECORD' -and $_.dataScope-eq 'OWN'}).Count-eq 1 -and @($me.grants|Where-Object code -in 'INCIDENT_CREATE','IR_CREATE','MAINTENANCE_ASSIGN','MAINTENANCE_REVIEW','SHIFT_MANAGE').Count-eq 0)
foreach($entry in @(@{token=$staff;own=$a;other=$b},@{token=$foreign;own=$b;other=$a})){
 foreach($path in @('insights/personal','catalog/assets','workspace/options','rooms','workday')){
  $null=Call GET "/api/v2/$path`?branchId=$($entry.own)&maintenance=true&date=$((Get-Date).ToString('yyyy-MM-dd'))" $null $entry.token
  $null=Call GET "/api/v2/$path`?branchId=$($entry.other)&maintenance=true&date=$((Get-Date).ToString('yyyy-MM-dd'))" $null $entry.token 403
 }
 foreach($kind in @('MAINTENANCE','INCIDENT','OPERATIONAL','INTERNAL_REQUEST')){$null=Call GET "/api/v2/work?branchId=$($entry.other)&kind=$kind" $null $entry.token 403}
}
Check 'Both branch Staff reads and personal dashboard enforce scope' $true
$work=NewMaintenance;$other=NewMaintenance 'Other branch repair' $b
$null=Call GET "/api/v2/work/$($other.workId)" $null $staff 403
$null=WorkAction $other start $staff @{} 403
$null=WorkAction $work start $staff @{} 403
$work=(WorkAction $work assign $lead @{userId=$staffId}).fields
foreach($verb in @('accept','start','progress','pause','complete')){$null=WorkAction $work $verb $peer @{progress=50} 403}
$null=WorkAction $work assign $staff @{userId=$peerId} 403
$null=WorkAction $work handover-offer $staff @{userId=$peerId} 403
foreach($kind in @('MAINTENANCE','INCIDENT')){$null=Call GET "/api/v2/work/candidates?branchId=$a&kind=$kind" $null $staff 403}
$null=Call GET "/api/v2/work/candidates?branchId=$a&kind=MAINTENANCE&workId=$($work.workId)" $null $staff 403
Check 'Unassigned/other-assignee/foreign execution and staff reassignment are denied' $true
$mine=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&assignedToMe=true&queue=awaiting&search=Staff&sort=due&pageSize=1" $null $staff
$notMine=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&assignedToMe=true" $null $peer
$stats=Call GET "/api/v2/insights/personal?branchId=$a&maintenance=true" $null $staff
$peerStats=Call GET "/api/v2/insights/personal?branchId=$a&maintenance=true" $null $peer
Check 'Assigned-only paging and personal KPI exclude peer tasks and branch-wide statistics' ($mine.total-eq 1 -and $notMine.total-eq 0 -and $stats.metrics.assignedToday-eq 1 -and $stats.metrics.awaitingAcceptance-eq 1 -and $stats.metrics.overdue-eq 1 -and $peerStats.metrics.assignedToday-eq 0)
$null=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&queue=unknown" $null $staff 400
$none=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&assignedToMe=true&priorityId=9223372036854775806" $null $staff
Check 'Queue validation and priority filtering use server data before paging' ($none.total-eq 0)
$null=WorkAction $work decline $staff @{} 409
Check 'Unsupported decline is rejected without inventing status or deleting assignment' ((Sql "SELECT COUNT(*) n FROM work_assignments WHERE work_id=$($work.workId) AND assignment_status='ACTIVE'").Rows[0].n-eq 1)
$null=WorkAction $work start $staff @{} 409
$stale=$work;$acceptKey=[Guid]::NewGuid().ToString();$accept=@{rowVersion=$work.rowVersion;operationKey=$acceptKey}
$work=(Call POST "/api/v2/work/$($work.workId)/actions/accept" $accept $staff).fields
$null=Call POST "/api/v2/work/$($work.workId)/actions/accept" $accept $staff 409
$null=WorkAction $stale start $staff @{} 409
$work=(WorkAction $work start $staff).fields
Check 'Accept then start records one acceptance; stale/retry writes are rejected' ($work.status-eq 'IN_PROGRESS' -and (Sql "SELECT COUNT(*) n FROM work_events WHERE work_id=$($work.workId) AND event_type='ACCEPT'").Rows[0].n-eq 1)
$null=UploadTechEvidence $work.workId $peer 403
$proof=(UploadTechEvidence $work.workId $staff).fields
$null=Call GET "/api/v2/evidence/$($proof.attachmentId)" $null $foreign 403
Check 'Evidence requires current maintenance assignee and branch-scoped download' ((Call GET "/api/v2/work/$($work.workId)" $null $staff).attachments.Count-eq 1)
$work=(WorkAction $work progress $staff @{progress=45;reason='Diagnosed and repaired wiring'}).fields
$null=WorkAction $work progress $staff @{progress=110} 400
$work=(WorkAction $work pause $staff).fields;$work=(WorkAction $work resume $staff).fields
$competing=NewMaintenance 'Competing timer';$competing=(WorkAction $competing assign $lead @{userId=$staffId}).fields;$competing=(WorkAction $competing accept $staff).fields
$null=WorkAction $competing start $staff @{} 409
Check 'Progress/pause/resume retain notes and cannot create overlapping timers' ((Call GET "/api/v2/work/$($work.workId)" $null $staff).events.fields.reason -contains 'Diagnosed and repaired wiring')
$mrKey=[Guid]::NewGuid().ToString()
$mrBody=@{branchId=$a;kind='MATERIAL_REQUEST';departmentId=$dept;sourceWorkId=$work.workId;reason='Material needed for assigned repair';operationKey=$mrKey}
$mr=(Call POST '/api/v2/documents' $mrBody $staff).fields
$null=Call POST '/api/v2/documents' $mrBody $staff 409
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='MATERIAL_REQUEST';departmentId=$dept;sourceWorkId=$competing.workId} $peer 409
$null=Call GET "/api/v2/documents/$($mr.documentId)" $null $foreign 403
$null=DocAction $mr approve $staff 403
Check 'MR links real owned active maintenance, respects scope and cannot approve' ((Call GET "/api/v2/work/$($work.workId)" $null $staff).maintenance.materials[0].documentId-eq $mr.documentId)
Check 'Retrying MR create operation key cannot create duplicate documents' ((Sql "SELECT COUNT(*) n FROM document_events WHERE operation_key='$mrKey' AND event_type='CREATED'").Rows[0].n-eq 1)
$work=(WorkAction $work complete $staff @{reason='Repair completed and ready for lead review'}).fields
$stats=Call GET "/api/v2/insights/personal?branchId=$a&maintenance=true" $null $staff
Check 'Completion waits for review and is distinct from accepted KPI' ($work.status-eq 'COMPLETED' -and $stats.metrics.pendingReview-eq 1 -and $stats.metrics.accepted-eq 0)
$null=UploadTechEvidence $work.workId $staff 409
foreach($verb in @('verify','reopen','assign')){$null=WorkAction $work $verb $staff @{userId=$peerId} 403}
$null=WorkAction $work complete $staff @{} 409
$work=(WorkAction $work reopen $lead @{reason='Wiring remains unstable; repair again'}).fields
$rework=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&assignedToMe=true&queue=rework" $null $staff
$detail=Call GET "/api/v2/work/$($work.workId)" $null $staff
Check 'Lead reopen is visible as rework with original reason and review history' ($rework.total-eq 1 -and $detail.maintenance.reviews[0].reason-eq 'Wiring remains unstable; repair again')
$null=WorkAction $work start $peer @{} 403
$work=(WorkAction $work start $staff).fields;$work=(WorkAction $work complete $staff).fields;$work=(WorkAction $work verify $lead).fields
$stats=Call GET "/api/v2/insights/personal?branchId=$a&maintenance=true" $null $staff
Check 'Assigned staff rework -> completion -> independent review -> accepted personal history' ($work.status-eq 'CLOSED' -and $stats.metrics.accepted-eq 1 -and $stats.metrics.pendingReview-eq 0 -and $stats.metrics.maintenanceRework-eq 0)
$closed=$work;$work=(WorkAction $work reopen $lead).fields
$own=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&assignedToMe=true" $null $staff
$history=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&historyMine=true" $null $staff
$null=WorkAction $work start $staff @{} 403
Check 'Reopening closed work waits for fresh assignment; past history is separate from my active work' (@($own.items|Where-Object {$_.fields.workId-eq $work.workId}).Count-eq 0 -and @($history.items|Where-Object {$_.fields.workId-eq $work.workId}).Count-eq 1)
$work=(WorkAction $work assign $lead @{userId=$peerId}).fields
$history=Call GET "/api/v2/work?branchId=$a&kind=MAINTENANCE&historyMine=true" $null $staff
Check 'Reassignment preserves original execution history without permission to execute new assignee work' (@($history.items|Where-Object {$_.fields.workId-eq $work.workId}).Count-eq 1)
$incident=(Call POST '/api/v2/work' @{branchId=$a;kind='INCIDENT';title='Incident for assigned technician'} $admin).fields
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INCIDENT';title='Forbidden reporting without grant'} $staff 403
$null=Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='Forbidden IR creation'} $staff 403
foreach($verb in @('triage','escalate','assign','cancel','verify','reopen')){$null=WorkAction $incident $verb $staff @{userId=$peerId} 403}
$null=WorkAction $incident start $staff @{} 403
$incident=(WorkAction $incident assign $lead @{userId=$staffId}).fields
foreach($verb in @('accept','start','complete')){$incident=(WorkAction $incident $verb $staff).fields}
$null=WorkAction $incident verify $staff @{} 403
Check 'Actual INCIDENT_MANAGE supports assigned execution only; creation/triage/escalation/coordination stay forbidden' ($incident.status-eq 'RESOLVED')
$linked=(Call POST "/api/v2/work/$($incident.workId)/maintenance" @{rowVersion=$incident.rowVersion;operationKey=[Guid]::NewGuid().ToString();title='Linked repair';reason='Diagnosed technical failure'} $lead).fields
$linked=(WorkAction $linked assign $lead @{userId=$staffId}).fields
foreach($verb in @('accept','start','complete')){$linked=(WorkAction $linked $verb $staff).fields}
Check 'Maintenance completion does not resolve or close its Incident automatically' ((FreshWork $incident).status-eq 'RESOLVED' -and $linked.status-eq 'COMPLETED')
$op=(Call POST '/api/v2/work' @{branchId=$a;kind='OPERATIONAL';title='Other assigned technical work'} $admin).fields;$op=(WorkAction $op assign $admin @{userId=$staffId}).fields
foreach($verb in @('accept','start','complete')){$op=(WorkAction $op $verb $staff).fields}
$ir=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='Assigned internal repair request';departmentId=$dept;requesterDepartmentId=$f.departmentId} $admin).fields
$ir=(WorkAction $ir submit $admin).fields;$ir=(WorkAction $ir route $admin @{relatedId=$dept}).fields;$ir=(WorkAction $ir assign $admin @{userId=$staffId}).fields
$null=WorkAction $ir route $staff @{relatedId=$dept} 403
$null=WorkAction $ir assign $staff @{userId=$peerId} 403
$null=WorkAction $ir start $peer @{} 403
foreach($verb in @('accept','start','complete')){$ir=(WorkAction $ir $verb $staff).fields}
Check 'Actual TASK_EXECUTE and IR_PROCESS work only through assigned independent lifecycles' ($op.status-eq 'PENDING_VERIFICATION' -and $ir.status-eq 'COMPLETED')
foreach($path in @('/api/v2/users',"/api/v2/catalog/roles?branchId=$a","/api/v2/templates?branchId=$a","/api/v2/work/technicians?branchId=$a","/api/v2/insights/maintenance?branchId=$a","/api/v2/insights/stock?branchId=$a")){$null=Call GET $path $null $staff 403}
foreach($kind in @('RECEIPT','ISSUE','TRANSFER','ADJUSTMENT')){$null=Call POST '/api/v2/documents' @{branchId=$a;kind=$kind} $staff 403}
$null=Call POST '/api/v2/users' @{employeeCode='FORBIDDEN';username='forbidden';fullName='Forbidden';password=$password} $staff 403
Check 'No PM planning, Lead dashboard, technician assignment, user administration or inventory posting rights' $true
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{code='TECH-STD';name='Standard';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{code='TECH-F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
$room=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$floor.locationId}} $admin).fields
$asset=(Call POST "/api/v2/catalog/assets?branchId=$a" @{fields=@{assetCode='TECH-DEVICE';name='Technician device';conditionCode='GOOD';roomId=$room.roomId}} $admin).fields
$null=Call GET "/api/v2/assets/$($asset.assetId)" $null $staff
$null=Call GET "/api/v2/assets/$($asset.assetId)" $null $foreign 403
foreach($verb in @('move','retire','condition')){$null=Call POST "/api/v2/assets/$($asset.assetId)/actions/$verb" @{rowVersion=$asset.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Forbidden asset change'} $staff 403}
foreach($verb in @('check-in','check-out','unblock')){$null=Call POST "/api/v2/rooms/$($room.roomId)/actions/$verb" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff 403}
Check 'Assets and room context remain read-only, branch scoped and separate from maintenance completion' $true
$shift=(Sql "SELECT TOP 1 shift_id FROM workday_shifts WHERE branch_id=$a AND role_id=(SELECT role_id FROM roles WHERE code='MAINTENANCE_STAFF')").Rows[0].shift_id
function AssignTechShift($user,$date){$version=[Convert]::ToBase64String((Sql "SELECT row_version FROM workday_shifts WHERE shift_id=$shift").Rows[0].row_version);return (Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$user;workDate=$date;rowVersion=$version;operationKey=[Guid]::NewGuid().ToString()} $admin).fields}
$today=(Get-Date).ToString('yyyy-MM-dd');$ownShift=AssignTechShift $staffId $today;$peerShift=AssignTechShift $peerId $today;$future=AssignTechShift $staffId (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
$null=Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$staffId;workDate=$today;rowVersion=$ownShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff 403
$null=Call POST "/api/v2/workday/assignments/$($peerShift.assignmentId)/start" @{rowVersion=$peerShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff 403
$null=Call POST "/api/v2/workday/assignments/$($future.assignmentId)/start" @{rowVersion=$future.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff 409
$null=Call POST "/api/v2/workday/assignments/$($ownShift.assignmentId)/end" @{rowVersion=$ownShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff 409
$started=(Call POST "/api/v2/workday/assignments/$($ownShift.assignmentId)/start" @{rowVersion=$ownShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff).fields
$ended=(Call POST "/api/v2/workday/assignments/$($started.assignmentId)/end" @{rowVersion=$started.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $staff).fields
Check 'Own Workday start/end checks owner, lifecycle and future date without scheduling rights' ($ended.status-eq 'ENDED')
$notices=Sql "SELECT notification_id,notification_type FROM notifications WHERE recipient_user_id=$staffId AND notification_type IN ('MAINTENANCE_VERIFY','MAINTENANCE_REOPEN')"
$notice=$notices.Rows[0].notification_id
$null=Call POST "/api/v2/notifications/$notice/read" $null $peer 404
$null=Call POST "/api/v2/notifications/$notice/read" $null $staff
Check 'Review/rework notifies actual assignee and notifications remain recipient-only' ($notices.Rows.Count-ge 2)
Check 'Execution/review events and work intervals retain actual personal audit history' ((Sql "SELECT COUNT(*) n FROM work_events WHERE actor_user_id=$staffId AND event_type='COMPLETE'").Rows[0].n-ge 4 -and (Sql "SELECT COUNT(*) n FROM work_intervals WHERE user_id=$staffId AND closed_at IS NULL").Rows[0].n-eq 0)
$live=NewMaintenance 'Live browser technician repair';$live=(WorkAction $live assign $lead @{userId=$staffId}).fields
$uom=(Call POST '/api/v2/catalog/references' @{fields=@{kind='UOM';code='TECH-EA';name='Piece'}} $admin).fields
$null=Call POST "/api/v2/catalog/items?branchId=$a" @{fields=@{code='TECH-PART';name='Repair part';baseUnitId=$uom.referenceId}} $admin
$liveEnv=@{TECH_TEST_DATABASE=$database;TECH_TEST_TOKEN=$staff;TECH_TEST_LEAD=$lead;TECH_TEST_BASE="http://127.0.0.1:$Port";TECH_TEST_WORK=[string]$live.workId}
$savedLive=@{};foreach($name in $liveEnv.Keys){$savedLive[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$liveEnv[$name])}
try{Push-Location (Join-Path $root 'frontend');try{node tests/compact-maintenance-staff-live.mjs;if($LASTEXITCODE-ne 0){throw 'Maintenance Staff live browser failed'}}finally{Pop-Location}}
finally{foreach($name in $savedLive.Keys){[Environment]::SetEnvironmentVariable($name,$savedLive[$name])}}
Check 'Browser real accept/start/progress/MR/complete/rework flow persists actual SQL outcomes' ((FreshWork $live).status-eq 'COMPLETED' -and (Sql "SELECT COUNT(*) n FROM inventory_documents WHERE source_work_id=$($live.workId) AND created_by=$staffId").Rows[0].n-eq 1)
