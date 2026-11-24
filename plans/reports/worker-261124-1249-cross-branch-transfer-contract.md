# Cross-branch transfer backend integration contract

Status: backend implementation in progress; parent must build and run isolated API verification before deployment. No SQL/schema/entity changes or live data changes performed by this worker.

## API

- `GET /api/v2/documents/transfer-catalog?branchId={destination}` requires the destination OM role and `TRANSFER_REQUEST_CREATE`. Returns `warehouses` (warehouseId, branchId, name, branchName), active `items` (itemId, code, name, baseUnitId), `units` (referenceId, name, baseUnitId, conversionFactor), and `balances` (warehouseId, itemId, availableBaseQty). It exposes no ledger or unrelated documents.
- `POST /api/v2/documents`: existing payload now accepts optional `destinationBranchId`. For `TRANSFER_REQUEST`, branchId is the **source** branch; destinationBranchId is the requester OM's branch; sourceWarehouseId and destinationWarehouseId must belong to those branches. No department/source-work/source-document links permitted on the request. OperationKey must be supplied by client for replay protection.
- Add lines with existing `/documents/{id}/lines`. Requested quantity is populated for transfer requests. Submit using existing `/actions/submit`.
- Source OM alone approves/rejects with existing `/actions/approve|reject`, rowVersion + operationKey. Independent creator enforced. ApprovedLines can reduce requested quantities. All-zero approval and insufficient current stock rejected. Approval does not move stock.
- Source storekeeper calls `/derive` on approved request with `kind: TRANSFER`, existing line IDs and selected quantities. Post child using `/actions/post`. Source stock decreases only at post. Partial dispatch supported; approvals and cumulative quantities revalidated at posting.
- Destination storekeeper calls `/derive` on posted TRANSFER with `kind: TRANSFER_RECEIPT` and actual quantities (partial allowed); `/actions/post` increases destination stock only. Overreceipt and duplicate/concurrent posting fail without duplicate movement.
- List kinds TRANSFER_REQUEST, TRANSFER, TRANSFER_RECEIPT requires explicit participant branchId and returns source/destination related documents only. Other document kinds retain branch scope.
- Detail adds `transfer`: sourceBranchId, destinationBranchId, workflowStatus, canApprove, canReject, canSubmit, canEdit, canCancel, children. Request workflowStatus projects PREPARING/IN_TRANSIT/COMPLETED without inventing stored statuses; transfer children use existing receipt progress projection. `inventory.derivations` includes TRANSFER or TRANSFER_RECEIPT only for the matching storekeeper branch.
- Cancel request is permitted to creator before any dispatch, including APPROVED requests. Existing prepared drafts become unpostable after cancellation. Cross-branch reversals are blocked because no approved compensation workflow exists.

## Parent integration required: notifications

No NotificationService edits made (parent ownership). Notifications retain `branch_id == linked document.branch_id`, preserving the existing composite FK. `NotifyTransfer` addresses active OM/storekeeper participants in both source and destination branches, with `TRANSFER_` notification prefix.

1. In `NotificationService.Visible`, for a document of kind TRANSFER_REQUEST/TRANSFER/TRANSFER_RECEIPT, call `DocumentService.CanReadTransfer(db, access, d, ct)`; helper is static to avoid DI cycle.
2. `Inbox` and `UnreadCount` branch filtering must include transfer notifications whose linked document participates in the requested branch (source header branch OR destination_branch_id OR receipt.source_document source branch), while preserving recipient filter and subsequently calling Visible. Do not globally ignore branch filters.
3. Notification title currently document number; frontend should label TRANSFER_SUBMIT / APPROVE / REJECT / POST / CANCEL contextually. No notification FK changes needed.

## Build and fixture verification coordination

Worker is prohibited from running shared build or changing the clone. Parent runs build and the provided isolated API test after role migration. Existing Material Request approval still references DEPT_MANAGER in DocumentService.cs; parent needs OM consolidation change there after worker handoff (not changed by this transfer scope).

Test: `frontend/tests/compact-cross-branch-transfer-live.mjs` (run from frontend). `P0_TRANSFER_FIXTURE` names a local JSON file with `database: HotelOpsCompactWorkdayTestP0261124`, `baseUrl`, `source: {branchId,warehouseId,omToken,storeToken,secondWarehouseId?}`, `destination: {branchId,warehouseId,omToken,storeToken}`, `itemId`, `unitId`, `outsiderToken` (active user with no source/destination transfer read). Tokens are never printed. itemId/unitId must be an active item and its base unit. Optional secondWarehouseId enables existing intrabranch regression. Production ports 5080/5769/5186 are blocked. Test seeds 100 units using stock receipt API only in isolated host, then verifies request/approval balances, scope denials, source-only dispatch, partial and concurrent destination receipt, conservation, completion, rejection, cancellation of approved prepared request, insufficient stock. Output `artifacts/p0-workflow/transfer-api-results.json` (override P0_TRANSFER_RESULT).

Syntax verification: `node --check frontend/tests/compact-cross-branch-transfer-live.mjs`. Backend build and API execution deliberately NOT RUN by worker; parent must run them. Inactive warehouse/item and changed conversion factor guards are implemented but need fixture toggles for endpoint coverage, and notification delivery needs the parent integration above before verification.
