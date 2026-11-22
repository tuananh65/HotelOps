# HTTP checks on the fixture. Inbox delivery stamps are recorded; no credentials in output.
$checks=New-Object 'System.Collections.Generic.List[object]'
function Check($name,$condition){if(-not $condition){throw "Verification failed: $name"};$checks.Add(@{name=$name;passed=$true});Write-Host "PASS: $name"}
Check 'all eleven demo identities authenticate' ($tokens.Count-eq 11)
$sgRooms=@(Call GET "/api/v2/rooms?branchId=$sg" $null demo_reception)
$dnRooms=@(Call GET "/api/v2/rooms?branchId=$dn" $null demo_dn_manager)
Check 'two branches contain 12 and 4 rooms' ($sgRooms.Count-eq 12 -and $dnRooms.Count-eq 4)
Check 'room 101 has independent inspection and is ready' (($sgRooms|Where-Object code -eq '101').readiness-eq 'READY')
Check 'room 102 is occupied' (($sgRooms|Where-Object code -eq '102').operationalStatus-eq 'OCCUPIED')
Check 'checkout invalidated room 103 cleaning cycle' (($sgRooms|Where-Object code -eq '103').turnoverNo-eq 1)
Check 'failed inspection keeps room 105 unavailable' (($sgRooms|Where-Object code -eq '105').readiness-ne 'READY')
Check 'active incident blocks room 110' (($sgRooms|Where-Object code -eq '110').readiness-eq 'BLOCKED')
$null=Call GET "/api/v2/rooms?branchId=$dn" $null demo_hk1 403
Check 'Saigon housekeeper cannot read Danang rooms' $true
$null=Call GET "/api/v2/rooms?branchId=$sg" $null demo_dn_hk 403
Check 'Danang housekeeper cannot read Saigon rooms' $true
$null=Call POST '/api/v2/work' @{branchId=$sg;kind='OPERATIONAL';title='Unauthorized read-only mutation'} demo_auditor 403
Check 'auditor cannot create work' $true
$null=Call GET "/api/v2/rooms?branchId=$sg" $null demo_admin 403
Check 'system administrator has no implicit operational grant' $true
$dashboard=Call GET "/api/v2/insights/dashboard?branchId=$sg" $null demo_auditor
Check 'auditor can read populated dashboard' (@($dashboard.rooms).Count-gt 0 -and @($dashboard.work).Count-gt 0)
$stock=Call GET "/api/v2/insights/reports/stock?branchId=$sg" $null demo_auditor
$soap=$stock|Where-Object {$_.fields.warehouseId-eq $main.warehouseId -and $_.fields.itemId-eq $items[0].itemId}
Check 'main warehouse soap balance is 100 minus 4 = 96' ($soap.fields.baseQty-eq 96)
$fulfillment=Call GET "/api/v2/documents/$($partialMr.documentId)" $null demo_head
Check 'partial request retains six unissued units' ($fulfillment.fulfillment[0].fields.missingBaseQty-eq 6)
$detail=Call GET "/api/v2/work/$($live.workId)" $null demo_hk1
Check 'housekeeper can continue live cleaning task' ($detail.work.fields.status-eq 'IN_PROGRESS')
$inbox=Call GET '/api/v2/notifications' $null demo_hk1
Check 'housekeeper has generated workflow notifications' (@($inbox.items).Count-gt 0)
$duration=Call GET "/api/v2/insights/time?branchId=$sg&userId=$($ids.demo_hk1)" $null demo_manager
Check 'time tracking includes open and closed intervals' (@($duration|Where-Object {$null-eq $_.fields.closedAt}).Count-eq 1 -and @($duration|Where-Object {$null-ne $_.fields.closedAt}).Count-gt 0)
@{database=$Database;verifiedAt=[DateTime]::UtcNow.ToString('o');passed=$checks.Count;checks=@($checks.ToArray())}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $folder 'api-verification.json') -Encoding UTF8
