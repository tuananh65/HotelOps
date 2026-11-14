# DEV3-FE02-001 — Frontend integration handoff

Verdict: **IMPLEMENTED — frontend/local, NOT ACCEPTED**. Date: 2026-11-08.

## Baseline and preservation

- Branch: `feature/dev3-platform`.
- HEAD: `e7f84422a33a8a65c771efdfb248e8231e83bb6a`.
- Before: 4 modified + 57 untracked. No reset/clean/commit/push/merge/deployment.
- `PRESERVATION.json` records all 60 accepted Auth/Users expected hashes, before/after hashes, source artifact hashes and verification observations. All 60 remain byte-identical.
- Existing accepted `src/main.tsx` and `src/auth/AuthRouteHost.tsx` are unchanged. `frontend/index.html` selects the new `src/hotel/main.tsx`; it uses the same AuthProvider and delegates non-FE02 routes to the accepted AuthRouteHost.
- The integrated dashboard adds a Hotel structure link without changing accepted screens. FE02 routes never import prototype services, mock Shell, domain permission graphs or FE04/FE05 screens.

## Coverage and progress

Percentages describe **this frontend integration slice only**: scoped operation wiring, response validation, forms and focused local tests. They are not whole-UC or full-stack acceptance percentages. Backend verification is reused, not rerun.

| UC | Integrated frontend and API | Slice progress |
| --- | --- | --- |
| UC013 | `/hotel`, `/branches`, `/branches/:branchId`; GET `/api/branches/{branchId}/structure`, node/collection selection, pagination/active filter, server visibility | 100% |
| UC014 | `/floors`, `/areas`; POST `/api/branches/{branchId}/floors` and `/areas`; all create fields, nullable parent/floor number | 100% |
| UC015 | Floor/Area editable GET and PUT on `/api/branches/{branchId}/{floors|areas}/{id}`; server ETag, reason, explicit review/confirm | 100% |
| UC017 | POST `/api/room-types`; branch, code/name, cleaning minutes, published checklist ID, description/capacity | 100% |
| UC018 | GET `/api/room-types/{id}?branchId=`; PUT same path; optional code, name/description/defaults; branch/capacity remain immutable | 100% |
| UC019 | POST `/api/room-types/{id}/deactivate?branchId=`; strong tag, confirmation/reason, sanitized active-room dependency references | 100% |
| UC020 | POST `/api/rooms`; branch/floor/area/type/number/notes; status is server-owned, initial DIRTY is represented by the test fixture | 100% |
| UC021 | GET `/api/rooms/{id}/edit?branchId=` and PUT `/api/rooms/{id}?branchId=`; metadata/references/reason only, no status or history writes | 100% |
| UC023 | GET `/api/rooms`, `/api/rooms/{id}`, `/api/rooms/{id}/history`; filters, server-authorized global mode, sort/page, recorded evidence/history | 100% |
| UC024 | POST `/api/branches/{branchId}/departments`; optional server-validated manager | 100% |
| UC025 | Department GET/PUT `/api/branches/{branchId}/departments/{id}`; full replacement nullable description/manager and ETag | 100% |
| UC142 | `/room-types`; GET `/api/room-types`; branch or server-scoped listing, literal search, active filter, sort/descending/page, legacy read-only flags | 100% |

Backend evidence: `scripts/verification/HotelStructure/TASK64_UC013_IMPLEMENTATION.md`, `TASK63_UC014_IMPLEMENTATION.md`, `TASK65_UC015_IMPLEMENTATION.md`, `TASK69_UC017_DEPLOYMENT_ACCEPTANCE.md`, `TASK70_BATCH1_IMPLEMENTATION.md`, `TASK71_BATCH2_IMPLEMENTATION.md`, and `scripts/verification/RoomDirectory/TASK67_UC023_COMPLETION.md`.

No branch-list endpoint is invented: the branch selector takes a supplied ID and loads the verified structure endpoint. Existing prototype hotel screens remain source references and are not imported into the real-auth graph.

## Security and concurrency behavior

- Reuse accepted AuthProvider/session/actions; no new Sign-In or permission evaluator.
- Cookie include, no-store, relative `/api` URLs and redirect error. Fresh CSRF bootstrap per explicit mutation; no browser storage or bearer token.
- Runtime decoders copy only DTO whitelist fields, enforce safe IDs and branch/target identity. Management reads require the backend's quoted 64-uppercase-hex strong ETag.
- Explicit Review/Confirm; edits consume old review; Enter does not save; synchronous ref consumption and busy guard prevent duplicate mutations.
- Snapshot belongs to session reference, route/query context and generation. Context/session changes abort requests and discard late responses. 401 uses bounded session revalidation, never mutation replay.
- 403/404/412/428 remove the old management snapshot; 412/428 require explicit load/review/confirm with a fresh ETag.
- Network/timeout/cancellation after dispatch, malformed success and unknown/conflicting 409 remain OUTCOME_UNKNOWN. The mounted flow stays locked after manual GET; GET cannot prove prior commit/rollback.
- Unknown lock is in memory for the mounted flow; browser reload/navigation can discard it. There is no persistent mutation journal or idempotency/recovery API. Operators must follow the displayed verification warning before another attempt.
- A successful GET does not establish write authority. Backend remains authoritative for permissions, references, checklist/manager eligibility and dependency conflicts.

## Focused verification receipts

These are command-result summaries observed in this task, not reconstructed raw logs.

| Command, from frontend/ | Result | Exit |
| --- | --- | --- |
| `npm.cmd run typecheck` | PASS on final source | 0 |
| `node --test tests/hotel-api.test.mjs tests/hotel-router-integration.test.mjs` | 124 PASS, 0 fail/cancel/skip; 70 API + 54 browser tests | 0 |
| `npm.cmd run build` | PASS; Vite 83 modules | 0 |
| `npm.cmd exec -- prettier --check src/hotel tests/hotel-fixtures.mjs tests/hotel-api.test.mjs tests/hotel-router-integration.test.mjs index.html` | PASS | 0 |

`git diff --check` is run from the repository root for the final handoff. Initial typecheck found TS2339 narrowing errors in new FE02 source; corrected before the final successful checks. Build retains two React Router `use client` warnings. Git reports LF/CRLF conversion notices.

Tests use the real accepted AuthStore/AuthProvider and real FE02 client with mocked fetch in an offline local browser. They cover every new mutation, reads/guards, CSRF/If-Match, scoped/global denial, review/Enter/double-submit, 401/403/412 handling, unknown outcomes, session replacement/unavailability, stale GET/POST and accepted Users/Profile routing through the new host.

No full frontend foundation rerun, Task44/45, backend build/tests, live HTTPS, shared SQL, migration or deployment was performed. Mocked tests do not certify SQL/RBAC transactions or live integration.

## Dependencies kept unchanged

- UC012: no branch maintenance UI added; existing backend/source status unchanged.
- UC016: no deactivation UI added. Task66 records writer-coordination BLOCKED/PARTIAL; no bypass.
- UC022/026: no room/department deactivation or dependency-policy changes.
- RoomType writes require the verified BranchV1 module in the target backend and an eligible published default checklist. No schema install or checklist creation in this task; manual checklist IDs are server-validated.
- FE05 reference supplied by PL: `integration/dev1-fe05-checklists`, commit `5f1cde86b5b6b730832da6207ef6eb3d3b15cfc9`, `docs/fe05/INT003/DEV3_HANDOFF.md`. The ref/commit object and handoff could not be read in this local checkout. No fetch/merge or FE05 integration was attempted. Reconciliation requires the supplied object/handoff to become locally available through an approved integration step.
- No FE04, FE05, DEV2, backend, permission/catalog or shared configuration source was modified.

## Exact task files

Modified: `frontend/index.html`.

Created:

- `frontend/src/hotel/contracts.ts`
- `frontend/src/hotel/hotelApi.ts`
- `frontend/src/hotel/operations.ts`
- `frontend/src/hotel/useHotelFlow.ts`
- `frontend/src/hotel/HotelWorkspace.tsx`
- `frontend/src/hotel/HotelRouteHost.tsx`
- `frontend/src/hotel/main.tsx`
- `frontend/src/hotel/hotel.css`
- `frontend/tests/hotel-fixtures.mjs`
- `frontend/tests/hotel-api.test.mjs`
- `frontend/tests/hotel-router-integration.test.mjs`
- `docs/fullstack-integration/DEV3-FE02-001/PRESERVATION.json`
- `docs/fullstack-integration/DEV3-FE02-001/HANDOFF.md`

Implementation is reviewable locally. Project Leader/independent review and separately authorized live integration remain necessary before FE02 acceptance.
