# Housekeeping V1 — Task73 disposable-only rollout

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
