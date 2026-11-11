# DEV3-NEXT-017 — UC138 internal preview presentation; PR51 hold

PR51_SAFE_MERGE_STATUS: MERGE_HOLD / RUNTIME_ACCEPTANCE_PENDING (Path B).
UC138_SOURCE_STATUS: PARTIAL / IMPLEMENTED_SOURCE_ONLY; no public API/writer activation.
Base e576e33f7d0546227414dacc2598985480df5661; branch feature/dev3-uc138-next017.

## PR51 decision

PR51 remains OPEN/unmerged at be6c7278e1f5749211e97b55c8c21c0727b31fba; no source changes in that branch. Path B selected explicitly. Coordination at Issue52 (comment id 6770424815); PR hold reiterated (comment id 6770564857). Active integration routes/frontend retain prior SHA ETag behavior because PR51 is not merged. Its change is an active-route cutover, not a dormant candidate. Prior offline tests do not authorize it.

PL/DEV1 runtime owner must provide approved disposable host/database/identity, ownership/schema metadata, fixture prerequisites, HTTPS certificate and lease. PL/business must decide access-model/cutover compatibility and DEV1/DEV3 sanitized audit-fault response (old trial expects500, proposed service returns503). Focused live acceptance: global/branch-bound admission, CSRF, create/read/update RowVersion, stale edits, duplicate create, no-op/timezone, final session/permission recheck and audit-fault rollback. Deadlock1205 FAIL. No lease, SQL/HTTPS live NOT_RUN; no connection/session-touch GET, migration, merge or deployment attempted.

## UC138 real source gap delivered

Existing ShiftSchedulePlanner and ShiftPlanPreviewService already perform trusted-timezone materialization, eligibility/template/scope diagnostics and existing/proposed overlaps. Their HTTP admission/resource authorization, version and writer policy are still pending per DEV3-NEXT-008 handoff; no new approval found in source. Do not expose their internal readers as public APIs.

Added ShiftPlanPreviewPanel: an unmounted AuthProvider-bound batch-draft composition and preview-results flow. Users can add/remove up to500 employee/branch/department/template/date drafts, validate safe IDs/real calendar dates, and review server-produced UTC intervals and all10 existing diagnostic categories. It does not calculate timezone/overlap/permission in the browser, generate identities, infer branch authority or use prototype services/mock roles. Manual IDs are candidates only. Server must authorize every resource before preview.

No HTTP route is invented. PreviewAdapter is an explicit injected internal boundary returning unknown, not a public API contract. Default adapter absent: preview unavailable, no request. No production route imports this panel. Tests inject synthetic adapters only. It is not real runtime API integration. A future approved adapter must perform server authentication/resource admission and map the approved transport without widening this internal DTO into authority.

Internal projection uses existing C# ShiftPlanPreview defaults (Web camel-case names, numeric enum values0..9), not new public error codes. Actual C# serialization compatibility checks added to existing backend verifier. Runtime validator requires exact row cardinality/index, known unique issues, valid positive UTC ranges or null unresolved intervals, and RequiresServerRevalidation=true. It strips arbitrary fields; clear diagnostics are never CanSave/version/authority. Immutable draft snapshots and response projections, single-flight preview (no queue/retry), AbortController/epoch invalidation on edits/removal, and session/adapter-key replacement prevent late results from restoring stale presentation. No Save/Create/Start/End action exists. State is memory-only.

## Current verification — all exit0

- dotnet build HotelOps.slnx: PASS,0 warnings/errors.
- ShiftPlanPreview.Verification:57/57 PASS (prior52 +5 internal presentation/serialization checks); fake-reader behavior and actual offline EF translation, not SQL execution.
- ShiftSchedulePlanning.Verification:24/24 PASS.
- node --test tests/shift-plan-preview.test.mjs tests/shift-plan-preview-browser.test.mjs:61/61 PASS (49 model +12 offline browser).
- node --test tests/auth-http.test.mjs tests/auth-state.test.mjs tests/workday-api.test.mjs:129/129 PASS.
- npm.cmd run typecheck, npm.cmd run build: PASS; two inherited react-router use-client bundling warnings.
- Scoped Prettier and C# whitespace verify: PASS.
- dotnet test HotelOps.slnx --no-build:0, no framework cases reported; not acceptance.
- Allowlist, whitespace and secret-pattern scan before commit.

Tests cover bound/immutable drafts, leap years, malformed IDs/dates/results/UTC intervals, all internal issue values, clear/overlap display, missing adapter, sanitized errors/no retry, double preview, edit/removal cancellation, restricted/anonymous, identity replacement and production bundle exclusion. No full historical regression repeated.

## Boundaries and remaining owners

Six-file delta: two new frontend source files, two new focused tests, existing backend verification Program only, and this handoff. All production backend source, Program/DI/schema, accepted Auth/Users, UC012/UC013/FE02, FE12 read/router/flags/scheduled candidate and DEV1/DEV2 writers unchanged. WorkdaysController NonController and Workday writes OFF; UC008 Update/UC010 revoke gates preserved.

UC138 public adapter/API blocked by PL/Security admission and branch/department/employee visibility approval plus TL HTTP contract; roster/template version and final transaction protocol by TL/DEV3 with affected writer owners; atomic audit/outbox/notification by DEV3 and notification owner; live SQL/schema/races/rollback by named PL-approved trial owner. These decisions are not resolved by preview UI or existing reader. SQL_ADAPTER_ACCEPTANCE: NOT_RUN; UC138 not COMPLETE_VERIFIED.

NEXT_UC: UC140; select a source gap only where its existing contract is approved, retaining notification/authorization/write gates wherever unresolved. No new permission, timezone, version or notification semantics are assumed.

## Exact source/test SHA-256 (LF)
- frontend/src/workday/shiftPlanPreview.ts: CA33CFB5A65F151D0835B1D8B720680DA63519161EBE0BBBC79ECBA1F8C2936F
- frontend/src/workday/ShiftPlanPreviewPanel.tsx: 7341FCF57183D2C9185BEDDFBBF99BD92718F8D6D3CA8F24710FD66ECC72ECCF
- frontend/tests/shift-plan-preview.test.mjs: FB193D90D211CFFE2D6591A2C4DE7CC353E3119E78A78188D943C91D9F780B87
- frontend/tests/shift-plan-preview-browser.test.mjs: DEABB1C76382D2F49B6AE3D62B959D583FA416478780EAFEA01C499C46EC8175
- scripts/verification/ShiftPlanPreview/Program.cs: 034732D2FCC6A17DEDB9F6CEF4E5BAB4BC1DCB1DEF61F57A756522116FD00043
