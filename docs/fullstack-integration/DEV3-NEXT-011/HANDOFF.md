# DEV3-NEXT-011 — UC004 credential handoff completion

Baseline 0bb7cef5a599a1c1cd0a5c66bcb247f4fac6745e; branch feature/dev3-uc004-completion.
Status: IMPLEMENTED_SOURCE_ONLY / independent review pending. Whole UC004 remains PARTIAL; no new ledger credit or runtime acceptance.

## Source readiness and actual gap fixed

The approved six-role, one-branch, one-department Create User operation already has active POST /api/users, strict request parsing/normalization, dual global USER_MANAGE and ROLE_MANAGE, active role/department/branch relationship checks, SQL session/permission gates before commit, serialized last-admin protection, atomic user/membership/role/audit/temporary-credential staging, and post-commit one-time initial credential release.

Source: Controllers/UserAccountCreationController.cs; DTOs/Users/CreateEmployeeAccountRequest.cs; Services/Users/UserAccountCreationService.cs (approved Task30 six-role map); Program.cs ApiCsrfMiddleware registration; Middleware/ApiCsrfMiddleware.cs. Existing frontend useCreateUser/CreateUserFlowBoundary/InitialCredentialHandoff handles double-submit, stale identity, abort, 401 recovery, hidden/reveal/copy/acknowledge and per-flow unknown-outcome lock. These sources are preserved.

Missing success correlation: createUserApi formerly accepted any structurally valid 201 account DTO with matching Location, even when its username, branch, department or initial role did not match the request. A misplaced response could therefore reach credential handoff. The decoder now compares all eight fields to the normalized immutable request snapshot, which matches backend response construction. IDs/account status/expiry/Location validation remains. Mismatch produces OUTCOME_UNKNOWN/INVALID_RESPONSE/status201, no credential/error-body leakage and no automatic POST replay. UI already locks unknown flows; new real-router tests prove handoff/reveal/copy is absent. No new authorization, grant, scope, HTTP endpoint or API contract invented.

Test fixture correction: the existing default API success fixture now echoes the actual normalized POST fields as backend does, instead of returning fixed HK_STAFF/null-phone data for every selected role/phone. This preserves real success coverage for all six roles. Existing assertions remain; thirteen new regressions cover eight field mismatches, pre-await snapshot correlation and four browser handoff/unknown-lock cases.

## Executed receipts

- dotnet build HotelOps.slnx: exit0; 0 warnings/errors. Backend source unchanged.
- node --test tests/create-user-api.test.mjs tests/create-user-router-integration.test.mjs: 171/171 PASS, exit0. Covers success, six roles, business errors, validation, CSRF, 401/restricted recovery, duplicate UI submission, session replacement, unmount, credential lifetime and new mismatch cases.
- node --test tests/auth-http.test.mjs tests/auth-state.test.mjs: 94/94 PASS, exit0.
- npm.cmd run typecheck: exit0.
- npm.cmd run build: exit0; two inherited React Router use-client warnings.
- Scoped Prettier on three changed frontend files: exit0.
- Exact four-file allowlist, whitespace and secret-pattern scan before publication.
No shared SQL, live authentication/POST, migration or deployment. No C# business source edited; no backend SQL verification project executed. UserAccountCreation/Program.cs requires an owned SQL trial and is intentionally NOT_RUN.

## Historical evidence versus current acceptance

scripts/verification/UserAccountCreationRelease/VERIFICATION_RESULTS.md reports Task31 HTTP/SQL combined 546 checks (includes inherited assertions, not 546 create-only cases) and participant26. This is historical reused evidence, not rerun or independent acceptance for this PR. OUTSTANDING_ISSUES.md states no business gate remains open for the approved six-role/single-branch/single-department slice, while broad provisioning is out of scope. Lost credential or uncertain delivery requires an approved reset process; no plaintext replay or reset endpoint added.
Current environment live acceptance remains NOT_RUN without approved disposable lease/identity/receipts. Need current binary/schema/startup prerequisites, HTTPS/cookie/CSRF and authorized success/denial/duplicate/postcommit handoff/atomicity acceptance in that trial. Multi-role/global/multi-branch alternatives need their own approved contract; not silently added from historical SRS.

## UC016 decisions (fail-closed preserved)

- PL/business + operational-task owner must settle whether COMPLETED tasks are terminal for verification/rework; current StructureDeactivationDependencies blocks COMPLETED with TASK_COMPLETION_CONTRACT_UNRESOLVED. It blocks active child areas/rooms rather than cascading; broader child/history semantics require explicit decision.
- DEV3 owns deactivation orchestration; module owners supply coordinated reference writers. Integration and acceptance of User-first/Branch locks, retained ancestry checks and final session/resource rereads is required; PR25 is separate and not merged here.
- DEV3 must implement atomic deactivation/audit and prove rollback once release conditions are approved; do not remove writer-coordination guard. Notification/outbox is pending only if business specifies a notification requirement; none inferred from unrelated UCs.
- PL/named trial owner must approve exact disposable server/database ownership, prerequisites, script scope and lease; then test placement-vs-deactivation races, stale ETag, authorization changes, rollback and audit atomicity. No current SQL lease assumed.

## Preservation

Only createUserApi.ts and its two focused test files plus this handoff changed. Backend, public auth/CSRF contracts, roles allowlist, Create User UI/store, UC007/010, FE02/FE12 and DEV1/DEV2 writers unchanged. Workday mutations OFF, WorkdaysController NonController, UC008 Update version-blocked. PR39 untouched. No merge/deploy.

## Source SHA256

C014909D95BBD9482B61F6E45882689022039D2D6581017852786134F1C92F0A  frontend/src/users/createUserApi.ts
B5B78B7D1FFA02AD7D98B75031E8AC17C5261A103916B539E5FF6A221D5DB318  frontend/tests/create-user-api.test.mjs
462662DA1FADAE46D937633465A6992D7F5A54BBDF9ADA7E8D492192326CBBB4  frontend/tests/create-user-router-integration.test.mjs
