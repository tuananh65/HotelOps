# Task55B current - runtime data versus release acceptance

IntegratedDeactivationResponsibilityQueries now returns only runtime Blockers, Unresolved
contracts/data and Unsupported models. It no longer places DEV1_WRITER_COORDINATION,
DEV2_INTEGRATED_SQL_ACCEPTANCE or GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE in a target's
business result. Those same three pending requirements live in the immutable
UserAccountDeactivationReleaseAcceptance guard, called immediately before the service's
first mutation. Clear runtime data cannot approve release. No approval/configuration switch
or test evaluator was added; the guard independently denies pending release.

New conservative V1 positive evidence in ApprovedDev1ResponsibilityQueries.ReadAsync:

| Code | Exact SQL evidence | Safety limit |
|---|---|---|
| LINE_MANAGER_DESIGNATION | users.manager_user_id=target; deleted_at NULL; employment_status ACTIVE | INACTIVE login does not end employment. Deleted references are not current positive evidence. Full transfer/nonactive-employment release semantics remain LINE_MANAGER_CONTRACT |
| SHIFT_OPEN_ATTENDANCE | target shift assignment ASSIGNED; existing shift; actual check-in, no checkout; valid nonfuture/ordered timestamps | No new status vocabulary, current Workday or terminal-state mapping inferred. Scheduled end alone cannot release duty; SHIFT_CONTRACT remains |

Shift missing parent, reversed schedule/timestamps, checkout without check-in or future actual
timestamps add SHIFT_ATTENDANCE_DATA_INCONSISTENCY. Open attendance with unmapped assignment
vocabulary adds SHIFT_OPEN_ATTENDANCE_MAPPING. Closed timestamp history does not prove
handover acknowledged. The full shift/attendance status contract remains unresolved, including
default NOT_STARTED rows with actual timestamps; open timestamp evidence is a conservative
positive record, not certification of a consistent complete workflow.
Manager employment vocabulary is classified ordinally after actual SQL reads; unmapped
nondeleted subordinate values add LINE_MANAGER_EMPLOYMENT_MAPPING. CI SQL casing equivalence
does not certify ACTIVE or a historical-release mapping.

Existing Operational/IR assignee predicates and cleaning/rework/inspection/defect/department
predicates remain. COMPLETED ambiguity, missing receiver acknowledgment and unscheduled
Workday models are not cleared by empty data. Source has no actual controllers/services/jobs
for the five business contracts, so complete lifecycle release cannot be reconstructed safely.
GATE_RESOLUTION_MATRIX.md records exact current evidence, V1 proposals and dependencies.

Clean runtime review now has five Unresolved (Internal Request, Line Manager, Operational,
Shift, Workday) and the same two Unsupported. The three release requirements remain pending
independently. Missing-model V1 exclusions are proposed conditionally but NOT APPLIED: external
production writers/imports/jobs cannot be ruled out from checkout/trial evidence alone.

# Task55A historical: ReturnWork coordinator integrated and verified

HEAD now includes official merge 4a187320643d0c82f232c6b9dd5a667b8401c280. Actual SQL confirms
ReturnWork retained User -> Incident -> Work locks, eligibility after waiting, authoritative
reload and stale participant/state/linkage rejection. The missing Return coordinator gate
is closed after that evidence. Exact current responsibilities and the Task54 DEV2 unknown/
inconsistent-state classifier are unchanged. Full DEV2/global writer acceptance stays gated.

Actual Return commit creates MAINTENANCE_ASSIGNEE; the real Deactivate waits then denies with
ResponsibilitiesUnresolved because other gates remain. Clean readiness decreases 9 -> 8
Unresolved, with 0 Blockers / 2 Unsupported. See task55a-returnwork-accepted.local.json and
INTEGRATION_DEPENDENCIES.md for scoped PASS versus full Deactivate-first/UC-006 BLOCKED.

# Task54 historical: exact state coverage, remaining gates retained

The exact ten Unresolved and two Unsupported identifiers from the Task53 final receipt,
their source methods, SQL fields, classifications and dependencies are in
[GATE_RESOLUTION_MATRIX.md](GATE_RESOLUTION_MATRIX.md). Task54 resolves the implementable
DEV2_UNKNOWN_STATE_COVERAGE gate for modeled, known, consistent data. It is no longer an
unconditional integration constant. `Dev2ResponsibilityStateQueries.ReadUnresolvedAsync`
executes SQL in the caller transaction and restores that code plus precise diagnostics
for unknown/legacy vocabulary, invalid lifecycle markers and latest-cycle/parent conflicts.
The existing six exact DEV2 positive predicates and protected Task46 helper are unchanged.

Task54 real SQL: 29 new positive/negative state cases, including current/history Maintenance,
corrective Incident pairs, Warehouse manager, Asset custody, receiver handover, legacy MR,
latest approval revision and conflicting/resolved markers. Fixtures roll back. Combined
gate/readiness runner PASS 62 checks, `task54-gates.local.json`; actual internal service
result remains ResponsibilitiesUnresolved: Blockers 0, Unresolved 9, Unsupported 2 (before
0/10/2). Invalid data never silently becomes NoBlocker. Writer acceptance remains separate.

No new evidence closes Internal Request, Operational Task, Shift, line-manager, unscheduled
Workday or mandatory acknowledgment contracts/models. The concrete V1 proposals and missing
data are specified per gate in the matrix; the approved provisional policy requires keeping
these uncertainties. Cleaning COMPLETED likewise remains conditionally unresolved when
an ACTIVE assignment exists. No status, model, writer behavior or schema was invented.

# Task53 V1 predicate implementation (historical; preserved)

ApprovedDev1ResponsibilityQueries now additionally reads both existing designated-assignee
models under the caller transaction; no schema, status, role or workflow was added.

| Responsibility | Implemented exact evidence | Still not a clear eligibility decision |
|---|---|---|
| Operational Task | assignee_id = target, assignment_status ACTIVE, ended_at NULL, parent TODO/IN_PROGRESS/BLOCKED -> OPERATIONAL_TASK_ASSIGNEE | ACTIVE with COMPLETED parent -> OPERATIONAL_TASK_TERMINAL_MAPPING; status/end contradictions and unknown/missing parent -> unresolved. Pending Verification/Closed/reopen/handover mapping remains missing |
| Internal Request | assignee_id = target, status ACTIVE, ended_at NULL, parent IN_PROGRESS -> INTERNAL_REQUEST_ASSIGNEE | Other active parent states -> INTERNAL_REQUEST_NEXT_ACTION_MAPPING. Requester next-action and mandatory receiver acknowledgment are not inferred from actor/event/creator FKs |

Known COMPLETED/REASSIGNED/CANCELLED assignment history with a valid EndedAt is not
current assignee evidence; it never clears global contract gates. Unknown vocabularies,
end-before-assigned, ACTIVE+end and nonactive-without-end fail closed. Existing cleaning,
rework, inspection, defect and active department manager predicates are preserved.

Task53 SQL tests: 9 new predicate assertions in task53-readiness.local.json / readiness log.
The OPERATIONAL_TASK_CONTRACT and INTERNAL_REQUEST_CONTRACT unresolved gates remain:
positive evidence is now executable but complete lifecycle/next-actor coverage is not proven.
Shift/Workday, line manager, handover acknowledgment and writer coordination remain unresolved.

Developer's Task53 clarification confirms no formal mapping exists for unscheduled Workday,
receiver acknowledgment or safe COMPLETED release. The review now separately exposes
Unsupported=[UNSCHEDULED_WORKDAY_MODEL, MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL]. These
are absent authoritative representations, not zero-row queries. Unresolved retains ambiguous
contracts/data and pending coordination; Blockers contains proven current SQL responsibilities.
The integrated wrapper preserves all three lists; the service denies either Unsupported or
Unresolved through its existing ResponsibilitiesUnresolved result. No new database status or
HTTP contract is introduced. The developer requested no repeated clarification without new evidence.

# Task51 historical predicate integration

The new integrated review calls ApprovedDev1ResponsibilityQueries and the unchanged
Task46 DeactivationResponsibilityQueries in the caller SQL transaction. It unions blockers,
retains every DEV1 unresolved code, and adds unresolved gates for Maintenance ReturnWork,
DEV2 integrated SQL acceptance, unknown-state coverage and global Users/feature lock order.
Unresolved takes precedence over known blockers and always prevents mutation.

The DEV2 helper covers exact current Maintenance, active Warehouse manager, operational
Asset custody, PENDING receiver, DRAFT/NEEDS_INFORMATION requester and latest effective
PENDING cycle under PENDING_APPROVAL parent. Older pending approvers cannot override a newer
cycle. Unknown/contradictory DEV2 states have no comprehensive classifier yet; empty exact
predicates MUST NOT certify clear responsibility. No untracked helper was overwritten.

DEV1 cleaning/rework/inspection/defect/department predicates remain provisional; Shift,
Workday, Internal Request (including mandatory acknowledgment), Operational Task and line
manager exact contracts remain unresolved. No new states or actors were invented.

Historical predicate notes follow.

# Open-work predicates

Task48 packaging: the DEV2 query helper described below is a local Task46 artifact,
excluded from this commit because SQL fixture coverage is pending. This document retains
the approved DEV2 contract for future integration. Current packaged DEV1 predicates are
defined in DEV1_PREDICATE_DECISIONS.md; V1 intent is in V1_BUSINESS_CONTRACT.md.
References to the earlier Task40 matrix are historical external context, not a package
dependency; current decision and writer tables here stand alone.

Implemented in DeactivationResponsibilityQueries.ReadApprovedDev2BlockersAsync:

| Code | Exact predicate |
|---|---|
| MAINTENANCE_ASSIGNEE | maintenance_works.assignee_id = target; status PENDING/IN_PROGRESS/ON_HOLD |
| WAREHOUSE_MANAGER | warehouses.manager_user_id = target; is_active |
| ASSET_CUSTODIAN | assets.custodian_user_id = target; lifecycle_status IN_USE/UNDER_REPAIR |
| ASSET_RECEIVER | asset_handovers.to_user_id = target; handover_status PENDING |
| MATERIAL_REQUEST_REQUESTER | material_requests.requester_id = target; status DRAFT/NEEDS_INFORMATION |
| MATERIAL_REQUEST_APPROVER | assigned_approver_id = target; cycle PENDING; parent PENDING_APPROVAL; no greater revision for same request |

MaterialRequestService.Latest/Cycle select greatest RevisionNo. HotelOpsDbContext
maps unique UQ_mr_cycles_revision (RequestId, RevisionNo). The predicate searches
for newer cycles regardless of status, excluding historical/superseded pending rows.
No branch filter is applied to dependencies: a target may retain duties elsewhere.
The helper is internal and exposes no resource identifiers or API response.

Historical completed/closed/cancelled maintenance, inactive warehouses, RETIRED custody,
ACCEPTED/REJECTED handovers and superseded approvals do not match these predicates.

DEV1: see ../Dev1Deactivation/CURRENT_RESPONSIBILITY_MATRIX.md for exact columns,
schema vocabulary and missing writers. This task does not approve unresolved semantics:
cleaning assignment end/status precedence and rework, inspection conclusion/defects,
task review/reopen, internal requester/assignee/acknowledged handover, shift status/time
boundaries and attendance, active department stewardship and subordinate manager duties.
The absent writers do not prove legacy data is safe. No empty DEV1 predicate is installed.
