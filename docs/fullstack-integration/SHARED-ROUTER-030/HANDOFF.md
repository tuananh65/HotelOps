# SHARED-ROUTER-030 — Shared Auth Host / Return URL V1

## Baseline discrepancy and preservation
Fresh fetch: origin/develop = 6143218925fddb2e9927a5d053ae43041940ae41. Contrary to the prompt's reported 029 baseline, this Git tree has no frontend/src/auth files. Therefore this PR imports the minimal accepted dependency closure prepared in 029A, not the platform tree. Existing ENV/DEV1/DEV2 worktrees and uncommitted source are untouched. Only existing tracked modification is frontend/src/main.tsx; eight operational route elements, root redirect, navigation and not-found behavior are retained in OperationsRoutes beneath the authenticated boundary. No operational business component, backend, schema or certificate changed.

Auth core/screens reference: 8fd30f2f12e9dae102de2826e0be20e99e6a14ad. Provider/store/transport/contracts and three auth screens are copied unchanged. SignInScreen's only functional delta delegates success navigation to LoginRoute, retaining validation and error handling. AuthForms preserves accepted route admission/retention for profile/change-password. AuthRouteHost is a minimal composition, not the large platform host with Users/Roles imports.

## Return URL V1
Protected deep links redirect to /login?returnTo=<encoded local path and query> only after registry validation. Unknown/disabled destinations are not restored. Authentication does not imply resource authorization.

Accept leading-single-slash local path/query, length <=2048; reject external/protocol-relative URLs, schemes/path colon, backslashes, control/space, fragments, malformed/recursive encoding and dot segments. Reject credential/token/secret/authorization and nested redirect query keys. branchId/roomId, when present, must be unique canonical positive JS-safe IDs. Query bytes are preserved without normalization. Destinations are limited to current operational routes/profile and numeric cleaning/rework detail IDs. Login/forgot-password/change-password loops and UC041/049 are excluded. No arbitrary same-origin URL is implicitly enabled.

After successful login or known-session entry at /login, LoginRoute performs GET session revalidation before redirect. Restricted sessions go to change-password. Revalidation failure shows explicit recovery and restores no protected destination. Success rechecks the destination registry and identity/expiry; invalid returns fallback /dashboard, whose operational landing is /rooms/status. No password, token, storage or location.state is used as authorization. No POST retry is introduced. StrictMode does not duplicate the login restoration flight.

## Owner destination boundaries
DEV1 source pin 104dcdf55fde2b924509032f920a3266157f5fdc: UC041 destination, parser and minimal execution/client closure imported byte-identically; existing api/dev1/client.ts is preserved. /inspections/assignment uses enabled=false. onOpenInspection targets /inspections/{branchId}/{inspectionId}/execution, matching DEV1 entry. Execution UI remains OFF.
DEV2 source pin 1536f7265781ec139e4e952fe0fe89cfe95c4880: UC049 route/screen/API/parser imported byte-identically. Outer incident capability remains immutable false. No screen/request/writer is activated. Existing owner error mapping can expose raw problem text for some responses; activation requires owner correction/review. Execution activation also requires owner Outlet context integration review.
No owner business source edited; no backend attribute, permission or Notification 503 behavior changed. Disabled destinations are excluded from return-URL restoration. Frontend chunks in a build do not grant server access.

## Verification
Final commands and exact source hashes are recorded in MANIFEST.json. Focused combined suite: shared-auth-host (45), Auth HTTP/state (94), Room Status/Actions (16) = 155 PASS, 0 FAIL, 0 cancelled, 0 skipped. The three real AuthApi/store browser cases use offline fetch responses, not live cookies/TLS/SQL. Other host cases use a synthetic store to control lifecycle transitions. No timeout/cancelled repeated. Historical 142+1 report is not claimed as a fresh run.
Typecheck/build/scoped Prettier/diff-check exit 0. Two inherited React Router use-client bundle warnings. No live backend/SQL, deployment or feature activation performed. Imported owner files retain original formatting for byte integrity; scoped formatting covers authored files only.

## Status / gates
IMPLEMENTATION_STATUS: IMPLEMENTED_SOURCE_ONLY, SOURCE_REVIEW_PENDING.
PRODUCTION_ACTIVATION: OFF. UC041/UC049 capabilities OFF.
Task implementation/verification: 100%; PR review/merge pending. No new whole-UC completion credit; live/business acceptance is unchanged.
Next: review PR into develop. Do not merge/deploy or enable owner features automatically. Production activation, destination owner safety reviews and live acceptance remain separate gates.

## SHARED-ROUTER-031 ? return-URL lifecycle correction
LoginNavigation is keyed by location key/path/query. Search-only navigation resets requested destination, attempt, target and loading state. Unmounted navigation callbacks cannot redirect. New navigation waits for any older revalidation to settle and then issues a fresh GET; it does not reuse the old flight as destination validation. Attempt generation drops superseded recovery callbacks. Redirect admission compares the exact public session snapshot rather than only user ID/expiry, covering same-account session replacement. Loading uses reactive state. No login POST is retried.

Eight additional browser regressions: A-to-B before login; pending GET switch to B; invalid destination; UC049 OFF; UC041 OFF; real-store same-account replacement; logout; and real-store query change requiring a third session GET (bootstrap, old revalidation, current revalidation). One login POST is retained in real-store race cases.
Final receipts: 163/163 (53 host, 94 Auth, 16 Room), no failures/cancellations; typecheck/build/scoped formatting/diff-check exit 0. Earlier development run: 48 PASS / 4 locator timeouts, exit 1, due non-reactive loading exposed by deferred fixture; corrected and rerun. See local ignored .test-output/shared031-final.txt for final output. No live HTTPS/SQL or operational runtime acceptance claimed.

Only two functional files changed: LoginRoute.tsx and shared-auth-host.test.mjs; this handoff and manifest updated as evidence. All other PR source is unchanged. UC041/049 remain OFF. AuthProvider in main is an active router cutover when deployed: FE03/DEV1 owner review and controlled HTTPS/session smoke remain separate pending integration gates. No automatic merge/deploy.
