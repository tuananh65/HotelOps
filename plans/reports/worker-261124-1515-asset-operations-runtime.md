# Supplemental A-scope runtime acceptance

Status: IMPLEMENTED test script; syntax VERIFIED; API execution NOT_TESTED by this worker, intentionally reserved for integration owner.

File: `scripts/verification/p0-runtime-asset-operations.mjs`.

Run sequentially after TEST API restart and lifecycle tests (private fixture requires approved elevated read):

```powershell
$env:P0_RUNTIME_FIXTURE='artifacts/p0-workflow/private/runtime-fixture.json'
$env:P0_RUNTIME_RESULT='artifacts/p0-workflow/runtime-asset-operations.json'
node scripts/verification/p0-runtime-asset-operations.mjs
```

The script verifies the real API listener's SQL target using the existing probe before mutations. Only new marked records in `P0_TEST_DVBV` are created. It never logs tokens, passwords, request bodies, or response bodies. Results include HTTP traces and created IDs to support recovery without reset.

## Assertions

- OM creates, self-assigns, accepts, starts, completes, then verifies an OPERATIONAL task. CLOSED state, verifier identity, `OM_OPERATIONAL_SELF_REVIEW` event and closed execution interval must exist.
- Owner, Administrator and foreign OM cannot create or verify that task.
- Action-center UNASSIGNED_TASKS and TASKS_TO_VERIFY must contain the newly created record at the right state, then remove the closed record. This tests populated queues rather than accepting empty arrays.
- Task report must contain the new closed task with verification timestamp.
- OM creates and edits a new asset, moves it between two rooms within branch and changes condition. Cross-branch move and Owner/Admin/foreign-OM mutations are rejected.
- OM offers asset custody to the fixture Maintenance Supervisor, who has branch ASSET_VIEW. Wrong recipient, Owner, Administrator and foreign supervisor cannot accept. Correct recipient acceptance changes custody.
- ASSET_HANDOVERS contains the pending offer and removes it after acceptance; immutable movement and accepted-custody events are verified.
- Asset report contains the new asset's actual location and condition. Successful scenario retires the synthetic asset, preserving history.

No C# files changed. `node --check scripts/verification/p0-runtime-asset-operations.mjs` passed. No backend/frontend build was needed for this test-only change.

## Concrete remaining gaps / review findings

1. **Potential stuck asset handover (source review, runtime NOT_TESTED):** `AssetService.Act` handover validates only `Access.Eligible`, while accept/reject additionally require recipient capability (branch ASSET_VIEW excluding Owner/Admin, or asset write capability). If an eligible recipient lacks those capabilities, the pending offer cannot be accepted/rejected by that recipient, and another handover is blocked by HANDOVER_ALREADY_PENDING. Check candidate filtering plus enforce receipt capability at offer creation; consider recovery of existing incompatible offers. This test deliberately uses an eligible Maintenance Supervisor and does not create a stuck negative fixture.
2. **Shift handover coverage is incomplete:** ActionCenter WORK_HANDOVERS reads work-item handover backlog. It does not by itself prove A §15/Action Center's shift-level handover lifecycle. Separate source/runtime trace to UC138/UC140 is still required.
3. Populated transfer and stocktake-difference reports are outside this script; no empty-response PASS is claimed. Use actual transfer/stocktake workflow fixtures before accepting those reports.
4. Asset movement across departments currently occurs via catalog DepartmentId update, while move accepts RoomId/LocationId only. Catalog audit captures before/after, but this script does not prove department reassignment or its audit. Verify that explicit requirement separately.

No API mutations executed by this worker; no server/process changes; no private fixture contents read; no database changes.

Unresolved questions: none. Integration owner should execute this script and address any real runtime failures before reporting VERIFIED.
