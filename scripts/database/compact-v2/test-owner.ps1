if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated fixture required'}
$fixture=Sql (Get-Content (Join-Path $PSScriptRoot 'owner-fixture.sql') -Raw -Encoding UTF8)
$ownerId=$fixture.Rows[0].owner_id;$a=$f.branchId;$b=$f.otherBranchId
$ownerToken=(Call POST '/api/v2/auth/login' @{username='qa_owner';password=$password}).accessToken
$reader=(Call POST '/api/v2/auth/login' @{username='qa_scope_reader';password=$password}).accessToken
$me=Call GET '/api/v2/auth/me' $null $ownerToken
Check 'Owner login has actual twelve ALL grants, global assignment, both branches and no fake membership' ($me.grants.Count-eq 12 -and @($me.grants|Where-Object {$_.dataScope-ne 'ALL' -or $null-ne $_.branchId}).Count-eq 0 -and $me.branches.Count-eq 2 -and $me.memberships.Count-eq 0)
$uom=(Call POST '/api/v2/catalog/references' @{fields=@{kind='UOM';code='OWN-EA';name='Piece'}} $admin).fields
$item=(Call POST "/api/v2/catalog/items?branchId=$a" @{fields=@{code='OWN-ITEM';name='Owner monitored item';baseUnitId=$uom.referenceId}} $admin).fields
$roomIds=@();$assets=@();$warehouses=@();$receipts=@()
foreach($branch in @($a,$b)){
 $department=(Sql "IF NOT EXISTS(SELECT 1 FROM departments WHERE branch_id=$branch) INSERT departments(branch_id,code,name) VALUES($branch,'OPS',N'Operations'); SELECT TOP 1 department_id FROM departments WHERE branch_id=$branch ORDER BY department_id").Rows[0].department_id
 $type=(Call POST "/api/v2/catalog/room-types?branchId=$branch" @{fields=@{code='OWN-STD';name='Standard';capacity=2}} $admin).fields
 $floor=(Call POST "/api/v2/catalog/locations?branchId=$branch" @{fields=@{code='OWN-F1';name='Floor 1';kind='FLOOR';floorNumber=1}} $admin).fields
 $room=(Call POST "/api/v2/catalog/rooms?branchId=$branch" @{fields=@{code='101';roomTypeId=$type.roomTypeId;locationId=$floor.locationId}} $admin).fields;$roomIds+=,$room
 $asset=(Call POST "/api/v2/catalog/assets?branchId=$branch" @{fields=@{assetCode="OWN-ASSET-$branch";name="Monitored device $branch";conditionCode='GOOD';roomId=$room.roomId}} $admin).fields;$assets+=,$asset
 $warehouse=(Call POST "/api/v2/catalog/warehouses?branchId=$branch" @{fields=@{code='OWN-WH';name='Warehouse'}} $admin).fields;$warehouses+=,$warehouse
 $receipt=(Call POST '/api/v2/documents' @{branchId=$branch;kind='RECEIPT';destinationWarehouseId=$warehouse.warehouseId} $admin).fields
 $receipt=(Call POST "/api/v2/documents/$($receipt.documentId)/lines" @{rowVersion=$receipt.rowVersion;itemId=$item.itemId;unitId=$uom.referenceId;quantity=7} $admin).document.fields
 $receipt=(DocAction $receipt post $admin).fields;$receipts+=,$receipt
 $null=Sql "INSERT inventory_policies(branch_id,warehouse_id,item_id,minimum_qty,target_qty) VALUES($branch,$($warehouse.warehouseId),$($item.itemId),10,20)"
 $null=Sql @"
DECLARE @severity bigint=(SELECT TOP 1 reference_id FROM reference_values WHERE kind='SEVERITY' AND code='CRITICAL');
IF @severity IS NULL BEGIN INSERT reference_values(kind,code,name) VALUES('SEVERITY','CRITICAL',N'Critical');SET @severity=SCOPE_IDENTITY(); END;
INSERT work_items(branch_id,work_no,kind,status,title,room_id,turnover_no,asset_id,severity_id,due_at,created_by)
SELECT $branch,CONVERT(varchar(36),NEWID()),k,s,N'Owner monitor '+k,$($room.roomId),0,$($asset.assetId),CASE WHEN k='INCIDENT' THEN @severity ELSE NULL END,DATEADD(hour,-1,SYSUTCDATETIME()),$ownerId
FROM (VALUES('CLEANING','PENDING_ASSIGNMENT'),('MAINTENANCE','PENDING'),('MAINTENANCE','COMPLETED'),('INCIDENT','OPEN'),('OPERATIONAL','PENDING_ASSIGNMENT'),('INTERNAL_REQUEST','SUBMITTED'))v(k,s);
DECLARE @clean bigint=(SELECT work_id FROM work_items WHERE branch_id=$branch AND kind='CLEANING');
UPDATE rooms SET current_cleaning_work_id=@clean WHERE room_id=$($room.roomId);
INSERT work_items(branch_id,work_no,kind,status,title,room_id,turnover_no,parent_work_id,created_by)
SELECT $branch,CONVERT(varchar(36),NEWID()),k,s,N'Owner monitor '+k,$($room.roomId),0,@clean,$ownerId FROM (VALUES('INSPECTION','ASSIGNED'),('REWORK','PENDING_ACCEPTANCE'))v(k,s);
INSERT work_templates(branch_id,code,kind,title,asset_id,frequency,frequency_interval,next_due_at,created_by)
VALUES($branch,'OWN-PM','MAINTENANCE',N'Owner preventive plan',$($asset.assetId),'WEEKLY',1,DATEADD(day,1,SYSUTCDATETIME()),$($f.adminId));
INSERT inventory_documents(branch_id,department_id,document_no,kind,status,created_by,reason) VALUES($branch,$department,CONVERT(varchar(36),NEWID()),'MATERIAL_REQUEST','PENDING_APPROVAL',$($f.adminId),N'Historical material request');
"@
}
$qc=(Call POST '/api/v2/templates/checklists' @{branchId=$a;code='OWN-QC';name='Owner evidence fixture';kind='INSPECTION';items=@(@{description='Required safety check';isRequired=$true;weight=1})} $admin).fields
$null=Sql @"
DECLARE @inspection bigint=(SELECT work_id FROM work_items WHERE branch_id=$a AND kind='INSPECTION'),@clean bigint=(SELECT work_id FROM work_items WHERE branch_id=$a AND kind='CLEANING');
UPDATE work_items SET checklist_template_id=$($qc.checklistTemplateId) WHERE work_id=@inspection;
INSERT work_checklist_items(branch_id,work_id,checklist_template_id,checklist_item_id,description_snapshot,required_snapshot,weight_snapshot,outcome,checked_by,checked_at)
SELECT $a,@inspection,checklist_template_id,checklist_item_id,description,is_required,weight,'PASS',$($f.adminId),SYSUTCDATETIME() FROM checklist_items WHERE checklist_template_id=$($qc.checklistTemplateId);
UPDATE work_items SET status='COMPLETED',completed_at=SYSUTCDATETIME() WHERE work_id=@clean OR (branch_id=$a AND kind='REWORK');
UPDATE work_items SET status='PASSED',result='PASS',verified_by=$($f.adminId),verified_at=SYSUTCDATETIME() WHERE work_id=@inspection;
UPDATE rooms SET current_cleaning_work_id=@clean WHERE room_id=$($roomIds[0].roomId);
UPDATE rooms SET operational_status='OCCUPIED' WHERE room_id=$($roomIds[0].roomId);
INSERT room_blocks(branch_id,room_id,reason,blocked_by) VALUES($a,$($roomIds[0].roomId),N'Owner blocked occupied fixture',$($f.adminId));
INSERT room_blocks(branch_id,room_id,reason,blocked_by,blocked_at) VALUES($b,$($roomIds[1].roomId),N'Future block fixture',$($f.adminId),DATEADD(day,1,SYSUTCDATETIME()));
INSERT work_items(branch_id,work_no,kind,status,title,created_by,created_at) VALUES($a,'OWN-MIDNIGHT','OPERATIONAL','PENDING_ASSIGNMENT',N'Midnight local fixture',$ownerId,'2026-11-01T18:30:00');
"@
for($i=0;$i-lt 26;$i++){$null=Call POST '/api/v2/work' @{branchId=$a;kind='OPERATIONAL';title="Paged monitored work $i"} $admin}
$null=Sql "INSERT work_items(branch_id,work_no,kind,status,title,due_at,created_by,completed_at) VALUES($b,'OWN-RESOLVED','INCIDENT','RESOLVED',N'Resolved awaiting independent closure',DATEADD(hour,-1,SYSUTCDATETIME()),$ownerId,SYSUTCDATETIME())"
$all=Call GET '/api/v2/insights/owner' $null $ownerToken
$one=Call GET "/api/v2/insights/owner?branchId=$a" $null $ownerToken;$two=Call GET "/api/v2/insights/owner?branchId=$b" $null $ownerToken
Check 'Global dashboard and branch comparison equal selected branch totals without duplicate work' ($all.metrics.branches-eq 2 -and $all.metrics.rooms-eq 2 -and $one.metrics.rooms-eq 1 -and $two.metrics.rooms-eq 1 -and $all.metrics.OPERATIONAL-eq ($one.metrics.OPERATIONAL+$two.metrics.OPERATIONAL))
Check 'Occupied and blocked overlap without double counting rooms; maintenance pending review stays open' ($all.metrics.occupied-eq 1 -and $all.metrics.blocked-eq 1 -and $all.metrics.MAINTENANCE-eq 4 -and $all.metrics.INCIDENT-eq 3 -and $all.critical.Count-eq 2)
Check 'Incident unresolved and unclosed SLA follow distinct current workflow definitions' ($all.metrics.INCIDENT_unresolved-eq 2 -and $all.metrics.INCIDENT_overdue-eq 3)
Check 'Shared inventory item counted once globally; low stock counts warehouse-item pairs only with policy' ($all.metrics.stockedItems-eq 1 -and $all.metrics.lowStock-eq 2 -and $all.metrics.pendingMaterials-eq 2)
$edge=Call GET "/api/v2/insights/owner?branchId=$a&from=2026-11-02&to=2026-11-02" $null $ownerToken
Check 'Date filter uses branch local day for SQL UTC values and preserves snapshot counts' ($edge.metrics.created-eq 1 -and $edge.timeline[0].created-eq 1 -and $edge.metrics.rooms-eq 1)
$null=Call GET '/api/v2/insights/owner?from=2026-11-23&to=2026-11-01' $null $ownerToken 400
$null=Call GET '/api/v2/insights/owner?from=2020-01-01&to=2026-11-23' $null $ownerToken 400
$null=Call GET '/api/v2/insights/owner?branchId=9223372036854775806' $null $ownerToken 403
Check 'Invalid date range and invalid branch are rejected without fallback to all' $true
$mixed=Call GET '/api/v2/insights/owner' $null $reader
$scoped=Call GET "/api/v2/insights/owner?branchId=$a" $null $reader
$null=Call GET '/api/v2/insights/owner/rooms' $null $reader 403
$null=Call GET '/api/v2/insights/owner/reports/rooms' $null $reader 403
$null=Call GET '/api/v2/work?kind=OPERATIONAL' $null $worker 403
$null=Call GET '/api/v2/documents?kind=RECEIPT' $null $worker 403
Check 'ALL dashboard/report does not widen branch-only domain grants; branch readers cannot use omitted branch as global' ($null-eq $mixed.metrics.rooms -and $scoped.metrics.rooms-eq 1)
foreach($path in @('rooms','assets','stock')){
 $rows=Call GET "/api/v2/insights/owner/$path" $null $ownerToken
 $selected=Call GET "/api/v2/insights/owner/$path`?branchId=$a" $null $ownerToken
 Check "Owner $path global and selected reads show correct branches" (@($rows|Select-Object -ExpandProperty branchId -Unique).Count-eq 2 -and @($selected|Where-Object branchId -ne $a).Count-eq 0)
}
foreach($kind in @('CLEANING','INSPECTION','REWORK','MAINTENANCE','INCIDENT','OPERATIONAL','INTERNAL_REQUEST')){
 $rows=Call GET "/api/v2/work?kind=$kind&sort=title&pageSize=100" $null $ownerToken
 $selected=Call GET "/api/v2/work?kind=$kind&branchId=$b" $null $ownerToken
 Check "Read-only $kind monitoring supports both branches and detail" (@($rows.items.fields|Select-Object -ExpandProperty branchId -Unique).Count-eq 2 -and @($selected.items.fields|Where-Object branchId -ne $b).Count-eq 0)
 foreach($w in $selected.items.fields){$null=Call GET "/api/v2/work/$($w.workId)" $null $ownerToken}
}
$paged=Call GET '/api/v2/insights/owner/reports/tasks?page=1' $null $ownerToken
$page2=Call GET '/api/v2/insights/owner/reports/tasks?page=2' $null $ownerToken
$search=Call GET '/api/v2/insights/owner/reports/tasks?search=Paged%20monitored%20work%2025' $null $ownerToken
Check 'Reports filter before paging; pages contain distinct rows and exact totals' ($paged.items.Count-eq 25 -and $page2.items.Count-gt 0 -and @($page2.items|Where-Object {$_.id-in $paged.items.id}).Count-eq 0 -and $search.total-eq 1)
foreach($report in @('overview','rooms','cleaning','inspection','maintenance','incidents','tasks','requests','materials','assets','stock','documents','preventive','staffing')){$null=Call GET "/api/v2/insights/owner/reports/$report" $null $ownerToken}
 $replenishment=(Call POST '/api/v2/documents' @{branchId=$a;kind='REPLENISHMENT';reason='Hidden from Owner without domain read grant'} $admin).fields
 $docs=Call GET '/api/v2/insights/owner/reports/documents' $null $ownerToken
 $null=Call GET "/api/v2/documents/$($replenishment.documentId)" $null $ownerToken 403
 Check 'Inventory report never links to Replenishment documents outside Owner domain permissions' (@($docs.items|Where-Object kind -eq 'REPLENISHMENT').Count-eq 0)
Check 'All permitted reports use real API projections; PM read works without PM_MANAGE' $true
$pm=(Call GET '/api/v2/insights/owner/reports/preventive' $null $ownerToken).items[0]
$null=Call GET "/api/v2/insights/owner/preventive/$($pm.id)" $null $ownerToken
$null=Call GET "/api/v2/templates?branchId=$a" $null $ownerToken 403
$staffing=Call GET '/api/v2/insights/owner/reports/staffing' $null $ownerToken
Check 'Staffing report exposes aggregates only, no user identities or emails' ((ConvertTo-Json $staffing -Depth 8)-notmatch 'employeeCode|fullName|email|password|token' -and $staffing.items.Count-gt 0)
$sample=(Call GET '/api/v2/work?kind=MAINTENANCE' $null $ownerToken).items[0].fields
$inspection=(Call GET '/api/v2/work?kind=INSPECTION' $null $ownerToken).items[0].fields
$clean=(Call GET '/api/v2/work?kind=CLEANING' $null $ownerToken).items[0].fields
$op=(Call GET '/api/v2/work?kind=OPERATIONAL' $null $ownerToken).items[0].fields
$inc=(Call GET '/api/v2/work?kind=INCIDENT' $null $ownerToken).items[0].fields
$null=Sql "INSERT asset_events(branch_id,asset_id,event_type,actor_user_id,to_user_id,handover_status,operation_key) VALUES($a,$($assets[0].assetId),'HANDOVER',$($f.adminId),$ownerId,'PENDING',NEWID())"
$before=Sql 'SELECT (SELECT COUNT(*) FROM work_events) we,(SELECT COUNT(*) FROM stock_movements) sm,(SELECT COUNT(*) FROM asset_events) ae,(SELECT COUNT(*) FROM room_events) re'
foreach($w in @($sample,$inspection,$clean,$op,$inc)){foreach($verb in @('assign','start','complete','verify','cancel','reopen')){$null=WorkAction $w $verb $ownerToken @{userId=$f.workerId} 403}}
foreach($kind in @('CLEANING','INSPECTION','MAINTENANCE','INCIDENT','OPERATIONAL','INTERNAL_REQUEST')){$null=Call POST '/api/v2/work' @{branchId=$a;kind=$kind;title='Forbidden Owner write';checklistTemplateId=1} $ownerToken 403}
Check 'Owner cannot create, assign, execute, inspect, review, cancel or reopen operational work' $true
foreach($verb in @('triage','escalate')){$null=WorkAction $inc $verb $ownerToken @{} 403}
foreach($room in $roomIds){foreach($verb in @('check-in','check-out','move','block','unblock')){$null=Call POST "/api/v2/rooms/$($room.roomId)/actions/$verb" @{rowVersion=$room.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Forbidden Owner room mutation'} $ownerToken 403}}
Check 'Incident triage/escalation and room occupancy/block mutations are forbidden' $true
foreach($asset in $assets){foreach($verb in @('move','condition','retire','handover','accept','reject')){$null=Call POST "/api/v2/assets/$($asset.assetId)/actions/$verb" @{rowVersion=$asset.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Forbidden Owner asset mutation';userId=$ownerId} $ownerToken 403}}
$null=Call POST "/api/v2/catalog/assets?branchId=$a" @{fields=@{assetCode='NO';name='No'}} $ownerToken 403
Check 'Asset read grant does not authorize creation, move, disposal or handover decisions' $true
foreach($kind in @('RECEIPT','ISSUE','RETURN','TRANSFER','STOCKTAKE','ADJUSTMENT','REVERSAL','MATERIAL_REQUEST')){$null=Call POST '/api/v2/documents' @{branchId=$a;kind=$kind} $ownerToken 403}
$mr=(Call GET '/api/v2/documents?kind=MATERIAL_REQUEST' $null $ownerToken).items[0].fields
foreach($verb in @('approve','reject','submit','cancel','close')){$null=DocAction $mr $verb $ownerToken 403}
$null=DocAction $receipts[0] post $ownerToken 403
Check 'Inventory/MR view never permits posting, approving, fulfilling or editing documents' $true
foreach($path in @('/api/v2/users',"/api/v2/insights/audit?branchId=$a","/api/v2/workday?branchId=$a",'/api/v2/catalog/roles')){$null=Call GET $path $null $ownerToken 403}
$null=Call POST '/api/v2/users' @{employeeCode='NO';username='no';fullName='No';password=$password} $ownerToken 403
$legacy=Sql "INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity) VALUES($a,$($f.departmentId),(SELECT role_id FROM roles WHERE code='QA_ALL'),N'Legacy Owner fixture',0,480,2);DECLARE @shift bigint=SCOPE_IDENTITY(); INSERT workday_assignments(shift_id,user_id,work_date,created_by) VALUES(@shift,$ownerId,CONVERT(date,GETDATE()),$($f.adminId));SELECT @shift shift_id,SCOPE_IDENTITY() assignment_id;"
$null=Call POST '/api/v2/workday/assignments' @{shiftId=$legacy.Rows[0].shift_id;userId=$ownerId;workDate='2026-11-23';rowVersion=$roomIds[0].rowVersion;operationKey=[Guid]::NewGuid().ToString()} $ownerToken 403
foreach($verb in @('start','end','cancel')){$null=Call POST "/api/v2/workday/assignments/$($legacy.Rows[0].assignment_id)/$verb" @{rowVersion=$roomIds[0].rowVersion;operationKey=[Guid]::NewGuid().ToString()} $ownerToken 403}
Check 'No audit grant, User/Role administration, Workday scheduling or attendance capability is borrowed' $true
$offer=Sql "INSERT work_events(branch_id,work_id,event_type,actor_user_id,recipient_user_id,operation_key,reason) VALUES($($op.branchId),$($op.workId),'HANDOVER_OFFERED',$($f.adminId),$ownerId,NEWID(),N'Historical handover context fixture'); SELECT SCOPE_IDENTITY() work_event_id;"
foreach($verb in @('handover-accept','handover-reject')){$null=WorkAction $op $verb $ownerToken @{relatedId=$offer.Rows[0].work_event_id} 403}
Check 'Legacy work handover invitation cannot turn read-only Owner into an executor' $true
$after=Sql 'SELECT (SELECT COUNT(*) FROM work_events) we,(SELECT COUNT(*) FROM stock_movements) sm,(SELECT COUNT(*) FROM asset_events) ae,(SELECT COUNT(*) FROM room_events) re'
Check 'Denied mutations leave stock, room, asset and work history unchanged except explicit fixture setup' ($after.Rows[0].we-eq ($before.Rows[0].we+1) -and $after.Rows[0].sm-eq $before.Rows[0].sm -and $after.Rows[0].ae-eq $before.Rows[0].ae -and $after.Rows[0].re-eq $before.Rows[0].re)
$json=ConvertTo-Json @{dashboard=$all;profile=$me;report=$paged;work=(Call GET "/api/v2/work/$($sample.workId)" $null $ownerToken)} -Depth 20
Check 'Authorized read payloads omit credential/session/password hashes and guest PII' ($json-notmatch 'passwordHash|tokenHash|securityStamp|passport|identityNumber|guestName')
$envs=@{OWNER_TEST_DATABASE=$database;OWNER_TEST_TOKEN=$ownerToken;OWNER_TEST_BASE="http://127.0.0.1:$Port";OWNER_TEST_A=[string]$a;OWNER_TEST_B=[string]$b;OWNER_TEST_WORK=[string]$sample.workId;OWNER_TEST_ROOM=[string]$roomIds[0].roomId;OWNER_TEST_ASSET=[string]$assets[0].assetId}
$savedOwner=@{};foreach($name in $envs.Keys){$savedOwner[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$envs[$name])}
try{Push-Location (Join-Path $root 'frontend');try{node tests/compact-owner-live.mjs;if($LASTEXITCODE-ne 0){throw 'Owner live browser failed'}}finally{Pop-Location}}
finally{foreach($name in $savedOwner.Keys){[Environment]::SetEnvironmentVariable($name,$savedOwner[$name])}}
Check 'Owner real browser global/branch/report/drill-down/read-only validation passed' $true
