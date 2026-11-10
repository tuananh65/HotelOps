# [HotelOps FE05] FAST016 — PARTIALLY_VERIFIED

## Identity and result

Worktree: D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST016.
Branch: feature/dev1-fe05-reinspection-conclusion.
Exact parent: 6dac65a63430ce6da07c2245a112f6f6118b3290 (FAST015).

Implemented a usable candidate Review→Confirm→persist→reload workflow for a UC047 conclusion **proposal**. All Fixed derives proposed Rework COMPLETED; any Not Fixed derives proposed FAILED from persisted complete decisions. This is NOT an operational Rework/Inspection/task/room conclusion. The Project Leader has not accepted the lifecycle/status/schema/notification/projection transitions. `/conclude` remains 503 and production controller/router dispatch remains OFF. Scope percentages below do not mean acceptance.

## FE05 source delta

- `ReinspectionConclusionProposalService.cs` adds prepare/read operations inside the existing partial service, reusing FAST015 ReinspectionWorkflowBatch/WorkflowDetails and BatchAsync. One physical connection and SERIALIZABLE transaction, existing Users-first actor lock, existing SQL session/current-user/INSPECTION_PERFORM branch/assigned-inspector/SOD checks and final gate remain. No transaction framework is copied or changed.
- Exact persisted Inspection/Rework/original task/current cycle/pinned original+assigned version/actor/result set/revisions are reloaded under existing locks. Confirmation, nonempty operation ID, exact selected defect identity and complete persisted Fixed/Not Fixed + actor/time are mandatory. Client fixed values are not conclusion authority.
- One candidate proposal row holds canonical request, exact saved decision snapshot, operation ID, proposed outcome, post-write root/binding/Rework revisions, timestamp and proposal rowversion. The operation/canonical request lives WITH this candidate result; it is not a second N01 ledger, registered issuer or durable public-writer framework. Canonical selected IDs are sorted; original request tags are retained so completed same-operation replay works despite the caller's old input ETag.
- Every replay first repeats real authorization/current-cycle/ownership checks, then compares exact canonical identity, actor/branch/resource and current persisted result/revision snapshot. Same ID/different canonical request or another operation for an already-reviewed source returns 409. Stale source returns 412. No replay trust is inferred from JSON alone. Different-resource operation reuse cannot disclose another result.
- Proposal persistence conditionally invalidates the existing Inspection and binding rowversions, writes the proposal and audit in the same Batch transaction, validates the persisted post-state before commit and returns updated ETag. It does not touch Rework/task/room status or claim proof/notification/projection success. Unknown commit errors retain the existing Batch boundary behavior; client performs no automatic mutation retry.
- New isolated-only `01_ConclusionProposalCandidate.sql` creates one proposal table, exact FK to the existing binding and branch/user FKs, unique operation ID, JSON/check constraints, immutable proposal trigger and result-freeze trigger. No production status enum/CK, generated entity, EF mapping or release SQL changes. Installer uses exact new FAST016 receipt path and server/database/owner guards, first installs the unchanged FAST014 candidate schema on a fresh empty target, then installs the new proposal schema with no overwrite/backfill.
- Reviewed candidate results cannot be edited through service or direct SQL; a trigger failure rolls back the statement including earlier revision-trigger effects, independent of trigger execution order. This is a candidate immutability/reopen restriction, not an accepted production reassignment/reinspection policy. Original Rework defect associations are preserved. NotFixedDefectIds plus source IDs/revisions prepare only a future policy-approved handoff; no next-round defects/Rework command or writer is created.
- Existing `[NonController]` candidate controller now exposes actual service-backed GET/POST `/{inspectionId}/conclusion-proposal` within `/api/branches/{branchId}/reinspection-workflow-candidate`. POST requires If-Match; body: operationId, reworkId, reworkRevision, bindingRevision, selectedDefectIds, confirmed. GET `?reworkId=` returns `{proposal:null}` or the persisted validated proposal. JSON DTOs and HTTPS/session/error conventions remain. Neither method is production-mounted.
- Typed client shares the existing cookie/CSRF transport helper with bind/save; it validates exact identities/version/outcome/revisions and always rejects lifecycleApplied/nextRoundCreationAvailable=true. Review UI loads actual typed proposal transport, requires saved complete decisions and explicit confirmation, retains one operation UUID for a source revision, reloads persisted state, disables reviewed edits, and blocks further saves after stale/revoked-session/forbidden/conflict/unknown outcomes until reload. Unsaved local decisions cannot be confirmed. No shared auth/router/CSRF changes or mock store. Conclude is still disabled.

## UC044 / UC045 / UC046 preservation

UC044 writer/preflight/receipt/replay composition and review UI are unchanged. Exact accepted-binding call sequence and mandatory original-cleaner notification are documented in DECISIONS.md. Current Fe05NotificationKind/Fe05OperationStaging has Failed/ReworkRequested only; no approved Passed event/profile/issuer implementation is available. ClosedInspectionApproveBinding remains closed; no Passed event is invented and no READY projection writer is added.

UC045/046 caller/issuer/N01/R03/R05/prelock/recovery/projection/evidence source is unchanged. Three summary-read acceptedConcreteBinding values remain null; private evidence=false. N01 L1 24/L2 14 remain NOT_RUN. Immutable evidence bytes remain unaccepted; proposal table immutability does not fix this. Successful authoritative Approve/Reject/Rework/Reinspect commit, proof and different-transaction FE04 consumer acceptance remain NOT_RUN.

## Actual verification

| Check | Actual result | Boundary |
|---|---|---|
| Backend solution build | PASS, 0 warnings/errors | Final source |
| Focused SQL harness build | PASS, 0 warnings/errors | Final harness |
| `dotnet test HotelOps.slnx` | Exit 0 | API-only solution; no discovered functional-test credit |
| All Fixed candidate SQL suite | 20/20 PASS | Real service/direct controller + owned SQL; includes successful proposal commit |
| Not Fixed candidate SQL suite | 8/8 PASS | Proposed Failed and future-source IDs only |
| Direct SQL freeze/immutable guards | 3/3 PASS | Existing trial proposal/result statements explicitly rolled back |
| Typed-client/review tests | 29/29 PASS | 12 new + 17 directly affected transport tests; controlled fetch |
| React/browser tests | 9/9 PASS | 4 new Review/Confirm + 5 directly affected workflow cases; HTTP interception |
| Frontend TypeScript/Vite build | PASS | Unmounted candidate source compiled |
| Live HTTPS/cookie/CSRF middleware E2E | NOT_RUN | Controller discovery/production routes OFF |
| Accepted operational lifecycle/proof/staging/projection commit | NOT_RUN | Required business/schema/runtime approvals absent |
| Unrelated historical suites / N01 L1/L2 | NOT_RERUN / NOT_RUN | No authority acceptance credit from candidate fixtures |

SQL checks exercise: unanswered/confirmation/If-Match; wrong branch and account without existing grant; stale root/binding; wrong selected IDs; actual audit participant SQL fault after proposal insertion with rollback of proposal/root/binding/audit; concurrent distinct operations produce one commit + one conflict; identical operation concurrency produces one commit + verified replay; proposed Completed/Failed; separate physical connection reload; repeated same operation with original request ETag; changed canonical revision; reviewed results cannot change; original defect association preservation; true Conclude stays 503 and actual Rework remains PENDING_REINSPECTION; replay rechecks persisted inspector, stale source and missing cycle. Three additional direct-SQL checks prove result freeze and immutable proposal UPDATE/DELETE rejection with rollback. No deadlock-free guarantee is inferred outside the tested service/catalog/source lock path.

Owned SQL target: DESKTOP-JI8U50T\SQLEXPRESS / HotelOps_Canonical_20261110_125803_1ee8826b_Fresh; owner 7351b09a-19ab-42e8-8ae7-a5fc1654ff41. New setup checks instance and exact ownership before mutations; separate-connection tests independently verify server/database/owner. Initially 0 users; synthetic data only. Actual committed SQL sessions/existing grants are used. Test ticket principal comes from the real committed SQL session, but direct controller action invocation is not HTTP middleware/cookie encryption/CSRF acceptance. Synthetic original Failed/Rework/remediation fixtures are not authoritative issuer/proof acceptance. The trial and all older databases are retained; no reset/drop or shared mutation. Test-only audit fault trigger is created on this exact trial and removed after the fault check; it is not part of production schema.

Development failures are retained locally, not hidden: initial harness used a span expression across await and frontend transport helper/method insertion was incomplete; both compile errors were fixed. First browser run found the new review component was imported but not mounted in the candidate screen; the actual FE05 candidate mount was fixed, then all 9 cases passed. No shared router/auth changes were used. Existing npm deprecation warnings remain; Vite reports ignored react-router `use client` directives and Playwright reports NO_COLOR/FORCE_COLOR. Final backend/harness builds have zero warnings.

Sanitized receipts are in receipts/ and VERIFICATION.json; exact new/modified source inventory and SHA256 are in source-manifest.csv. Runtime credentials/private artifacts/TestResults are excluded. Source line endings may be normalized for Git without semantic changes after verification; hash basis is explicitly recorded. No historical tests were rerun unless their actual client/UI boundary changed.

## Delivery / approvals / progress

One concise DECISIONS.md records exact UC047 mappings, version/reassignment policy, UC044 Passed event/recipient/staging, three summary-read contracts and schema/storage prerequisites. None is automatically approved. No task is assigned to another developer.

Estimated implementation progress, not acceptance: FAST016 85%; UC041 70% unchanged; UC044 72% unchanged; UC045 80% unchanged; UC046 80% unchanged; UC047 75% (persisted conclusion proposal + functional review/replay/concurrency/rollback, actual lifecycle still closed).

FE04_DEPENDENCY_STATUS: R03/R05_PINNED_R05_INTEGRATED_VERIFIED_ISOLATED / NOT_READY_FOR_AUTHORITATIVE_HANDOFF. R05 95f7422e284bc81bcfcf9f460dace52022de5cc7 retained. No AUTHORITATIVE_INSPECTION_HANDOFF_READY or UC ACCEPTED emitted. Scoped private commit/push only; no merge/deploy/force-push/shared SQL or public producer/writer activation.
