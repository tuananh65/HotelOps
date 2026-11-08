# Housekeeping V1 — Task73 disposable-only rollout

## INSP-004C2 evidence revision patch

INSP-004C3 subsequently verified the patch on a newly created, owner-marked local disposable
database only. See `tests/HotelOps.Inspection.EvidenceTests/INSP004C3_REPORT.md` for actual
receipts, failures/fixes and limits. This is not D4 acceptance or shared deployment approval.

No database deployment or D4 acceptance is granted by this patch. Original
`01_Housekeeping.sql` is unchanged, preserving existing V1 package identity. The existing
runner accepts explicit `-Stage Apply -EvidenceInvalidation` only on its already guarded
disposable targets, after a separate authorization to execute. Default Apply does not install
the amendment. `-Stage Verify -EvidenceInvalidation` refuses unamended V1. Once installed,
any runner invocation verifies the amendment automatically, including without the switch.

`04_EvidenceInvalidation.sql` replaces the two link trigger definitions and adds one
Attachment trigger. All three handle INSERT/UPDATE/DELETE set-wise; existing ownership
guards remain. Attachment rebinds inconsistent with existing links throw. Soft-delete/restore
and metadata updates invalidate related Inspection roots. Unlinked direct Inspection evidence
also invalidates its old/new root, because existing history includes these attachments.
Legacy null-workflow Inspections are not adopted. No columns, FK, enum or ledger are added.

The separate `HotelOps.Housekeeping.EvidenceRevision.V1` marker stores the amendment file
hash. `05_VerifyEvidence.sql` checks marker, trigger parents, enabled state and all I/U/D
events. The runner additionally checks exact marker hash and OBJECT_DEFINITION of all nine
effective triggers (only CREATE/ALTER header normalization accounts for SQL Server's stored
CREATE OR ALTER representation; the body is compared exactly). Base trigger definitions are overlaid with the three amendment definitions;
old same-name V1 triggers cannot satisfy amendment verification. Missing/partial/unknown/drifted
packages are refused, not silently repaired. Generic runtime HousekeepingSchema remains the
V1 compatibility gate; it does not certify D4. No runtime writer is enabled by this release.

DDL, marker and verification use the runner's single transaction/catalog lock. Existing full
module rollback verifies definitions and refuses owned data first; on an empty module it also
drops the Attachment trigger and evidence marker before removing referenced tables. There is
no separate populated amendment downgrade. After data exists use a reviewed forward recovery.
Install, replay, post-DDL fault rollback and empty/full rollback guards were subsequently
exercised in INSP-004C3; populated forward recovery remains a separately reviewed operation.

Deploy the prepared Attachment partial mapping (`UseSqlOutputClause(false)`) with the patch
before enabling triggers. Without it EF inserts can fail with SQL Server bare OUTPUT rules.
Shared StoreFeatureEvidence users (Inspection and WorkProgress, plus FE-04 consumers at
integration) need real upload/save/download regressions. Generated entities remain unchanged.

No trigger execution order is assumed: all validations and touches participate in the same
mutation transaction; any THROW rolls everything back. Root updates do not mutate links or
attachments, so these three triggers form no recursive cycle. Do not configure trigger-order
overrides or global server settings as part of this package. Existing nested root guards may
execute; their behavior and relevant server settings must be captured in isolated SQL tests.

Multi-row mutations deduplicate parents within each trigger, not across an entire operation;
rowversion may advance multiple times. Rollback leaves rowversion gaps, never a committed
evidence change with only a rolled-back root touch. No numeric increment contract is promised.
Child-first DML then parent touch can deadlock with root-first conclusion. No deadlock-free
guarantee/retry layer is introduced. Activated writers must reconcile ordering and handle
victim rollback using existing transaction error behavior. UC045 still needs a conditional
state/rowversion update; comparing an ETag outside the transaction is insufficient.

Offline checks:

```powershell
& scripts/verification/Housekeeping/Test-EvidencePatch.ps1
dotnet run --project tests/HotelOps.Inspection.EvidenceTests
```

These verify PowerShell/package comparison logic and EF metadata only, not SQL semantics.
At patch preparation no authorized isolated database was supplied. The following is the
regression plan; INSP-004C3 records the subset actually run and does not claim all settings
or all possible batch permutations were exhaustively exercised:

- Base installation then explicit upgrade; fresh base+amendment; repeat Apply/Verify; old V1
  refusal under explicit evidence Verify; missing/disabled/altered triggers and marker drift.
- Single/multi-row I/U/D on both bridges; old/new parent changes, multiple parents and shared
  attachment references; zero-row DML; unchanged legacy roots and unrelated attachments.
- Direct Inspection attachment insert/delete/rebind, soft-delete/restore and metadata updates;
  rejected linked-owner rebind and failed link to deleted/foreign attachment; FK hard-delete.
- Existing result/defect mutations, conclusion-root guards, no recursion, effective trigger
  settings/order variations; EF private evidence upload and identity return after mapping change.
- Rollback after evidence mutation/parent touch/DDL fault; marker and definitions rollback;
  refusal of populated full rollback without deletion, and successful empty full rollback.
- Two sessions racing evidence mutation and conditional conclusion in both directions;
  stale rejection, blocking/deadlock victim rollback, final committed source/revision consistency.
- Private bytes remain create-new storage assets. Out-of-band byte replacement is not observable
  by database triggers; do not claim filesystem immutability or distributed atomicity from D4.

Implementation owner: DEV1 FE-05; shared database/Attachment mapping approval and FE-04/DEV2
consumer coordination remain required. D3/D5/D7 and authoritative UC045 writer are excluded.

Conditionally approved for UC029/041/042/043/046. No production/shared database deployment
is authorized. No generated entities, roles/permission grants or DEV2 source are changed.
No startup migrations. Existing coarse status values remain available to legacy readers.

```powershell
& scripts/database/release/canonical/Run-Canonical.ps1 -Stage Trial -RunId $uniqueRun -Housekeeping
& scripts/database/release/housekeeping/Run-Housekeeping.ps1 -Stage Verify -Database $trialDatabase -Receipt $unusedReceipt
```

`-Housekeeping` explicitly includes UC017 and checklist V1 prerequisites, all source hashes,
Fresh/Upgrade/AuthUpgrade integrity and schema comparison. The individual runner accepts
only the reviewed instance, exact canonical disposable-name regex and Task17 marker.
Installation/replay/rollback execute in a transaction under the existing catalog applock.
Unknown package hash or partial schema is rejected; no automatic repair/conversion.

Schema: nullable workflow_status and real generated rowversion on cleaning task/inspection/
rework aggregates; inspection/rework due_at; nullable defect room_location; normalized
rework-selected-defect, defect-attachment and result-attachment bridges. Existing legacy
workflow_status stays NULL. Metadata rowversion creation is not a business-state backfill.
Status CHECKs require compatible coarse mirrors for opt-in records. No unrelated columns.
Supporting indexes/FKs/column types/triggers are checked by postflight; trigger definitions
are compared to the exact package. Read/write services explicitly gate on installed schema.

| SRS state | Existing coarse status |
|---|---|
| Cleaning PENDING_ASSIGNMENT/ASSIGNED | PENDING |
| Cleaning IN_PROGRESS/ON_HOLD/PENDING_INSPECTION/REWORK_REQUIRED | IN_PROGRESS |
| Cleaning COMPLETED/CANCELLED | COMPLETED/CANCELLED |
| Inspection ASSIGNED | PENDING, result NULL |
| Inspection INSPECTING/PENDING_CONCLUSION | IN_PROGRESS, result NULL |
| Inspection PASSED/FAILED | COMPLETED, result PASS/FAIL |
| Inspection CANCELLED | CANCELLED |
| Rework PENDING_ACCEPTANCE | PENDING |
| Rework IN_PROGRESS/PENDING_REINSPECTION | IN_PROGRESS |
| Rework COMPLETED/CANCELLED | COMPLETED/CANCELLED |

No legacy COMPLETED cleaning row is automatically treated as Pending Inspection/Ready.
Legacy unassigned PENDING cleaning can opt in only on explicit assignment request with
`adoptUnassignedLegacy=true`, no current assignment and retained eligibility checks.
No legacy inspection/rework adoption endpoint exists. Completion/rejection prerequisite
states in acceptance are explicit disposable fixtures; UC034/044/045 are not implemented.

Eight triggers preserve cycle/criterion/evidence ownership, unique open inspection, immutable
assigned versions (including legacy templates), and prohibit result/defect writes after
their allowed phase. Results/defects advance parent inspection rowversion even for direct
SQL edits; APIs additionally touch the root within the same transaction. Controlled partial
mapping disables bare OUTPUT for result/defect trigger tables; generated entities unchanged.
Room status is not mutated by these workflows; room lifecycle recalculation/DEV2 protocol
is a separate unresolved contract, not inferred from coarse legacy status.

Evidence: JSON Base64 request, at most5 files per operation, JPG/PNG/PDF magic/MIME/extension
match, max10MiB each, server-generated random private storage keys, no client paths/FileUrl.
Default private root is ContentRoot/App_Data/evidence; configurable Evidence:PrivateRoot
is operator-controlled/trusted and never served statically. Only files created by the current
operation are removed on transaction failure/cancellation. SQL rows/audit/links are atomic;
a process crash before SQL commit can leave inaccessible orphan bytes, requiring reviewed
offline recovery, not startup/bulk deletion. No distributed filesystem transaction claimed.
Source-authorized downloads reuse Task72 INSPECTION_VIEW history policy and no-store/nosniff.

Writes use existing SQL CLEANING_ASSIGN or INSPECTION_PERFORM/BRANCH, session/final gate,
branch membership and assigned-inspector ownership; selectors use CLEANING_ASSIGN or
INSPECTION_VIEW. No HK automatic grant. Housekeepers require active HK staff role, canonical
HK department/branch/access and an assigned planned shift window. PLANNED/ASSIGNED are
existing SQL defaults; interpreting their current scheduled window is an ASSUMED_V1 for
the PARTIAL assignment/rework flows, not attendance/workday verification. Prospective Users
are locked in sorted order before feature locks and eligibility/due time rechecked at commit.

Rollback refuses any opt-in business rows, locations or bridge data. Empty-extension rollback
removes only module objects; do not delete data to make rollback pass. After refusal, keep
the installed module and prepare a separately reviewed forward recovery. Verify upstream
checklist compatibility before use. No arbitrary external writer compatibility certified.

UC042/043 can complete independently. UC029/041/046 return `notification: NOT_INTEGRATED`;
no notification rows/outbox or delivery claim are fabricated. They remain PARTIAL until
the DEV2 contract in the Task73 handoff is approved, implemented and verified end-to-end.
