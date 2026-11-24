# P0 runtime verification contract

> Cập nhật nghiệm thu 24/11/2026: [báo cáo hiện tại](implementation-report.md) và [bằng chứng thực thi](validation-evidence.md) thay thế các đánh giá NOT_TESTED/PARTIAL tại mốc audit ban đầu bên dưới. Giữ nội dung audit để theo dõi lịch sử; 28 nhóm E2E đã chạy, không suy rộng thành mọi tổ hợp hoặc Production đã triển khai.

Status: **PARTIAL / runtime NOT_TESTED**. These files prepare an actual API runner; they do not establish that all 28 workflows passed.

## Target and safety

- Only database `HotelOpsCompactWorkdayTestP0261124` on a dedicated local TEST API port.
- Reject Production 5080/5769 and existing development 5186/5173.
- Integration owner supplies a fresh independent `verifyTarget(fixture)` probe of the running API PID, connection database and origin. Merely echoing fixture values is not valid evidence.
- Existing accounts, password hashes, historical records and source inventory remain unchanged. Additional isolated fixture accounts/items may be created by the owner; synthetic physical receipts require `allowSyntheticTestReceipt: true` and explicit TEST-only provenance.
- Tokens stay in an untracked fixture outside documentation. Results store actor aliases, routes, HTTP status and timings, never headers/request or response bodies.

## Execution

```powershell
$env:P0_RUNTIME_FIXTURE = 'absolute-path-to-untracked-fixture.json'
$env:P0_RUNTIME_ADAPTER = 'absolute-path-to-integration-adapter.mjs'
$env:P0_RUNTIME_RESULT = 'artifacts/p0-workflow/runtime-results.json'
node scripts/verification/p0-runtime-runner.mjs
```

Adapter exports `verifyTarget` and `scenarios`, keyed `E2E-01` through `E2E-28`. Runner exports reconciliation helpers. Every executed scenario must assert business invariants and produce actual API traces or explicit live database probe evidence. Missing scenarios are `NOT_TESTED`; any failure or missing scenario returns a nonzero exit code. No network retries are implicit; retry/idempotency cases deliberately reuse operation keys.

`p0-runtime-request-scenarios.mjs` supplies actual endpoint cases 09, 10, 11, 16 and 26. Compose its `requestScenarios(probe)` with the remaining owner-supplied scenarios. The probe exposes asynchronous `readStock(fixture)` and `readAdditionalIssued(fixture)` reading the tested item. These must query live APIs or read-only SQL, never return stored expectations.

Fixture shape:

```json
{
  "database": "HotelOpsCompactWorkdayTestP0261124",
  "baseUrl": "http://127.0.0.1:5791",
  "actors": {
    "hk": { "token": "LOCAL_SECRET" },
    "supervisor": { "token": "LOCAL_SECRET" },
    "store": { "token": "LOCAL_SECRET" },
    "om": { "token": "LOCAL_SECRET" },
    "foreignOm": { "token": "LOCAL_SECRET" }
  },
  "requests": {
    "branchId": 0,
    "warehouseId": 0,
    "allocationId": 0,
    "itemId": 0,
    "allowSyntheticTestReceipt": false
  }
}
```

Zero IDs above are placeholders, not executable fixture data. The allocation must be active, owned by hk and have enough central stock for the success case. Run every case with correct prerequisites; failed cases may leave pending requests, so subsequent failure is retained instead of silently clearing business records. Case 11 proves store rejection and required reason; a separate insufficiency fulfillment attempt is still required to establish that genuine shortage prevents issue.

## Remaining fixture/scenario work

The canonical 28 acceptance names are in `p0-runtime-cases.mjs`. Integration owner must add live scenarios 01–08, 12–15, 17–25, 27–28. Existing adapters in `p0-supply-contract.mjs` and `p0-request-workflow-contract.mjs` may be reused but their assertions must be surfaced to the runtime context.

Required evidence: before/after user counts and hashes (never password values), role/membership/session migration; all eight logins; scope negatives; scheduled shifts currently open in Asia/Saigon; eight fill configurations; capacity-based reservation; actual assignment cancellation/reassignment; timed-out shift manual completion; one return document; event and document counts after repeated operation keys; request claim and approve/reject races; two competing stock issues; configuration revision while assigning; two concurrent room blockers; all inspection retries and occupancy preservation.

Reconciliation reads opening stock plus posted receipt/issue/return totals, closing stock and staff ledger from the same bounded test window. Returns must be excluded from `receipts` if counted in `returns`; otherwise the formula double-counts them. Staff remaining equals initial plus additional minus used minus returned; available equals remaining minus reserved. Both branches must be represented. Use transactionally consistent snapshots to avoid apparent discrepancies while a writer commits.

## Contract issues found during preparation

- Existing supply contract requires same-key start/end retry success. Public Workday concurrency validation must honor idempotency before rejecting stale row versions; internal allocation idempotency alone is insufficient.
- Existing supply lifecycle helper expects `CLEANING_WORK_OPEN`, while current `EndBlocker` reports `SHIFT_WORK_OPEN`; align error assertions to actual agreed API code.
- Generic authorization tests must require 403/404. A 409 proves business-state rejection, not authorization denial, and cannot certify OM read-only.
- Replenishment receipt must preserve its operation key and original version when retrying, then use refreshed version with a new key for the remaining physical quantity.
- The direct cross-branch transfer test seeds a simulated receipt. It is TEST fixture evidence only and must never run against Production or claim real received inventory.

No server was started, migration applied or runtime test reported as passed by this preparation task.
