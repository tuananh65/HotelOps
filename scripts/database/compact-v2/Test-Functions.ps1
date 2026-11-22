# Extended API cases run only inside Test-Backend.ps1's fresh isolated database.
if($database -notmatch '^HotelOpsCompactApiTest[a-f0-9]{12}$'){throw 'Isolated API fixture required.'}
function Feature($caseId,[scriptblock]$caseBody){
    Write-Host "Testing: $caseId"
    try{& $caseBody}catch{
        $results.Add(@{name=$caseId;passed=$false;error=$_.Exception.Message})
        Write-Host "FAIL: $caseId : $($_.Exception.Message)"
    }finally{
        # A failed case must not leave a running timer that contaminates another feature.
        foreach($open in (Sql 'SELECT DISTINCT work_id FROM work_intervals WHERE closed_at IS NULL').Rows){
            try{$current=(Call GET "/api/v2/work/$($open.work_id)" $null $admin).work.fields;$null=WorkAction $current cancel $admin}
            catch{$results.Add(@{name="$caseId cleanup";passed=$false;error=$_.Exception.Message})}
        }
    }
}
function NewWork($kind='OPERATIONAL',$extra=@{}){
    $body=@{branchId=$f.branchId;kind=$kind;title='Functional '+[Guid]::NewGuid().ToString('N').Substring(0,8);departmentId=$f.departmentId}
    foreach($key in $extra.Keys){$body[$key]=$extra[$key]}
    return (Call POST '/api/v2/work' $body $admin).fields
}
function NewDocument($kind,$extra=@{},$token=$admin){
    $body=@{branchId=$f.branchId;kind=$kind;reason='Functional verification fixture'}
    foreach($key in $extra.Keys){$body[$key]=$extra[$key]}
    return (Call POST '/api/v2/documents' $body $token).fields
}
function AddLine($doc,$itemId,$quantity,$sourceLineId=$null,$token=$admin,$countedQty=$null){
    $body=@{rowVersion=$doc.rowVersion;itemId=$itemId;unitId=$unit.referenceId;quantity=$quantity}
    if($null-ne $sourceLineId){$body.sourceLineId=$sourceLineId}
    if($null-ne $countedQty){$body.countedQty=$countedQty}
    return Call POST "/api/v2/documents/$($doc.documentId)/lines" $body $token
}
function StockFixture($code,$quantity=20){
    $i=(Call POST '/api/v2/catalog/items' @{fields=@{code=$code;name=$code;baseUnitId=$unit.referenceId}} $admin).fields
    $d=NewDocument RECEIPT @{destinationWarehouseId=$warehouse.warehouseId}
    $l=AddLine $d $i.itemId $quantity;$d=(DocAction $l.document.fields post $admin).fields
    return @{item=$i;receipt=$d;line=$l.line.fields}
}
function Balance($itemId,$warehouseId=$warehouse.warehouseId){
    return [decimal](Sql "SELECT COALESCE(SUM(signed_base_qty),0) qty FROM stock_movements WHERE item_id=$itemId AND warehouse_id=$warehouseId").Rows[0].qty
}
. (Join-Path $PSScriptRoot 'Test-Functions-Core.ps1')
. (Join-Path $PSScriptRoot 'Test-Functions-Work.ps1')
. (Join-Path $PSScriptRoot 'Test-Functions-Inventory.ps1')
