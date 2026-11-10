# [HotelOps FE05] FAST015 — PARTIALLY_VERIFIED

## Identity / preservation

Worktree: D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST015.
Branch: feature/dev1-fe05-reinspection-workflow.
Exact parent: c235e23e5f52428554490cab2fc93c65164533ff (FAST014).
Scoped private branch commit/push is authorized; no merge/deploy/force-push. All old worktrees, uncommitted INSP source and old trial databases are retained. New source inventory and SHA256 are in source-manifest.csv; commit identity is provided by Git/final delivery, not a self-referential manifest.

## UC047 implemented

- New internal workflow list/read/bind/save methods in the existing RoomTypeCreationService reuse existing BatchAsync: one owned SERIALIZABLE transaction, real SQL session + current-user identity, Users-first lock, existing INSPECTION_PERFORM branch checks before/after, and existing rollback/commit behavior. No new auth/transaction framework.
- Reuse FAST014 persisted component, trusted source reader, complete exact selected-defect validator and aggregate invalidation. All client identities/revisions are checked against persisted Inspection/Rework/current task/cycle, assigned reinspector and separation of duties. Original and assigned versions stay separate, including Retired referenced versions; no latest lookup/backfill.
- Candidate DTO/controller calls the real service. [ApiController]/[FromBody] prepare JSON binding, [NonController] keeps production MVC discovery OFF. No Program.cs, DI, shared auth/CSRF/router, EF entities/mappings or SQL schema changes.
- Candidate route prefix `/api/branches/{branchId}/reinspection-workflow-candidate`: GET collection; GET `/{inspectionId}?reworkId=`; POST `/{inspectionId}/bind`; PUT `/{inspectionId}/results`; POST `/{inspectionId}/conclude?reworkId=`. These are NOT active production endpoints.
- Bind body: reworkId, inspectionRevision, reworkRevision. Save body: reworkId, reworkRevision, bindingRevision, decisions[{defectId,fixed}]; root revision comes from strong If-Match. Missing header 428; stale root/binding 412; incomplete/foreign/duplicate decisions 400; branch/ownership/grant denial 403; duplicate binding 409; lifecycle Conclude 503 after authorized read. No lifecycle transition or success receipt is fabricated.
- Detail returns original/assigned version IDs, pinned assigned checklist criteria, original selected defect description/criterion and remediation note, persisted Fixed/Not Fixed + actor/time, fresh Inspection/Rework/binding revisions. No evidence bytes/URLs. List uses explicit persisted bindings assigned to actor and branch/current cycle, not inferred same-task/latest pairing.
- Actual typed client reuses existing same-origin cookie/CSRF transport, validates response identities/strong revisions and exact decisions, sends current If-Match + Rework/binding tags, and never automatically retries an uncertain mutation.
- Unmounted React workflow loads assigned candidates, opens exact source/checklist/selected defects, selects Fixed/Not Fixed, saves complete decision set and reloads persisted state. A stale/unknown outcome requires reload before another save. Saved actor/time is shown. No mock store; Conclude stays disabled. Candidate list currently has no pagination and exposes only already-bound sources, not an assignment queue.

## UC044 / UC045 / UC046

New UC044 review/confirmation UI candidate shows assigned version and mandatory-PASS eligibility, retains operation UUID per source revision and resets review when revision changes. It is unmounted, receives source props and has no mutation transport or frontend authorization authority. Actual trusted preflight, canonical identity, domain writer, durable/result/replay/receipt/commit-guard composition and original FE04 projection are reused unchanged. No accepted Passed event/profile/issuer is present, so completing a real durable issuer binding cannot safely be claimed; ClosedInspectionApproveBinding remains closed. Exactly one DECISIONS.md record addresses Passed recipient/event/staging, optional FAIL and other remaining approvals.

UC045/046 caller/issuer/N01/R03/R05/prelock/recovery/storage/owner-driver source is unchanged. No new permissive participant, authority adapter, permission or notification graph. Recipient acceptedConcreteBinding remains null; private evidence=false. Mutable bytes after lease release remain a real storage blocker. N01 L1 24 and L2 14 remain NOT_RUN. Successful authoritative Approve/Reject/Rework commit and independent FE04 consumer/projection remain NOT_RUN; handoff NOT_READY.

## Actual verification

| Check | Result | Credit/limit |
|---|---|---|
| Backend solution build | PASS, 0 warnings/errors | Final source, backend-final.log |
| Focused console harness build | PASS, 0 warnings/errors | workflow-harness-final.log |
| `dotnet test HotelOps.slnx` | Exit 0 | API-only solution, no claim of discovered functional tests |
| Candidate controller→real service→SQL | 20/20 PASS | Final workflow-final.log and receipts/workflow-results.json; direct action invocation, not HTTP middleware E2E |
| Typed client/API + UC044 review | 17/17 PASS | Actual client with controlled fetch responses; not live backend |
| React/browser + typed transport | 5/5 PASS | HTTP interception; no live SQL/cookie middleware credit |
| Frontend TypeScript/Vite build | PASS | Candidate TypeScript included in compilation; unmounted components are not shipped via shared routes |
| Authoritative public lifecycle / HTTPS / CSRF middleware E2E | NOT_RUN | Public routes OFF; required schema/staging/authority/storage missing |
| Historical unrelated suites / N01 L1/L2 | NOT_RERUN / NOT_RUN | No source changes requiring historical rerun; original gates unchanged |

Final SQL checks: authorized bind commit + audit; duplicate bind 409 without audit duplication; invalid branch 400; assigned list; Not Fixed commit with actor/time/revisions; different physical connection reload; stale root 412; stale binding 412; missing If-Match 428; incomplete decisions 400; wrong branch 403; missing session 401; actual account without grant 403; All Fixed save without lifecycle conclusion; two concurrent saves yield one commit + one stale rejection; explicit rollback restores results/root/binding/audit; Conclude 503 with unchanged Inspection/Rework phases; wrong persisted reinspector 403; missing current cycle fails closed; production discovery OFF.

This is a successful isolated candidate business-storage transaction, not a successful authoritative UC047 lifecycle or FE04 proof. Synthetic original FAILED/Rework/remediation fixtures are clearly test setup, not trusted issuer acceptance. Service reads/writes use real SQL-created committed sessions and existing grants, no fake permission provider. Controller tests construct the real protected-ticket principal after SQL session commit but invoke actions directly: authentication-cookie encryption/middleware, actual HTTPS routing and CSRF enforcement are NOT verified here.

Final owned SQL target: DESKTOP-JI8U50T\SQLEXPRESS, HotelOps_Canonical_20261110_123615_24e3668c_Fresh; owner 3ce45781-28c5-461f-8a97-e185a6052f5a. Server/database/owner are checked before mutations and on each separate connection. Initially empty personnel; synthetic-only baseline/schema installer and test fixtures. First final-DTO run 18/18; final error-handling run 20/20 uses the untouched second synthetic source in this same owned trial, not a reset/reuse of any older task database. Final check intentionally removes only its fixture current-cycle link to assert fail-closed; synthetic trial data is retained. No shared/appsettings connection string is used.

Earlier owned trial HotelOps_Canonical_20261110_122134_1ea1ef08_Fresh and all old INSP/FAST trials remain intact. A second setup attempt correctly refused existing release receipt paths; its newly created partial trial is retained, not reset/dropped. Final verification uses a new unused receipt directory and successful new schema installation. Sanitized environment/install/workflow receipts are committed; runtime.private.json, session credentials, raw SQL connection configuration, certificates and TestResults are excluded.

Build/test development failures retained locally: initial test accessor signature did not match ICurrentUserAccessor (fixed); one static frontend source-name assertion was removed in favor of actual browser behavior; initial browser interception matched `/src/api` modules and prevented UI loading (restricted to pathname `/api/`, then all 5 PASS). No application bypass was used. npm install reports existing deprecated packages (inflight/glob/rimraf/fstream/uuid/lodash.isequal); no dependency upgrades. Frontend build warns that react-router `use client` directives are ignored; Playwright logs NO_COLOR/FORCE_COLOR warning. Backend final build/harness have zero warnings. No claim every intermediate run passed.

Concurrency evidence is limited to two same-source service saves using existing Users/catalog/source locks. It is not a deadlock-free guarantee across all cross-module child-first writers. BatchAsync preserves unknown commit errors; candidate UI never silently retries and requires persisted reload. Explicit rollback testing covers results/audit/source invalidation; no durable lifecycle replay or notification/projection atomic acceptance is inferred.

## Delivery / progress

Candidate user capability: in an isolated candidate host an assigned inspector can review the pinned checklist and selected remediated defects, save Fixed/Not Fixed and see the committed results again. This capability is deliberately not mounted in production. All Fixed / Not Fixed conclusions, Rework Completed/Failed, new-round UC046 and READY remain closed until explicit accepted policies/schema/participants.

Technical estimates, not acceptance: FAST015 85%; UC041 70% unchanged; UC044 72% (review UI/preparation only); UC045 80% unchanged; UC046 80% unchanged; UC047 65% (functional candidate service/client and committed storage verification, no accepted lifecycle/public workflow).

FE04_DEPENDENCY_STATUS: R03/R05_PINNED_R05_INTEGRATED_VERIFIED_ISOLATED / NOT_READY_FOR_AUTHORITATIVE_HANDOFF. R05 95f7422e284bc81bcfcf9f460dace52022de5cc7 remains pinned; no AUTHORITATIVE_INSPECTION_HANDOFF_READY or UC ACCEPTED.
