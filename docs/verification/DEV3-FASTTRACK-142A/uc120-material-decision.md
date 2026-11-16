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
