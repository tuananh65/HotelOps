# UC032 / UC109 — explicit disposable progress module

Approved scope: [Task74 contract](../../../verification/Housekeeping/TASK74_CONTRACT_PROPOSAL.md).
This is Database First SQL, not an EF migration or production deployment authorization.

```powershell
./scripts/database/release/progress/Run-Progress.ps1 -Stage Apply -Database <canonical-trial> -Receipt <unused-receipt.json>
./scripts/database/release/progress/Run-Progress.ps1 -Stage Verify -Database <canonical-trial> -Receipt <another-unused-receipt.json>
```

Runner pins the reviewed local instance, exact disposable Canonical name, actual server/database
and Task17 trial marker. HotelOpsDB/shared/production are rejected. It holds the existing
transaction-owned feature catalog lock; apply + postflight + package marker commit atomically.
Apply is idempotent only for the exact installed package hash and verified trigger definitions;
partial/unknown packages fail closed. No automatic repair, legacy conversion or data reset.

Six additive tables: room-type cleaning definitions, immutable per-cleaning-task snapshots,
cleaning/task progress revision records, immutable task completion criteria, typed evidence links.
Original entities remain unchanged; unmapped SQL projections are the controlled Database First
read/write boundary. Cleaning snapshot applicability is checked at creation; later RoomType
metadata changes preserve the original immutable, writable cycle items. Existing CLEANING_TASK/TASK attachment enum values are retained. Nine trusted
FKs, six checks, named unique constraints, two log indexes, two real rowversion columns and seven
guards cover source ownership, phase, immutable identities, required completion criteria and
revision advancement from existing parent/assignment writers. No task time is modeled.

Explicit opt-in HTTP initialization is audited and copies room-type cleaning definitions;
task criteria are provided by an authorized TASK_MANAGE caller. No initialization happens on read.
Existing task fields/history are retained. Cleaning requires the prior approved Housekeeping V1
workflow state; operational progress stores IN_PROGRESS/PAUSED independently of coarse legacy
status, never treating BLOCKED as PAUSED. Initialization does not start/pause/resume/complete work.
UC032/109 retain state even at 100%; mandatory missing items/results cannot be bypassed through
an opted-in parent COMPLETED write. Other completion/time/inspection rules belong to their UCs.

`Rollback` first verifies the installed package, then refuses any populated module. An empty
module can be removed atomically, including parent/assignment triggers and indexes. For populated
deployment: keep this package/matching API, restore from a reviewed trial backup or prepare an
explicit forward migration after coordination; do not delete snapshots/evidence or drop guards.
DDL failure rolls back all tables/marker so the same approved package can be applied again.

Controlled mapping disables bare SQL OUTPUT on CleaningTask/Assignment and
OperationalTask/Assignment; compatible before and after module deployment. Generated entities,
main DbContext, DEV2 modules, RBAC catalog and UC006 gates are not modified.

Verification uses the existing HotelStructure harness `--task74`, real production HTTPS,
SQL sessions/permissions, private evidence, temporal expiry, actual races/cancellation and audit
faults. [Report](../../../verification/Housekeeping/TASK74_BATCH5_IMPLEMENTATION.md) distinguishes
accepted source behavior from missing workflows and external release readiness.
