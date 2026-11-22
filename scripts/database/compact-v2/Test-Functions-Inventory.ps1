Feature 'FE-08 receipt line validation unit conversion and posting immutability' {
    $s=StockFixture 'F-RECEIPT' 20
    $pack=(Call POST '/api/v2/catalog/references' @{fields=@{kind='UOM';code='FUNCTIONAL_PACK';name='Pack of five';baseUnitId=$unit.referenceId;conversionFactor=5}} $admin).fields
    $doc=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId}
    $null=AddLine $doc $s.item.itemId 1 $null $admin # create the initial editable line
    $detail=Call GET "/api/v2/documents/$($doc.documentId)" $null $admin;$doc=$detail.document.fields;$line=$detail.lines[0].fields
    $null=Call PUT "/api/v2/documents/$($doc.documentId)/lines/$($line.lineId)" @{rowVersion=$doc.rowVersion;itemId=$s.item.itemId;unitId=$unit.referenceId;quantity=-1} $admin 400
    $saved=Call PUT "/api/v2/documents/$($doc.documentId)/lines/$($line.lineId)" @{rowVersion=$doc.rowVersion;itemId=$s.item.itemId;unitId=$pack.referenceId;quantity=2;unitCost=100} $admin
    $doc=(DocAction $saved.document.fields post $admin).fields
    Check 'FE-08 converted receipt posts ten base units for two packs' ((Balance $s.item.itemId)-eq 30 -and $saved.line.fields.conversionFactor-eq 5)
    $null=Call PUT "/api/v2/documents/$($doc.documentId)/lines/$($line.lineId)" @{rowVersion=$doc.rowVersion;itemId=$s.item.itemId;unitId=$unit.referenceId;quantity=1} $admin 409
    $null=DocAction $doc cancel $admin 409
    Check 'FE-08 posted document cannot edit lines or cancel' $true
    $draft=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId}
    $draft=(AddLine $draft $s.item.itemId 2).document.fields;$draft=(DocAction $draft cancel $admin).fields
    Check 'FE-08 unposted draft can cancel without ledger movement' ($draft.status-eq 'CANCELLED' -and (Balance $s.item.itemId)-eq 30)
}
Feature 'FE-09 request information partial approval overissue and remainder close' {
    $s=StockFixture 'F-MATERIAL' 20
    $request=NewDocument MATERIAL_REQUEST @{departmentId=$f.departmentId;sourceWarehouseId=$warehouse.warehouseId} $worker
    $line=AddLine $request $s.item.itemId 10 $null $worker;$request=$line.document.fields
    $request=(DocAction $request submit $worker).fields
    $request=(DocAction $request request-information $admin).fields
    Check 'FE-09 approver can request additional information' ($request.status-eq 'NEEDS_INFORMATION')
    $request=(DocAction $request submit $worker).fields
    $null=Call POST "/api/v2/documents/$($request.documentId)/actions/approve" @{rowVersion=$request.rowVersion;operationKey=[Guid]::NewGuid().ToString();approvedLines=@(@{lineId=$line.line.fields.lineId;quantity=11})} $admin 400
    $request=(Call POST "/api/v2/documents/$($request.documentId)/actions/approve" @{rowVersion=$request.rowVersion;operationKey=[Guid]::NewGuid().ToString();approvedLines=@(@{lineId=$line.line.fields.lineId;quantity=6})} $admin).fields
    Check 'FE-09 approval quantity cannot exceed request and may be reduced' ($request.status-eq 'APPROVED')
    $bad=NewDocument ISSUE @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId} $worker
    $bad=(AddLine $bad $s.item.itemId 7 $line.line.fields.lineId $worker).document.fields
    $null=DocAction $bad post $worker 409
    $badDetail=Call GET "/api/v2/documents/$($bad.documentId)" $null $worker
    Check 'FE-09 overissue rolls back posting header and stock' ($badDetail.document.fields.status-eq 'DRAFT' -and (Balance $s.item.itemId)-eq 20)
    $good=NewDocument ISSUE @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId} $worker
    $good=(AddLine $good $s.item.itemId 4 $line.line.fields.lineId $worker).document.fields;$good=(DocAction $good post $worker).fields
    $detail=Call GET "/api/v2/documents/$($request.documentId)" $null $admin
    Check 'FE-09 partial issue computes approved remainder correctly' ($detail.fulfillment[0].fields.missingBaseQty-eq 2 -and (Balance $s.item.itemId)-eq 16)
    $request=(DocAction $detail.document.fields close $admin).fields
    $extra=NewDocument ISSUE @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId} $worker
    $extra=(AddLine $extra $s.item.itemId 1 $line.line.fields.lineId $worker).document.fields
    $null=DocAction $extra post $worker 409
    Check 'FE-09 closing remainder prevents additional issue' ($request.status-eq 'REMAINDER_CLOSED')
}
Feature 'FE-08 material return requires independent approval and respects source quantity' {
    $s=StockFixture 'F-RETURN' 20
    $request=NewDocument MATERIAL_REQUEST @{departmentId=$f.departmentId;sourceWarehouseId=$warehouse.warehouseId} $worker
    $l=AddLine $request $s.item.itemId 5 $null $worker;$request=(DocAction $l.document.fields submit $worker).fields;$request=(DocAction $request approve $admin).fields
    $issued=NewDocument ISSUE @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId} $worker
    $il=AddLine $issued $s.item.itemId 5 $l.line.fields.lineId $worker;$issued=(DocAction $il.document.fields post $worker).fields
    $ret=NewDocument RETURN @{destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$issued.documentId} $worker
    $ret=(AddLine $ret $s.item.itemId 2 $il.line.fields.lineId $worker).document.fields
    $null=DocAction $ret post $worker 409
    $ret=(DocAction $ret approve $admin).fields;$ret=(DocAction $ret post $worker).fields
    Check 'FE-08 approved return restores only returned quantity' ((Balance $s.item.itemId)-eq 17)
    $excess=NewDocument RETURN @{destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$issued.documentId} $worker
    $excess=(AddLine $excess $s.item.itemId 4 $il.line.fields.lineId $worker).document.fields;$excess=(DocAction $excess approve $admin).fields
    $reply=DocAction $excess post $worker 409
    Check 'FE-08 cumulative return cannot exceed original issue' ($reply.title-eq 'SOURCE_QUANTITY_EXCEEDED' -and (Balance $s.item.itemId)-eq 17)
}
Feature 'FE-08 transfer request and warehouse transfer receipts' {
    $s=StockFixture 'F-TRANSFER' 20
    $destination=(Call POST "/api/v2/catalog/warehouses?branchId=$($f.branchId)" @{fields=@{code='FUNCTIONAL-DEST';name='Transfer destination';departmentId=$f.departmentId}} $admin).fields
    $request=NewDocument TRANSFER_REQUEST @{sourceWarehouseId=$warehouse.warehouseId;destinationWarehouseId=$destination.warehouseId}
    $l=AddLine $request $s.item.itemId 6;$request=(DocAction $l.document.fields submit $admin).fields;$request=(DocAction $request approve $worker).fields
    Check 'FE-08 transfer request supports submit and independent approval' ($request.status-eq 'APPROVED')
    $transfer=NewDocument TRANSFER @{sourceWarehouseId=$warehouse.warehouseId;destinationWarehouseId=$destination.warehouseId;sourceDocumentId=$request.documentId}
    $tl=AddLine $transfer $s.item.itemId 6 $l.line.fields.lineId;$transfer=(DocAction $tl.document.fields post $admin).fields
    Check 'FE-08 transfer dispatch decrements source and records in transit' ($transfer.status-eq 'IN_TRANSIT' -and (Balance $s.item.itemId)-eq 14 -and (Balance $s.item.itemId $destination.warehouseId)-eq 0)
    $postedVersion=$transfer.rowVersion;$receipts=@()
    foreach($qty in @(2,4)){
        $received=NewDocument TRANSFER_RECEIPT @{destinationWarehouseId=$destination.warehouseId;sourceDocumentId=$transfer.documentId}
        $receivedLine=AddLine $received $s.item.itemId $qty $tl.line.fields.lineId
        $received=(DocAction $receivedLine.document.fields post $admin).fields
        $receipts+=@{document=$received;line=$receivedLine.line.fields;quantity=$qty}
        $expected='PARTIALLY_RECEIVED';if($qty-eq 4){$expected='RECEIVED'}
        $detail=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
        $listed=((Call GET "/api/v2/documents?branchId=$($f.branchId)&kind=TRANSFER" $null $admin).items|Where-Object {$_.fields.documentId-eq $transfer.documentId}).fields
        $reported=((Call GET "/api/v2/insights/reports/documents?branchId=$($f.branchId)" $null $admin)|Where-Object {$_.fields.documentId-eq $transfer.documentId}).fields
        Check "FE-08 transfer detail list report agree on $expected" ($detail.status-eq $expected -and $listed.status-eq $expected -and $reported.status-eq $expected -and $detail.recordedStatus-eq 'IN_TRANSIT')
    }
    Check 'FE-08 receiving transferred stock credits destination exactly' ((Balance $s.item.itemId $destination.warehouseId)-eq 6)
    $extra=NewDocument TRANSFER_RECEIPT @{destinationWarehouseId=$destination.warehouseId;sourceDocumentId=$transfer.documentId}
    $extra=(AddLine $extra $s.item.itemId 1 $tl.line.fields.lineId).document.fields
    $reply=DocAction $extra post $admin 409
    Check 'FE-08 overreceipt of transfer is rejected' ($reply.title-eq 'SOURCE_QUANTITY_EXCEEDED')
    $now=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
    Check 'FE-08 fully received transfer leaves in-transit state' ($now.status-eq 'RECEIVED')
    $stored=(Sql "SELECT status,row_version FROM inventory_documents WHERE document_id=$($transfer.documentId)").Rows[0]
    Check 'FE-08 derived status preserves posted header and rowversion' ($stored.status-eq 'IN_TRANSIT' -and [Convert]::ToBase64String($stored.row_version)-eq $postedVersion -and $now.rowVersion-eq $postedVersion)
    foreach($part in @($receipts[1],$receipts[0])){
        $reverse=NewDocument REVERSAL @{sourceWarehouseId=$destination.warehouseId;sourceDocumentId=$part.document.documentId}
        $reverse=(AddLine $reverse $s.item.itemId $part.quantity $part.line.lineId).document.fields
        $reverse=(DocAction $reverse approve $worker).fields;$reverse=(DocAction $reverse post $admin).fields
        $expected='PARTIALLY_RECEIVED';if($part.quantity-eq 2){$expected='IN_TRANSIT'}
        $now=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
        Check "FE-08 receipt reversal recalculates transfer to $expected" ($now.status-eq $expected)
    }
    $replacement=NewDocument TRANSFER_RECEIPT @{destinationWarehouseId=$destination.warehouseId;sourceDocumentId=$transfer.documentId}
    $replacement=(AddLine $replacement $s.item.itemId 6 $tl.line.fields.lineId).document.fields;$replacement=(DocAction $replacement post $admin).fields
    $now=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
    Check 'FE-08 replacement receipt after reversals restores received status and net stock' ($now.status-eq 'RECEIVED' -and (Balance $s.item.itemId $destination.warehouseId)-eq 6)
}
Feature 'FE-08 multiline transfer requires every line and excludes draft receipts' {
    $first=StockFixture 'F-MULTI-A' 20;$second=StockFixture 'F-MULTI-B' 20
    $destination=(Call POST "/api/v2/catalog/warehouses?branchId=$($f.branchId)" @{fields=@{code='FUNCTIONAL-MULTI-DEST';name='Multiline transfer destination';departmentId=$f.departmentId}} $admin).fields
    $transfer=NewDocument TRANSFER @{sourceWarehouseId=$warehouse.warehouseId;destinationWarehouseId=$destination.warehouseId}
    $a=AddLine $transfer $first.item.itemId 4;$b=AddLine $a.document.fields $second.item.itemId 6
    $draft=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
    Check 'FE-08 unposted transfer retains draft status' ($draft.status-eq 'DRAFT')
    $transfer=(DocAction $b.document.fields post $admin).fields
    $receipt=NewDocument TRANSFER_RECEIPT @{destinationWarehouseId=$destination.warehouseId;sourceDocumentId=$transfer.documentId}
    $receipt=(AddLine $receipt $first.item.itemId 4 $a.line.fields.lineId).document.fields;$receipt=(DocAction $receipt post $admin).fields
    $receipt=NewDocument TRANSFER_RECEIPT @{destinationWarehouseId=$destination.warehouseId;sourceDocumentId=$transfer.documentId}
    $receipt=(AddLine $receipt $second.item.itemId 6 $b.line.fields.lineId).document.fields
    $partial=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
    Check 'FE-08 fully receiving one line and drafting another is still partial' ($partial.status-eq 'PARTIALLY_RECEIVED')
    $receipt=(DocAction $receipt post $admin).fields
    $full=(Call GET "/api/v2/documents/$($transfer.documentId)" $null $admin).document.fields
    Check 'FE-08 all transfer lines must be posted as received' ($full.status-eq 'RECEIVED')
}
Feature 'FE-08 stocktake adjustment positive and negative' {
    $s=StockFixture 'F-COUNT' 20
    foreach($target in @(18,21)){
        $before=Balance $s.item.itemId
        $count=NewDocument STOCKTAKE @{sourceWarehouseId=$warehouse.warehouseId}
        $cl=AddLine $count $s.item.itemId $before $null $admin $target;$count=(DocAction $cl.document.fields count $admin).fields
        $adjust=NewDocument ADJUSTMENT @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$count.documentId}
        $adjust=(AddLine $adjust $s.item.itemId ([Math]::Abs($target-$before)) $cl.line.fields.lineId).document.fields
        $null=DocAction $adjust post $admin 409
        $adjust=(DocAction $adjust approve $worker).fields;$adjust=(DocAction $adjust post $admin).fields
        Check "FE-08 counted stock adjustment reconciles quantity to $target" ((Balance $s.item.itemId)-eq $target)
    }
}
Feature 'FE-08 stale stocktake requires recount' {
    $s=StockFixture 'F-STALE-COUNT' 20
    $count=NewDocument STOCKTAKE @{sourceWarehouseId=$warehouse.warehouseId}
    $cl=AddLine $count $s.item.itemId 20 $null $admin 18;$count=(DocAction $cl.document.fields count $admin).fields
    $newReceipt=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId};$newReceipt=(AddLine $newReceipt $s.item.itemId 5).document.fields;$null=DocAction $newReceipt post $admin
    $adjust=NewDocument ADJUSTMENT @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$count.documentId};$adjust=(AddLine $adjust $s.item.itemId 2 $cl.line.fields.lineId).document.fields;$adjust=(DocAction $adjust approve $worker).fields
    $reply=DocAction $adjust post $admin 409
    Check 'FE-08 inventory movement after count requires recount before adjustment' ($reply.title-eq 'STOCK_CHANGED_RECOUNT_REQUIRED' -and (Balance $s.item.itemId)-eq 25)
}
Feature 'FE-08 exact reversal and duplicate protection' {
    $s=StockFixture 'F-REVERSE' 20
    $reverse=NewDocument REVERSAL @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$s.receipt.documentId}
    $reverse=(AddLine $reverse $s.item.itemId 20 $s.line.lineId).document.fields
    $reverse=(DocAction $reverse approve $worker).fields;$reverse=(DocAction $reverse post $admin).fields
    Check 'FE-08 exact reversal offsets original ledger movement' ((Balance $s.item.itemId)-eq 0)
    $duplicate=NewDocument REVERSAL @{sourceWarehouseId=$warehouse.warehouseId;sourceDocumentId=$s.receipt.documentId}
    $duplicate=(AddLine $duplicate $s.item.itemId 20 $s.line.lineId).document.fields;$duplicate=(DocAction $duplicate approve $worker).fields
    $null=DocAction $duplicate post $admin 409
    Check 'FE-08 original movement cannot be reversed twice' ((Balance $s.item.itemId)-eq 0)
}
Feature 'FE-10 replenishment partial and full receipt closure' {
    $s=StockFixture 'F-REPLENISH' 1
    $request=NewDocument REPLENISHMENT @{destinationWarehouseId=$warehouse.warehouseId}
    $rl=AddLine $request $s.item.itemId 10;$request=$rl.document.fields
    foreach($quantity in @(4,6)){
        $receipt=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId}
        $receipt=(AddLine $receipt $s.item.itemId $quantity $rl.line.fields.lineId).document.fields;$receipt=(DocAction $receipt post $admin).fields
        $request=(Call GET "/api/v2/documents/$($request.documentId)" $null $admin).document.fields
        $expected='PARTIALLY_RECEIVED';if($quantity-eq 6){$expected='FULLY_RECEIVED'}
        Check "FE-10 receipt advances replenishment to $expected" ($request.status-eq $expected)
    }
    $extra=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId}
    $extra=(AddLine $extra $s.item.itemId 1 $rl.line.fields.lineId).document.fields
    $reply=DocAction $extra post $admin 409
    Check 'FE-10 replenishment cannot be overreceived' ($reply.title-eq 'SOURCE_QUANTITY_EXCEEDED' -and (Balance $s.item.itemId)-eq 11)
    $request=(DocAction $request close $admin).fields
    Check 'FE-10 fulfilled replenishment can be closed' ($request.status-eq 'CLOSED' -and $null-ne $request.closedAt)
}
Feature 'FE-10 closed replenishment rejects subsequent receipt' {
    $s=StockFixture 'F-CLOSED-REPLENISH' 1
    $request=NewDocument REPLENISHMENT @{destinationWarehouseId=$warehouse.warehouseId}
    $rl=AddLine $request $s.item.itemId 10;$request=(DocAction $rl.document.fields close $admin).fields
    $receipt=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId}
    $receipt=(AddLine $receipt $s.item.itemId 1 $rl.line.fields.lineId).document.fields
    $reply=DocAction $receipt post $admin 409
    $source=(Call GET "/api/v2/documents/$($request.documentId)" $null $admin).document.fields
    $rejected=(Call GET "/api/v2/documents/$($receipt.documentId)" $null $admin).document.fields
    Check 'FE-10 closing unreceived remainder prevents later receipt' ($reply.title-eq 'REPLENISHMENT_CLOSED' -and (Balance $s.item.itemId)-eq 1)
    Check 'FE-10 rejected posting preserves closed source and draft receipt' ($source.status-eq 'CLOSED' -and $source.rowVersion-eq $request.rowVersion -and $rejected.status-eq 'DRAFT' -and $null-eq $rejected.postedAt -and $rejected.rowVersion-eq $receipt.rowVersion)
    $writes=(Sql "SELECT (SELECT COUNT(*) FROM stock_movements m JOIN inventory_document_lines l ON l.line_id=m.line_id WHERE l.document_id=$($receipt.documentId)) ledger,(SELECT COUNT(*) FROM document_events WHERE document_id=$($receipt.documentId) AND event_type='POST') posted_events").Rows[0]
    Check 'FE-10 rejected closed-source posting leaves no ledger or post event' ($writes.ledger-eq 0 -and $writes.posted_events-eq 0)
}
Feature 'FE-10 draft predating closure and historical inconsistent source stay blocked' {
    $s=StockFixture 'F-PRECLOSE-REPLENISH' 1
    $request=NewDocument REPLENISHMENT @{destinationWarehouseId=$warehouse.warehouseId}
    $rl=AddLine $request $s.item.itemId 10;$request=$rl.document.fields
    $receipt=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId;sourceDocumentId=$request.documentId}
    $receipt=(AddLine $receipt $s.item.itemId 2 $rl.line.fields.lineId).document.fields
    $request=(DocAction $request close $admin).fields
    $reply=DocAction $receipt post $admin 409
    Check 'FE-10 posting rechecks source closed after receipt draft was saved' ($reply.title-eq 'REPLENISHMENT_CLOSED' -and (Balance $s.item.itemId)-eq 1)
    # Isolated fixture only: reproduce an inconsistent header left by the previously confirmed defect.
    $null=Sql "UPDATE inventory_documents SET status='PARTIALLY_RECEIVED' WHERE document_id=$($request.documentId) AND closed_at IS NOT NULL;"
    $reply=DocAction $receipt post $admin 409
    Check 'FE-10 closedAt prevents further receipts even with historical inconsistent status' ($reply.title-eq 'REPLENISHMENT_CLOSED' -and (Balance $s.item.itemId)-eq 1)
}
