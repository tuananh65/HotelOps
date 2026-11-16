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
An additive lifecycle migration and explicit legacy reconciliation are still needed
before this recovery database can exercise the approved terminal completion paths.
