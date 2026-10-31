# Task58 current - executable existing-backend acceptance achieved, release still pending

See [DEV1 SQL/source acceptance](TASK58_DEV1_WRITER_ACCEPTANCE.md) and
[DEV2 method-level handoff](TASK58_DEV2_HANDOFF.md). Task58 is no longer documentation-only:
112 accepted SQL cases, 83 real waits; Create/Profile session-revocation race and four
cancellation cases repaired in DEV1. Relevant regression PASS: service 53, contracts 64,
ReturnWork 57, reauth/reset 165, credential participant 26, Profile HTTPS/SQL 329.
Solution build PASS (one NU1900 vulnerability-feed warning); dotnet test exit 0 but no test
project discovery in API-only solution. Failed baseline/earlier harness logs/receipts retained.

Remaining release blockers, with owners/actions:

1. **DEV1_WRITER_COORDINATION PENDING:** current account/auth subset has actual source/SQL
   acceptance; deferred business writers and deployment/import/HR/job coverage have not been
   accepted or excluded. Do not label nonexistent workflow tests PASS. SA57/D06/D07 remain draft.
2. **DEV2_INTEGRATED_SQL_ACCEPTANCE PENDING:** H01–H12 list exact corrections/tests for current
   methods. ReturnWork hotfix is integrated and scoped SQL PASS, not a pending merge. Missing
   broad Warehouse/Asset/MR/Inventory/Replenishment/other Maintenance operation evidence and
   server-side actor/session/permission/scope/SoD boundaries require DEV2 owner action.
3. **GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE PENDING:** actual current DEV1 User-first
   schedules pass; deliberate injected deadlocks prove recovery only. Need complete graph/
   tracked-resource reread/isolation/plan/deployed external-writer proof and owner review.
   Retained User-before-feature paths must be distinguished from unprotected feature-first
   paths; ordinary late actor read alone does not prove every protected method deadlocks.
4. **Runtime unresolved unchanged:** INTERNAL_REQUEST_CONTRACT, LINE_MANAGER_CONTRACT,
   OPERATIONAL_TASK_CONTRACT, SHIFT_CONTRACT, WORKDAY_CONTRACT. D05 policy is already APPROVED;
   Workday/interval representation and writer are missing, not another Working-alone decision.
5. **Unsupported unchanged:** UNSCHEDULED_WORKDAY_MODEL,
   MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL. Deferred/source absence is not zero duty; no gate
   bypass or exclusion is active. Employee unable to End needs separate operational exception.
6. **Full UC006 still BLOCKED:** no real successful Deactivate INACTIVE/version/session-revoke/
   audit commit, no Deactivate poststate LastAdmin or downstream audit rollback acceptance.
   Raw Reset LastAdmin rejection and foundation tests are independent evidence, not that branch.

No DEV2 source, production schema/RBAC catalog, Deactivate controller/DI, responsibility
predicate or release guard was changed. No HotelOpsDB access or Git mutation. Verdict PARTIAL:
current DEV1 safety defects fixed and executable evidence supplied; whole release not accepted.

# Task57 historical - existing backend first; no automatic exclusion

Ngày 2026-10-30, HEAD unchanged `4a187320643d0c82f232c6b9dd5a667b8401c280`.
Deliverables: [deployment proposal SA57](UC006_V1_DEPLOYMENT_SCOPE.md),
[deferred safety F01–F10](UC006_DEFERRED_WORKFLOWS.md), updated contract/gate matrices.

**D05 APPROVED**: employee must End Working Workday before Admin Deactivate even without
open interval; Ended alone does not release source assignments. No auto-end or invented
Admin forced-end; inability to End requires separate operational exception approval.
Workday representation/writer/tests remain missing, so WORKDAY_CONTRACT is still unresolved.
Approved delivery priority defers missing workflows to separate tasks, not safety exclusion.
D01–D04/D06–D07 remain without new formal approval; prior V1 approvals remain in force.

Actionable next work (can proceed without implementing all missing modules):

1. DEV1: finish per-operation account/auth shared-graph evidence E08; preserve scoped
   Activation/LastAdmin foundation receipts, do not call these business-writer/full UC006 PASS.
2. DEV2: actual Warehouse/Asset/MR and other existing E01–E07 integrated races/fault snapshots;
   actor/session/permission/scope/SoD gap on Maintenance/Incident/Asset/MR/Replenishment routes
   must be addressed by owner. Inventory already has server-derived actor/access gates.
   ReturnWork hotfix is integrated; this is not a pending hotfix merge dependency.
3. Joint graph review: global admin/Users universe, sorted participant Users, Catalog,
   feature locks and ordinary actor/FK/detail reads; no-owner/release paths included.
   Current source still has feature-first User reads; scoped ReturnWork proof is insufficient.
4. Deployment/DB owner: artifact/schema/patch/module/trigger/job/import/write-permission
   inventory and copy-data assessment, including legacy partially modeled duties. UNKNOWN
   remains until evidence exists; controller/source absence is not deployed no-writer proof.
5. Separate implementation tasks F01–F10 for request/task/shift/day/unscheduled/ack/JOB04/05/
   manager transfer. Model/current-owner/lifecycle/writer and actual SQL evidence required.
6. Human review of SA57/D06 if limited UC006 deployment scope is desired before those tasks;
   identify exact exclusions/restrictions/legacy handling. No exclusion active now. D07 release
   evidence/promotion boundary also remains proposal; cannot clear guard from build/query PASS.

Unchanged exact business Unresolved: INTERNAL_REQUEST_CONTRACT, LINE_MANAGER_CONTRACT,
OPERATIONAL_TASK_CONTRACT, SHIFT_CONTRACT, WORKDAY_CONTRACT.
Unsupported: UNSCHEDULED_WORKDAY_MODEL, MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL.
Pending release: DEV1_WRITER_COORDINATION, DEV2_INTEGRATED_SQL_ACCEPTANCE,
GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE. No new safety gate added.

**Verdict PARTIAL:** existing-backend acceptance can advance now using SA57 §3/§5; full
internal Deactivate mutation/session-revocation/audit/LastAdmin and HTTP remain BLOCKED.
Task57 docs only; no newly executed SQL/build/API acceptance claimed.

# Task56 historical - exact decisions and implementation work

See [UC006_V1_CONTRACT_MATRIX.md](UC006_V1_CONTRACT_MATRIX.md) for nine evidence fields per
workflow, scope recommendations, W01-W18 writer inventory, C01-C12 tests and release closure
criteria. Task56 changes documentation only; existing runtime/release gates remain unchanged.

Requirements were found, including original Word confirmation: GBR028 current work must be
reassigned/handed over; task submit is not accepted closure; UC119 ack transfers handler;
UC140 ack does not reassign items; UC139 permits unscheduled days; JOB05 creates tasks with
default assignees. These are not absent SRS requirements. Revised complete approved UC006
scope/lifecycle baseline and final Permission Matrix are still not found; existing developer
technical/provisional approvals and confirmed coordination decisions are preserved.

Remaining decisions/work, ready to assign:

1. **D01:** confirm UC120 detailed closure versus BF06 requester confirmation, then implement
   request lifecycle/closure/current cycle. No invented pending-requester approval.
2. **D02:** finish manager designation/history/employment release mapping and transfer writer;
   broad V1 current-management blocker is already approved, not being reopened for approval.
3. **Task verification/reopen/recurrence:** implement authoritative Pending Verification vs
   Closed evidence, UC105 return/UC106 reopen and JOB05 eligibility/coordinated writer paths.
4. **D03:** future roster boundary still needs a decision. **D05 approved by Task57:**
   Working even without interval must End first; implement Workday/interval separately;
   no forced-end/auto-end/auto-cancel by assumption.
5. **D04:** implement typed request/shift receiver and ack/current assignment binding under
   existing V1 mandatory-action rule; distinguish transfer from awareness acknowledgment.
6. **D06:** no exclusion applied. Workday/unscheduled/ack are IN_SCOPE_MISSING_IMPLEMENTATION;
   any deliberate scope cut needs versioned approval and proof for external/import/job paths.
7. **D07:** agree evidence/promotion sequence for the existing three release gates, without
   circularly requiring full Deactivate success before mutation is permitted. No source
   promotion/clear takes place in this task.

Exact unchanged clean-target runtime Unresolved: INTERNAL_REQUEST_CONTRACT,
LINE_MANAGER_CONTRACT, OPERATIONAL_TASK_CONTRACT, SHIFT_CONTRACT, WORKDAY_CONTRACT.
Unsupported: UNSCHEDULED_WORKDAY_MODEL, MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL.
Pending release: DEV1_WRITER_COORDINATION, DEV2_INTEGRATED_SQL_ACCEPTANCE,
GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE. Per-row malformed-data diagnostics may add more
codes; an empty fixture does not remove missing-model/contract or release requirements.

External deployed writers remain UNKNOWN; SQL Agent/import/direct-SQL inventory has not
been supplied or inspected. Missing source methods are not acceptance PASS. No DEV1 writer
violation with a newly verified approved business fix was found to implement in Task56.
All prior artifacts/source are retained. HTTP Deactivate and full mutation/session/audit/
LastAdmin acceptance remain BLOCKED. Verdict: PARTIAL; concrete contract proposal available,
not a claim that final approval or missing implementations now exist.

# Task55B historical - business uncertainty and release requirements separated

HEAD/branch remain 4a187320643d0c82f232c6b9dd5a667b8401c280 / feature/BE1-database-integration.
No Git synchronization or production-schema/RBAC change in Task55B. No HTTP Deactivate route.

Exact runtime Unresolved on a clean valid target:

- INTERNAL_REQUEST_CONTRACT
- LINE_MANAGER_CONTRACT
- OPERATIONAL_TASK_CONTRACT
- SHIFT_CONTRACT
- WORKDAY_CONTRACT

Exact runtime Unsupported: UNSCHEDULED_WORKDAY_MODEL,
MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL. Existing schema/SRS differ and no actual business
controller/service/writer supplies full next-actor/release semantics. New line-manager and
open-attendance positive evidence does not close those complete contracts. SQL markers and
historical creator/assignee references cannot substitute for missing acceptance/acknowledgment.

Separate mandatory release acceptance, all still PENDING:

- DEV1_WRITER_COORDINATION
- DEV2_INTEGRATED_SQL_ACCEPTANCE
- GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE

The immutable release guard runs before the first account mutation even if runtime review
becomes clear in a future implementation. It has no configuration/caller approval or injected
clear evaluator. Moving these three codes out of responsibility results is not gate removal.

No DEV1 current-work/manager/shift/request/handover writer implementation was found to repair
or test. Actual existing account activation writer races/faults are tested separately; they
cannot certify nonexistent assignment writers, external jobs/imports/direct SQL or complete
global feature/User compatibility. Keep DEV1 writer acceptance pending until that inventory
and actual implementation/SQL evidence exist.

Conditional V1 missing-model exclusion is proposed in GATE_RESOLUTION_MATRIX.md but NOT APPLIED.
Source absence is not proof that deployed external writers cannot create such obligations.
Need an authoritative deployed writer/access/schema inventory or approved model/workflow.

Actual clean Deactivate still returns ResponsibilitiesUnresolved. Success/revocation/audit
commit, downstream audit-failure rollback, LastAdmin service poststate and full successful
Deactivate-first remain BLOCKED. The LastAdmin foundation test is distinct from that branch.
Verdict: PARTIAL; runtime 0 Blockers / 5 Unresolved / 2 Unsupported + 3 pending release gates.

# Task55A historical - sync complete; full deactivation still BLOCKED

Official ReturnWork hotfix is integrated at HEAD 4a187320643d0c82f232c6b9dd5a667b8401c280.
It is no longer a missing-source dependency. Source review and actual scoped SQL race/fault
acceptance close MAINTENANCE_RETURN_WRITER_COORDINATION in the DEV1 wrapper. Concrete approved
predicates, unknown-state classification and Unsupported checks remain unchanged.

Final clean readiness: Blockers 0 / Unresolved 8 / Unsupported 2. Exact remaining Unresolved:

- DEV1_WRITER_COORDINATION
- DEV2_INTEGRATED_SQL_ACCEPTANCE
- GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE
- INTERNAL_REQUEST_CONTRACT
- LINE_MANAGER_CONTRACT
- OPERATIONAL_TASK_CONTRACT
- SHIFT_CONTRACT
- WORKDAY_CONTRACT

Unsupported remains UNSCHEDULED_WORKDAY_MODEL and MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL.
Current contracts/model dependencies are described in GATE_RESOLUTION_MATRIX.md. No new
business decision, schema, status or fake clear evaluator closes these requirements.

ReturnWork standalone and integrated writer-first/actual-denial-release races PASS; full
Deactivate-first with an actual INACTIVE commit and full UC-006 remain BLOCKED. The direct
INACTIVE fixture is labeled standalone and uses a separate owned User. Global compatibility
for feature-first actor reads and all DEV1/external responsibility writers remains pending.
Target state/version/credentials/roles/memberships/sessions/audit remain unchanged by all
blocked actual service attempts. HTTP Deactivate is still unregistered / NOT RUN.

# Task54 historical blockers - actual success attempt remains BLOCKED

`task54-gates.local.json` embeds the Task53 baseline and actual Task54 review/service attempt.
Blockers remain []; Unresolved decreases 10 -> 9; Unsupported remains 2. The state-coverage
implementation and all twelve baseline gates are detailed in
[GATE_RESOLUTION_MATRIX.md](GATE_RESOLUTION_MATRIX.md).

Exact remaining Unresolved:

- DEV1_WRITER_COORDINATION
- DEV2_INTEGRATED_SQL_ACCEPTANCE
- GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE
- INTERNAL_REQUEST_CONTRACT
- LINE_MANAGER_CONTRACT
- MAINTENANCE_RETURN_WRITER_COORDINATION
- OPERATIONAL_TASK_CONTRACT
- SHIFT_CONTRACT
- WORKDAY_CONTRACT

Exact remaining Unsupported: UNSCHEDULED_WORKDAY_MODEL,
MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL. The latter two require an authoritative model,
not an empty-table test. Existing source cannot prove release from the incomplete contracts.
DEV2 ReturnWorkAsync remains unchanged without prepareOwners; hotfix and full actual writer
races are pending. Global Users TABLOCKX versus feature-first actor reads is still unaccepted.

DEV2_UNKNOWN_STATE_COVERAGE is resolved for known consistent modeled rows; malformed/legacy
rows still return it plus precise state diagnostics. It does not close either writer gate.
No bypass was used. The blocked attempt preserves account/version/sessions/credential/access
graphs/audit. Actual successful mutation, revoke/audit commit, downstream audit rollback,
LastAdmin poststate and concurrent successful deactivations remain BLOCKED.

The Task53 startup regression is resolved in the test harness: Windows Error Reporting
delayed intentional fatal child exit. Test-only inherited error mode avoids that delay;
production startup and 45-second bound are unchanged. New full sequential startup suite
PASS 377 checks (`task54-startup.local.json`), including three negative probes and Profile.
The original Task53 FAIL log/receipt remains historical evidence. HTTP Deactivate NOT RUN;
no controller, registration or enablement is added. Verdict: PARTIAL.

# Task53 historical blockers — actual success attempt BLOCKED

task53-readiness.local.json records an actual internal service attempt with active,
employment-active, nondeleted target; supported durable credential; two sessions; existing
role/membership; real SQL actor session + USER_MANAGE/ALL; another eligible global admin;
zero target references in all 15 existing modeled responsibility groups. Known Blockers=[]
but the following concrete Unresolved codes prevented mutation (all remain mandatory):

| Gate | Exact requirement to close it |
|---|---|
| OPERATIONAL_TASK_CONTRACT | SQL COMPLETED vs SRS Pending Verification/Closed/reopen and mandatory task handover mapping; positive TODO/IN_PROGRESS/BLOCKED assignee predicates now implemented/tested |
| INTERNAL_REQUEST_CONTRACT | Exact requester/receiver next actions and structured mandatory handover receiver acknowledgment; IN_PROGRESS assignee evidence now implemented/tested |
| SHIFT_CONTRACT | Allowed statuses and current/resolved shift responsibility including checkout/handover; defaults ASSIGNED/PLANNED/NOT_STARTED do not prove a full lifecycle |
| WORKDAY_CONTRACT | Existing representation of UC139 Unscheduled Working/Ended workdays; attendance requires a ShiftAssignment FK and cannot represent this by assumption |
| LINE_MANAGER_CONTRACT | Current obligation/release semantics for users.manager_user_id, beyond separately proven active departments.manager_user_id |
| DEV1_WRITER_COORDINATION | Audit/coordinate actual responsibility writers/imports/jobs; missing source does not certify absent duties |
| MAINTENANCE_RETURN_WRITER_COORDINATION | DEV2 ReturnWork hotfix still absent from local source; integrate only through an explicitly authorized later Git task |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | Full actual writer/service races after source gaps close |
| DEV2_UNKNOWN_STATE_COVERAGE | Exhaustive unknown/inconsistent DEV2 responsibility classification/fixtures |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | Resolve broad Users TABLOCKX versus feature-first actor reads with actual writer evidence |

Cleaning ACTIVE assignment with SQL COMPLETED parent additionally yields CLEANING_TERMINAL_MAPPING
when such a row exists. Its absence in this fixture is not approval of a terminal-state mapping.
Workday/acknowledgment missing models remain unresolved even for a target with no modeled rows.
Neither V1 provisional approval nor an empty trial permits removing these gates.

Following the developer's Task53 clarification, missing models are additionally classified
explicitly as Unsupported: UNSCHEDULED_WORKDAY_MODEL and MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL.
The service checks Unsupported independently of Unresolved, so future changes to a contract
list cannot silently certify a missing model. BLOCKED describes the operation/verification
outcome; it is distinct from a proven responsibility Blocker and from an Unsupported model.
No further approval question about these three mappings is pending; a source/schema contract
decision is required before implementation can close them.

Success, actual Deactivate session revocation/audit commit, post-mutation audit-failure rollback,
last-admin rejection and concurrent successful deactivations remain BLOCKED at responsibility
review. They were not bypassed, mocked clear, or simulated by direct account-status writes.
HTTP Deactivate remains unregistered. Exact post-hotfix ReturnWork acceptance is already in
INTEGRATION_DEPENDENCIES.md; Task53 does not repeat that audit or modify DEV2.

Regression issue: the existing full Startup Schema Gate runner timed out in its negative-startup
process/HTTP probe after validator classification passed. That full runner is FAIL in Task53;
its log is preserved and the timeout cause remains unverified. Dedicated Profile regression
passes separately. Do not treat the compatible trial startup checks as full negative-startup acceptance.

# Task52 historical blockers — PARTIAL; HTTP BLOCKED

Trial prerequisite is restored for NEW verification runs: approved canonical setup + original
Task26 guarded rowversion patch passed 26 fresh/upgrade checks, and fresh Profile passed 329.
Do not reuse the old Task51 trial 181431_4ef90204: its existing binary(8) row_version is
incompatible and unowned. The original forward patch correctly rejected it with 51972;
no column replacement or schema invention was performed. New patched trials are recorded
in the Task52 schema receipt and VERIFICATION_RESULTS.md.

Still required before integration readiness:

1. DEV2 ReturnWork hotfix and actual writer coordination tests described precisely in
   INTEGRATION_DEPENDENCIES.md. No DEV2 source change or hotfix merge is part of Task52.
2. Exact unresolved DEV1 responsibility contracts/writers and DEV2 unknown-state coverage.
3. Resolve global Users TABLOCKX versus feature-first actor-read ordering with actual writers.
4. Real successful Deactivate, LastAdmin poststate, session revocation and audit failure rollback
   through the complete operation. Current concrete predicates always prevent reaching them;
   these cases remain BLOCKED, despite passing foundation tests and rejection-path verification.
5. Full SQL authority/writer races and HTTP contract acceptance. BRANCH/OWN composition tests
   use explicitly synthetic grants with real SQL session/memberships; catalog has no approved
   USER_MANAGE BRANCH/OWN grants and was not changed. These are not full SQL-grant acceptance.

No Deactivate route/DI/enablement flag is added. Missing HTTP If-Match/428, strong header grammar,
status mapping, CSRF/no-store and Deactivate HTTPS remain NOT RUN / BLOCKED while route is absent.
Internal null/invalid-length version and strict reason checks do not certify HTTP parsing.

# Task51 historical remaining blockers — endpoint BLOCKED

1. DEV2 must coordinate Maintenance ReturnWorkAsync: discover current assignee, lock Users
   before Incident/Work, validate active account/employment/nondeleted state, reread and reject
   participant drift. COMPLETED work itself is historical; returning it creates new responsibility.
2. Review feature-first actor reads against GlobalAdminSnapshot's Users TABLOCKX. Establish
   one compatible protocol and verify both orders with actual SQL blocking/deadlock evidence.
3. Supply exact executable DEV1 contracts for Shift/Workday/Operational Task/Internal Request,
   mandatory handover acknowledgment, line manager and ambiguous completed cleaning parent.
   Implement/audit all related writer paths, including jobs/imports/direct SQL participants.
4. Add exhaustive DEV2 unknown/inconsistent-state classification and SQL fixtures for all six
   predicates, including latest-cycle historical exclusions and workflow reactivation paths.
5. After these source gates close, execute real Deactivate success, last eligible admin,
   revoke-all sessions, audit-failure rollback, no unintended changes, scope/OWN/cross-branch,
   and races A–G. Do not replace the concrete evaluator or clear gates in test to fake acceptance.
6. Only after acceptance passes add the strict HTTPS/CSRF/no-store PATCH endpoint and validate
   missing/stale If-Match, response codes and out-of-scope nondisclosure over actual HTTP.

Task51 prepared the service transaction but success/revocation/audit/poststate execution is
unverified because the concrete review cannot clear current unresolved contracts. SQL rejection
paths are useful evidence, not full UC-006 acceptance. No HTTP route/DI/flag was added.

# Historical remaining blockers

## Task48 current boundary

V1 intent for all responsibilities is now PROVISIONAL, approved by DEV1 owner.
Missing exact schema/workflow semantics remain UNSUPPORTED/UNRESOLVED; V1 approval
does not invent statuses or designate a next actor. See V1_BUSINESS_CONTRACT.md.
The older phrase "unapproved Shift/Workday/Internal Request contracts" below refers
to missing exact executable contracts, not absence of the new high-level V1 intent.

Full Deactivate remains BLOCKED by lifecycle/actor mappings, missing writer coordination,
DEV2 source handoff and atomic transaction/integrated race verification. Internal package
can be reviewed independently because no runtime endpoint/writer is wired. Task46 DEV2
helper is excluded pending SQL coverage. Remaining source is limited by FILE_MANIFEST.txt.

## Task47 delta — 2026-10-29

Post-approval update: approved cleaning/open rework/designated inspection/assigned
defect/active department predicates are now implemented and SQL-tested. The statement
below about no approval describes the first Task47 pass only. Exact APPROVED /
IMPLEMENTED / UNRESOLVED status is in DEV1_PREDICATE_DECISIONS.md.
Remaining business blockers: ACTIVE cleaning with ambiguous COMPLETED parent,
Operational Task review/reopen mapping, line-manager duties, and explicitly unapproved
Shift/Workday/Internal Request contracts. Writer coordination and full Deactivate
transaction/race integration remain BLOCKED. No endpoint is added.

Implemented only the known executing-cleaning positive subset from the existing
ownership contract. This reduces executable predicate work but does not close the
full cleaning gate. See DEV1_PREDICATE_DECISIONS.md for exact approval questions,
DEV1_WRITER_LOCK_MATRIX.md for present/absent writers, and DEV2_INTEGRATION_CHECKLIST.md
for handoff acceptance. No business approval response was available when implementing
this subset; wider proposed predicates are not active. HTTP remains closed in Task47.

The Task46 blockers below remain applicable; its proposed HTTP409 is still a proposal.

1. Integrate DEV2 coordinated writers through the team's approved Git workflow.
   Task46 does not authorize copying/cherry-picking/merging them here.
2. Approve DEV1 lifecycle mappings and predicates enumerated in OPEN_WORK_PREDICATES,
   including legacy contradictions, shift boundaries, requester/manager obligations
   and acknowledged internal-request handover representation.
3. Implement/audit all relevant DEV1 writers and external/import paths against the
   users-first eligibility protocol, including exact employment eligibility.
4. Implement the atomic Deactivate transaction after these contracts are sufficient:
   SQL session/ALL-BRANCH authority, self/OWN denial, strong If-Match, strict reason,
   global-first serialization, current responsibilities, LastAdmin poststate,
   account/version mutation, session revocation and audit rollback.
5. Execute approved predicate fixtures, required concurrency/fault cases and all
   regressions exclusively on guarded disposable SQL databases. Collect real SQL
   lock-manager evidence in both race orders and resolve reverse lock paths.
6. Only then register HTTP Deactivate. Proposed responsibility response: HTTP409,
   code OPEN_RESPONSIBILITIES, without cross-scope resource details. This is a
   proposed future contract, not a currently exposed response.

Final verdict: PARTIAL independent predicate implementation; integrated Deactivate
BLOCKED. Exact unresolved decisions require developer confirmation before mutation
logic, per AGENTS.md; none are inferred from missing source or schema defaults.
