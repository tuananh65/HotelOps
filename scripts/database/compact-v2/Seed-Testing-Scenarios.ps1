# Dot-sourced only by Seed-Testing.ps1; each mutation has a persisted checkpoint.
$priorityRows=Sql "SELECT reference_id FROM dbo.reference_values WHERE kind='PRIORITY' AND code='HIGH' AND is_active=1"
$severityRows=Sql "SELECT reference_id FROM dbo.reference_values WHERE kind='SEVERITY' AND code='NORMAL' AND is_active=1"
if($priorityRows.Rows.Count -ne 1 -or $severityRows.Rows.Count -ne 1){throw 'Initialize the fixed operational levels in SQL before seeding scenarios. The catalog API cannot create these values.'}
$priority=@{referenceId=[long]$priorityRows.Rows[0].reference_id}
$severity=@{referenceId=[long]$severityRows.Rows[0].reference_id}
$unit=Catalog 'unit' references @{kind='UOM';code='DEMO-EA';name='Cái / chai'}
$category=Catalog 'category' references @{kind='ITEM_CATEGORY';code='DEMO-SUPPLIES';name='Vật tư khách sạn'}
$assetType=Catalog 'asset-type' references @{kind='ASSET_TYPE';code='DEMO-AC';name='Điều hòa'}
$rooms=@{};$locations=@{};$checklists=@{}
foreach($site in @(@{key='sg';id=$sg;count=12;manager='demo_manager';dept=$hk},@{key='dn';id=$dn;count=4;manager='demo_dn_manager';dept=$departments['DEMO-DN.HK']})){
    $location=Catalog "$($site.key).floor" locations @{code='DEMO-F1';name='Tầng 1';kind='FLOOR';floorNumber=1} $site.id
    $locations[$site.key]=$location
    $standard=Catalog "$($site.key).type.std" room-types @{code='DEMO-STD';name='Standard';capacity=2} $site.id
    $suite=Catalog "$($site.key).type.suite" room-types @{code='DEMO-SUITE';name='Suite';capacity=4} $site.id
    for($i=1;$i-le $site.count;$i++){
        $roomType=$standard;if($i-ge 10){$roomType=$suite}
        $rooms["$($site.key).$i"]=Catalog "$($site.key).room.$i" rooms @{code=[string](100+$i);roomTypeId=$roomType.roomTypeId;locationId=$location.locationId;departmentId=$site.dept} $site.id
    }
    $checklists[$site.key]=Step "$($site.key).checklist" {
        (Call POST '/api/v2/templates/checklists' @{branchId=$site.id;code='DEMO-QC';name='Kiểm tra chất lượng phòng';kind='INSPECTION';items=@(
            @{description='Giường và ga sạch, không có vết bẩn';isRequired=$true;weight=2},
            @{description='Nhà tắm sạch, thiết bị hoạt động';isRequired=$true;weight=2},
            @{description='Đủ khăn và vật phẩm trong phòng';isRequired=$true;weight=1})} $site.manager).fields
    }
}
$assets=@()
for($i=1;$i-le 6;$i++){$assets+=Catalog "asset.$i" assets @{assetCode="DEMO-AC-$i";name="Điều hòa phòng $(100+$i)";assetTypeId=$assetType.referenceId;roomId=$rooms["sg.$i"].roomId;departmentId=$eng;conditionCode='GOOD';serialNumber="DEMO-SERIAL-$i"} $sg}
$main=Catalog 'warehouse.main' warehouses @{code='DEMO-MAIN';name='Kho tổng Sài Gòn';departmentId=$storeDept;locationId=$locations.sg.locationId} $sg
$floor=Catalog 'warehouse.floor' warehouses @{code='DEMO-FLOOR';name='Kho tầng Sài Gòn';departmentId=$hk;locationId=$locations.sg.locationId} $sg
$dnWarehouse=Catalog 'warehouse.dn' warehouses @{code='DEMO-MAIN';name='Kho Đà Nẵng';departmentId=$departments['DEMO-DN.STORE'];locationId=$locations.dn.locationId} $dn
$items=@();$names=@('Xà phòng','Dầu gội','Khăn tắm','Nước uống','Găng tay','Dung dịch vệ sinh')
for($i=0;$i-lt $names.Count;$i++){
    $item=Catalog "item.$i" items @{code="DEMO-ITEM-$($i+1)";name=$names[$i];baseUnitId=$unit.referenceId;categoryId=$category.referenceId};$items+=$item
    $null=Catalog "policy.$i" stock-policies @{warehouseId=$main.warehouseId;itemId=$item.itemId;minimumQty=20;targetQty=100} $sg
}

function Cleaning-Cycle($key,$room,$outcome='PASS',$manager='demo_manager',$cleaner='demo_hk1',$branch=$sg,$template=$checklists.sg){
    $clean=Work "$key.clean" CLEANING "Dọn phòng $($room.code) - $outcome" @{roomId=$room.roomId} $manager $branch
    $clean=Act "$key.clean" $clean assign $manager @{userId=$ids[$cleaner]}
    foreach($action in @('accept','start','complete')){$clean=Act "$key.clean" $clean $action $cleaner}
    if($outcome-eq 'PENDING'){return $clean}
    $inspection=Work "$key.inspect" INSPECTION "Kiểm tra phòng $($room.code) - $outcome" @{roomId=$room.roomId;parentWorkId=$clean.workId;checklistTemplateId=$template.checklistTemplateId} $manager $branch
    $inspection=Act "$key.inspect" $inspection assign $manager @{userId=$ids[$manager]}
    foreach($action in @('accept','start')){$inspection=Act "$key.inspect" $inspection $action $manager}
    $detail=Call GET "/api/v2/work/$($inspection.workId)" $null $manager
    $n=0;foreach($ci in $detail.checklist){$n++;$cf=$ci.fields;$result='PASS';if($outcome-eq 'FAIL' -and $n-eq 1){$result='FAIL'}
        $null=Step "$key.check.$n" {Call PUT "/api/v2/work/$($inspection.workId)/checklist/$($cf.workChecklistItemId)" @{rowVersion=$cf.rowVersion;outcome=$result;note='Dữ liệu kiểm thử mẫu'} $manager}
    }
    if($outcome-eq 'FAIL'){
        $null=Step "$key.finding" {Call POST "/api/v2/work/$($inspection.workId)/findings" @{rowVersion=$inspection.rowVersion;description='Ga giường còn vết bẩn, yêu cầu làm lại';workChecklistItemId=$detail.checklist[0].fields.workChecklistItemId;severityId=$severity.referenceId} $manager}
        $inspection=(Call GET "/api/v2/work/$($inspection.workId)" $null $manager).work.fields
    }
    $inspection=Act "$key.inspect" $inspection complete $manager
    $command='pass';if($outcome-eq 'FAIL'){$command='fail'}
    $inspection=Act "$key.inspect" $inspection $command $manager
    return $clean
}
foreach($i in 1..3){$null=Cleaning-Cycle "ready.$i" $rooms["sg.$i"]}
$null=Cleaning-Cycle 'dn.ready' $rooms['dn.1'] 'PASS' 'demo_dn_manager' 'demo_dn_hk' $dn $checklists.dn
foreach($i in 2..3){
    $arrival=Step "room.$i.check-in" {
        $r=Call GET "/api/v2/rooms?branchId=$sg" $null 'demo_reception'|Where-Object roomId -eq $rooms["sg.$i"].roomId
        Call POST "/api/v2/rooms/$($r.roomId)/actions/check-in" @{rowVersion=$r.rowVersion;operationKey=[Guid]::NewGuid().ToString()} 'demo_reception'
    }
    if($i-eq 3){$null=Step 'room.3.check-out' {Call POST "/api/v2/rooms/$($arrival.room.fields.roomId)/actions/check-out" @{rowVersion=$arrival.room.fields.rowVersion;operationKey=[Guid]::NewGuid().ToString()} 'demo_reception'}}
}
$pendingClean=Cleaning-Cycle 'pending' $rooms['sg.4'] 'PENDING'
$failedClean=Cleaning-Cycle 'failed' $rooms['sg.5'] 'FAIL'
$rework=Work 'rework' REWORK 'Làm lại ga giường phòng 105' @{roomId=$rooms['sg.5'].roomId;parentWorkId=$failedClean.workId}
$rework=Act 'rework' $rework assign demo_manager @{userId=$ids.demo_hk2}
$rework=Act 'rework' $rework accept demo_hk2
$assignedClean=Work 'assigned.clean' CLEANING 'Phòng 107 đã phân công' @{roomId=$rooms['sg.7'].roomId}
$assignedClean=Act 'assigned.clean' $assignedClean assign demo_manager @{userId=$ids.demo_hk2}

# Completed, pending review, paused, unassigned and cancelled operational work.
foreach($sample in @(@{key='task.closed';mode='CLOSED'},@{key='task.review';mode='REVIEW'},@{key='task.paused';mode='PAUSED'})){
    $task=Work $sample.key OPERATIONAL "Kiểm tra hành lang - $($sample.mode)" @{departmentId=$hk}
    $task=Act $sample.key $task assign demo_manager @{userId=$ids.demo_hk1}
    foreach($a in @('accept','start')){$task=Act $sample.key $task $a demo_hk1}
    if($sample.mode-eq 'PAUSED'){$task=Act $sample.key $task pause demo_hk1}else{
        $task=Act $sample.key $task complete demo_hk1
        if($sample.mode-eq 'CLOSED'){$task=Act $sample.key $task verify demo_manager}
    }
}
$unassigned=Work 'task.unassigned' OPERATIONAL 'Chuẩn bị hội nghị - chưa phân công' @{departmentId=$hk;priorityId=$priority.referenceId;dueAt=[DateTime]::UtcNow.AddDays(1).ToString('o')}
$cancelled=Work 'task.cancelled' OPERATIONAL 'Trang trí sự kiện đã hủy' @{departmentId=$hk}
$cancelled=Act 'task.cancelled' $cancelled cancel demo_manager
$handover=Work 'handover' OPERATIONAL 'Bàn giao bổ sung vật phẩm hành lang' @{departmentId=$hk}
$handover=Act 'handover' $handover assign demo_manager @{userId=$ids.demo_hk1}
foreach($a in @('accept','start')){$handover=Act 'handover' $handover $a demo_hk1}
$handover=Act 'handover' $handover handover-offer demo_hk1 @{userId=$ids.demo_hk2}
$null=Step 'handover.decision.accept' {
    $offer=(Call GET "/api/v2/work/$($handover.workId)" $null demo_hk2).events|Where-Object {$_.fields.eventType-eq 'HANDOVER_OFFERED'}|Select-Object -First 1
    (Call POST "/api/v2/work/$($handover.workId)/actions/handover-accept" @{rowVersion=$handover.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Chấp nhận bàn giao công việc mẫu';relatedId=$offer.fields.workEventId} demo_hk2).fields
}
$dependency=Work 'task.dependent' OPERATIONAL 'Bố trí phòng họp sau khi chuẩn bị hội nghị' @{departmentId=$hk}
$null=Step 'task.link' {Call POST "/api/v2/work/$($dependency.workId)/links" @{rowVersion=$dependency.rowVersion;targetWorkId=$unassigned.workId;relation='DEPENDS_ON'} demo_manager}

# Independent technician and verifier; room 110 stays blocked by an open incident.
$incident=Work 'incident.block' INCIDENT 'Rò nước phòng 110' @{roomId=$rooms['sg.10'].roomId;departmentId=$eng;severityId=$severity.referenceId} demo_reception
$null=Step 'room.block' {Call POST "/api/v2/rooms/$($rooms['sg.10'].roomId)/actions/block" @{rowVersion=$rooms['sg.10'].rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Tạm khóa phòng để xử lý rò nước';sourceWorkId=$incident.workId} demo_manager}
$maintenance=Work 'maintenance.closed' MAINTENANCE 'Bảo dưỡng điều hòa phòng 101' @{assetId=$assets[0].assetId;roomId=$rooms['sg.1'].roomId;departmentId=$eng}
$maintenance=Act 'maintenance.closed' $maintenance assign demo_manager @{userId=$ids.demo_tech}
foreach($a in @('accept','start','complete')){$maintenance=Act 'maintenance.closed' $maintenance $a demo_tech}
$maintenance=Act 'maintenance.closed' $maintenance verify demo_manager
$maintenance=Work 'maintenance.review' MAINTENANCE 'Bảo dưỡng điều hòa chờ nghiệm thu' @{assetId=$assets[1].assetId;departmentId=$eng}
$maintenance=Act 'maintenance.review' $maintenance assign demo_manager @{userId=$ids.demo_tech}
foreach($a in @('accept','start','complete')){$maintenance=Act 'maintenance.review' $maintenance $a demo_tech}
$internal=Work 'ir.closed' INTERNAL_REQUEST 'Nhờ kỹ thuật kiểm tra ổ điện kho' @{departmentId=$hk;requesterDepartmentId=$hk} demo_hk1
$internal=Act 'ir.closed' $internal submit demo_hk1
$internal=Act 'ir.closed' $internal route demo_manager @{relatedId=$eng}
$internal=Act 'ir.closed' $internal assign demo_manager @{userId=$ids.demo_tech}
foreach($a in @('accept','start','complete')){$internal=Act 'ir.closed' $internal $a demo_tech}
$internal=Act 'ir.closed' $internal verify demo_hk1
$internal=Work 'ir.submitted' INTERNAL_REQUEST 'Đề nghị bổ sung xe đẩy buồng phòng' @{departmentId=$hk;requesterDepartmentId=$hk} demo_hk2
$internal=Act 'ir.submitted' $internal submit demo_hk2
foreach($schedule in @(@{key='pm';kind='MAINTENANCE';dept=$eng;title='Bảo dưỡng điều hòa định kỳ';frequency='MONTHLY'},@{key='ops';kind='OPERATIONAL';dept=$hk;title='Kiểm tra khu vực chung hàng ngày';frequency='DAILY'})){
    $null=Step "schedule.$($schedule.key)" {(Call POST '/api/v2/templates/schedules' @{branchId=$sg;code='DEMO-'+$schedule.key;kind=$schedule.kind;title=$schedule.title;departmentId=$schedule.dept;frequency=$schedule.frequency;frequencyInterval=1;nextDueAt=[DateTime]::UtcNow.AddDays(1).ToString('o')} demo_manager).fields}
}
$null=Step 'asset.condition' {Call POST "/api/v2/assets/$($assets[5].assetId)/actions/condition" @{rowVersion=$assets[5].rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Thiết bị mẫu cần bảo dưỡng định kỳ';condition='FAULTY'} demo_manager}

# Real ledger postings; two items intentionally below the minimum, one with no stock.
$receipt=Document 'receipt.main' RECEIPT @{destinationWarehouseId=$main.warehouseId;supplierName='Nhà cung cấp giả lập DEMO'}
for($i=0;$i-lt 5;$i++){
    $qty=100;if($i-eq 4){$qty=5}
    $result=Line "receipt.main.$i" $receipt $items[$i] $qty;$receipt=$result.document.fields
}
$receipt=DocAct 'receipt.main' $receipt post
$receipt=Document 'receipt.floor' RECEIPT @{destinationWarehouseId=$floor.warehouseId;supplierName='Nhà cung cấp giả lập DEMO'}
$receipt=(Line 'receipt.floor' $receipt $items[0] 10).document.fields
$receipt=DocAct 'receipt.floor' $receipt post
foreach($sample in @(@{key='mr.partial';mode='PARTIAL';qty=10},@{key='mr.approved';mode='APPROVED';qty=8},@{key='mr.submitted';mode='SUBMITTED';qty=6},@{key='mr.draft';mode='DRAFT';qty=4},@{key='mr.rejected';mode='REJECTED';qty=3})){
    $mr=Document $sample.key MATERIAL_REQUEST @{departmentId=$hk;sourceWarehouseId=$main.warehouseId} demo_hk1
    $result=Line $sample.key $mr $items[0] $sample.qty demo_hk1;$mr=$result.document.fields;$sourceLine=$result.line.fields
    if($sample.mode-ne 'DRAFT'){$mr=DocAct $sample.key $mr submit demo_hk1}
    if($sample.mode-eq 'REJECTED'){$mr=DocAct $sample.key $mr reject demo_head}
    if($sample.mode-in @('PARTIAL','APPROVED')){$mr=DocAct $sample.key $mr approve demo_head}
    if($sample.mode-eq 'PARTIAL'){
        $partialMr=$mr;$issue=Document 'issue.partial' ISSUE @{sourceWarehouseId=$main.warehouseId;sourceDocumentId=$mr.documentId}
        $issue=(Line 'issue.partial' $issue $items[0] 4 demo_store @{sourceLineId=$sourceLine.lineId}).document.fields
        $issue=DocAct 'issue.partial' $issue post
    }
}
$replenishment=Document 'replenishment' REPLENISHMENT @{destinationWarehouseId=$main.warehouseId;expectedOn=[DateTime]::UtcNow.AddDays(3).ToString('yyyy-MM-dd')}
$replenishment=(Line 'replenishment' $replenishment $items[4] 95).document.fields
$stocktake=Document 'stocktake' STOCKTAKE @{sourceWarehouseId=$main.warehouseId}
$stocktake=(Line 'stocktake' $stocktake $items[0] 96 demo_store @{countedQty=96}).document.fields

# Keep live timers last so a user never has two open work intervals.
$live=Work 'live.clean' CLEANING 'Đang dọn phòng 106' @{roomId=$rooms['sg.6'].roomId}
$live=Act 'live.clean' $live assign demo_manager @{userId=$ids.demo_hk1}
foreach($a in @('accept','start')){$live=Act 'live.clean' $live $a demo_hk1}
$liveTask=Work 'live.task' OPERATIONAL 'Đang kiểm kê xe đẩy tầng 1' @{departmentId=$hk}
$liveTask=Act 'live.task' $liveTask assign demo_manager @{userId=$ids.demo_hk2}
foreach($a in @('accept','start')){$liveTask=Act 'live.task' $liveTask $a demo_hk2}
$liveMaintenance=Work 'live.maintenance' MAINTENANCE 'Đang sửa đường nước phòng 110' @{roomId=$rooms['sg.10'].roomId;departmentId=$eng;parentWorkId=$incident.workId}
$liveMaintenance=Act 'live.maintenance' $liveMaintenance assign demo_manager @{userId=$ids.demo_tech}
foreach($a in @('accept','start')){$liveMaintenance=Act 'live.maintenance' $liveMaintenance $a demo_tech}
