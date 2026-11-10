# DEV3-NEXT-013 — UC006 deactivation vertical source candidate

UC_STATUS: PARTIAL. Candidate HTTP/frontend source ready for independent review. Not COMPLETE_SOURCE for the whole UC; no COMPLETE_VERIFIED or business approval inferred from full implementation authority.
Base22cff049c343b1e742ec6068dc7f6fb96202f83f; feature/dev3-uc006-deactivation-candidate.

## Autonomous selection

UC010 candidate PR45 is open/mergeable, review pending, so no merge authorization conditions are satisfied. No duplicate UC010 implementation. UC007 approved Add membership is already implemented; Department/primary replacement/remove/transfer are explicitly excluded in UserBranchAssignment/OUTSTANDING_ISSUES.md. Moved to actual UC006 gap: internal deactivation service/strict reason DTO already exist but no HTTP/frontend confirmation flow. Reused these business rules rather than inventing a new authorization/dependency/version protocol.

## Delivered candidate

UserDeactivationCandidateController is NonController with no Program.cs/DI/production-route edit. Proposed technical HTTP shape PATCH /api/users/{userId:long}/deactivate, JSON exactly reason, body limit16KiB, HTTPS/cookie/CSRF metadata, strong quoted uppercase16hex User ETag. Success-only candidate DTO {userId,accountStatus:"INACTIVE"} with new ETag;400 invalid body/token,401 identity,403 self/restricted/authority,404 target,409 state/known responsibilities/LastAdmin,412 stale,428 missing If-Match,503 unresolved/release/unavailable. This shape is a technical source proposal, NOT approved public API or activation.

Existing UserAccountDeactivationRequest parser trims reason and requires at least10 characters, no added1000-character business limit. Unknown/duplicate fields rejected. Controller uses existing UserAccountDeactivationReleaseAcceptance BEFORE calling service, so a direct candidate invocation cannot even reach SQL while gates pending. Existing service is unchanged: own transaction/global-first/User locks, SQL session and approved UserAccountStatusAccess scope, concrete integrated responsibility review, immutable release gate, conditional User update, same-context session revoke+audit and LastAdmin poststate. No new global/branch/OWN rule, resource reader, clear evaluator or release toggle introduced.

Standalone UserDeactivationCandidate uses accepted AuthProvider, profile GET ETag and User detail GET; separate snapshots never authorize mutation. Explicit reason -> Review -> Confirm; synchronous duplicate guard; default dispatch OFF and absent from production import graph. Client snapshots reason, sends only {reason}, validates safe ID/User ETag and16KiB technical payload bound, obtains CSRF before PATCH, checks identity/cancellation before dispatch and after body parse, validates returned target/INACTIVE/fresh ETag before confirming. Unknown outcomes are locked without auto queue/retry/replay.401 offers explicit session recovery;412 requires a fresh flow/read; protected data/reason disappear on identity replacement/unmount. No storage, credentials, raw ProblemDetails or logging.

## Actual verification

- dotnet build HotelOps.slnx: exit0,0warnings/errors.
- dotnet run --project scripts/verification/UserDeactivationCandidate/UserDeactivationCandidate.Verification.csproj:20/20 PASS exit0. Actual MVC exclusion/CSRF metadata, HTTP/cookie/self/restricted/If-Match/reason preflight and existing release gate. Null service fixture proves release-blocked valid action never calls SQL writer; not mocked writer success.
- dotnet run --project scripts/verification/UserAccountDeactivation/UserAccountDeactivation.Verification.csproj WITHOUT ARGUMENTS:12/12 independent checks PASS exit0; runner explicitly returns before SQL in this mode.
- node --test tests/user-deactivation-candidate-api.test.mjs tests/user-deactivation-candidate-browser.test.mjs tests/auth-http.test.mjs tests/auth-state.test.mjs:132/132 PASS exit0 (38 new candidate,94 Auth).
- node --test tests/user-activation-api.test.mjs:96/96 PASS exit0.
- npm.cmd run typecheck/build:exit0; two inherited React Router use-client warnings.
- scoped C# whitespace verify and scoped Prettier:exit0.
- dotnet test HotelOps.slnx --no-build:exit0, no framework cases reported; NOT acceptance evidence.
- Exact8new-file allowlist, whitespace/secret scan before publication. Source SHA below.

No SQL/runtime/server authentication executed. Synthetic200 success verifies client/UI only. Real deactivation success, target-session invalidation, final authorization under races, provider fault/audit rollback, LastAdmin poststate and complete responsibility coverage NOT_RUN. Never interpret empty diagnostics or release preparation as permission to activate.

## Existing gates preserved / next decisions

UC006 immutable release gates remain DEV1_WRITER_COORDINATION, DEV2_INTEGRATED_SQL_ACCEPTANCE, GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE. UserAccountDeactivation/GATE_RESOLUTION_MATRIX.md preserves deferred OperationalTask/InternalRequest/Shift/LineManager contracts and unsupported models. D05 Working-workday blocker is already approved: employee End first, no forced/auto-end; do not reopen that business decision. Model/writer acceptance is still distinct.

Autonomous backlog decisions, not another implementation permission request:
- UC007: PL/business must define Department replacement/remove/transfer semantics; module owners must accept affected open-work/handover writers. Approved Add is preserved.
- UC008/009: canonical protection, aggregate revision acquisition, anti-escalation ceiling and shared-writer policies need signoff. Update remains version-blocked; no grant/catalog/schema edits.
- UC012/016/022/026: final open-work/child/reference-writer coverage and approved disposable SQL race/rollback acceptance remain; no fuse removed. DEV2PR25 handled separately.
- UC138/139/140: management/public HTTP/version/resource authority and handover/notification/transaction acceptance still constrain new writes; existing candidates/read routes reused, Workday writes OFF.
- UC004: approved narrow source already integrated; current end-to-end trial needs named approved disposable lease/identity. No duplicate source PR/credit created.
Full implementation authority does not sign these business/SQL decisions. No new prompt needed for technical edits once their evidence/contracts arrive.

## Preservation

Eight NEW files only; existing service, DTO, release acceptance, auth/session, Assign/Activate, FE02/FE12, DbContext/entities, Program.cs and DEV1/DEV2 writers unchanged. PR45 worktree and root local ZIPs preserved. No merge/rebase/cherry-pick/deploy/SQL/migration. Source-only PR for review, not runtime activation.

## SHA256

3DE1500C3EA0CE9349D763D1D155D593BFEB805B9C10FF4232F23068CECFD622  src/HotelOps.Api/Controllers/UserDeactivationCandidateController.cs
1BC835E60B7E1593711315EB8ED6E105CB8B8AE5614457EF5CA2D7EFE1609F6C  frontend/src/users/userDeactivationCandidateApi.ts
35715D3D8A9452EBF50AD49AF1A2571D38F69C0B97C28567137FE2CBF09AB937  frontend/src/users/UserDeactivationCandidate.tsx
6D4CF527455B519019078F41853763E7D029DFAD0FEE0F3AF053ABB3D02A51F7  frontend/tests/user-deactivation-candidate-api.test.mjs
97FB6DEE73BEAC66E480DC0725D3D91560632CB5A9FCD6FE12B81B2235C78AD2  frontend/tests/user-deactivation-candidate-browser.test.mjs
845B5DA39BF5866EA7A324F6832F5C36D8916A1B7C70FCE2CED2E83DAE6168FF  scripts/verification/UserDeactivationCandidate/Program.cs
720478BD353C02A24462E2FE4F00BD713CC9ABD45A42AE42F904BDF140B13A50  scripts/verification/UserDeactivationCandidate/UserDeactivationCandidate.Verification.csproj
