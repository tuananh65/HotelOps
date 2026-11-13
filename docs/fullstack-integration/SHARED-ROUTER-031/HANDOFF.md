# SHARED-ROUTER-031 — Final consolidated candidate

Canonical PR: #64, target develop. Current develop: 6143218925fddb2e9927a5d053ae43041940ae41. Accepted PR64 functional source: 9d97635261132bcd84bea1c47c8391e5f2a24567. Historical PR65: f96dd50b94b9fe4cde45f5c49cda92cecc045803. Fresh remote fetch and both open PRs verified on 2026-11-11. Neither PR is merged. PR65 must remain historical; do not merge or close independently without Leader decision.

## Consolidation and exact allowlist

No missing frontend dependency was found: all 31 historical frontend paths exist in PR64. No functional source change or merge of the older candidate is necessary. Twenty-five of the 31 files remain byte-identical. Six are superseded by accepted PR64: main.tsx, SignInScreen.tsx, OperationsRoutes.tsx, SharedAuthHost.tsx, returnUrl.ts, shared-auth-host.test.mjs. Two additional frontend files are AuthRouteHost.tsx and LoginRoute.tsx. All 33 final frontend files remain byte-identical to accepted PR64 HEAD, including its navigation lifecycle fix. No new provider, route or navigation handler is introduced in this revision.

Historical 25 imported identities: 24 remain exact; SignInScreen alone has the already accepted PR64 success-navigation delegation to LoginRoute. Do not claim 25/25 unchanged for this final composition. All FE05/FE06 imported business sources remain byte-identical; the 24 unchanged Auth/owner/test imports match their exact pins in SHARED-ROUTER-030/MANIFEST.json. No FE03 business source changed. Owner reviews already accepted by Leader are reused; no repeat review requested.

This revision adds exactly four documentation files:
- docs/fullstack-integration/SHARED-ROUTER-029A/MANIFEST.json
- docs/fullstack-integration/SHARED-ROUTER-029A/HANDOFF.md
- docs/fullstack-integration/SHARED-ROUTER-031/MANIFEST.json
- docs/fullstack-integration/SHARED-ROUTER-031/HANDOFF.md

The two 029A documents are copied byte-identically from PR65 as historical receipts, including their historical status wording. They are not current execution or activation authority. The final manifest records all 31 historical and current SHA-256 pairs with explicit supersession. Existing 030 receipts remain intact. Final PR scope: 33 frontend files and six documentation files, 39 paths total. No backend changes.

## Auth and owner destinations

Production entry composition is main.tsx -> AuthProvider -> AuthRouteHost -> SharedAuthHost. SignInScreen delegates successful login to LoginRoute. Navigation-key/query remount, separate revalidation for the current destination, stale-flight suppression, exact session-snapshot admission and explicit recovery remain unchanged. Return URL validation preserves valid local pathname/query and rejects external/unsafe/loop/disabled destinations. No login POST replay or authorization inference is introduced.

All eight operational routes, root redirect, navigation and not-found behavior remain. UC041 /inspections/assignment retains branchId required, optional roomId and the exact DEV1 parser/component; enabled=false. onOpenInspection targets /inspections/{branchId}/{inspectionId}/execution, whose execution capability remains OFF. UC049 /incidents/new retains the DEV2 parser/component and immutable outer capability false; UI/dispatch OFF. No controller/notification behavior changed: existing NonController and Notification 503 remain untouched. Compatibility does not establish business acceptance.

## Fresh verification

On this isolated final worktree, combined Auth/Router/Return URL/UC041 OFF/UC049 OFF/FE03/StrictMode regression: 163/163 PASS, zero failed/cancelled/skipped (53 host, 94 Auth HTTP/state, 16 Room Status/Actions). Real AuthApi/store tests use offline HTTP fixtures, not live cookies/SQL. Includes query A->B before login, pending old GET, invalid/OFF destinations, same-account replacement, logout and fresh GET after query change. Source was not modified after this run.

Typecheck and frontend build exit 0. Two inherited React Router use-client bundler warnings remain. Scoped formatting and diff/allowlist checks are performed before publication. Initial test setup failed to resolve esbuild because a new worktree dependency junction was misplaced; corrected locally, no source change, followed by the full successful 163-case rerun. No hidden timeout/cancellation.

## Integration gates

SOURCE_READY_FOR_REVIEW: YES. READY_FOR_MERGE: PENDING_REVIEW. UC041/UC049 PRODUCTION_ACTIVATION: OFF. No deployment occurred. AuthProvider in main is a real authentication-router cutover if deployed; it is not a runtime feature flag, so controlled deployment remains a separate decision.

HTTPS_SESSION_SMOKE: RUNTIME_SMOKE_PENDING (no authorized runtime used). No SQL, certificate, environment, backend or business mutation changed. Leader review/integration decision and controlled HTTPS/session smoke remain. Task source preparation and verification complete; merge/runtime acceptance pending. No whole-UC completion credit added.

Next: Project Leader reviews canonical PR64 and decides integration after remaining smoke gate. Keep PR65 open as historical reference. Do not auto-merge or activate owner features.

Formatting receipt: scoped Prettier with --end-of-line auto PASS, preserving existing checkout CRLF and accepted Git blobs. Initial root check flagged line-ending differences; no source formatting rewrite performed.
