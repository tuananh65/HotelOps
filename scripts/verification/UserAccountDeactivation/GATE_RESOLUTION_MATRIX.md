# Task58 current - actual DEV1 SQL acceptance and defects repaired

2026-10-30, unchanged HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`.
[DEV1 acceptance](TASK58_DEV1_WRITER_ACCEPTANCE.md): actual services, 112 PASS/0 FAIL,
83 DMV-confirmed waits, all mapped rows preserved after owned cleanup on a fresh canonical
patched trial. Baseline 101 PASS/6 FAIL retained: Create/Profile stale revoked actor sessions
and four provider-cancellation failures. Added transactional SQL session revalidation to
Create/Profile and cancellation normalization to the existing DEV1 global snapshot read.
No new runtime responsibility/release gate or change to Deactivate source/registration.

| Existing gate / subset | Executed Task58 evidence | Disposition / exact remaining dependency |
|---|---|---|
| Existing DEV1 account/auth subset of DEV1_WRITER_COORDINATION | Create, Activate, Role, Branch, Profile, Reset, Login, Logout, PasswordChange, Touch, RevokeAll, Reauthenticate: both-order User serialization, actual Deactivate-first denial, stale/cancel/1222/1205/audit rollback. Real Profile HTTPS regression 329, reauth/reset 165, credential participant 26 | Scoped acceptance PASS after fixes and source review; NOT whole DEV1 gate acceptance or deferred writer approval |
| DEV1_WRITER_COORDINATION (whole gate) | E08 current implementation now has executable evidence; LastAdmin raw Reset rejection + foundation tests; no assignment writer fabricated | PENDING: deferred workflows remain required, deployed/import/job/HR writer inventory UNKNOWN, SA57/D06/D07 not approved; complete applicable criteria/review absent |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | ReturnWork actual regression 57 PASS, source unchanged; predicates/other historical receipts kept scoped | PENDING: H01–H12 in TASK58_DEV2_HANDOFF.md specify exact methods, risks, owner corrections and tests; Warehouse/Asset/MR breadth and authorization evidence incomplete |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | No unexpected inversion in executed DEV1 schedules; forced test-only 1205 cycles verify rollback/retry, not canonical deadlock freedom | PENDING: feature-first actor/FK/detail reads, stale tracked resource paths, complete caller/callee/isolation and external deployment graph require proof/review |
| WORKDAY_CONTRACT / D05 | D05 remains APPROVED: Working requires employee End even no interval; no auto-end/Admin forced-end. No Workday model or writer added | UNRESOLVED implementation, unchanged |
| INTERNAL_REQUEST_CONTRACT, LINE_MANAGER_CONTRACT, OPERATIONAL_TASK_CONTRACT, SHIFT_CONTRACT | No deferred implementation or clear evaluator; --contracts actual service still ResponsibilitiesUnresolved | Four runtime UNRESOLVED retained, unchanged |
| UNSCHEDULED_WORKDAY_MODEL, MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL | Missing models explicitly retained in fresh SQL regression | Two UNSUPPORTED retained, not NoBlocker |

No gate closed or SA57 activated. Full actual UC006 success/deactivation-first INACTIVE commit,
deactivation session/audit/LastAdmin poststate and HTTP remain BLOCKED/NOT RUN. Handoff items
are implementation acceptance work, not extra target-owned business predicates.

# Task57 historical - approved D05, deployment scope and acceptance separation

2026-10-30; unchanged baseline `4a187320643d0c82f232c6b9dd5a667b8401c280`.
[Deployment scope proposal](UC006_V1_DEPLOYMENT_SCOPE.md) inventories actual registrations,
all writer families E01–E08 and existing/deferred/deployment-unknown acceptance.
[Deferred matrix](UC006_DEFERRED_WORKFLOWS.md) gives F01–F10 SQL/legacy/writer dependencies.
Delivery priority is approved, scope exclusion is not. No runtime/gate/DI/HTTP change.

| Gate / decision | Task57 disposition | Evidence / next action | Result |
|---|---|---|---|
| WORKDAY_CONTRACT / D05 | D05 APPROVED V1 POLICY: Working blocks even zero intervals; employee End first; Ended does not clear source assignments; no auto-end/Admin forced-end | F04/F05: no authoritative Workday/interval model/writer. Separate operational exception approval if employee cannot End | Policy RESOLVED; implementation gate UNRESOLVED |
| INTERNAL_REQUEST_CONTRACT | Delivery deferred; partial current-handler SQL model still permits legacy duty | F01, D01 closure/current-cycle and D04 typed receiver/transfer; external writer/data UNKNOWN | UNRESOLVED |
| OPERATIONAL_TASK_CONTRACT | Delivery deferred; SQL COMPLETED not accepted-Closed proof | F02/F09, verification/reopen/JOB05 authoritative mapping and eligibility writers | UNRESOLVED |
| SHIFT_CONTRACT | Delivery deferred; current attendance subset retained, clock expiry not release | F03; D03 future roster boundary and exact lifecycle; deployment/legacy UNKNOWN | UNRESOLVED |
| LINE_MANAGER_CONTRACT | No new formal D02 approval; approved broad current-management blocker retained | F10 transfer/history/employment binding + HR/external writer inventory | UNRESOLVED |
| UNSCHEDULED_WORKDAY_MODEL | Requirement present; missing source model does not prove no deployed responsibility | F05; D05 applies, D06 exclusion not approved | UNSUPPORTED retained |
| MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL | Request/shift models missing; asset subtype already implemented is not substitute | F06/F07, D04 exact binding/atomic transfer, deployed receiver obligations UNKNOWN | UNSUPPORTED retained |
| DEV1_WRITER_COORDINATION | Can accept E08 account/auth operations now; missing business writers separately assigned, not silently excluded | Existing actual Activation evidence limited; remaining auth/account graph + F01–F10 DEV1 paths/external inventory need owner review | PENDING, not cleared |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | Can test actual E01–E07; ReturnWork scoped evidence retained, merge no longer pending | Warehouse/Asset/MR broader integration/fault coverage; server-side actor/session/scope/SoD gap on non-Inventory controllers; JOB04 separate | PENDING, not cleared |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | Can review/test current E01–E08 whole graph; future writers remain separate dependencies | Feature-first actor/FK/detail/Catalog paths need complete SQL/plan/isolation proof and deployed writer review | PENDING, not cleared |

D01–D04/D06–D07 specific proposals remain unapproved; prior V1 PROVISIONAL approvals preserved.
SA57 is a draft for human review, not an active OUT_OF_SCOPE decision or new gate.
No runtime result counts changed: five Unresolved + two Unsupported; three separate release
gates pending. Clean-target actual success remains BLOCKED; Task57 SQL/build/tests NOT RUN.

# Task56 historical - contract source and implementation gap classification

Authoritative current proposal: [UC006_V1_CONTRACT_MATRIX.md](UC006_V1_CONTRACT_MATRIX.md).
Task56 reads UC/SRS, Business Rules, draft Permission Matrix, database design/mappings,
actual source/writers and prior approved V1 PROVISIONAL rules. Six core rules were also
cross-checked in the original Word SRS. No runtime/predicate/gate is changed or added.

The evidence now distinguishes known business requirements from missing implementation:

| Existing gate | Task56 finding | Concrete next action |
|---|---|---|
| INTERNAL_REQUEST_CONTRACT | UC115/119/120/121 and GBR146-153 define current handler, ack transfer, closure and reopen. SQL lifecycle/typed receiver binding and production writers are missing. BF06 requester confirmation versus UC120 closure is a specific source conflict | D01 selects closure interpretation; implement authoritative lifecycle/current-cycle and request handover. Block current handler/mandatory receiver; do not infer requester approval from FK |
| LINE_MANAGER_CONTRACT | Existing V1 approval already blocks current management; SRS does not define every reporting FK as open work, and DH role is not scalar line manager | D02 finalizes current/history/employment release semantics and transfer writer; retain existing positive subset until then, no repeated approval of the broad V1 rule |
| OPERATIONAL_TASK_CONTRACT | GBR132/133 explicitly distinguish submit Pending Verification from accepted Closed; GBR134/124 specify reopen and JOB05 creation | Implement authoritative verification/cycle mapping and every assignment/return/reopen/job writer; SQL COMPLETED alone cannot clear current owner |
| SHIFT_CONTRACT | UC138 defines roster and future cancellation; UC140 explicitly says acknowledgment does not reassign source work | D03 settles future-roster UC006 boundary and exact SQL lifecycle; preserve actual open evidence, never release by elapsed clock or snapshot ack |
| WORKDAY_CONTRACT | UC139/GBR142-143 require Working/Ended and unscheduled alternative; Task57 D05 now APPROVED, Working-alone policy no longer pending | Implement UC139/interval representation separately; employee End required even zero interval, no invented Admin forced-end operation |
| UNSCHEDULED_WORKDAY_MODEL | Explicit in original SRS UC139; recommended IN_SCOPE_MISSING_IMPLEMENTATION | No OUT_OF_SCOPE applied. Implement independent approved representation or supply a deliberate scope amendment plus complete writer/deployment proof |
| MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL | UC119 transfers handler at ack; UC140 ack is awareness only. V1 already blocks unfulfilled mandatory next actor | D04 binds subtype/current actor/state/cycle and atomic transfer; do not re-ask the general rule or substitute asset/free-text data |
| DEV1_WRITER_COORDINATION | Release acceptance, still pending; source lacks business writer implementation but SRS specifies JOB05 and other paths | New matrix W01-08/W17-18 and §7 specify complete writer inventory, participant lock sets and actual SQL protocol proof |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | Release acceptance, still pending; scoped ReturnWork and classifier PASS do not cover all families | New matrix W09-16 and §7 specify all operations, current/history/reapproval/default creation and drift/fault evidence |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | Release acceptance, still pending; actor/FK/navigation reads in feature-first paths are part of the graph | New matrix §7 requires complete graph/plan/isolation and both-order SQL evidence, including no-owner/release-only paths |

Workflow scope recommendations: six IN_SCOPE_MISSING_IMPLEMENTATION, one CONTRACT_UNRESOLVED
(line manager); implemented predicate subsets are marked separately. No complete missing
workflow is relabeled IN_SCOPE_IMPLEMENTED and none is automatically OUT_OF_SCOPE.

Decision register D01-D07 includes recommended outcomes and implementation exit criteria,
not a generic request to “supply mapping.” Broad Task48/53 V1 approvals remain in force.
Release criteria explicitly avoid requiring full successful Deactivate before the fuse can
ever permit it: pre-release protocol evidence -> reviewed source promotion after acceptance
-> full actual internal UC006 before any endpoint enablement. This boundary is a proposal
for D07 review, not a change to current guard or a new safety gate. Historical notes follow.

# Task55B historical - 2026-10-30

Baseline HEAD remains 4a187320643d0c82f232c6b9dd5a667b8401c280. The previous eight
Unresolved codes are now classified as **five runtime business uncertainties** and **three
pending release acceptance gates**, not eight target-owned duties. Both Unsupported models
remain. Moving a release gate does not approve it or reduce the requirements for mutation.

`UserAccountDeactivationReleaseAcceptance.cs` holds a read-only, nonconfigurable pending list.
`UserAccountDeactivationService.DeactivateAsync` calls its EnsureAccepted immediately before
the first account mutation, after runtime review. If runtime ever becomes clear while release
is pending, the service returns ReleaseAcceptanceRequired. There is no injected approval,
environment switch, test clear evaluator or alternate mutation method. Actual execution of
that future clear-runtime service branch is NOT RUN: the real fixture still has business/model
gates. The independent fuse rejection/immutability contract is tested; service safety is also
reviewed at the only mutation entry point.

| Gate | Type | Root Cause | Evidence | Action | Result | Remaining Dependency |
|---|---|---|---|---|---|---|
| INTERNAL_REQUEST_CONTRACT | Runtime / CONTRACT_REQUIRED | SQL lifecycle and requester/receiver next actor differ from SRS; no actual controller/service implements the transition | ApprovedDev1ResponsibilityQueries.ReadAsync; Entities/InternalRequest, InternalRequestAssignment, InternalRequestEvent; internal_requests.status DRAFT/SUBMITTED/IN_PROGRESS/COMPLETED/CLOSED/REJECTED/CANCELLED; assignments assignee_id/status/assigned_at/ended_at. SRS UC114-123, appendix GBR151/UC119 requires receiver acknowledgment | Keep ACTIVE+NULL end+IN_PROGRESS assignee blocker, valid ended history exclusion, contradictions/ambiguous parents unresolved. SQL rerun covers current assignee, history and creator/requester attribution | UNRESOLVED; proven subset PASS | Approved SQL-to-SRS mapping, exact requester/receiver next actions, structured mandatory acknowledgment and actual coordinated writer |
| LINE_MANAGER_CONTRACT | Runtime / CONTRACT_REQUIRED | Current employee-manager designation is explicit, but release/transfer and nonactive employment semantics are not specified by a writer | users.manager_user_id FK to User; deleted_at/employment_status; UserProfileService updates only name/email/phone, CreateAsync sets no manager; no line-manager mutation controller/service found. Department Head role meaning is separate per SRS_CHANGE_NOTES | New conservative V1 positive subset: manager_user_id=target, deleted_at NULL, employment_status ACTIVE -> LINE_MANAGER_DESIGNATION. Account INACTIVE alone does not end employment. Deleted/reference-only rows do not clear the global contract | UNRESOLVED full contract; positive/negative SQL subset PASS | Approved transfer/release rule and authoritative employment/manager writer, including inactive employment and external HR/import paths |
| OPERATIONAL_TASK_CONTRACT | Runtime / CONTRACT_REQUIRED | SQL COMPLETED cannot distinguish SRS Pending Verification versus Closed; reviewer/reopen/ack actor not fully represented | OperationalTask/OperationalTaskAssignment mappings; TODO/IN_PROGRESS/BLOCKED/COMPLETED/CANCELLED and ACTIVE/COMPLETED/REASSIGNED/CANCELLED assignments. SRS UC102-112 / appendix task transitions. No corresponding controller/service/job implementation found | Preserve ACTIVE+NULL end+TODO/IN_PROGRESS/BLOCKED blocker and valid ended history exclusion; ACTIVE+COMPLETED remains OPERATIONAL_TASK_TERMINAL_MAPPING. Rerun positive/negative SQL cases | UNRESOLVED; proven subset PASS | Exact completion/verification/reopen/remaining-duty mapping and coordinated writers; completed_at alone does not prove acceptance |
| SHIFT_CONTRACT | Runtime / CONTRACT_REQUIRED | Schema supplies roster/attendance, but only defaults ASSIGNED/PLANNED/NOT_STARTED and no complete allowed lifecycle or handover release mapping | ShiftAssignment/WorkShift/ShiftAttendance; target user_id, assignment_status, assigned_at, actual_check_in_at/out, scheduled times. SRS UC138-140; no Shift controller/service/job found | New positive actual evidence: ASSIGNED + actual check-in + no checkout, valid timestamps -> SHIFT_OPEN_ATTENDANCE. Elapsed scheduled end alone never releases it. Missing/future/reversed timestamps fail closed; unmapped assignment vocabulary with open attendance is unresolved. Attendance status/Workday terminal semantics remain unapproved | UNRESOLVED full contract; SQL subset PASS | Approved lifecycle/status/timezone/checkout-versus-handover rules and actual coordinated writer. Timestamp history is not an acknowledgment |
| WORKDAY_CONTRACT | Runtime / MODEL_MISSING | Unscheduled Workday cannot be inferred from required ShiftAssignment attendance | ShiftAttendance.ShiftAssignmentId is nonnullable; canonical sys.columns verified; no independent Workday entity/table/controller/service/job. SRS UC139 Working/Ended is conceptual, not implemented | Retain uncertainty and Unsupported model; do not make every roster entry a blocker or declare empty rows safe | UNRESOLVED | Authoritative Workday/current-duty representation or proven approved V1 exclusion with complete production-writer inventory |
| UNSCHEDULED_WORKDAY_MODEL | Runtime / MODEL_MISSING | No authoritative independent Workday representation in current source/canonical schema | Compiled API/source inventory, Program DI, no hosted worker registration; sys.tables Workday search zero; shift_attendance requires FK | Conditional exclusion proposal below; NOT APPLIED | UNSUPPORTED | Deployment/schema parity plus external writer/access inventory; source absence alone does not prove production absence |
| MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL | Runtime / MODEL_MISSING | Request/shift receiver acknowledgment is not structured; free text/event actor is not the designated receiver | internal_request_events columns actor_id/event_type/event_note/old_status/new_status; no receiver/ack columns; no request/shift handover controller/service. Existing asset_handovers is a different modeled DEV2 workflow | Keep Unsupported independently checked by service. Do not read event authors/history as pending receiver obligation or terminal proof | UNSUPPORTED | Authoritative model/workflow or proven approved V1 exclusion; no schema/migration added |
| DEV1_WRITER_COORDINATION | Release / WRITER_COORDINATION_REQUIRED | No current DEV1 assignment/rework/inspection/manager/shift/workday/request writer implementation to certify; external imports/jobs/direct SQL unverified | Source writer inventory below; obj/task55b-writer-source-evidence.log. Existing account/credential/access writers are distinct | Move from unconditional business Unresolved list to mandatory release fuse; actual DEV1 ActivateAsync SQL lock races/faults tested | PENDING release; not passed vacuously | Complete actual writer inventory/protocol and SQL races for each future/legacy responsibility writer; no missing writer is simulated |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | Release / WRITER_COORDINATION_REQUIRED | Scoped ReturnWork acceptance is not acceptance for all actual writers/full Deactivate success | Task55A source/receipts and Task55B actual ReturnWork rerun | Move unchanged requirement into mandatory release fuse | PENDING release | Full actual integration races after business/model contracts close |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | Release / WRITER_COORDINATION_REQUIRED | Broad Users TABLOCKX versus other feature-first actor reads remains unaccepted | GlobalAdminSerialization.ReadSnapshotAsync; non-owner-prepared Maintenance paths, Asset reject/retire and other feature-first actor reads. ReturnWork's new Users-first path does not certify them | Move into mandatory release fuse; retain canonical order and no DEV2/shared serialization edit | PENDING release | Compatible complete writer protocol with real two-order blocking/deadlock evidence |

## DEV1 writer inventory and scope of SQL acceptance

| Actual source / method | Writes that matter | Current ordering / finding |
|---|---|---|
| UserAccountCreationService.CreateAsync | New User, branch membership, role assignment, credential/audit; no ManagerUserId/current work assignment | Global guard/Users universe before reference/catalog locks and writes; no assignment model writer is hidden here |
| UserAccountActivationService.ActivateAsync | Account ACTIVE/version + atomic user audit | Sorted retained actor/target Users before authority locks, SQL session/version reread, write/commit. Actual writer-first and User-lock-first/stale/cancel/post-flush failure tested in Task55B |
| UserProfileService.UpdateAsync | Explicit whitelist FullName/Email/Phone | Existing global guard and Users-first locks; cannot set manager, employment, department or business assignment |
| UserRoleAssignmentService.AssignAsync | Role assignment/version/audit | Global guard/Users universe then sorted Users before authority/destination locks; grants do not designate a current business assignee |
| UserBranchAssignmentService.AddAsync | Branch membership/version/audit | Retained Users-first authority protocol; no current work/manager field mutation |
| AdminCredentialResetWorkflow.ResetAsync / TemporaryCredentialService | Credentials, revocation, audit | Existing global Users-first foundation; no business assignment/current-duty restoration writer |
| Cleaning/rework/inspection/defect/Operational/InternalRequest/Shift/Workday/line-manager writers | No controller/service/registered hosted job found in this API | Only entities/mappings, authorization readers and deactivation readers; Inventory warehouse manager is DEV2 and untouched. Missing implementations/external inventory cannot receive PASS |

Canonical source has the required FK/status fields but no implemented Workday/ack workflow.
The inspected source inventory is evidence about this checkout, not an assertion about a
live database that was not accessed. No DEV1 business writer requiring a lock-order fix was
found. No new generic assignment writer is invented solely to make a concurrency test pass.
Line-manager employment classification materializes target manager references and compares
stored states ordinally; unmapped employment/casing adds LINE_MANAGER_EMPLOYMENT_MAPPING.
Final SQL evidence: task55b-contracts-accepted.local.json, 64 total checks including 22 new
contract/release/model cases, 8 existing-writer/LastAdmin foundation checks and 3 DMV waits.

## Conditional V1 exclusion proposal - not applied

Exclude UC139 unscheduled Workday and UC119/140 mandatory acknowledgment from V1 only after
the deployed artifact and schema are attested to match this inventory, all API/import/job/
external/direct-SQL writer entry points are inventoried and shown unable to create these
obligations, and any existing alternative representation is ruled out. Keep the features
unimplemented/denied in that scope; adding a writer later requires an approved representation,
predicate and coordinated lock protocol before release. Current evidence proves no such API
implementation here, but cannot establish external production-writer absence without accessing
or receiving an authoritative inventory. Therefore neither exclusion is applied and both
Unsupported gates remain. No feature/configuration flag can bypass them.

## Current result

Task55A combined list 8 Unresolved + 2 Unsupported -> Task55B runtime 5 Unresolved + 2
Unsupported, plus 3 separate mandatory pending release gates. This is a separation, not
three acceptance PASS results. A clean actual internal service attempt remains
ResponsibilitiesUnresolved, preserving account/version/credentials/access/sessions/audit.
Actual successful mutation, session revocation/audit commit, post-mutation audit rollback and
LastAdmin service poststate remain BLOCKED. Foundation LastAdmin checks are reported separately.

# Task55A historical update

Official ReturnWork merge 4a187320643d0c82f232c6b9dd5a667b8401c280 is integrated by fast-forward.
The historical MAINTENANCE_RETURN_WRITER_COORDINATION row below is now RESOLVED for the
missing writer protocol: actual User -> Incident -> Work order, SQL eligibility after wait,
authoritative reread/drift rejection and scoped race/fault tests PASS. DEV1 removes only
that obsolete unconditional constant. The remaining writer/global acceptance gates remain.
`task55a-returnwork-accepted.local.json` records the real service races and their precise
limits. Full successful Deactivate-first remains BLOCKED; no direct INACTIVE fixture is
reported as integrated success. See INTEGRATION_DEPENDENCIES.md and LOCK_ORDER.md.

Task54 clean baseline 0 Blockers / 9 Unresolved / 2 Unsupported becomes Task55A 0 / 8 / 2.
All remaining gate IDs and dependencies are in REMAINING_BLOCKERS.md. The Task54 table and
receipts below are retained as historical evidence, not a claim that the hotfix is pending.

# Task54 historical - exact responsibility gate resolution

Baseline: `task53-readiness-final.local.json`, actual internal service attempt, HEAD
`71a17f98b8c8e1dc0cfa54a12ca595e4e44a800d`. The ten Unresolved and two Unsupported
identifiers below are copied from that receipt, not inferred from the count.
`task54-gates.local.json` embeds that baseline and the new actual SQL result.

Source aliases below are relative to `src/HotelOps.Api/`:

- **D1**: `Services/Users/ApprovedDev1ResponsibilityQueries.cs`, `ReadAsync`.
- **INT**: `Services/Users/IntegratedDeactivationResponsibilityQueries.cs`, `ReadAsync`.
- **D2**: `Services/Users/DeactivationResponsibilityQueries.cs`, `ReadApprovedDev2BlockersAsync` (preserved).
- **STATE**: `Services/Users/Dev2ResponsibilityStateQueries.cs`, `ReadUnresolvedAsync` (Task54).
- **MAP**: `Data/HotelOpsDbContext.cs` and the corresponding generated `Entities/` classes (read only).

Types: IMPLEMENTABLE = source/schema/approved semantics suffice; CONTRACT_REQUIRED =
representation exists but release/current-duty rules are incomplete; MODEL_MISSING =
no authoritative representation; WRITER_COORDINATION_REQUIRED = predicates alone cannot
prove races safe. BLOCKED is an operation outcome, not a stored responsibility state.

| Gate | Type | Root Cause | Evidence | Action | Result | Remaining Dependency |
|---|---|---|---|---|---|---|
| DEV1_WRITER_COORDINATION | WRITER_COORDINATION_REQUIRED | Missing complete responsibility-writer inventory/protocol, not a column mapping defect | D1.ReadAsync adds this unconditionally. Reads cleaning_assignments, cleaning_reworks, room_inspections, inspection_defects, operational_task_assignments, internal_request_assignments, departments; no complete corresponding mutation/import/job implementation is present in Services | Preserve gate. Require discovery of designated users, retained ascending Users eligibility locks before feature locks, authoritative owner reread and drift rejection in each actual writer | UNRESOLVED | DEV1 writer implementations and inventory of external/import/job writers, then real SQL races; absent methods cannot be certified |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | WRITER_COORDINATION_REQUIRED | Actual integrated writer/deactivation races are not accepted | INT.ReadAsync; combines D2's six tables with D1. No extra SQL status or column defines this gate. `INTEGRATION_DEPENDENCIES.md` specifies ReturnWork tests | Keep gate independent of vocabulary validation | UNRESOLVED | DEV2 hotfix, compatible lock order and actual full-service races after all other eligibility gates close |
| DEV2_UNKNOWN_STATE_COVERAGE | IMPLEMENTABLE | Six exact positive predicates previously lacked unknown/contradictory-state classification | INT.ReadAsync previously added unconditional code. D2: maintenance_works.assignee_id/status; warehouses.manager_user_id/is_active; assets.custodian_user_id/lifecycle_status; asset_handovers.to_user_id/handover_status; material_requests.requester_id/status; material_request_approval_cycles.assigned_approver_id/status/revision_no | Add STATE SQL-backed scalar reads, ordinal classification, current/history vocabulary and marker/parent checks; preserve D2 blockers and latest-revision rule. Unknown/legacy/inconsistent rows add this code plus a precise diagnostic | RESOLVED for clean modeled fixture: removed from unconditional gates; 29 positive/negative SQL cases PASS. Malformed/legacy data still UNRESOLVED | Future vocabulary changes require explicit classifier/fixtures. This resolves state coverage, not DEV2 writer acceptance |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | WRITER_COORDINATION_REQUIRED | Broad retained Users lock can conflict with feature-first actor reads | `Services/Users/GlobalAdminSerialization.cs`, GlobalAdminTransaction.ReadSnapshotAsync: dbo.users TABLOCKX,HOLDLOCK before feature reads. `Services/Maintenance/MaintenanceWorkService.Commands.cs`, ExecuteCorrectiveAsync: Incident/Work before RequireUser. `Services/Assets/AssetService.Handover.cs`, FinalizeHandoverAsync and `AssetService.Retirement.cs`, RetireAssetAsync: feature before RequireActorAsync | Preserve gate; document actual inversion, require all participant/actor Users locks before feature locks and reread/discovery checks; no shared serialization change | UNRESOLVED | DEV2-compatible writer protocol plus two-order blocking/deadlock evidence. Existing retained locks do not alone prove safety |
| INTERNAL_REQUEST_CONTRACT | CONTRACT_REQUIRED | Requester/receiver next action and mandatory acknowledgment/release are not fully mapped | D1.ReadAsync; MAP InternalRequest/Assignment/Event. internal_requests.requester_id/status DRAFT/SUBMITTED/IN_PROGRESS/COMPLETED/CLOSED/REJECTED/CANCELLED; internal_request_assignments.assignee_id/status/assigned_at/ended_at; events actor_id/status/note do not designate acknowledgment receiver | Keep proven ACTIVE + ended_at NULL + IN_PROGRESS assignee blocker and valid ended history exclusion. V1 proposal: block explicitly assigned next actor until authoritative completion/ack; never infer it from event author | UNRESOLVED; positive subset already implemented/tested in Task53, retained in Task54 | Approved next-actor/release mapping and authoritative receiver acknowledgment model/writer; V1 permission to retain uncertainty does not supply these data |
| LINE_MANAGER_CONTRACT | CONTRACT_REQUIRED | Scalar subordinate-manager reference lacks current-duty/release semantics | D1.ReadAsync; MAP User.ManagerUserId -> users.manager_user_id plus account_status/employment_status/is_deleted. departments.manager_user_id + is_active is a separate proven blocker | Preserve active department predicate. V1 proposal: block explicit current management obligation, release only after verified transfer/end; do not equate Department Head RBAC with this scalar FK | UNRESOLVED | Define which subordinates impose a current duty, inactive/deleted cases and authoritative transfer/release; implement/test actual manager writers |
| MAINTENANCE_RETURN_WRITER_COORDINATION | WRITER_COORDINATION_REQUIRED | Return creates renewed assignee responsibility without preparing retained owner locks | `Services/Maintenance/MaintenanceWorkService.Workflow.cs`, ReturnWorkAsync: maintenance_works COMPLETED -> IN_PROGRESS, retained assignee_id, incident RESOLVED -> IN_PROGRESS. Calls ExecuteCorrectiveAsync without prepareOwners; assignment path in Commands.cs does prepare owners | No DEV2 edit. Required order: discover assignee -> ascending retained Users eligibility locks -> Incident/Work locks -> reread assignee/state, reject drift -> write/commit | UNRESOLVED; hotfix pending in local source | DEV2 ReturnWorkAsync hotfix and exact real writer/service tests already specified in INTEGRATION_DEPENDENCIES.md |
| OPERATIONAL_TASK_CONTRACT | CONTRACT_REQUIRED | SQL COMPLETED does not identify Pending Verification/Closed/reopen or acknowledgment responsibility | D1.ReadAsync; MAP OperationalTask/Assignment. operational_tasks.status TODO/IN_PROGRESS/BLOCKED/COMPLETED/CANCELLED; operational_task_assignments.assignee_id/assignment_status/assigned_at/ended_at | Keep ACTIVE + NULL end + TODO/IN_PROGRESS/BLOCKED blocker and valid ended history exclusion. V1 proposal: only release COMPLETED after proving no rework/verification/ack/current duty; existing fields do not supply full proof | UNRESOLVED; ACTIVE+COMPLETED also yields OPERATIONAL_TASK_TERMINAL_MAPPING when present | Approved lifecycle/verification/reopen mapping and authoritative remaining-duty evidence/writers; no invented status |
| SHIFT_CONTRACT | CONTRACT_REQUIRED | Schema defaults do not define current versus historical shift responsibility | D1.ReadAsync; MAP ShiftAssignment/WorkShift/ShiftAttendance. shift_assignments.user_id/assignment_status (default ASSIGNED), work_shifts.status (default PLANNED), shift_attendance.attendance_status (default NOT_STARTED), actual_check_in_at/actual_check_out_at | V1 proposal: block demonstrable current assigned/checked-in duties until completed release/handover; elapsed scheduled time alone cannot release. No predicate installed without approved status/time/ack mapping | UNRESOLVED | Exact status whitelist, timezone/boundaries, overnight/cancelled/missed rules, checkout versus handover release and coordinated writers |
| WORKDAY_CONTRACT | MODEL_MISSING | UC139 unscheduled Working/Ended cannot be represented by required shift assignment | D1.ReadAsync; MAP ShiftAttendance.ShiftAssignmentId is nonnullable; dbo.shift_attendance.shift_assignment_id FK. No independent Workday entity/table/writer found; Task53 live metadata evidence retained in obj/task53-model-schema-evidence.log | Retain gate; V1 explicitly permits blocking only proven SQL current duties and requires unresolved when they cannot be determined. Do not make all scheduled days blockers | UNRESOLVED | Authoritative unscheduled-workday representation and Working/Ended/current-duty contract; see unsupported model row |
| UNSCHEDULED_WORKDAY_MODEL | MODEL_MISSING | Missing model, not an empty responsibility query | D1.ReadAsync returns Unsupported. MAP ShiftAttendance -> required ShiftAssignment; source/schema lacks independent user workday start/end/current state and unscheduled workflow | Keep Unsupported independently checked by service. Minimal design proposal only: authoritative user workday identity, start/end/state and optional shift association, plus duty/release relation; decide representation in separate approved schema task | UNSUPPORTED | Approved model/mapping and workflow; no table/column/migration is created here |
| MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL | MODEL_MISSING | Missing authoritative receiver plus pending/completed acknowledgment | D1.ReadAsync returns Unsupported. InternalRequestEvent has actor_id/status/note, no designated receiver/ack mapping; no shift/workday acknowledgment model. Existing asset_handovers.to_user_id/handover_status/accepted_at is asset-specific and already checked by D2/STATE | Keep Unsupported. Minimal design proposal only: link to obligation/transfer, designated sender and receiver, mandatory/pending/ack evidence and timestamp with writer that transfers/releases duty atomically; do not infer acknowledgment from free text/history | UNSUPPORTED | Approved authoritative representation and acknowledgment/transfer workflow; existing asset handover does not fill this unrelated gap |

## Implemented state classifier evidence

The current source vocabulary is read from `Services/Maintenance/MaintenanceWorkService.Commands.cs`
(`RequireSynchronizedPair`) and `.Workflow.cs`, `Services/Assets/AssetService.Handover.cs`
(`FinalizeHandoverAsync`), `.Retirement.cs` (`RetireAssetAsync`), and
`Services/MaterialRequests/MaterialRequestService.Commands.cs` (`Command`, `Issue`) /
`MaterialRequestService.cs` (`Cycle`, `Latest`), together with MAP and canonical/guarded SQL.

- Maintenance: PENDING/IN_PROGRESS/ON_HOLD are current; COMPLETED/CLOSED/CANCELLED history
  retains the approved ReturnWork coordination gate. Corrective incident pairs must match
  OPEN/IN_PROGRESS/IN_PROGRESS/RESOLVED/CLOSED/CANCELLED respectively. Completion/review
  markers must match the demonstrated writer. Preventive work is allowed no Incident FK.
- Warehouse is_active is a nonnullable bit; active manager blocks.
- Asset custody IN_USE/UNDER_REPAIR blocks, RETIRED is history; other vocabulary is unresolved.
- Handover PENDING receiver blocks; ACCEPTED requires accepted_at, PENDING/REJECTED must
  not have that marker. Unknown casing remains unresolved despite CI SQL comparisons.
- MR requester DRAFT/NEEDS_INFORMATION blocks. Current request vocabulary has nine states:
  DRAFT, PENDING_APPROVAL, NEEDS_INFORMATION, APPROVED, PARTIALLY_ISSUED, FULLY_ISSUED,
  REMAINDER_CLOSED, REJECTED, CANCELLED. Legacy SUBMITTED/RETURNED/PARTIALLY_FULFILLED/
  COMPLETED retained by the schema patch does not define a current next actor and stays
  unresolved. Latest greatest approval revision controls the approver predicate; older
  PENDING history does not recreate responsibility. Latest parent/cycle mismatch, unknown
  cycle state or inconsistent submitted/resolved marker stays unresolved.

These reads use the caller DbContext/transaction, sequential async SQL with CancellationToken
and AsNoTracking. They do not acquire an independent connection, commit, alter data or relax
any writer gate. Exact positive SQL blocker predicates are unchanged.

## Actual before/after

| Receipt | Blockers | Unresolved | Unsupported | Actual internal service result |
|---|---:|---:|---:|---|
| Task53 baseline | 0 | 10 | 2 | ResponsibilitiesUnresolved |
| Task54, same compatible disposable trial, fresh valid fixture | 0 | 9 | 2 | ResponsibilitiesUnresolved |

The remaining nine identifiers are exactly the ten-row baseline above excluding
DEV2_UNKNOWN_STATE_COVERAGE. Both Unsupported identifiers remain unchanged. The service
denial preserves ACTIVE, rowversion, both sessions, credentials, roles, memberships and
audit; all new business test fixtures roll back. Success/revocation/audit commit and
post-mutation rollback remain BLOCKED, not PASS. HTTP remains unregistered.
