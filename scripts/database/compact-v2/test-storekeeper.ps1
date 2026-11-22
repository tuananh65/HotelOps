# Every write is confined to the fresh isolated fixture created by Test-Backend.ps1.
if($database -notlike 'HotelOpsCompactApiTest*'){throw 'Isolated database required'}
$actors=Sql (Get-Content (Join-Path $PSScriptRoot 'storekeeper-fixture.sql') -Raw -Encoding UTF8)
$store=(Call POST '/api/v2/auth/login' @{username='qa_store_a';password=$password}).accessToken
$peer=(Call POST '/api/v2/auth/login' @{username='qa_store_peer';password=$password}).accessToken
$foreign=(Call POST '/api/v2/auth/login' @{username='qa_store_b';password=$password}).accessToken
$storeId=($actors.Rows|Where-Object username -eq 'qa_store_a').user_id
$peerId=($actors.Rows|Where-Object username -eq 'qa_store_peer').user_id
$a=$f.branchId;$b=$f.otherBranchId
function NewStockDoc($kind,$extra=@{},$token=$store){$body=@{branchId=$a;kind=$kind;reason='Isolated inventory verification';operationKey=[Guid]::NewGuid().ToString()};foreach($key in $extra.Keys){$body[$key]=$extra[$key]};return (Call POST '/api/v2/documents' $body $token).fields}
function StockLine($doc,$qty,$token=$store,$source=$null,$count=$null){return Call POST "/api/v2/documents/$($doc.documentId)/lines" @{rowVersion=$doc.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=$qty;sourceLineId=$source;countedQty=$count} $token}
function Fresh($doc,$token=$store){return (Call GET "/api/v2/documents/$($doc.documentId)" $null $token).document.fields}
function Derived($source,$kind,$lineId,$qty,$token=$store,$expected=200){$source=Fresh $source $token;return Call POST "/api/v2/documents/$($source.documentId)/derive" @{rowVersion=$source.rowVersion;operationKey=[Guid]::NewGuid().ToString();kind=$kind;reason='Linked isolated document verification';warehouseId=$warehouse.warehouseId;lines=@(@{lineId=$lineId;quantity=$qty})} $token $expected}
function StockBalance($warehouseId=$warehouse.warehouseId){return [decimal](Sql "SELECT COALESCE(SUM(signed_base_qty),0) qty FROM stock_movements WHERE warehouse_id=$warehouseId AND item_id=$($item.itemId)").Rows[0].qty}
function ApprovedRequest($qty){$d=NewStockDoc MATERIAL_REQUEST @{departmentId=$f.departmentId;sourceWarehouseId=$warehouse.warehouseId} $worker;$l=StockLine $d $qty $worker;$d=(DocAction $l.document.fields submit $worker).fields;$d=(DocAction $d approve $admin).fields;return @{doc=$d;line=$l.line.fields}}
$me=Call GET '/api/v2/auth/me' $null $store
Check 'Storekeeper has exact 16 baseline grants and OWN attendance' (@($me.grants).Count-eq 16 -and @($me.grants|Where-Object {$_.code-eq 'ATTENDANCE_RECORD' -and $_.dataScope-eq 'OWN'}).Count-eq 1)
foreach($scope in @(@{token=$store;own=$a;other=$b},@{token=$foreign;own=$b;other=$a})){
 foreach($path in @('insights/inventory','insights/stock','workspace/options','documents?kind=RECEIPT','workday?date=2026-11-23')){$sep=if($path.Contains('?')){'&'}else{'?'};$null=Call GET "/api/v2/$path$($sep)branchId=$($scope.own)" $null $scope.token;$null=Call GET "/api/v2/$path$($sep)branchId=$($scope.other)" $null $scope.token 403}
}
Check 'Both Storekeeper branches enforce stock/dashboard/documents/Workday scope' $true
foreach($path in @('users','catalog/roles','assets/1','rooms', 'insights/audit')){$null=Call GET "/api/v2/$path`?branchId=$a" $null $store $(if($path-eq 'assets/1'){404}else{403})}
$null=Call POST '/api/v2/work' @{branchId=$a;kind='CLEANING';title='Forbidden'} $store 403
Check 'No User/Role/Asset/Room/Cleaning/Audit authority' $true
$unit=(Call POST "/api/v2/catalog/references?branchId=$a" @{fields=@{kind='UOM';code='STORE-EA';name='Piece'}} $admin).fields
$warehouse=(Call POST "/api/v2/catalog/warehouses?branchId=$a" @{fields=@{code='STORE-A';name='Store A';departmentId=$f.departmentId}} $store).fields
$destination=(Call POST "/api/v2/catalog/warehouses?branchId=$a" @{fields=@{code='STORE-DEST';name='Store destination';departmentId=$f.departmentId}} $store).fields
$otherWarehouse=(Call POST "/api/v2/catalog/warehouses?branchId=$b" @{fields=@{code='STORE-B';name='Store B'}} $foreign).fields
$item=(Call POST "/api/v2/catalog/items?branchId=$a" @{fields=@{code='STORE-TOWEL';name='Towel';baseUnitId=$unit.referenceId}} $store).fields
$null=Call POST "/api/v2/catalog/items?branchId=$a" @{fields=@{code='STORE-TOWEL';name='Duplicate';baseUnitId=$unit.referenceId}} $store 409
$null=Call POST "/api/v2/catalog/items?branchId=$b" @{fields=@{code='FORBIDDEN';name='Forbidden';baseUnitId=$unit.referenceId}} $store 403
$item=(Call PUT "/api/v2/catalog/items/$($item.itemId)?branchId=$a" @{rowVersion=$item.rowVersion;fields=@{name='Branch managed towel'}} $store).fields
Check 'Existing shared item catalog create/edit enforces selected branch grant and unique code' $true
$policy=(Call POST "/api/v2/catalog/stock-policies?branchId=$a" @{fields=@{warehouseId=$warehouse.warehouseId;itemId=$item.itemId;minimumQty=10;targetQty=30}} $store).fields
$insight=Call GET "/api/v2/insights/inventory?branchId=$a" $null $store
Check 'Policy establishes zero-stock tracked item; real minimum drives low-stock KPI' ($insight.metrics.emptyItems-eq 1 -and $insight.metrics.lowStock-eq 1)
$key=[Guid]::NewGuid().ToString();$create=@{branchId=$a;kind='RECEIPT';destinationWarehouseId=$warehouse.warehouseId;reason='Initial verified stock';operationKey=$key}
$receipt=(Call POST '/api/v2/documents' $create $store).fields
$null=Call POST '/api/v2/documents' $create $store 409
$receipt=(StockLine $receipt 30).document.fields;$stale=$receipt
$post=@{rowVersion=$receipt.rowVersion;operationKey=[Guid]::NewGuid().ToString()}
$receipt=(Call POST "/api/v2/documents/$($receipt.documentId)/actions/post" $post $store).fields
$null=Call POST "/api/v2/documents/$($receipt.documentId)/actions/post" $post $store 409
Check 'Receipt posts +30; retries do not duplicate draft or posting' ((StockBalance)-eq 30 -and (Sql "SELECT COUNT(*) qty FROM stock_movements WHERE item_id=$($item.itemId)").Rows[0].qty-eq 1)
$secondUnit=(Call POST "/api/v2/catalog/references?branchId=$a" @{fields=@{kind='UOM';code='STORE-OTHER';name='Other unit'}} $admin).fields
$null=Call PUT "/api/v2/catalog/items/$($item.itemId)?branchId=$a" @{rowVersion=$item.rowVersion;fields=@{baseUnitId=$secondUnit.referenceId}} $store 409
$null=Call POST "/api/v2/documents/$($receipt.documentId)/lines" @{rowVersion=$stale.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=2} $store 409
Check 'Item unit with history and stale/posted document edits are rejected' $true
$unapproved=NewStockDoc MATERIAL_REQUEST @{departmentId=$f.departmentId;sourceWarehouseId=$warehouse.warehouseId} $worker
$unapprovedLine=StockLine $unapproved 5 $worker;$unapproved=$unapprovedLine.document.fields
$null=Derived $unapproved ISSUE $unapprovedLine.line.fields.lineId 1 $store 403
$null=DocAction $unapproved approve $store 403
Check 'Storekeeper cannot approve MR or derive issue from unapproved request' $true
$mr=ApprovedRequest 10
$mrDetail=Call GET "/api/v2/documents/$($mr.doc.documentId)" $null $store
Check 'MR exposes fulfillment derivation without MR approval capability' (-not $mrDetail.canApprove -and $mrDetail.inventory.derivations-contains 'ISSUE')
$draft=Derived $mr.doc ISSUE $mr.line.lineId 4
$issue=$draft.fields;$before=Fresh $mr.doc
$null=Derived $before ISSUE $mr.line.lineId 11 $store 409
Check 'Linked issue draft is atomic and rejects quantity over approved remaining' ((StockBalance)-eq 30 -and (Call GET "/api/v2/documents/$($issue.documentId)" $null $store).lines.Count-eq 1)
$issue=(DocAction $issue post $store).fields
$mrDetail=Call GET "/api/v2/documents/$($mr.doc.documentId)" $null $store
Check 'Approved MR -> partial issue -> ledger and remaining quantities agree' ((StockBalance)-eq 26 -and $mrDetail.document.fields.status-eq 'PARTIALLY_ISSUED' -and $mrDetail.fulfillment[0].fields.missingBaseQty-eq 6)
$issue2=(Derived $mr.doc ISSUE $mr.line.lineId 6).fields;$issue2=(DocAction $issue2 post $store).fields
$mrDetail=Call GET "/api/v2/documents/$($mr.doc.documentId)" $null $store
Check 'Final fulfillment marks source fully issued only after real movement' ((StockBalance)-eq 20 -and $mrDetail.document.fields.status-eq 'FULLY_ISSUED' -and $mrDetail.fulfillment[0].fields.missingBaseQty-eq 0)
$large=ApprovedRequest 100;$over=(Derived $large.doc ISSUE $large.line.lineId 25).fields
$null=DocAction $over post $store 409
Check 'Insufficient stock rolls back document header and all ledger effects' ((StockBalance)-eq 20 -and (Fresh $over).status-eq 'DRAFT')
$null=Call GET "/api/v2/documents/$($issue.documentId)" $null $foreign 403
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='TRANSFER';sourceWarehouseId=$warehouse.warehouseId;destinationWarehouseId=$otherWarehouse.warehouseId} $store 409
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='RETURN';destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=(NewStockDoc RECEIPT @{branchId=$b;destinationWarehouseId=$otherWarehouse.warehouseId} $foreign).documentId} $store 403
Check 'Foreign documents and cross-branch warehouse/source combinations are rejected' $true
$issueLine=(Call GET "/api/v2/documents/$($issue.documentId)" $null $store).lines[0].fields
$return=(Derived $issue RETURN $issueLine.lineId 2).fields
$null=DocAction $return post $store 409
$null=DocAction $return approve $store 403
$return=(DocAction $return approve $peer).fields
$returnLine=(Call GET "/api/v2/documents/$($return.documentId)" $null $store).lines[0].fields
$null=Call PUT "/api/v2/documents/$($return.documentId)/lines/$($returnLine.lineId)" @{rowVersion=$return.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=3;sourceLineId=$issueLine.lineId} $store 409
$null=DocAction $return post $peer 409
$return=(DocAction $return post $store).fields
Check 'Return needs independent approval, freezes approved lines, separates poster and restores +2' ((StockBalance)-eq 22)
$null=Derived $issue RETURN $issueLine.lineId 3 $store 409
Check 'Cumulative return cannot exceed original issue' $true
$transfer=NewStockDoc TRANSFER @{sourceWarehouseId=$warehouse.warehouseId;destinationWarehouseId=$destination.warehouseId}
$transferLine=StockLine $transfer 5;$transfer=(DocAction $transferLine.document.fields post $store).fields
Check 'Transfer outbound deducts source and waits for destination receipt' ((StockBalance)-eq 17 -and (StockBalance $destination.warehouseId)-eq 0 -and $transfer.status-eq 'IN_TRANSIT')
$receive=(Derived $transfer TRANSFER_RECEIPT $transferLine.line.fields.lineId 3).fields;$receive=(DocAction $receive post $store).fields
$transferDetail=Call GET "/api/v2/documents/$($transfer.documentId)" $null $store
Check 'Partial transfer receipt updates destination and derived transfer status' ((StockBalance $destination.warehouseId)-eq 3 -and $transferDetail.document.fields.status-eq 'PARTIALLY_RECEIVED')
$receive=(Derived $transfer TRANSFER_RECEIPT $transferLine.line.fields.lineId 2).fields;$receive=(DocAction $receive post $store).fields
$null=Derived $transfer TRANSFER_RECEIPT $transferLine.line.fields.lineId 1 $store 409
Check 'Full transfer receipt has no duplicate receiving and conserves total stock' ((StockBalance $destination.warehouseId)-eq 5 -and (StockBalance)-eq 17)
$null=Call POST '/api/v2/documents' @{branchId=$a;kind='TRANSFER';sourceWarehouseId=$warehouse.warehouseId;destinationWarehouseId=$warehouse.warehouseId} $store 409
Check 'Transfer rejects identical source/destination warehouses' $true
$replen=NewStockDoc REPLENISHMENT @{destinationWarehouseId=$warehouse.warehouseId;supplierName='Test supplier'}
$rl=StockLine $replen 8;$replen=$rl.document.fields
$received=(Derived $replen RECEIPT $rl.line.fields.lineId 3).fields;$received=(DocAction $received post $store).fields
Check 'Replenishment receipt increases stock once and marks partial receipt' ((StockBalance)-eq 20 -and (Fresh $replen).status-eq 'PARTIALLY_RECEIVED')
$received2=(Derived $replen RECEIPT $rl.line.fields.lineId 5).fields
$postKey=[Guid]::NewGuid().ToString();$body=@{rowVersion=$received2.rowVersion;operationKey=$postKey}
$received2=(Call POST "/api/v2/documents/$($received2.documentId)/actions/post" $body $store).fields
$null=Call POST "/api/v2/documents/$($received2.documentId)/actions/post" $body $store 409
$null=Derived $replen RECEIPT $rl.line.fields.lineId 1 $store 403
$replen=(DocAction (Fresh $replen) close $store).fields
Check 'Replenishment completion/retry/close cannot add stock twice' ((StockBalance)-eq 25 -and $replen.status-eq 'CLOSED')
$stocktake=NewStockDoc STOCKTAKE @{sourceWarehouseId=$warehouse.warehouseId}
$count=StockLine $stocktake 1 $store $null 23;$stocktake=(DocAction $count.document.fields count $store).fields
Check 'Stocktake snapshots expected and counted quantities without changing stock' ($count.line.fields.expectedQty-eq 25 -and $count.line.fields.countedQty-eq 23 -and (StockBalance)-eq 25)
$adjust=(Derived $stocktake ADJUSTMENT $count.line.fields.lineId 2).fields
$null=DocAction $adjust post $store 409
$adjust=(DocAction $adjust approve $peer).fields;$adjust=(DocAction $adjust post $store).fields
Check 'Independently approved adjustment posts exact -2 discrepancy' ((StockBalance)-eq 23)
$null=Derived $stocktake ADJUSTMENT $count.line.fields.lineId 2 $store 409
Check 'Same count cannot be adjusted twice' $true
$reverse=(Derived $adjust REVERSAL (Call GET "/api/v2/documents/$($adjust.documentId)" $null $store).lines[0].fields.lineId 2).fields
$reverse=(DocAction $reverse approve $peer).fields;$reverse=(DocAction $reverse post $store).fields
$null=Derived $adjust REVERSAL (Call GET "/api/v2/documents/$($adjust.documentId)" $null $store).lines[0].fields.lineId 2 $store 409
Check 'Reversal references exact original movement and cannot reverse twice' ((StockBalance)-eq 25 -and (Sql "SELECT COUNT(*) qty FROM stock_movements WHERE reversal_of_movement_id IS NOT NULL").Rows[0].qty-eq 1)
$null=Derived $issue REVERSAL $issueLine.lineId 4 $store 403
Check 'Reversal of a document with posted dependent return is blocked' $true
$stocktake2=NewStockDoc STOCKTAKE @{sourceWarehouseId=$warehouse.warehouseId}
$count2=StockLine $stocktake2 1 $store $null 24;$stocktake2=(DocAction $count2.document.fields count $store).fields
$changed=NewStockDoc RECEIPT @{destinationWarehouseId=$warehouse.warehouseId};$changed=(StockLine $changed 1).document.fields;$changed=(DocAction $changed post $store).fields
$adjust2=(Derived $stocktake2 ADJUSTMENT $count2.line.fields.lineId 1).fields;$adjust2=(DocAction $adjust2 approve $peer).fields;$null=DocAction $adjust2 post $store 409
Check 'Changed stock after count requires recount and rolls back adjustment' ((StockBalance)-eq 26 -and (Fresh $adjust2).status-eq 'DRAFT')
$ledger=Call GET "/api/v2/insights/ledger?branchId=$a&warehouseId=$($warehouse.warehouseId)&itemId=$($item.itemId)&page=1" $null $store
$null=Call GET "/api/v2/insights/ledger?branchId=$a&warehouseId=$($otherWarehouse.warehouseId)&itemId=$($item.itemId)" $null $store 404
$insight=Call GET "/api/v2/insights/inventory?branchId=$a" $null $store
Check 'Scoped ledger exposes business documents/actor; dashboard reads real posting counts' ($ledger.total-gt 5 -and $ledger.items[0].actor -and $insight.metrics.receipts-ge 3 -and $insight.metrics.issues-eq 2 -and $insight.timeline.Count-gt 0)
$null=Call GET "/api/v2/documents?branchId=$a&kind=RECEIPT&sort=bad" $null $store 400
$null=Call GET "/api/v2/documents?branchId=$a&kind=RECEIPT&from=2026-12-01&to=2026-11-01" $null $store 400
$listed=Call GET "/api/v2/documents?branchId=$a&kind=RECEIPT&sort=oldest&pageSize=1&search=$($receipt.documentNo)" $null $store
Check 'Server search/sort/date validation and pagination execute before page slicing' ($listed.total-eq 1 -and $listed.items[0].fields.documentId-eq $receipt.documentId)
$shift=(Sql "SELECT TOP 1 shift_id FROM workday_shifts WHERE role_id=(SELECT role_id FROM roles WHERE code='STOREKEEPER')").Rows[0].shift_id
$today=(Get-Date).ToString('yyyy-MM-dd');$future=(Get-Date).AddDays(2).ToString('yyyy-MM-dd')
function AssignStoreShift($user,$date){$version=[Convert]::ToBase64String((Sql "SELECT row_version FROM workday_shifts WHERE shift_id=$shift").Rows[0].row_version);return (Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$user;workDate=$date;rowVersion=$version;operationKey=[Guid]::NewGuid().ToString()} $admin).fields}
$assigned=AssignStoreShift $storeId $today
$peerShift=AssignStoreShift $peerId $today
$futureShift=AssignStoreShift $storeId $future
$null=Call POST '/api/v2/workday/assignments' @{shiftId=$shift;userId=$storeId;workDate=$today;rowVersion=$assigned.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $store 403
$null=Call POST "/api/v2/workday/assignments/$($peerShift.assignmentId)/start" @{rowVersion=$peerShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $store 403
$null=Call POST "/api/v2/workday/assignments/$($futureShift.assignmentId)/start" @{rowVersion=$futureShift.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $store 409
$working=(Call POST "/api/v2/workday/assignments/$($assigned.assignmentId)/start" @{rowVersion=$assigned.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $store).fields
$ended=(Call POST "/api/v2/workday/assignments/$($working.assignmentId)/end" @{rowVersion=$working.rowVersion;operationKey=[Guid]::NewGuid().ToString()} $store).fields
Check 'Workday start/end is own-only; no scheduling or future-start privilege' ($ended.status-eq 'ENDED')
$audit=(Sql "SELECT COUNT(*) qty FROM audit_logs WHERE actor_user_id=$storeId AND entity_type='InventoryDocument'").Rows[0].qty
$duplicates=(Sql 'SELECT COUNT(*) qty FROM (SELECT line_id,warehouse_id FROM stock_movements GROUP BY line_id,warehouse_id HAVING COUNT(*)>1) x').Rows[0].qty
Check 'Inventory writes retain audit and unique movement trace without duplicates' ($audit-gt 10 -and $duplicates-eq 0)
$reversibleLine=(Call GET "/api/v2/documents/$($issue2.documentId)" $null $store).lines[0].fields
$reverseIssue=(Derived $issue2 REVERSAL $reversibleLine.lineId 6).fields;$reverseIssue=(DocAction $reverseIssue approve $peer).fields;$reverseIssue=(DocAction $reverseIssue post $store).fields
$restored=Call GET "/api/v2/documents/$($mr.doc.documentId)" $null $store
Check 'Reversing an issue recomputes MR state and approved remaining quantity' ($restored.document.fields.status-eq 'PARTIALLY_ISSUED' -and $restored.fulfillment[0].fields.missingBaseQty-eq 6 -and (StockBalance)-eq 32)
$replen2=NewStockDoc REPLENISHMENT @{destinationWarehouseId=$warehouse.warehouseId};$r2=StockLine $replen2 2;$replen2=$r2.document.fields
$incoming=(Derived $replen2 RECEIPT $r2.line.fields.lineId 2).fields;$incoming=(DocAction $incoming post $store).fields
$incomingLine=(Call GET "/api/v2/documents/$($incoming.documentId)" $null $store).lines[0].fields
$reverseReceipt=(Derived $incoming REVERSAL $incomingLine.lineId 2).fields;$reverseReceipt=(DocAction $reverseReceipt approve $peer).fields;$reverseReceipt=(DocAction $reverseReceipt post $store).fields
Check 'Reversing replenishment receipt restores request state without duplicate stock' ((Fresh $replen2).status-eq 'REQUESTED' -and (StockBalance)-eq 32)
$receivedTransfers=Call GET "/api/v2/documents?branchId=$a&kind=TRANSFER&status=RECEIVED&pageSize=1" $null $store
Check 'Transfer projected-status filter runs before pagination' ($receivedTransfers.total-eq 1 -and $receivedTransfers.items[0].fields.documentId-eq $transfer.documentId)
$savedLive=@{};foreach($name in @('STORE_TEST_DATABASE','STORE_TEST_TOKEN','STORE_TEST_BASE','STORE_TEST_WAREHOUSE','STORE_TEST_REQUEST')){$savedLive[$name]=[Environment]::GetEnvironmentVariable($name)}
try {
 $env:STORE_TEST_DATABASE=$database;$env:STORE_TEST_TOKEN=$store;$env:STORE_TEST_BASE="http://127.0.0.1:$Port";$env:STORE_TEST_WAREHOUSE=[string]$warehouse.warehouseId;$env:STORE_TEST_REQUEST=[string]$large.doc.documentId
 Push-Location (Join-Path $root 'frontend')
 try {node tests/compact-storekeeper-live.mjs;if($LASTEXITCODE-ne 0){throw 'Live Storekeeper browser failed'}}finally{Pop-Location}
 Check 'Live browser creates/posts receipt and fulfills MR through real API; SQL delta +2' ((StockBalance)-eq 34)
}finally{foreach($name in $savedLive.Keys){[Environment]::SetEnvironmentVariable($name,$savedLive[$name])}}
