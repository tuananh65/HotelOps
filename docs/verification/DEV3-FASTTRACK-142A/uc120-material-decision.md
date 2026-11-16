# UC120 material completion decision

2026-11-16: DEV3 approved both FULLY_ISSUED and REMAINDER_CLOSED as valid
terminal material outcomes. Other states remain blockers for mandatory links.

The existing Complete read/command now evaluates material evidence in the caller's
Serializable transaction. The evidence reader verifies same-branch request,
department and warehouse; latest independent approved cycle and exact line snapshot;
fulfillment-to-posted-ISSUE correlation and cumulative quantities. Full issue requires
all approved quantities fulfilled and matching completion time. Remainder closure
requires issued quantity plus open remainder, assigned approver, reason, timestamp
and matching existing business audit. Missing schema/history fails closed.

The existing fulfillment reconciliation validator is reused without changing its
behavior. No new permissions, mutation endpoints, schema or database writes were added.
IR_VIEW, mapped IR_PROCESS, CSRF/ETag, closure/audit/outbox transaction stay intact.
Material identifiers/details are not disclosed by the completion prerequisite.

Build: `dotnet build HotelOps.slnx --no-restore` PASS, zero warnings/errors.
Tests, HTTP and business SQL acceptance: NOT RUN per user instruction.
Read-only recovery metadata inspection found the legacy material schema lacks the
new approval/remainder capability; this database cannot verify these paths yet.
No shared database mutation. Whole UC120 remains PARTIAL: independently authorized
linked-material display, legacy reconciliation and Human QA remain outstanding.

The request history now refreshes after confirmed handover Submit/Acknowledge/Reject,
linking a task/cleaning task/incident, or creating a linked task. Refresh invalidates
the old timeline and unmounts its completion panel immediately, then reloads current
authority, responsibility, linked work and completion eligibility. A confirmation is
retained only for the same request/session after authorized history reload. Existing
task assignment navigation, CSRF, ETag and independent target authorization remain.

Frontend `npm.cmd run build` (TypeScript + Vite): PASS. Vite still warns about ignored
React Router `use client` directives and a chunk above 500 kB. No test suites, browser,
API or SQL business checks run for this refresh change, as requested. No backend or
database changes. This integration repair does not promote UC120 to complete.

## Approved MR_VIEW and linked-material read

DEV3 subsequently approved independent `MR_VIEW`. The new read-only
`GET /api/internal-requests/{id}/linked-materials?page=1` and canonical history panel
now list linked material codes, purpose, stored status, mandatory flag and completion
evidence outcome. They require current HTTPS/session/IR_VIEW for the source and an
independent live MR_VIEW grant for every returned material. Existing grant rules
apply: branch-bound ALL stays branch-bound; global ALL still intersects the source
request branch; OWN is limited to the persisted material RequesterId. No actor role
bypass, IR_VIEW-to-MR_VIEW inference, approval/fulfillment grant inference or role seed.
Same-branch department/warehouse provenance is required. Counts and page rows are
rechecked before disclosure within the Serializable transaction. Hidden targets and
their counts are not returned. An empty filtered list cannot authorize closure.

Missing material lifecycle schema returns unconfirmed evidence for visible mandatory
rows; historical statuses are not converted to approved terminal states. No generic
demo mutation/detail route is enabled. Material link creation and legacy provenance
reconciliation remain separate from this read endpoint.

Direct validation: backend build PASS (zero warnings/errors); frontend TypeScript +
Vite build PASS (existing React Router directive and >500 kB chunk warnings remain).
Business tests, HTTP and browser acceptance NOT RUN per instruction. No UC promoted.

`scripts/database/development/MaterialRequestViewRecovery.sql` was run in default
rollback mode, independently confirmed zero MR_VIEW rows, then installed twice
idempotently on `meita\MSSQLSERVER01`, `HotelOps_DEV3_142A_Recovery_2b221f40`, GUID
`5270D07C-7C27-477B-A60E-86485ADD7FAD`. Final catalog has one MR_VIEW entry with
INVENTORY/material_request/VIEW; role_permissions remains 140 rows and MR_VIEW grants
remain zero. Script SHA256:
`619DFC828C2559AAE9964EDA9B7245BB9A5CFF7D4DCF8C3442432FDF6332CD30`.
This is catalog installation/rollback evidence, not business acceptance. No shared DB
write, production seed, generated entity change or automatic role grant.

Read-only recovery inspection also confirms legacy material status CHECK values
(SUBMITTED/RETURNED/PARTIALLY_FULFILLED/COMPLETED) differ from current source and
approval cycle/snapshot storage is absent. Thirteen requests, twelve approvals and
fourteen lines were preserved. Do not relabel them or invent cycle/issue receipts.
The additive lifecycle migration below resolves the storage gap. Explicit legacy
reconciliation and workflow acceptance are still needed; installation alone does not
prove that existing requests meet the approved terminal completion paths.

## Material link workflow and recovery compatibility

UC118 Link Existing Material is now connected from the canonical history panel to
the HTTPS API and transactional link/audit writer. Source must be Accepted/In Progress,
readable with IR_VIEW, and manageable through current exact receiving-department
DEPT_MANAGER/IR_PROCESS authority. Each target separately requires MR_VIEW and matching
branch provenance. Candidates use the current source's open states only; legacy
SUBMITTED/RETURNED/PARTIALLY_FULFILLED are not silently remapped. Selection of mandatory
or optional is explicit. Strong If-Match, duplicate/state recheck, current session,
final source/target reauthorization, existing CSRF and Serializable transaction remain.
The target material request is not mutated, fulfilled or approved. After confirmed
save the UI refreshes history/links and completion eligibility. No automatic retry.

`MaterialLifecycleRecovery.sql` adds the columns, approval-cycle/snapshot tables,
restrictive FKs and unique indexes already mapped by the Database First source. The
status CHECK is replaced atomically to retain every legacy value while admitting the
current source values. No legacy row is relabeled; no cycle, decision or posted-issue
correlation is inferred. No EF entities/mappings or role grants changed.

The first rollback probe failed on sqlcmd's QUOTED_IDENTIFIER setting; the transaction
rolled back. Explicit filtered-index SET options fixed it. The final script passed
default rollback with independent absence checks, then installed/replayed successfully
on the same identity-guarded disposable recovery clone. All 13 new FKs are checked for
exact endpoints, trust and no cascading action; column types and unique index keys
are validated on replay. Final script SHA256:
`064BD516A21FE36DAC2164E511C4A0C6F64FC910A8A42507AF3AE3D098297CCB`.

Original business-field SHA256 fingerprints were identical before/after installation:

| Table | SHA256 |
| --- | --- |
| material_requests | 0EA3D340A5F3133497217CD08F96F1E569BC4B358F86E8EFE8DF533B2040366B |
| material_request_lines | 15774A5BFC22DBC7DC2A4B2312CADC71E819C76237D9E4A5A596ACEB81337EA3 |
| material_request_approvals | 2453CF7F1DEBACC116DD4B7CF1C65ADC47CD8C0420D59D2E82E98F322495604F |
| material_fulfillment_lines | D8964FDB02547F7541857DFC46CABD144154F9A9B402F142DB533795268FB15E |

The 12 legacy decisions still have NULL cycle binding; new cycles/snapshots are empty.
13 request statuses remain 7 APPROVED, 3 COMPLETED, 1 DRAFT, 1 REJECTED, 1 SUBMITTED.
Backend build PASS 0 warnings/errors; TypeScript/Vite build PASS with the existing
directive/chunk warnings. Business tests/API/browser NOT RUN per instruction. No
whole-UC completion claim; current runtime still needs other FE13 capabilities and
approved grants, and legacy proof gaps continue to fail closed.

## Approved material approver routing — source implementation

The approved GBR-111 decision now guards approval-cycle creation and current
assigned-approver decisions, including a final check before transaction commit.
An approver must be an eligible, independent DEPT_MANAGER with a current exact
user/branch/department mapping and MR_APPROVE_L1 for the requester's department.
When the requester manages that department, routing requires the issuing warehouse's
department and MR_APPROVE_FALLBACK instead. MR_APPROVE_L2, IR_PROCESS and MR_VIEW
do not replace these grants. Missing capability, mapping or grant rejects the write.
No role IDs, automatic grants or inferred ManagerUserId authority were introduced.

Linked material creation now has a source-side transaction participant: validate
IR_VIEW/current processing authority, lock the source, compare its version, then
persist the new material link, source revision and audit within the material writer's
transaction. It does not enable target admission. Submitting an unchanged draft
also creates its approval cycle rather than returning early on content equality.

Production material mutation admission and the create/approver-selection UI remain
outstanding; these changes alone do not complete UC091/094/095/118/120. No database
mutation or business/API/browser tests were run for this source batch.

## Material Create action admission

Added the production source contract for POST /api/material-requests only. Current
HTTPS cookie/SQL session, active user and branch access, an approved UC091 role's
MR_CREATE grant, and independent MR_VIEW are required. OWN grants resolve against
the authenticated creation context; caller ActorUserId cannot create authority.
Optional incident links also require current incident read scope.

The contract opts into an outer Serializable action transaction through response
authorization. Service save/commit calls lease that transaction; response failures
roll back the creation, link and audit. Final disclosure rechecks the mapped
approver and linked IR authority. Other contracts retain their existing behavior.
The material lifecycle capability is required; missing schema fails closed. Empty
fulfillment history no longer queries unrelated return storage for a new request.
Source-link failures now return their intended 403/404/409/412/503 HTTP status.

Backend build PASS, 0 warnings/errors. CSRF middleware remains unchanged. No SQL
mutations or API/business/browser tests were executed for this batch. UC091 still
needs its UI, approver/catalog lookup and durable notification; other material
mutation/read action admissions remain separate outstanding work. No UC promoted.

## Approval notification delivery source

The persisted approval cycle now supplies durable notification intent without a new
table: only committed, latest PENDING cycles on PENDING_APPROVAL requests are scanned.
Drafts, rolled-back cycles and superseded/resolved cycles do not generate delivery.
The worker uses a fresh context per cycle, ordered user locks and a Serializable
transaction. It checks exact assigned-approver mapping/approval grant and independent
MR_VIEW before delivery and again before commit. Notification provenance and recipient
receipt conflicts fail closed; retries reuse the same cycle source/recipient receipt.
The existing finite NotificationCenter retention bounds scans and delivery.

Registered MaterialApprovalNotifications options (Enabled defaults to false,
PollSeconds defaults to 30) and the worker; it was not enabled or executed against SQL.
No schema, grants or database data changed. Backend build PASS, 0 warnings/errors;
business/API/browser tests NOT RUN per instruction. Notification source navigation,
material detail admission and the creation UI remain outstanding; no UC promoted.

## Material read and notification navigation

GET list/detail now use independent MR_VIEW, a current HTTPS SQL session and
Serializable scoped reads. List requires an explicit branch and filters OWN to
RequesterId. The final disclosure scope must match the scope used to select the
response, including when a broader grant expires while OWN remains valid.
Detail validates approval-line/request and fulfillment/document branch provenance;
missing legacy proof fails closed rather than exposing unrelated stock records.
Raw audit history, inventory availability and other material actions are not admitted.

Canonical operations routes now expose material list/detail alongside incident reads,
with an explicit admitted-action menu. Linked material rows open this detail route.
Approval notification source resolution rechecks receipt/cycle provenance, current
mapped approval authority, MR_VIEW and current session before returning that route.
Read-only screens no longer show the fulfillment mutation shortcut.

The first backend build caught a nullable-pattern long.Value error, corrected before
delivery. Final backend build PASS (0 warnings/errors), TypeScript PASS, Vite PASS
(existing React Router use-client and >500 kB chunk warnings). SQL/API/browser and
business tests NOT RUN per instruction. No database mutation. Creation UI/catalog,
remaining lifecycle admission and full UC092 history acceptance remain open; no UC
promoted on source/build evidence alone.

## Create form and approved approver picker

Canonical Material Requests now exposes Create alongside the admitted list/detail
actions. Its Submit selector uses a typed approver dropdown from the new scoped
GET /api/material-requests/creation-approvers lookup. Changing branch/warehouse
clears the selected approver. Session changes or lookup errors discard the list;
an empty/unavailable list cannot satisfy the browser's required Submit selection.
The existing transport supplies the session actor and CSRF token; the server
continues to independently authorize every command and revalidate approver mapping.

Lookup requires the same MR_CREATE + MR_VIEW/current session/branch authority as
creation. It returns only active independent DEPT_MANAGER candidates with current
exact department mapping and the approved L1/fallback grant. Before disclosure it
recomputes the routing and candidate list, rejecting expired or changed authority.
No client-selected actor, role ID or inferred department-manager relationship is used.

Backend build PASS (0 warnings/errors), frontend typecheck/build PASS with unchanged
React Router/chunk warnings. No SQL writes or business/API/browser tests. The form
still needs business catalog selectors in place of the inherited numeric reference
fields, linked-create UI integration and Human QA; no full UC completion claimed.

## Business catalog selection in Create

Added the authorized creation-catalog read using the same current session,
MR_CREATE/MR_VIEW and branch boundary. It supplies active receiving departments,
warehouses, rooms/areas, PRIORITY references and material item/unit choices. Units
are the active base UOM or an active direct item-specific conversion to that base.
No stock balances, prices or supplier data are exposed. Optional incidents are
independently INCIDENT_VIEW-scoped and checked again before response disclosure.

The Create form now selects these references by name; line numbers are assigned
and renumbered automatically, room/area selections are mutually exclusive, changing
item clears the unit, and changing branch clears all dependent selections/lines.
Submit requires a needed-by time, warehouse and eligible approver; an empty line
list disables Save. Backend validation remains authoritative. The branch field
still needs an authorized branch selector; linked-create integration remains open.

Fixed build-detected C# local-name collision and TypeScript decoder narrowing error.
Final backend build PASS (0 warnings/errors), TypeScript and Vite build PASS with
existing router/chunk warnings. No SQL writes or business/API/browser tests. No UC
completion claim on source/build evidence alone.

## Authorized branch selection and linked-create UI

Added creation-branches under the current HTTPS cookie/SQL session. Branch choices
intersect active branch access, an approved creator role's MR_CREATE and independent
MR_VIEW. Create/catalog/approver admission now reuses that same branch calculation;
the branch-list response rechecks the list before disclosure to catch expiring grants.
The form uses branch names and clears dependent choices when the branch changes.

An authorized Internal Request material panel now opens Create with its branch and
loaded source version. The user must explicitly choose required/optional completion.
The transport sends InternalRequestId + RequestLink together in the existing atomic
Create command. The source branch is fixed in this flow; backend source scope/state,
version and target creation authority remain mandatory. Repeated attempts do not
silently refresh the source version or automatically retry after uncertain results.

Fixed a build-detected Span/await expression by awaiting the final branch snapshot
before comparison. Backend build PASS (0 warnings/errors); TypeScript/Vite PASS
with existing router/chunk warnings. No SQL mutation or business/API/browser tests.
Runtime still needs verified FE13 capabilities, approved grants/mappings and enabled
notification delivery; source completion is not COMPLETE_VERIFIED acceptance.

## Current approver actions through API and UI

Admitted Request Information, Approve, Reject and Close Remainder with exact
principal/input/request binding and a Serializable action transaction through final
response authorization. Each phase requires the latest assigned independent
DEPT_MANAGER, the approved current department mapping/L1-or-fallback grant, and
independent MR_VIEW. Existing service state/version/quantity/reason/audit checks
remain authoritative; no global MR_APPROVE_L2 change or grant was introduced.

Canonical UI exposes these admitted actions. It loads the current request/version,
limits approval quantities to active lines, and displays item/unit names from the
material detail DTO. Lines and IDs are not manually added or reassigned in the
approval form. Save is disabled unless the loaded cycle identifies this actor and
the required current state. Server authorization still handles role/grant revocation.
Required reasons retain the service's minimum; optional approval notes are not
given an extra minimum.

Backend build PASS (0 warnings/errors), frontend typecheck PASS; production build
PASS with existing router/chunk warnings. No SQL writes or business/API/browser
tests. Requester resubmission/edit/cancel admission, requester decision notices,
inventory review/fulfillment and full runtime acceptance remain outstanding.
No UC promoted solely from these source/build results.
