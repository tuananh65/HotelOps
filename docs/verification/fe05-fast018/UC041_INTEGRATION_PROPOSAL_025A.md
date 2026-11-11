# FE03-UNBLOCK-025A — UC041 integration boundary proposal

Status: PROPOSED / NOT APPROVED. Date: 2026-11-11.
Worktree: HotelOps-DEV1-FE05-FAST018.
Branch: feature/dev1-fe05-source-consolidation.
Inspected HEAD: 940b26b06f4bd19c457cdfea71a81a6d2c59ae19 (clean before this task).
FE03-DEPENDENCY-024A is a dependency handoff, not contract approval.

## Concrete decision requested from Project Leader

Approve the FE05 destination `/inspections/assignment?branchId={branchId}&roomId={roomId}` and the behavior below. `roomId` is optional for a branch queue, required for navigation originating from a room. Example syntax only: `/inspections/assignment?branchId=12&roomId=34`.

FE05 owns destination screen/context and backend resource checks. DEV3 owns registration inside the actual application authentication/router boundary and restoration of this local URL after sign-in. FE03 owns only the navigation caller after approval. Project Leader approves this boundary. Approval of this proposal does not approve production command dispatch, schema, permissions or notification activation.

- `branchId` is required; each supplied ID must occur exactly once and be a canonical positive decimal safe integer. Reject missing/empty branch, empty room, duplicates, zero, negative, fractions, exponent notation and unsafe integers before API calls. Do not infer branch from room, session default or previous screen.
- No task/inspector/supervisor/checklist/cycle/revision/confirmation is accepted from navigation. Reject unsupported query keys rather than interpreting them as workflow input. No auto-select, auto-open or auto-assign, including the single-candidate case.
- Direct-open, reload and browser back/forward use the same validation/authenticated loading path as FE03 navigation. A branch/room change clears prior results, selection, confirmation and errors and cancels/ignores prior asynchronous responses.
- Backend validates the actual session, existing branch grants and room membership/activity before returning room context. UI filtering is not resource authorization. Preserve current candidate queue intersection of INSPECTION_PERFORM and INSPECTION_VIEW; do not invent an ASSIGN permission or broaden access.
- For a valid authorized room, show all eligible pending candidates for that room with explicit task selection; zero candidates is an empty state, not permission to substitute another room/task. Assigned-self entries remain explicitly opened by persisted InspectionId. Multiple candidates are listed, never reduced to first/latest.
- Selecting a task reloads authoritative options and verifies returned room matches the requested room. Server resolves task -> room -> branch/current cycle/checklist and eligibility. Client IDs are context, not authority. Keep explicit inspector and supervisor selection, due/reason, review and confirmation.
- Proposed room-context read extends the existing gated candidate queue with optional `roomId`, server validation and filtering. It must distinguish an authorized empty result from nonexistent/inactive/out-of-branch room according to approved error semantics. Current API has no room-context input; this change is pending approval, not implemented or registered.
- Error proposal: malformed context 400; unauthenticated 401/sign-in via DEV3; denied branch 403; nonexistent or out-of-branch room 404 after branch authorization (no cross-branch existence disclosure); inactive room or ineligible task 409; stale revision 412; missing If-Match 428; unavailable notification 503. A failure clears/disables the stale form; it never falls back to the full branch or automatically retries a mutation.
- POST remains gated with task If-Match, cycleRevision and checklistVersionId, real CSRF and authoritative preflight. Existing controller is NonController; public API activation is a separate owner-approved integration change. Candidate-host tests do not prove public routing or committed assignment.

## Affected files after approval (not changed by this proposal)

| Owner | File | Planned change |
|---|---|---|
| FE05 | frontend/src/features/inspectionAssignment/InspectionAssignmentDestination.tsx (new) | Query-driven destination and invalid/direct-open states |
| FE05 | frontend/src/features/inspectionAssignment/navigation.ts (new) | Exact parser and URL builder |
| FE05 | frontend/src/features/inspectionAssignment/InspectionAssignmentCandidate.tsx | Optional room context, explicit candidate handling and response invalidation |
| FE05 | frontend/src/features/inspectionAssignment/client.ts | Typed room-context read and context consistency checks |
| FE05 | src/HotelOps.Api/Controllers/InspectionAssignmentCandidateController.cs | Optional room query passed to service; retain NonController until separate activation approval |
| FE05 | src/HotelOps.Api/Services/Housekeeping/InspectionAssignmentDomainComponent.cs | Authorize/resolve room before candidate query; reuse preflight/guard |
| FE05 | src/HotelOps.Api/DTOs/Housekeeping/InspectionAssignmentCandidateDtos.cs | Room-context response if required by approved read contract |
| FE05 | frontend/tests/inspection-assignment.test.mjs, frontend/tests/inspection-assignment.spec.ts; dedicated backend room-context tests | Parser/context/error/candidate coverage |
| DEV3 | frontend/src/main.tsx on this baseline; exact integration-router file to be confirmed by DEV3 | Protected route registration, sign-in restoration and public direct-open tests |
| FE03 | Caller file to be identified by FE03 owner | Approved URL navigation only; no FE05 changes in FE03 |

No shared SQL, generated entities, mapping, notification profile or permission changes are proposed in this patch. No production route is mounted.

## Delivered within existing FE05 contract

Before every task-options load, clear the previous source, inspector, supervisor, due time, reason and confirmation, and block the form until a fresh source succeeds. Failed loads cannot leave the previous task form visible; successful switches cannot reuse the previous task's due/reason. This also applies to explicit source reload. Added browser regressions for successful switch and 404/412 failure; these are intercepted HTTP tests, not live backend authorization evidence.

Changed files in this task:
1. frontend/src/features/inspectionAssignment/InspectionAssignmentCandidate.tsx
2. frontend/tests/inspection-assignment.spec.ts
3. docs/verification/fe05-fast018/UC041_INTEGRATION_PROPOSAL_025A.md

Validation: FE05 Vite build PASS (no warnings); TypeScript --noEmit PASS; focused client tests 14/14 PASS; focused Edge browser tests 8/8 PASS (three new cases). Node emitted NO_COLOR ignored because FORCE_COLOR is set. No C# source changed; dotnet build/test NOT_RUN. Live API/database/public-navigation/committed-assignment tests NOT_RUN. No previous FAST018 receipts are credited as new verification.

## Acceptance tests after route/read-contract approval

Public application URL via FE03 caller and direct-open/reload/back; real sign-in restoration; malformed/duplicate/missing/unsafe IDs without network side effects; unauthorized branch and mismatched room; zero/one/multiple candidates with no automatic options read or POST; task options returning a different room; stale task/cycle/checklist; rapid context changes with delayed old responses; CSRF and real authenticated preflight; mandatory 503 without persisted mutation or retry. Successful committed workflow requires separately accepted notification composition and fresh live persistence/atomicity/replay checks.

## Notification blocker and delivery status

Refer to NOTIFICATION_AND_LIFECYCLE.md. Inspection Assigned event/version/producer/read-policy and recipient profile, pre-Inspection task/cycle operation reservation, atomic staging/durable receipt/replay/guard, explicit-supervisor schema and public initial/reassignment command contracts remain unaccepted. Existing Failed/ReworkRequested support is not an assignment binding. Do not reuse Cleaning Assigned or bypass guard.

UC041_ROUTE_STATUS: PROPOSED_PENDING_PL_AND_DEV3; development /assignment only; production route absent on inspected baseline.
ROOM_CONTEXT_CONTRACT: EXACT_PROPOSAL_ABOVE_PENDING_APPROVAL; current entry supports branchId only.
NOTIFICATION_BLOCKER: UC041_ASSIGNMENT_NOTIFICATION_NOT_ACCEPTED; 503 guard unchanged; no new live POST claimed.
INTEGRATION_SHA_OR_PR: NONE; uncommitted FE05 patch on 940b26b06f4bd19c457cdfea71a81a6d2c59ae19.
READY_FOR_FE03: NO.