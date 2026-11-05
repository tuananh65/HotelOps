# Checklist V1 — UC039/040 explicit disposable deployment

Developer conditionally approved the Task72 proposal for disposable SQL verification.
No production/shared deployment is authorized. No schema changes occur at application startup.

Prerequisites: guarded canonical trial, Task17 verification marker, deployed UC017 module.
The runner pins the reviewed local SQL instance and validates the exact trial database
name, server identity and marker before opening its own transaction. It retains the
same catalog transaction applock as UC017/018 and checklist writers.

```powershell
& scripts/database/release/checklists/Run-Checklists.ps1 -Stage Apply -Database $trialDatabase -Receipt $unusedReceipt
& scripts/database/release/checklists/Run-Checklists.ps1 -Stage Verify -Database $trialDatabase -Receipt $anotherUnusedReceipt
```

`Run-Canonical.ps1 -Stage Trial -RunId $newTrialRun -Checklists` explicitly includes
UC017 and checklist V1 for Fresh/Upgrade/AuthUpgrade. Omitting `-Checklists` retains
the existing canonical behavior. Canonical receipts include source hashes and schema
comparison. Verification does not repair unknown/partial schema or overwrite receipts.

V1 adds nullable checklist branch ownership, real root rowversion (GBR-005), nullable
criterion_group nvarchar(100), an applicability bridge with composite PK/non-cascade
FKs/reverse index, a branch read index, and six compatibility/immutability triggers.
Legacy rows retain NULL ownership and original content; rowversion is generated
metadata, not an ownership/content backfill. No generated entities are overwritten:
opt-in read projections and trigger-compatible mappings live in partial source files.
No role, permission or role_permission records are changed by deployment. HK_SUPERVISOR
requires an independently configured effective SQL CHECKLIST_MANAGE/BRANCH grant plus
branch assignment/access; role name alone never authorizes the API. The acceptance
fixture explicitly creates/removes a trial-only grant to prove both sides.

Existing generic legacy templates remain eligible for UC017. Scoped checklist creation
selects existing active RoomTypes in the same branch; overlapping checklist applicability
is allowed. UC018 may choose a scoped default only when applicable and effective Published.
Creating a new RoomType with a scoped default is refused (the new type has no bridge yet).
Publication never bulk-changes defaults or applicability. Retirement can leave an existing
default referencing a retired checklist; selectors/new assignments reject it until an
authorized replacement is chosen. Existing assigned inspections retain their version.
No future inspection assignment service is claimed; the SQL selection guard protects
new or changed version/room bindings to owned checklists, while existing bindings can progress.

Published/used criteria and version identity/content are immutable. Lifecycle retirement
is allowed. Draft editing replaces Draft criteria; editing a Published/Retired checklist
creates the next Draft. Applicability is fixed once any version has been published.
Checklist code/name/ownership/applicability are immutable through the V1 edit API;
UC040 edits criteria as its SRS specifies. HTTP If-Match binds real rowversion and the
materialized aggregate/audit revision. Every aggregate write advances the root rowversion
within the same transaction as criteria/version changes and its audit.

Rollback is explicit `-Stage Rollback`: only an empty V1 extension may be removed.
Any scoped checklist, bridge data or non-null group causes refusal; no data is deleted
to make rollback pass. Genuine mid-DDL failure rolls the complete installation back.
After populated rollback refusal, retain V1 and prepare a separately reviewed forward
recovery; do not manipulate the package hash or run automatic legacy conversion.
The runner verifies exact trigger definitions against the package, enabled/trusted
constraints, supporting indexes and package hash before replay/rollback.

Source regeneration must retain these controlled partial projections and
`UseSqlOutputClause(false)` for trigger tables. UC017 uses SCOPE_IDENTITY on the same
transaction/connection so INSERT triggers do not break its identity retrieval.
DEV2 source was not changed. Existing columns/FKs remain available for legacy readers;
DEV2/future writers that deliberately target owned checklist/version bindings must
honor the new database guards rather than disabling them.
