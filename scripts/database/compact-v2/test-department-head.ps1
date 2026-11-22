# Invoked by Test-Backend.ps1 -DepartmentHead after provisioning a unique fixture DB.
if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required.'}
$people=Sql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'department-head-fixture.sql')))
function Person($name){return ($people.Rows|Where-Object username -eq $name)}
$headA=(Call POST '/api/v2/auth/login' @{username='qa_head_a';password=$password}).accessToken
$headB=(Call POST '/api/v2/auth/login' @{username='qa_head_b';password=$password}).accessToken
$fallback=(Call POST '/api/v2/auth/login' @{username='qa_fallback';password=$password}).accessToken
$a=(Person 'qa_head_a').branch_id;$b=(Person 'qa_head_b').branch_id
$date=[DateTime]::Today.AddDays(1).ToString('yyyy-MM-dd')
$weekA=Call GET "/api/v2/workday?branchId=$a&date=$date" $null $headA
$weekB=Call GET "/api/v2/workday?branchId=$b&date=$date" $null $headB
Check 'DH sees other department shifts in own branch' (@($weekA.shifts).Count-eq 3 -and @($weekB.shifts).Count-eq 1)
$shift=$weekA.shifts|Where-Object name -eq 'HK morning';$overlap=$weekA.shifts|Where-Object name -eq 'HK overlapping'
$fo=$weekA.shifts|Where-Object name -eq 'FO morning'
$ca=Call GET "/api/v2/workday/candidates?shiftId=$($shift.shiftId)&date=$date" $null $headA
$cb=Call GET "/api/v2/workday/candidates?shiftId=$($weekB.shifts[0].shiftId)&date=$date" $null $headB
Check 'branch A candidates exclude inactive and branch B' (@($ca).Count-eq 2 -and @($ca|Where-Object fullName -eq 'qa_staff_b').Count-eq 0)
Check 'branch B candidates belong to branch B' (@($cb).Count-eq 1 -and $cb[0].fullName-eq 'qa_staff_b')
$otherDept=Call GET "/api/v2/workday/candidates?shiftId=$($fo.shiftId)&date=$date" $null $headA
Check 'employment department does not restrict DH scheduling' (@($otherDept).Count-eq 1 -and $otherDept[0].fullName-eq 'qa_staff_fo')
$null=Call GET "/api/v2/workday?branchId=$b&date=$date" $null $headA 403
$null=Call GET "/api/v2/workday/candidates?shiftId=$($weekB.shifts[0].shiftId)&date=$date" $null $headA 403
$null=Call GET "/api/v2/insights/dashboard?branchId=$b" $null $headA 403
$null=Call GET '/api/v2/users' $null $headA 403
Check 'cross branch reads and User Management denied' $true
function Assign($s,$u,$expected=200,$version=$null){
 if(-not $version){$version=$s.rowVersion}
 return Call POST '/api/v2/workday/assignments' @{shiftId=$s.shiftId;userId=$u;workDate=$date;rowVersion=$version;operationKey=[Guid]::NewGuid().ToString()} $headA $expected
}
$null=Assign $shift (Person 'qa_staff_b').user_id 409
$null=Assign $shift (Person 'qa_inactive').user_id 409
$null=Assign $shift (Person 'qa_staff_a').user_id 409 'AAAAAAAAAAA='
$assigned=(Assign $shift (Person 'qa_staff_a').user_id).fields
Check 'valid shift assignment succeeds; inactive, wrong branch and stale version denied' ($assigned.status-eq 'PLANNED')
$null=Assign $overlap (Person 'qa_staff_a').user_id 409
$null=Assign $shift (Person 'qa_staff_a2').user_id 409
$null=Call POST "/api/v2/workday/assignments/$($assigned.assignmentId)/start" @{rowVersion=$assigned.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $headA 403
Check 'overlap, capacity and manager starting employee shift denied' $true
$docs=Call GET "/api/v2/documents?branchId=$a&kind=MATERIAL_REQUEST&status=PENDING_APPROVAL&search=QA-L1" $null $headA
Check 'MR filter executes on server with exact total' ($docs.total-eq 1)
$l1=($docs.items[0]).fields
$detail=Call GET "/api/v2/documents/$($l1.documentId)" $null $headA
Check 'L1 capability follows backend department authority' $detail.canApprove
$wrong=Call GET "/api/v2/documents/$($l1.documentId)" $null $fallback
Check 'unmapped approver capability is false' (-not $wrong.canApprove)
$null=DocAction $l1 approve $fallback 403
$approved=(DocAction $l1 approve $headA).fields
Check 'L1 approval succeeds' ($approved.status-eq 'APPROVED')
$fb=(Call GET "/api/v2/documents?branchId=$a&kind=MATERIAL_REQUEST&search=QA-FALLBACK" $null $headA).items[0].fields
$self=Call GET "/api/v2/documents/$($fb.documentId)" $null $headA
$eligible=Call GET "/api/v2/documents/$($fb.documentId)" $null $fallback
Check 'fallback capability respects requester and warehouse department' (-not $self.canApprove -and $eligible.canApprove)
$null=DocAction $fb approve $headA 403
$approved=(DocAction $fb approve $fallback).fields
Check 'fallback approval succeeds with mapped authority' ($approved.status-eq 'APPROVED')
$task=(Call POST '/api/v2/work' @{branchId=$a;kind='OPERATIONAL';title='DH searchable task';departmentId=$shift.departmentId;dueAt=[DateTime]::UtcNow.AddHours(-1).ToString('o')} $headA).fields
$filtered=Call GET "/api/v2/work?branchId=$a&kind=OPERATIONAL&search=searchable&departmentId=$($shift.departmentId)&status=PENDING_ASSIGNMENT&overdue=true" $null $headA
Check 'task filters match across server result set' ($filtered.total-eq 1)
$request=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='DH request';departmentId=$shift.departmentId;requesterDepartmentId=$shift.departmentId} $headA).fields
$request=(WorkAction $request submit $headA).fields
Check 'DH creates and submits internal request' ($request.status-eq 'SUBMITTED')
foreach($kind in @('CLEANING','INSPECTION','MAINTENANCE')){
 $null=Call POST '/api/v2/work' @{branchId=$a;kind=$kind;title='Forbidden specialist task'} $headA 403
}
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='RECEIPT'} $headA 403
Check 'specialist and warehouse write permissions not inherited' $true
$dashboard=Call GET "/api/v2/insights/dashboard?branchId=$a" $null $headA
Check 'dashboard uses real branch tasks and pending requests' ($dashboard.operations.activeTasks-eq 1 -and $dashboard.operations.overdueTasks-eq 1 -and $dashboard.operations.pendingRequests-eq 1 -and $dashboard.operations.pendingMaterials-eq 0)
Check 'dashboard exposes branch scheduling only with SHIFT_MANAGE' ($null-ne $dashboard.scheduling)
$period="branchId=$a&from=$date`T00:00:00Z&to=$date`T23:59:59Z"
$report=Call GET "/api/v2/insights/reports/workday?$period" $null $headA
Check 'workday report includes actual fixture assignment' (@($report).Count-eq 1 -and $report[0].employeeCode-eq 'qa_staff_a')
$materialReport=Call GET "/api/v2/insights/reports/materials?branchId=$a" $null $headA
Check 'material report is limited to branch MR documents' (@($materialReport).Count-eq 2)
$null=Call GET "/api/v2/insights/reports/materials?branchId=$b" $null $headA 403
Check 'report rejects wrong branch' $true
