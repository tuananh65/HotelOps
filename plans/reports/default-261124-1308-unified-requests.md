# P0 unified requests — implementation handoff

Implemented the assigned additive backend module. No SQL applied, no build or API runtime started, no Production changes. Existing shared services were not edited.

## Behavior

- Normalized `request_workflows`, `request_workflow_lines`, `request_workflow_events`; existing WorkItem ID remains the request ID. Branch FKs, immutable origin/correlation/line snapshots, typed states, role queues, append-only history and operation idempotency.
- HK refill: own active shift allocation and snapshot items → HK supervisor confirms exact full line set → same request goes to Storekeeper → atomic full issue via existing `StaffSupplyService.Refill`. Rejection requires reason; no partial refill; no issuing closed/expired allocations. One open refill per allocation; retries return the original result without duplicate issue.
- Warehouse replenishment: Storekeeper creates request and linked existing REPLENISHMENT document → independent OM approval → Storekeeper records real partial/full arrivals through existing `DocumentService.Post`. Approval changes no stock. Receipt retries cannot duplicate stock; existing remaining-quantity and ledger guards remain in force.
- Role queue chooses a single valid on-duty handler when unambiguous, otherwise keeps branch role queue. Claim/release plus rowversion and serializable locking prevent two handlers winning one transition.
- Unified list supports mine/action/progress/completed/rejected/all. Existing generic requests and incidents remain legacy projections; typed domain records can be attached to existing roots. OM ordinary requests remain read-only; only warehouse replenishment approval is writable.
- Structured history records previous/new status and role queue/handler, actor/role, time, reason, document, correlation and previous rowversion. Notifications are inserted inside the transaction, visible after commit.

## Mandatory parent integration before enabling

1. Add `MapRequests(modelBuilder)` in Runtime mapping **before** the loop disabling OUTPUT for trigger-backed tables; register scoped `RequestWorkflowService`. Add new schema contract: three tables and four triggers, plus associated FKs/checks. Apply `p0-requests.sql` after `p0-supplies.sql`, only on `HotelOpsCompactWorkdayTestP0...` clone. No schema version update is made by this worker.
2. Generic WorkService mutations must reject financial typed requests with `USE_REQUEST_WORKFLOW`. `RequestWorkflowService.IsManagedRequest(workId, ct)` tests refill/replenishment only. Apply before every generic action/assignment/link/checklist/attachment mutation that might alter these roots. Existing specialized RoomIssue/maintenance logic remains responsible for its own transitions.
3. Generic document mutations must call `DocumentService.GuardWorkflowDocumentMutation(document, ct)` from Load for non-read operations, and validate source documents in Create/Derive/Post. This rejects changes to linked replenishment or its receipts outside the typed request API. Generic creation of a NEW REPLENISHMENT must route through the typed service (or reject with USE_REQUEST_WORKFLOW); otherwise the old unlinked API can bypass OM. Do not simply add the hook to the private Post writer: the typed method deliberately calls that writer after authorization.
4. NotificationService.Visible: call `RequestWorkflowVisibility.CanRead(db, access, actor.Id, workId, now, ct)` before legacy work visibility; non-null result is authoritative. Add the same scoped helper to generic read/deeplink paths for typed roots. This avoids a DI cycle through NotificationService.
5. Workday end: `HasOpenRefill(allocationId, ct)` must be false or explicitly resolve pending requests according to agreed policy before ending/returning the allocation. Expired shift issue is independently blocked inside this module and StaffSupplyService.
6. Existing RoomIssue/generic domain transitions may call `RecordDomainTransition(...)` **inside their own validated transaction** to attach typed metadata to the SAME root. It accepts only defined room/remediation/reinspection types and allowed branch role queues, preserves parent correlation and logs history. It is not a public arbitrary state setter. Parent retains domain state machine/readiness/claim authorization.
7. Parent low-stock reconciler must run after successful refill issue and receipt posting. This module emits request events but does not duplicate inventory alert policy.
8. Frontend: `/api/v2/requests` replaces workspace listing, detail for `structured:true` uses `/requests/{id}`; legacy `structured:false` should keep existing Work/RoomIssue detail endpoints (legacy request detail DTO intentionally has no synthetic history). `/requests/types` returns only the new directly creatable refill/replenishment types; combine existing authorized generic/room type catalogs rather than inventing application types.

## HTTP

- GET `/api/v2/requests?branchId=&queue=mine|action|progress|completed|rejected|all&page=&pageSize=&search=`
- GET `/api/v2/requests/types?branchId=`
- GET `/api/v2/requests/{workId}`
- POST `/api/v2/requests/refills`: allocationId, items[{itemId,quantity}], reason, operationKey
- POST `/api/v2/requests/replenishments`: branchId, warehouseId, items, reason, operationKey, optional priorityId
- POST `/api/v2/requests/{id}/actions/{claim|release|approve|reject|issue|cancel}`: rowVersion, operationKey, optional reason/approvedItems. Approvals can reduce positive quantities but must cover every requested line; no added item.
- POST `/api/v2/requests/{id}/receive`: rowVersion, operationKey, actual items, optional supplierName/note.

## Files

- `src/HotelOps.Api/Compact/Entities/RequestWorkflow.cs`
- `src/HotelOps.Api/Compact/Data/HotelOpsCompactDbContext.Requests.cs`
- `src/HotelOps.Api/Compact/Contracts/RequestWorkflowContracts.cs`
- `src/HotelOps.Api/Compact/Controllers/RequestWorkflowsController.cs`
- `src/HotelOps.Api/Compact/Services/RequestWorkflowService.cs`
- `src/HotelOps.Api/Compact/Services/RequestWorkflowService.Actions.cs`
- `src/HotelOps.Api/Compact/Services/RequestWorkflowService.Domain.cs`
- `src/HotelOps.Api/Compact/Services/RequestWorkflowVisibility.cs`
- `src/HotelOps.Api/Compact/Services/DocumentService.ReplenishmentWorkflow.cs`
- `scripts/database/compact-v2/p0-requests.sql`
- `scripts/verification/p0-request-workflow-contract.mjs`

## Verification

- `node --check scripts/verification/p0-request-workflow-contract.mjs`: **Passed**, exit 0.
- Build: **Not Run** (parent owns build/integration).
- SQL/API/concurrency tests: **Not Run** (migration and host hooks not applied by worker).
- Contract exports real API scenarios for refill routing, cross-branch denial, stale version, retry/no double issue, independent replenishment approval/no fabricated stock, partial receipts, retry/no duplicate receipts, exclusive concurrent claims and rejection reason/no stock change. Parent must supply authenticated real-role clients and an explicitly prepared clone fixture with genuine stock; contract creates no seed stock.

## Limits / unresolved

- No decision was invented for ordinary MR approval, missing-durable catalog expansion, auto-expiry handling or automatic warehouse selection.
- Domain-specific room request transitions and generic API bypass guards require the parent hooks above; the module must not be treated as end-to-end enabled until these are wired and verified.
- Legacy history is deliberately not rewritten or guessed. No new application/leave request type is introduced.
