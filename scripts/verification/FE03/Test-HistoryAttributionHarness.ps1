$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HistoryAttribution.Assertions.ps1')
$script:passed=0
function Test-Case([string]$Name,[scriptblock]$Body){& $Body;$script:passed++;Write-Output ('PASS '+$Name)}
function Reject([scriptblock]$Body,[string]$Code){$caught=$false;try{& $Body}catch{if($_.Exception.Message -notlike ($Code+'*')){throw};$caught=$true};if(!$caught){throw ('Expected '+$Code)}}
function Require([bool]$Value){if(!$Value){throw 'Test assertion failed'}}
$sha='717715fa0f9fa51ba467592f1dd01ea06e8ce568'
$manifest=[pscustomobject]@{source_commit=$sha;lease_end_utc='2030-01-01T00:00:00Z';https_base_url='https://localhost:7039';database_guid='test-guid';lease_id='test-lease'}
$contract=[pscustomobject]@{source_commit=$sha;ENV_STATUS='TRIAL_READY';LEASE_EXPIRY='2030-01-01T00:00:00Z';HTTPS_BASE_URL='https://localhost:7039';DATABASE_IDENTITY='test-guid'}
$now=[DateTimeOffset]::Parse('2026-11-10T00:00:00Z')
Test-Case 'correct source and leased identity accepted offline' {Assert-HistoryRuntimeContract $contract $manifest $sha $now}
Test-Case 'old runtime source rejected' {$manifest.source_commit='6a0c9248008203c245c347d9d6831279835ee544';try{Reject {Assert-HistoryRuntimeContract $contract $manifest $sha $now} 'BLOCKED_RUNTIME_VERSION'}finally{$manifest.source_commit=$sha}}
Test-Case 'contract source mismatch rejected' {$contract.source_commit='old';try{Reject {Assert-HistoryRuntimeContract $contract $manifest $sha $now} 'BLOCKED_RUNTIME_VERSION'}finally{$contract.source_commit=$sha}}
Test-Case 'expected source cannot downgrade acceptance' {Reject {Assert-HistoryRuntimeContract $contract $manifest '6a0c9248008203c245c347d9d6831279835ee544' $now} 'BLOCKED_RUNTIME_VERSION'}
Test-Case 'expired lease rejected' {Reject {Assert-HistoryRuntimeContract $contract $manifest $sha ([DateTimeOffset]::Parse('2031-01-01T00:00:00Z'))} 'BLOCKED_ENVIRONMENT'}
Test-Case 'unready contract rejected' {$contract.ENV_STATUS='NOT_READY';try{Reject {Assert-HistoryRuntimeContract $contract $manifest $sha $now} 'BLOCKED_ENVIRONMENT'}finally{$contract.ENV_STATUS='TRIAL_READY'}}
Test-Case 'identity mismatch rejected' {$contract.DATABASE_IDENTITY='other';try{Reject {Assert-HistoryRuntimeContract $contract $manifest $sha $now} 'BLOCKED_ENVIRONMENT'}finally{$contract.DATABASE_IDENTITY='test-guid'}}
Test-Case 'non-HTTPS target rejected' {$manifest.https_base_url='http://localhost:7039';try{Reject {Assert-HistoryRuntimeContract $contract $manifest $sha $now} 'BLOCKED_ENVIRONMENT'}finally{$manifest.https_base_url='https://localhost:7039'}}
$time=[DateTime]::Parse('2026-11-10T00:00:00Z').ToUniversalTime()
$rows=@([pscustomobject]@{history_id=1L;room_id=7L;from_status=[DBNull]::Value;to_status='DIRTY';changed_at=$time;changed_by=8L},
 [pscustomobject]@{history_id=2L;room_id=7L;from_status='DIRTY';to_status='READY';changed_at=$time;changed_by=[DBNull]::Value})
function Page { return ('{"items":[{"id":2,"fromStatusCode":"DIRTY","toStatusCode":"READY","changedAtUtc":"2026-11-10T00:00:00Z","changedByUserId":null},{"id":1,"fromStatusCode":null,"toStatusCode":"DIRTY","changedAtUtc":"2026-11-10T00:00:00Z","changedByUserId":8}],"page":1,"pageSize":20,"totalCount":2,"coverage":"RECORDED_STATUS_EVENTS_ONLY"}'|ConvertFrom-Json) }
Test-Case 'recorded actor/null and same-time ID tie-break verified' {Assert-RecordedHistoryPage (Page) $rows 7}
Test-Case 'missing actor property rejected' {$p=Page;$p.items[0].PSObject.Properties.Remove('changedByUserId');Reject {Assert-RecordedHistoryPage $p $rows 7} 'MISSING_HISTORY_ACTOR'}
foreach($bad in @(0,-1,'8',9007199254740992L,1.5)){
 $invalid=$bad
 Test-Case ('invalid actor rejected '+[string]$bad) {$p=Page;$p.items[1].changedByUserId=$invalid;Reject {Assert-RecordedHistoryPage $p $rows 7} 'INVALID_HISTORY_ACTOR'}
}
Test-Case 'invented actor fallback rejected' {$p=Page;$p.items[0].changedByUserId=8;Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_SQL_ATTRIBUTION_OR_ORDER_MISMATCH'}
Test-Case 'foreign room SQL rows rejected' {Reject {Assert-RecordedHistoryPage (Page) $rows 9} 'HISTORY_SCOPE_MISMATCH'}
Test-Case 'foreign history ID rejected' {$p=Page;$p.items[0].id=99;Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_SQL_ATTRIBUTION_OR_ORDER_MISMATCH'}
Test-Case 'history order mismatch rejected' {$p=Page;$p.items=@($p.items[1],$p.items[0]);Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_SQL_ATTRIBUTION_OR_ORDER_MISMATCH'}
Test-Case 'changed_at mismatch rejected' {$p=Page;$p.items[0].changedAtUtc='2026-11-09T00:00:00Z';Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_SQL_ATTRIBUTION_OR_ORDER_MISMATCH'}
Test-Case 'wrong total rejected' {$p=Page;$p.totalCount=3;Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_PAGE_MISMATCH'}
Test-Case 'wrong page rejected' {$p=Page;$p.page=2;Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_PAGE_MISMATCH'}
Test-Case 'wrong coverage rejected' {$p=Page;$p.coverage='COMPLETE';Reject {Assert-RecordedHistoryPage $p $rows 7} 'HISTORY_PAGE_MISMATCH'}
Test-Case 'empty next page accepted without false null-actor coverage' {$p=Page;$p.page=2;$p.items=@();Assert-RecordedHistoryPage $p $rows 7 2}
Test-Case 'complete fixture coverage accepted' {Assert-HistoryFixtureCoverage $rows}
Test-Case 'missing null actor blocks instead of inventing fixture' {Reject {Assert-HistoryFixtureCoverage @($rows[0])} 'BLOCKED_FIXTURE_COVERAGE'}
Test-Case 'missing recorded actor blocks' {Reject {Assert-HistoryFixtureCoverage @($rows[1])} 'BLOCKED_FIXTURE_COVERAGE'}
Test-Case 'single-event rooms cannot prove live ordering' {
 $a=[pscustomobject]@{room_id=7;changed_by=8};$b=[pscustomobject]@{room_id=9;changed_by=[DBNull]::Value}
 Reject {Assert-HistoryFixtureCoverage @($a,$b)} 'BLOCKED_FIXTURE_COVERAGE'
}
Write-Output ('HISTORY_HARNESS_OFFLINE_PASS: '+$script:passed+' cases; no SQL/HTTP/credentials accessed')
