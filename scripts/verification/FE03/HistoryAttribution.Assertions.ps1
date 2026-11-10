# Pure acceptance assertions. No SQL, HTTP, credentials or fixture writes.
function Assert-HistoryRuntimeContract($Contract, $Manifest, [string]$ExpectedSha, [DateTimeOffset]$Now = [DateTimeOffset]::UtcNow) {
    if ($ExpectedSha -cne '717715fa0f9fa51ba467592f1dd01ea06e8ce568' -or
        $Contract.source_commit -cne $ExpectedSha -or $Manifest.source_commit -cne $ExpectedSha) {
        throw 'BLOCKED_RUNTIME_VERSION: attribution application717715 handoff required.'
    }
    if ($Contract.ENV_STATUS -cne 'TRIAL_READY') { throw 'BLOCKED_ENVIRONMENT: trial not ready.' }
    if ([DateTimeOffset]::Parse($Manifest.lease_end_utc) -le $Now -or
        [DateTimeOffset]::Parse($Contract.LEASE_EXPIRY) -ne [DateTimeOffset]::Parse($Manifest.lease_end_utc)) {
        throw 'BLOCKED_ENVIRONMENT: expired or inconsistent lease.'
    }
    $uri = [Uri]$Manifest.https_base_url
    if ($uri.Scheme -cne 'https' -or !$uri.IsLoopback -or $uri.UserInfo -or $uri.Query -or $uri.AbsolutePath -ne '/' -or
        $Contract.HTTPS_BASE_URL.TrimEnd('/') -cne $Manifest.https_base_url.TrimEnd('/') -or
        $Contract.DATABASE_IDENTITY -cne $Manifest.database_guid -or !$Manifest.database_guid -or !$Manifest.lease_id) {
        throw 'BLOCKED_ENVIRONMENT: endpoint/database identity mismatch.'
    }
}
function Assert-HistoryActor($Value) {
    if ($null -eq $Value) { return }
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -le 0 -or $Value -gt 9007199254740991L) {
        throw 'INVALID_HISTORY_ACTOR'
    }
}
function Assert-RecordedHistoryPage($Page, [object[]]$SqlRows, [long]$RoomId, [int]$Number = 1, [int]$Size = 20) {
    if (!$Page -or $Page.page -ne $Number -or $Page.pageSize -ne $Size -or
        $Page.totalCount -ne $SqlRows.Count -or $Page.coverage -cne 'RECORDED_STATUS_EVENTS_ONLY') { throw 'HISTORY_PAGE_MISMATCH' }
    if (@($SqlRows | Where-Object { $_.room_id -ne $RoomId }).Count -gt 0) { throw 'HISTORY_SCOPE_MISMATCH' }
    $sorted = @($SqlRows | Sort-Object @{Expression={ [DateTime]$_.changed_at };Descending=$true}, @{Expression={ [long]$_.history_id };Descending=$true})
    $expected = @($sorted | Select-Object -Skip (($Number - 1) * $Size) -First $Size)
    $items = @($Page.items)
    if ($null -eq $Page.PSObject.Properties['items'] -or $null -eq $Page.items -or $items.Count -ne $expected.Count) { throw 'HISTORY_ITEMS_MISMATCH' }
    for ($i = 0; $i -lt $items.Count; $i++) {
        $item = $items[$i]; $row = $expected[$i]
        if (!$item -or $null -eq $item.PSObject.Properties['changedByUserId']) { throw 'MISSING_HISTORY_ACTOR' }
        Assert-HistoryActor $item.changedByUserId
        $actor = if ($row.changed_by -is [DBNull]) { $null } else { $row.changed_by }
        $from = if ($row.from_status -is [DBNull]) { $null } else { $row.from_status }
        $at = [DateTimeOffset]::MinValue
        if ($item.id -ne $row.history_id -or $item.fromStatusCode -cne $from -or $item.toStatusCode -cne $row.to_status -or
            $item.changedByUserId -cne $actor -or $item.changedAtUtc -notmatch 'Z$' -or
            ![DateTimeOffset]::TryParse([string]$item.changedAtUtc, [ref]$at) -or
            $at.UtcDateTime.Ticks -ne ([DateTime]::SpecifyKind([DateTime]$row.changed_at, [DateTimeKind]::Utc)).Ticks) {
            throw 'HISTORY_SQL_ATTRIBUTION_OR_ORDER_MISMATCH'
        }
    }
}
function Assert-HistoryFixtureCoverage([object[]]$Rows) {
    $recorded=@($Rows|Where-Object {$null -ne $_.changed_by -and $_.changed_by -isnot [DBNull]})
    $missing=@($Rows|Where-Object {$null -eq $_.changed_by -or $_.changed_by -is [DBNull]})
    $multi=@($Rows|Group-Object room_id|Where-Object Count -ge 2)
    if ($recorded.Count -eq 0 -or $missing.Count -eq 0 -or $multi.Count -eq 0) {
        throw 'BLOCKED_FIXTURE_COVERAGE: approved recorded/null actors and multiple same-room history rows required.'
    }
    foreach($row in $recorded){Assert-HistoryActor $row.changed_by}
}
