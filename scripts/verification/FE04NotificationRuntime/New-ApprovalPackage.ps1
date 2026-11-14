#requires -Version 5.1
# Produces review artifacts only. Never connects, grants, signs an approval or invokes an installer.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$permissions=New-Object Collections.Generic.List[object]
function Right([string]$Object,[string]$Operation,[string]$Column,[bool]$Allowed,[string]$Phase){
 $permissions.Add([pscustomobject]@{Object=$Object;Permission=$Operation;Column=$(if($Column){$Column}else{$null});Allowed=$Allowed;phase=$Phase;status='PENDING'})
}
foreach($table in @('users','user_sessions','user_branch_access','user_roles','roles','role_permissions','permissions',
 'branches','departments','rooms','floors','areas','room_types','shift_assignments','work_shifts','shift_templates',
 'cleaning_tasks','cleaning_assignments','room_cleaning_cycle_state',
 'audit_logs','notification_operation_reservations','notification_publications','notification_delivery_intents')){
 Right ('dbo.'+$table) 'SELECT' '' $true 'NATIVE_UC029_SOURCE_READ'
}
foreach($module in @('fs013_assign_with_evidence','fs009_verify_assignment_guard')){
 Right ('dbo.'+$module) 'EXECUTE' '' $true 'NATIVE_UC029'
 Right ('dbo.'+$module) 'VIEW DEFINITION' '' $true 'PREFLIGHT_METADATA'
}
foreach($table in @('notification_operation_reservations','notification_publications','notification_delivery_intents','cleaning_assignments')){
 foreach($operation in @('INSERT','DELETE')){Right ('dbo.'+$table) $operation '' $false 'RETAIN_EXPLICIT_DENY'}
 if($table -ne 'notification_delivery_intents'){Right ('dbo.'+$table) 'UPDATE' '' $false 'RETAIN_EXPLICIT_DENY'}
 Right ('dbo.'+$table) 'VIEW DEFINITION' '' $true 'PREFLIGHT_METADATA'
}
foreach($operation in @('UPDATE','DELETE')){Right 'dbo.audit_logs' $operation '' $false 'RETAIN_EXPLICIT_DENY'}
foreach($table in @('notifications','notification_recipients')){
 Right ('dbo.'+$table) 'SELECT' '' $true 'CONTAINED_DELIVERY_INBOX_REQUIRES_OWNER_ACCEPTANCE'
 Right ('dbo.'+$table) 'INSERT' '' $true 'CONTAINED_DELIVERY_INBOX_REQUIRES_OWNER_ACCEPTANCE'
}
Right 'dbo.notification_recipients' 'UPDATE' 'read_at' $true 'INBOX_MARK_READ_REQUIRES_OWNER_ACCEPTANCE'
foreach($column in @('status','attempt_count','next_attempt_at','claim_id','claimed_until','failure_code','inbox_notification_id')){
 Right 'dbo.notification_delivery_intents' 'UPDATE' $column $true 'CONTAINED_DELIVERY_INBOX_REQUIRES_OWNER_ACCEPTANCE'
}
[pscustomobject]@{
 version='FE04_NATIVE_PERMISSION_PROPOSAL_V1';status='PENDING_NOT_EXECUTABLE';login='HotelOps_DEV2_FullStack_Runtime';
 databaseUser='HotelOps_DEV2_FullStack_Runtime';authentication='SQL_LOGIN';roles=@();
 scope='Native UC029 plus separately approved delivery/inbox. Not an installer or accepted all-15-case rights profile.';
 excluded='No schema-wide SELECT, broad server metadata grant, direct EF graph/assignment writes, ALTER/CONTROL/IMPERSONATE or DENY removal.';
 metadataVisibility='PENDING: restricted GUID/login/TLS queries must work with scoped observer approved by schema/security owner; never elevate to pass.';
 entries=$permissions.ToArray()
}|ConvertTo-Json -Depth 7|Set-Content (Join-Path $PSScriptRoot 'PERMISSION_CONTRACT.json') -Encoding UTF8
$paths=@(
 'src/HotelOps.Api/Services/Housekeeping/Acceptance/Fe04AcceptanceTransaction.cs',
 'src/HotelOps.Api/Services/Housekeeping/Acceptance/Fe04CleaningSourceAuthority.cs',
 'src/HotelOps.Api/Services/Housekeeping/Acceptance/Fe04NotificationApproval.cs',
 'src/HotelOps.Api/Services/Housekeeping/Acceptance/Fe04NotificationRuntimeAdapter.cs',
 'tests/HotelOps.Dev1.RegressionTests/Fe04NotificationRuntimeTests.cs',
 'scripts/verification/FE04NotificationRuntime/FE04NotificationRuntime.csproj',
 'scripts/verification/FE04NotificationRuntime/Program.cs',
 'scripts/verification/FE04NotificationRuntime/Invoke-Preflight.ps1',
 'scripts/verification/FE04NotificationRuntime/Test-Launcher.ps1',
 'scripts/verification/FE04NotificationRuntime/New-ApprovalPackage.ps1',
 'scripts/verification/FE04NotificationRuntime/APPROVAL_PACKAGE.md',
 'scripts/verification/FE04NotificationRuntime/PERMISSION_CONTRACT.json',
 'src/HotelOps.Api/Services/Notifications/NotifWriter004AContract.cs',
 'src/HotelOps.Api/Data/IntegrationSqlGuard.cs',
 'src/HotelOps.Api/Services/Notifications/CleaningAssignmentStore.cs',
 'src/HotelOps.Api/Services/Notifications/CleaningNotificationRuntime.cs',
 'src/HotelOps.Api/Services/Notifications/ProvisionalCleaningNotification.cs',
 'scripts/database/notifications/Schema002/AcceptanceCases.json',
 'scripts/database/notifications/Schema002/Install.ps1',
 'scripts/database/notifications/Schema002/Preflight.sql',
 'scripts/database/notifications/Schema002/Verify.sql',
 'scripts/database/notifications/Fe04UnblockP0/01_ReconcileCandidate.sql',
 'scripts/database/notifications/Fe04UnblockP0/02_RollbackCandidate.sql',
 'scripts/database/notifications/N01Implement06/01_ForwardCandidate.sql',
 'scripts/database/notifications/N01Implement06/02_RollbackCandidate.sql',
 'scripts/verification/Schema002CorrectionFix02/NamedIndexContract.json',
 'scripts/verification/Schema002CorrectionFix02/ActualEvidence.cs',
 'scripts/verification/Schema002CorrectionFix02/README.md'
)
$artifacts=@(foreach($path in ($paths|Sort-Object -Unique)){
 [pscustomobject]@{path=$path;sha256=(Get-FileHash -LiteralPath (Join-Path $root $path) -Algorithm SHA256).Hash}
})
$canonical=($artifacts|ForEach-Object {$_.path+[char]0+$_.sha256+"`n"}) -join ''
$sha=[Security.Cryptography.SHA256]::Create()
try{$setHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-','')}finally{$sha.Dispose()}
function Decision($Id,$Owner,$Authority,$Status,$Proposal,$Evidence,$Next){
 [pscustomobject]@{id=$Id;responsibleOwner=$Owner;approvalAuthority=$Authority;status=$Status;proposedContract=$Proposal;
  artifactSetSha256=$setHash;evidence=$Evidence;requiredNextAction=$Next}
}
$decisions=@(
 (Decision 'D01_AUTH' 'ENV-001' 'SecurityOwner + ProjectLeader' 'PENDING' 'SQL_LOGIN; mixed mode already present; no server roles/restart.' 'ENV OWNER-DECISIONS and authentication-mode evidence.' 'Approve authentication and named-login provisioning scope.'),
 (Decision 'D02_MAPPING' 'ENV-001' 'SecurityOwner + RuntimeOwner' 'PENDING' 'One HotelOps_DEV2_FullStack_Runtime login and same-named authenticated user in exact GUID; fs013_app remains WITHOUT LOGIN.' 'ENV reports runtime login/user absent.' 'Approve mapping, then ENV verifies SID/authentication under real restricted connection.'),
 (Decision 'D03_PERMISSIONS' 'DEV1 FE04 proposal / ENV execution' 'SecurityOwner + NotificationOwner' 'PENDING' 'Exact PERMISSION_CONTRACT.json; native module ownership chain and retained explicit graph/assignment DENYs; no direct EF graph staging.' 'Prior 49-entry baseline not accepted full 15-case profile.' 'Approve object/operation rights, scoped metadata observation and delivery/inbox profile; no grants yet.'),
 (Decision 'D04_ADAPTER' 'DEV1 FE04' 'ProjectLeader + RuntimeOwner + SecurityOwner' 'PENDING' 'Accept signed owner/source/fixture/catalog preflight, protected trust policy/credential pipe and rollback-only probe API. No production DI.' 'Source files and focused receipts; no owner signatures shipped.' 'Accept exact adapter/launcher hashes and OS-protected trust transport.'),
 (Decision 'D05_NOTIFICATION_HOOK' 'DEV2 Notification exact store/runtime hook; DEV1 isolated caller wiring' 'NotificationOwner + DEV1 FE04' 'BLOCKED' 'Exact-GUID approved runtime guard hook required in CleaningAssignmentStore.EnsureAsync and CleaningNotificationRuntime.RequireClean; leave historical guard intact.' 'Pinned KIENPC/HotelOps_DEV2_FullStack_Trial guards; published Writer004A is predicate only.' 'Owner approve concrete hook/permission lane; DEV1 wires accepted isolated candidate only, no public activation.'),
 (Decision 'D06_SCHEMA' 'FS013 Schema Owner' 'SchemaOwner' 'PENDING' 'Choose existing assignment-only FE04 lane vs full Fix02 probe lane. No guessed index/filter DDL; prior trigger capture/restoration mandatory.' 'Schema002 Verify.sql and Fix02 NamedIndexContract.json describe different index filters.' 'Approve exact installer/rollback/catalog; provide missing preserved workspace if full Fix02 lane required.'),
 (Decision 'D07_FIXTURES_OBSERVER' 'Schema/Runtime owners' 'SchemaOwner + RuntimeOwner' 'BLOCKED' 'Signed actual fixture bytes/IDs/ownership; accepted independent SqlException.Errors observer; no TRY/CATCH-derived provider proof.' 'Missing preserved .fe04schema002/.n01sql09/.n01decision08 manifests and trust/provider implementations.' 'Provide exact fixture manifest, source/assembly provenance, provider observer and owned authoritative scenario flows.'),
 (Decision 'D08_LEASE' 'ENV-001' 'RuntimeOwner + NotificationOwner' 'BLOCKED' 'Original lease expired. New accepted lease plus versioned Writer successor required; published hard expiry remains enforced.' 'Original expiry 2026-11-10T11:44:09.1792768Z; Writer004A HardExpiry.' 'Owner reissue lease and publish accepted versioned expiry contract; no timestamp edits by DEV1.'),
 (Decision 'D09_MUTATION_SCOPE' 'DEV1 FE04 runner / ENV fixtures' 'RuntimeOwner + ProjectLeader' 'PENDING' 'Exactly cases 01-15, sole central runner, real session/branch/fixture authorization; no worker/public producer; cleanup separate.' 'AcceptanceCases.json, all 15 NOT_RUN.' 'Approve case-specific mutation/fixture scope and concurrency subcase lane after prior gates.'),
 (Decision 'D10_RUNTIME_HANDOFF' 'ENV-001' 'RuntimeOwner' 'BLOCKED' 'Protected DPAPI credential + real TLS/permissions/GUID/catalog verification; matching source/assembly owned 7029 only after release.' 'Existing 7029 source e76d6fc uses hax, not restricted acceptance runtime.' 'Provision only after decisions ACCEPTED; verify actual connection and then owned runtime handoff. Do not stop another module.' )
)
[pscustomobject]@{
 package='FE04_NOTIFICATION_FINAL_APPROVAL_PACKAGE';state='CONTRACT_PREPARED_NOT_ACCEPTED';
 parentSource='22b005b106e8dc1eeb75205b98c0b341af23c697';dev2Source='d60022b487b5b59e84d6551a0ee5162c9fd32164';
 target=@{server='DESKTOP-JI8U50T\SQLEXPRESS';database='HotelOps_ENVNOTIF001_Trial_20261109';guid='a138fa48-cc14-4070-9ca4-793f1b60c5c1'};
 artifactSetSha256=$setHash;artifacts=$artifacts;decisions=$decisions;
 contractAccepted=$false;provisioningHandoff='NOT_READY_DO_NOT_PROVISION';sqlAcceptanceReady=$false;
 sql=@{pass=0;fail=0;notRun=15};https='NOT_RUN';unitFixturesAreOwnerAuthority=$false;
 runtimeAssemblyBinding='After exact source build: owners sign actual loaded API and launcher hashes; no placeholder accepted.';
 expectedCatalog='Existing assignment-only schema unchanged until explicit owner decision. Capture actual ordered CatalogSql output and module UTF-16 hashes; never infer from source.'
}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $PSScriptRoot 'APPROVAL_PACKAGE.json') -Encoding UTF8
Write-Output "CONTRACT_PREPARED_NOT_ACCEPTED; artifact set $setHash; decisions=$($decisions.Count); SQL_15_NOT_RUN"
