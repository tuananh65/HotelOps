# SHARED-ROUTER-029A â€” Local minimal host candidate

Baseline: develop 6143218925fddb2e9927a5d053ae43041940ae41.
Accepted host reference: integration 8fd30f2f12e9dae102de2826e0be20e99e6a14ad.
Worktree: D:\HotelOps-worktrees\dev3-shared-router029a.
Branch: feature/dev3-shared-router029a. No commit, push, merge, deployment or live API/SQL execution.

## Composition and exact scope

MANIFEST.json lists every frontend file and SHA-256. Nine accepted source files and two historical test files are byte-identical to Git blobs at the accepted pin. AuthForms extracts AuthRouteHost's Recovery/AuthFormRoute lifecycle, without its unrelated Users/Roles imports. The original accepted AuthRouteHost is a reference, not imported wholesale.

Only existing tracked delta is frontend/src/main.tsx. OperationsRoutes preserves the eight existing operational route elements, root redirect, fallback and navigation. No FE03/FE04 business file changed. Auth bootstrap unknown/unavailable fails closed; anonymous redirects login; restricted redirects password change. Default accepted login destination /dashboard aliases /rooms/status. Server APIs remain responsible for resource permissions. Existing downstream business clients are not refactored to provide a new global 401 interceptor.

## Return URL V1 â€” PROPOSED, NOT WIRED

Local absolute-path reference and query, maximum 2048 characters; reject external/protocol-relative URL, backslash, controls/space, fragment, malformed or recursive percent encoding, dot segments and path colon. Preserve original query bytes. A second registry check requires explicit successful session revalidation and an enabled known destination. UC041/049 excluded while OFF. No URL/query/location.state grants resource authorization. SignInScreen remains byte-identical and consumes no return URL. Revalidation and destination flags must be checked again at actual restoration time when implementation is approved.

## UC041 / DEV1

Owner source pin 104dcdf55fde2b924509032f920a3266157f5fdc.
Imported exact assignment destination, candidate, client, navigation; candidateClient; ExecutionPage, InspectionDirectory, reads, shared and api. Existing api/dev1/client.ts is semantically identical to owner ignoring line endings and is NOT overwritten.
Required branchId and optional roomId use the exact owner parser. InspectionAssignmentDestination always receives enabled={false}; no candidate child or requests are mounted. onOpenInspection is wired to /inspections/{branchId}/{inspectionId}/execution, matching owner features/fe05Integration/entry.tsx. ExecutionPage is lazy-imported but guarded OFF. Callback target exists as a disabled authenticated route; execution UI is not activated. Enabling execution requires a later owner/release approval and Outlet context integration review.

## UC049 / DEV2

Owner source pin 1536f7265781ec139e4e952fe0fe89cfe95c4880.
Imported exact ReportIncidentRoute, ReportIncidentScreen, api and navigation. Parser preserves owner semantics: optional branchId, roomId requires branchId; duplicate/unsafe IDs rejected. Unknown keys follow owner parser behavior, not a stricter invented parser.
ReportIncidentRoute is lazy-imported behind immutable false capability. No report screen, GET or writer mounted by the shared candidate. Owner screen currently renders raw problem detail/title for some errors; keep OFF until targeted owner review addresses safe error handling. No owner source was changed.

## Verification

123/123 PASS, 0 FAIL, exit 0: 29 new host/return URL/parser/offline browser cases + 94 accepted Auth HTTP/state regressions. Browser host tests inject a synthetic store; accepted store/API tests independently test transport/lifecycle. They are not live cookie/TLS/SQL acceptance or full operational business acceptance.
Typecheck exit 0. Build exit 0. Scoped Prettier exit 0. git diff --check exit 0 (autocrlf advisory only). Two inherited React Router use-client bundling warnings. No backend changes, storage writes, credentials or live calls introduced by host. Browser fixtures use synthetic data and mock API failures.

## Verdict and approvals

LOCAL_HOST_CANDIDATE: PREPARED_NOT_ACCEPTED.
READY_FOR_INTEGRATION: NO â€” UC041 callback/execution composition and full UC049 owner destination import remain incomplete; return URL restoration contract approval pending. Placeholder tests prove OFF behavior only.
LOCAL_CANDIDATE_SHA: no commit created; MANIFEST SHA-256 identifies local source bytes, HEAD remains develop baseline.
Required next decisions: approve return URL V1; review imported owner dependency scope and disabled execution/report boundaries; approve later publication/integration separately. No production SignIn change, new permissions, public writer or feature activation.