# DEV3-NEXT-009 — UC016 dependency review route

IMPLEMENTED_SOURCE_ONLY; source review pending. UC016 remains PARTIAL.
Baseline: 4ee4e0d3e5afd8e3a6aaaa888128bc24c3eab4e3.
Branch: feature/dev3-uc016-dependency-review.

## Delivered behavior

New authenticated routes /floors/:id/deactivation-review?branchId=... and /areas/:id/deactivation-review?branchId=... consume existing active GET /api/branches/{branchId}/floors/{id}/deactivation-dependencies and /areas/{id}/deactivation-dependencies. A loaded Floor/Area detail links to the corresponding review. Explicit positive safe IDs required; selection is not authority. Backend remains authoritative for access. Restricted/anonymous sessions send no location read.

Only counts and sanitized record fields appear. No dependency IDs, raw response, error detail, ETag, token or exception shown. Loading, denial/unavailable, empty and manual refresh supported. Empty dependency list does not approve mutation. 401 clears review and disables refresh; explicit Check session invokes accepted revalidation. Session/branch/record changes remount the bound review, clear snapshot, abort old read and reject late completion. No storage or permission inference.

The separate component imports only the accepted GET preview client. Existing candidate mutation panel/client remain unmounted. No CSRF bootstrap, POST or Deactivate control is introduced. Existing deactivation fail-closed guards are unchanged. This is an independently usable dependency-review business step, not end-to-end UC016 acceptance.

## Existing backend source authority

src/HotelOps.Api/Controllers/HotelStructureUpdateController.cs: existing two HttpGet deactivation-dependencies actions and cookie/HTTP handling.
src/HotelOps.Api/Services/HotelStructure/HotelStructureUpdateService.cs: ReadDeactivationDependenciesAsync performs existing admission/resource/transaction checks.
frontend/src/structure-deactivation/deactivationPreviewApi.ts: accepted record/branch/ETag/dependency whitelist validation, GET credentials include/no-store, no mutation authority.
No backend, policy, schema, writer, FE01 or FE12 source changes.

## Verification receipts

node --test tests/structure-dependency-review.test.mjs tests/deactivation-preview-api.test.mjs: exit 0, 31 PASS / 0 FAIL (17 route/browser/import-graph tests + 14 existing API tests).
node --test --test-name-pattern="actual entry|branch change|new entry|dashboard|UC023" tests/hotel-router-integration.test.mjs: exit 0, 8 PASS / 0 FAIL.
npm.cmd run typecheck: exit 0.
npm.cmd run build: exit 0; 2 existing React Router use-client directive warnings, build succeeds.
Scoped Prettier, exact allowlist, git diff --check and secret-pattern scan recorded before publication. Offline browser uses synthetic fetch; external network blocked, no SQL/API trial. No server authorization/SQL race/rollback/atomic audit acceptance inferred.

## Preservation and dependencies

Exactly five files in this PR: four listed in SHA block below and this handoff. Only additions to existing route host/workspace. All other tracked bytes equal integration baseline, including UC012/UC013/Auth/Users/Workday and all backend sources. WorkdaysController remains NonController; Workday writes OFF, frontend FE12 flag unchanged. No shared SQL, migrations, deployment, merge, DEV2 writer edits or trial use.
UC016 business activation remains blocked by shared placement/operational-task writer coordination and approved isolated SQL race/rollback/audit acceptance. Read review is not a final mutation snapshot and is not an ETag acquisition guarantee for future writers.

## UC138 decision register (separate from this independent UC016 slice)

Pending: PL/Security preview-management admission/resource visibility; PL/TL/DEV3 public HTTP DTO/query/error contract; TL/Security/DEV3 aggregate version/concurrency/shared writer protocol; DEV3 + notification owner atomic audit/outbox requirements; PL named disposable SQL trial owner/identity/lease. No signoff inferred. Public preview/API/writer remains unauthorized, internal PR38 only.

## Source SHA-256

DB23416B11528144170E2287F0F62D593FD9BA97A24DB81FD02B1632125AA93C  frontend/src/hotel/HotelRouteHost.tsx
1B29C12E786EC19920EFDF209BBA129F8EB013314F6D06B073FECA99BB3279DA  frontend/src/hotel/HotelWorkspace.tsx
A64A4180AB1565C5F87E3BB119C139230AEC2911F4A3FB9BA11847F769E3814D  frontend/src/structure-deactivation/StructureDependencyReview.tsx
B0E726429478E27507C2D06B727BF5C503945BABC8EC0B0BCDAC0293C062DB01  frontend/tests/structure-dependency-review.test.mjs
