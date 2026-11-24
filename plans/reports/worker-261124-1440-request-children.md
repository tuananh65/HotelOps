# Structured room request children

Status: IMPLEMENTED; runtime NOT_TESTED. No database writes, migration, deployment or production process changes.

## Changes

- Added `WorkService.RoomIssueChildren.cs`; hooked once after root domain transition in `WorkService.RoomIssueActions.cs`.
- Existing maintenance work gets `MAINTENANCE_REMEDIATION` header linked to incident root. Accepted/start/remedy/rework/pass record structured transitions.
- Each material remediation document gets an `INTERNAL_REQUEST` child and `DURABLE_ITEM_REMEDIATION` header with immutable document reference. Superseded open children cancel; failed inspection rejects the delivery child, retaining evidence for the subsequent explicit replacement quantity.
- Each reinspection round gets a fresh `ROOM_REINSPECTION` child. Pass completes; fail rejects that round. Existing pre-cutover material/inspection paths lazily gain missing headers.
- All headers share root correlation and use `ParentRequestWorkId`; WorkItems also use `ParentWorkId`. Deterministic derived child operation keys avoid the actor-wide unique-key constraint; root replay exits before creating children.
- `RequestWorkflowService.Domain.cs` rejects generic mutation of durable/reinspection children through existing `IsManagedRequest` guards. Child claim/release is rejected with `USE_PARENT_REQUEST_WORKFLOW` so independently claiming a child cannot bypass the root handler. Maintenance generic execution remains available as required by the existing assigned maintenance flow.

## Verification

- `git diff --check`: passed.
- Node static source-contract snippet: syntax checked with `node --check`, assertions passed. This is not C# compilation or runtime verification.
- Build not run: parent integration build was active; parent must run consolidated build.
- Runtime tests needed: missing/BROKEN success and rework, multiple inspection rounds, root retry produces no extra children/events, correlation/parent scope, document reference, cross-branch visibility, generic child mutations denied, maintenance start/complete remains allowed.

## Integration notes

- Parent document guard must permit legitimate remediation document issue when `request_workflows.document_id` points to a `DURABLE_ITEM_REMEDIATION` child; typed refill/replenishment restrictions must not blanket-deny this new header.
- UI should use detail `parentRequestWorkId` to open the root room workflow for specialized child records. Generic claim buttons are deliberately unavailable for child rows.
- Direct generic maintenance start/complete is still supported; structured child timeline synchronizes when its root specialized action executes. Root `report-remedy` records pending reinspection even if generic completion was used first.

Unresolved questions: none. Runtime database trigger behavior remains unverified.
