# DEV3-FASTTRACK-034 — UC007 eligibility and UC005 credential-reset completion

Base integration: 70e1d4ee2c4b63b14543f90c1e315bec39585d80 (fresh fetch verified). Separate branch feature/dev3-uc005-credential-reset. No changes to PR64/68 or other worktrees. UC009 is not reopened.

## UC007 existing implementation and exact blockers

Existing Add remains POST /api/users/{userId}/branches through UserBranchAssignmentController/Service and AddUserBranchFlowBoundary. It adds STANDARD nonprimary membership with User ETag, authority, roles, session checks and atomic audit. No changes here.

PR61 already implemented same-primary-branch Change Department V1: UserDepartmentChangeService + ChangeDepartmentFlow. Do not duplicate it. Its explicit role-less-only policy rejects ANY UserRole history and retains immutable responsibility/writer/SQL gates. The contract is docs/fullstack-integration/DEV3-NEXT-025/CONTRACT_V1.md, authority Issue57 comment6775216478. Source approval does not establish runtime acceptance.

| Requested operation | Concrete missing contract/dependency | Responsible owner |
| --- | --- | --- |
| Remove nonprimary branch | Preserve all active/inactive/expired UserRole paths, define effects on effective grants and target sessions, final GBR028 open-work predicates/writer census and lock participation; do not treat soft RevokedAt as harmless | PL/business/security + DEV3 scope orchestration; DEV1/DEV2 own responsibility predicates/writers |
| Change Primary Branch | Atomic PrimaryBranchId + UserBranchAccess.IsPrimary + DepartmentId relationship; old/new scope/role preservation and session effects; final no-orphan-work locks | PL/business/security + DEV3; module writers remain with owners |
| Transfer Department with roles | Existing CreateUser maps six canonical role families to HK/MT/WH/FO; unique departments_index_4 (BranchId,DepartmentCode) means two Departments in one Branch cannot share that canonical code. Current V1 rejects any role history. New role-envelope eligibility/transfer rule is needed; do not silently rewrite roles or invent department memberships | PL/business/security + DEV3; DEV1/DEV2 open-work/writer coverage |

Schema inspected: User.PrimaryBranchId and DepartmentId are scalar nullable FKs; UserBranchAccess is branch-only (AccessId, UserId, BranchId, IsPrimary, RevokedAt), UserRole contains RoleId/BranchId but no DepartmentId. GBR031 multi-department behavior is not automatically represented by these fields. Issue57 currently contains only the restricted Department V1 instruction plus pending broader decisions. GBR028 clearance cannot be inferred from empty reader results. No new UC007 mutation or 503-only candidate was created. UC007 remains PARTIAL/BLOCKED_CONTRACT, with no new completion credit.

## Selected independent functional gap: UC005 Reset Password alternative

Requirement: SRS UC005 alternative Step3.1 and GBR023/024; accepted contract scripts/verification/AdminReauthentication/RESET_INTEGRATION.md, CredentialResetRelease/SECURITY_REVIEW.md and RELEASE_LIMITATIONS.md. Backend already implements the successful transaction, but integration had no frontend reset adapter, form, navigation or route. Reuse rather than duplicate.

Backend source unchanged:
- Controllers/AdminReauthenticationController.cs: POST /api/auth/reauthenticate, exactly currentPassword; trusted proof retained server-side, expires within five minutes; 401 INVALID_CREDENTIALS differs from SESSION_INVALID.
- Controllers/AdminCredentialResetController.cs: POST /api/users/{userId}/reset-credential, exactly identityVerificationConfirmed=true; HTTPS/cookie, mandatory quoted uppercase 16-hex User If-Match; 200 temporaryCredential + fresh ETag.
- Services/Users/AdminCredentialResetWorkflow.cs and UserCredentialResetAccess.cs: server actor/session/proof and reset resource authority, self/OWN denial; no authority from profile GET.
- Services/Authentication/TemporaryCredentialService.cs: conditional credential update, target session revocation, audit and durable-administrator preservation inside existing owned transaction, final authorization gate. Existing server proof/collation/SQL semantics unchanged.

New actual flow: account detail link -> /users/:id/reset-credential -> current admin password -> CSRF + POST reauthentication -> GET target profile+ETag -> identity attestation + review -> explicit Confirm -> CSRF + one reset POST -> validate 200/new ETag/credential -> hidden one-time credential with reveal/copy/manual handoff and acknowledge-clear. Old password, token/proof, actor, roles, scope or expiry are never sent in reset body. No frontend password generation.

Synchronous exclusion covers reauthentication/reset. Unknown reset (network, 5xx or malformed success) permanently consumes the mounted flow, including target/session replacement; no queue/replay/retry. Known stale/rejected outcomes require new password verification and new profile review. Reauth wrong-password is an operation denial; verified 401 session failure hides protected data and requires explicit session recovery. Target change cannot clear that recovery gate. Session/target remount aborts old work and drops late secrets. Admin password field is cleared at submission. Secret state stays in mounted leaf; acknowledge, session/target replacement and navigation remove access. Clipboard retention and JavaScript zeroization cannot be guaranteed. No logging, storage or secret-bearing URLs.

New route and link are source integration with already active backend, not a deployment or permission grant. No controller/security/DI/schema changes or newly activated server writer. No shared SQL, runtime calls, migrations or production configuration changes. Workday and other release gates remain unchanged.

## Exact file allowlist

Modified:
- frontend/src/auth/AuthRouteHost.tsx (one import + additive route)
- frontend/src/users/UserAccessReviewPage.tsx (link after successful account detail)
New:
- frontend/src/users/credentialResetApi.ts
- frontend/src/users/CredentialResetRoute.tsx
- frontend/tests/credential-reset-api.test.mjs
- frontend/tests/credential-reset-browser.test.mjs
- docs/fullstack-integration/DEV3-FASTTRACK-034/HANDOFF.md
- docs/fullstack-integration/DEV3-FASTTRACK-034/MANIFEST.json

No existing business source other than the two small router/navigation changes is modified. UC007 Add/Department, backend, FE02/FE12, DEV1/DEV2 sources are untouched.

## Verification and limitations

Backend build: dotnet build HotelOps.slnx PASS, 0 warnings/errors. Initial sandbox attempt could not read local NuGet.Config; rerun with authorized build permissions succeeded. No database connection was used. Existing reset verification harness is SQL-backed and was NOT_RUN; historical accepted receipts are not new independent SQL evidence.

Frontend regression command (exit0): node --test tests/auth-router-integration.test.mjs tests/auth-http.test.mjs tests/auth-state.test.mjs tests/user-profile-api.test.mjs tests/update-user-router-integration.test.mjs tests/users-router-integration.test.mjs tests/create-user-router-integration.test.mjs tests/add-user-branch-router-integration.test.mjs -> 459/459 PASS, no failed/cancelled/skipped. New reset API/browser receipt recorded in MANIFEST.json. Offline browser runs use real AuthApi/AuthStore/router and synthetic HTTP responses, not live SQL/security acceptance.

Initial browser fixture omitted required session csrfToken, producing pre-form locator timeouts. Corrected fixture (not application expectation); full new suite rerun. Development checkpoints 53/53 then55/55; final includes wrong-password/session distinction. No failed run is concealed. Typecheck/build and scoped formatting/allowlist/secret/diff checks are publication gates. Two inherited React Router use-client bundler warnings remain.

Live HTTPS/SQL, physical concurrency, audit/session rollback, real reauthentication/ETag and credential-response delivery acceptance: NOT_RUN. These require approved disposable identity/GUID/lease/fixtures and trusted TLS. Do not use primary/shared database or old hardcoded SQL runner without an approved isolated lane. No whole-UC completion credit claimed.

Next: review this functional UC005 integration PR and run authorized targeted runtime acceptance. UC007 broader operations need the exact table decisions and writer participation before any successful source mutation is built. Keep release gates; do not create duplicate candidates.
