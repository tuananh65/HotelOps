# Invoked only by Test-Backend.ps1 -OperationsManager on a fresh fixture.
if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required.'}
$people=Sql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'operations-manager-fixture.sql')))
$omA=(Call POST '/api/v2/auth/login' @{username='qa_om_a';password=$password}).accessToken
$omB=(Call POST '/api/v2/auth/login' @{username='qa_om_b';password=$password}).accessToken
$a=$f.branchId;$b=$f.otherBranchId
$me=Call GET '/api/v2/auth/me' $null $omA
Check 'OM has exactly approved 24 scoped grants' (@($me.grants).Count-eq 24 -and @($me.grants|Where-Object dataScope -ne 'BRANCH').Count-eq 0)
foreach($path in @('rooms','insights/dashboard','insights/stock','insights/audit','work?kind=INCIDENT')){
 $sep=if($path.Contains('?')){'&'}else{'?'}
 $null=Call GET "/api/v2/$path${sep}branchId=$a" $null $omA
 $null=Call GET "/api/v2/$path${sep}branchId=$b" $null $omB
 $null=Call GET "/api/v2/$path${sep}branchId=$b" $null $omA 403
}
Check 'both branches readable only by assigned OM' $true
foreach($path in @('/api/v2/users',"/api/v2/catalog/roles?branchId=$a","/api/v2/workday?branchId=$a")){$null=Call GET $path $null $omA 403}
foreach($kind in @('CLEANING','INSPECTION','MAINTENANCE','OPERATIONAL')){
 $null=Call POST '/api/v2/work' @{branchId=$a;kind=$kind;title='Forbidden specialist operation'} $omA 403
}
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='RECEIPT'} $omA 403
Check 'no administration, scheduling, specialist creation or stock posting rights' $true
$type=(Call POST "/api/v2/catalog/room-types?branchId=$a" @{fields=@{code='OM-STD';name='Standard';capacity=2}} $admin).fields
$floor=(Call POST "/api/v2/catalog/locations?branchId=$a" @{fields=@{code='OM-F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
$otherFloor=(Call POST "/api/v2/catalog/locations?branchId=$b" @{fields=@{code='OM-F1';name='Other floor';kind='FLOOR';floorNumber=1}} $admin).fields
$room=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$floor.locationId}} $admin).fields
function RoomAction($room,$verb,$extra=@{},$expected=200){
 $body=@{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='OM integration room verification'}
 foreach($key in $extra.Keys){$body[$key]=$extra[$key]}
 return Call POST "/api/v2/rooms/$($room.roomId)/actions/$verb" $body $omA $expected
}
$null=RoomAction $room 'check-in' @{} 403
$blocked=RoomAction $room block
$null=RoomAction $room unblock @{} 409
$released=RoomAction $blocked.room.fields unblock
Check 'block/unblock is versioned and does not force room READY' ($blocked.readiness.fields.readiness-eq 'BLOCKED' -and $released.room.fields.operationalStatus-eq 'DIRTY')
$room=$released.room.fields
$incident=(Call POST '/api/v2/work' @{branchId=$a;kind='INCIDENT';title='OM incident';roomId=$room.roomId;departmentId=$f.departmentId;dueAt=[DateTime]::UtcNow.AddHours(-1).ToString('o')} $omA).fields
$blocked=RoomAction $room block @{sourceWorkId=$incident.workId}
$null=RoomAction $blocked.room.fields unblock @{} 409
Check 'unblock denied while source incident is unresolved' $true
$priority=(Sql "SELECT TOP(1) reference_id FROM reference_values WHERE kind='PRIORITY' AND is_active=1 ORDER BY reference_id").Rows[0].reference_id
$incident=(WorkAction $incident triage $omA @{priorityId=$priority;dueAt=[DateTime]::UtcNow.AddHours(2).ToString('o')}).fields
$incident=(WorkAction $incident escalate $omA).fields
$null=WorkAction $incident start $omA @{} 403
$null=Call GET "/api/v2/work/$($incident.workId)" $null $omB 403
Check 'incident triage/escalation succeed but unassigned execution and foreign read denied' ($incident.priorityId-eq $priority)
$asset=(Call POST "/api/v2/catalog/assets?branchId=$a" @{fields=@{assetCode='OM-ASSET';name='Air conditioner';conditionCode='GOOD';roomId=$room.roomId}} $omA).fields
$asset=(Call PUT "/api/v2/catalog/assets/$($asset.assetId)?branchId=$a" @{rowVersion=$asset.rowVersion;fields=@{name='Updated air conditioner'}} $omA).fields
function AssetAction($asset,$verb,$extra=@{},$expected=200){
 $body=@{rowVersion=$asset.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='OM integration asset verification'}
 foreach($key in $extra.Keys){$body[$key]=$extra[$key]}
 return Call POST "/api/v2/assets/$($asset.assetId)/actions/$verb" $body $omA $expected
}
$null=AssetAction $asset move @{locationId=$otherFloor.locationId} 409
$asset=(AssetAction $asset move @{locationId=$floor.locationId}).fields
$null=Call GET "/api/v2/assets/$($asset.assetId)" $null $omB 403
Check 'asset create/edit/move succeeds only in authorized branch' ($asset.locationId-eq $floor.locationId)
$repair=(Call POST '/api/v2/work' @{branchId=$a;kind='MAINTENANCE';title='Overdue asset repair';assetId=$asset.assetId;dueAt=[DateTime]::UtcNow.AddHours(-1).ToString('o')} $admin).fields
$null=AssetAction $asset retire @{} 409
$null=WorkAction $repair assign $omA @{userId=$f.workerId} 403
$null=WorkAction $repair verify $omA @{} 403
Check 'asset with active work cannot retire; OM cannot assign or verify repair' $true
$task=(Call POST '/api/v2/work' @{branchId=$a;kind='OPERATIONAL';title='Other employee work';departmentId=$f.departmentId} $admin).fields
$found=Call GET "/api/v2/work?branchId=$a&kind=OPERATIONAL&mine=false&search=Other&departmentId=$($f.departmentId)" $null $omA
Check 'branch monitoring includes work created by another employee' ($found.total-eq 1)
$request=(Call POST '/api/v2/work' @{branchId=$a;kind='INTERNAL_REQUEST';title='OM internal request';departmentId=$f.departmentId;requesterDepartmentId=$f.departmentId} $omA).fields
$request=(WorkAction $request submit $omA).fields
$request=(WorkAction $request route $omA @{relatedId=$f.departmentId}).fields
$request=(WorkAction $request escalate $omA).fields
Check 'OM internal request creation routing and escalation use existing workflow' ($request.status-eq 'PENDING_ACCEPTANCE')
$dashboard=Call GET "/api/v2/insights/dashboard?branchId=$a" $null $omA
$maintenance=@($dashboard.monitoring|Where-Object kind -eq 'MAINTENANCE')[0]
Check 'dashboard metrics and attention use actual branch records' ($maintenance.active-eq 1 -and $maintenance.overdue-eq 1 -and @($dashboard.attention|Where-Object workId -eq $repair.workId).Count-eq 1 -and @($dashboard.assets)[0].count-eq 1)
$empty=Call GET "/api/v2/insights/dashboard?branchId=$b" $null $omB
Check 'other branch dashboard does not leak records' (@($empty.attention).Count-eq 0 -and @($empty.assets).Count-eq 0)
foreach($report in @('rooms','assets','maintenance','incidents','tasks','requests','stock','materials')){
 $null=Call GET "/api/v2/insights/reports/$report`?branchId=$a" $null $omA
 $null=Call GET "/api/v2/insights/reports/$report`?branchId=$b" $null $omA 403
}
Check 'reports respect domain permissions and branch scope' $true
$audit=Call GET "/api/v2/insights/audit?branchId=$a&search=Asset&page=1" $null $omA
Check 'audit search returns metadata only for selected branch' (@($audit).Count-gt 0 -and @($audit|Where-Object {$_.branchId-ne $a -or $_.entityType-ne 'Asset'}).Count-eq 0 -and -not ($audit[0].PSObject.Properties.Name -contains 'afterJson'))
$occupiedRoom=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='102';roomTypeId=$type.roomTypeId;locationId=$floor.locationId}} $admin).fields
$checklist=(Call POST '/api/v2/templates/checklists' @{branchId=$a;code='OM-QC';name='Inspection';kind='INSPECTION';items=@(@{description='Clean surfaces';isRequired=$true;weight=1})} $admin).fields
$clean=(Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Prepare occupied room';roomId=$occupiedRoom.roomId} $admin).fields
$clean=(WorkAction $clean assign $admin @{userId=$f.workerId}).fields
foreach($verb in @('accept','start','complete')){$clean=(WorkAction $clean $verb $worker).fields}
$inspection=(Call POST '/api/v2/work' @{branchId=$a;kind='INSPECTION';title='Inspect occupied room';roomId=$occupiedRoom.roomId;parentWorkId=$clean.workId;checklistTemplateId=$checklist.checklistTemplateId} $admin).fields
$inspection=(WorkAction $inspection assign $admin @{userId=$f.adminId}).fields
foreach($verb in @('accept','start')){$inspection=(WorkAction $inspection $verb $admin).fields}
$check=(Call GET "/api/v2/work/$($inspection.workId)" $null $admin).checklist[0].fields
$null=Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($check.workChecklistItemId)" @{rowVersion=$check.rowVersion;outcome='PASS'} $admin
$inspection=(Call GET "/api/v2/work/$($inspection.workId)" $null $admin).work.fields
$inspection=(WorkAction $inspection complete $admin).fields
$inspection=(WorkAction $inspection pass $admin).fields
$occupiedRoom=@(Call GET "/api/v2/rooms?branchId=$a" $null $omA)|Where-Object roomId -eq $occupiedRoom.roomId
$occupiedRoom=(Call POST "/api/v2/rooms/$($occupiedRoom.roomId)/actions/check-in" @{rowVersion=$occupiedRoom.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $admin).room.fields
$null=RoomAction $occupiedRoom block
$occupied=@(Call GET "/api/v2/rooms?branchId=$a" $null $omA)|Where-Object roomId -eq $occupiedRoom.roomId
Check 'occupancy and active block are independently represented' ($occupied.isOccupied -and $occupied.isBlocked)
# Reduce domain grants only in the fixture, then verify the dashboard omits those domains.
$null=Sql "DELETE rp FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE r.code='OPERATION_MANAGER' AND p.code IN ('ASSET_VIEW','MAINTENANCE_VIEW','INCIDENT_VIEW')"
$restricted=Call GET "/api/v2/insights/dashboard?branchId=$a" $null $omA
Check 'dashboard grant alone does not reveal specialist or asset data' ($null-eq $restricted.assets -and @($restricted.monitoring|Where-Object {$_.kind-in @('MAINTENANCE','INCIDENT')}).Count-eq 0)
