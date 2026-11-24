# Only dot-sourced inside Test-Backend against an isolated fixture, after paper inspection tests.
if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required'}
$null=Sql (Get-Content (Join-Path $PSScriptRoot 'grant-room-remediation.sql') -Encoding UTF8 -Raw)
$null=Sql (Get-Content (Join-Path $PSScriptRoot 'grant-room-remediation.sql') -Encoding UTF8 -Raw)
$techActors=Sql (Get-Content (Join-Path $PSScriptRoot 'maintenance-lead-fixture.sql') -Encoding UTF8 -Raw)
$a=$f.branchId;$b=$f.otherBranchId
$null=Sql @"
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT r.role_id,p.permission_id,'BRANCH' FROM roles r CROSS JOIN permissions p
WHERE ((r.code='DEPT_MANAGER' AND p.code='MR_VIEW') OR (r.code='STOREKEEPER' AND p.code IN ('MR_VIEW','INVENTORY_VIEW')))
AND NOT EXISTS(SELECT 1 FROM role_permissions x WHERE x.role_id=r.role_id AND x.permission_id=p.permission_id);
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT 'QA_ISSUE_HEAD','qa_issue_head',N'Issue Head',password_hash,'ACTIVE','ACTIVE' FROM users WHERE user_id=$($f.adminId);
INSERT users(employee_code,username,full_name,password_hash,account_status,employment_status)
SELECT 'QA_ISSUE_STORE','qa_issue_store',N'Issue Store',password_hash,'ACTIVE','ACTIVE' FROM users WHERE user_id=$($f.adminId);
INSERT user_roles(user_id,role_id,branch_id,assigned_by)
SELECT u.user_id,r.role_id,$a,$($f.adminId) FROM users u JOIN roles r ON (u.username='qa_issue_head' AND r.code='DEPT_MANAGER') OR (u.username='qa_issue_store' AND r.code='STOREKEEPER');
INSERT memberships(user_id,branch_id,kind,recorded_by)
SELECT user_id,$a,'BRANCH_ACCESS',$($f.adminId) FROM users WHERE username IN ('qa_issue_head','qa_issue_store');
INSERT memberships(user_id,branch_id,department_id,kind,recorded_by)
SELECT user_id,$a,$($f.departmentId),'EMPLOYMENT',$($f.adminId) FROM users WHERE username IN ('qa_issue_head','qa_issue_store');
"@
$manager=(Call POST '/api/v2/auth/login' @{username='qa_issue_head';password=$password}).accessToken
$store=(Call POST '/api/v2/auth/login' @{username='qa_issue_store';password=$password}).accessToken
$lead=(Call POST '/api/v2/auth/login' @{username='qa_lead_a';password=$password}).accessToken
$tech=(Call POST '/api/v2/auth/login' @{username='qa_tech_a';password=$password}).accessToken
$techId=($techActors.Rows|Where-Object username -eq 'qa_tech_a').user_id
$supervisor=(Call POST '/api/v2/auth/login' @{username='qa_hks_a';password=$password}).accessToken
$hk=(Call POST '/api/v2/auth/login' @{username='qa_hk_a';password=$password}).accessToken
function IssueDetail($id,$token=$manager){Call GET "/api/v2/work/room-issues/$id" $null $token}
function IssueAct($id,$command,$token,$extra=@{},$expected=200){
 $d=IssueDetail $id $token
 $body=@{rowVersion=$d.work.fields.rowVersion;roomRowVersion=$d.room.rowVersion;operationKey=[Guid]::NewGuid().ToString();note='Isolated remediation verification'}
 foreach($key in $extra.Keys){$body[$key]=$extra[$key]}
 Call POST "/api/v2/work/room-issues/$id/$command" $body $token $expected
}
$broken=Call PUT "/api/v2/work/room-issue-types?branchId=$a" @{kind='BROKEN';name='Hong do vat';isActive=$true} $manager
$missing=Call PUT "/api/v2/work/room-issue-types?branchId=$a" @{kind='MISSING';name='Thieu do vat';isActive=$true} $manager
$null=Call PUT "/api/v2/work/room-issue-types?branchId=$a" @{kind='MISSING';name='Hong do vat';isActive=$true;rowVersion=$missing.rowVersion} $manager 409
$null=Call PUT "/api/v2/work/room-issue-types?branchId=$a" @{kind='BROKEN';name='Forbidden';isActive=$true;rowVersion=$broken.rowVersion} $supervisor 403
$null=Call GET "/api/v2/work/room-issue-types?branchId=$b" $null $manager 403
$null=Call GET "/api/v2/work/room-issues?branchId=$a" $null $hk 403
Check 'Head config uses approved BRANCH grants; duplicate name, non-head and foreign branch denied' $true
$issueRoom=(Call POST "/api/v2/catalog/rooms?branchId=$a" @{fields=@{code='901';roomTypeId=$type.roomTypeId;locationId=$floor.locationId;departmentId=$f.departmentId}} $admin).fields
$priorCleaning=(Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';durationMinutes=30;title='Cleaning before incident review';roomId=$issueRoom.roomId;assigneeUserId=$hkId} $supervisor).fields
$priorCleaning=(WorkAction $priorCleaning start $hk @{reason=$null}).fields
$priorCleaning=(WorkAction $priorCleaning complete $hk @{reason=$null}).fields
$asset=(Call POST "/api/v2/catalog/assets?branchId=$a" @{fields=@{name='Room television';conditionCode='FAULTY';roomId=$issueRoom.roomId;departmentId=$f.departmentId}} $admin).fields
$outside=(Call POST "/api/v2/catalog/assets?branchId=$a" @{fields=@{name='Outside object';conditionCode='GOOD';departmentId=$f.departmentId}} $admin).fields
function NewIssueBody($typeId=$broken.referenceId){
 $o=Call GET "/api/v2/work/room-issue-objects?branchId=$a&roomId=$($issueRoom.roomId)" $null $supervisor
 @{branchId=$a;roomId=$issueRoom.roomId;typeId=$typeId;assetId=$asset.assetId;roomRowVersion=$o.rowVersion;operationKey=[Guid]::NewGuid().ToString()}
}
$body=NewIssueBody;$body.assetId=$outside.assetId
$null=Call POST '/api/v2/work/room-issues' $body $supervisor 409
$body=NewIssueBody
$null=Call POST '/api/v2/work/room-issues' $body $hk 403
$issue=(Call POST '/api/v2/work/room-issues' $body $supervisor).fields;$id=$issue.workId
$null=Call POST '/api/v2/work/room-issues' $body $supervisor 409
Check 'Room report is fixed to actual room objects; staff and duplicate operation denied' ($issue.title -like '*Room television*901*')
$missingIssue=(Call POST '/api/v2/work/room-issues' (NewIssueBody $missing.referenceId) $supervisor).fields;$missingId=$missingIssue.workId
Check 'Multiple issues in one room share one active block' ((Sql "SELECT COUNT(*) n FROM room_blocks WHERE room_id=$($issueRoom.roomId) AND released_at IS NULL").Rows[0].n-eq 1)
$null=Call GET "/api/v2/work/room-issues/$id" $null $store 403
$null=WorkAction $issue triage $lead @{} 409
$null=IssueAct $id send-repair $manager @{} 409
$null=IssueAct $id approve $supervisor @{} 403
$null=IssueAct $id approve $manager
$null=IssueAct $id send-repair $manager
$null=IssueAct $id report-remedy $lead @{} 409
function RepairIssue($id){
 $null=IssueAct $id create-repair $lead
 $d=IssueDetail $id $lead;$workId=($d.maintenance|Sort-Object workId -Descending|Select-Object -First 1).workId
 $repair=(Call GET "/api/v2/work/$workId" $null $lead).work.fields
 $repair=(WorkAction $repair assign $lead @{userId=$techId}).fields
 foreach($verb in @('accept','start','complete')){$repair=(WorkAction $repair $verb $tech).fields}
 $null=IssueAct $id report-remedy $lead @{} 409
 $repair=(WorkAction $repair verify $lead).fields
 $null=IssueAct $id report-remedy $lead
}
RepairIssue $id
$null=IssueAct $id inspection-pass $supervisor @{} 409
$null=IssueAct $id request-inspection $manager
$null=IssueAct $id inspection-fail $supervisor @{note='Still broken during independent inspection'}
$null=IssueAct $id approve $manager
$null=IssueAct $id send-repair $manager
$null=IssueAct $id report-remedy $lead @{} 409
Check 'Broken flow requires assigned repair, lead review, head confirmation, independent supervisor; old repair cannot satisfy retry' $true
RepairIssue $id
$null=IssueAct $id request-inspection $manager
$beforeTurnover=(Sql "SELECT turnover_no FROM rooms WHERE room_id=$($issueRoom.roomId)").Rows[0].turnover_no
$null=IssueAct $id inspection-pass $supervisor
$state=(Sql "SELECT r.operational_status,r.turnover_no,r.current_cleaning_work_id,v.readiness FROM rooms r JOIN v_room_readiness v ON v.room_id=r.room_id WHERE r.room_id=$($issueRoom.roomId)").Rows[0]
Check 'Remediation pass opens new cleaning cycle but retains block for another unresolved issue' ($state.operational_status-eq 'DIRTY' -and $state.readiness-eq 'BLOCKED' -and $state.turnover_no-eq $beforeTurnover+1)
Check 'Old pending-inspection cleaning is cancelled and cannot carry clean proof into new cycle' ((Sql "SELECT status FROM work_items WHERE work_id=$($priorCleaning.workId)").Rows[0].status-eq 'CANCELLED' -and $state.current_cleaning_work_id-is [DBNull])
$roomState=(Call GET "/api/v2/rooms?branchId=$a" $null $admin)|Where-Object roomId -eq $issueRoom.roomId
$null=Call POST "/api/v2/rooms/$($issueRoom.roomId)/actions/unblock" @{rowVersion=$roomState.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Attempt premature block release'} $admin 409
$null=IssueAct $id inspection-pass $supervisor @{} 409
# Missing object: an asset currently assigned to this room; replacement is stock, no invented expected inventory.
$unit=(Call POST '/api/v2/catalog/references' @{fields=@{kind='UOM';name='Piece';code='REMEDY-EA'}} $admin).fields
$item=(Call POST '/api/v2/catalog/items' @{fields=@{name='Replacement device';baseUnitId=$unit.referenceId;code='REMEDY-DEVICE'}} $admin).fields
$warehouse=(Call POST "/api/v2/catalog/warehouses?branchId=$a" @{fields=@{name='Remedy warehouse';departmentId=$f.departmentId;code='REMEDY-WH'}} $admin).fields
$receipt=(Call POST '/api/v2/documents' @{branchId=$a;kind='RECEIPT';destinationWarehouseId=$warehouse.warehouseId} $admin).fields
$receipt=(Call POST "/api/v2/documents/$($receipt.documentId)/lines" @{rowVersion=$receipt.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=10} $admin).document.fields
$null=DocAction $receipt post $admin
$null=IssueAct $missingId approve $manager
$null=IssueAct $missingId request-material $manager @{departmentId=$f.departmentId;warehouseId=$warehouse.warehouseId;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2}
$d=IssueDetail $missingId $store;$mrId=$d.materials[0].documentId
$mrDetail=Call GET "/api/v2/documents/$mrId" $null $admin;$mr=$mrDetail.document.fields
$null=DocAction $mr reject $admin 409
$null=DocAction $mr close $admin
$null=IssueAct $missingId request-material $manager @{departmentId=$f.departmentId;warehouseId=$warehouse.warehouseId;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2}
$d=IssueDetail $missingId $store;$mrId=($d.materials|Sort-Object documentId -Descending|Select-Object -First 1).documentId
$mrDetail=Call GET "/api/v2/documents/$mrId" $null $admin;$mr=$mrDetail.document.fields
Check 'Closed remediation request can be replaced; new request reaches warehouse already authorized by Head' ($d.materials.Count-eq 2 -and $mr.status-eq 'APPROVED' -and $mr.approvedBy-eq $mr.createdBy)
$null=DocAction $mr approve $manager 403
$null=IssueAct $missingId report-remedy $store @{documentId=$mrId} 409
$null=DocAction $mr approve $admin 409
$draft=(Call POST "/api/v2/documents/$mrId/derive" @{rowVersion=$mr.rowVersion;operationKey=[Guid]::NewGuid().ToString();kind='ISSUE';warehouseId=$warehouse.warehouseId;reason='Supply missing room object';lines=@(@{lineId=$mrDetail.lines[0].fields.lineId;quantity=2})} $store).fields
$posted=(DocAction $draft post $store).fields
Check 'Warehouse fulfills authorized remediation directly with real stock issue and receiving room' ($posted.receivingRoomId-eq $issueRoom.roomId -and (Sql "SELECT SUM(signed_base_qty) qty FROM stock_movements WHERE item_id=$($item.itemId)").Rows[0].qty-eq 8)
$objects=Call GET "/api/v2/work/room-issue-objects?branchId=$a&roomId=$($issueRoom.roomId)" $null $supervisor
Check 'Only delivered stock appears in room objects' (@($objects.items|Where-Object itemId -eq $item.itemId).Count-eq 1)
$issueDetail=Call GET "/api/v2/documents/$($posted.documentId)" $null $admin
$return=(Call POST '/api/v2/documents' @{branchId=$a;kind='RETURN';destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$posted.documentId} $admin).fields
$return=(Call POST "/api/v2/documents/$($return.documentId)/lines" @{rowVersion=$return.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2;sourceLineId=$issueDetail.lines[0].fields.lineId} $admin).document.fields
$return=(DocAction $return approve $worker).fields
$null=DocAction $return post $admin
$null=IssueAct $missingId report-remedy $store @{documentId=$mrId} 409
$objects=Call GET "/api/v2/work/room-issue-objects?branchId=$a&roomId=$($issueRoom.roomId)" $null $supervisor
Check 'Returned stock is excluded and cannot prove remediation even if MR says fully issued' (@($objects.items|Where-Object itemId -eq $item.itemId).Count-eq 0)
$null=IssueAct $missingId request-material $manager @{departmentId=$f.departmentId;warehouseId=$warehouse.warehouseId;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2}
$oldMrId=$mrId;$d=IssueDetail $missingId $store;$mrId=($d.materials|Sort-Object documentId -Descending|Select-Object -First 1).documentId
$null=IssueAct $missingId report-remedy $store @{documentId=$oldMrId} 409
$mrDetail=Call GET "/api/v2/documents/$mrId" $null $admin;$mr=$mrDetail.document.fields
$draft=(Call POST "/api/v2/documents/$mrId/derive" @{rowVersion=$mr.rowVersion;operationKey=[Guid]::NewGuid().ToString();kind='ISSUE';warehouseId=$warehouse.warehouseId;reason='Supply replacement after return';lines=@(@{lineId=$mrDetail.lines[0].fields.lineId;quantity=2})} $store).fields
$null=DocAction $draft post $store
Check 'Replacement requires current material request, not earlier fulfilled request' $true
$null=IssueAct $missingId report-remedy $store @{documentId=$mrId}
$null=IssueAct $missingId request-inspection $manager
$null=IssueAct $missingId inspection-pass $supervisor
Check 'Missing issue completes warehouse -> head -> supervisor -> cleaning required' ((IssueDetail $missingId).stage-eq 'ROOM_ISSUE_INSPECTION_PASSED')
Check 'Final issue releases shared issue block and still requires new cleaning' ((Sql "SELECT readiness FROM v_room_readiness WHERE room_id=$($issueRoom.roomId)").Rows[0].readiness-eq 'PENDING_CLEANING')
$notifications=Call GET '/api/v2/notifications' $null $manager
Check 'Cross-role notifications and phase audit persisted' (@($notifications.items).Count-gt 0 -and (Sql "SELECT COUNT(*) n FROM audit_logs WHERE action LIKE 'ROOM_ISSUE_%'").Rows[0].n-gt 0)
$storeNotifications=Call GET '/api/v2/notifications' $null $store
Check 'Storekeeper receives routed issue notifications without INCIDENT_VIEW grant' (@($storeNotifications.items|Where-Object {$_.fields.notificationType-eq 'ROOM_ISSUE_MATERIAL_REQUESTED'}).Count-gt 0)
# Ordinary MRs remain independently approved. Direct SQL cannot convert one into an unproven self-approval.
$ordinary=(Call POST '/api/v2/documents' @{branchId=$a;kind='MATERIAL_REQUEST';departmentId=$f.departmentId;sourceWarehouseId=$warehouse.warehouseId} $manager).fields
$ordinary=(Call POST "/api/v2/documents/$($ordinary.documentId)/lines" @{rowVersion=$ordinary.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=1} $manager).document.fields
$ordinary=(DocAction $ordinary submit $manager).fields
$null=DocAction $ordinary approve $manager 403
$ordinary=(DocAction $ordinary approve $admin).fields
$rejected=$false
try{$null=Sql "UPDATE inventory_documents SET created_by=approved_by,source_work_id=$missingId,receiving_room_id=$($issueRoom.roomId) WHERE document_id=$($ordinary.documentId)"}catch{if($_.Exception.ToString()-notlike '*Self approval is limited*'){throw};$rejected=$true}
Check 'SQL rejects changing creator to bypass remediation proof; ordinary MR still needs independent approval' $rejected
