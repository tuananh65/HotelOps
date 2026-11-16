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

## Approved material approver routing â€” source implementation

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

## UC093 requester lifecycle source closure (2026-11-16)

Edit, Submit/resubmit and Cancel now have compiled production admission with current
HTTPS cookie session, requester ownership, MR_CREATE plus independent MR_VIEW,
approved creator role and branch access. The action transaction remains Serializable
through final response authorization. Approver fallback is checked again after
ordered user locks; stale discovery cannot acquire a different approver out of order.
First submission through Edit now accepts a Draft with no previous approval cycle.

Canonical UI loads the request/version and active lines. It preserves historical
line identities/numbers, requires a new line for item/UOM replacement, and allocates
new numbers above all historical lines. Submit uses the mapped approver selector;
Cancel requires a reason and explicit confirmation. State/ownership/first-issue
checks disable unavailable actions; server state/version guards remain authoritative.

Cancellation of a previously submitted request persists a notification intent in
the same transaction as status/audit. The existing opt-in worker delivers it with
current mapped approver authority and MR_VIEW, finite notification retention and
idempotent receipt checks. Opening it rechecks source provenance and current access.
Draft cancellation without a prior cycle does not invent an approver. No schema,
grant, shared database or worker configuration was changed.

Direct checks: backend build PASS, zero warnings/errors; frontend typecheck and
production build PASS. Existing React Router use-client and >500 kB bundle warnings
remain. Business/API/browser/SQL tests NOT RUN per acceleration instruction.
Source is ready for UC093 Human QA, not COMPLETE_VERIFIED. Runtime requires the
verified material lifecycle and department-mapping capabilities, actual grants and
enabled notification worker. Human QA must cover edit/reapproval, stale versions,
revoked access, first-issue rejection, cancellation rollback and notice retries.

## Requester review-decision notifications (2026-11-16)

RETURNED, APPROVED and REJECTED decision receipts now act as durable requester
notification intents. The opt-in material worker scans committed, cycle-correlated
receipts inside the finite notification retention window. Delivery validates current
requester eligibility, independent MR_VIEW and branch/resource provenance, and
rechecks before committing its idempotent receipt. Notification Center source
opening verifies decision actor/time and current access again. Historical decisions
remain identified by their receipt instead of pretending to describe current state.
No schema, grants, database contents or worker configuration were changed.

UC095 approval quantities now default to requested quantities, matching its normal
flow step 2; explicit Save, quantity validation and all-zero rejection reason remain.
Backend build PASS (0 warnings/errors), frontend typecheck and production build
PASS with existing React Router/use-client and large-bundle warnings. Business,
SQL, API and browser tests NOT RUN. No additional COMPLETE_VERIFIED UC claimed.
Storekeeper notification, remainder-close requester notification, scoped stock
review and fulfillment admission still require implementation/acceptance.

## Remainder closure requester notice (2026-11-16)

The material worker now scans committed REMAINDER_CLOSED requests with a recorded
closer/time/reason, issued quantities plus remaining approved quantities and a latest
approved cycle identifying that closer. These persisted terminal facts are the
durable event; notification processing does not invent a timestamp or modify the
request. It reuses requester decision delivery with a distinct source/type namespace
so request IDs cannot collide with approval decision IDs.

Current requester account eligibility, independent MR_VIEW, branch/resource scope
and provenance are checked before delivery and before commit. Notification source
opening also verifies the stored closure actor/time. Finite retention, retry cursors,
receipt idempotency and the existing opt-in worker configuration are preserved.
No schema, grant or database change was made. Backend build PASS (0 warnings/errors),
diff check PASS. No frontend change; frontend checks were not repeated. SQL/API/
browser/business tests NOT RUN. Storekeeper recipients, stock review and fulfillment
admission remain open; no new COMPLETE_VERIFIED UC is asserted.

## UC094/096 scoped stock review (2026-11-16)

Availability A004A-040 is now admitted through the compiled material read contract.
It requires independent MR_VIEW on the request and INVENTORY_VIEW on its actual
issuing warehouse branch, plus current HTTPS/SQL session and account eligibility.
The warehouse must be active and in the request branch. Request scope/provenance
and both grants are checked through final disclosure under Serializable; no stock
reservation or inventory mutation is introduced.

Canonical review pages link to the existing base-unit stock preview and back to
the material request. Each distinct active item also links to the scoped material
list filtered by the current request's branch, requester and item, ordered newest
first. MR_VIEW filtering remains on the server and no broader read grant is inferred
from approval authority. Availability metadata now names approved MR_VIEW instead
of the former demo-only permission.

Backend build PASS (0 warnings/errors), frontend typecheck and production build
PASS with existing React Router/use-client and bundle-size warnings; diff check
PASS. No DB changes, runtime/API/browser or business tests. Review/fulfillment
end-to-end acceptance and Storekeeper notifications remain; no UC promoted to
COMPLETE_VERIFIED from build results.

## UC097/098 issue admission and form (2026-11-16)

Material Issue A004A-051 now has a compiled contract binding the exact principal,
input and request. It requires an active STOREKEEPER assignment/access in the actual
issuing warehouse branch, MR_FULFILL, STOCK_ISSUE_POST, INVENTORY_VIEW and independent
MR_VIEW. Requester and current approved-cycle approver cannot issue. The service
also rejects requester self-issue, closing a missing defense in the former service.
Existing stock locks, conversion/aggregate balance checks, request version,
fulfillment reconciliation and same-transaction stock/request/audit writes remain.
Authority is checked before persistence/posting and before the outer commit after
response authorization. No client ActorUserId establishes authority.

Canonical UI exposes full remaining or partial issue using loaded approved lines,
current request version, explicit document date and bounded quantities. It sends
only positive partial lines and no explicit lines for FullRemaining. It cannot
manually substitute line IDs; zero skips a partial line. Uncertain network outcomes
retain the existing no-auto-retry behavior.

Direct backend build PASS (0 warnings/errors); frontend typecheck/build PASS with
existing React Router/use-client and bundle-size warnings. No SQL mutation, schema,
grant or worker configuration change. Business/SQL/API/browser tests NOT RUN.
Pickup notices, Storekeeper notification and full issue/rollback/concurrency Human
QA remain; UC097/098 are not COMPLETE_VERIFIED solely from source/build evidence.

## UC097/098 requester pickup notice (2026-11-16)

Posted material fulfillments now provide durable pickup notification events for
the requester, for both full and partial batches. The event requires an ISSUE/
POSTED stock document linked to the exact fulfillment and warehouse, matching
posting actor/time and nonempty fulfillment lines. Delivery and source opening
reuse full fulfillment reconciliation of line identities, quantities, stock line
correlation and cumulative quantities before allowing access.

The existing finite-retention, idempotent requester delivery path uses separate
event namespaces for decision, remainder closure and fulfillment. Current account,
MR_VIEW, branch and provenance checks remain mandatory; notification links confer
no permission. Batch notices do not claim that the entire request is fully issued.
The worker remains opt-in and was not enabled or executed against a database here.

Backend build PASS (0 warnings/errors); no frontend edits, SQL changes or runtime/
business tests in this batch. No COMPLETE_VERIFIED promotion. Storekeeper approval/
closure notices and full workflow runtime acceptance remain outstanding.

## Authorized fulfillment receipt navigation (2026-11-16)

Issue returns a Location pointing to its fulfillment receipt; that GET previously
remained outside compiled admission. A004A-041/042 now use the current HTTPS/SQL
session, independent MR_VIEW, parent-request branch/ownership scope and descendant
provenance checks of the material read contract. A child receipt must belong to
the exact authorized parent request; a substituted fulfillment ID is rejected.
The scoped read transaction and final authority recheck cover returned receipts.

Canonical request detail links each fulfillment number to its receipt, with a
return-to-request path. Permission metadata uses MR_VIEW instead of the former
demo code. No stock movement/audit endpoint or inventory mutation authority is
conferred by these receipt reads.

Backend build PASS (0 warnings/errors), frontend typecheck/build PASS with existing
router/chunk warnings; no DB writes or business/API/browser tests. Storekeeper
recipient contract is pending user clarification: eligible branch Storekeepers
versus Warehouse.ManagerUserId. No UC acceptance promotion from these checks.

### Approved Storekeeper recipients — 2026-11-16

User approved all eligible Storekeepers in the issuing warehouse branch, rather than Warehouse.ManagerUserId. Implemented approval and remainder-closure notifications from committed business receipts, with separate source namespaces from requester notices. Current active STOREKEEPER role, branch access, MR_FULFILL and independent MR_VIEW are checked at delivery and source opening. Requester/event approver excluded; recipient deduplication and source provenance enforced in a serializable transaction. Retained events are rescanned for retry and newly eligible recipients. No schema, grant or database mutation.

Changed source: MaterialStorekeeperNotification.cs, MaterialApprovalNotificationWorker.cs, NotificationCenterService.cs.
Direct validation: dotnet build HotelOps.slnx --no-restore PASS, zero errors/warnings; git diff --check PASS (Git reports LF/CRLF normalization warnings). Automated/business/API/SQL/browser tests NOT RUN per code-first instruction. Worker remains opt-in; delivery and query translation have not been verified against SQL. UC095 remains pending runtime/business acceptance; no new COMPLETE_VERIFIED claim.

### UC096 availability preconditions — 2026-11-16

Direct source comparison against UC096 PRE-02 found Availability admitted stock reads for any request state with MR_VIEW/INVENTORY_VIEW. Added MaterialAvailabilityAccess: current eligible Storekeeper/Department Head/Operations Manager role and branch access; Approved/Partially Issued, or Pending Approval with the latest unresolved cycle assigned to this actor and current mapped material approval authority. Rechecked before disclosure inside the existing serializable contract; current SQL session and independent grants remain required. Requester/actor locks acquired in canonical order before reviewer checks. UI availability link follows the loaded lifecycle/reviewer state; server remains authoritative. No schema or DB changes.

Backend build --no-restore PASS (0 warnings/errors). Runtime, SQL and business tests NOT RUN per instruction. UC096 remains PARTIAL: current source only previews the source warehouse; warehouse selection, in-transit quantities and explicit unit-safe fulfillable-line presentation remain to implement. This is an authorization correction, not full UC completion.
Frontend typecheck and production build PASS. Existing Vite warnings: ignored React Router use-client directives and bundle above 500 kB (913.89 kB).

### UC096 branch warehouse availability source — 2026-11-16

Implemented the remaining availability presentation across DTO, service and UI. Availability returns active warehouses in the authorized request branch; the UI selects a warehouse without changing the request's issuing warehouse. Stock comes from signed stock movements in base UOM. Inbound TRANSFER quantities subtract reconciled received/damaged/missing receipts with the immutable dispatch conversion factor; legacy states, mismatched receipt/document provenance or inconsistent quantities produce N/A with a reason. Transit is never added to issuable stock and transfer/source-branch identifiers are not exposed.

Per-line fulfillment is capped by approved remainder and available stock using an active item-specific conversion. The preview respects decimal(18,3) for both issue and base quantity, and actual decimal(18,6) conversion mapping. Pending approval quantities remain N/A; inactive/ambiguous conversion or negative ledger balances carry explicit reasons. Repeated-item lines are labeled shared stock rather than claiming independent allocations. UI highlights shortage and suggests partial fulfillment or replenishment. Read contract retains Serializable disclosure, MR_VIEW, INVENTORY_VIEW, current session/role/branch and current reviewer preconditions. No reservation, posting, schema or database mutation.

Direct evidence: dotnet build HotelOps.slnx --no-restore PASS (0 warnings/errors); npm run typecheck PASS; npm run build PASS. Existing Vite warnings: React Router use-client ignored; JS bundle 916.87 kB exceeds warning threshold. git diff --check PASS. Automated/business/API/SQL/browser tests NOT RUN per code-first instruction. SQL query translation, concurrent inventory changes, multiple warehouses/UOMs, duplicate-item partial issue and UI-to-HTTPS-to-SQL acceptance remain Human QA gates. UC096 source implemented pending QA, not COMPLETE_VERIFIED.

Files: MaterialRequestContracts.cs, MaterialRequestService.Queries.cs, MaterialRequestService.Availability.cs, MaterialAvailabilityView.tsx, Workspace.tsx; matrix and this receipt updated.

### UC097/098 issue-form availability integration — 2026-11-16

Issue form now loads availability through the existing authenticated transport, bound to current authority generation, request ID, issuing warehouse and request rowversion. Availability response carries the rowversion selected with its snapshot. Mismatching/failed responses show unavailable, and partial input limits become zero rather than reusing stale stock. Refresh aborts superseded reads; session loss triggers revalidation. No automatic mutation/retry or quantity allocation is introduced.

Each open line displays remaining approved and fulfillable quantities in request UOM, missing-data reasons, shared-item-stock warning and shortage guidance. Partial inputs are capped at the current preview; a shortage offers a button to enter partial quantities. FullRemaining retains all-or-nothing server posting. The API remains authoritative for combined stock, decimal conversion, concurrency and permissions.

Backend build --no-restore PASS, zero warnings/errors. No database/schema changes. Business/API/SQL/browser tests NOT RUN per code-first instruction. Replenishment creation remains a separate UC100 integration dependency; UI currently suggests replenishment without claiming that workflow is complete. No new COMPLETE_VERIFIED UC.
Final frontend typecheck and production build PASS; existing React Router use-client warning and >500 kB bundle warning remain (919.53 kB JS). git diff --check PASS apart from line-ending normalization notices.

### UC099 business history presentation — 2026-11-16

Scoped request list now includes latest assigned approver and active line requested/approved/issued quantities. Enrichment is limited to IDs in the already-authorized page and preserves newest-first ordering/filter scope. Quantities remain separated by item/UOM.

MaterialHistoryView renders creation, submission cycles, decisions with cycle quantity snapshots, fulfillment receipt links, remainder closure, current quantities and linked return document metadata from the existing authorized detail response. Missing cancellation/return event timestamps are not fabricated from UpdatedAt. Raw audit endpoint remains unadmitted; no new permissions or broad audit payload disclosure. Linked incident ID remains metadata and does not confer incident access. Other linked-work navigation and full return/cancellation chronological evidence remain gaps, so UC099 is still PARTIAL.

Direct verification: dotnet build HotelOps.slnx --no-restore PASS (0 warnings/errors); npm run typecheck PASS; npm run build PASS. Existing Vite warnings: ignored React Router use-client directive and bundle >500 kB (922.99 kB JS). git diff --check PASS. Business/API/SQL/browser tests NOT RUN per code-first instruction; projected nested list query requires SQL QA. No database mutation. No new COMPLETE_VERIFIED UC.

UC100/101 decision requested: production replenishment permissions and role/scope contract. Existing catalog uses DEV2_DEMO_REPLENISHMENT_*; production mutations remain closed while awaiting decision. Independent UC099 work proceeded.
