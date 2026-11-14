# UC139 Scheduled Start/End — IMPLEMENTED_SOURCE_ONLY

Baseline: 5411c1b86006d767fbad9aaf4c78cc23c9ece3e4. Dedicated feature/dev3-uc139-scheduled worktree. No production route, controller, service, policy, schema or feature flag changed.

## Implemented contract

Standalone ScheduledWorkdayPanel uses accepted AuthProvider, cookie read clients and CSRF bootstrap transport. Requires confirmed unrestricted session plus explicit positive safe branch ID. Assigned shifts and current workday reads remain independent. Start requires a selected assignment actually returned for this branch AND an authoritative empty current read. End requires current scheduled day with no open interval and canonical eight-byte rowversion. No unscheduled start, Pause All, interval close or handover.

POST /api/branches/{branchId}/workdays sends exactly {shiftAssignmentId}. Expects 201 WorkdayResponse with WORKING, matching branch/assignment, isUnscheduled=false, null endedAtUtc and matching quoted rowversion ETag.

POST /api/branches/{branchId}/workdays/{workdayId}/end sends exactly {pauseAll:false}, canonical strong If-Match. Expects 200 ENDED response matching branch/workday/assignment, valid end timestamp and matching response ETag. Mutation WorkdayResponse is decoded separately from OwnWorkingDaySnapshot. No caller userId or optional reason is submitted.

Dispatch defaults OFF in both API factory and standalone component; default mode cannot request CSRF or POST. No production caller enables it. Synchronous ref prevents double submission; session/branch binding and abort guards discard late responses. Confirmed mutation triggers fresh own-shifts/current reads. 400/401/403/409/412 are rejected without replay. 404/5xx, malformed success, cancellation after dispatch and network failure are outcome unknown; no automatic retry and replay remains locked in that flow even after snapshot refresh. Error UI exposes no raw ProblemDetails, token or exception. No browser persistence/logging.

## Backend verification, not activation

Existing WorkdaysController keeps NonController. Existing StartAsync/EndAsync and transaction participant reused unchanged. Focused executable tests cover inactive controller metadata, HTTPS, initial missing/restricted identity rejection, strict request DTO unknown-field behavior and invalid If-Match. Source assertions separately cover actor/branch assignment checks, active template/department, one Working/open interval, user-first retained locks, rowversion update predicate, audit and enclosing transaction disposal/commit boundary. Source assertions are not SQL behavioral verification. No claim is made that real authorization, cancellation or rollback/races have passed with SQL.

## Receipts

- dotnet build HotelOps.slnx: exit 0; 0 errors, 0 warnings.
- dotnet run --project scripts/verification/ScheduledWorkday/ScheduledWorkday.Verification.csproj: 43 checks PASS, exit 0 (16 executable adapter/DTO/early-service checks, 27 explicitly labeled source/order assertions).
- dotnet run --project scripts/verification/WorkdayReadRouting/WorkdayReadRouting.Verification.csproj: 44 checks PASS, exit 0; MVC exposes only existing two GET actions, no Workday writes/collisions.
- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs: final 48/48 PASS, exit 0 (29 API, 19 offline browser).
- Earlier combined targeted run: 110/110 PASS, exit 0: 41 then-current new tests + 69 existing workday API/browser/router tests. Seven additional new browser tests then passed in the final 48-test run; production source unchanged between these successful runs.
- npm.cmd run typecheck: exit 0.
- npm.cmd run build: exit 0; two inherited React Router use-client warnings.
- Scoped Prettier and staged git diff --check: exit 0.

Initial browser selectors exposed an ambiguous select label; component now has explicit accessible name. A test attempted selecting the intentionally disabled dropdown after unknown outcome; corrected to assert the lock rather than force selection. Final runs above passed. Initial shell file-write/format command used an incorrect frontend-prefixed path and failed before browser file existed; corrected path, final formatting passes.

Live HTTPS/SQL, business authorization, scheduled success against SQL, cancellation/race/rollback and runtime mutation activation: NOT_RUN / PENDING. No approved trial lease or shared runtime use. No dotnet test empty-discovery receipt is counted as acceptance.

Status: source-ready candidate for review. UC139 remains PARTIAL; not COMPLETE_VERIFIED. Production mutation endpoints remain inactive; frontend dispatch remains OFF; no merge/deploy authorization.
